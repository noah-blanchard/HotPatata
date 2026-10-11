using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>
    /// Riding carriers (#97): the velocity of a point on a carrier, what a player leaving one keeps (PROJECT_SPEC §11), and the
    /// aim assist solving a pass in the frame of the lift both players ride (PROJECT_SPEC §8.2).
    /// </summary>
    public class CarrierMotionTests
    {
        const float Dt = 1f / 60f;

        [Test]
        public void VelocityAt_ASlidingCarrier_IsItsDeltaOverTime()
        {
            Vector3 v = CarrierMotion.VelocityAt(new Vector3(3f, 1f, 2f), new Vector3(0f, 0.15f, 0f), Vector3.zero, Quaternion.identity, Dt);
            Assert.Less((v - new Vector3(0f, 9f, 0f)).magnitude, 1e-3f, "a lift rising 0.15 m in a 60th of a second goes 9 m/s");
        }

        [Test]
        public void VelocityAt_ATurningCarrier_GrowsWithTheDistanceFromItsAnchor()
        {
            var turn = Quaternion.Euler(0f, 90f * Dt, 0f);   // 90°/s about the anchor
            Vector3 near = CarrierMotion.VelocityAt(new Vector3(1f, 0f, 0f), Vector3.zero, Vector3.zero, turn, Dt);
            Vector3 far = CarrierMotion.VelocityAt(new Vector3(4f, 0f, 0f), Vector3.zero, Vector3.zero, turn, Dt);
            Assert.AreEqual(Mathf.PI / 2f, near.magnitude, 0.01f, "1 m from the pivot at 90°/s");
            Assert.AreEqual(4f * Mathf.PI / 2f, far.magnitude, 0.04f);
            Assert.Less(Mathf.Abs(near.y), 1e-4f, "a flat turn never lifts");
        }

        [Test]
        public void VelocityAt_NoTime_IsZero()
        {
            Assert.AreEqual(Vector3.zero, CarrierMotion.VelocityAt(Vector3.one, Vector3.up, Vector3.zero, Quaternion.identity, 0f));
        }

        [Test]
        public void LeavingACarrier_KeepsAllOfItsVelocity_UpToTheCap()
        {
            var lift = new Vector3(0f, 9f, 0f);
            Assert.AreEqual(lift, PlayerMotor.KeptCarrierVelocity(lift, 12f), "a lift at 9 m/s: all of it");
            var fast = new Vector3(20f, 0f, 0f);
            Assert.AreEqual(12f, PlayerMotor.KeptCarrierVelocity(fast, 12f).magnitude, 1e-4f, "a 20 m/s shuttle: capped");
            Assert.AreEqual(Vector3.zero, PlayerMotor.KeptCarrierVelocity(fast, 0f));
        }

        [Test]
        public void AimAssist_OnTheSameLift_SolvesAsOnStillGround()
        {
            var t = ScriptableObject.CreateInstance<GameTuning>();
            try
            {
                var lift = new Vector3(0f, 9f, 0f);
                var center = new Vector3(0.4f, 0f, 3f);
                Vector3 raw = (center + Vector3.up * 0.4f).normalized;
                var still = AimAssist.Correct(t, Vector3.zero, raw, 14f, Vector3.zero, center, Vector3.zero, 0f, 3f);
                // Both on the lift: the launch keeps the lift's velocity, the receiver moves with it.
                var riding = AimAssist.Correct(t, Vector3.zero, raw, 14f, lift, center, lift, 0f, 3f, lift);
                Assert.Less(Vector3.Angle(still.Direction, riding.Direction), 0.01f);
                Assert.AreEqual(still.Reachable, riding.Reachable);
            }
            finally
            {
                Object.DestroyImmediate(t);
            }
        }
    }
}
