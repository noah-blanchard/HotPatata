using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The level's own inspector (ARCHITECTURE §27): its name, players and day, then its terrain's tools (the nature look, valley walls,
    /// forest brushes, paths cleared of painted trees), its mist and its sound (each section's ambience).
    /// </summary>
    [CustomEditor(typeof(LevelSettings))]
    public sealed class LevelSettingsEditor : UnityEditor.Editor
    {
        static float wallWidth = 30f, wallHeight = 35f;

        LevelSettings Settings => (LevelSettings)target;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minPlayers"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("day"));
            if (GUILayout.Button("Apply the day (Apply Look)")) LookBuilder.ApplyToScene(Settings.gameObject.scene);
            serializedObject.ApplyModifiedProperties();

            var scene = Settings.gameObject.scene;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Course", EditorStyles.boldLabel);
            var placed = LevelCourse.Checkpoints(scene);
            bool startContract = Settings.GetComponentsInChildren<SectionContract>(true).Any(c => c.GetComponentInParent<Checkpoint>() == null);
            bool finish = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FinishZone>(true)).Any();
            EditorGUILayout.LabelField($"{placed.Count} checkpoint(s), {(finish ? "a finish" : "no finish")}, " +
                                       $"{(startContract ? "the start's contract" : "no contract for the start")}", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Number checkpoints along the course")) LevelPlayMenu.Number();
                using (new EditorGUI.DisabledScope(startContract))
                    if (GUILayout.Button("Add the start's contract")) LevelCourse.AddContract(Settings.transform, Settings.MinPlayers);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Verify Level")) LevelCheckWindow.Open();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("Run Route Runner")) EditorApplication.ExecuteMenuItem("HotPatata/Level/Run Route Runner");
                    if (GUILayout.Button("Play from the start")) LevelPlayMenu.PlayFrom(0);
                }
            }
            var terrain = LevelTerrain.Terrains(scene).FirstOrDefault();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Terrain", EditorStyles.boldLabel);
            if (terrain == null) EditorGUILayout.HelpBox("No terrain in the level.", MessageType.Info);
            else
            {
                EditorGUILayout.HelpBox("Sculpt it with the Terrain's own brushes (Raise or Lower, Set Height, Smooth, Stamp). It is drawn as the forest " +
                                        "floor: leaf litter where it is flat, rock where it is steep. Paint trees and ferns with its Paint Trees and " +
                                        "Paint Details tools (the forest brushes).", MessageType.None);
                if (GUILayout.Button("Draw it in the nature look")) LevelTerrain.ApplyLook(terrain);
                using (new EditorGUILayout.HorizontalScope())
                {
                    wallWidth = EditorGUILayout.FloatField("Valley walls: width", wallWidth);
                    wallHeight = EditorGUILayout.FloatField("height", wallHeight);
                }
                if (GUILayout.Button("Raise valley walls round the edge"))
                {
                    int n = LevelTerrain.RaiseBorder(terrain, Mathf.Max(5f, wallWidth), Mathf.Max(2f, wallHeight));
                    Debug.Log($"[Level] valley walls: {n} ground samples raised (Ctrl+Z undoes it). Rebuild All Pieces if timber stands near them.");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Set up the forest brushes")) LevelTerrain.SetupBrushes(terrain);
                    if (GUILayout.Button("Clear painted paths"))
                    {
                        var (trees, cells) = LevelTerrain.ClearPaths(scene);
                        Debug.Log($"[Level] {trees} painted tree(s) and {cells} detail cell(s) taken off the paths, the solids and the pass arcs.");
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mist", EditorStyles.boldLabel);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mistDensity"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mistDepth"));
            serializedObject.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(terrain == null))
                if (GUILayout.Button("Bake the mist now (it bakes on every save)") && LevelTerrain.BakeMist(scene, Settings)) LookBuilder.ApplyToScene(scene);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sound", EditorStyles.boldLabel);
            var environments = new[] { "(default)" }.Concat(LevelTerrain.Environments()).ToArray();
            serializedObject.Update();
            var fallback = serializedObject.FindProperty("defaultAmbience");
            int f = Mathf.Max(0, System.Array.IndexOf(environments, fallback.stringValue));
            int pickedDefault = EditorGUILayout.Popup("Ambience by default", f, environments);
            if (pickedDefault != f) fallback.stringValue = pickedDefault == 0 ? "" : environments[pickedDefault];
            var list = serializedObject.FindProperty("sectionAmbience");
            int checkpoints = RegisterLevelWindow.CheckpointsOf(scene);
            if (list.arraySize < checkpoints + 1) list.arraySize = checkpoints + 1;
            for (int i = 0; i <= checkpoints; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                int index = Mathf.Max(0, System.Array.IndexOf(environments, item.stringValue));
                int picked = EditorGUILayout.Popup(i == 0 ? "From the start" : $"From CP{i}", index, environments);
                if (picked != index) item.stringValue = picked == 0 ? "" : environments[picked];
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Each section's ambience is written on save. Place river, waterfall or wind sounds with GameObject > HotPatata Level > " +
                                    "Sound Source, and a local ambience (a cave in the forest) with Ambience Zone.", MessageType.None);
            if (GUI.changed) EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
