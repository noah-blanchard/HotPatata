using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>M9 movement: sprint, carving turns, momentum slide, slide-jump, crouch under ceilings, mantle.</summary>
    public class SprintSlideTests : SandboxTestBase
    {
        static readonly Vector3 LaneStart = new Vector3(-15f, 0.05f, -17f);   // west edge of the floor, clear to +Z

        [UnityTest]
        public IEnumerator Sprint_ReachesSprintSpeed_AndLettingGoCarriesMomentum()
        {
            yield return Place(p1, LaneStart);
            Drive.Move = Vector2.up;
            Drive.Sprint = true;
            yield return WaitSeconds(0.9f);
            Assert.AreEqual(tuning.sprintSpeed, HorizontalSpeed(p1), 0.1f, "holding sprint reaches sprint speed");
            Assert.IsTrue(p1.Motor.IsSprinting);

            Drive.Sprint = false;
            yield return WaitSeconds(0.1f);
            Assert.Greater(HorizontalSpeed(p1), tuning.moveSpeed + 1f, "letting go of sprint bleeds speed off, it does not snap");
            yield return WaitSeconds(0.6f);
            Assert.AreEqual(tuning.moveSpeed, HorizontalSpeed(p1), 0.1f, "then settles at run speed");
        }

        [UnityTest]
        public IEnumerator Sprint_OnlyWhenMovingMostlyForward()
        {
            yield return Place(p1, LaneStart);
            Drive.Move = Vector2.right;
            Drive.Sprint = true;
            yield return WaitSeconds(0.7f);
            Assert.IsFalse(p1.Motor.IsSprinting, "no sideways sprint");
            Assert.AreEqual(tuning.moveSpeed, HorizontalSpeed(p1), 0.1f);
        }

        [UnityTest]
        public IEnumerator Turning_CarvesInsteadOfSnapping()
        {
            yield return Place(p1, LaneStart);
            Drive.Move = Vector2.up;
            yield return WaitSeconds(0.6f);

            Drive.Move = Vector2.right;   // a 90° change of direction
            yield return WaitSeconds(0.04f);
            Assert.Greater(Vector3.Angle(Flat(p1.Motor.Velocity), Vector3.right), 30f, "the turn takes a moment: it carves");
            Assert.Greater(HorizontalSpeed(p1), tuning.moveSpeed * 0.9f, "and keeps the speed through the turn");

            yield return WaitSeconds(0.3f);
            Assert.Less(Vector3.Angle(Flat(p1.Motor.Velocity), Vector3.right), 3f, "but completes quickly");
        }

        [UnityTest]
        public IEnumerator Slide_BoostsThenDecays_AndLowersTheBody()
        {
            yield return SprintAlongLane(0.8f);
            Drive.Crouch = true;
            yield return null;
            yield return null;

            Assert.IsTrue(p1.Motor.IsSliding, "crouch at speed slides");
            Assert.AreEqual(tuning.sprintSpeed + tuning.slideBoost, HorizontalSpeed(p1), 0.4f, "with a boost");
            yield return WaitSeconds(0.25f);
            Assert.Less(p1.Motor.HeightScale, 0.7f, "the body (eye, catch sphere) goes low");
            Assert.Less(p1.CatchVolume.CatchCenter.y - p1.transform.position.y, tuning.catchCenterHeight * 0.75f);

            yield return WaitUntil(() => !p1.Motor.IsSliding, 3f, "the slide never ended");
            Assert.Less(HorizontalSpeed(p1), tuning.slideExitSpeed + 0.5f, "it ends once the speed has decayed");
            Assert.IsTrue(p1.Motor.Crouched, "still holding crouch: crouch-walk");

            Drive.Crouch = false;
            yield return WaitSeconds(0.3f);
            Assert.IsFalse(p1.Motor.Crouched, "letting go stands up");
            Assert.AreEqual(1f, p1.Motor.HeightScale, 0.01f);
        }

        [UnityTest]
        public IEnumerator SlideBoost_HasACooldown()
        {
            yield return SprintAlongLane(0.8f);
            Drive.Crouch = true;
            yield return null;
            yield return null;
            float boosted = HorizontalSpeed(p1);
            Drive.Crouch = false;
            yield return WaitSeconds(0.1f);

            float before = HorizontalSpeed(p1);
            Drive.Crouch = true;
            yield return null;
            yield return null;
            Assert.IsTrue(p1.Motor.IsSliding);
            Assert.LessOrEqual(HorizontalSpeed(p1), before + 0.05f, "a second slide right away gets no boost");
            Assert.Less(HorizontalSpeed(p1), boosted);
        }

        [UnityTest]
        public IEnumerator SlideJump_KeepsTheSlideSpeed()
        {
            yield return SprintAlongLane(0.8f);
            Drive.Crouch = true;
            yield return null;
            yield return null;
            Drive.PressJump();
            yield return WaitUntil(() => !p1.Motor.Grounded, 1f, "did not jump out of the slide");
            yield return WaitSeconds(0.2f);
            Assert.Greater(HorizontalSpeed(p1), tuning.sprintSpeed + 1f, "the jump carries the slide's momentum");
        }

        [UnityTest]
        public IEnumerator LowCeiling_KeepsThePlayerCrouched_UntilClear()
        {
            // A 3 m long bar 1.3 m up, 4 m ahead: too low to stand under, fine to slide under.
            Box(new Vector3(LaneStart.x, 1.3f + 0.25f, LaneStart.z + 8f), new Vector3(3f, 0.5f, 3f));
            yield return SprintAlongLane(0.3f);
            Drive.Crouch = true;
            yield return WaitUntil(() => p1.transform.position.z > LaneStart.z + 7.5f, 2f, "never reached the bar");

            Drive.Crouch = false;   // let go right under it
            yield return WaitSeconds(0.05f);
            Assert.IsTrue(p1.Motor.Crouched, "cannot stand up into the ceiling");

            Drive.Move = Vector2.up;
            yield return WaitUntil(() => !p1.Motor.Crouched, 3f, "never stood up after leaving the ceiling");
            Assert.Greater(p1.transform.position.z, LaneStart.z + 9.3f, "stood only once clear of it");
        }

        [UnityTest]
        public IEnumerator Mantle_ClimbsAChestHighLedge()
        {
            // Ledge top at 1.5 m; the player falls past it with feet at ~0.6 m, pushing forward.
            Box(new Vector3(LaneStart.x, 0.75f, LaneStart.z + 2.0f), new Vector3(3f, 1.5f, 3f));
            p1.TeleportTo(new Vector3(LaneStart.x, 0.6f, LaneStart.z), Quaternion.identity);
            Drive.Move = Vector2.up;

            yield return WaitUntil(() => p1.Motor.State == MoveState.Mantle, 1f, "no mantle");
            yield return WaitUntil(() => p1.Motor.Grounded, 1f, "did not land after the mantle");
            Assert.AreEqual(1.5f, p1.transform.position.y, 0.1f, "standing on top of the ledge");
        }

        [UnityTest]
        public IEnumerator Mantle_DoesNotClimbATallWall()
        {
            Box(new Vector3(LaneStart.x, 1.5f, LaneStart.z + 1.2f), new Vector3(3f, 3f, 1.4f));
            p1.TeleportTo(new Vector3(LaneStart.x, 0.6f, LaneStart.z), Quaternion.identity);
            Drive.Move = Vector2.up;

            float end = Time.time + 0.8f;
            while (Time.time < end)
            {
                Assert.AreNotEqual(MoveState.Mantle, p1.Motor.State, "a 2.4 m wall is not a ledge");
                yield return null;
            }
            Assert.Less(p1.transform.position.y, 0.2f, "fell back to the floor");
        }

        [Test]
        public void ThrowInheritance_IsCapped_SoSlidesDoNotMakePassesHuge()
        {
            var t = ScriptableObject.CreateInstance<GameTuning>();
            try
            {
                Vector3 kept = PlayerThrower.InheritedVelocity(t, Vector3.forward * 18f, Vector3.forward);
                Assert.AreEqual(t.throwInheritMaxSpeed * t.throwInheritForward, kept.magnitude, 1e-3f);
            }
            finally { Object.DestroyImmediate(t); }
        }

        // ------------------------------------------------------------------ helpers

        IEnumerator SprintAlongLane(float seconds)
        {
            yield return Place(p1, LaneStart);
            Drive.Move = Vector2.up;
            Drive.Sprint = true;
            yield return WaitSeconds(seconds);
        }

        static void Box(Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TestBox";
            go.layer = LayerMask.NameToLayer("Environment");
            go.transform.position = center;
            go.transform.localScale = size;
            Physics.SyncTransforms();
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
