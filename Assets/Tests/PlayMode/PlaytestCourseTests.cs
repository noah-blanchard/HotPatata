using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// M10.4: PlaytestCourse - every beat is there and wired, the nine checkpoints (two of them arches claimed by a pass)
    /// lead to the finish, the last gate opens the podium door, and a rematch starts over.
    /// </summary>
    public class PlaytestCourseTests : SandboxTestBase
    {
        protected override string SceneName => "PlaytestCourse";

        RunManager run;

        [UnitySetUp]
        public IEnumerator Find()
        {
            run = RunManager.Instance;
            yield break;
        }

        static Vector3 PosOf(string objectName)
        {
            var go = GameObject.Find(objectName);
            Assert.IsNotNull(go, "missing " + objectName);
            return go.transform.position;
        }

        static T Get<T>(string objectName) where T : Component => GameObject.Find(objectName).GetComponent<T>();

        IEnumerator StandTeamAt(Vector3 center)
        {
            yield return Place(p1, center + new Vector3(-0.8f, 0.05f, 0f));
            yield return Place(p2, center + new Vector3(0.8f, 0.05f, 0f));
        }

        IEnumerator ReachCheckpoint(int id)
        {
            yield return StandTeamAt(PosOf($"CP_0{id}"));
            yield return WaitUntil(() => run.CurrentCheckpoint != null && run.CurrentCheckpoint.Id == id, 2f, "checkpoint " + id);
        }

        /// <summary>A real pass from <paramref name="from"/> through an arch or ring to a teammate on <paramref name="to"/>.</summary>
        IEnumerator PassThrough(Vector3 from, Vector3 to)
        {
            yield return Place(p2, to + new Vector3(0f, 0.05f, 0f));
            yield return Place(p1, from + new Vector3(0f, 0.05f, 0f));
            Give(p1);
            yield return null;
            int caught = 0;
            System.Action<Player> onCatch = r => caught++;
            bomb.BombCaught += onCatch;
            ThrowAt(p1, p2, 0.55f);
            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => caught > 0 || bomb.State == BombState.Exploding, 3f, "the pass");
            bomb.BombCaught -= onCatch;
            Assert.AreEqual(1, caught, "the pass through the gate was caught");
        }

        [UnityTest]
        public IEnumerator Course_HasEveryBeat_NineCheckpoints_TwoArches_AndAFinish()
        {
            foreach (var n in new[] { "A_StartCourt", "B_Landing", "C3", "D_Moving_1", "E_Belt_C", "F_Sweeper_1",
                                      "H_Strip_1", "H_Strip_2", "I_Wall_Curtain_1", "J_HotZone", "J_ColdPocket_L", "K_Wall_3_Hoop",
                                      "L_Gate", "L_Bridge", "M_Plate", "M_Door", "M_DoorCurtain", "M_ColdPocket", "N_Shutter", "N_Crusher",
                                      "CP_06_Arch", "O_Gate_1", "O_Lift", "O_Gate_2", "O_Bridge", "P_Tube", "Q_Tube", "Q_Left_Falling_1",
                                      "Q_Centre_Sweeper", "Q_Right_Belt", "R_Cannon", "R_Shuttle_1", "S_Lane_L", "S_Hoop_1", "S_HotZone",
                                      "T_Gate_1", "CP_09_Arch", "T_Gate", "T_Door", "FinishZone" })
                Assert.IsNotNull(GameObject.Find(n), "missing " + n);

            var checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, System.Array.ConvertAll(checkpoints, c => c.Id));
            for (int id = 1; id < 9; id++)
                Assert.Less(PosOf($"CP_0{id}").z, PosOf($"CP_0{id + 1}").z, $"checkpoint {id} comes before {id + 1}");
            Assert.Less(PosOf("CP_09").z, PosOf("FinishZone").z);
            foreach (var cp in checkpoints)
                Assert.AreEqual(cp.Id == 6 || cp.Id == 9, cp.ClaimGate != null, $"checkpoint {cp.Id} arch");

            foreach (var a in Object.FindObjectsByType<SignalActuator>(FindObjectsSortMode.None))
                Assert.IsNotNull(a.Source, a.name + " has a source");
            Assert.AreEqual(3, Get<BombTransit>("Q_Tube").GetComponentsInChildren<TransitMouth>(false).Length, "three mouths, one per lane");
            Assert.AreEqual(1, Get<BombTransit>("P_Tube").GetComponentsInChildren<TransitMouth>(false).Length);

            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreSame(p1, bomb.Carrier);
            Assert.AreEqual(6f, Mathf.Abs(p2.transform.position.x - p1.transform.position.x), 0.3f, "the pair starts 6 m apart");
            yield break;
        }

        [UnityTest]
        public IEnumerator Team_CanCompleteTheCourse_ThroughBothArches_ThenReplayFromTheStart()
        {
            int completions = 0;
            run.RunCompleted += t => completions++;

            for (int id = 1; id <= 5; id++) yield return ReachCheckpoint(id);
            Assert.AreEqual(tuning.holdFuseDuration, bomb.Fuse.Duration, 0.001f, "normal fuse through act 3");

            // CP6 is an arch: standing on the pad is not enough.
            yield return StandTeamAt(PosOf("CP_06"));
            yield return WaitSeconds(0.4f);
            Assert.AreEqual(5, run.CurrentCheckpoint.Id, "no pass through the arch yet");
            Vector3 arch6 = PosOf("CP_06_Arch");
            yield return PassThrough(arch6 + new Vector3(0f, 0f, -6.5f), PosOf("CP_06") + new Vector3(0.8f, 0f, 0f));
            yield return Place(p1, PosOf("CP_06") + new Vector3(-0.8f, 0.05f, 0f));
            yield return WaitUntil(() => run.CurrentCheckpoint.Id == 6, 2f, "arch checkpoint 6");

            yield return ReachCheckpoint(7);
            yield return ReachCheckpoint(8);
            Assert.AreEqual(5f, bomb.Fuse.Duration, 0.001f, "the finale shortens the fuse");

            Vector3 arch9 = PosOf("CP_09_Arch");
            yield return PassThrough(arch9 + new Vector3(0f, 0f, -0.9f), PosOf("CP_09") + new Vector3(0.8f, 0f, 0f));
            yield return Place(p1, PosOf("CP_09") + new Vector3(-0.8f, 0.05f, 0f));
            yield return WaitUntil(() => run.CurrentCheckpoint.Id == 9, 2f, "arch checkpoint 9");
            Assert.AreEqual(5f, bomb.Fuse.Duration, 0.001f);

            yield return StandTeamAt(PosOf("FinishZone"));
            yield return WaitUntil(() => run.State == RunState.Completed, 2f, "finish");
            Assert.AreEqual(1, completions);

            run.Restart();
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.IsNull(run.CurrentCheckpoint, "a rematch starts from the beginning");
            Assert.IsFalse(Get<Checkpoint>("CP_06").ClaimGate.PassedThisSection, "arches are cleared for the next run");
            yield return null;
            Assert.AreEqual(4f, p1.transform.position.z, 0.5f);
        }

        [UnityTest]
        public IEnumerator LastGate_OpensThePodiumDoor()
        {
            RunOptions.StartCheckpoint = 9;
            try
            {
                run.Restart();
                var door = Get<SignalActuator>("T_Door");
                yield return null;
                Assert.AreEqual(0f, door.CurrentProgress, "the podium door starts closed");

                Vector3 ring = PosOf("T_Gate");
                Vector3 floor = new Vector3(ring.x, PosOf("CP_09").y, ring.z);
                yield return PassThrough(floor + new Vector3(0f, 0f, -4.5f), floor + new Vector3(0f, 0f, 4.5f));
                yield return WaitUntil(() => door.CurrentProgress >= 1f, 3f, "the gate opened the podium door");
            }
            finally
            {
                RunOptions.StartCheckpoint = 0;
            }
        }
    }
}
