using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>
    /// The pure parts of the online-feel netcode (#117): flight segments, the back-date clamp, moving a chord into a mover's
    /// present frame, the stamp history the host checks claims against, the waits for late messages, and the measurements.
    /// </summary>
    public class NetcodeFlightTests
    {
        const float G = 14.7f;

        [Test]
        public void ASegment_IsTheBallisticArc_FromItsStartTime()
        {
            var s = new FlightSegment(100.0, new Vector3(1f, 2f, 3f), new Vector3(10f, 5f, 0f), 7);
            Assert.IsTrue(s.Valid);
            Assert.AreEqual(s.Origin, s.PositionAt(100.0, G), "at t0 it is at the origin");
            Assert.AreEqual(s.Origin, s.PositionAt(99.0, G), "never before its start");
            Vector3 expected = ThrowBallistics.PositionAt(s.Origin, s.Velocity, G, 0.4f);
            Assert.That(Vector3.Distance(expected, s.PositionAt(100.4, G)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(ThrowBallistics.VelocityAt(s.Velocity, G, 0.4f), s.VelocityAt(100.4, G)), Is.LessThan(1e-4f));
        }

        [Test]
        public void TwoMachines_DrawTheSameSegment_AtTheSameServerTime()
        {
            // Large server times keep their precision: the subtraction is done in double.
            var s = new FlightSegment(86400.0 * 3 + 0.123, Vector3.zero, new Vector3(0f, 10f, 20f), 1);
            Vector3 a = s.PositionAt(s.T0 + 0.25, G);
            Vector3 b = s.PositionAt(s.T0 + 0.25, G);
            Assert.AreEqual(a, b);
            Assert.AreEqual(5f, a.z, 1e-3f);
        }

        [Test]
        public void ABackDatedThrow_IsClampedToTheCap_AndNeverInTheFuture()
        {
            Assert.AreEqual(9.9, HazardRewind.ClampClaim(9.9, 10.0, 0.3f), 1e-9);
            Assert.AreEqual(9.7, HazardRewind.ClampClaim(9.0, 10.0, 0.3f), 1e-6, "older than the cap: started at the cap");
            Assert.AreEqual(10.0, HazardRewind.ClampClaim(10.5, 10.0, 0.3f), 1e-9, "a future release starts now");
        }

        [Test]
        public void AChordInThePast_IsCarriedIntoTheMoversPresentFrame()
        {
            // A lift that rose 2 m since then: a point 1 m above its deck then is 1 m above its deck now.
            Vector3 then = new Vector3(0f, 5f, 0f), now = new Vector3(0f, 7f, 0f);
            Vector3 p = BombFlightSweep.ToNow(new Vector3(0.5f, 6f, 0f), then, Quaternion.identity, now, Quaternion.identity);
            Assert.That(Vector3.Distance(new Vector3(0.5f, 8f, 0f), p), Is.LessThan(1e-5f));

            // A bar that turned 90° about Y since then.
            Vector3 q = BombFlightSweep.ToNow(new Vector3(2f, 0f, 0f), Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
            Assert.That(Vector3.Distance(new Vector3(0f, 0f, -2f), q), Is.LessThan(1e-4f));
        }

        static NetworkPlayer.PlayerStamp Stamp(int seq, double time, float x, byte epoch = 0) =>
            new NetworkPlayer.PlayerStamp { Sequence = seq, Time = time, Position = new Vector3(x, 0f, 0f), Velocity = new Vector3(10f, 0f, 0f), Epoch = epoch };

        [Test]
        public void TheStampHistory_InterpolatesTheOwnersFeet()
        {
            var h = new StampHistory(8);
            for (int i = 0; i < 5; i++) h.Add(Stamp(i + 1, 1.0 + i * 0.1, i));
            Assert.IsTrue(h.TryFeetAt(1.25, 0.1f, out Vector3 feet));
            Assert.AreEqual(2.5f, feet.x, 1e-4f);
            Assert.IsTrue(h.TryFeetAt(1.45, 0.1f, out feet), "a little past the newest: carried by its velocity");
            Assert.AreEqual(4.5f, feet.x, 1e-4f);
            Assert.IsFalse(h.TryFeetAt(1.8, 0.1f, out _), "too far past the newest");
            Assert.IsFalse(h.TryFeetAt(0.5, 0.1f, out _), "older than the history");
        }

        [Test]
        public void TheStampHistory_NeverInterpolatesAcrossATeleport()
        {
            var h = new StampHistory(8);
            h.Add(Stamp(1, 1.0, 0f));
            h.Add(Stamp(2, 1.1, 50f, epoch: 1));
            Assert.IsTrue(h.TryFeetAt(1.05, 0.1f, out Vector3 feet));
            Assert.AreEqual(50f, feet.x, 1e-4f);
        }

        [Test]
        public void TheStampHistory_IgnoresOlderStamps_AndWrapsAround()
        {
            var h = new StampHistory(4);
            for (int i = 0; i < 10; i++) h.Add(Stamp(i + 1, i * 0.1, i));
            h.Add(Stamp(3, 0.2, 99f));
            Assert.AreEqual(4, h.Count);
            Assert.AreEqual(9f, h.Newest.Position.x);
            Assert.AreEqual(6f, h.At(0).Position.x);
        }

        [Test]
        public void RollingSamples_GivesPercentilesOfTheLastValues()
        {
            var r = new RollingSamples(10);
            for (int i = 1; i <= 20; i++) r.Add(i);
            Assert.AreEqual(10, r.Count);
            Assert.AreEqual(11.0, r.Min);
            Assert.AreEqual(20.0, r.Max);
            Assert.AreEqual(15.0, r.Percentile(0.5));
            Assert.AreEqual(20.0, r.Percentile(0.95));
        }

        [Test]
        public void TheViewDelay_CoversOnlyTheJitterBeyondNgosBuffer()
        {
            Assert.AreEqual(0f, LagBudget.ViewDelay(0.03f, 0.15f));
            Assert.AreEqual(0.05f, LagBudget.ViewDelay(0.1f, 0.15f), 1e-6f);
            Assert.AreEqual(0.15f, LagBudget.ViewDelay(0.5f, 0.15f), 1e-6f, "capped");
        }

        [Test]
        public void SnapshotStreams_CountLostAndRecoveredSnapshots()
        {
            NetQuality.Clear();
            const int Slot = 2;
            Assert.IsTrue(NetQuality.NoteSnapshot(Slot, 1, false));
            Assert.IsTrue(NetQuality.NoteSnapshot(Slot, 2, false));
            // Packet 3 lost; packet 4 carries 2, 3, 4.
            Assert.IsFalse(NetQuality.NoteSnapshot(Slot, 2, true), "already seen");
            Assert.IsTrue(NetQuality.NoteSnapshot(Slot, 3, true), "recovered from redundancy");
            Assert.IsTrue(NetQuality.NoteSnapshot(Slot, 4, false));
            // Packets 5..8 lost; 9 carries 7, 8, 9: 5 and 6 are gone for good.
            Assert.IsTrue(NetQuality.NoteSnapshot(Slot, 7, true));
            Assert.IsTrue(NetQuality.NoteSnapshot(Slot, 8, true));
            Assert.IsTrue(NetQuality.NoteSnapshot(Slot, 9, false));
            var s = NetQuality.StreamOf(Slot);
            Assert.AreEqual(2, s.Lost);
            Assert.AreEqual(3, s.Recovered);
            Assert.AreEqual(7, s.Received);
            NetQuality.Clear();
        }

        [Test]
        public void NetworkPresets_AreKnown()
        {
            Assert.IsTrue(NetSimSettings.TryPreset("bad", out var bad));
            Assert.AreEqual(100, bad.DelayMs);
            Assert.AreEqual(2, bad.LossPercent);
            Assert.IsTrue(NetSimSettings.TryPreset("Spiky", out var spiky));
            Assert.Greater(spiky.SpikeEverySeconds, 0f);
            Assert.IsFalse(NetSimSettings.TryPreset("nope", out _));
            Assert.AreEqual(20, new NetSimSettings { DelayMs = 100, JitterMs = -1 }.EffectiveJitterMs, "default jitter: delay / 5");
        }
    }
}
