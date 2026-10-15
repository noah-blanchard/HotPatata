using System.Collections.Generic;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Pencil mode (ARCHITECTURE §27): a piece's points drawn click by click in the Scene view, on whatever the mouse is over (the ground,
    /// a deck) or, for a deck, on its own height. Enter, Escape or a right click ends it.
    /// </summary>
    public static class PiecePencil
    {
        static LevelPiece target;
        static List<Vector3> points;
        static bool flat;

        public static bool IsDrawing(LevelPiece piece) => piece != null && target == piece;

        [InitializeOnLoadMethod]
        static void Hook() => Selection.selectionChanged += () =>
        {
            // drawing ends when the piece is no longer selected (not when its inspector is rebuilt after a point is added)
            if (target != null && Selection.activeGameObject != target.gameObject) End();
        };

        /// <summary>Starts drawing <paramref name="piece"/>'s <paramref name="list"/> anew (it is cleared).</summary>
        public static void Begin(LevelPiece piece, List<Vector3> list, bool flatPoints)
        {
            Undo.RecordObject(piece, "Draw points");
            list.Clear();
            target = piece;
            points = list;
            flat = flatPoints;
            EditorUtility.SetDirty(piece);
            SceneView.lastActiveSceneView?.Focus();
        }

        public static void End()
        {
            target = null;
            SceneView.RepaintAll();
        }

        /// <summary>Call from the piece's OnSceneGUI: takes the clicks while drawing.</summary>
        public static void OnSceneGUI(LevelPiece piece)
        {
            if (!IsDrawing(piece)) return;
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(id);
            var hit = PointUnder(piece, e.mousePosition);
            if (points.Count > 0 && hit.HasValue)
            {
                Handles.color = PieceHandles.Line;
                Handles.DrawDottedLine(piece.World(points[points.Count - 1]), hit.Value, 4f);
            }
            Handles.BeginGUI();
            GUI.Label(new Rect(10, 10, 420, 22), "Pencil: click to add points; Enter, Esc or right click to finish.", EditorStyles.helpBox);
            Handles.EndGUI();
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && !e.alt:
                    if (hit.HasValue)
                    {
                        Undo.RecordObject(piece, "Draw point");
                        var local = piece.Local(hit.Value);
                        points.Add(flat ? new Vector3(local.x, 0f, local.z) : local);
                        EditorUtility.SetDirty(piece);
                    }
                    e.Use();
                    break;
                case EventType.MouseDown when e.button == 1:
                case EventType.KeyDown when e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape:
                    End();
                    e.Use();
                    break;
                case EventType.MouseMove:
                    SceneView.RepaintAll();
                    break;
            }
        }

        /// <summary>The world point under the mouse: on a collider, else on the piece's horizontal plane; a deck's points on its plane.</summary>
        static Vector3? PointUnder(LevelPiece piece, Vector2 mouse)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            var plane = new Plane(Vector3.up, piece.transform.position);
            if (flat) return plane.Raycast(ray, out float d) ? ray.GetPoint(d) : (Vector3?)null;
            // the nearest collider under the mouse that is not the piece's own geometry (points never pile up on what they drew)
            var hits = Physics.RaycastAll(ray, 2000f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3? found = null;
            foreach (var hit in hits)
            {
                if (hit.distance >= best || hit.collider.transform.IsChildOf(piece.transform)) continue;
                best = hit.distance;
                found = hit.point;
            }
            if (found.HasValue) return found;
            return plane.Raycast(ray, out float e) ? ray.GetPoint(e) : (Vector3?)null;
        }
    }
}
