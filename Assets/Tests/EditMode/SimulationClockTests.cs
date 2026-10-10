using NUnit.Framework;

namespace HotPatata.Tests
{
    /// <summary>
    /// The client side of <see cref="SimulationClock"/> (docs/netcode-deterministic-plan.md §2.1): it follows the network
    /// time, slews corrections in instead of jumping, never runs backwards, and snaps only on a real desync.
    /// </summary>
    public class SimulationClockTests
    {
        const double Dt = 1.0 / 60.0;
        const float MaxSlew = 0.1f, Snap = 0.5f;

        [Test]
        public void InStep_ItAdvancesWithTheFrame()
        {
            double t = SimulationClock.Step(10.0, 10.0 + Dt, Dt, MaxSlew, Snap);
            Assert.AreEqual(10.0 + Dt, t, 1e-9);
        }

        [Test]
        public void ABackwardCorrection_IsSlewed_AndNeverRunsBackwards()
        {
            double clock = 10.0, raw = 10.0 - 0.2;   // NGO hard-reset its estimate 200 ms back
            for (int i = 0; i < 600; i++)
            {
                raw += Dt;
                double next = SimulationClock.Step(clock, raw, Dt, MaxSlew, Snap);
                Assert.Greater(next, clock, "the level never runs backwards");
                Assert.GreaterOrEqual(next - clock, Dt * (1 - MaxSlew) - 1e-9, "at most 10% slower");
                clock = next;
            }
            Assert.AreEqual(raw, clock, 1e-6, "caught up after a while");
        }

        [Test]
        public void AForwardCorrection_IsSlewed_AtMostTheSlewRate()
        {
            double next = SimulationClock.Step(10.0, 10.0 + Dt + 0.2, Dt, MaxSlew, Snap);
            Assert.AreEqual(10.0 + Dt * (1 + MaxSlew), next, 1e-9);
        }

        [Test]
        public void ARealDesync_Snaps()
        {
            Assert.AreEqual(12.0, SimulationClock.Step(10.0, 12.0, Dt, MaxSlew, Snap), 1e-9);
            Assert.AreEqual(9.0, SimulationClock.Step(10.0, 9.0, Dt, MaxSlew, Snap), 1e-9);
        }

        [Test]
        public void Offline_ItIsTheLocalClock()
        {
            Assert.IsFalse(NetMode.IsNetworked);
            Assert.AreEqual(NetMode.ServerTime, SimulationClock.ServerNow);
            Assert.AreEqual((float)NetMode.ServerTime, SectionClock.Now, 1e-3f, "no RunManager: the local time, as before");
        }
    }
}
