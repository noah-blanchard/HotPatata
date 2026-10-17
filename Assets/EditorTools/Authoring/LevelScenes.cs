using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Makes an authored level (ARCHITECTURE §27, docs/LEVEL_AUTHORING.md): a scene in <see cref="LevelsFolder"/> copied from the
    /// courses' template (its run, spawns, bomb, camera and networking), cleared, given its <see cref="LevelSettings"/>, its groups, a
    /// flat terrain under the start, a safety net and the day it borrows. Ready to play from the first second.
    /// </summary>
    public static class LevelScenes
    {
        public const string LevelsFolder = "Assets/Scenes/Levels/";
        /// <summary>The terrain of a new level: its size (m), how deep it can be dug under the start and how high it can rise.</summary>
        public const float TerrainSize = 256f, TerrainDepth = 20f, TerrainHeight = 120f;
        public const int TerrainResolution = 257;
        /// <summary>The safety net under everything: this far under the terrain's lowest point.</summary>
        public const float NetDrop = 25f;

        public static string ScenePath(string name) => LevelsFolder + name + ".unity";

        /// <summary>A level's name: a scene name the menu loads it by (letters, digits, no spaces), not taken by another scene.</summary>
        public static string NameProblem(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Give the level a name.";
            if (!Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_]*$")) return "Letters, digits and _ only, starting with a letter (the scene's name).";
            if (AssetDatabase.FindAssets("t:Scene " + name).Select(AssetDatabase.GUIDToAssetPath).Any(p => Path.GetFileNameWithoutExtension(p) == name))
                return "A scene of that name exists already.";
            return null;
        }

        /// <summary>
        /// Makes the level <paramref name="name"/> (in <paramref name="folder"/>, the levels' folder by default) with a terrain of
        /// <paramref name="size"/> metres a side and <paramref name="resolution"/> height samples (0: the defaults).
        /// </summary>
        public static Scene Create(string name, string displayName, int minPlayers, LevelDay day, int maxPlayers = 4, string folder = LevelsFolder,
                                   float size = 0f, int resolution = 0)
        {
            string problem = NameProblem(name);
            if (problem != null) throw new ArgumentException(problem);
            folder = folder.TrimEnd('/') + "/";
            if (!AssetDatabase.IsValidFolder(folder.TrimEnd('/'))) AssetDatabase.CreateFolder(Path.GetDirectoryName(folder.TrimEnd('/')).Replace('\\', '/'), Path.GetFileName(folder.TrimEnd('/')));
            var scene = CourseKit.PrepareCourseScene(folder + name + ".unity", out var section);
            // the template's plant exposure is the plant's own (a closed, lamp-lit factory)
            foreach (var volume in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Volume>(true)).ToArray())
                if (volume.sharedProfile != null && AssetDatabase.GetAssetPath(volume.sharedProfile) == LookBuilder.PlantProfilePath)
                    Object.DestroyImmediate(volume.gameObject);

            var level = new GameObject("Level").transform;
            level.SetParent(section, false);
            var settings = level.gameObject.AddComponent<LevelSettings>();
            settings.DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
            settings.MinPlayers = minPlayers;
            settings.MaxPlayers = maxPlayers;
            settings.Day = day;
            Transform Group(string groupName)
            {
                var g = new GameObject(groupName).transform;
                g.SetParent(level, false);
                return g;
            }
            settings.SetGroups(Group("Timber"), Group("Trees"), Group("Kit"), Group("Props"));

            var starts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerSpawn>(true))
                .Where(s => s.GetComponentInParent<Checkpoint>() == null).Select(s => s.transform.position).ToList();
            var start = starts.Count > 0 ? starts.Aggregate(Vector3.zero, (a, b) => a + b) / starts.Count : Vector3.zero;
            float groundY = starts.Count > 0 ? starts.Min(s => s.y) : 0f;
            var terrain = MakeTerrain(name, level, new Vector3(start.x, groundY, start.z), size > 0f ? size : TerrainSize, resolution > 0 ? resolution : TerrainResolution);
            SafetyNet(level, terrain);
            LevelTerrain.SetupBrushes(terrain);

            if (section.GetComponent<RouteWalks>() == null) section.gameObject.AddComponent<RouteWalks>();
            LevelTerrain.BakeMist(scene, settings);
            LevelTerrain.WriteAudio(scene, settings);
            LookBuilder.ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Level] {name} made at {folder}{name}.unity: register it in the menu with HotPatata/Level/Register In Menu when it is ready.");
            return scene;
        }

        /// <summary>
        /// A flat Unity terrain centred on the start, its surface at <paramref name="surface"/>'s height (it can be dug and raised),
        /// drawn in the nature look (<see cref="LevelTerrain.ApplyLook"/>).
        /// </summary>
        static Terrain MakeTerrain(string levelName, Transform parent, Vector3 surface, float size, int resolution)
        {
            string folder = LevelMeshStore.GeneratedRoot + levelName + "/Terrain";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var data = new TerrainData { heightmapResolution = resolution };
            resolution = data.heightmapResolution;
            data.size = new Vector3(size, TerrainDepth + TerrainHeight, size);
            float flat = TerrainDepth / (TerrainDepth + TerrainHeight);
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++) heights[z, x] = flat;
            data.SetHeights(0, 0, heights);
            AssetDatabase.CreateAsset(data, folder + "/" + levelName + "_Terrain.asset");

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.layer = CourseKit.Layer("Environment");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(surface.x - size / 2f, surface.y - TerrainDepth, surface.z - size / 2f);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            var terrain = go.GetComponent<Terrain>();
            LevelTerrain.ApplyLook(terrain);
            return terrain;
        }

        /// <summary>A kill zone under the whole terrain: nothing falls out of the world.</summary>
        static void SafetyNet(Transform parent, Terrain terrain)
        {
            float size = terrain.terrainData.size.x;
            var at = terrain.transform.position + new Vector3(size / 2f, -NetDrop, size / 2f);
            var net = CourseKit.Place(parent, CourseKit.GameplayDir + "KillZone", "Safety net", at, Quaternion.identity);
            net.transform.localScale = new Vector3(size * 3f, 4f, size * 3f);
            net.AddComponent<LevelSafetyNet>();
        }

        /// <summary>The authored levels' scenes.</summary>
        public static string[] All() =>
            AssetDatabase.IsValidFolder(LevelsFolder.TrimEnd('/'))
                ? AssetDatabase.FindAssets("t:Scene", new[] { LevelsFolder.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray()
                : Array.Empty<string>();

        public static LevelSettings SettingsOf(Scene scene) =>
            scene.IsValid() ? scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelSettings>(true)).FirstOrDefault() : null;
    }
}
