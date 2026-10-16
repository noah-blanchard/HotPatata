using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// Keeps the timber elements in step with their data (ARCHITECTURE §27.1): once the mouse lets go after an edit (a move, the
    /// inspector, a delete, an undo), every element whose shape changed is redrawn, every rope follows what it hangs on, and every frozen
    /// structure whose walkable boards moved lays its walking surface again. New meshes go into the catalogue at once.
    /// </summary>
    [InitializeOnLoad]
    public static class TimberElementWatcher
    {
        static bool pending;

        /// <summary>Off in the tests that drive the refreshes themselves.</summary>
        public static bool Enabled { get; set; } = true;

        static TimberElementWatcher()
        {
            ObjectChangeEvents.changesPublished += OnChanges;
            Undo.undoRedoPerformed += () => pending = true;
            EditorApplication.update += Tick;
            EditorSceneManager.sceneSaving += (scene, path) => TimberCatalog.Flush();
            PrefabStage.prefabSaving += _ => TimberCatalog.Flush();
        }

        static void OnChanges(ref ObjectChangeEventStream stream)
        {
            if (stream.length > 0) pending = true;
        }

        static void Tick()
        {
            if (!pending || !Enabled || GUIUtility.hotControl != 0 || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            pending = false;
            Flush();
        }

        /// <summary>Refreshes every element, wall and surface of the open scenes and the open prefab; returns how many changed.</summary>
        public static int Flush()
        {
            var roots = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) roots.AddRange(scene.GetRootGameObjects());
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null) roots.Add(stage.prefabContentsRoot);
            return Flush(roots);
        }

        public static int Flush(IEnumerable<GameObject> roots)
        {
            int changed = 0;
            var list = roots.Where(r => r != null).ToList();
            // anchors first: a rope follows what it hangs on, which may itself be an element
            foreach (var element in list.SelectMany(r => r.GetComponentsInChildren<TimberElement>(true)).OrderBy(e => e is TimberPath ? 1 : 0))
                if (TimberElementBuilder.Refresh(element)) changed++;
            foreach (var wall in list.SelectMany(r => r.GetComponentsInChildren<TimberWall>(true)))
                if (TimberElementBuilder.Refresh(wall)) changed++;
            foreach (var structure in list.SelectMany(r => r.GetComponentsInChildren<FrozenStructure>(true)))
                if (WalkSurfaceBuilder.Refresh(structure)) changed++;
            if (changed == 0) return 0;
            TimberCatalog.Flush();
            Physics.SyncTransforms();
            return changed;
        }
    }
}
