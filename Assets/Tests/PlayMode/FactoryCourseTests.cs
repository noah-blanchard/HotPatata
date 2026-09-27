using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// PrototypeCourse Acts 2-3 (Patata Factory, The Climb &amp; The Drop): belts carry, elevators lift, sweepers and
    /// crushers are lethal only to what they should catch, and the mega slide is fast.
    /// </summary>
    public class FactoryCourseTests : SandboxTestBase
    {
        protected override string SceneName => "PrototypeCourse";

        RunManager run;

        [UnitySetUp]
        public IEnumerator Find()
        {
            run = RunManager.Instance;
            bomb.Fuse.SetDurationOverride(999f);   // these tests wait on obstacles, not on the fuse
            yield break;
        }

        static Vector3 PosOf(string objectName) => GameObject.Find(objectName).transform.position;

        static Vector3 TopOf(string objectName)
        {
            var b = GameObject.Find(objectName).GetComponentInChildren<Collider>().bounds;
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        [UnityTest]
        public IEnumerator Conveyors_CarryAnIdlePlayer_ForwardOnTheSideBelts_BackOnTheCentreBelt()
        {
            yield return Place(p1, TopOf("H_Belt_L") + new Vector3(0f, 0.05f, -6f));
            float z0 = p1.transform.position.z;
            yield return WaitSeconds(1f);
            Assert.Greater(p1.transform.position.z - z0, 2f, "the side belt carries the player forward (3 m/s)");

            yield return Place(p1, TopOf("H_Belt_C") + new Vector3(0f, 0.05f, 6f));
            z0 = p1.transform.position.z;
            yield return WaitSeconds(1f);
            Assert.Less(p1.transform.position.z - z0, -2.5f, "the centre belt carries the player back (4 m/s)");
            Assert.AreEqual(0, run.ResetCount);
        }

        [UnityTest]
        public IEnumerator Elevator_LiftsARiderToTheNextLanding()
        {
            var platform = GameObject.Find("L_Elevator_1").transform.Find("Platform");
            // Board at the bottom: wait until the elevator is down.
            yield return WaitUntil(() => platform.position.y < 9.5f, 8f, "the elevator never came down");
            yield return Place(p1, platform.position + new Vector3(0f, 0.3f, 0f));
            float top = 0f;
            float end = Time.time + 6f;
            while (Time.time < end)
            {
                top = Mathf.Max(top, p1.transform.position.y);
                yield return null;
            }
            Assert.Greater(top, 16.4f, "the rider reached the L1 landing height (16.6)");
            Assert.AreEqual(0, run.ResetCount, "riding an elevator is safe");
        }

        [UnityTest]
        public IEnumerator Sweeper_FailsTheSection_ForAPlayerStandingInItsPath()
        {
            Vector3 hub = PosOf("K_Sweeper_1");
            yield return Place(p1, hub + new Vector3(4f, 0.05f, 0f));
            yield return WaitUntil(() => run.ResetCount == 1, 5f, "the sweeper should hit a player who stands still");
        }

        [UnityTest]
        public IEnumerator Sweeper_CanBeJumped()
        {
            var sweeper = GameObject.Find("K_Sweeper_1").GetComponent<RotatingObstacle>();
            Vector3 hub = sweeper.transform.position;
            Vector3 spot = hub + new Vector3(4f, 0f, 0f);   // on the +x axis: the bar is here when the angle is 0 or 180
            // Step in while the bar is well clear of the spot (passed it at least 30 deg ago, back in 60 deg or more).
            yield return WaitUntil(() => DegreesUntilBar(sweeper) > 60f && DegreesUntilBar(sweeper) < 150f, 3f, "the bar never moved away");
            yield return Place(p1, spot + new Vector3(0f, 0.05f, 0f));

            // Jump so that the apex (0.34 s) is when the bar passes; the bar needs ~0.2 s to cross a standing player.
            for (int pass = 0; pass < 2; pass++)
            {
                yield return WaitUntil(() => DegreesUntilBar(sweeper) < 32f && DegreesUntilBar(sweeper) > 26f,
                                       5f, "never lined up the jump");
                Drive.PressJump();
                yield return WaitSeconds(0.9f);
            }
            Assert.AreEqual(0, run.ResetCount, "well-timed jumps clear the knee-high bar");
        }

        // Degrees the sweeper still has to turn before its bar crosses the +x axis again (bar is symmetric: every 180).
        static float DegreesUntilBar(RotatingObstacle r)
        {
            float a = Mathf.Repeat(r.CurrentAngle, 180f);
            return 180f - a;
        }

        [UnityTest]
        public IEnumerator Crusher_CatchesAStandingPlayer()
        {
            Vector3 floor = TopOf("K_Exit");
            yield return Place(p1, new Vector3(0f, floor.y + 0.05f, PosOf("K_Crusher").z));
            yield return WaitUntil(() => run.ResetCount == 1, 5f, "standing under the crusher should fail the section");
        }

        [UnityTest]
        public IEnumerator Crusher_SparesACrouchedPlayer()
        {
            Vector3 floor = TopOf("K_Exit");
            var slab = GameObject.Find("K_Crusher").transform.Find("Platform");
            float high = GameObject.Find("K_Crusher").transform.Find("Waypoint_A").position.y;
            // Step in at the start of the crusher's wait at the top, then crouch before it comes down.
            yield return WaitUntil(() => slab.position.y < high - 0.5f, 5f, "the crusher never came down");
            yield return WaitUntil(() => slab.position.y >= high - 0.01f, 5f, "the crusher never went back up");
            Drive.Crouch = true;
            yield return Place(p1, new Vector3(0f, floor.y + 0.05f, PosOf("K_Crusher").z));
            yield return WaitSeconds(6f);   // two full crusher cycles
            Assert.AreEqual(0, run.ResetCount, "a crouched player (1.0 m) fits under the lowered crusher");
            Drive.Crouch = false;
        }

        [UnityTest]
        public IEnumerator MegaSlide_ASlideDownTheLaneIsFasterThanASprint()
        {
            var summit = GameObject.Find("M_Summit").GetComponent<Collider>().bounds;
            yield return Place(p1, new Vector3(-2.5f, summit.max.y + 0.05f, summit.max.z - 9f));
            Drive.Move = Vector2.up;
            Drive.Sprint = true;
            // Slide off the summit edge (a slide started far back runs out on the flat).
            yield return WaitUntil(() => p1.transform.position.z > summit.max.z - 2f, 3f, "never reached the edge");
            Drive.Crouch = true;

            float fastest = 0f;
            float end = Time.time + 8f;
            while (Time.time < end && p1.transform.position.z < 612f)
            {
                fastest = Mathf.Max(fastest, HorizontalSpeed(p1));
                yield return null;
            }
            Drive.Move = Vector2.zero;
            Drive.Crouch = false;
            Drive.Sprint = false;
            Assert.Greater(p1.transform.position.z, 612f, "slid all the way down");
            Assert.Greater(fastest, tuning.sprintSpeed + 2f, "the slope keeps the slide well above sprint speed");
            Assert.AreEqual(0, run.ResetCount, "the lane keeps the slider in");
        }

        [UnityTest]
        public IEnumerator PistonTiles_CarryARiderUpAndDown()
        {
            var platform = GameObject.Find("I_Piston_1").transform.Find("Platform");
            yield return WaitUntil(() => platform.position.y < 9.2f, 6f, "the tile never came down");
            yield return Place(p1, platform.position + new Vector3(0f, 0.6f, 0f));
            float top = 0f;
            float end = Time.time + 5f;
            while (Time.time < end)
            {
                top = Mathf.Max(top, p1.transform.position.y);
                yield return null;
            }
            Assert.Greater(top, 12.4f, "rode the tile up to its high stop (12.6)");
            Assert.AreEqual(0, run.ResetCount);
        }
    }
}
