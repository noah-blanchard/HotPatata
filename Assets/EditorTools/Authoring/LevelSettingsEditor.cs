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

        /// <summary>The ground: valley or layered, the rules' bake, the painted overlay, the terrain's size (ARCHITECTURE §27.4).</summary>
        void GroundSection(Terrain terrain)
        {
            var d = terrain.terrainData;
            EditorGUILayout.LabelField($"{d.size.x:F0} m, {d.heightmapResolution}² heights ({d.size.x / (d.heightmapResolution - 1):F2} m a sample), " +
                                       $"{TerrainResample.MemoryMb(d.heightmapResolution):F0} MB", EditorStyles.miniLabel);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ground"));
            bool layered = Settings.Ground == GroundLook.Layered;
            if (layered) EditorGUILayout.PropertyField(serializedObject.FindProperty("bakeGroundOnSave"), new GUIContent("Bake rules on save"));
            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed || layered != GroundBaker.IsLayered(terrain) && GUILayout.Button(layered ? "Use the layered ground" : "Use the valley ground"))
                LevelTerrain.ApplyLook(terrain);
            if (!layered)
            {
                EditorGUILayout.HelpBox("Sculpt it with the Terrain's brushes (and Terrain Tools'). It is drawn as the forest floor: leaf litter where it is flat, " +
                                        "rock where it is steep. Choose Layered to paint eight layers.", MessageType.None);
            }
            else if (GroundBaker.IsLayered(terrain))
            {
                EditorGUILayout.HelpBox("Eight layers: leaf litter, grass, trail, gravel, mud, riverbed, scree, rock. Bake the rules, then paint any layer with " +
                                        "the Terrain's Paint Texture brush: a bake keeps what you painted (Show painted). Sculpt with Terrain Tools' brushes.", MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Bake ground rules"))
                    {
                        int kept = GroundBaker.Bake(terrain);
                        Debug.Log($"[Ground] rules baked; {kept} painted texel(s) kept.");
                    }
                    var splat = terrain.GetComponent<GroundSplat>();
                    bool showing = splat != null && splat.ShowingPainted;
                    if (splat != null && GUILayout.Button(showing ? "Hide painted" : "Show painted"))
                        splat.ShowPainted(showing ? null : GroundBaker.PaintedTexture(terrain), !showing);
                    if (GUILayout.Button(GroundTools.ResetBrush ? "Stop resetting" : "Reset painted (brush)")) GroundTools.ResetBrush = !GroundTools.ResetBrush;
                }
                if (GUILayout.Button("Edit the rules")) Selection.activeObject = GroundRules.Load();
            }
            if (GUILayout.Button("Resample terrain (size, fineness)...")) ResampleTerrainWindow.Open();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minPlayers"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxPlayers"), new GUIContent("Max Players"));
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
                GroundSection(terrain);
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
