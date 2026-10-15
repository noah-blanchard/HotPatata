using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// Where the meshes of an authored level's pieces live (ARCHITECTURE §27): in memory while they are edited, then, when the scene is
    /// saved, in <c>Assets/Art/Generated/&lt;Level&gt;/Pieces/&lt;piece id&gt;/</c> (Git LFS), one folder per piece so a piece's rebuild
    /// only ever replaces its own. A folder whose piece left the scene is deleted with it.
    /// </summary>
    public static class LevelMeshStore
    {
        public const string GeneratedRoot = "Assets/Art/Generated/";

        public static string LevelFolder(Scene scene) => GeneratedRoot + scene.name + "/";

        public static string PiecesFolder(string levelFolder) => levelFolder + "Pieces/";

        /// <summary>Writes the meshes of every piece of <paramref name="scene"/> still in memory; returns how many were written.</summary>
        public static int Persist(Scene scene) =>
            string.IsNullOrEmpty(scene.path) ? 0 : Persist(TimberLinks.Pieces(scene), LevelFolder(scene));

        /// <summary>Writes the in-memory meshes of <paramref name="pieces"/> under <paramref name="levelFolder"/>.</summary>
        public static int Persist(IEnumerable<LevelPiece> pieces, string levelFolder)
        {
            int written = 0;
            foreach (var piece in pieces)
            {
                if (piece.Output == null) continue;
                var fresh = LevelPieceBuilder.Meshes(piece.Output).Where(m => !EditorUtility.IsPersistent(m)).ToList();
                if (fresh.Count == 0) continue;
                piece.EnsureId();
                OrganicAssets.Begin(PiecesFolder(levelFolder) + piece.PieceId + "/");
                try
                {
                    var saved = new Dictionary<Mesh, Mesh>();
                    foreach (var mesh in fresh) saved[mesh] = OrganicAssets.Save(mesh);
                    foreach (var filter in piece.Output.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.sharedMesh != null && saved.TryGetValue(filter.sharedMesh, out var m)) filter.sharedMesh = m;
                    foreach (var collider in piece.Output.GetComponentsInChildren<MeshCollider>(true))
                        if (collider.sharedMesh != null && saved.TryGetValue(collider.sharedMesh, out var m)) collider.sharedMesh = m;
                    written += saved.Count;
                }
                finally { OrganicAssets.End(); }
            }
            return written;
        }

        /// <summary>Deletes the mesh folders of pieces no longer in the level; returns how many.</summary>
        public static int DeleteOrphans(Scene scene) =>
            string.IsNullOrEmpty(scene.path) ? 0 : DeleteOrphans(TimberLinks.Pieces(scene).Select(p => p.PieceId), LevelFolder(scene));

        public static int DeleteOrphans(IEnumerable<string> liveIds, string levelFolder)
        {
            string folder = PiecesFolder(levelFolder).TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder)) return 0;
            var live = new HashSet<string>(liveIds.Where(id => !string.IsNullOrEmpty(id)));
            int deleted = 0;
            foreach (var dir in Directory.GetDirectories(folder))
            {
                string id = Path.GetFileName(dir);
                if (live.Contains(id)) continue;
                if (AssetDatabase.DeleteAsset(dir.Replace('\\', '/'))) deleted++;
            }
            return deleted;
        }
    }
}
