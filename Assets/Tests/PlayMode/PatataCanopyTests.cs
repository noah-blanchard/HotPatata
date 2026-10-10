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

        List<Vector3> PassSamples()
        {
            var all = new List<Vector3>();
            var points = new List<Vector3>();
            foreach (var pass in All<PassCorridor>())
                if (pass.TrySample(tuning, points)) all.AddRange(points);
            return all;
        }

        static IEnumerable<(string batch, Vector3 position, float scale, Bounds box)> Instances()
        {
            foreach (var instancer in All<FoliageInstancer>())
                foreach (var batch in instancer.Set.batches)
                {
                    if (batch.lods.Length == 0 || batch.lods[0] == null) continue;
                    var local = batch.LocalBounds(0);
                    foreach (var cell in batch.cells)
                        for (int i = 0; i < cell.Count; i++)
                        {
                            var (position, yaw, scale) = FoliageSet.Unpack(batch, cell, i);
                            var m = Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
                            var box = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
                            for (int c = 0; c < 8; c++)
                                box.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
                            yield return (batch.name, position, scale, box);
                        }
                }
        }

        [Test]
        public void Forest_GiantsAndPlants_StayClearOfEveryPass()
        {
            var samples = PassSamples();
            var instances = Instances().ToList();
            Assert.Greater(instances.Count(t => t.batch.StartsWith("OakCrown") || t.batch.StartsWith("RedwoodCrown")), 1500, "a forest of giants (ARCHITECTURE §4)");
            var cells = new Dictionary<(int, int, int), List<Vector3>>();
            foreach (var p in samples)
            {
                var key = (Mathf.FloorToInt(p.x / 4f), Mathf.FloorToInt(p.y / 4f), Mathf.FloorToInt(p.z / 4f));
                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Vector3>();
                list.Add(p);
            }
            var errors = new List<string>();
            float clear = PassCorridor.DecorationClearance;
            foreach (var (batch, _, _, box) in instances)
            {
                var min = box.min - Vector3.one * clear;
                var max = box.max + Vector3.one * clear;
                bool hit = false;
                for (int x = Mathf.FloorToInt(min.x / 4f); x <= Mathf.FloorToInt(max.x / 4f) && !hit; x++)
                    for (int y = Mathf.FloorToInt(min.y / 4f); y <= Mathf.FloorToInt(max.y / 4f) && !hit; y++)
                        for (int z = Mathf.FloorToInt(min.z / 4f); z <= Mathf.FloorToInt(max.z / 4f) && !hit; z++)
                            if (cells.TryGetValue((x, y, z), out var list) && list.Any(p => box.SqrDistance(p) < clear * clear)) hit = true;
                if (hit) errors.Add(batch + " at " + box.center);
            }
            Assert.IsEmpty(errors.Take(20).ToArray());
        }

        [Test]
        public void DeckTrunks_ReachTheForestFloor_UnderTheirDecks_AndTheFloorFollowsTheCourse()
        {
            var mist = All<MistField>().Single();
            var trunks = Instances().Where(t => t.batch.StartsWith("DeckTrunk")).ToList();
            Assert.Greater(trunks.Count, 60, "the posts under the decks are trunks");
            foreach (var (_, position, scale, _) in trunks)
            {
                Assert.AreEqual(mist.FloorAt(position), position.y, 5f, "a deck trunk stands on the forest floor at " + position);
                var top = position + Vector3.up * (50f * scale - 1f);
                Assert.IsTrue(Physics.Raycast(top, Vector3.up, 3f, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore), "a deck trunk ends under its deck at " + top);
            }
            foreach (var cp in All<Checkpoint>())
            {
                float below = cp.transform.position.y - mist.FloorAt(cp.transform.position);
                Assert.That(below, Is.InRange(30f, 60f), "the forest floor lies about 40 m under checkpoint " + cp.Id);
            }
            var net = All<KillZone>().Single(k => k.name == "Safety net").GetComponent<Collider>().bounds;
            var floors = Instances().Where(t => t.batch.StartsWith("DeckTrunk")).Select(t => t.position.y);
            Assert.Less(net.max.y, floors.Min(), "the safety net lies under the forest floor");
        }

        [Test]
        public void LeafRoofs_NothingHangsUnderTheirUnderside_TheirMatReadsSolid()
        {
            var roofs = All<CourseCeiling>().Select(c => c.GetComponent<Collider>().bounds).ToList();
            Assert.Greater(roofs.Count, 10);
            foreach (var (batch, position, _, box) in Instances().Where(t => t.batch.StartsWith("CanopyMat") || t.batch.StartsWith("CanopyTuft")))
            {
                // its own roof: the one it stands in (from the underside up)
                var own = roofs.Where(r => position.x >= r.min.x - 0.1f && position.x <= r.max.x + 0.1f && position.z >= r.min.z - 0.1f && position.z <= r.max.z + 0.1f
                                           && position.y >= r.min.y - 0.01f && position.y <= r.max.y + 0.01f).ToList();
                Assert.IsNotEmpty(own, batch + " stands in a leaf roof at " + position);
                Assert.GreaterOrEqual(box.min.y, own[0].min.y - 0.01f, batch + " hangs under its roof at " + position);
                // and never reaches under another roof's underside, where the passes are
                foreach (var r in roofs)
                    if (!own.Contains(r) && box.max.x > r.min.x && box.min.x < r.max.x && box.max.z > r.min.z && box.min.z < r.max.z)
                        Assert.IsFalse(box.min.y < r.min.y - 0.01f && box.max.y > r.min.y - PassCorridor.CeilingMargin, batch + " reaches under another roof at " + position);
            }
        }

        [Test]
        public void Mist_LiesOnTheForestFloor_ThickAtDawn_ClearAtDeckHeight()
        {
            var mist = All<MistField>().Single();
            Assert.IsNotNull(mist.Floor);
            var day = All<TimeOfDayBlender>().Single().Presets;
            Assert.AreEqual(5, day.Count);
            StringAssert.Contains("/Canopy/", AssetDatabase.GetAssetPath(day[0]), "PatataCanopy has its own day (PatataCanopyLook)");
            foreach (var p in day)
            {
                // the mist over a 14.5 m pass at deck height, 40 m over the floor: barely there
                float atDeck = p.mistDensity * Mathf.Exp(-40f / p.mistFalloff) * 14.5f;
                Assert.Less(1f - Mathf.Exp(-atDeck), 0.15f, p.name + ": the mist veils the longest pass by less than 15 %");
            }
        }

        [Test]
        public void LightShafts_StayClearOfEveryPass_WhateverTheTimeOfDay()
        {
            var samples = PassSamples();
            var shafts = All<MeshRenderer>().Where(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == "HotPatata/LightShaft").ToList();
            Assert.Greater(shafts.Count, 80, "god rays hang from the crowns beside the course");
            float vertical = shafts[0].sharedMaterial.GetFloat("_Vertical");
            var axes = All<TimeOfDayBlender>().Single().Presets.Select(p => (p.SunRotation * Vector3.forward + Vector3.down * vertical).normalized).ToList();
            var errors = new List<string>();
            foreach (var r in shafts)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                float length = r.transform.lossyScale.y, radius = mesh.bounds.size.x > 0 ? mesh.vertices.Max(v => Mathf.Abs(v.x)) * length * 1.35f : 0f;
                var top = r.transform.position;
                foreach (var axis in axes)
                    foreach (var p in samples)
                    {
                        float t = Mathf.Clamp(Vector3.Dot(p - top, axis), 0f, length);
                        if ((top + axis * t - p).magnitude < radius + PassCorridor.DecorationClearance) { errors.Add(r.name + " at " + top); break; }
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
