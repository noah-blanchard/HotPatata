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
    /// Keeps an authored level's geometry in step with its data (ARCHITECTURE §27): after any edit (a handle, the inspector, a move,
    /// an undo) the pieces whose inputs changed are rebuilt once the mouse lets go of the handle; duplicated pieces get their own id;
    /// on save the meshes are written, the orphan folders deleted, the level's walk lines gathered and the quick checks logged.
    /// </summary>
    [InitializeOnLoad]
    public static class LevelPieceWatcher
    {
        static bool pending;

        /// <summary>Off in the tests that drive the rebuilds themselves.</summary>
        public static bool Enabled { get; set; } = true;

        static LevelPieceWatcher()
        {
            ObjectChangeEvents.changesPublished += OnChanges;
            Undo.undoRedoPerformed += () => pending = true;
            EditorApplication.update += Tick;
            EditorSceneManager.sceneSaving += OnSaving;
        }

        static void OnChanges(ref ObjectChangeEventStream stream)
        {
            // any edit may move a piece, a deck an end sits on, or a parent: the signatures tell which pieces really changed
            if (stream.length > 0) pending = true;
        }

        static void Tick()
        {
            if (!pending || !Enabled || GUIUtility.hotControl != 0 || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            pending = false;
            Flush();
        }

        /// <summary>Rebuilds now every piece of the open scenes whose inputs changed; returns how many.</summary>
        public static int Flush()
        {
            int rebuilt = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++) rebuilt += Flush(SceneManager.GetSceneAt(i));
            return rebuilt;
        }

        public static int Flush(Scene scene)
        {
            var pieces = TimberLinks.Pieces(scene).ToList();
            if (pieces.Count == 0) return 0;
            RenewDuplicates(pieces);
            int count = LevelPieceBuilder.Refresh(pieces);
            if (count > 0 && LevelPieceBuilder.LastMilliseconds > 500)
                Debug.LogWarning($"[Level] rebuilding {count} piece(s) took {LevelPieceBuilder.LastMilliseconds:F0} ms");
            return count;
        }

        /// <summary>A duplicated piece shares its original's id (and so its mesh folder): it gets its own, and is rebuilt.</summary>
        static void RenewDuplicates(List<LevelPiece> pieces)
        {
            foreach (var group in pieces.Where(p => !string.IsNullOrEmpty(p.PieceId)).GroupBy(p => p.PieceId).Where(g => g.Count() > 1))
                foreach (var copy in group.Skip(1))
                {
                    copy.EnsureId(renew: true);
                    LevelPieceBuilder.Clear(copy);
                    EditorUtility.SetDirty(copy);
                }
        }

        static void OnSaving(Scene scene, string path)
        {
            if (!Enabled || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!TimberLinks.Pieces(scene).Any() && Object.FindObjectsByType<LevelSettings>(FindObjectsSortMode.None).All(s => s.gameObject.scene != scene)) return;
            Flush(scene);
            int written = LevelMeshStore.Persist(scene);
            int orphans = LevelMeshStore.DeleteOrphans(scene);
            LevelRoutes.Gather(scene);
            string land = LevelTerrain.OnSave(scene);
            var findings = LevelQuickCheck.Run(scene);
            string summary = $"[Level] {scene.name} saved: {TimberLinks.Pieces(scene).Count()} piece(s), {written} asset(s) written, {orphans} old folder(s) removed{land}";
            if (findings.Count == 0) Debug.Log(summary + ", quick checks clean.");
            else Debug.LogWarning(summary + $", {findings.Count} quick check finding(s):\n- " + string.Join("\n- ", findings));
        }
    }
}
