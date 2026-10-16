using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Lays a frozen structure's walking surface from its walkable elements (ARCHITECTURE §27.1): one closed collider a walker never
    /// catches on. A slab board gets a slab under its top, a little wider than the board so the gaps between boards close; the boards
    /// of a run (a walkway's boards and treads, a bridge's slats) get one smooth ramp swept through the middles of their tops, as wide
    /// as the run, broken where boards are missing. A board taken away leaves a real hole. Also the structure's walk line (its runs).
    /// </summary>
    public static class WalkSurfaceBuilder
    {
        /// <summary>A slab is wider than its board by this (m) across and along: the gaps between boards close.</summary>
        public const float SlabGrowAcross = 0.03f, SlabGrowAlong = 0.02f;
        /// <summary>A run breaks where its next point is this many times its usual spacing away (a missing board).</summary>
        public const float RunBreak = 1.6f;

        /// <summary>Lays the surface again if a walkable element moved, changed or went (or always with <paramref name="force"/>).</summary>
        public static bool Refresh(FrozenStructure structure, bool force = false)
        {
            if (structure == null) return false;
            var boards = WalkBoards(structure);
            string from = Signature(structure, boards);
            if (!force && structure.SurfaceFrom == from && (boards.Count == 0) == (structure.Surface == null)) return false;
            if (boards.Count == 0)
            {
                if (structure.Surface != null) Object.DestroyImmediate(structure.Surface);
                structure.SetSurface(null, null, from);
                EditorUtility.SetDirty(structure);
                return true;
            }
            var root = structure.transform;
            var mb = new MeshBuilder();
            var line = new List<Vector3>();
            foreach (var board in boards.Where(b => b.Walk == TimberWalk.Slab)) Slab(mb, root, board, structure.SlabDepth);
            foreach (var run in boards.Where(b => b.Walk == TimberWalk.Run).GroupBy(b => b.Run).OrderBy(g => g.Key))
                line.AddRange(Run(mb, root, run.ToList(), structure.RunInfo(run.Key)).Select(root.TransformPoint));
            var mesh = mb.Triangles > 0 ? MeshCatalog.Get("surface|" + from, () => mb.ToSingleMesh("surface")) : null;
            var surface = structure.Surface;
            if (surface == null)
            {
                surface = new GameObject("Walk surface") { layer = CourseKit.Layer("Environment"), hideFlags = HideFlags.NotEditable };
                surface.transform.SetParent(root, false);
                surface.AddComponent<MeshCollider>();
                surface.AddComponent<WalkableProxy>();
                GameObjectUtility.SetStaticEditorFlags(surface, StaticEditorFlags.BatchingStatic);
            }
            surface.transform.localPosition = Vector3.zero;
            surface.transform.localRotation = Quaternion.identity;
            surface.transform.localScale = Vector3.one;
            surface.GetComponent<MeshCollider>().sharedMesh = mesh;
            surface.GetComponent<WalkableProxy>().Configure(structure.SurfaceKind, structure.Tolerance);
            structure.SetSurface(surface, line.ToArray(), from);
            EditorUtility.SetDirty(structure);
            return true;
        }

        /// <summary>The frozen structures of a scene.</summary>
        public static IEnumerable<FrozenStructure> Structures(UnityEngine.SceneManagement.Scene scene) =>
            scene.IsValid() && scene.isLoaded
                ? scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FrozenStructure>(true))
                : Enumerable.Empty<FrozenStructure>();

        /// <summary>The tops of a structure's walkable boards (world): where a walker can stand on it.</summary>
        public static IEnumerable<Vector3> StandingPoints(FrozenStructure structure) =>
            WalkBoards(structure).Select(b => b.transform.TransformPoint(b.TopCenter));

        /// <summary>The structure's own walkable boards (not those of a structure inside it), active ones only.</summary>
        public static List<TimberBoard> WalkBoards(FrozenStructure structure) =>
            structure.GetComponentsInChildren<TimberBoard>(false).Where(b => b.Walk != TimberWalk.None && b.Structure == structure).ToList();

        static void Slab(MeshBuilder mb, Transform root, TimberBoard board, float depth)
        {
            var t = board.transform;
            var rotation = Quaternion.Inverse(root.rotation) * t.rotation;
            var up = rotation * Vector3.up;
            var top = root.InverseTransformPoint(t.TransformPoint(board.TopCenter));
            var size = Vector3.Scale(board.Size, t.lossyScale);
            mb.Box(top - up * depth * 0.5f, rotation, new Vector3(size.x + SlabGrowAcross, depth, size.z + SlabGrowAlong), 0);
        }

        /// <summary>The run's ramp (one per stretch without a missing board); returns its points (root space).</summary>
        static List<Vector3> Run(MeshBuilder mb, Transform root, List<TimberBoard> boards, FrozenStructure.RunSurface info)
        {
            // one point per order: the middle of the tops of its boards
            var points = boards.GroupBy(b => b.Order).OrderBy(g => g.Key).Select(g =>
                g.Select(b => root.InverseTransformPoint(b.transform.TransformPoint(b.TopCenter))).Aggregate(Vector3.zero, (s, p) => s + p) / g.Count()).ToList();
            float width = info.width > 0f ? info.width : boards.Max(b => b.Size.z * b.transform.lossyScale.z) + SlabGrowAcross;
            float depth = info.depth > 0f ? info.depth : 0.3f;
            if (points.Count == 1)
            {
                var only = boards[0];
                Slab(mb, root, only, depth);
                return points;
            }
            var gaps = new List<float>();
            for (int i = 1; i < points.Count; i++) gaps.Add(Vector3.Distance(points[i], points[i - 1]));
            // the usual spacing where each gap is (its neighbours' median): a stair's treads lie further apart than a level walk's boards
            float Usual(int gap)
            {
                var near = Enumerable.Range(gap - 3, 7).Where(k => k != gap && k >= 0 && k < gaps.Count).Select(k => gaps[k]).OrderBy(g => g).ToList();
                return near.Count == 0 ? gaps[gap] : near[near.Count / 2];
            }
            var stretch = new List<Vector3> { points[0] };
            float spacing = gaps[0];
            for (int i = 1; i <= points.Count; i++)
            {
                if (i < points.Count && gaps[i - 1] <= Usual(i - 1) * RunBreak)
                {
                    stretch.Add(points[i]);
                    spacing = gaps[i - 1];
                    continue;
                }
                Sweep(mb, stretch, spacing, width, depth, boards, root);
                if (i < points.Count) stretch = new List<Vector3> { points[i] };
            }
            return points;
        }

        /// <summary>A ramp through <paramref name="stretch"/>, half a spacing past its first and last points (it covers their boards).</summary>
        static void Sweep(MeshBuilder mb, List<Vector3> stretch, float spacing, float width, float depth, List<TimberBoard> boards, Transform root)
        {
            if (stretch.Count == 1)
            {
                // a lone board between holes: a slab under it
                var lone = boards.OrderBy(b => Vector3.Distance(root.InverseTransformPoint(b.transform.position), stretch[0])).First();
                Slab(mb, root, lone, depth);
                return;
            }
            var path = new List<Vector3>(stretch);
            path.Insert(0, stretch[0] + (stretch[0] - stretch[1]).normalized * spacing * 0.5f);
            int n = stretch.Count;
            path.Add(stretch[n - 1] + (stretch[n - 1] - stretch[n - 2]).normalized * spacing * 0.5f);
            mb.Sweep(path, width, depth, 0, MeshBuilder.Anchor.Top);
        }

        /// <summary>What the surface is built from: every walkable board's role, place (root space, millimetres) and size, and the runs.</summary>
        static string Signature(FrozenStructure structure, List<TimberBoard> boards)
        {
            var s = new StringBuilder();
            var root = structure.transform;
            s.Append(structure.SurfaceKind).Append('|').Append(Mm(structure.SlabDepth)).Append('|').Append(Mm(structure.Tolerance)).Append('|');
            foreach (var r in structure.Runs) s.Append(r.run).Append(':').Append(Mm(r.width)).Append(',').Append(Mm(r.depth)).Append(';');
            s.Append('|');
            foreach (var b in boards)
            {
                var t = b.transform;
                var p = root.InverseTransformPoint(t.position);
                var q = Quaternion.Inverse(root.rotation) * t.rotation;
                var size = Vector3.Scale(b.Size, t.lossyScale);
                s.Append((int)b.Walk).Append(b.Run).Append(',').Append(b.Order).Append(':')
                 .Append(Mm(p.x)).Append(',').Append(Mm(p.y)).Append(',').Append(Mm(p.z)).Append(',')
                 .Append(Mathf.RoundToInt(q.x * 1e4f)).Append(',').Append(Mathf.RoundToInt(q.y * 1e4f)).Append(',').Append(Mathf.RoundToInt(q.z * 1e4f)).Append(',').Append(Mathf.RoundToInt(q.w * 1e4f)).Append(',')
                 .Append(Mm(size.x)).Append(',').Append(Mm(size.y)).Append(',').Append(Mm(size.z)).Append(';');
            }
            return s.ToString();
        }

        static string Mm(float v) => Mathf.RoundToInt(v * 1000f).ToString(CultureInfo.InvariantCulture);
    }
}
