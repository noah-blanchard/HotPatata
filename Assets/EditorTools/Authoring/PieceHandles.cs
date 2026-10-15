using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The Scene view handles of the authored level's pieces (ARCHITECTURE §27, docs/LEVEL_AUTHORING.md): a dot per point (click to
    /// select it, Ctrl+click to delete it), a move handle on the selected point, a "+" in the middle of each segment (click to insert a
    /// point there), and the line drawn live while the geometry waits for the mouse to let go.
    /// </summary>
    public static class PieceHandles
    {
        public static readonly Color Line = new Color(1f, 0.82f, 0.25f);
        public static readonly Color Attached = new Color(0.35f, 1f, 0.45f);
        public static readonly Color OpeningColor = new Color(0.3f, 0.85f, 1f);
        /// <summary>A dragged end attaches to a deck edge drawn this close to it on screen (pixels).</summary>
        public const float SnapPixels = 28f;

        static LevelPiece owner;
        static int selected = -1;

        public static int Selected(LevelPiece piece) => owner == piece ? selected : -1;

        public static void Select(LevelPiece piece, int index)
        {
            owner = piece;
            selected = index;
        }

        static bool Delete => Event.current.control || Event.current.command;

        /// <summary>
        /// Edits <paramref name="points"/> (the piece's local space). <paramref name="flat"/>: the points stay at local height 0 (a deck's
        /// outline). <paramref name="world"/> overrides where a point is drawn (an end attached to a deck). Returns the index of the point the
        /// move handle moved, -1 if none moved (an insert or a delete is not a move).
        /// </summary>
        public static int EditPoints(LevelPiece piece, List<Vector3> points, bool closed, bool flat, int minPoints, Func<int, Vector3?> world = null)
        {
            int moved = -1;
            var w = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++) w[i] = world?.Invoke(i) ?? piece.World(Flat(points[i], flat));
            Handles.color = Line;
            if (w.Length > 1)
            {
                Handles.DrawAAPolyLine(3f, w);
                if (closed && w.Length > 2) Handles.DrawAAPolyLine(3f, w[w.Length - 1], w[0]);
            }

            // the "+" in each segment's middle inserts a point there
            int segments = closed ? points.Count : points.Count - 1;
            for (int i = 0; i < segments && points.Count >= 2; i++)
            {
                int j = (i + 1) % points.Count;
                var mid = (w[i] + w[j]) * 0.5f;
                float size = HandleUtility.GetHandleSize(mid) * 0.05f;
                Handles.color = new Color(1f, 1f, 1f, 0.7f);
                if (Handles.Button(mid, Quaternion.identity, size, size * 1.4f, Handles.CircleHandleCap))
                {
                    Undo.RecordObject(piece, "Insert point");
                    points.Insert(i + 1, Flat((points[i] + points[j]) * 0.5f, flat));
                    Select(piece, i + 1);
                    EditorUtility.SetDirty(piece);
                    GUI.changed = true;
                    return -1;
                }
            }

            // a dot per point: select it, or Ctrl+click to delete it
            for (int i = 0; i < points.Count; i++)
            {
                float size = HandleUtility.GetHandleSize(w[i]) * 0.07f;
                Handles.color = Selected(piece) == i ? Color.white : Line;
                if (Handles.Button(w[i], Quaternion.identity, size, size * 1.3f, Handles.DotHandleCap))
                {
                    if (Delete && points.Count > minPoints)
                    {
                        Undo.RecordObject(piece, "Delete point");
                        points.RemoveAt(i);
                        Select(piece, -1);
                        EditorUtility.SetDirty(piece);
                        GUI.changed = true;
                        return -1;
                    }
                    Select(piece, i);
                }
            }

            // the move handle on the selected point
            int s = Selected(piece);
            if (s >= 0 && s < points.Count)
            {
                EditorGUI.BeginChangeCheck();
                var rotation = Tools.pivotRotation == PivotRotation.Local ? piece.transform.rotation : Quaternion.identity;
                var to = Handles.PositionHandle(w[s], rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(piece, "Move point");
                    points[s] = Flat(piece.Local(to), flat);
                    EditorUtility.SetDirty(piece);
                    moved = s;
                }
            }
            return moved;
        }

        static Vector3 Flat(Vector3 p, bool flat) => flat ? new Vector3(p.x, 0f, p.z) : p;

        /// <summary>A free end's move handle (a bridge's end, a walk's end): returns the new world position, or null.</summary>
        public static Vector3? EndHandle(Vector3 at, bool attached)
        {
            float size = HandleUtility.GetHandleSize(at) * 0.11f;
            Handles.color = attached ? Attached : Line;
            Handles.SphereHandleCap(0, at, Quaternion.identity, size, EventType.Repaint);
            EditorGUI.BeginChangeCheck();
            var to = Handles.PositionHandle(at, Quaternion.identity);
            return EditorGUI.EndChangeCheck() ? to : (Vector3?)null;
        }

        /// <summary>
        /// Snaps an end dropped at <paramref name="world"/> to the nearest deck edge (unless Shift is held: free placement); returns the
        /// attachment (default when none) and where the end goes.
        /// </summary>
        public static TimberAttach SnapEnd(LevelPiece piece, Vector3 world, out Vector3 at)
        {
            at = world;
            if (Event.current != null && Event.current.shift) return default;
            if (TrySnapOnScreen(piece, world, out var onScreen, out at)) return onScreen;
            return TimberLinks.TrySnap(piece.gameObject.scene, world, piece, out var attach, out at) ? attach : default;
        }

        /// <summary>
        /// The deck edge drawn nearest the dragged end on screen, within <see cref="SnapPixels"/>: what the author sees touching is what
        /// attaches, at whatever height the drag left the end.
        /// </summary>
        static bool TrySnapOnScreen(LevelPiece piece, Vector3 world, out TimberAttach attach, out Vector3 edge)
        {
            attach = default;
            edge = world;
            var camera = Camera.current;
            if (camera == null) return false;
            var target = HandleUtility.WorldToGUIPoint(world);
            float best = SnapPixels;
            bool InFront(Vector3 p) => Vector3.Dot(camera.transform.forward, p - camera.transform.position) > 0f;
            // another walkway's or bridge's end first: joined end to end
            foreach (var (other, start, at) in TimberLinks.Ends(piece.gameObject.scene, piece))
            {
                if (!InFront(at)) continue;
                float d = Vector2.Distance(HandleUtility.WorldToGUIPoint(at), target);
                if (d >= best) continue;
                best = d;
                attach = new TimberAttach { joined = other, joinedStart = start };
                edge = at;
            }
            if (attach.Attached) return true;
            foreach (var deck in TimberLinks.Pieces(piece.gameObject.scene).OfType<TimberDeckShape>())
            {
                if (deck == piece || deck.Outline.Count < 3) continue;
                var ring = deck.WorldOutline();
                float total = TimberDeckShape.Perimeter(ring), run = 0f;
                if (total < 0.01f) continue;
                for (int i = 0; i < ring.Count; i++)
                {
                    var a = ring[i];
                    var b = ring[(i + 1) % ring.Count];
                    float l = Vector2.Distance(a, b);
                    int n = Mathf.Max(1, Mathf.CeilToInt(l / 0.2f));
                    for (int k = 0; k <= n; k++)
                    {
                        var q = Vector2.Lerp(a, b, k / (float)n);
                        var p = new Vector3(q.x, deck.TopAt(q), q.y);
                        if (Vector3.Dot(camera.transform.forward, p - camera.transform.position) <= 0f) continue;
                        float d = Vector2.Distance(HandleUtility.WorldToGUIPoint(p), target);
                        if (d >= best) continue;
                        best = d;
                        attach = new TimberAttach { deck = deck, along = (run + l * k / n) / total };
                        edge = p;
                    }
                    run += l;
                }
            }
            return attach.Attached;
        }

        /// <summary>Shows where an end is attached: a green ring on the deck's edge.</summary>
        public static void DrawAttached(Vector3 at)
        {
            Handles.color = Attached;
            Handles.DrawWireDisc(at, Vector3.up, HandleUtility.GetHandleSize(at) * 0.18f, 3f);
        }

        /// <summary>A label in the Scene view over a point.</summary>
        public static void Label(Vector3 at, string text)
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
            Handles.Label(at, text, style);
        }
    }
}
