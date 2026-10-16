using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// A section's contract in the Scene view (spec §13.20, ARCHITECTURE §27): each shortcut it locks drawn from its first point to its
    /// last (gap red, climb orange, lob blue, spread green), its points moved with handles, locks added from the inspector, and the
    /// contract checked on demand (<see cref="CourseContractCheck"/>).
    /// </summary>
    [CustomEditor(typeof(SectionContract))]
    public sealed class SectionContractEditor : UnityEditor.Editor
    {
        static readonly Dictionary<SectionContract.ShortcutKind, Color> Colors = new Dictionary<SectionContract.ShortcutKind, Color>
        {
            { SectionContract.ShortcutKind.Gap, new Color(1f, 0.35f, 0.3f) },
            { SectionContract.ShortcutKind.Climb, new Color(1f, 0.65f, 0.2f) },
            { SectionContract.ShortcutKind.Lob, new Color(0.35f, 0.65f, 1f) },
            { SectionContract.ShortcutKind.Spread, new Color(0.4f, 0.95f, 0.45f) },
        };

        List<string> lastCheck;

        SectionContract Contract => (SectionContract)target;

        public override void OnInspectorGUI()
        {
            int section = LevelCourse.SectionOf(Contract);
            EditorGUILayout.LabelField(section == 0 ? "The start's section" : $"The section from checkpoint {section}", EditorStyles.boldLabel);
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lock a shortcut (then place its points in the Scene view)", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Gap", "A jump the team must not make: from its take-off to its landing."))) AddLock(SectionContract.ShortcutKind.Gap);
                if (GUILayout.Button(new GUIContent("Climb", "A face the team must not climb: from its foot to its top."))) AddLock(SectionContract.ShortcutKind.Climb);
                if (GUILayout.Button(new GUIContent("Lob", "A wall the bomb must not be lobbed over: from the thrower to the wall's top."))) AddLock(SectionContract.ShortcutKind.Lob);
                if (GUILayout.Button(new GUIContent("Spread", "Two plates one body cannot hold in turn: the first, the second, the walk between."))) AddLock(SectionContract.ShortcutKind.Spread);
            }
            if (GUILayout.Button("Check this contract"))
            {
                string path = PathOf(Contract.transform);
                lastCheck = CourseContractCheck.Problems(Contract.gameObject.scene, CourseContractCheck.Tuning).Where(p => p.StartsWith(path)).ToList();
            }
            if (lastCheck != null)
            {
                if (lastCheck.Count == 0) EditorGUILayout.HelpBox("Every lock holds.", MessageType.Info);
                foreach (var p in lastCheck) EditorGUILayout.HelpBox(p, MessageType.Warning);
            }
            EditorGUILayout.HelpBox("Gap: the far side out of a running jump's reach (14 m, less going up). Climb: a face of 3.4 m or more. Lob: walls up " +
                                    "to a roof. A window a player fits through is not bomb-only (docs/OBSTACLES.md §5).", MessageType.None);
        }

        static string PathOf(Transform t)
        {
            var names = new List<string>();
            for (; t != null; t = t.parent) names.Insert(0, t.name);
            return string.Join("/", names);
        }

        void AddLock(SectionContract.ShortcutKind kind)
        {
            var so = new SerializedObject(Contract);
            var list = so.FindProperty("shortcuts");
            list.arraySize++;
            var item = list.GetArrayElementAtIndex(list.arraySize - 1);
            item.FindPropertyRelative("name").stringValue = kind.ToString().ToLowerInvariant() + " " + list.arraySize;
            item.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            item.FindPropertyRelative("from").vector3Value = new Vector3(-3f, 0f, 0f);
            item.FindPropertyRelative("to").vector3Value = kind == SectionContract.ShortcutKind.Climb ? new Vector3(-3f, 4f, 0f)
                : kind == SectionContract.ShortcutKind.Lob ? new Vector3(3f, 4f, 0f) : new Vector3(3f, 0f, 0f);
            item.FindPropertyRelative("via").arraySize = 0;
            so.ApplyModifiedProperties();
        }

        void OnSceneGUI()
        {
            var contract = Contract;
            var t = contract.transform;
            var so = new SerializedObject(contract);
            var list = so.FindProperty("shortcuts");
            bool changed = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                var kind = (SectionContract.ShortcutKind)item.FindPropertyRelative("kind").enumValueIndex;
                var from = item.FindPropertyRelative("from");
                var to = item.FindPropertyRelative("to");
                var via = item.FindPropertyRelative("via");
                var points = new List<SerializedProperty> { from };
                for (int v = 0; v < via.arraySize; v++) points.Add(via.GetArrayElementAtIndex(v));
                points.Add(to);
                Handles.color = Colors[kind];
                Handles.DrawAAPolyLine(4f, points.Select(p => t.TransformPoint(p.vector3Value)).ToArray());
                PieceHandles.Label(t.TransformPoint(Vector3.Lerp(from.vector3Value, to.vector3Value, 0.5f)) + Vector3.up * 0.5f,
                                   $"{kind}: {item.FindPropertyRelative("name").stringValue}");
                foreach (var p in points)
                {
                    var world = t.TransformPoint(p.vector3Value);
                    EditorGUI.BeginChangeCheck();
                    var moved = Handles.FreeMoveHandle(world, HandleUtility.GetHandleSize(world) * 0.1f, Vector3.zero, Handles.SphereHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        // a free handle slides in the view's plane: snap it onto what is under the mouse when there is something
                        var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                        if (Physics.Raycast(ray, out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore)) moved = hit.point;
                        p.vector3Value = t.InverseTransformPoint(moved);
                        changed = true;
                    }
                }
            }
            if (changed) so.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// A checkpoint's tools in an authored level (ARCHITECTURE §27): play from it, its section's contract, and in the Scene view its
    /// zone (where the team reaches it), its spawns and its campfire, the fire moved with its handle (the zone follows).
    /// </summary>
    [CustomEditor(typeof(Checkpoint)), CanEditMultipleObjects]
    public sealed class CheckpointEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var cp = (Checkpoint)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button("Play from here")) LevelPlayMenu.PlayFrom(cp.Id);
                var settings = LevelScenes.SettingsOf(cp.gameObject.scene);
                bool hasContract = cp.GetComponentsInChildren<SectionContract>(true).Any();
                using (new EditorGUI.DisabledScope(settings == null || hasContract))
                    if (GUILayout.Button(hasContract ? "Its section has a contract" : "Add this section's contract"))
                        LevelCourse.AddContract(cp.transform, settings.MinPlayers);
            }
            if (LevelScenes.SettingsOf(cp.gameObject.scene) != null && LevelCourse.FireOf(cp) != null)
            {
                EditorGUILayout.HelpBox("The team gathers at the campfire (its child): move it where you like, 3 to 6 m from the checkpoint, " +
                                        "on the same floor (drag its orange ring in the Scene view): the checkpoint's zone follows it.", MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Place the campfire beside it")) LevelCourse.PlaceFire(cp);
                    if (GUILayout.Button("Fit the zone round the campfire")) LevelCourse.FitZone(cp);
                }
            }
        }

        void OnSceneGUI()
        {
            var cp = (Checkpoint)target;
            if (LevelScenes.SettingsOf(cp.gameObject.scene) == null) return;
            // the zone: reaching it (with the bomb) reaches the checkpoint
            if (new SerializedObject(cp).FindProperty("trigger").objectReferenceValue is BoxCollider trigger)
                using (new Handles.DrawingScope(new Color(1f, 0.7f, 0.25f, 0.9f), trigger.transform.localToWorldMatrix))
                    Handles.DrawWireCube(trigger.center, trigger.size);
            // the spawns: where the team comes back after a reset
            Handles.color = new Color(0.5f, 0.85f, 1f);
            foreach (var spawn in cp.GetComponentsInChildren<PlayerSpawn>(true))
                Handles.DrawWireDisc(spawn.transform.position, Vector3.up, 0.35f, 2f);
            PieceHandles.Label(cp.transform.position + Vector3.up * 2.2f, $"{cp.name}: spawns");
            var fire = LevelCourse.FireOf(cp);
            if (fire == null) return;
            var at = fire.transform.position;
            Handles.color = new Color(1f, 0.55f, 0.2f);
            Handles.DrawWireDisc(at, Vector3.up, NatureKit.CampfireGatherRadius, 2f);
            Handles.DrawDottedLine(cp.transform.position + Vector3.up * 0.1f, at + Vector3.up * 0.1f, 4f);
            float d = Vector3.Distance(new Vector3(at.x, 0f, at.z), new Vector3(cp.transform.position.x, 0f, cp.transform.position.z));
            PieceHandles.Label(at + Vector3.up * 1.6f, d < 3f || d > 6f ? $"campfire {d:0.0} m away (3 to 6 m)" : "campfire: the team gathers here");
            EditorGUI.BeginChangeCheck();
            var moved = Handles.Slider2D(at, Vector3.up, Vector3.right, Vector3.forward, HandleUtility.GetHandleSize(at) * 0.12f, Handles.CircleHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(fire.transform, "Move the campfire");
                // it stays on the checkpoint's floor
                moved.y = cp.transform.position.y;
                fire.transform.position = moved;
                LevelCourse.FitZone(cp);
            }
        }
    }
}
