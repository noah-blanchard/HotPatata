using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// PROJECT_SPEC §15d (PatataCanopy, M13): structure, rules and signature puzzles of the tree-top course. Its contracts are
    /// checked in EditMode (<c>CourseContractTests</c>); teleport-based tests do not certify a human clear.
    /// </summary>
    public class PatataCanopyTests : SandboxTestBase
    {
        protected override string SceneName => "PatataCanopy";
        static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        static T Named<T>(string name) where T : Component => All<T>().Single(c => c.name == name);

        [Test]
        public void Route_FiveActs_25Checkpoints_OneFinish_EveryContractDeclared()
        {
            var checkpoints = All<Checkpoint>().OrderBy(c => c.Id).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(1, 25), checkpoints.Select(c => c.Id));
            Assert.AreEqual(1, All<FinishZone>().Length);
            Assert.AreEqual(1, All<BombController>().Length);
            for (int i = 1; i < checkpoints.Length; i++)
            {
                var a = checkpoints[i - 1].transform.position;
                var b = checkpoints[i].transform.position;
                float d = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                Assert.That(d, Is.InRange(20f, 150f), $"checkpoints {i} and {i + 1} are {d:F0} m apart");
            }
            var contracts = All<SectionContract>();
            Assert.AreEqual(25, contracts.Length, "one contract per section");
            foreach (var c in contracts) Assert.IsNotEmpty(c.Force, c.transform.parent.name + " says what it forces");
        }

        [Test]
        public void FuseOverride_FiveFromCheckpoint15_FourAndAHalfFrom20()
        {
            foreach (var cp in All<Checkpoint>())
            {
                float expected = cp.Id >= 20 ? 4.5f : cp.Id >= 15 ? 5f : 0f;
                Assert.AreEqual(expected, cp.HoldFuseOverride, 1e-4f, "checkpoint " + cp.Id);
            }
        }

        [Test]
        public void IntendedPasses_ClearCeilings_Solids_AndDecoration()
        {
            Physics.SyncTransforms();
            var points = new List<Vector3>();
            var errors = new List<string>();
            var allDecorations = All<CourseDecoration>().SelectMany(d => d.GetComponentsInChildren<Renderer>()).ToArray();
            Assert.GreaterOrEqual(All<PassCorridor>().Length, 40);
            foreach (var pass in All<PassCorridor>())
            {
                Assert.IsTrue(pass.TrySample(tuning, points), pass.name + " is unreachable with throw tuning");
                if (pass.Opening > 0) Assert.GreaterOrEqual(pass.Opening, tuning.catchRadius + PassCorridor.OpeningMargin, pass.name);
                if (pass.Arc == PassCorridor.ArcKind.Normal)
                {
                    float length = Vector2.Distance(new Vector2(pass.From.x, pass.From.z), new Vector2(pass.To.x, pass.To.z));
                    Assert.LessOrEqual(length, 14.5f, pass.name + " is longer than a normal pass (spec §14)");
                }
                var arc = new Bounds(points[0], Vector3.zero);
                foreach (var point in points) arc.Encapsulate(point);
                arc.Expand(2f * PassCorridor.DecorationClearance);
                var decorations = allDecorations.Where(r => r.bounds.Intersects(arc)).ToArray();
                foreach (var point in points)
                {
                    if (Physics.RaycastAll(point, Vector3.up, PassCorridor.CeilingMargin, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore)
                        .Any(h => h.collider.GetComponentInParent<CourseCeiling>() != null))
                        errors.Add(pass.name + " ceiling at " + point);
                    foreach (var c in Physics.OverlapSphere(point, PassCorridor.SweepRadius, LayerMask.GetMask("Environment", "Hazard"), QueryTriggerInteraction.Ignore))
                    {
                        if (pass.Timed && (c.GetComponentInParent<MovingPlatform>() != null || c.GetComponentInParent<RotatingObstacle>() != null)) continue;
                        if (pass.Arc == PassCorridor.ArcKind.Fixed && c.GetComponentInParent<BombTransit>() != null) continue;   // the muzzle it leaves
                        errors.Add(pass.name + " blocked by " + c.transform.parent?.name + "/" + c.name + " at " + point);
                    }
                    foreach (var d in decorations)
                        if (Vector3.Distance(d.bounds.ClosestPoint(point), point) < PassCorridor.DecorationClearance)
                            errors.Add(pass.name + " decoration " + d.name);
                }
            }
            Assert.IsEmpty(errors.Distinct().Take(20).ToArray());
        }

        [Test]
        public void Signals_AreOneToOne_SwitchesHaveTargets_TransitsHaveReceivers()
        {
            var actuators = All<SignalActuator>();
            Assert.GreaterOrEqual(actuators.Length, 12);
            foreach (var a in actuators) Assert.IsNotNull(a.Source, a.name);
            foreach (var plate in All<PressurePlate>()) Assert.AreEqual(1, actuators.Count(a => ReferenceEquals(a.Source, plate)), plate.name);
            foreach (var gate in All<BombGate>())
                Assert.AreEqual(1, actuators.Count(a => ReferenceEquals(a.Source, gate)) + All<Checkpoint>().Count(c => c.ClaimGate == gate), gate.name);
            var switches = All<SignalSwitch>();
            Assert.AreEqual(3, switches.Length, "the fireflies' lasers, the hedge and the spores");
            foreach (var s in switches) Assert.IsTrue(s.Targets.Count > 0 && s.Targets.All(t => t != null), s.name);
            Assert.GreaterOrEqual(All<PressurePlate>().Count(p => !p.CountsCarrier), 5, "hands-free plates");
            foreach (var transit in All<BombTransit>())
                for (int i = 0; i < transit.ExitCount; i++)
                    Assert.IsTrue(Physics.Raycast(transit.GetExit(i).pad.position + Vector3.up, Vector3.down, 2, LayerMask.GetMask("Environment")), "No floor under " + transit.name);
        }

        [Test]
        public void BodyScreens_StopPlayersOnly_AndCampfiresMarkTheCheckpoints()
        {
            int screenLayer = LayerMask.NameToLayer(BodyScreen.LayerName);
            Assert.GreaterOrEqual(All<BodyScreen>().Length, 12);
            foreach (var s in All<BodyScreen>())
                foreach (var c in s.GetComponentsInChildren<Collider>(true))
                    Assert.AreEqual(screenLayer, c.gameObject.layer, s.name);
            var fires = All<CampfirePresentation>();
            foreach (var cp in All<Checkpoint>())
            {
                AssertCampfireMarks(fires.Single(f => f.Checkpoint == cp), cp);
            }
            Assert.AreEqual(1, fires.Count(f => f.Checkpoint == null), "one summit beacon");
        }

        [UnityTest]
        public IEnumerator Campfire_CatchesWhenTheTeamGathersAtIt()
        {
            var cp = All<Checkpoint>().Single(c => c.Id == 1);
            var fire = All<CampfirePresentation>().Single(f => f.Checkpoint == cp);
            yield return null;
            Assert.IsFalse(fire.Lit);
            yield return GatherAtCampfire(fire, cp);
            yield return WaitUntil(() => cp.Activated, 2, "checkpoint 1 activation");
            yield return WaitUntil(() => fire.Lit && fire.Lighting >= 1f, tuning.campfireIgniteSeconds + 2, "the campfire catches");
        }

        [Test]
        public void Bootstrap_ListsPatataCanopySecond_With25SpawnChoices()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Network/NetworkManager.prefab");
            var so = new SerializedObject(prefab.GetComponent<NetworkBootstrap>());
            Assert.AreEqual("PatataCanopy", so.FindProperty("gameplayScenes").GetArrayElementAtIndex(1).stringValue);
            Assert.AreEqual(25, so.FindProperty("sceneCheckpoints").GetArrayElementAtIndex(1).intValue);
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/PatataCanopy.unity")));
        }

        // ------------------------------------------------------------------ signature puzzles

        [UnityTest]
        public IEnumerator Lucioles_ThePlateBeyondCutsTheLasers_TheCatchOnItBringsThemBack()
        {
            var plate = Named<PressurePlate>("Firefly plate");
            var sw = All<SignalSwitch>().Single(s => ReferenceEquals(s.Source, plate));
            var lasers = sw.Targets[0];
            var pass = Named<PassCorridor>("Pass Through the cut lasers");
            Assert.IsTrue(lasers.activeSelf);
            yield return Place(p2, plate.transform.position + Vector3.up * 0.1f);
            yield return WaitUntil(() => !lasers.activeSelf, 2, "the plate cuts the lasers");
            yield return Place(p1, pass.From - Vector3.up * 1.4f);
            Give(p1);
            yield return null;
            ThrowAt(p1, p2, 0.6f);
            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => bomb.Carrier == p2, 2, "the pass through the cut lasers");
            yield return WaitUntil(() => lasers.activeSelf, 2, "the catch on the hands-free plate brings the lasers back");
        }

        [UnityTest]
        public IEnumerator PontLevis_TheEastPlateRaisesTheWestBridge_TheDivideRingRaisesTheEast()
        {
            var plate = Named<PressurePlate>("East plate");
            var west = Named<SignalActuator>("West drawbridge");
            var east = Named<SignalActuator>("East drawbridge");
            var pass = Named<PassCorridor>("Pass Through the divide ring");
            yield return Place(p2, plate.transform.position + Vector3.up * 0.1f);
            yield return WaitUntil(() => west.CurrentProgress > 0.95f, 4, "the hands-free plate raises the west bridge");
            yield return Place(p2, pass.To - Vector3.up * 1.4f);
            yield return Place(p1, pass.From - Vector3.up * 1.4f);
            Give(p1);
            yield return null;
            ThrowAt(p1, p2, 0.5f);
            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => bomb.Carrier == p2, 2, "the pass through the ring and the brambles in the divide");
            yield return WaitUntil(() => east.CurrentProgress > 0.95f, 4, "the ring raises the east bridge");
        }

        [UnityTest]
        public IEnumerator LaPlaque_TheHolderRaisesTheCarriersBridge_TheThrowBackRaisesTheHolders()
        {
            var plate = Named<PressurePlate>("Hands-free plate");
            var plateBridge = Named<SignalActuator>("Plate bridge");
            var ringBridge = Named<SignalActuator>("Ring bridge");
            var back = Named<PassCorridor>("Pass Back through the ring");
            yield return Place(p2, plate.transform.position + Vector3.up * 0.1f);
            Give(p2);
            yield return WaitSeconds(0.3f);
            Assert.Less(plateBridge.CurrentProgress, 0.05f, "a carrier never holds a hands-free plate");
            Give(p1);
            yield return WaitUntil(() => plateBridge.CurrentProgress > 0.95f, 4, "the empty-handed holder raises the bridge");
            yield return Place(p1, back.From - Vector3.up * 1.4f);
            yield return Place(p2, back.To - Vector3.up * 1.4f);
            Give(p1);
            yield return null;
            Vector3 origin = p1.ThrowOrigin.position;
            Assert.IsTrue(bomb.TryThrow(p1, origin, VelocityToHit(origin, CatchPoint(p2), Vector3.Distance(origin, CatchPoint(p2)) / 26f)));
            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => bomb.Carrier == p2, 2, "the throw back through the ring");
            yield return WaitUntil(() => ringBridge.CurrentProgress > 0.95f, 4, "the ring raises the holder's bridge");
        }

        [UnityTest]
        public IEnumerator PontDesSpores_AnEmptyHandedHolderClearsTheSpores_TheCarrierDoesNot()
        {
            var plate = Named<PressurePlate>("Spore plate");
            var spores = All<SignalSwitch>().Single(s => ReferenceEquals(s.Source, plate)).Targets[0];
            yield return Place(p2, plate.transform.position + Vector3.up * 0.1f);
            yield return WaitUntil(() => !spores.activeSelf, 2, "the holder clears the spores");
            Give(p2);
            yield return WaitUntil(() => spores.activeSelf, 2, "the carrier on the plate does not hold it");
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
                    bomb.Explode(BombFailReason.HoldFuseExpired, "PatataCanopy reset regression");
                    yield return WaitUntil(() => run.State == RunState.Playing && bomb.Carrier != null, 4, "checkpoint reset " + cp.Id);
                    Assert.AreEqual(before + 1, run.ResetCount);
                    Assert.Less(Vector3.Distance(p1.transform.position, cp.transform.position), 8);
                    foreach (var transit in All<BombTransit>()) Assert.AreEqual(-1, transit.ActiveExit);
                    foreach (var actuator in All<SignalActuator>()) Assert.Less(actuator.CurrentProgress, 0.1f, actuator.name);
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
            }
            finally { RunOptions.StartCheckpoint = 0; }
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
                    yield return WaitUntil(() => bomb.State == BombState.Thrown, transit.Delay + 2, "transit release " + transit.name);
                    yield return CatchWhenNear(p2);
                    yield return WaitUntil(() => bomb.Carrier == p2 || bomb.State == BombState.Exploding, 3, "exit catch " + transit.name);
                    Assert.AreSame(p2, bomb.Carrier, transit.name + " exit " + i + " must reach the receiver without hitting the roof");
                }
        }
    }
}
