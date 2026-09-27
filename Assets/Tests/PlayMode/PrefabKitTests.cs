using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Beep.Tests
{
    /// <summary>M2: the reusable greybox kit (moving / falling platforms, rotating bar, kill zone, checkpoint, finish zone).</summary>
    public class PrefabKitTests : SandboxTestBase
    {
        RunManager run;

        [UnitySetUp]
        public IEnumerator Find()
        {
            run = RunManager.Instance;
            yield break;
        }

        static T Get<T>(string objectName) where T : Component => GameObject.Find(objectName).GetComponent<T>();

        IEnumerator FailAndWaitForReset()
        {
            int before = run.ResetCount;
            bomb.Explode(BombFailReason.WorldContact, "test");
            yield return WaitUntil(() => run.ResetCount > before && run.State == RunState.Playing, 4f, "section never reset");
        }

        // ------------------------------------------------------------------ Platform_Moving

        [UnityTest]
        public IEnumerator MovingPlatform_FollowsItsWaypoints_Deterministically()
        {
            var demo = GameObject.Find("Platform_Moving_Demo");
            var body = demo.transform.Find("Platform");
            Vector3 a = demo.transform.Find("Waypoint_A").position, b = demo.transform.Find("Waypoint_B").position;

            for (int i = 0; i < 3; i++)
            {
                yield return WaitSeconds(1.3f);
                float u = Mathf.PingPong(SectionClock.Now * 2f / Vector3.Distance(a, b), 1f);   // speed 2, phase 0
                Vector3 expected = Vector3.Lerp(a, b, u);
                Assert.AreEqual(0f, Vector3.Distance(expected, body.position), 0.25f, "position must be a function of the section clock");
            }
        }

        [UnityTest]
        public IEnumerator MovingPlatform_CarriesAPlayerStandingOnIt()
        {
            var body = GameObject.Find("Platform_Moving_Demo").transform.Find("Platform");
            yield return Place(p1, body.position + new Vector3(0f, 0.45f, 0f));
            Vector3 platformStart = body.position;
            float offsetStart = p1.transform.position.x - body.position.x;

            yield return WaitSeconds(1.2f);

            Assert.Greater(Vector3.Distance(platformStart, body.position), 1.5f, "platform should have travelled");
            Assert.AreEqual(offsetStart, p1.transform.position.x - body.position.x, 0.4f, "rider must stay on the platform");
            Assert.IsTrue(p1.Motor.Grounded);
        }

        [UnityTest]
        public IEnumerator MovingPlatform_ReturnsToItsStart_AfterASectionReset()
        {
            var demo = GameObject.Find("Platform_Moving_Demo");
            var body = demo.transform.Find("Platform");
            Vector3 a = demo.transform.Find("Waypoint_A").position;
            yield return WaitSeconds(1.5f);
            Assert.Greater(Vector3.Distance(body.position, a), 1f, "should have moved away first");

            yield return FailAndWaitForReset();
            yield return null;
            Assert.AreEqual(0f, Vector3.Distance(body.position, a), 0.4f, "reset restarts the cycle at waypoint A");
        }

        // ------------------------------------------------------------------ Obstacle_RotatingBar

        [UnityTest]
        public IEnumerator RotatingBar_Rotates_Deterministically_AndRestartsOnReset()
        {
            var bar = Get<RotatingObstacle>("Obstacle_RotatingBar_Demo");
            yield return WaitSeconds(0.5f);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(bar.transform.eulerAngles.y, bar.CurrentAngle)), 3f, "angle follows the section clock");

            float y1 = bar.transform.eulerAngles.y;
            yield return WaitSeconds(0.5f);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(y1, bar.transform.eulerAngles.y)), 20f, "bar keeps turning (60 deg/s)");

            yield return FailAndWaitForReset();
            yield return null;
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(bar.transform.eulerAngles.y, 0f)), 8f, "reset restarts the rotation");
        }

        // ------------------------------------------------------------------ Platform_Falling

        [UnityTest]
        public IEnumerator FallingPlatform_HoldsUntilSteppedOn_ThenFalls_AndRestoresOnReset()
        {
            var fp = Get<FallingPlatform>("Platform_Falling_Demo");
            var body = fp.transform.Find("Body");
            Vector3 start = body.position;

            yield return WaitSeconds(0.5f);
            Assert.IsTrue(fp.IsIdle, "nobody on it yet");

            yield return Place(p1, start + new Vector3(0f, 0.4f, 0f));
            yield return WaitUntil(() => fp.HasFallen, 3f, "stepping on it never triggered the collapse");
            yield return WaitSeconds(0.5f);
            Assert.Less(body.position.y, start.y - 1f, "platform should have dropped away");

            yield return FailAndWaitForReset();
            Assert.IsTrue(fp.IsIdle);
            Assert.IsTrue(body.gameObject.activeSelf);
            Assert.AreEqual(0f, Vector3.Distance(start, body.position), 0.01f);
        }

        // ------------------------------------------------------------------ KillZone

        [UnityTest]
        public IEnumerator KillZone_PlayerEntering_FailsTheSection_AndTheTeamResets()
        {
            p1.TeleportTo(new Vector3(0f, -15f, 0f), Quaternion.identity);
            yield return WaitUntil(() => run.ResetCount == 1, 2f, "falling into the kill zone never failed the section");
            yield return WaitUntil(() => run.State == RunState.Playing, 3f, "no resume");
            Assert.AreEqual(-3f, p1.transform.position.x, 0.1f, "player is back at spawn");
            Assert.AreEqual(BombState.Held, bomb.State);
        }

        // ------------------------------------------------------------------ LaunchPad

        [UnityTest]
        public IEnumerator LaunchPad_ThrowsAPlayerStandingOnItToTheConfiguredHeight()
        {
            Vector3 pad = GameObject.Find("LaunchPad_Demo").transform.position;
            yield return Place(p1, pad + new Vector3(0f, 0.05f, 0f));
            float y0 = p1.transform.position.y;
            float apex = y0;
            float end = Time.time + 2.5f;
            while (Time.time < end)
            {
                apex = Mathf.Max(apex, p1.transform.position.y);
                yield return null;
            }
            Assert.AreEqual(6f, apex - y0, 1.0f, "launch height");
        }

        // ------------------------------------------------------------------ Checkpoint

        [UnityTest]
        public IEnumerator Checkpoint_ActivatesOnlyWhenEveryPlayerIsInside_AndResetsReturnThere()
        {
            var cp = Get<Checkpoint>("CP_01");
            Vector3 c = cp.transform.position;

            yield return Place(p1, c + new Vector3(-0.8f, 0.05f, 0f));
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(cp.Activated, "one player alone must not activate it");
            Assert.IsNull(run.CurrentCheckpoint);

            yield return Place(p2, c + new Vector3(0.8f, 0.05f, 0f));
            yield return WaitUntil(() => cp.Activated, 1.5f, "checkpoint never activated with the whole team inside");
            Assert.AreSame(cp, run.CurrentCheckpoint);

            // Scatter, fail, and confirm the team returns to the checkpoint's spawn slots.
            yield return Place(p1, new Vector3(0f, 0.05f, 5f));
            yield return Place(p2, new Vector3(6f, 0.05f, 5f));
            yield return FailAndWaitForReset();

            Assert.AreEqual(0f, Vector3.Distance(p1.transform.position, cp.FindSpawn(0).transform.position), 0.3f);
            Assert.AreEqual(0f, Vector3.Distance(p2.transform.position, cp.FindSpawn(1).transform.position), 0.3f);
            Assert.AreSame(p1, bomb.Carrier, "checkpoint carrier slot 0 holds the bomb");
        }

        // ------------------------------------------------------------------ FinishZone

        [UnityTest]
        public IEnumerator FinishZone_NeedsEveryPlayer_AndCompletesExactlyOnce()
        {
            int completions = 0;
            run.RunCompleted += t => completions++;
            Vector3 c = GameObject.Find("FinishZone").transform.position;

            yield return Place(p1, c + new Vector3(-0.8f, 0.05f, 0f));
            yield return WaitSeconds(0.5f);
            Assert.AreEqual(RunState.Playing, run.State, "one player alone cannot finish a two-player run");
            Assert.AreEqual(0, completions);

            yield return Place(p2, c + new Vector3(0.8f, 0.05f, 0f));
            yield return WaitUntil(() => run.State == RunState.Completed, 1.5f, "run never completed");
            yield return WaitSeconds(0.5f);

            Assert.AreEqual(1, completions, "completion fires once");
            Assert.IsTrue(p1.ControlLocked && p2.ControlLocked);
            Assert.AreEqual(BombState.Resetting, bomb.State, "bomb is inert after the finish");
            Assert.Greater(run.RunTime, 0f);
        }
    }
}
