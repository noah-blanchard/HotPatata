using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HotPatata.Editor
{
    // ------------------------------------------------------------------ giant tree

    [CustomEditor(typeof(HeroTreeShape)), CanEditMultipleObjects]
    public sealed class HeroTreeShapeEditor : LevelPieceEditor
    {
        HeroTreeShape Tree => (HeroTreeShape)target;

        protected override string Help =>
            "Scene view: drag the white handle at the top of the trunk to lean the tree and set its height. Each carrying limb has a dot on " +
            "the trunk (drag it up or down) and a handle at its tip (drag it to aim it, lengthen it or raise it). Decks that wrap the trunk " +
            "saw the crown's limbs off round them by themselves. A tree takes a second or two to grow.";

        protected override void DrawTools()
        {
            EditorGUILayout.LabelField("Start from", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Old oak")) Shape(HeroTreeShape.Kind.Broadleaf, 58f, 2.8f, 1.35f, 27f, 15);
                if (GUILayout.Button("Tall fir")) Shape(HeroTreeShape.Kind.Conifer, 68f, 2.4f, 0.45f, 22f, 0);
                if (GUILayout.Button("Pine")) Shape(HeroTreeShape.Kind.Conifer, 56f, 2.1f, 0.5f, 30f, 0);
                if (GUILayout.Button("Snag")) Shape(HeroTreeShape.Kind.Dead, 44f, 1.5f, 0.35f, 28f, 0);
            }
            if (GUILayout.Button("Add a carrying limb"))
            {
                Undo.RecordObject(Tree, "Add limb");
                Tree.Limbs.Add(new HeroTreeShape.Limb { height = Mathf.Max(6f, Tree.CrownStart * 0.6f), yaw = Tree.Limbs.Count * 90f, length = 9f, rise = 8f, radius = 0.6f, leaves = Tree.TreeKind == HeroTreeShape.Kind.Broadleaf });
                EditorUtility.SetDirty(Tree);
            }
        }

        void Shape(HeroTreeShape.Kind kind, float height, float baseRadius, float topRadius, float crownStart, int crownLimbs)
        {
            Undo.RecordObject(Tree, "Tree shape");
            Tree.TreeKind = kind;
            Tree.Height = height;
            Tree.BaseRadius = baseRadius;
            Tree.TopRadius = topRadius;
            Tree.CrownStart = crownStart;
            Tree.CrownLimbs = crownLimbs;
            EditorUtility.SetDirty(Tree);
        }

        protected override void DrawScene()
        {
            var tree = Tree;
            var foot = tree.transform.position;
            var turn = Quaternion.Euler(0f, tree.transform.eulerAngles.y, 0f);
            // the trunk's line and its crown's start
            var line = Enumerable.Range(0, 13).Select(i => foot + turn * tree.AxisAt(tree.Height * i / 12f)).ToArray();
            Handles.color = PieceHandles.Line;
            Handles.DrawAAPolyLine(4f, line);
            var crown = foot + turn * tree.AxisAt(tree.CrownStart);
            Handles.DrawWireDisc(crown, Vector3.up, tree.RadiusAt(tree.CrownStart) + 1.5f);
            PieceHandles.Label(line[line.Length - 1] + Vector3.up, $"{tree.TreeKind} {tree.Height:F0} m");

            // the top: lean (x, z) and height (y)
            var top = foot + turn * new Vector3(tree.Lean.x, tree.Height, tree.Lean.y);
            EditorGUI.BeginChangeCheck();
            var to = Handles.PositionHandle(top, turn);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(tree, "Lean tree");
                var local = Quaternion.Inverse(turn) * (to - foot);
                tree.Lean = new Vector2(local.x, local.z);
                tree.Height = local.y;
                EditorUtility.SetDirty(tree);
            }

            // each carrying limb: its root on the trunk (height), its tip (aim, length, rise)
            for (int i = 0; i < tree.Limbs.Count; i++)
            {
                var limb = tree.Limbs[i];
                var root = foot + turn * tree.AxisAt(limb.height);
                var dir = turn * (Quaternion.Euler(-limb.rise, limb.yaw, 0f) * Vector3.forward);
                var tip = root + dir * limb.length;
                Handles.color = new Color(0.85f, 0.6f, 0.35f);
                Handles.DrawAAPolyLine(Mathf.Max(3f, limb.radius * 8f), root, tip);
                EditorGUI.BeginChangeCheck();
                var newRoot = Handles.Slider(root, Vector3.up, HandleUtility.GetHandleSize(root) * 0.08f, Handles.DotHandleCap, 0.1f);
                var newTip = Handles.FreeMoveHandle(tip, HandleUtility.GetHandleSize(tip) * 0.09f, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(tree, "Move limb");
                    limb.height = Mathf.Clamp(newRoot.y - foot.y, 1f, tree.Height - 2f);
                    var from = foot + turn * tree.AxisAt(limb.height);
                    var reach = Quaternion.Inverse(turn) * (newTip - from);
                    var flat = new Vector2(reach.x, reach.z);
                    limb.length = Mathf.Max(0.5f, reach.magnitude);
                    if (flat.sqrMagnitude > 1e-4f) limb.yaw = Mathf.Atan2(flat.x, flat.y) * Mathf.Rad2Deg;
                    limb.rise = Mathf.Atan2(reach.y, flat.magnitude) * Mathf.Rad2Deg;
                    tree.Limbs[i] = limb;
                    EditorUtility.SetDirty(tree);
                }
            }
        }
    }

    // ------------------------------------------------------------------ water

    [CustomEditor(typeof(WaterShape)), CanEditMultipleObjects]
    public sealed class WaterShapeEditor : LevelPieceEditor
    {
        WaterShape Water => (WaterShape)target;

        protected override string Help =>
            "The surface is at the piece's height; drag the yellow squares to size it. A kill zone lies under it down to its depth: a player who " +
            "falls in, or a thrown bomb that touches it, fails the section. Turn the piece to turn its flow (along its z).";

        protected override void DrawScene()
        {
            var water = Water;
            var t = water.transform;
            var turn = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            float hx = water.Size.x * 0.5f, hz = water.Size.y * 0.5f;
            Vector3 W(float x, float y, float z) => t.position + turn * new Vector3(x, y, z);
            Handles.color = new Color(0.35f, 0.7f, 1f);
            Handles.DrawAAPolyLine(3f, W(-hx, 0, -hz), W(hx, 0, -hz), W(hx, 0, hz), W(-hx, 0, hz), W(-hx, 0, -hz));
            Handles.color = new Color(1f, 0.35f, 0.3f, 0.7f);
            Handles.DrawAAPolyLine(2f, W(-hx, -water.Depth, -hz), W(hx, -water.Depth, -hz), W(hx, -water.Depth, hz), W(-hx, -water.Depth, hz), W(-hx, -water.Depth, -hz));
            foreach (var (axis, half) in new[] { (Vector3.right, hx), (Vector3.forward, hz) })
            {
                var at = t.position + turn * axis * half;
                Handles.color = PieceHandles.Line;
                EditorGUI.BeginChangeCheck();
                var to = Handles.Slider(at, turn * axis, HandleUtility.GetHandleSize(at) * 0.08f, Handles.CubeHandleCap, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(water, "Size water");
                    float v = Mathf.Max(0.5f, Vector3.Dot(to - t.position, turn * axis)) * 2f;
                    water.Size = axis == Vector3.right ? new Vector2(v, water.Size.y) : new Vector2(water.Size.x, v);
                    EditorUtility.SetDirty(water);
                }
            }
        }
    }

    // ------------------------------------------------------------------ forest area

    [CustomEditor(typeof(ForestArea)), CanEditMultipleObjects]
    public sealed class ForestAreaEditor : LevelPieceEditor
    {
        ForestArea Forest => (ForestArea)target;

        protected override string Help =>
            "Scene view: its outline's dots work like a deck's (click to move, + to add, Ctrl+click to delete, or the pencil). The forest is " +
            "planted inside it, kept off walkways, bridges, decks, pass arcs and giant trees; it replants when a piece near it changes. " +
            "For trees and plants one by one, paint them with the terrain's own tree and detail brushes.";

        protected override void DrawTools()
        {
            PencilButton(Forest.Outline, true);
            if (GUILayout.Button("Replant now")) LevelPieceBuilder.Rebuild(Forest);
            var output = Forest.Output != null ? Forest.Output.GetComponentInChildren<FoliageInstancer>() : null;
            if (output != null && output.Set != null) EditorGUILayout.LabelField($"{output.Set.InstanceCount()} plants and trees", EditorStyles.miniLabel);
        }

        protected override void DrawScene()
        {
            var forest = Forest;
            // the outline drawn on the ground (the piece's height is only where its dots are shown)
            PieceHandles.EditPoints(forest, forest.Outline, true, true, 3);
        }
    }

    // ------------------------------------------------------------------ mist zone

    [CustomEditor(typeof(MistZone)), CanEditMultipleObjects]
    public sealed class MistZoneEditor : UnityEditor.Editor
    {
        readonly BoxBoundsHandle box = new BoxBoundsHandle();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("The mist fills this box up to its top (blended at its sides). It is baked into the level's mist when the scene is saved.",
                                    MessageType.None);
        }

        void OnSceneGUI()
        {
            var zone = (MistZone)target;
            using (new Handles.DrawingScope(zone.transform.localToWorldMatrix))
            {
                box.center = Vector3.zero;
                box.size = zone.Size;
                box.SetColor(new Color(0.75f, 0.85f, 0.95f));
                EditorGUI.BeginChangeCheck();
                box.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(new Object[] { zone, zone.transform }, "Size mist zone");
                    zone.transform.position = zone.transform.TransformPoint(box.center);
                    zone.Size = box.size;
                    EditorUtility.SetDirty(zone);
                }
            }
        }
    }

    // ------------------------------------------------------------------ ambient sound source

    [CustomEditor(typeof(AmbientLoop)), CanEditMultipleObjects]
    public sealed class AmbientLoopEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var emitter = serializedObject.FindProperty("emitter");
            var options = new[] { "(as built)" }.Concat(LevelTerrain.Emitters()).ToArray();
            int index = Mathf.Max(0, System.Array.IndexOf(options, emitter.stringValue));
            int picked = EditorGUILayout.Popup(new GUIContent("Sound", "A folder of Assets/Audio/Ambience/Emitters: drop sound files in it."), index, options);
            if (picked != index) emitter.stringValue = picked == 0 ? "" : options[picked];
            serializedObject.ApplyModifiedProperties();
            var source = ((AmbientLoop)target).GetComponent<AudioSource>();
            if (source != null)
            {
                EditorGUI.BeginChangeCheck();
                float volume = EditorGUILayout.Slider("Volume", source.volume, 0f, 1f);
                float reach = EditorGUILayout.Slider("Heard up to (m)", source.maxDistance, 5f, 200f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(source, "Ambient sound");
                    source.volume = volume;
                    source.maxDistance = reach;
                    source.minDistance = Mathf.Min(6f, reach * 0.2f);
                }
            }
        }
    }
}
