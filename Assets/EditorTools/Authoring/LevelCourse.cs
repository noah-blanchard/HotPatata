using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// An authored level's course (ARCHITECTURE §27): its checkpoints (campfires, AGENTS: a checkpoint is a campfire in the nature
    /// look), numbered along the way a team walks from the start; its finish; and a contract per section (§13.20: the start's section,
    /// then one from each checkpoint). A contract belongs to the section of the checkpoint it sits under; the start's sits under the level.
    /// </summary>
    public static class LevelCourse
    {
        /// <summary>The campfire stands this far beside its checkpoint: past its spawns, never on one (the tests want 3 to 6 m).</summary>
        public const float FireDistance = 3.4f;
        /// <summary>The room a campfire needs round it: its stone ring and the team gathered at it.</summary>
        const float FireRoom = 1.1f;

        public static List<Checkpoint> Checkpoints(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Checkpoint>(true)).ToList();

        /// <summary>Where the run starts: the middle of the start spawns (those under no checkpoint).</summary>
        public static Vector3 Start(Scene scene)
        {
            var spawns = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerSpawn>(true))
                .Where(s => s.GetComponentInParent<Checkpoint>() == null).Select(s => s.transform.position).ToList();
            return spawns.Count == 0 ? Vector3.zero : spawns.Aggregate(Vector3.zero, (a, b) => a + b) / spawns.Count;
        }

        // ------------------------------------------------------------------ placing

        /// <summary>A checkpoint at <paramref name="at"/> with its campfire beside it, numbered after the last one.</summary>
        public static Checkpoint AddCheckpoint(Scene scene, Transform parent, Vector3 at)
        {
            int id = Checkpoints(scene).Select(c => c.Id).DefaultIfEmpty(0).Max() + 1;
            var go = CourseKit.AddCheckpoint(parent, $"CP_{id:00}", id, at, 0f);
            Undo.RegisterCreatedObjectUndo(go, "Add checkpoint");
            var cp = go.GetComponent<Checkpoint>();
            Physics.SyncTransforms();
            // the fire is the checkpoint's child (it moves with it): its position is local
            NatureKit.CampfireCheckpoint(go.transform, cp, go.transform.InverseTransformPoint(FireSpot(cp)));
            return cp;
        }

        /// <summary>A checkpoint's campfire (its child), or null.</summary>
        public static CampfirePresentation FireOf(Checkpoint cp) => cp.GetComponentsInChildren<CampfirePresentation>(true).FirstOrDefault();

        /// <summary>
        /// Where a checkpoint's campfire goes: <see cref="FireDistance"/> beside it, on the first side (its right, its left, ahead, behind,
        /// then the diagonals) with floor at its height all round the fire and nothing in the way (a rail, a trunk, a deck's edge); its
        /// right when no side has room.
        /// </summary>
        public static Vector3 FireSpot(Checkpoint cp)
        {
            var t = cp.transform;
            var c = t.position;
            var ignore = new HashSet<Collider>(cp.GetComponentsInChildren<Collider>(true));
            float[] turns = { 90f, -90f, 0f, 180f, 45f, -45f, 135f, -135f };
            foreach (float turn in turns)
            {
                var dir = Quaternion.AngleAxis(turn, Vector3.up) * t.forward;
                var spot = c + dir * FireDistance;
                if (HasRoom(c, spot, ignore)) return spot;
            }
            return c + t.right * FireDistance;
        }

        static bool HasRoom(Vector3 from, Vector3 spot, HashSet<Collider> ignore)
        {
            int solid = LayerMask.GetMask("Environment", "Hazard");
            bool Floor(Vector3 p) => Physics.RaycastAll(p + Vector3.up * 1f, Vector3.down, 1.6f, solid, QueryTriggerInteraction.Ignore)
                                            .Any(h => !ignore.Contains(h.collider) && h.normal.y > 0.6f && Mathf.Abs(h.point.y - from.y) < 0.4f);
            if (!Floor(spot)) return false;
            for (int k = 0; k < 4; k++)
                if (!Floor(spot + Quaternion.AngleAxis(k * 90f + 45f, Vector3.up) * Vector3.forward * FireRoom)) return false;
            // nothing standing round the fire or between it and the spawns
            bool Standing(Collider col) => !ignore.Contains(col) && !(col is TerrainCollider) && col.GetComponent<WalkableProxy>() == null;
            // nor on the kit's volumes (a plate, a zone, a gate): the fire marks the checkpoint alone
            int bodies = LayerMask.GetMask("Player", "PlayerCatch", "Bomb");
            bool InTheWay(Collider col) => Standing(col) && ((1 << col.gameObject.layer) & bodies) == 0 &&
                                          (!col.isTrigger || (col.GetComponentInParent<LevelSafetyNet>() == null && col.GetComponentInParent<MistZone>() == null));
            if (Physics.OverlapCapsule(spot + Vector3.up * (FireRoom + 0.25f), spot + Vector3.up * 1.6f, FireRoom, ~0, QueryTriggerInteraction.Collide).Any(InTheWay)) return false;
            return !Physics.RaycastAll(from + Vector3.up * 0.8f, spot - from, Vector3.Distance(from, spot), solid, QueryTriggerInteraction.Ignore).Any(h => Standing(h.collider));
        }

        /// <summary>Puts <paramref name="cp"/>'s campfire back beside it (<see cref="FireSpot"/>) and fits its zone round it (undoable).</summary>
        public static void PlaceFire(Checkpoint cp)
        {
            var fire = FireOf(cp);
            if (fire == null) return;
            Physics.SyncTransforms();
            Undo.RecordObject(fire.transform, "Place the campfire");
            fire.transform.position = FireSpot(cp);
            FitZone(cp);
        }

        /// <summary>Fits <paramref name="cp"/>'s zone round its campfire wherever it stands (a fire moved by hand); returns whether it changed.</summary>
        public static bool FitZone(Checkpoint cp, bool undo = true)
        {
            var fire = FireOf(cp);
            if (fire == null) return false;
            var trigger = new SerializedObject(cp).FindProperty("trigger").objectReferenceValue as BoxCollider;
            if (trigger != null && undo) Undo.RecordObject(trigger, "Fit the checkpoint's zone");
            bool changed = NatureKit.FitCampfireZone(cp, fire.transform.position, true);
            if (changed) EditorUtility.SetDirty(cp);
            return changed;
        }

        /// <summary>On a save: every checkpoint's zone follows its campfire. Returns how many were refitted.</summary>
        public static int FitZones(Scene scene) => Checkpoints(scene).Count(cp => FitZone(cp, false));

        /// <summary>The finish (the course's end volume) at <paramref name="at"/>.</summary>
        public static FinishZone AddFinish(Transform parent, Vector3 at)
        {
            var go = CourseKit.Place(parent, CourseKit.GameplayDir + "FinishZone", "Finish", at, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(go, "Add finish");
            return go.GetComponent<FinishZone>();
        }

        /// <summary>A section's contract under <paramref name="owner"/> (a checkpoint, or the level for the start's section).</summary>
        public static SectionContract AddContract(Transform owner, int minPlayers)
        {
            var go = new GameObject("Contract");
            Undo.RegisterCreatedObjectUndo(go, "Add contract");
            go.transform.SetParent(owner, false);
            var contract = go.AddComponent<SectionContract>();
            if (minPlayers >= 3) contract.Configure("What this section forces (and why a shortcut fails).", minPlayers, "With two: why two players fail here.");
            else contract.Configure("What this section forces (and why a shortcut fails).");
            Selection.activeGameObject = go;
            return contract;
        }

        /// <summary>The section a contract belongs to: its checkpoint's id, 0 for the start's.</summary>
        public static int SectionOf(SectionContract contract)
        {
            var cp = contract.GetComponentInParent<Checkpoint>();
            return cp != null ? cp.Id : 0;
        }

        // ------------------------------------------------------------------ numbering

        /// <summary>
        /// Numbers the checkpoints 1..n in the order a team reaches them: the shortest walk from the start through the level (along
        /// walkways and bridges, across decks, over the ground between pieces). A checkpoint no walk reaches comes after, by distance.
        /// Returns the checkpoints in their new order.
        /// </summary>
        public static List<Checkpoint> Number(Scene scene)
        {
            var checkpoints = Checkpoints(scene);
            if (checkpoints.Count == 0) return checkpoints;
            var start = Start(scene);
            var distance = WalkDistances(scene, start, checkpoints.Select(c => c.transform.position).ToList());
            var ordered = checkpoints.Select((c, i) => (c, d: distance[i]))
                .OrderBy(t => float.IsInfinity(t.d) ? 1 : 0).ThenBy(t => float.IsInfinity(t.d) ? Vector3.Distance(start, t.c.transform.position) : t.d)
                .Select(t => t.c).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                var cp = ordered[i];
                var so = new SerializedObject(cp);
                so.FindProperty("id").intValue = i + 1;
                so.ApplyModifiedProperties();
                Undo.RecordObject(cp.gameObject, "Number checkpoints");
                cp.gameObject.name = $"CP_{i + 1:00}";
            }
            return ordered;
        }

        /// <summary>
        /// The walking distance from <paramref name="start"/> to each target (infinity when none reaches it): a graph of the level's walk
        /// lines (each walkway and bridge, consecutive points joined), deck outlines and the targets, any two points joined when they are
        /// within <see cref="Stride"/> on the flat and a step or a short climb apart (the ground and deck surfaces between them).
        /// </summary>
        public static float[] WalkDistances(Scene scene, Vector3 start, List<Vector3> targets)
        {
            var nodes = new List<Vector3> { start };
            var edges = new List<(int a, int b, float cost)>();
            void Chain(IList<Vector3> line)
            {
                int first = nodes.Count;
                nodes.AddRange(line);
                for (int i = first; i + 1 < nodes.Count; i++) edges.Add((i, i + 1, Vector3.Distance(nodes[i], nodes[i + 1])));
            }
            foreach (var piece in TimberLinks.Pieces(scene))
            {
                var output = piece.Output != null ? piece.Output.GetComponent<LevelPieceOutput>() : null;
                if (output != null && output.WalkLine.Length > 1) Chain(Curves.Resample(output.WalkLine, 1f));
                if (piece is TimberDeckShape deck && deck.Outline.Count >= 3)
                {
                    var ring = deck.WorldOutline().Select(p => new Vector3(p.x, deck.TopAt(p), p.y)).ToList();
                    ring.Add(ring[0]);
                    Chain(Curves.Resample(ring, 1.5f));
                    var centre = deck.WorldOutline().Aggregate(Vector2.zero, (a, b) => a + b) / deck.Outline.Count;
                    nodes.Add(new Vector3(centre.x, deck.TopAt(centre), centre.y));
                }
            }
            // frozen timber: its walk lines, and every walkable board's top (the deck surfaces join them to the rest)
            foreach (var frozen in WalkSurfaceBuilder.Structures(scene))
            {
                if (frozen.WalkLine.Length > 1) Chain(Curves.Resample(frozen.WalkLine, 1f));
                nodes.AddRange(WalkSurfaceBuilder.StandingPoints(frozen));
            }
            int targetsFrom = nodes.Count;
            nodes.AddRange(targets);
            // the ground between the pieces: a point every GroundStep metres over the area, on the ground
            float minX = nodes.Min(n => n.x) - 10f, maxX = nodes.Max(n => n.x) + 10f, minZ = nodes.Min(n => n.z) - 10f, maxZ = nodes.Max(n => n.z) + 10f;
            if ((maxX - minX) * (maxZ - minZ) < 600f * 600f)
                for (float x = minX; x <= maxX; x += GroundStep)
                    for (float z = minZ; z <= maxZ; z += GroundStep)
                    {
                        float y = LevelNature.GroundAt(scene, new Vector2(x, z));
                        if (!float.IsNaN(y)) nodes.Add(new Vector3(x, y, z));
                    }
            // the ground and deck surfaces: nearby points joined (a grid of cells keeps it linear)
            var cells = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var key = (Mathf.FloorToInt(nodes[i].x / Stride), Mathf.FloorToInt(nodes[i].z / Stride));
                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<int>();
                list.Add(i);
            }
            foreach (var ((cx, cz), list) in cells)
                foreach (int i in list)
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (!cells.TryGetValue((cx + dx, cz + dz), out var other)) continue;
                            foreach (int j in other)
                            {
                                if (j <= i) continue;
                                var d = nodes[j] - nodes[i];
                                float flat = new Vector2(d.x, d.z).magnitude;
                                if (flat > Stride || Mathf.Abs(d.y) > Climb) continue;
                                edges.Add((i, j, flat + Mathf.Abs(d.y) * 2f));
                            }
                        }
            // Dijkstra from the start
            var adjacency = new List<(int, float)>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) adjacency[i] = new List<(int, float)>();
            foreach (var (a, b, cost) in edges)
            {
                adjacency[a].Add((b, cost));
                adjacency[b].Add((a, cost));
            }
            var dist = Enumerable.Repeat(float.PositiveInfinity, nodes.Count).ToArray();
            dist[0] = 0f;
            var open = new SortedSet<(float d, int i)> { (0f, 0) };
            while (open.Count > 0)
            {
                var (d, i) = open.Min;
                open.Remove(open.Min);
                if (d > dist[i]) continue;
                foreach (var (j, cost) in adjacency[i])
                {
                    float nd = d + cost;
                    if (nd >= dist[j]) continue;
                    if (!float.IsInfinity(dist[j])) open.Remove((dist[j], j));
                    dist[j] = nd;
                    open.Add((nd, j));
                }
            }
            return Enumerable.Range(targetsFrom, targets.Count).Select(i => dist[i]).ToArray();
        }

        /// <summary>The ground is sampled this often (m) between the pieces.</summary>
        public const float GroundStep = 5f;
        /// <summary>Two points this close on the flat are joined (the ground and deck surfaces between them).</summary>
        public const float Stride = 6f;
        /// <summary>... and this close in height (a step, a short ramp).</summary>
        public const float Climb = 1.6f;
    }
}
