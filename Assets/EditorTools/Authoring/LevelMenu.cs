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
    /// The authored levels' menus (ARCHITECTURE §27, docs/LEVEL_AUTHORING.md): HotPatata/Level (new level, register, look, rebuild) and
    /// GameObject/HotPatata Level (a new piece where the Scene view looks, in its level group).
    /// </summary>
    public static class LevelMenu
    {
        const string Root = "HotPatata/Level/";
        const string Add = "GameObject/HotPatata Level/";

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

        [MenuItem(Add + "Deck", priority = 10)] static void AddDeck() => Create<TimberDeckShape>("Deck", 3f);
        [MenuItem(Add + "Walkway or Stair", priority = 11)] static void AddWalk() => Create<TimberWalkShape>("Walkway", 0f);
        [MenuItem(Add + "Rope Bridge", priority = 12)] static void AddBridge() => Create<RopeBridgeShape>("Rope bridge", 3f);
        [MenuItem(Add + "Railing", priority = 13)] static void AddRailing() => Create<RailingShape>("Railing", 0f);
        [MenuItem(Add + "House", priority = 14)] static void AddHouse() => Create<HouseShape>("House", 0f);
        [MenuItem(Add + "Trestle Tower", priority = 15)] static void AddTrestle() => Create<TrestleShape>("Trestle", 6f);
        [MenuItem(Add + "Structure/Shelter", priority = 20)] static void AddShelter() => Create<ShelterShape>("Shelter", 0f);
        [MenuItem(Add + "Structure/Gantry", priority = 21)] static void AddGantry() => Create<GantryShape>("Gantry", 0f);
        [MenuItem(Add + "Structure/Sign", priority = 22)] static void AddSign() => Create<SignShape>("Sign", 0f);
        [MenuItem(Add + "Structure/Pulley Beam", priority = 23)] static void AddPulleyBeam() => Create<PulleyBeamShape>("Pulley beam", 0f);
        [MenuItem(Add + "Giant Tree", priority = 30)] static void AddTree() => Create<HeroTreeShape>("Giant tree", -0.4f, LevelGroup.Trees);
        [MenuItem(Add + "Forest Area", priority = 31)] static void AddForest() => Create<ForestArea>("Forest", 0f, LevelGroup.Trees);
        [MenuItem(Add + "Water", priority = 32)] static void AddWater() => Create<WaterShape>("Water", 0.5f, LevelGroup.Props);

        [MenuItem(Add + "Mist Zone", priority = 50)]
        static void AddMistZone()
        {
            var go = NewObject("Mist zone", Ground(0f) + Vector3.up * 2f, LevelGroup.Props);
            Undo.AddComponent<MistZone>(go);
        }

        [MenuItem(Add + "Sound Source", priority = 51)]
        static void AddSound()
        {
            var settings = LevelScenes.SettingsOf(EditorSceneManager.GetActiveScene());
            var parent = settings != null ? settings.Group(LevelGroup.Props) : null;
            var at = Ground(1f);
            var audio = NatureKit.Ambient(parent, "Sound source", parent != null ? parent.InverseTransformPoint(at) : at, NatureAudioFactory.Ambience.River, 0.8f, 40f);
            audio.GetComponent<AmbientLoop>().Configure("");
            Undo.RegisterCreatedObjectUndo(audio.gameObject, "Add sound source");
            Selection.activeGameObject = audio.gameObject;
        }

        [MenuItem(Add + "Ambience Zone", priority = 52)]
        static void AddAmbienceZone()
        {
            var go = NewObject("Ambience zone", Ground(4f), LevelGroup.Props);
            Undo.AddComponent<AmbienceZone>(go).Configure("Cave", new Vector3(20f, 10f, 20f), 1);
        }

        /// <summary>Where the Scene view looks, on the ground, <paramref name="above"/> it.</summary>
        static Vector3 Ground(float above)
        {
            var view = SceneView.lastActiveSceneView;
            var pivot = view != null ? view.pivot : Vector3.zero;
            // the level's ground (its terrain first): what was just placed there is not the ground
            float ground = LevelNature.GroundAt(EditorSceneManager.GetActiveScene(), new Vector2(pivot.x, pivot.z));
            return new Vector3(pivot.x, (float.IsNaN(ground) ? pivot.y : ground) + above, pivot.z);
        }

        static GameObject NewObject(string name, Vector3 at, LevelGroup group)
        {
            var settings = LevelScenes.SettingsOf(EditorSceneManager.GetActiveScene());
            var go = new GameObject(ObjectNames.GetUniqueName(Siblings(settings, group), name));
            Undo.RegisterCreatedObjectUndo(go, "Add " + name);
            if (settings != null) Undo.SetTransformParent(go.transform, settings.Group(group), "Add " + name);
            go.transform.position = at;
            Selection.activeGameObject = go;
            return go;
        }

        static void Create<T>(string name, float above, LevelGroup group = LevelGroup.Timber) where T : LevelPiece => CreatePiece(typeof(T), name, Ground(above), group);

        /// <summary>A new piece of type <paramref name="type"/> at <paramref name="at"/>, in its level group, selected and built.</summary>
        public static LevelPiece CreatePiece(Type type, string name, Vector3 at, LevelGroup group = LevelGroup.Timber)
        {
            var go = NewObject(name, at, group);
            var piece = (LevelPiece)Undo.AddComponent(go, type);
            piece.EnsureId();
            LevelPieceBuilder.Rebuild(piece);
            return piece;
        }

        static string[] Siblings(LevelSettings settings, LevelGroup group = LevelGroup.Timber) =>
            settings == null ? Array.Empty<string>() : settings.Group(group).Cast<Transform>().Select(t => t.name).ToArray();
    }

    /// <summary>HotPatata/Level/New Level: the level's scene name, its name in the menu, the players it needs and its day.</summary>
    public sealed class NewLevelWindow : EditorWindow
    {
        string levelName = "MyLevel", displayName = "", problem;
        int minPlayers = 2, maxPlayers = 4;
        LevelDay day = LevelDay.Ranger;

        public static void Open()
        {
            var w = GetWindow<NewLevelWindow>(true, "New Level");
            w.minSize = new Vector2(420, 210);
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
            problem = LevelScenes.NameProblem(levelName);
            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
            using (new EditorGUI.DisabledScope(problem != null))
                if (GUILayout.Button("Create Level") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    LevelScenes.Create(levelName, displayName, minPlayers, day, maxPlayers);
                    Close();
                }
        }
    }
}
