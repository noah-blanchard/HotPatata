using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The inspector and Scene view of the timber elements (ARCHITECTURE §27.1, docs/LEVEL_AUTHORING.md §17): their fields, the tools
    /// that work on every selected element at once (wood, solid, jitter, set down, align), and their handles: a board's two ends (drag
    /// to lengthen it), a path's points (a rope's end let go on an element hangs on it).
    /// </summary>
    public abstract class TimberElementEditor : UnityEditor.Editor
    {
        protected List<TimberElement> Elements => targets.Cast<TimberElement>().ToList();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawTools();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected elements", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New boards")) TimberElementTools.SetWood(Elements, TimberWood.Plank);
                if (GUILayout.Button("Aged boards")) TimberElementTools.SetWood(Elements, TimberWood.PlankAged);
                if (GUILayout.Button("Timber")) TimberElementTools.SetWood(Elements, TimberWood.Timber);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Solid")) TimberElementTools.SetSolid(Elements, true);
                if (GUILayout.Button("Drawn only")) TimberElementTools.SetSolid(Elements, false);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Jitter", "Moves and turns each a little at random: hand-laid, not machined."))) TimberElementTools.Jitter(Elements);
                if (GUILayout.Button(new GUIContent("Set down", "Each onto what is under it (ground, deck, timber)."))) TimberElementTools.SetDown(Elements);
                if (GUILayout.Button(new GUIContent("Align", "Each board level with the boards beside it."))) TimberElementTools.AlignToNeighbours(Elements);
            }
            EditorGUILayout.HelpBox("Move, turn or delete elements as usual: the walking surface of the structure follows when you let go. " +
                                    "Deleting walkable boards makes a real hole.", MessageType.None);
        }

        protected virtual void DrawTools() { }
    }

    [CustomEditor(typeof(TimberBoard)), CanEditMultipleObjects]
    public sealed class TimberBoardEditor : TimberElementEditor
    {
        void OnSceneGUI()
        {
            var board = (TimberBoard)target;
            var t = board.transform;
            float size = HandleUtility.GetHandleSize(t.position) * 0.06f;
            foreach (bool front in new[] { false, true })
            {
                var end = t.TransformPoint(front ? board.FrontEnd : board.BackEnd);
                var dir = t.forward * (front ? 1f : -1f);
                Handles.color = Color.yellow;
                EditorGUI.BeginChangeCheck();
                var moved = Handles.Slider(end, dir, size, Handles.DotHandleCap, 0f);
                if (!EditorGUI.EndChangeCheck()) continue;
                // the other end stays where it is
                float grow = Vector3.Dot(moved - end, dir);
                float length = Mathf.Max(TimberBoard.MinSize * 10f, board.Size.z + grow);
                grow = length - board.Size.z;
                Undo.RecordObjects(new Object[] { board, t }, "Resize board");
                board.Size = new Vector3(board.Size.x, board.Size.y, length);
                t.position += dir * grow * 0.5f;
                EditorUtility.SetDirty(board);
            }
        }
    }

    [CustomEditor(typeof(TimberTurned)), CanEditMultipleObjects]
    public sealed class TimberTurnedEditor : TimberElementEditor
    {
    }

    [CustomEditor(typeof(TimberPath)), CanEditMultipleObjects]
    public sealed class TimberPathEditor : TimberElementEditor
    {
        protected override void DrawTools()
        {
            if (targets.Length != 1) return;
            var path = (TimberPath)target;
            EditorGUILayout.Space();
            foreach (bool start in new[] { true, false })
                using (new EditorGUILayout.HorizontalScope())
                {
                    var anchor = start ? path.StartAnchor : path.EndAnchor;
                    EditorGUILayout.LabelField(start ? "Start" : "End", anchor != null ? "hangs on " + anchor.name : "free", GUILayout.ExpandWidth(true));
                    if (anchor == null && GUILayout.Button("Attach", GUILayout.Width(70)) && !TimberElementTools.AttachEnd(path, start))
                        Debug.LogWarning($"[Timber] {path.name}: its {(start ? "start" : "end")} touches nothing within {TimberElementTools.AttachReach} m");
                    if (anchor != null && GUILayout.Button("Detach", GUILayout.Width(70))) TimberElementTools.DetachEnd(path, start);
                }
        }

        /// <summary>The end being dragged: 0 the start, 1 the end, -1 none.</summary>
        static int endDragged = -1;

        void OnSceneGUI()
        {
            var path = (TimberPath)target;
            var t = path.transform;
            int last = path.Points.Count - 1;
            if (endDragged >= 0 && GUIUtility.hotControl == 0)
            {
                // let go: the dragged end hangs on what it was let go on
                bool start = endDragged == 0;
                endDragged = -1;
                if ((start ? path.StartAnchor : path.EndAnchor) == null) TimberElementTools.AttachEnd(path, start);
            }
            for (int i = 0; i <= last; i++)
            {
                var world = t.TransformPoint(path.Points[i]);
                bool end = i == 0 || i == last;
                bool hung = (i == 0 && path.StartAnchor != null) || (i == last && path.EndAnchor != null);
                Handles.color = hung ? Color.green : end ? Color.yellow : Color.white;
                float size = HandleUtility.GetHandleSize(world) * 0.07f;
                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(path, "Move rope point");
                    path.Points[i] = t.InverseTransformPoint(moved);
                    // a dragged end lets go of what it hung on; it hangs again on what it is let go on
                    if (i == 0 && path.StartAnchor != null) path.Attach(true, null);
                    if (i == last && path.EndAnchor != null) path.Attach(false, null);
                    if (end) endDragged = i == 0 ? 0 : 1;
                    EditorUtility.SetDirty(path);
                }
            }
        }
    }

    [CustomEditor(typeof(TimberWall)), CanEditMultipleObjects]
    public sealed class TimberWallEditor : UnityEditor.Editor
    {
        void OnSceneGUI()
        {
            var wall = (TimberWall)target;
            var t = wall.transform;
            Handles.color = new Color(0.4f, 0.8f, 1f);
            for (int i = 0; i < wall.Points.Count; i++)
            {
                var world = t.TransformPoint(wall.Points[i]);
                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(world, HandleUtility.GetHandleSize(world) * 0.06f, Vector3.zero, Handles.CubeHandleCap);
                if (!EditorGUI.EndChangeCheck()) continue;
                Undo.RecordObject(wall, "Move wall point");
                wall.Points[i] = t.InverseTransformPoint(moved);
                EditorUtility.SetDirty(wall);
            }
            for (int i = 0; i + 1 < wall.Points.Count; i++)
            {
                var a = t.TransformPoint(wall.Points[i]);
                var b = t.TransformPoint(wall.Points[i + 1]);
                Handles.DrawLine(a + Vector3.up * wall.Height, b + Vector3.up * wall.Height);
            }
        }
    }

    [CustomEditor(typeof(FrozenStructure)), CanEditMultipleObjects]
    public sealed class FrozenStructureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var structure = (FrozenStructure)target;
            var elements = structure.GetComponentsInChildren<TimberElement>(true);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Elements", elements.Length.ToString());
            EditorGUILayout.LabelField("Walkable boards", elements.Count(e => e.Walk != TimberWalk.None).ToString());
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Redraw all"))
                    foreach (var s in targets.Cast<FrozenStructure>()) TimberFreezer.Refresh(s);
                if (GUILayout.Button(new GUIContent("Save to library...", "Saves this structure as a prefab of the structure library.")))
                    StructureLibrary.SaveDialog(structure);
            }
            EditorGUILayout.HelpBox("A frozen structure: each element is moved by hand. Its walking surface (and walk line) is laid again from its " +
                                    "walkable boards when you let go.", MessageType.None);
        }
    }
}
