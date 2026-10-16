using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Hands PatataRanger over to be edited by hand (M18.4c, run once): its builder draws it one last time with every piece of timber
    /// frozen in place (<see cref="ElementRecorder.FreezeInPlace"/>: decks, walkways, bridges, rails, huts, booths and towers as elements
    /// under their own structures), then the scene gets an authored level's settings (its menu name, two players, its own day, the
    /// music and ambience of each section as the builder wrote them) and is saved. From then on the scene is the source: the builder
    /// goes. Menu HotPatata/Course/Convert PatataRanger To Authored.
    /// </summary>
    public static class RangerConversion
    {
        [MenuItem("HotPatata/Course/Convert PatataRanger To Authored")]
        static void ConvertMenu()
        {
            if (!EditorUtility.DisplayDialog("Convert PatataRanger", "PatataRanger is drawn one last time with its timber frozen into elements, " +
                                             "then becomes an authored level edited by hand.", "Convert", "Cancel")) return;
            Debug.Log("[Ranger] " + Convert());
        }

        public static string Convert()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "cancelled";
            EditorSceneManager.OpenScene(PatataRangerBuilder.ScenePath, OpenSceneMode.Single);
            bool pieces = LevelPieceWatcher.Enabled, elements = TimberElementWatcher.Enabled;
            LevelPieceWatcher.Enabled = false;
            TimberElementWatcher.Enabled = false;
            ElementRecorder.FreezeInPlace = true;
            try { PatataRangerBuilder.Build(); }
            finally
            {
                ElementRecorder.FreezeInPlace = false;
                ElementRecorder.LastInPlace = null;
                LevelPieceWatcher.Enabled = pieces;
                TimberElementWatcher.Enabled = elements;
            }
            TimberCatalog.Flush();

            var scene = EditorSceneManager.GetActiveScene();
            var section = LevelRoutes.SectionRoot(scene);
            var group = section.Cast<Transform>().FirstOrDefault(t => t.GetComponentInChildren<FrozenStructure>(true) != null);
            var level = new GameObject("Level");
            var settings = level.AddComponent<LevelSettings>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CourseKit.NetworkPrefab);
            var entry = CourseKit.ReadMenu(new SerializedObject(prefab.GetComponent<NetworkBootstrap>())).FirstOrDefault(e => e.scene == scene.name);
            settings.DisplayName = entry.displayName ?? "";
            settings.MinPlayers = PatataRangerBuilder.Players;
            settings.Day = LevelDay.Ranger;
            settings.DefaultAmbience = "Forest";
            var audio = section.GetComponent<CourseAudio>();
            for (int id = 0; id <= PatataRangerBuilder.CheckpointCount; id++)
            {
                settings.SectionActs.Add(audio != null ? audio.ActAt(id) : 1);
                settings.SectionAmbience.Add(audio != null ? audio.EnvironmentAt(id) : "");
            }
            settings.SetGroups(group != null ? group : section, group != null ? group : section, section, group != null ? group : section);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            int structures = scene.GetRootGameObjects().Sum(g => g.GetComponentsInChildren<FrozenStructure>(true).Length);
            int count = scene.GetRootGameObjects().Sum(g => g.GetComponentsInChildren<TimberElement>(true).Length);
            return $"PatataRanger converted: {structures} structure(s), {count} element(s), saved as an authored level.";
        }
    }
}
