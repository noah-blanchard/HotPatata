using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// PatataPark (the KayKit course): every beat is there and wired, the nine checkpoints (two of them arches claimed by a
    /// pass) lead to the finish, the last gate opens the podium door, and a rematch starts over.
    /// </summary>
    public class PatataParkTests : SandboxTestBase
    {
        protected override string SceneName => "PatataPark";

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
            foreach (var n in new[] { "A_Start", "B_Hop_1", "B_Hop_3", "C_Ramp", "D_Falling_1", "E_Piston_1", "F_Sweeper", "F_Windmill",
                                      "F_Crusher", "G_Strip_L", "G_Strip_R", "H_Wall_1_Curtain_1", "H_Wall_3_Hoop", "I_HotZone", "I_ColdPocket",
                                      "J_Gate", "J_Bridge", "K_Plate", "K_Door", "K_DoorCurtain", "K_ColdPocket", "CP_06_Arch", "M_Gate_1",
                                      "M_Lift", "M_Gate_2", "M_Bridge", "N_Tube", "N_Left_Falling_1", "N_Centre_Sweeper", "N_Right_Belt",
                                      "O_Cannon", "O_Shuttle_1", "P_Lane_L", "P_Hoop_1", "P_HotZone", "Q_Gate_1", "CP_09_Arch", "Q_Gate",
                                      "Q_Door", "FinishZone" })
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
            Assert.AreEqual(3, Get<BombTransit>("N_Tube").GetComponentsInChildren<TransitMouth>(false).Length, "three mouths, one per lane");
            foreach (var skin in Object.FindObjectsByType<KitSkin>(FindObjectsSortMode.None))
                Assert.IsNotNull(skin.GetComponent<MeshFilter>().sharedMesh, skin.name + " is drawn in KayKit pieces");

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
            Assert.AreEqual(tuning.holdFuseDuration, bomb.Fuse.Duration, 0.001f, "normal fuse up to act 4");

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
                var door = Get<SignalActuator>("Q_Door");
                yield return null;
                Assert.AreEqual(0f, door.CurrentProgress, "the podium door starts closed");

                Vector3 ring = PosOf("Q_Gate");
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
