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
    /// PROJECT_SPEC §15c (PatataWilds, M12): structure, rules and look regressions of the outdoor course. Teleport-based tests do
    /// not certify a human clear (the 20+ minute two-player clear stays a human gate).
    /// </summary>
    public class PatataWildsTests : SandboxTestBase
    {
        protected override string SceneName => "PatataWilds";
        static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None);

        [Test]
        public void Route_FiveActs_25Checkpoints_ClimbsToTheSummit()
        {
            var route = All<CourseRoute>().Single();
            var points = route.Points;
            Assert.Greater(points.Max(p => p.x) - points.Min(p => p.x), 250);
            Assert.Greater(points.Max(p => p.z) - points.Min(p => p.z), 250);
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
                        .Any(h => h.collider.GetComponentInParent<CourseCeiling>() != null), "No cave roof at " + midpoint);
                }
            }
            Assert.GreaterOrEqual(left + right, 10);
            Assert.Greater(left, 3); Assert.Greater(right, 3);
            Assert.GreaterOrEqual(climb, 90); Assert.GreaterOrEqual(drop, 20);
            var checkpoints = All<Checkpoint>().OrderBy(c => c.Id).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(1, 25), checkpoints.Select(c => c.Id));
            Assert.AreEqual(1, All<FinishZone>().Length);
            Assert.AreEqual(1, All<BombController>().Length);
            var finish = All<FinishZone>().Single().transform.position;
            Assert.GreaterOrEqual(finish.y, checkpoints.Max(c => c.transform.position.y) - 0.5f, "the finish is the summit");
            // checkpoints every section: a stand-in for 30-60 s apart (spec §12.3), measured along the ground
            for (int i = 1; i < checkpoints.Length; i++)
            {
                var a = checkpoints[i - 1].transform.position;
                var b = checkpoints[i].transform.position;
                float d = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                Assert.That(d, Is.InRange(20f, 150f), $"checkpoints {i} and {i + 1} are {d:F0} m apart");
            }
        }

        [Test]
        public void FuseOverride_StartsAt21_AndTightensAt24()
        {
            foreach (var cp in All<Checkpoint>())
            {
                float expected = cp.Id >= 24 ? 4.5f : cp.Id >= 21 ? 5f : 0f;
                Assert.AreEqual(expected, cp.HoldFuseOverride, 1e-4f, "checkpoint " + cp.Id);
            }
        }

        [Test]
        public void IntendedPasses_ClearCeilings_Solids_Decoration_AndScatter()
        {
            Physics.SyncTransforms();
            var points = new List<Vector3>();
            var errors = new List<string>();
            var decorations = All<CourseDecoration>().SelectMany(d => d.GetComponentsInChildren<Renderer>()).ToArray();
            var scatter = All<FoliageInstancer>().SelectMany(f => f.InstanceBounds()).ToArray();
            Assert.Greater(scatter.Length, 1000, "the forest and the ground cover are scattered");
            Assert.GreaterOrEqual(All<PassCorridor>().Length, 50);
            foreach (var pass in All<PassCorridor>())
            {
                Assert.IsTrue(pass.TrySample(tuning, points), pass.name + " is unreachable with throw tuning");
                if (pass.Opening > 0) Assert.GreaterOrEqual(pass.Opening, tuning.catchRadius + PassCorridor.OpeningMargin, pass.name);
                if (pass.Arc == PassCorridor.ArcKind.Normal)
                {
                    float length = Vector2.Distance(new Vector2(pass.From.x, pass.From.z), new Vector2(pass.To.x, pass.To.z));
                    Assert.LessOrEqual(length, 14f, pass.name + " is longer than a normal pass (spec §14)");
                }
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
                    foreach (var b in scatter)
                        if (b.SqrDistance(point) < PassCorridor.DecorationClearance * PassCorridor.DecorationClearance)
                            errors.Add(pass.name + " plant at " + b.center);
                }
            }
            Assert.IsEmpty(errors.Distinct().Take(20).ToArray());
        }

        [Test]
        public void Signals_AreOneToOne_AndEveryTransitHasAReceiver()
        {
            var actuators = All<SignalActuator>();
            Assert.GreaterOrEqual(actuators.Length, 8);
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

        [Test]
        public void Checkpoints_AreCampfires_NotPads()
        {
            var fires = All<CampfirePresentation>();
            foreach (var cp in All<Checkpoint>())
            {
                var so = new SerializedObject(cp);
                Assert.IsNull(so.FindProperty("padRenderer").objectReferenceValue, cp.name + " still paints a pad");
                foreach (var r in cp.GetComponentsInChildren<Renderer>()) Assert.IsFalse(r.enabled && r.gameObject.activeInHierarchy, cp.name + " shows " + r.name);
                AssertCampfireMarks(fires.Single(f => f.Checkpoint == cp), cp);
            }
            Assert.AreEqual(1, fires.Count(f => f.Checkpoint == null), "one summit beacon");
        }

        [UnityTest]
        public IEnumerator Campfire_CatchesWhenReached_AndGoesOutOnRestart()
        {
            var run = RunManager.Instance;
            var cp = All<Checkpoint>().Single(c => c.Id == 1);
            var fire = All<CampfirePresentation>().Single(f => f.Checkpoint == cp);
            yield return null;
            Assert.IsFalse(fire.Lit);
            yield return GatherAtCampfire(fire, cp);   // the team gathers at the fire, the only mark on the ground
            yield return WaitUntil(() => cp.Activated, 2, "checkpoint 1 activation");
            yield return WaitUntil(() => fire.Lit && fire.Lighting >= 1f, tuning.campfireIgniteSeconds + 2, "the campfire catches");
            RunOptions.StartCheckpoint = 0;
            run.Restart();
            yield return null;
            yield return null;
            Assert.IsFalse(fire.Lit, "a new run puts the fire out");
        }

        [Test]
        public void Water_IsLethal_EverywhereItShows()
        {
            var waters = All<NatureWater>();
            Assert.Greater(waters.Length, 5);
            foreach (var water in waters)
            {
                var bounds = water.GetComponent<Renderer>().bounds;
                for (float u = 0.1f; u < 1f; u += 0.2f)
                    for (float v = 0.1f; v < 1f; v += 0.2f)
                    {
                        var p = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, u), bounds.max.y + 0.05f, Mathf.Lerp(bounds.min.z, bounds.max.z, v));
                        var hits = Physics.RaycastAll(p, Vector3.down, 0.6f, ~0, QueryTriggerInteraction.Collide);
                        bool killed = hits.Any(h => h.collider.GetComponent<KillZone>() != null);
                        bool covered = Physics.Raycast(p + Vector3.up * 2f, Vector3.down, 2.04f, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore);
                        Assert.IsTrue(killed || covered, $"{water.name}: no kill zone under the surface at {p}");
                    }
            }
        }

        [UnityTest]
        public IEnumerator TimeOfDay_FollowsTheCheckpoint_SnapsOnPracticeStartAndRestart()
        {
            var run = RunManager.Instance;
            var blender = All<TimeOfDayBlender>().Single();
            Assert.AreEqual(5, blender.Presets.Count);
            yield return null;
            Assert.AreEqual(0f, blender.Current, 1e-3f, "the run starts at dawn");
            try
            {
                RunOptions.StartCheckpoint = 13;
                run.Restart();
                yield return null;
                yield return null;
                Assert.Greater(blender.Target, 1.5f);
                Assert.AreEqual(blender.Target, blender.Current, 1e-3f, "a practice start shows its hour at once");
                RunOptions.StartCheckpoint = 0;
                run.Restart();
                yield return null;
                yield return null;
                Assert.AreEqual(0f, blender.Current, 1e-3f, "a restart goes back to dawn at once");
            }
            finally { RunOptions.StartCheckpoint = 0; }
        }

        [Test]
        public void NoMeshColliders_DecorationNeverCollides_VisualsStayInTheirBoxes()
        {
            Assert.IsEmpty(All<MeshCollider>().Select(c => c.name).ToArray());
            foreach (var d in All<CourseDecoration>()) Assert.IsEmpty(d.GetComponentsInChildren<Collider>().Select(c => c.name).ToArray(), d.name);
            foreach (var t in All<NatureTerrain>()) Assert.IsNull(t.GetComponentInChildren<Collider>(), t.name);
            foreach (var w in All<NatureWater>()) Assert.IsNull(w.GetComponent<Collider>(), w.name);
            var errors = new List<string>();
            foreach (var skin in All<KitSkin>())
            {
                if (!KitSkin.IsGenerated(skin.Shape) || skin.Shape == KitShape.Skirt) continue;   // a skirt is decoration under a slab
                var box = skin.GetComponent<BoxCollider>() ?? skin.transform.parent?.GetComponent<BoxCollider>()
                          ?? skin.transform.parent?.Find("Collision")?.GetComponent<BoxCollider>();
                if (box == null) continue;
                var r = skin.GetComponent<Renderer>().bounds;
                var b = box.bounds;
                // rock sides may bulge 30 cm out (turned boxes: their world bounds grow too); nothing rises above the walked-on top
                bool rock = skin.Shape == KitShape.RoughBox || skin.Shape == KitShape.Crag;
                var side = b;
                side.Expand(new Vector3(rock ? 0.9f : 0.12f, rock ? 0.9f : 0.12f, rock ? 0.9f : 0.12f));
                if (!side.Contains(r.min) || !side.Contains(r.max) || r.max.y > b.max.y + 0.05f) errors.Add(skin.transform.parent?.name + "/" + skin.name);
            }
            Assert.IsEmpty(errors.Take(20).ToArray(), "visuals outside their collider");
        }

        [Test]
        public void Hazards_AreStriped_NotColourAlone()
        {
            foreach (var c in All<Collider>().Where(c => c.gameObject.layer == LayerMask.NameToLayer("Hazard") && !c.isTrigger))
            {
                var visual = c.transform.parent != null ? c.transform.parent.Find("Visual") : null;
                if (visual == null) visual = c.transform.Find("Visual");
                var r = visual != null ? visual.GetComponent<Renderer>() : c.GetComponent<Renderer>();
                if (r == null) continue;
                Assert.Greater(r.sharedMaterial.GetFloat("_StripeStrength"), 0.5f, c.transform.parent?.name + "/" + c.name);
            }
        }

        [Test]
        public void Ambience_Is3D_OnTheEffectsGroup_UnderTheBeeps_NoMovementSounds()
        {
            var sources = All<AudioSource>().Where(a => a.GetComponent<AmbientLoop>() != null || a.GetComponentInParent<CampfirePresentation>() != null).ToArray();
            Assert.Greater(sources.Length, 25);
            foreach (var a in sources)
            {
                Assert.AreEqual(1f, a.spatialBlend, a.name);
                Assert.GreaterOrEqual(a.priority, 128, a.name);
                Assert.IsNotNull(a.outputAudioMixerGroup, a.name);
                Assert.IsNotNull(a.clip, a.name);
                string clip = a.clip.name.ToLowerInvariant();
                Assert.IsFalse(clip.Contains("step") || clip.Contains("land") || clip.Contains("slide") || clip.Contains("foot"), clip);
            }
        }

        [Test]
        public void Bootstrap_ListsPatataWildsFirst_With25SpawnChoices()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Network/NetworkManager.prefab");
            var so = new SerializedObject(prefab.GetComponent<NetworkBootstrap>());
            Assert.AreEqual("PatataWilds", so.FindProperty("gameplayScenes").GetArrayElementAtIndex(0).stringValue);
            Assert.AreEqual(25, so.FindProperty("sceneCheckpoints").GetArrayElementAtIndex(0).intValue);
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/PatataWilds.unity")));
        }

        [UnityTest]
        public IEnumerator HoldTheRope_TwoPlayersPassFromThePlate_AndOpenTheReturnGate()
        {
            var plate = All<PressurePlate>().Single(p => p.name == "Rope plate");
            var lift = All<SignalActuator>().Single(a => ReferenceEquals(a.Source, plate));
            var ringPass = All<PassCorridor>().Single(c => c.name == "Pass Rope return ring");
            yield return Place(p1, plate.transform.position + Vector3.up * 0.1f);
            yield return Place(p2, ringPass.To - Vector3.up * 1.4f);
            Give(p1);
            yield return WaitUntil(() => lift.CurrentProgress > 0.9f, 4, "rope lift");
            ThrowAt(p1, p2, 0.55f);
            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => bomb.Carrier == p2, 2, "two-player rope catch");
            var door = All<SignalActuator>().Single(a => a.name == "Return gate");
            yield return WaitUntil(() => door.CurrentProgress > 0.9f, 2, "return gate");
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
                    bomb.Explode(BombFailReason.HoldFuseExpired, "PatataWilds reset regression");
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
                    Assert.AreSame(p2, bomb.Carrier, transit.name + " exit " + i + " must reach the receiver without hitting the cliffs");
                }
        }
    }
}
