using System;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// The authored levels' menus (ARCHITECTURE §27, docs/LEVEL_AUTHORING.md): HotPatata/Level (new level, register, look, rebuild). What is
    /// placed (GameObject/HotPatata Level) comes from the palette's catalogue (<see cref="PaletteCatalog"/>, <see cref="PaletteMenuWriter"/>).
    /// </summary>
    public static class LevelMenu
    {
        const string Root = "HotPatata/Level/";

        [MenuItem(Root + "New Level...", priority = 0)]
        static void NewLevel() => NewLevelWindow.Open();

        [MenuItem(Root + "Register In Menu...", priority = 1)]
        static void Register() => RegisterLevelWindow.Open();

        [MenuItem(Root + "Apply Look", priority = 20)]
        static void ApplyLook()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (LevelScenes.SettingsOf(scene) == null)
            {
                Debug.LogWarning("[Level] the open scene is not an authored level (no LevelSettings)");
                return;
            }
            LookBuilder.ApplyToScene(scene);
            AssetDatabase.SaveAssets();
        }

        [MenuItem(Root + "Rebuild All Pieces", priority = 21)]
        static void RebuildAll()
        {
            var pieces = TimberLinks.Pieces(EditorSceneManager.GetActiveScene()).ToList();
            LevelPieceBuilder.Rebuild(pieces);
            Debug.Log($"[Level] {pieces.Count} piece(s) rebuilt in {LevelPieceBuilder.LastMilliseconds:F0} ms");
        }

        /// <summary>A new piece of type <paramref name="type"/> at <paramref name="at"/>, in its level group, selected and built (a preset dropped in the Scene view).</summary>
        public static LevelPiece CreatePiece(Type type, string name, Vector3 at, LevelGroup group = LevelGroup.Timber)
        {
            var piece = PaletteCatalog.NewPiece(PlaceContext.At(at, Vector3.up), type, name, at, group);
            Selection.activeGameObject = piece.gameObject;
            return piece;
        }
    }

    /// <summary>HotPatata/Level/New Level: the level's scene name, its name in the menu, the players it needs and its day.</summary>
    public sealed class NewLevelWindow : EditorWindow
    {
        string levelName = "MyLevel", displayName = "", problem;
        int minPlayers = 2, maxPlayers = 4, size;
        LevelDay day = LevelDay.Ranger;
        TerrainFineness fineness;

        public static void Open()
        {
            var w = GetWindow<NewLevelWindow>(true, "New Level");
            w.minSize = new Vector2(440, 290);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("A new level is a scene in Assets/Scenes/Levels, ready to play: the run, the players, the bomb, a flat terrain " +
                                    "under the start and the day it borrows. Build it with the GameObject > HotPatata Level pieces.", MessageType.None);
            levelName = EditorGUILayout.TextField("Scene name", levelName);
            displayName = EditorGUILayout.TextField("Name in the menu", displayName);
            minPlayers = EditorGUILayout.IntSlider("Players needed", minPlayers, 1, 4);
            maxPlayers = EditorGUILayout.IntSlider("Players at most", Mathf.Max(minPlayers, maxPlayers), minPlayers, 4);
            day = (LevelDay)EditorGUILayout.EnumPopup("Day", day);
            size = EditorGUILayout.Popup(new GUIContent("Terrain size", "The terrain's side (m); Resample Terrain changes it later."), size,
                                         TerrainResample.Sizes.Select(s => $"{s:F0} m").ToArray());
            fineness = (TerrainFineness)EditorGUILayout.EnumPopup(new GUIContent("Fineness", "Normal: a height sample a metre. Fine: two (sharper banks, a heavier terrain)."), fineness);
            int res = TerrainResample.Resolution(TerrainResample.Sizes[size], fineness);
            float mb = TerrainResample.MemoryMb(res);
            EditorGUILayout.LabelField(" ", $"{res}² heights, {TerrainResample.Sizes[size] / (res - 1):F2} m a sample, {mb:F0} MB", EditorStyles.miniLabel);
            if (mb > TerrainResample.WarnMb) EditorGUILayout.HelpBox($"Over {TerrainResample.WarnMb:F0} MB of terrain data: heavy to load and to save.", MessageType.Warning);
            problem = LevelScenes.NameProblem(levelName);
            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
            using (new EditorGUI.DisabledScope(problem != null))
                if (GUILayout.Button("Create Level") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    LevelScenes.Create(levelName, displayName, minPlayers, day, maxPlayers, LevelScenes.LevelsFolder, TerrainResample.Sizes[size], res);
                    Close();
                }
        }
    }
}
