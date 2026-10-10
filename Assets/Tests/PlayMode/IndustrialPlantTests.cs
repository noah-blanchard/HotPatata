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
    /// ARCHITECTURE §25.2 (issue #87): the industrial plant is a full, closed, dark, playable course drawn only in the industrial look:
    /// every obstacle of the kit works (the enclosed-course rules), the route climbs, drops and turns, and the building has no opening.
    /// </summary>
    public class IndustrialPlantTests : SandboxTestBase
    {
        protected override string SceneName => "IndustrialPlant";
        static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None);

        [Test]
        public void Route_HasNineCheckpoints_AFinish_AndOneBomb_ClimbsDropsAndSpreads()
        {
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 9), All<Checkpoint>().Select(c => c.Id));
            Assert.AreEqual(1, All<FinishZone>().Length);
            Assert.AreEqual(1, All<BombController>().Length);
            var points = All<Checkpoint>().OrderBy(c => c.Id).Select(c => c.transform.position).Append(All<FinishZone>().Single().transform.position).ToArray();
            Assert.GreaterOrEqual(points.Max(p => p.y) - points.Min(p => p.y), 30f, "verticality");
            Assert.GreaterOrEqual(points.Max(p => p.x) - points.Min(p => p.x), 100f, "turns");
            Assert.GreaterOrEqual(points.Max(p => p.z) - points.Min(p => p.z), 100f, "turns");
            Assert.Greater(points[8].y, points[0].y + 15f, "the finish is high");
            Assert.Less(points.Min(p => p.y), points[0].y - 5f, "the route descends below the start");
        }

        [Test]
        public void EveryObstacleOfTheKit_IsPresent()
        {
            Assert.GreaterOrEqual(All<MovingPlatform>().Length, 7, "crane, press, rising platforms, crushers");
            Assert.GreaterOrEqual(All<Conveyor>().Length, 2);
            Assert.GreaterOrEqual(All<RotatingObstacle>().Length, 2, "sweeper, windmill");
            Assert.GreaterOrEqual(All<BombGate>().Length, 4);
            Assert.GreaterOrEqual(All<TransitMouth>().Length, 4, "tubes and the cannon");
            Assert.GreaterOrEqual(All<PressurePlate>().Length, 1);
            Assert.GreaterOrEqual(All<SignalActuator>().Length, 4, "doors, lift");
            Assert.GreaterOrEqual(All<KillZone>().Length, 4);
            var kinds = All<FuseZone>().Select(z => new SerializedObject(z).FindProperty("kind").enumValueIndex).Distinct().Count();
            Assert.AreEqual(3, kinds, "forbidden, hot and cold zones");
        }

        [Test]
        public void TheBuilding_HasNoOpening_FromAnyPointOfTheRoute()
        {
            Physics.SyncTransforms();
            var points = All<PassCorridor>().Select(p => p.transform.position)
                .Concat(All<Checkpoint>().Select(c => c.transform.position + Vector3.up * 1.7f))
                .Concat(All<FinishZone>().Select(f => f.transform.position + Vector3.up * 1.7f)).ToList();
            Assert.GreaterOrEqual(points.Count, 30);
            int mask = LayerMask.GetMask("Environment");
            var dirs = new List<Vector3>();
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                        if (x != 0 || y != 0 || z != 0) dirs.Add(new Vector3(x, y, z).normalized);
            var leaks = new List<string>();
            foreach (var p in points)
                foreach (var d in dirs)
                    if (!Physics.Raycast(p, d, 400f, mask, QueryTriggerInteraction.Ignore)) leaks.Add($"{p} towards {d}");
            Assert.IsEmpty(leaks.Take(10).ToArray(), "a ray escapes the building");
        }

        [Test]
        public void IntendedPasses_ClearCeilings_Solids_AndDecoration()
        {
            Physics.SyncTransforms();
            var points = new List<Vector3>();
            var errors = new List<string>();
            var decorations = All<CourseDecoration>().SelectMany(d => d.GetComponentsInChildren<Renderer>()).ToArray();
            Assert.GreaterOrEqual(All<PassCorridor>().Length, 18);
            Assert.Greater(decorations.Length, 300);
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
            foreach (var a in actuators) Assert.IsNotNull(a.Source, a.name);
            foreach (var plate in All<PressurePlate>()) Assert.AreEqual(1, actuators.Count(a => ReferenceEquals(a.Source, plate)), plate.name);
            foreach (var gate in All<BombGate>())
                Assert.AreEqual(1, actuators.Count(a => ReferenceEquals(a.Source, gate)) + All<Checkpoint>().Count(c => c.ClaimGate == gate), gate.name);
            foreach (var mouth in All<TransitMouth>())
            {
                var so = new SerializedObject(mouth);
                var transit = (BombTransit)so.FindProperty("transit").objectReferenceValue;
                Assert.IsNotNull(transit);
                var exit = transit.GetExit(so.FindProperty("exit").intValue);
                Assert.IsNotNull(exit.muzzle); Assert.IsNotNull(exit.pad); Assert.IsNotNull(exit.hold);
                Assert.IsTrue(exit.pad.gameObject.activeInHierarchy);
                Assert.IsTrue(Physics.Raycast(exit.pad.position + Vector3.up, Vector3.down, 2, LayerMask.GetMask("Environment")), "No floor under " + transit.name);
            }
        }

        [Test]
        public void TheLook_IsLitOnlyByWarmLamps_WithNoSun_AndExposedForIndoors()
        {
            float Luma(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            var fill = RenderSettings.ambientSkyColor;
            Assert.Greater(Luma(fill), 0.45f, "a bounce fill bright enough that no floor goes black between two lamps");
            Assert.Less(Mathf.Abs(fill.r - fill.b), 0.1f, "a neutral fill: the lamps keep their colour");
            Assert.Less(Luma(RenderSettings.fogColor), 0.3f);
            Assert.IsTrue(All<UnityEngine.Rendering.Volume>().Any(v => v.isGlobal && v.priority >= 1f && v.sharedProfile != null
                && v.sharedProfile.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments c) && c.postExposure.overrideState && c.postExposure.value > 0.5f),
                "the plant's own exposure volume");
            foreach (var sun in All<Light>().Where(l => l.type == LightType.Directional))
            {
                Assert.AreEqual(0f, sun.intensity, "a sun's shadows depend on the camera: none under a roof");
                Assert.AreEqual(LightShadows.None, sun.shadows);
            }
            var lights = All<Light>().Where(l => l.type == LightType.Point).ToArray();
            Assert.GreaterOrEqual(lights.Length, 90);
            Assert.GreaterOrEqual(lights.Count(l => l.color.r > l.color.b * 1.5f), 70, "warm lamps");
            Assert.IsTrue(lights.All(l => l.shadows == LightShadows.None), "no light casts a moving shadow");
        }

        [Test]
        public void EverySurface_IsIndustrial_AndBevelledVisualsStayInTheirColliders()
        {
            Physics.SyncTransforms();
            var names = All<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).ToHashSet();
            foreach (var name in new[] { "Industrial_PaintedMetal", "Industrial_PaintedMetal_Yellow", "Industrial_Rubber",
                                         "Industrial_Lamp", "Industrial_Glow", "Industrial_Hazard", "Industrial_Pipe_1", "Industrial_Pipe_2" })
                Assert.IsTrue(names.Contains(name), name + " is not used");
            var cartoon = All<Renderer>().Where(r => r.sharedMaterials.Any(m => m != null && (m.name.StartsWith("KayKit_") || m.name.StartsWith("Greybox_"))))
                .Where(r => r.GetComponentInParent<PressurePlate>() == null).Select(r => r.name).ToArray();
            CollectionAssert.IsEmpty(cartoon, "no cartoon material (the plate's rim is a gameplay cue)");
            int boxes = 0;
            foreach (var skin in All<KitSkin>().Where(s => s.Shape == KitShape.BevelBox))
            {
                // a kit block: its own parent carries the solid box the visual stands for (gate rings, pipes and tubes are other shapes)
                var collider = skin.transform.parent != null ? skin.transform.parent.GetComponent<BoxCollider>() : null;
                if (collider == null || collider.isTrigger) continue;
                var visual = skin.GetComponent<Renderer>().bounds;
                var box = collider.bounds;
                for (int a = 0; a < 3; a++)
                {
                    Assert.LessOrEqual(visual.max[a], box.max[a] + 1e-3f, skin.name + " axis " + a);
                    Assert.GreaterOrEqual(visual.min[a], box.min[a] - 1e-3f, skin.name + " axis " + a);
                }
                boxes++;
            }
            Assert.Greater(boxes, 100);
        }

        [Test]
        public void SurfacesAreDiverse_FloorsWallsAndCeilings_AndDecalsBreakTheRepetition()
        {
            Physics.SyncTransforms();
            var names = All<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).ToHashSet();
            Assert.GreaterOrEqual(names.Count(n => n.StartsWith("Industrial_Floor_")), 4, "four kinds of floor");
            Assert.GreaterOrEqual(names.Count(n => n.StartsWith("Industrial_Wall_")), 4, "four kinds of wall");
            Assert.GreaterOrEqual(names.Count(n => n.StartsWith("Industrial_Ceiling_")), 2, "two kinds of ceiling");
            Assert.IsTrue(names.Contains("Industrial_Metal_Rust"));
            var decals = All<Renderer>().Where(r => r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Decal_")).ToArray();
            Assert.GreaterOrEqual(decals.Length, 150, "random stains, cracks, oil, scuffs and drips");
            Assert.GreaterOrEqual(decals.Select(r => r.sharedMaterial.name).Distinct().Count(), 3);
            foreach (var d in decals)
            {
                Assert.IsNull(d.GetComponent<Collider>(), "a decal never has a collider");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, d.shadowCastingMode);
                Assert.IsNotNull(d.GetComponentInParent<CourseDecoration>());
                var normal = -d.transform.forward;
                Assert.IsTrue(Physics.Raycast(d.transform.position + normal * 0.05f, -normal, out var under, 0.2f, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore),
                    d.name + " floats in the air");
                Assert.IsTrue(under.collider.attachedRigidbody == null && under.collider.GetComponentInParent<MovingPlatform>() == null
                              && under.collider.GetComponentInParent<FallingPlatform>() == null && under.collider.GetComponentInParent<SignalActuator>() == null,
                    d.name + " lies on something that moves: it would be left in the air");
            }
        }

        [Test]
        public void Details_LineEveryRoom_AsCollidersFreeDecoration_NeverOnAMover()
        {
            var groups = All<Transform>().Where(t => t.name == "Details" && t.GetComponent<CourseDecoration>() != null).ToArray();
            Assert.AreEqual(11, groups.Length, "one detail group per room");
            foreach (var g in groups)
            {
                Assert.Greater(g.childCount, 50, g.parent.name + " has its mouldings and props");
                Assert.IsEmpty(g.GetComponentsInChildren<Collider>(), g.parent.name + ": details never collide");
                Assert.IsNull(g.GetComponentInParent<MovingPlatform>());
            }
            var names = groups.SelectMany(g => g.Cast<Transform>()).Select(t => t.name).ToHashSet();
            foreach (var kind in new[] { "Baseboard", "Cornice", "Pilaster", "Panel frame", "Edge angle", "Door leaf", "Window glass", "Wall pipe", "Cabinet",
                                         "Cable tray", "Barrel", "Pallet deck", "Bench top", "Zone number" })
                Assert.IsTrue(names.Contains(kind), "the plant has a " + kind.ToLowerInvariant());
        }

        [Test]
        public void TheMenu_ListsTheWildsTheCanopyTheTempleThePlantAndTheSandbox_WithTheirSpawnChoices()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Network/NetworkManager.prefab");
            var so = new SerializedObject(prefab.GetComponent<NetworkBootstrap>());
            var names = so.FindProperty("gameplayScenes");
            var counts = so.FindProperty("sceneCheckpoints");
            var mins = so.FindProperty("sceneMinPlayers");
            CollectionAssert.AreEqual(new[] { "PatataWilds", "PatataCanopy", "PatataTemple", "IndustrialPlant", "PassSandbox" },
                Enumerable.Range(0, names.arraySize).Select(i => names.GetArrayElementAtIndex(i).stringValue).ToArray());
            Assert.AreEqual(9, counts.GetArrayElementAtIndex(3).intValue, "nine spawn choices in the plant");
            CollectionAssert.AreEqual(new[] { 2, 2, 3, 2, 2 }, Enumerable.Range(0, mins.arraySize).Select(i => mins.GetArrayElementAtIndex(i).intValue).ToArray(),
                                      "only the temple needs three players");
            foreach (var scene in new[] { "Bootstrap", "PatataWilds", "PatataCanopy", "PatataTemple", "IndustrialPlant", "PassSandbox" })
                Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == $"Assets/Scenes/{scene}.unity"), scene + " is in the build");
            Assert.AreEqual(6, EditorBuildSettings.scenes.Length, "no other scene is in the build");
        }

        [Test]
        public void FloorsOfTwoRooms_NeverOverlapAtTheSameHeight()
        {
            // Two coplanar slabs of different rooms (different themes) would fight over one surface and flicker.
            Physics.SyncTransforms();
            var slabs = new List<(Transform room, Bounds bounds, string name)>();
            foreach (var c in All<BoxCollider>())
            {
                if (c.isTrigger || c.attachedRigidbody != null || c.bounds.size.y > 1.5f) continue;
                var t = c.transform;
                while (t != null && (t.parent == null || t.parent.name != "IndustrialPlant")) t = t.parent;
                if (t != null) slabs.Add((t, c.bounds, c.name));
            }
            Assert.Greater(slabs.Count, 50);
            var overlaps = new List<string>();
            for (int i = 0; i < slabs.Count; i++)
                for (int j = i + 1; j < slabs.Count; j++)
                {
                    var a = slabs[i]; var b = slabs[j];
                    if (a.room == b.room || Mathf.Abs(a.bounds.max.y - b.bounds.max.y) > 0.02f) continue;
                    float ox = Mathf.Min(a.bounds.max.x, b.bounds.max.x) - Mathf.Max(a.bounds.min.x, b.bounds.min.x);
                    float oz = Mathf.Min(a.bounds.max.z, b.bounds.max.z) - Mathf.Max(a.bounds.min.z, b.bounds.min.z);
                    if (ox > 0.05f && oz > 0.05f) overlaps.Add($"{a.room.name}/{a.name} and {b.room.name}/{b.name}");
                }
            Assert.IsEmpty(overlaps.Take(10).ToArray());
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
                    bomb.Explode(BombFailReason.HoldFuseExpired, "IndustrialPlant reset regression");
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
