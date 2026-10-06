using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>PROJECT_SPEC §15b: structural and rule regressions. Teleport-based tests do not certify a human clear.</summary>
    public class PatataWorksTests : SandboxTestBase
    {
        protected override string SceneName => "PatataWorks";
        static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None);

        [Test]
        public void Route_TurnsBothWays_ClimbsAndDrops_InsideTheFactory()
        {
            var route = All<CourseRoute>().Single();
            var points = route.Points;
            Assert.Greater(points.Max(p => p.x) - points.Min(p => p.x), 60);
            Assert.Greater(points.Max(p => p.z) - points.Min(p => p.z), 60);
            float climb = 0, drop = 0;
            int left = 0, right = 0;
            Vector3 previous = Vector3.zero;
            for (int i = 1; i < points.Count; i++)
            {
                var delta = points[i] - points[i - 1];
                climb += Mathf.Max(0, delta.y);
                drop = Mathf.Max(drop, -delta.y);
                delta.y = 0;
                if (delta.sqrMagnitude < 0.01f) continue;
                if (previous.sqrMagnitude > 0 && Vector3.Angle(previous, delta) > 45)
                {
                    if (Vector3.Cross(previous, delta).y > 0) right++; else left++;
                }
                previous = delta;
                if (!route.Outdoors(i))
                {
                    var midpoint = (points[i] + points[i - 1]) / 2 + Vector3.up;
                    Assert.IsTrue(Physics.RaycastAll(midpoint, Vector3.up, 80, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore)
                        .Any(h => h.collider.GetComponentInParent<CourseCeiling>() != null), "No ceiling at " + midpoint);
                }
            }
            Assert.GreaterOrEqual(left + right, 6);
            Assert.Greater(left, 0); Assert.Greater(right, 0);
            Assert.GreaterOrEqual(climb, 30); Assert.GreaterOrEqual(drop, 20);
            Assert.AreNotEqual(points[0].y, points[points.Count - 1].y);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 9), All<Checkpoint>().Select(c => c.Id));
            Assert.AreEqual(1, All<FinishZone>().Length);
            Assert.AreEqual(1, All<BombController>().Length);
        }

        [Test]
        public void IntendedPasses_ClearCeilings_Solids_AndDecoration()
        {
            Physics.SyncTransforms();
            var points = new List<Vector3>();
            var errors = new List<string>();
            var decorations = All<CourseDecoration>().SelectMany(d => d.GetComponentsInChildren<Renderer>()).ToArray();
            Assert.GreaterOrEqual(All<PassCorridor>().Length, 20);
            foreach (var pass in All<PassCorridor>())
            {
                Assert.IsTrue(pass.TrySample(tuning, points), pass.name + " is unreachable with throw tuning");
                if (pass.Opening > 0) Assert.GreaterOrEqual(pass.Opening, tuning.catchRadius + PassCorridor.OpeningMargin, pass.name);
                foreach (var point in points)
                {
                    if (Physics.RaycastAll(point, Vector3.up, PassCorridor.CeilingMargin, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore)
                        .Any(h => h.collider.GetComponentInParent<CourseCeiling>() != null))
                        errors.Add(pass.name + " ceiling at " + point);
                    foreach (var c in Physics.OverlapSphere(point, PassCorridor.SweepRadius, LayerMask.GetMask("Environment", "Hazard"), QueryTriggerInteraction.Ignore))
                    {
                        // A timed pass is tested with the mover clear; its static walls must still clear.
                        if (pass.Timed && (c.GetComponentInParent<MovingPlatform>() != null || c.GetComponentInParent<RotatingObstacle>() != null)) continue;
                        errors.Add(pass.name + " blocked by " + c.transform.parent.name + "/" + c.name + " at " + point);
                    }
                    foreach (var d in decorations)
                        if (Vector3.Distance(d.bounds.ClosestPoint(point), point) < PassCorridor.DecorationClearance)
                            errors.Add(pass.name + " decoration " + d.name);
                }
            }
            Assert.IsEmpty(errors.Distinct().Take(20).ToArray());
        }

        [Test]
        public void Signals_AreOneToOne_AndEveryTransitHasAReceiver()
        {
            var actuators = All<SignalActuator>();
            Assert.GreaterOrEqual(actuators.Length, 4);
            foreach (var a in actuators) Assert.IsNotNull(a.Source, a.name);
            foreach (var plate in All<PressurePlate>()) Assert.AreEqual(1, actuators.Count(a => ReferenceEquals(a.Source, plate)), plate.name);
            foreach (var gate in All<BombGate>())
                Assert.AreEqual(1, actuators.Count(a => ReferenceEquals(a.Source, gate)) + All<Checkpoint>().Count(c => c.ClaimGate == gate), gate.name);
            foreach (var mouth in All<TransitMouth>())
            {
                var so = new SerializedObject(mouth);
                var transit = (BombTransit)so.FindProperty("transit").objectReferenceValue;
                Assert.IsNotNull(transit);
                int index = so.FindProperty("exit").intValue;
                Assert.Less(index, transit.ExitCount);
                var exit = transit.GetExit(index);
                Assert.IsNotNull(exit.muzzle); Assert.IsNotNull(exit.pad); Assert.IsNotNull(exit.hold);
                Assert.IsTrue(exit.pad.gameObject.activeInHierarchy);
                Assert.IsTrue(Physics.Raycast(exit.pad.position + Vector3.up, Vector3.down, 2, LayerMask.GetMask("Environment")), "No floor under " + transit.name);
            }
        }

        [UnityTest]
        public IEnumerator EveryCheckpoint_ResetsAfterAnExplosion_ThenFinishAndRematch()
        {
            var run = RunManager.Instance;
            try
            {
                foreach (var cp in All<Checkpoint>().OrderBy(c => c.Id))
                {
                    RunOptions.StartCheckpoint = cp.Id;
                    run.Restart();
                    yield return null;
                    Assert.AreEqual(cp.Id, run.CurrentCheckpoint.Id);
                    foreach (var transit in All<BombTransit>()) transit.SetTransit(0, NetMode.ServerTime + 20);
                    foreach (var actuator in All<SignalActuator>()) actuator.SetState(NetMode.ServerTime - 10, 0, true);
                    int before = run.ResetCount;
                    bomb.Explode(BombFailReason.HoldFuseExpired, "PatataWorks reset regression");
                    yield return WaitUntil(() => run.State == RunState.Playing && bomb.Carrier != null, 4, "checkpoint reset " + cp.Id);
                    Assert.AreEqual(before + 1, run.ResetCount);
                    Assert.AreEqual(cp.Id, run.CurrentCheckpoint.Id);
                    Assert.Less(Vector3.Distance(p1.transform.position, cp.transform.position), 8);
                    foreach (var transit in All<BombTransit>()) Assert.AreEqual(-1, transit.ActiveExit);
                    foreach (var actuator in All<SignalActuator>()) Assert.Less(actuator.CurrentProgress, 0.1f);
                    yield return WaitUntil(() => p1.Motor.Grounded && p2.Motor.Grounded, 2, "checkpoint " + cp.Id + " spawns must stand on solid floor");
                }
                var finish = All<FinishZone>().Single().transform.position;
                yield return Place(p1, finish + new Vector3(-1, 0.1f, 0));
                yield return Place(p2, finish + new Vector3(1, 0.1f, 0));
                yield return WaitUntil(() => run.State == RunState.Completed, 2, "finish");
                RunOptions.StartCheckpoint = 0;
                run.Restart();
                yield return null;
                Assert.IsNull(run.CurrentCheckpoint);
                Assert.AreEqual(RunState.Playing, run.State);
            }
            finally { RunOptions.StartCheckpoint = 0; }
        }

        [Test]
        public void Bootstrap_ListsTheFactory_WithNineSpawnChoices()
        {
            // PatataWilds took the first place (M12); the factory keeps its place in the level list
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Network/NetworkManager.prefab");
            var so = new SerializedObject(prefab.GetComponent<NetworkBootstrap>());
            var names = so.FindProperty("gameplayScenes");
            int index = Enumerable.Range(0, names.arraySize).Single(i => names.GetArrayElementAtIndex(i).stringValue == "PatataWorks");
            Assert.AreEqual(9, so.FindProperty("sceneCheckpoints").GetArrayElementAtIndex(index).intValue);
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/PatataWorks.unity")));
        }

        [UnityTest]
        public IEnumerator Atrium_TwoPlayersPassFromThePlate_AndOpenTheReturnDoor()
        {
            var plate = All<PressurePlate>().Single();
            var lift = All<SignalActuator>().Single(a => ReferenceEquals(a.Source, plate));
            var ringPass = All<PassCorridor>().Single(c => c.name == "Pass Atrium return ring");
            yield return Place(p1, plate.transform.position + Vector3.up * 0.1f);
            yield return Place(p2, ringPass.To - Vector3.up * 1.4f);
            Give(p1);
            yield return WaitUntil(() => lift.CurrentProgress > 0.9f, 4, "plate lift");
            ThrowAt(p1, p2, 0.55f);
            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => bomb.Carrier == p2, 2, "two-player atrium catch");
            var door = All<SignalActuator>().Single(a => a.name == "Atrium return door");
            yield return WaitUntil(() => door.CurrentProgress > 0.9f, 2, "return door");
        }

        [UnityTest]
        public IEnumerator TransitExitFlights_ReachAnExplicitCatch_OnEveryReceiverPad()
        {
            foreach (var transit in All<BombTransit>())
                for (int i = 0; i < transit.ExitCount; i++)
                {
                    var exit = transit.GetExit(i);
                    if (!exit.pad.gameObject.activeInHierarchy) continue;
                    yield return Place(p2, exit.pad.position + Vector3.up * 0.1f);
                    Give(p1);
                    Assert.IsTrue(bomb.TryThrow(p1, p1.ThrowOrigin.position, Vector3.up));
                    transit.Capture(bomb, i);
                    Assert.AreEqual(BombState.InTransit, bomb.State);
                    yield return WaitUntil(() => bomb.State == BombState.Thrown, transit.Delay + 2, "transit release " + transit.name);
                    yield return CatchWhenNear(p2);
                    yield return WaitUntil(() => bomb.Carrier == p2 || bomb.State == BombState.Exploding, 3, "exit catch " + transit.name);
                    Assert.AreSame(p2, bomb.Carrier, transit.name + " exit " + i + " must reach the receiver without hitting the building");
                }
        }
    }
}
