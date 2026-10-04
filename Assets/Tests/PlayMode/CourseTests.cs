using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>M5: the PrototypeCourse - structure, checkpoints, finish, rematch, the launch pad beat. Acts 2-3: FactoryCourseTests.</summary>
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
        public IEnumerator Course_HasEveryBeat_SevenCheckpoints_AndAFinish()
        {
            foreach (var n in new[] { "A_SafeCourt", "B_Landing", "C1", "C2", "C3", "D_Moving_1", "D_Moving_2", "E_Floor",
                                      "F_LaunchPad", "F2_HighLanding", "G1_Narrow_1", "G2_Falling_1", "G3_Moving_1",
                                      "H_Belt_C", "I_Piston_1", "J_Windmill", "J_Elevator_L", "K_Sweeper_1", "K_Crusher",
                                      "L_Elevator_1", "L2_Sweeper", "M_Summit", "M_Lane_L", "M_Hoop_1", "N_Crusher", "N_Belt",
                                      "N_Gate_1", "FinishZone" })
                Assert.IsNotNull(GameObject.Find(n), "missing " + n);

            var checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
            Assert.AreEqual(7, checkpoints.Length);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6, 7 }, System.Array.ConvertAll(checkpoints, c => c.Id));
            foreach (var cp in checkpoints)
                Assert.Less(cp.transform.position.z, PosOf("FinishZone").z, $"checkpoint {cp.Id} comes before the finish");
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
            Assert.AreEqual(tuning.holdFuseDuration, bomb.Fuse.Duration, 0.001f, "Act 1 keeps the normal fuse");

            yield return StandTeamAt(PosOf("CP_04"));
            yield return WaitUntil(() => run.CurrentCheckpoint.Id == 4, 2f, "checkpoint 4");
            yield return StandTeamAt(PosOf("CP_05"));
            yield return WaitUntil(() => run.CurrentCheckpoint.Id == 5, 2f, "checkpoint 5");
            Assert.AreEqual(tuning.holdFuseDuration, bomb.Fuse.Duration, 0.001f, "the factory keeps the normal fuse");

            yield return StandTeamAt(PosOf("CP_06"));
            yield return WaitUntil(() => run.CurrentCheckpoint.Id == 6, 2f, "checkpoint 6");
            Assert.AreEqual(5f, bomb.Fuse.Duration, 0.001f, "the climb shortens the fuse");

            yield return StandTeamAt(PosOf("CP_07"));
            yield return WaitUntil(() => run.CurrentCheckpoint.Id == 7, 2f, "checkpoint 7");
            Assert.AreEqual(4.5f, bomb.Fuse.Duration, 0.001f, "the drop and the finale use the shortest fuse");

            yield return StandTeamAt(PosOf("FinishZone"));
            yield return WaitUntil(() => run.State == RunState.Completed, 2f, "finish");
            Assert.AreEqual(1, completions);
            Assert.Greater(run.RunTime, 0f);

            // #23: the results screen shows the run, and its Rematch button restarts it.
            yield return null;
            var results = ScreenStack.Existing != null ? ScreenStack.Existing.Top as ResultsScreen : null;
            Assert.IsNotNull(results, "the results screen opens when the course is complete");
            Assert.AreEqual(ResultsScreen.FormatTime(run.RunTime), results.Time.text);
            Assert.IsNotNull(results.Rematch, "offline, the player can rematch");
            using (var press = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled())
            {
                press.target = results.Rematch;
                results.Rematch.SendEvent(press);
            }
            yield return null;
            Assert.IsFalse(ScreenStack.Existing.Top is ResultsScreen, "the results close when the run starts again");

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
        public IEnumerator StartCheckpoint_BeginsTheRunThere_AndARematchStartsThereAgain()
        {
            RunOptions.StartCheckpoint = 6;
            try
            {
                run.Restart();   // same path as the level start: the run begins at the chosen checkpoint
                Assert.AreEqual(RunState.Playing, run.State);
                Assert.AreEqual(6, run.CurrentCheckpoint.Id);
                Assert.AreEqual(5f, bomb.Fuse.Duration, 0.001f, "checkpoint 6's fuse");
                Assert.AreEqual(BombState.Held, bomb.State);
                foreach (var cp in Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                    Assert.AreEqual(cp.Id <= 6, cp.Activated, $"checkpoint {cp.Id} reached");
                yield return null;
                Assert.AreEqual(PosOf("CP_06").z, p1.transform.position.z, 2f, "spawned at checkpoint 6");

                yield return Place(p1, new Vector3(0f, 3.65f, 118f));
                bomb.Explode(BombFailReason.WorldContact, "test");
                yield return WaitUntil(() => run.State == RunState.Playing && run.ResetCount == 1, 4f, "reset");
                yield return null;
                Assert.AreEqual(PosOf("CP_06").z, p1.transform.position.z, 2f, "a reset returns to the start checkpoint");

                RunOptions.StartCheckpoint = 99;   // not in this level
                run.Restart();
                Assert.IsNull(run.CurrentCheckpoint, "an unknown checkpoint starts at the beginning");
                yield return null;
                Assert.AreEqual(4f, p1.transform.position.z, 0.5f);
            }
            finally
            {
                RunOptions.StartCheckpoint = 0;
            }
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

        // ------------------------------------------------------------------ M9.2: beats tuned for the new movement

        [UnityTest]
        public IEnumerator StairRelay_TopStepIsTooHighToJump_ButCanBeMantled()
        {
            // C2 top 2.4 m, C3 top 4.2 m (a 1.8 m step, above the 1.6 m jump), 2 m apart.
            yield return Place(p1, new Vector3(0f, 2.45f, 64f));
            Drive.Move = Vector2.up;
            yield return WaitUntil(() => p1.transform.position.z > 68.4f, 2f, "never reached the edge of C2");
            Drive.PressJump();

            bool mantled = false;
            float end = Time.time + 2f;
            while (Time.time < end && !(mantled && p1.Motor.Grounded))
            {
                mantled |= p1.Motor.State == MoveState.Mantle;
                yield return null;
            }
            Assert.IsTrue(mantled, "the run-and-jump should end in a mantle");
            Assert.AreEqual(4.2f, p1.transform.position.y, 0.1f, "standing on C3");
        }

        [UnityTest]
        public IEnumerator FinalSprint_EntryGap_ASprintJumpMakesIt() =>
            JumpFromHighLandingEdge(sprint: true, shouldLand: true);

        [UnityTest]
        public IEnumerator FinalSprint_EntryGap_ARunJumpFallsShort() =>
            JumpFromHighLandingEdge(sprint: false, shouldLand: false);

        IEnumerator JumpFromHighLandingEdge(bool sprint, bool shouldLand)
        {
            // F2 (top 8.4) ends at z 204; G1_Narrow_1 starts 7.6 m further on: out of reach of a run jump even with a mantle.
            yield return Place(p1, new Vector3(0f, 8.45f, 194f));
            Drive.Move = Vector2.up;
            Drive.Sprint = sprint;
            yield return WaitUntil(() => p1.transform.position.z > 203.7f, 3f, "never reached the edge");
            Drive.PressJump();
            yield return WaitUntil(() => p1.transform.position.y > 8.9f, 1f, "did not jump");   // airborne for real, not a grounded flicker
            yield return WaitUntil(() => p1.Motor.Grounded || p1.transform.position.y < 7.5f, 2f, "never came down");
            bool landed = p1.Motor.Grounded && p1.transform.position.y > 8.3f;
            Drive.Move = Vector2.zero;
            Assert.AreEqual(shouldLand, landed, sprint ? "a sprint jump should clear the gap" : "a plain run jump should not");
        }

        [UnityTest]
        public IEnumerator FinalSprint_LowBar_BlocksARun_ButASlidePassesUnder()
        {
            var bar = GameObject.Find("G_SlideBar").GetComponentInChildren<Collider>().bounds;
            Vector3 start = new Vector3(0f, 8.45f, bar.min.z - 2.5f);

            yield return Place(p1, start);
            Drive.Move = Vector2.up;
            yield return WaitSeconds(1f);
            Assert.Less(p1.transform.position.z, bar.min.z, "standing up, the bar stops you");

            yield return Place(p1, start);
            Drive.Sprint = true;
            yield return WaitUntil(() => p1.transform.position.z > start.z + 1f, 1f, "did not start running");
            Drive.Crouch = true;
            yield return WaitUntil(() => p1.transform.position.z > bar.max.z + 0.5f, 2f, "did not get under the bar");
            Drive.Move = Vector2.zero;
        }
    }
}
