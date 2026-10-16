using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// HotPatata/Level/Verify Level (ARCHITECTURE §27): <see cref="LevelVerifier"/> on the open level, as a list. Click a line to select
    /// what it is about (and frame it in the Scene view). Errors keep a level out of the menu.
    /// </summary>
    public sealed class LevelCheckWindow : EditorWindow
    {
        List<LevelFinding> findings = new List<LevelFinding>();
        Vector2 scroll;
        string level = "";
        bool showWarnings = true, showInfo = true;

        [MenuItem("HotPatata/Level/Verify Level", priority = 40)]
        public static void Open()
        {
            var w = GetWindow<LevelCheckWindow>(false, "Verify Level");
            w.minSize = new Vector2(480, 300);
            w.Run();
        }

        void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            level = scene.name;
            findings = LevelVerifier.Run(scene);
            Repaint();
        }

        void OnGUI()
        {
            int errors = findings.Count(f => f.severity == LevelFinding.Severity.Error);
            int warnings = findings.Count(f => f.severity == LevelFinding.Severity.Warning);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Verify again", EditorStyles.toolbarButton, GUILayout.Width(90))) Run();
                showWarnings = GUILayout.Toggle(showWarnings, $"Warnings ({warnings})", EditorStyles.toolbarButton);
                showInfo = GUILayout.Toggle(showInfo, "Info", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
                GUILayout.Label(level, EditorStyles.miniLabel);
            }
            if (errors == 0)
                EditorGUILayout.HelpBox(warnings == 0 ? "Nothing to fix: the level can go in the menu." : "No error: the level can go in the menu (look at the warnings).",
                                        MessageType.Info);
            else EditorGUILayout.HelpBox($"{errors} error(s): fix them before the level goes in the menu.", MessageType.Error);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var f in findings)
            {
                if (f.severity == LevelFinding.Severity.Warning && !showWarnings) continue;
                if (f.severity == LevelFinding.Severity.Info && !showInfo) continue;
                var icon = f.severity switch
                {
                    LevelFinding.Severity.Error => EditorGUIUtility.IconContent("console.erroricon.sml"),
                    LevelFinding.Severity.Warning => EditorGUIUtility.IconContent("console.warnicon.sml"),
                    _ => EditorGUIUtility.IconContent("console.infoicon.sml"),
                };
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(18));
                    var style = new GUIStyle(EditorStyles.label) { wordWrap = true };
                    if (GUILayout.Button(f.message, style)) Show(f);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        static void Show(LevelFinding f)
        {
            if (f.context != null)
            {
                Selection.activeObject = f.context;
                EditorGUIUtility.PingObject(f.context);
                SceneView.FrameLastActiveSceneView();
            }
            else if (f.at.HasValue && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(f.at.Value, SceneView.lastActiveSceneView.rotation, 8f);
        }
    }

    /// <summary>The level's play and course commands (ARCHITECTURE §27): play from a checkpoint, the route runner, checkpoints and the finish.</summary>
    public static class LevelPlayMenu
    {
        const string Root = "HotPatata/Level/";
        const string Add = "GameObject/HotPatata Level/";

        [MenuItem(Root + "Play From Selected Checkpoint", priority = 41)]
        static void PlayFromSelected()
        {
            var cp = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Checkpoint>() : null;
            PlayFrom(cp != null ? cp.Id : 0);
        }

        /// <summary>Enters Play with the run starting at checkpoint <paramref name="id"/> (0 = the start).</summary>
        public static void PlayFrom(int id)
        {
            if (EditorApplication.isPlaying) return;
            SessionState.SetInt(EditorPlayRequests.PlayFromKey, id);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(Root + "Run Route Runner", priority = 42)]
        static void RunRouteRunner()
        {
            if (EditorApplication.isPlaying) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);   // the walk lines are gathered on save
            SessionState.SetBool(EditorPlayRequests.RouteRunKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[RouteRunner] player 1 walks every walkway, stair and bridge; the result comes in the Console.");
        }

        [MenuItem(Root + "Number Checkpoints Along The Course", priority = 43)]
        public static void Number()
        {
            var ordered = LevelCourse.Number(EditorSceneManager.GetActiveScene());
            Debug.Log("[Level] checkpoints in order: " + (ordered.Count == 0 ? "none" : string.Join(", ", ordered.Select(c => $"{c.name}"))));
        }

        [MenuItem(Add + "Checkpoint (campfire)", priority = 70)]
        static void AddCheckpoint()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var settings = LevelScenes.SettingsOf(scene);
            var cp = LevelCourse.AddCheckpoint(scene, settings != null ? settings.Group(LevelGroup.Kit) : null, ViewGround());
            Selection.activeGameObject = cp.gameObject;
        }

        [MenuItem(Add + "Finish", priority = 71)]
        static void AddFinish()
        {
            var settings = LevelScenes.SettingsOf(EditorSceneManager.GetActiveScene());
            var finish = LevelCourse.AddFinish(settings != null ? settings.Group(LevelGroup.Kit) : null, ViewGround());
            Selection.activeGameObject = finish.gameObject;
        }

        /// <summary>The floor the Scene view looks at: a checkpoint or the finish goes on the deck in view, not on the ground under it.</summary>
        static Vector3 ViewGround() => LevelNature.ViewFloor(EditorSceneManager.GetActiveScene());
    }
}
