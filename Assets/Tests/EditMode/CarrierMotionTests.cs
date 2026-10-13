using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>
    /// Riding carriers (#97): the velocity of a point on a carrier, what a player leaving one keeps (PROJECT_SPEC §11), when a
    /// rising deck puts a player it came up into on top of it, and the aim assist solving a pass in the frame of the lift both
    /// players ride (PROJECT_SPEC §8.2).
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
        public void LeavingACarrier_AJumpKeepsItsClimb_NeverItsDescent()
        {
            var rising = new Vector3(0f, 9f, 0f);
            Assert.AreEqual(rising, PlayerMotor.KeptCarrierVelocity(rising, 12f, 0f, true), "a jump off a lift rising at 9 m/s keeps all of it");
            Assert.AreEqual(Vector3.zero, PlayerMotor.KeptCarrierVelocity(-rising, 12f, 0f, true), "a jump off a descending lift is a full jump");
            Assert.AreEqual(Vector3.zero, PlayerMotor.KeptCarrierVelocity(rising, 12f, 0f, false), "walking off keeps no climb");
            Assert.AreEqual(12f, PlayerMotor.KeptCarrierVelocity(new Vector3(0f, 20f, 0f), 12f, 0f, true).magnitude, 1e-4f, "capped");
        }

        [Test]
        public void LeavingACarrier_KeepsOnlyItsHorizontalShare()
        {
            var shuttle = new Vector3(6f, 0f, 8f);
            Assert.AreEqual(Vector3.zero, PlayerMotor.KeptCarrierVelocity(shuttle, 12f, 0f, true), "by default the deck never drags a jumper along");
            Assert.Less((PlayerMotor.KeptCarrierVelocity(shuttle, 12f, 0.5f, false) - new Vector3(3f, 0f, 4f)).magnitude, 1e-4f);
            Assert.AreEqual(12f, PlayerMotor.KeptCarrierVelocity(shuttle * 2f, 12f, 1f, false).magnitude, 1e-4f, "a 20 m/s shuttle: capped");
            Assert.AreEqual(Vector3.zero, PlayerMotor.KeptCarrierVelocity(shuttle, 0f, 1f, true));
        }

        // A 6 x 0.5 x 6 deck centred at the origin: its top is at y = 0.25.
        static readonly Vector3 Half = new Vector3(3f, 0.25f, 3f);
        const float Radius = 0.35f, Skin = 0.05f;

        static float Lift(Vector3 feet, float rise, Quaternion? rotation = null) =>
            CarrierMotion.DeckLift(feet, Radius, Vector3.zero, rotation ?? Quaternion.identity, Half, rise, Skin);

        [Test]
        public void DeckLift_ADeckRisingIntoTheFeet_PutsThePlayerOnTop()
        {
            Assert.AreEqual(0.25f + Skin - 0.1f, Lift(new Vector3(1f, 0.1f, -2f), 0.15f), 1e-4f, "rose 15 cm past feet 15 cm under its top");
            Assert.AreEqual(0.25f + Skin - 0.05f, Lift(new Vector3(3.1f, 0.05f, 0f), 0.15f), 1e-4f, "feet overhanging the edge a little are caught");
            Assert.Greater(Lift(new Vector3(0f, 0.1f, 0f), 0.15f, Quaternion.Euler(0f, 30f, 0f)), 0f, "a deck turned about up is still a deck");
        }

        [Test]
        public void DeckLift_LeavesAloneWhoeverItDidNotRiseInto()
        {
            Assert.AreEqual(0f, Lift(new Vector3(0f, 0.25f + Skin, 0f), 0.15f), "standing on it already (carried with it)");
            Assert.AreEqual(0f, Lift(new Vector3(0f, 0.245f, 0f), 0.15f), "within the touch tolerance");
            Assert.AreEqual(0f, Lift(new Vector3(0f, 0.6f, 0f), 0.15f), "above it");
            Assert.AreEqual(0f, Lift(new Vector3(0f, -2.2f, 0f), 0.15f), "under it: it rises away");
            Assert.AreEqual(0f, Lift(new Vector3(0f, -0.3f, 0f), 0.15f), "deeper in it than it rose: not this frame's doing");
            Assert.AreEqual(0f, Lift(new Vector3(3.5f, 0.1f, 0f), 0.15f), "beside it");
            Assert.AreEqual(0f, Lift(new Vector3(0f, 0.1f, 0f), 0f), "it did not rise");
            Assert.AreEqual(0f, Lift(new Vector3(0f, 0.1f, 0f), 0.15f, Quaternion.Euler(60f, 0f, 0f)), "a wall or a steep ramp is no deck");
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
