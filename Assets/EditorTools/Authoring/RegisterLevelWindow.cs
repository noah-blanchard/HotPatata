using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// HotPatata/Level/Register In Menu (ARCHITECTURE §27): the menu's level list, the open level in it (drag it where it goes), its
    /// name in the menu and the players it needs. Written through <see cref="CourseKit.WriteMenu"/>: the other levels keep their order.
    /// </summary>
    public sealed class RegisterLevelWindow : EditorWindow
    {
        List<CourseKit.MenuEntry> entries;
        ReorderableList list;
        Scene scene;
        LevelSettings settings;
        int checkpoints;

        public static void Open()
        {
            var w = GetWindow<RegisterLevelWindow>(true, "Register In Menu");
            w.minSize = new Vector2(440, 380);
            w.Load();
        }

        /// <summary>The highest checkpoint id of <paramref name="s"/> (the menu offers Start and CP1..CPn).</summary>
        public static int CheckpointsOf(Scene s) =>
            s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Checkpoint>(true)).Select(c => c.Id).DefaultIfEmpty(0).Max();

        void Load()
        {
            scene = EditorSceneManager.GetActiveScene();
            settings = LevelScenes.SettingsOf(scene);
            checkpoints = CheckpointsOf(scene);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CourseKit.NetworkPrefab);
            entries = CourseKit.ReadMenu(new SerializedObject(prefab.GetComponent<NetworkBootstrap>()));
            if (settings != null && entries.All(e => e.scene != scene.name))
            {
                int sandbox = entries.FindIndex(e => e.scene == "PassSandbox");
                entries.Insert(sandbox >= 0 ? sandbox : entries.Count,
                               new CourseKit.MenuEntry { scene = scene.name, displayName = settings.DisplayName, checkpoints = checkpoints, minPlayers = settings.MinPlayers, maxPlayers = settings.MaxPlayers });
            }
            list = new ReorderableList(entries, typeof(CourseKit.MenuEntry), true, true, false, false)
            {
                drawHeaderCallback = r => EditorGUI.LabelField(r, "The menu's levels, in order (drag to move)"),
                drawElementCallback = (r, i, active, focused) =>
                {
                    var e = entries[i];
                    bool mine = e.scene == scene.name;
                    string shown = string.IsNullOrWhiteSpace(e.displayName) ? e.scene : e.displayName;
                    var style = mine ? EditorStyles.boldLabel : EditorStyles.label;
                    int max = CourseKit.MaxOf(e);
                    string players = max == e.minPlayers ? $"{e.minPlayers} players" : max < 4 ? $"{e.minPlayers}-{max} players" : $"{e.minPlayers}+ players";
                    EditorGUI.LabelField(r, $"{i + 1}. {shown}   ({e.scene}, CP{e.checkpoints}, {players})", style);
                },
            };
        }

        void OnGUI()
        {
            if (entries == null) Load();
            if (settings == null)
            {
                EditorGUILayout.HelpBox("Open an authored level (a scene made with HotPatata/Level/New Level) to put it in the menu.", MessageType.Info);
                if (GUILayout.Button("Reload")) Load();
                return;
            }
            int index = entries.FindIndex(e => e.scene == scene.name);
            var mine = entries[index];
            EditorGUILayout.LabelField("Level", scene.name, EditorStyles.boldLabel);
            mine.displayName = EditorGUILayout.TextField("Name in the menu", mine.displayName);
            mine.minPlayers = EditorGUILayout.IntSlider("Players needed", mine.minPlayers, 1, 4);
            mine.maxPlayers = EditorGUILayout.IntSlider("Players at most", Mathf.Max(mine.minPlayers, CourseKit.MaxOf(mine)), mine.minPlayers, 4);
            mine.checkpoints = checkpoints;
            EditorGUILayout.LabelField("Checkpoints", checkpoints == 0 ? "none yet (only the start is offered)" : $"CP1 to CP{checkpoints}");
            entries[index] = mine;
            EditorGUILayout.Space();
            list.DoLayoutList();
            if (string.IsNullOrEmpty(scene.path) || !scene.path.StartsWith(LevelScenes.LevelsFolder))
                EditorGUILayout.HelpBox("Save the level in Assets/Scenes/Levels first.", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save to the menu")) Apply(false);
                if (GUILayout.Button("Remove from the menu")) Apply(true);
            }
        }

        void Apply(bool remove)
        {
            var mine = entries.First(e => e.scene == scene.name);
            Undo.RecordObject(settings, "Register level");
            settings.DisplayName = mine.displayName;
            settings.MinPlayers = mine.minPlayers;
            settings.MaxPlayers = CourseKit.MaxOf(mine);
            EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(scene);
            var written = remove ? entries.Where(e => e.scene != scene.name).ToList() : entries;
            if (!remove) CourseKit.AddToBuild(scene.path);
            CourseKit.EditMenu(so => CourseKit.WriteMenu(so, written));
            AssetDatabase.SaveAssets();
            Debug.Log(remove ? $"[Level] {scene.name} removed from the menu" : $"[Level] {scene.name} is in the menu, {entries.IndexOf(mine) + 1} of {entries.Count}");
            Load();
        }
    }
}
