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
    /// Where what an authored level's pieces make is kept (ARCHITECTURE §27, §27.1): in memory while they are edited, then, when the
    /// scene is saved, their meshes in the mesh catalogue (<see cref="MeshCatalog"/>, shared with the frozen timber, found again by their
    /// content) and their forests' planting (a <see cref="FoliageSet"/>, the level's own data) in
    /// <c>Assets/Art/Generated/&lt;Level&gt;/Foliage/&lt;piece id&gt;.asset</c> (Git LFS). A planting whose piece left the level is
    /// deleted with it; meshes nothing uses leave the catalogue on their own (<see cref="MeshCatalog.CleanSoon"/>).
    /// </summary>
    public static class LevelMeshStore
    {
        public const string GeneratedRoot = "Assets/Art/Generated/";

        public static string LevelFolder(Scene scene) => GeneratedRoot + scene.name + "/";

        public static string FoliageFolder(string levelFolder) => levelFolder + "Foliage/";

        /// <summary>Where the pieces' meshes were kept before the catalogue (one folder per piece): moved out when a level is saved.</summary>
        public static string LegacyPiecesFolder(string levelFolder) => levelFolder + "Pieces/";

        /// <summary>Writes what the pieces of <paramref name="scene"/> hold in memory; returns how many assets were written.</summary>
        public static int Persist(Scene scene) =>
            string.IsNullOrEmpty(scene.path) ? 0 : Persist(TimberLinks.Pieces(scene), LevelFolder(scene));

        /// <summary>
        /// Writes the pieces' meshes into the catalogue (and moves there any a piece still holds from its old folder) and their
        /// planting under <paramref name="levelFolder"/>; returns how many were written.
        /// </summary>
        public static int Persist(IEnumerable<LevelPiece> pieces, string levelFolder)
        {
            int written = 0;
            foreach (var piece in pieces)
            {
                if (piece.Output == null) continue;
                written += PersistFoliage(piece, levelFolder);
                // who draws and collides with what, read before storing: a mesh the catalogue already holds (the same content, a piece
                // rebuilt as it was) is destroyed by Store, and a component holding a destroyed mesh reads null
                var filters = piece.Output.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).Select(f => (f, id: f.sharedMesh.GetInstanceID())).ToList();
                var colliders = piece.Output.GetComponentsInChildren<MeshCollider>(true).Where(c => c.sharedMesh != null).Select(c => (c, id: c.sharedMesh.GetInstanceID())).ToList();
                var saved = new Dictionary<int, Mesh>();
                foreach (var mesh in LevelPieceBuilder.Meshes(piece.Output).Where(m => !MeshCatalog.Contains(m)).ToList())
                {
                    int id = mesh.GetInstanceID();
                    saved[id] = MeshCatalog.Store(mesh);
                }
                if (saved.Count == 0) continue;
                foreach (var (filter, id) in filters)
                    if (saved.TryGetValue(id, out var m)) filter.sharedMesh = m;
                foreach (var (collider, id) in colliders)
                    if (saved.TryGetValue(id, out var m)) collider.sharedMesh = m;
                written += saved.Count;
            }
            MeshCatalog.Flush();
            return written;
        }

        /// <summary>A forest's planting (an in-memory <see cref="FoliageSet"/>, or one in its old piece folder), written as Foliage/&lt;id&gt;.asset.</summary>
        static int PersistFoliage(LevelPiece piece, string levelFolder)
        {
            int written = 0;
            foreach (var instancer in piece.Output.GetComponentsInChildren<FoliageInstancer>(true))
            {
                var set = instancer.Set;
                if (set == null) continue;
                piece.EnsureId();
                string path = FoliageFolder(levelFolder) + piece.PieceId + ".asset";
                if (EditorUtility.IsPersistent(set))
                {
                    // planted before the catalogue: its file moves to the level's foliage folder
                    string at = AssetDatabase.GetAssetPath(set);
                    if (at == path) continue;
                    Directory.CreateDirectory(FoliageFolder(levelFolder).TrimEnd('/'));
                    if (AssetDatabase.LoadAssetAtPath<FoliageSet>(path) == null && string.IsNullOrEmpty(AssetDatabase.MoveAsset(at, path))) written++;
                    continue;
                }
                Directory.CreateDirectory(FoliageFolder(levelFolder).TrimEnd('/'));
                var existing = AssetDatabase.LoadAssetAtPath<FoliageSet>(path);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(set, existing);
                    Object.DestroyImmediate(set);
                    EditorUtility.SetDirty(existing);
                    instancer.Configure(existing);
                }
                else
                {
                    AssetDatabase.CreateAsset(set, path);
                    instancer.Configure(set);
                }
                EditorUtility.SetDirty(instancer);
                written++;
            }
            return written;
        }

        /// <summary>Deletes the plantings of pieces no longer in the level, and the old piece folders once emptied; returns how many.</summary>
        public static int DeleteOrphans(Scene scene) =>
            string.IsNullOrEmpty(scene.path) ? 0 : DeleteOrphans(TimberLinks.Pieces(scene).Select(p => p.PieceId), LevelFolder(scene));

        public static int DeleteOrphans(IEnumerable<string> liveIds, string levelFolder)
        {
            var live = new HashSet<string>(liveIds.Where(id => !string.IsNullOrEmpty(id)));
            int deleted = 0;
            string foliage = FoliageFolder(levelFolder).TrimEnd('/');
            if (AssetDatabase.IsValidFolder(foliage))
                foreach (var file in Directory.GetFiles(foliage, "*.asset"))
                {
                    if (live.Contains(Path.GetFileNameWithoutExtension(file))) continue;
                    if (AssetDatabase.DeleteAsset(file.Replace('\\', '/'))) deleted++;
                }
            // the pieces' old folders: their meshes are in the catalogue now, their plantings in Foliage/ (what is still used stays)
            string legacy = LegacyPiecesFolder(levelFolder).TrimEnd('/');
            if (AssetDatabase.IsValidFolder(legacy))
            {
                var stillUsed = new HashSet<string>(OpenDependencies());
                foreach (var dir in Directory.GetDirectories(legacy))
                {
                    var assets = Directory.GetFiles(dir, "*.asset").Select(f => f.Replace('\\', '/')).ToList();
                    if (assets.Any(stillUsed.Contains)) continue;
                    if (AssetDatabase.DeleteAsset(dir.Replace('\\', '/'))) deleted++;
                }
                if (Directory.GetDirectories(legacy).Length == 0 && Directory.GetFiles(legacy, "*.asset").Length == 0) AssetDatabase.DeleteAsset(legacy);
            }
            return deleted;
        }

        /// <summary>The asset files the open scenes' meshes and plantings come from.</summary>
        static IEnumerable<string> OpenDependencies()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var f in root.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null) yield return AssetDatabase.GetAssetPath(f.sharedMesh);
                    foreach (var c in root.GetComponentsInChildren<MeshCollider>(true)) if (c.sharedMesh != null) yield return AssetDatabase.GetAssetPath(c.sharedMesh);
                    foreach (var fi in root.GetComponentsInChildren<FoliageInstancer>(true)) if (fi.Set != null) yield return AssetDatabase.GetAssetPath(fi.Set);
                }
            }
        }
    }
}
