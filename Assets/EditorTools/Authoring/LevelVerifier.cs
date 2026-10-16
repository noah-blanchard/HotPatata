using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>One thing Verify Level found: how bad, what, and the object (or the place) it is about.</summary>
    public struct LevelFinding
    {
        public enum Severity { Error, Warning, Info }

        public Severity severity;
        public string message;
        public Object context;
        public Vector3? at;

        public static LevelFinding Error(string message, Object context = null, Vector3? at = null) =>
            new LevelFinding { severity = Severity.Error, message = message, context = context, at = at };

        public static LevelFinding Warning(string message, Object context = null, Vector3? at = null) =>
            new LevelFinding { severity = Severity.Warning, message = message, context = context, at = at };

        public static LevelFinding Info(string message) => new LevelFinding { severity = Severity.Info, message = message };
    }

    /// <summary>
    /// Verify Level (ARCHITECTURE §27, docs/LEVEL_AUTHORING.md): everything an authored level must hold before it is official. Errors
    /// keep it out of the menu; warnings are worth a look; a line of info gives its budget.
    /// <list type="bullet">
    /// <item>the course: a start, checkpoints numbered 1..n without gaps (campfires), one finish, a contract per section (spec §13.20)
    /// that <see cref="CourseContractCheck"/> finds nothing in, the "With two" line for a level for three;</item>
    /// <item>the walks: the <see cref="WalkProbe"/> along every walkway and bridge, the passes (reachable, wide enough, no ceiling);</item>
    /// <item>the falls: a safety net, a kill zone under every water surface;</item>
    /// <item>the rules: gameplay objects under the section root, decoration off the paths and passes, pieces built;</item>
    /// <item>the menu: listed with its checkpoint count and players; and the budget (triangles, plants, generated meshes).</item>
    /// </list>
    /// </summary>
    public static class LevelVerifier
    {
        /// <summary>Over these a level is heavy: drawn triangles of its generated pieces, planted instances, generated mesh memory (MB).</summary>
        public const int TriangleBudget = 3_000_000, PlantBudget = 250_000;
        public const float MeshBudgetMb = 400f;

        public static List<LevelFinding> Run(Scene scene)
        {
            var findings = new List<LevelFinding>();
            Physics.SyncTransforms();
            var settings = LevelScenes.SettingsOf(scene);
            if (settings == null)
            {
                findings.Add(LevelFinding.Error("Not an authored level: no LevelSettings (make levels with HotPatata/Level/New Level)."));
                return findings;
            }
            void Guard(string what, Action check)
            {
                try { check(); }
                catch (Exception e) { findings.Add(LevelFinding.Error($"{what} could not be checked: {e.Message}")); }
            }
            Guard("The course", () => Course(scene, settings, findings));
            Guard("The contracts", () => Contracts(scene, findings));
            Guard("The walks", () => Walks(scene, findings));
            Guard("The passes", () => Passes(findings));
            Guard("The falls", () => Falls(scene, findings));
            Guard("The rules", () => Rules(scene, findings));
            Guard("The timber", () => Timber(scene, findings));
            Guard("The menu", () => Menu(scene, settings, findings));
            Guard("The budget", () => Budget(scene, findings));
            return findings;
        }

        // ------------------------------------------------------------------ the course

        static void Course(Scene scene, LevelSettings settings, List<LevelFinding> findings)
        {
            var section = LevelRoutes.SectionRoot(scene);
            if (section == null) findings.Add(LevelFinding.Error("No start: the level has no start spawns."));
            var checkpoints = LevelCourse.Checkpoints(scene).OrderBy(c => c.Id).ToList();
            if (checkpoints.Count == 0) findings.Add(LevelFinding.Warning("No checkpoint: the whole level is one section (GameObject > HotPatata Level > Checkpoint)."));
            foreach (var group in checkpoints.GroupBy(c => c.Id).Where(g => g.Count() > 1))
                findings.Add(LevelFinding.Error($"{group.Count()} checkpoints share the id {group.Key} (Number checkpoints on the Level object).", group.First()));
            var ids = checkpoints.Select(c => c.Id).Distinct().OrderBy(i => i).ToList();
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] != i + 1)
                {
                    findings.Add(LevelFinding.Error($"Checkpoint ids skip or start wrong ({string.Join(", ", ids)}): they must be 1, 2, 3... (Number checkpoints).",
                                                    checkpoints.FirstOrDefault(c => c.Id == ids[i])));
                    break;
                }
            if (checkpoints.Count > 1)
            {
                var start = LevelCourse.Start(scene);
                var walk = LevelCourse.WalkDistances(scene, start, checkpoints.Select(c => c.transform.position).ToList());
                for (int i = 1; i < checkpoints.Count; i++)
                    if (!float.IsInfinity(walk[i]) && !float.IsInfinity(walk[i - 1]) && walk[i] + 1f < walk[i - 1])
                        findings.Add(LevelFinding.Warning($"{checkpoints[i].name} (id {checkpoints[i].Id}) is reached before {checkpoints[i - 1].name}: number them along the course?",
                                                          checkpoints[i]));
            }
            foreach (var cp in checkpoints.Where(c => c.GetComponentInChildren<CampfirePresentation>(true) == null))
                findings.Add(LevelFinding.Warning($"{cp.name} is a pad, not a campfire (the nature courses' checkpoints are campfires).", cp));
            foreach (var cp in checkpoints)
            {
                var fire = LevelCourse.FireOf(cp);
                if (fire == null) continue;
                var off = fire.transform.position - cp.transform.position;
                float d = new Vector2(off.x, off.z).magnitude;
                if (d < 3f || d > 6f || Mathf.Abs(off.y) > 0.5f)
                    findings.Add(LevelFinding.Error($"{cp.name}'s campfire stands {d:0.0} m away and {off.y:+0.0;-0.0} m up: it goes 3 to 6 m beside its spawns, on their floor " +
                                                    "(Place the campfire beside it, on the checkpoint).", cp, fire.transform.position));
            }
            Spawns(scene, findings);
            var finishes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FinishZone>(true)).ToList();
            if (finishes.Count == 0) findings.Add(LevelFinding.Error("No finish (GameObject > HotPatata Level > Finish)."));
            if (finishes.Count > 1) findings.Add(LevelFinding.Error($"{finishes.Count} finishes: a level has one.", finishes[1]));
            if (finishes.Count == 1 && checkpoints.Count > 0)
            {
                var start = LevelCourse.Start(scene);
                var d = LevelCourse.WalkDistances(scene, start, checkpoints.Select(c => c.transform.position).Append(finishes[0].transform.position).ToList());
                if (!float.IsInfinity(d.Last()) && d.Take(d.Length - 1).Where(x => !float.IsInfinity(x)).Any(x => x > d.Last() + 1f))
                    findings.Add(LevelFinding.Warning("The finish is reached before the last checkpoint.", finishes[0]));
            }
        }

        /// <summary>
        /// Every spawn (the start's and each checkpoint's, spread round its fire) stands on something and has room for a body: a
        /// checkpoint too near a rail, a trunk or a deck's edge puts a player in the rail or in the air.
        /// </summary>
        public static void Spawns(Scene scene, List<LevelFinding> findings)
        {
            int solid = LayerMask.GetMask("Environment", "Hazard", BodyScreen.LayerName);
            foreach (var spawn in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerSpawn>(true)))
            {
                var p = spawn.transform.position;
                var cp = spawn.GetComponentInParent<Checkpoint>();
                string who = cp != null ? $"{cp.name}'s spawn {spawn.name}" : $"The start's spawn {spawn.name}";
                Object context = cp != null ? (Object)cp.gameObject : spawn.gameObject;
                bool floor = Physics.RaycastAll(p + Vector3.up * 1f, Vector3.down, 1.6f, solid, QueryTriggerInteraction.Ignore).Any(h => h.normal.y > 0.6f);
                if (!floor)
                {
                    findings.Add(LevelFinding.Error($"{who} stands on nothing (move it onto the deck, clear of its edge).", context, p));
                    continue;
                }
                var blocker = Physics.OverlapCapsule(p + Vector3.up * (WalkProbe.BodyRadius + 0.1f), p + Vector3.up * (WalkProbe.BodyHeight - WalkProbe.BodyRadius),
                                                     WalkProbe.BodyRadius, solid, QueryTriggerInteraction.Ignore)
                                     .FirstOrDefault(c => !(c is TerrainCollider) && c.GetComponent<WalkableProxy>() == null);
                if (blocker != null)
                    findings.Add(LevelFinding.Error($"{who} stands in {blocker.name} ({blocker.transform.parent?.name}): move it clear of rails and trunks.", context, p));
            }
        }

        // ------------------------------------------------------------------ the contracts

        static void Contracts(Scene scene, List<LevelFinding> findings)
        {
            var contracts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SectionContract>(true)).ToList();
            var sections = new[] { 0 }.Concat(LevelCourse.Checkpoints(scene).Select(c => c.Id)).Distinct().OrderBy(i => i);
            foreach (int section in sections)
                if (!contracts.Any(c => LevelCourse.SectionOf(c) == section))
                {
                    var cp = LevelCourse.Checkpoints(scene).FirstOrDefault(c => c.Id == section);
                    findings.Add(LevelFinding.Error(section == 0
                        ? "The start's section has no contract (Add the start's contract, on the Level object)."
                        : $"The section from {cp?.name} has no contract (Add this section's contract, on the checkpoint).", cp));
                }
            foreach (var contract in contracts.Where(c => string.IsNullOrWhiteSpace(c.Force) || c.Force.StartsWith("What this section forces")))
                findings.Add(LevelFinding.Warning("A contract does not say what its section forces yet.", contract));
            foreach (var problem in CourseContractCheck.Problems(scene, CourseContractCheck.Tuning))
                findings.Add(LevelFinding.Error(problem, ByPath(scene, problem)));
        }

        /// <summary>The object a contract check's finding names ("A/B/C: ..." or "A/B/C 'name': ...").</summary>
        static Object ByPath(Scene scene, string finding)
        {
            int cut = finding.IndexOfAny(new[] { ':', '\'' });
            if (cut <= 0) return null;
            var path = finding.Substring(0, cut).Trim().Split('/');
            foreach (var root in scene.GetRootGameObjects().Where(r => r.name == path[0]))
            {
                var t = root.transform;
                for (int i = 1; i < path.Length && t != null; i++) t = t.Cast<Transform>().FirstOrDefault(c => c.name == path[i]);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        // ------------------------------------------------------------------ the walks and passes

        static void Walks(Scene scene, List<LevelFinding> findings)
        {
            var pieces = TimberLinks.Pieces(scene).ToList();
            var walks = new List<(string, IList<Vector3>)>();
            var owners = new Dictionary<string, LevelPiece>();
            foreach (var piece in pieces)
            {
                var output = piece.Output != null ? piece.Output.GetComponent<LevelPieceOutput>() : null;
                if (output == null || output.WalkLine.Length < 2) continue;
                walks.Add((piece.name, output.WalkLine));
                owners[piece.name] = piece;
            }
            var structures = new Dictionary<string, FrozenStructure>();
            foreach (var frozen in WalkSurfaceBuilder.Structures(scene).Where(f => f.WalkLine.Length > 1))
            {
                walks.Add((frozen.name, frozen.WalkLine));
                structures[frozen.name] = frozen;
            }
            foreach (var problem in WalkProbe.Run(walks))
            {
                string owner = problem.message.Split(':')[0];
                Object context = problem.blocker != null ? problem.blocker.gameObject
                    : owners.TryGetValue(owner, out var p) ? p : structures.TryGetValue(owner, out var f) ? f : null;
                findings.Add(LevelFinding.Warning(problem.message, context, problem.at));
            }
            foreach (var quick in LevelQuickCheck.Run(scene))
                findings.Add(LevelFinding.Warning(quick, pieces.FirstOrDefault(p => quick.StartsWith(p.name + ":"))));
        }

        static void Passes(List<LevelFinding> findings)
        {
            var tune = CourseContractCheck.Tuning;
            var samples = new List<Vector3>();
            foreach (var corridor in Object.FindObjectsByType<PassCorridor>(FindObjectsSortMode.None))
            {
                if (!corridor.TrySample(tune, samples)) { findings.Add(LevelFinding.Error($"Pass {corridor.name}: out of reach.", corridor)); continue; }
                if (corridor.Opening > 0 && corridor.Opening < tune.catchRadius + PassCorridor.OpeningMargin)
                    findings.Add(LevelFinding.Error($"Pass {corridor.name}: its opening is too small to catch through.", corridor));
                foreach (var point in samples)
                    foreach (var hit in Physics.RaycastAll(point, Vector3.up, PassCorridor.CeilingMargin, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore))
                        if (hit.collider.GetComponentInParent<CourseCeiling>() != null)
                        {
                            findings.Add(LevelFinding.Error($"Pass {corridor.name}: a ceiling ({hit.collider.name}) over its arc.", corridor, point));
                            goto next;
                        }
                next:;
            }
        }

        // ------------------------------------------------------------------ the falls

        static void Falls(Scene scene, List<LevelFinding> findings)
        {
            var roots = scene.GetRootGameObjects();
            if (!roots.SelectMany(g => g.GetComponentsInChildren<LevelSafetyNet>(true)).Any() &&
                LevelTerrain.Terrains(scene).Any())
                findings.Add(LevelFinding.Warning("No safety net under the level (a kill zone marked LevelSafetyNet): a fall off the world never ends."));
            var kills = roots.SelectMany(g => g.GetComponentsInChildren<KillZone>(true)).Select(k => k.GetComponent<Collider>()).Where(c => c != null).ToList();
            foreach (var water in roots.SelectMany(g => g.GetComponentsInChildren<NatureWater>(true)))
            {
                var under = water.transform.position + Vector3.down * 0.6f;
                if (!kills.Any(k => k.bounds.Contains(under)))
                    findings.Add(LevelFinding.Error($"{water.name}: water with no kill zone under it.", water));
            }
        }

        // ------------------------------------------------------------------ the rules

        static void Rules(Scene scene, List<LevelFinding> findings)
        {
            var section = LevelRoutes.SectionRoot(scene);
            var roots = scene.GetRootGameObjects();
            if (section != null)
                foreach (var gameplay in roots.Where(r => r.transform != section)
                             .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                             .Where(m => m is Checkpoint || m is FinishZone || m is SignalActuator || m is PressurePlate || m is KillZone || m is Zone || m is BombGate ||
                                         m is MovingPlatform || m is FallingPlatform || m is RotatingObstacle || m is BombTransit || m is SunBeam)
                             .Where(m => m.GetComponentInParent<LevelSafetyNet>() == null).Select(m => m.gameObject).Distinct())
                    findings.Add(LevelFinding.Error($"{gameplay.name} is outside {section.name}: gameplay objects go under it (resets and the run only see it).", gameplay));
            foreach (var piece in TimberLinks.Pieces(scene).Where(p => p.Output == null))
                findings.Add(LevelFinding.Error($"{piece.name} is not built (Rebuild All Pieces).", piece));
            // decoration off the paths: planted and painted plants
            var paths = LevelForest.Paths(scene);
            if (paths.Count == 0) return;
            foreach (var forest in TimberLinks.Pieces(scene).OfType<ForestArea>())
            {
                var set = forest.Output != null ? forest.Output.GetComponentInChildren<FoliageInstancer>()?.Set : null;
                if (set == null) continue;
                int onPath = 0;
                foreach (var batch in set.batches)
                    foreach (var cell in batch.cells)
                        for (int i = 0; i < cell.Count; i++)
                        {
                            var (p, _, _) = FoliageSet.Unpack(batch, cell, i);
                            if (LevelForest.Distance(paths, new Vector2(p.x, p.z)) < 1.2f) onPath++;
                        }
                if (onPath > 0) findings.Add(LevelFinding.Warning($"{forest.name}: {onPath} plant(s) on a path (Replant now).", forest));
            }
            foreach (var terrain in LevelTerrain.Terrains(scene))
            {
                var o = terrain.transform.position;
                var size = terrain.terrainData.size;
                int onPath = terrain.terrainData.treeInstances.Count(t =>
                {
                    var p = o + Vector3.Scale(t.position, size);
                    return LevelForest.Distance(paths, new Vector2(p.x, p.z)) < 2.5f;
                });
                if (onPath > 0) findings.Add(LevelFinding.Warning($"{onPath} painted tree(s) on a path (Clear painted paths, on the Level object).", terrain));
            }
        }

        // ------------------------------------------------------------------ the timber

        /// <summary>A rope end or a post foot farther than this (m) from anything hangs or stands on nothing.</summary>
        public const float TimberReach = 0.15f;

        /// <summary>Frozen timber moved by hand: ropes that hang on nothing, posts that stand on nothing (one finding per structure).</summary>
        static void Timber(Scene scene, List<LevelFinding> findings)
        {
            Physics.SyncTransforms();
            var elements = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TimberElement>(false)).ToList();
            var loose = new List<(TimberElement element, Vector3 at, string what)>();
            foreach (var rope in elements.OfType<TimberPath>().Where(p => p.Kind == TimberElementKind.Rope && p.Points.Count >= 2))
                foreach (bool start in new[] { true, false })
                {
                    if ((start ? rope.StartAnchor : rope.EndAnchor) != null) continue;
                    var end = rope.transform.TransformPoint(start ? rope.Points[0] : rope.Points[rope.Points.Count - 1]);
                    if (TimberElementTools.AnchorAt(rope, end, TimberReach) == null) loose.Add((rope, end, "a rope hangs on nothing"));
                }
            foreach (var post in elements.OfType<TimberBoard>().Where(b => b.Kind == TimberElementKind.Post))
            {
                var a = post.transform.TransformPoint(post.BackEnd);
                var b = post.transform.TransformPoint(post.FrontEnd);
                var foot = a.y < b.y ? a : b;
                float ground = LevelNature.GroundAt(scene, new Vector2(foot.x, foot.z));
                if (!float.IsNaN(ground) && foot.y <= ground + TimberReach) continue;
                bool touches = Physics.OverlapSphere(foot, TimberReach + Mathf.Max(post.Size.x, post.Size.y) * 0.5f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => !c.transform.IsChildOf(post.transform) && c.GetComponentInParent<TimberWall>() == null);
                if (!touches) loose.Add((post, foot, "a post stands on nothing"));
            }
            foreach (var group in loose.GroupBy(l => (l.element.Structure != null ? l.element.Structure.name : l.element.name, l.what)))
            {
                var first = group.First();
                string where = first.element.Structure != null ? first.element.Structure.name : first.element.name;
                findings.Add(LevelFinding.Warning($"{where}: {group.Count()} time(s) {group.Key.what} (first: {first.element.name}).", first.element, first.at));
            }
        }

        // ------------------------------------------------------------------ the menu

        static void Menu(Scene scene, LevelSettings settings, List<LevelFinding> findings)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CourseKit.NetworkPrefab);
            var entries = CourseKit.ReadMenu(new SerializedObject(prefab.GetComponent<NetworkBootstrap>()));
            var entry = entries.FirstOrDefault(e => e.scene == scene.name);
            int checkpoints = RegisterLevelWindow.CheckpointsOf(scene);
            if (entry.scene == null) { findings.Add(LevelFinding.Info("Not in the menu yet (HotPatata/Level/Register In Menu once it passes).")); return; }
            if (entry.checkpoints != checkpoints)
                findings.Add(LevelFinding.Error($"The menu offers CP1 to CP{entry.checkpoints}, the level has {checkpoints} (Register In Menu again)."));
            if (entry.minPlayers != settings.MinPlayers)
                findings.Add(LevelFinding.Error($"The menu asks {entry.minPlayers} players, the level {settings.MinPlayers} (Register In Menu again)."));
            if (!EditorBuildSettings.scenes.Any(s => s.path == scene.path && s.enabled))
                findings.Add(LevelFinding.Error("The level is in the menu but not in the build (Register In Menu again)."));
        }

        // ------------------------------------------------------------------ the budget

        static void Budget(Scene scene, List<LevelFinding> findings)
        {
            long triangles = 0, bytes = 0;
            var meshes = new HashSet<Mesh>();
            // frozen timber: every element draws its (shared) mesh once more
            foreach (var filter in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TimberElement>(false)).Select(e => e.GetComponent<MeshFilter>()))
            {
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null) continue;
                triangles += (long)mesh.GetIndexCount(0) / 3;
                if (meshes.Add(mesh)) bytes += (long)mesh.vertexCount * 48 + (long)mesh.GetIndexCount(0) * 4;
            }
            foreach (var piece in TimberLinks.Pieces(scene).Where(p => p.Output != null))
                foreach (var mesh in LevelPieceBuilder.Meshes(piece.Output))
                    if (meshes.Add(mesh))
                    {
                        for (int s = 0; s < mesh.subMeshCount; s++) triangles += (long)mesh.GetIndexCount(s) / 3;
                        bytes += (long)mesh.vertexCount * 48 + (long)mesh.GetIndexCount(0) * 4;
                    }
            int plants = TimberLinks.Pieces(scene).OfType<ForestArea>()
                .Select(f => f.Output != null ? f.Output.GetComponentInChildren<FoliageInstancer>()?.Set : null).Where(s => s != null).Sum(s => s.InstanceCount());
            plants += LevelTerrain.Terrains(scene).Sum(t => t.terrainData.treeInstanceCount);
            float mb = bytes / (1024f * 1024f);
            findings.Add(LevelFinding.Info($"Budget: {triangles:N0} triangles of timber and trees ({meshes.Count} meshes, about {mb:F0} MB), {plants:N0} planted or painted plants."));
            if (triangles > TriangleBudget) findings.Add(LevelFinding.Warning($"Over {TriangleBudget:N0} triangles of generated pieces: frame rate at risk."));
            if (plants > PlantBudget) findings.Add(LevelFinding.Warning($"Over {PlantBudget:N0} plants: thin the forests."));
            if (mb > MeshBudgetMb) findings.Add(LevelFinding.Warning($"Over {MeshBudgetMb:F0} MB of generated meshes."));
        }
    }
}
