using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// The systems built for three players (MVP_TASKS M15, PROJECT_SPEC §13.21-§13.24), played on PassSandbox's
    /// <c>KitDemo/TrioObstacles</c> corner (<c>BombObstacleKitBuilder.BuildSandboxTrioDemo</c>): the heavy plate, the bomb in
    /// flight still being its thrower's, the hourglass plate, the sun beam, and the pivot carrying its rider round.
    /// </summary>
    public class TrioObstacleTests : SandboxTestBase
    {
        const string Demo = "SectionRoot/KitDemo/TrioObstacles/";

        [UnitySetUp]
        public IEnumerator DriveTheBombByHand()
        {
            Object.FindFirstObjectByType<RunManager>().enabled = false;   // no automatic reset in the way
            yield break;
        }

        static T Find<T>(string name) where T : Component
        {
            var go = GameObject.Find(Demo + name);
            Assert.IsNotNull(go, "PassSandbox has no " + name + " (run HotPatata/Course/Build Sandbox Trio Obstacles)");
            return go.GetComponent<T>();
        }

        [UnityTest]
        public IEnumerator HeavyPlate_NeedsTwoEmptyHands()
        {
            var plate = Find<PressurePlate>("Demo_HeavyPlate");
            var lift = Find<SignalActuator>("Demo_HeavyLift");
            Vector3 c = plate.transform.position;
            yield return Place(p2, c + new Vector3(0f, 0.05f, 6f));
            Give(p2);   // the bomb starts in p1's hands: hand it to someone off the plate
            yield return Place(p1, c + new Vector3(-0.9f, 0.05f, 0f));
            yield return WaitSeconds(0.3f);
            Assert.AreEqual(1, plate.CountedBodies);
            Assert.IsFalse(plate.Active, "one body does not hold a heavy plate");

            Give(p1);
            yield return Place(p2, c + new Vector3(0.9f, 0.05f, 0f));
            yield return WaitSeconds(0.3f);
            Assert.AreEqual(1, plate.CountedBodies, "the carrier does not count");
            Assert.IsFalse(plate.Active, "one empty-handed body and the carrier are not enough");

            bomb.BeginReset();   // nobody holds the bomb: both count
            yield return WaitUntil(() => lift.CurrentProgress > 0.5f, 3f, "two empty-handed bodies raise the lift");

            Give(p1);
            yield return WaitSeconds(0.2f);
            Assert.IsFalse(plate.Active, "the bomb back in a hand");

            // A lob straight up: while it flies, the bomb is still its thrower's (§13.21).
            Vector3 origin = p1.ThrowOrigin.position;
            Assert.IsTrue(bomb.TryThrow(p1, origin, Vector3.up * 9f));
            yield return WaitSeconds(0.3f);
            Assert.AreEqual(BombState.Thrown, bomb.State);
            Assert.IsFalse(plate.Active, "a throw does not free the thrower");
        }

        [UnityTest]
        public IEnumerator HourglassPlate_RunsOnAfterRelease()
        {
            var plate = Find<PressurePlate>("Demo_HourglassPlate");
            var lift = Find<SignalActuator>("Demo_HourglassLift");
            Vector3 c = plate.transform.position;
            yield return Place(p2, c + new Vector3(0f, 0.05f, 0f));
            yield return WaitUntil(() => plate.Active, 1f, "held");
            Assert.IsTrue(plate.Held);

            yield return Place(p2, c + new Vector3(0f, 0.05f, -4f));   // off it
            yield return WaitSeconds(0.3f);
            Assert.IsFalse(plate.Held);
            Assert.IsTrue(plate.Active, "the sand is running");
            yield return WaitSeconds(plate.MemorySeconds - 1f);
            Assert.IsTrue(plate.Active, "still running a second before the end");
            Assert.Greater(lift.CurrentProgress, 0.9f, "the lift stays up meanwhile");
            yield return WaitUntil(() => !plate.Active, 2f, "the sand ran out");
            yield return WaitUntil(() => lift.CurrentProgress < 0.1f, 4f, "and the lift came down");
        }

        [UnityTest]
        public IEnumerator SunBeam_TurnsThePivot_WithItsRider()
        {
            var beam = Find<SunBeam>("Demo_SunBeam");
            var pivot = Find<SignalActuator>("Demo_Pivot");
            Vector3 hub = pivot.Platform.position;
            Vector3 riderStart = hub + new Vector3(0f, 0.3f, 1.6f);   // on the slab, toward its +Z end
            yield return Place(p1, riderStart);
            float yawBefore = p1.Look.Yaw;
            Assert.IsFalse(beam.Cut);

            // A body anywhere along the beam cuts it.
            Vector3 slit = beam.transform.position;
            yield return Place(p2, new Vector3(slit.x - 2.5f, 0.05f, slit.z));
            yield return WaitUntil(() => beam.Cut && beam.Active, 1f, "the body cuts the beam");
            Assert.Less(beam.Reach, 0.7f, "the light stops at the body");

            yield return WaitUntil(() => pivot.CurrentProgress >= 1f, pivot.TravelSeconds + 2f, "the pivot turned");
            yield return WaitSeconds(0.2f);
            Vector3 expected = hub + Quaternion.Euler(0f, 90f, 0f) * (riderStart - hub);
            Vector3 now = p1.transform.position;
            Assert.Less(new Vector2(now.x - expected.x, now.z - expected.z).magnitude, 0.5f, "the rider was carried a quarter round");
            Assert.IsTrue(p1.Motor.Grounded, "and is still standing on it");
            Assert.AreEqual(90f, Mathf.DeltaAngle(yawBefore, p1.Look.Yaw), 3f, "and turned with it");
        }
    }
}
