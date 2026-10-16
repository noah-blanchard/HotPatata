using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// What an author does to timber elements by hand (ARCHITECTURE §27.1, docs/LEVEL_AUTHORING.md §17): change their wood, jitter them
    /// so a row of boards stops looking machined, set them down on what is under them, line a board up with its neighbours, hang a
    /// rope's end on what it touches. Every tool is one undo step; the watcher redraws what changed.
    /// </summary>
    public static class TimberElementTools
    {
        /// <summary>A rope's end hangs on an element within this (m) when it is let go.</summary>
        public const float AttachReach = 0.3f;

        public static void SetWood(IList<TimberElement> elements, TimberWood wood)
        {
            Undo.RecordObjects(elements.Cast<Object>().ToArray(), "Set wood");
            foreach (var e in elements)
            {
                e.Wood = wood;
                EditorUtility.SetDirty(e);
            }
        }

        public static void SetSolid(IList<TimberElement> elements, bool solid)
        {
            Undo.RecordObjects(elements.Cast<Object>().ToArray(), solid ? "Make solid" : "Make drawn only");
            foreach (var e in elements)
            {
                e.Solid = solid;
                EditorUtility.SetDirty(e);
            }
        }

        /// <summary>
        /// Moves and turns each element a little at random (a board: up to <paramref name="shift"/> across and along, a fifth of it up
        /// and down, <paramref name="turn"/> degrees round its up and a third of it across): hand-laid, not machined.
        /// </summary>
        public static void Jitter(IList<TimberElement> elements, float shift = 0.015f, float turn = 1.2f, int seed = 0)
        {
            var rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount);
            float R(float a) => (float)(rng.NextDouble() * 2.0 - 1.0) * a;
            Undo.RecordObjects(elements.Select(e => (Object)e.transform).ToArray(), "Jitter timber");
            foreach (var e in elements)
            {
                var t = e.transform;
                t.position += t.right * R(shift) + t.forward * R(shift) + t.up * R(shift * 0.2f);
                t.rotation = t.rotation * Quaternion.Euler(R(turn * 0.3f), R(turn), R(turn * 0.3f));
            }
        }

        /// <summary>Sets each element down on what is under it (the ground, a deck, another element): its lowest point on it.</summary>
        public static int SetDown(IList<TimberElement> elements, float reach = 6f)
        {
            Undo.RecordObjects(elements.Select(e => (Object)e.transform).ToArray(), "Set timber down");
            Physics.SyncTransforms();
            int moved = 0;
            foreach (var e in elements)
            {
                var r = e.GetComponent<Renderer>();
                if (r == null) continue;
                var b = r.bounds;
                float best = float.NaN;
                foreach (var probe in new[] { b.center, new Vector3(b.min.x, b.center.y, b.min.z), new Vector3(b.max.x, b.center.y, b.max.z) })
                    foreach (var hit in Physics.RaycastAll(new Vector3(probe.x, b.min.y + 0.05f, probe.z), Vector3.down, reach, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.transform.IsChildOf(e.transform) || IsBody(hit.collider)) continue;
                        if (float.IsNaN(best) || hit.point.y > best) best = hit.point.y;
                    }
                if (float.IsNaN(best)) continue;
                e.transform.position += Vector3.up * (best - b.min.y);
                moved++;
            }
            return moved;
        }

        /// <summary>
        /// Lines each board up with the boards beside it (same parent, within <paramref name="reach"/> m, not selected): its top at their
        /// tops' height there, its up as theirs.
        /// </summary>
        public static int AlignToNeighbours(IList<TimberElement> elements, float reach = 0.6f)
        {
            var chosen = new HashSet<TimberElement>(elements);
            Undo.RecordObjects(elements.Select(e => (Object)e.transform).ToArray(), "Align timber");
            int moved = 0;
            foreach (var board in elements.OfType<TimberBoard>())
            {
                var t = board.transform;
                var top = t.TransformPoint(board.TopCenter);
                var near = t.parent == null ? new List<TimberBoard>() : t.parent.GetComponentsInChildren<TimberBoard>()
                    .Where(o => !chosen.Contains(o) && o.Kind == board.Kind && Distance(o, top) < reach).ToList();
                if (near.Count == 0) continue;
                var up = near.Aggregate(Vector3.zero, (s, o) => s + o.transform.up).normalized;
                // each neighbour's top plane, met above this board's middle
                float y = near.Select(o =>
                {
                    var p = o.transform.TransformPoint(o.TopCenter);
                    var n = o.transform.up;
                    return Mathf.Abs(n.y) < 0.2f ? p.y : p.y - (n.x * (top.x - p.x) + n.z * (top.z - p.z)) / n.y;
                }).OrderBy(v => v).ElementAt(near.Count / 2);
                t.rotation = Quaternion.FromToRotation(t.up, up) * t.rotation;
                top = t.TransformPoint(board.TopCenter);
                t.position += Vector3.up * (y - top.y);
                moved++;
            }
            return moved;
        }

        static float Distance(TimberBoard board, Vector3 point)
        {
            var local = board.transform.InverseTransformPoint(point);
            var half = board.Size * 0.5f;
            var outside = new Vector3(Mathf.Max(0f, Mathf.Abs(local.x) - half.x), Mathf.Max(0f, Mathf.Abs(local.y) - half.y), Mathf.Max(0f, Mathf.Abs(local.z) - half.z));
            return board.transform.TransformVector(outside).magnitude;
        }

        /// <summary>The element (or other object) a rope's end at <paramref name="point"/> touches: what it should hang on, or null.</summary>
        public static Transform AnchorAt(TimberPath path, Vector3 point, float reach = AttachReach)
        {
            Physics.SyncTransforms();
            Transform best = null;
            float bestDistance = reach;
            foreach (var c in Physics.OverlapSphere(point, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(path.transform) || IsBody(c)) continue;
                float d = Vector3.Distance(c.ClosestPoint(point), point);
                if (d <= bestDistance) { bestDistance = d; best = c.transform; }
            }
            if (best != null) return best;
            // a drawn-only element has no collider: its renderer's box
            foreach (var e in Object.FindObjectsByType<TimberElement>(FindObjectsSortMode.None))
            {
                if (e == path || e.gameObject.scene != path.gameObject.scene) continue;
                var r = e.GetComponent<Renderer>();
                if (r == null) continue;
                float d = Vector3.Distance(r.bounds.ClosestPoint(point), point);
                if (d <= bestDistance) { bestDistance = d; best = e.transform; }
            }
            return best;
        }

        /// <summary>Hangs a rope's start (or end) on what it touches now; false if it touches nothing.</summary>
        public static bool AttachEnd(TimberPath path, bool start)
        {
            if (path.Points.Count < 2) return false;
            var point = path.transform.TransformPoint(start ? path.Points[0] : path.Points[path.Points.Count - 1]);
            var anchor = AnchorAt(path, point);
            if (anchor == null) return false;
            Undo.RecordObject(path, "Attach rope");
            path.Attach(start, anchor);
            EditorUtility.SetDirty(path);
            return true;
        }

        public static void DetachEnd(TimberPath path, bool start)
        {
            Undo.RecordObject(path, "Detach rope");
            path.Attach(start, null);
            EditorUtility.SetDirty(path);
        }

        /// <summary>A collider that is not something to stand or hang on: a structure's walking surface sits under its boards, a wall is air.</summary>
        static bool IsBody(Collider c) => c.GetComponentInParent<TimberWall>() != null;
    }
}
