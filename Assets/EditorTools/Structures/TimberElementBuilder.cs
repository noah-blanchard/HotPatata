using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Draws the timber elements (ARCHITECTURE §27.1): each element's mesh comes from the shared catalogue (<see cref="TimberCatalog"/>,
    /// made from its shape's data the first time), its material from its wood, and its collider from <see cref="TimberElement.Solid"/>
    /// (a box for a board, its own mesh for a path or a turned part, on Environment). An invisible wall gets its chain of boxes. An
    /// element is redrawn only when what it is drawn from changed.
    /// </summary>
    public static class TimberElementBuilder
    {
        /// <summary>Redraws <paramref name="element"/> if its data changed (or always with <paramref name="force"/>); true if it did.</summary>
        public static bool Refresh(TimberElement element, bool force = false)
        {
            if (element == null) return false;
            if (element is TimberPath path && path.FollowAnchors()) EditorUtility.SetDirty(path);
            string key = element.ShapeKey();
            string built = key + "|" + element.Wood + "|" + element.Solid;
            var filter = element.GetComponent<MeshFilter>();
            if (!force && element.BuiltFrom == built && filter != null && filter.sharedMesh != null) return false;
            var mesh = TimberCatalog.Get(key, () => Mesh(element));
            if (filter == null) filter = element.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = element.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = element.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = NatureMaterialBuilder.Load(TimberKit.SlotMaterials[(int)element.Wood]);
            Collide(element, mesh);
            GameObjectUtility.SetStaticEditorFlags(element.gameObject, StaticEditorFlags.BatchingStatic);
            element.BuiltFrom = built;
            EditorUtility.SetDirty(element);
            return true;
        }

        static void Collide(TimberElement element, Mesh mesh)
        {
            var go = element.gameObject;
            var box = go.GetComponent<BoxCollider>();
            var meshCollider = go.GetComponent<MeshCollider>();
            if (!element.Solid)
            {
                if (box != null) Object.DestroyImmediate(box);
                if (meshCollider != null) Object.DestroyImmediate(meshCollider);
                go.layer = 0;
                return;
            }
            go.layer = CourseKit.Layer("Environment");
            if (element is TimberBoard board)
            {
                if (meshCollider != null) Object.DestroyImmediate(meshCollider);
                if (box == null) box = go.AddComponent<BoxCollider>();
                box.center = Vector3.zero;
                box.size = board.Size;
                return;
            }
            if (box != null) Object.DestroyImmediate(box);
            if (meshCollider == null) meshCollider = go.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>The mesh of an element's shape, in its local space, one material slot.</summary>
        public static Mesh Mesh(TimberElement element)
        {
            var mb = new MeshBuilder(1);
            var shift = ElementRecorder.Shift(element.Grain);
            bool cut = false;
            switch (element)
            {
                case TimberBoard board:
                    mb.Box(Vector3.zero, Quaternion.identity, board.Size, 0, shift);
                    if (Mathf.Abs(board.CutStart) > 0.05f || Mathf.Abs(board.CutEnd) > 0.05f)
                    {
                        // slant the ends: every corner moves along the board by its offset across times the cut's tangent
                        float back = Mathf.Tan(board.CutStart * Mathf.Deg2Rad), front = Mathf.Tan(board.CutEnd * Mathf.Deg2Rad);
                        for (int i = 0; i < mb.V.Count; i++)
                        {
                            var v = mb.V[i];
                            v.z += v.x * (v.z > 0f ? front : back);
                            mb.V[i] = v;
                        }
                        cut = true;
                    }
                    break;
                case TimberPath path:
                    var points = PathPoints(path);
                    if (points.Count < 2) points = new List<Vector3> { Vector3.zero, Vector3.forward * 0.1f };
                    if (path.Shape == TimberPath.Profile.Round)
                        mb.Tube(points, points.Select(_ => path.Radius).ToList(), Sides(path.Radius), 0, path.Caps, shift.x);
                    else
                        mb.Sweep(points, path.Width, path.Height, 0, (MeshBuilder.Anchor)(int)Seat(path.Anchor), path.Caps, shift.x);
                    break;
                case TimberTurned turned:
                    if (turned.Profile.Count >= 2) mb.Lathe(Vector3.zero, Quaternion.identity, turned.Profile, turned.Sides, 0);
                    break;
            }
            var mesh = mb.ToMesh(element.ShapeKey(), out _);
            if (cut) mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>The line a path is drawn along: its points, a two-point path hanging by its sag, a closed one back to its start.</summary>
        public static List<Vector3> PathPoints(TimberPath path)
        {
            var points = new List<Vector3>(path.Points);
            if (points.Count == 2 && path.Sag > 0.001f)
                points = Curves.Sag(points[0], points[1], path.Sag, Mathf.Max(4, Mathf.CeilToInt(Vector3.Distance(points[0], points[1]) / 0.3f)));
            if (path.Closed && points.Count > 2) points.Add(points[0]);
            return points;
        }

        /// <summary>A rope's sides (a thin one is a square, a thick log an octagon).</summary>
        public static int Sides(float radius) => radius < 0.02f ? 4 : radius < 0.06f ? 6 : 8;

        static MeshBuilder.Anchor Seat(TimberPath.Seat seat) =>
            seat == TimberPath.Seat.Top ? MeshBuilder.Anchor.Top : seat == TimberPath.Seat.Bottom ? MeshBuilder.Anchor.Bottom : MeshBuilder.Anchor.Center;

        // ------------------------------------------------------------------ walls

        /// <summary>Lays a wall's boxes again if its line changed; true if it did.</summary>
        public static bool Refresh(TimberWall wall, bool force = false)
        {
            if (wall == null) return false;
            string key = wall.ShapeKey();
            if (!force && wall.BuiltFrom == key) return false;
            foreach (Transform child in wall.transform.Cast<Transform>().ToList())
                if (child.GetComponent<BoxCollider>() != null && child.GetComponent<TimberElement>() == null) Object.DestroyImmediate(child.gameObject);
            wall.gameObject.layer = CourseKit.Layer("Environment");
            var pts = Curves.Resample(wall.Points, 1.2f);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                var flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                if (flat.sqrMagnitude < 1e-4f) continue;
                var child = new GameObject("Wall " + i) { layer = wall.gameObject.layer, hideFlags = HideFlags.NotEditable };
                child.transform.SetParent(wall.transform, false);
                float baseY = Mathf.Min(a.y, b.y) - 0.1f;
                float topY = Mathf.Max(a.y, b.y) + wall.Height;
                child.transform.localPosition = new Vector3((a.x + b.x) * 0.5f, (baseY + topY) * 0.5f, (a.z + b.z) * 0.5f);
                child.transform.localRotation = Quaternion.LookRotation(flat.normalized);
                child.AddComponent<BoxCollider>().size = new Vector3(0.12f, topY - baseY, flat.magnitude + 0.06f);
                GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.BatchingStatic);
            }
            wall.BuiltFrom = key;
            EditorUtility.SetDirty(wall);
            return true;
        }
    }
}
