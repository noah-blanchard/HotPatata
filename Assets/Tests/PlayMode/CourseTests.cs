using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>M5: the PrototypeCourse - structure, checkpoints, finish, rematch, the launch pad beat.</summary>
    public class CourseTests : SandboxTestBase
    {
        protected override string SceneName => "PrototypeCourse";

        RunManager run;

        [UnitySetUp]
        public IEnumerator Find()
        {
            run = RunManager.Instance;
            yield break;
        }

        static Vector3 PosOf(string objectName) => GameObject.Find(objectName).transform.position;

        IEnumerator StandTeamAt(Vector3 center)
        {
            yield return Place(p1, center + new Vector3(-0.8f, 0.05f, 0f));
            yield return Place(p2, center + new Vector3(0.8f, 0.05f, 0f));
        }

        [UnityTest]
        public IEnumerator Course_HasEveryBeat_ThreeCheckpoints_AndAFinish()
        {
            foreach (var n in new[] { "A_SafeCourt", "B_Landing", "C1", "C2", "C3", "D_Moving_1", "D_Moving_2", "E_Floor",
                                      "F_LaunchPad", "F2_HighLanding", "G1_Narrow_1", "G2_Falling_1", "G3_Moving_1", "FinishZone" })
                Assert.IsNotNull(GameObject.Find(n), "missing " + n);

            Assert.AreEqual(3, Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreSame(p1, bomb.Carrier);
            Assert.AreEqual(-3f, p1.transform.position.x, 0.2f);
            Assert.AreEqual(3f, p2.transform.position.x, 0.2f, "the safe court starts the pair 6 m apart");
            yield break;
        }

        [UnityTest]
        public IEnumerator Team_CanCompleteTheCourse_ThenReplayFromTheStart()
        {
            int completions = 0;
            run.RunCompleted += t => completions++;

            yield return StandTeamAt(PosOf("CP_01"));
            yield return WaitUntil(() => run.CurrentCheckpoint != null && run.CurrentCheckpoint.Id == 1, 2f, "checkpoint 1");

            yield return StandTeamAt(PosOf("CP_02"));
            yield return WaitUntil(() => run.CurrentCheckpoint.Id == 2, 2f, "checkpoint 2");
            Assert.AreEqual(tuning.holdFuseDuration, bomb.Fuse.Duration, 0.001f);

            yield return StandTeamAt(PosOf("CP_03"));
            yield return WaitUntil(() => run.CurrentCheckpoint.Id == 3, 2f, "checkpoint 3");
            Assert.AreEqual(4.5f, bomb.Fuse.Duration, 0.001f, "the final sprint uses the shorter fuse");

            yield return StandTeamAt(PosOf("FinishZone"));
            yield return WaitUntil(() => run.State == RunState.Completed, 2f, "finish");
            Assert.AreEqual(1, completions);
            Assert.Greater(run.RunTime, 0f);

            run.Restart();
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(0, run.ResetCount);
            Assert.IsNull(run.CurrentCheckpoint, "a rematch starts from the beginning");
            Assert.AreEqual(tuning.holdFuseDuration, bomb.Fuse.Duration, 0.001f, "normal fuse again");
            Assert.AreEqual(BombState.Held, bomb.State);
            yield return null;
            Assert.AreEqual(4f, p1.transform.position.z, 0.5f);
            foreach (var cp in Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                Assert.IsFalse(cp.Activated, "checkpoints are rearmed for the next run");
        }

        [UnityTest]
        public IEnumerator FailureAfterACheckpoint_ReturnsTheTeamThere_NotToTheStart()
        {
            yield return StandTeamAt(PosOf("CP_01"));
            yield return WaitUntil(() => run.CurrentCheckpoint != null, 2f, "checkpoint 1");
            yield return Place(p1, new Vector3(0f, 3.65f, 118f));
            bomb.Explode(BombFailReason.WorldContact, "test");
            yield return WaitUntil(() => run.State == RunState.Playing && run.ResetCount == 1, 4f, "reset");
            yield return null;
            Assert.AreEqual(87f, p1.transform.position.z, 3f, "back at checkpoint 1, not at the start");
        }

        [UnityTest]
        public IEnumerator VerticalCatchBeat_LaunchPadLiftsThePlayerOntoTheHighLanding()
        {
            Vector3 pad = PosOf("F_LaunchPad");
            yield return Place(p1, pad + new Vector3(0f, 0.05f, 0f));
            float apex = 0f;
            float end = Time.time + 2f;
            while (Time.time < end)
            {
                apex = Mathf.Max(apex, p1.transform.position.y);
                yield return null;
            }
            Assert.Greater(apex, pad.y + 5f, "the receiver should be thrown high enough to reach the upper landing (8.4)");
        }

        [UnityTest]
        public IEnumerator FallingPlatformsInTheFinalSprint_CollapseAfterSomeoneStepsOn()
        {
            var fp = GameObject.Find("G2_Falling_1").GetComponent<FallingPlatform>();
            Assert.IsTrue(fp.IsIdle);
            yield return Place(p1, PosOf("G2_Falling_1") + new Vector3(0f, 0.5f, 0f));
            yield return WaitUntil(() => fp.HasFallen, 3f, "did not collapse");
        }

        [UnityTest]
        public IEnumerator FallingIntoThePit_FailsTheSection()
        {
            yield return Place(p1, new Vector3(0f, 0.05f, 40f));   // safe: standing on the landing platform
            p1.TeleportTo(new Vector3(0f, 0.5f, 32f), Quaternion.identity);   // over the first gap
            yield return WaitUntil(() => run.ResetCount == 1, 6f, "falling into the pit should fail the section");
        }
    }
}
