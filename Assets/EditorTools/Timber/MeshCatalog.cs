using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// The one store of the meshes the level tools make (ARCHITECTURE §27.1): timber elements, the pieces' drawn timber and walking
    /// surfaces, giant trees, water, the kit's forest looks. A mesh is named after what it is (its shape's key, or its content's hash)
    /// and kept once whoever uses it: small meshes as sub-assets of sixteen <see cref="MeshBank"/> files, large ones (over
    /// <see cref="LargeVertices"/>) as files of their own in <c>Meshes/</c>, all in <see cref="Folder"/>. Meshes are written as they are
    /// made (a prefab may hold them) and removed once no scene, prefab or open object uses them (on every save,
    /// <see cref="CleanSoon"/>). A level's own data (its terrain, its forests' planting, its mist) stays in its level folder.
    /// </summary>
    public static class MeshCatalog
    {
        public const string DefaultFolder = "Assets/Art/Generated/Catalog/";
        /// <summary>A mesh with more vertices than this is a file of its own (a bank is rewritten whole when one of its meshes changes).</summary>
        public const int LargeVertices = 4000;

        static string folder = DefaultFolder;

        /// <summary>Where the catalogue is (the tests point it at a folder of their own).</summary>
        public static string Folder
        {
            get => folder;
            set
            {
                folder = value.EndsWith("/") ? value : value + "/";
                Reload();
            }
        }

        public static string LargeFolder => Folder + "Meshes/";

        static Dictionary<string, Mesh> index;
        static readonly List<Mesh> pending = new List<Mesh>();

        /// <summary>The mesh for <paramref name="key"/>: the catalogue's, else <paramref name="build"/>'s, added to it.</summary>
        public static Mesh Get(string key, Func<Mesh> build)
        {
            Load();
            string name = NameOf(key);
            if (index.TryGetValue(name, out var mesh))
            {
                if (mesh != null) return mesh;
                Reload();   // a bank was cleaned or deleted on disk
                Load();
                if (index.TryGetValue(name, out mesh) && mesh != null) return mesh;
            }
            mesh = build();
            mesh.name = name;
            index[name] = mesh;
            pending.Add(mesh);
            return mesh;
        }

        /// <summary>
        /// The catalogue's copy of <paramref name="mesh"/> (found by its content): the one already there, else this one, added. A mesh
        /// already in the catalogue is itself; a mesh of another folder is copied in (it is not moved).
        /// </summary>
        public static Mesh Store(Mesh mesh)
        {
            if (mesh == null || Contains(mesh)) return mesh;
            var copy = EditorUtility.IsPersistent(mesh) ? Object.Instantiate(mesh) : mesh;
            var stored = Get("mesh|" + ContentHash(copy).ToString("x16"), () => copy);
            if (stored != copy && !EditorUtility.IsPersistent(copy)) Object.DestroyImmediate(copy);
            return stored;
        }

        /// <summary>True if <paramref name="mesh"/> is one of the catalogue's (written or about to be).</summary>
        public static bool Contains(Mesh mesh)
        {
            if (mesh == null) return false;
            if (pending.Contains(mesh)) return true;
            string path = AssetDatabase.GetAssetPath(mesh);
            return !string.IsNullOrEmpty(path) && path.StartsWith(Folder);
        }

        /// <summary>The catalogue name of a key: "t" and 16 hex digits of its hash.</summary>
        public static string NameOf(string key) => "t" + Hash(key).ToString("x16");

        /// <summary>Writes the meshes made since the last flush; returns how many.</summary>
        public static int Flush()
        {
            var fresh = pending.Where(m => m != null && !EditorUtility.IsPersistent(m)).ToList();
            pending.Clear();
            if (fresh.Count == 0) return 0;
            Directory.CreateDirectory(Folder.TrimEnd('/'));
            var touched = new HashSet<MeshBank>();
            foreach (var mesh in fresh)
            {
                if (mesh.vertexCount > LargeVertices)
                {
                    Directory.CreateDirectory(LargeFolder.TrimEnd('/'));
                    AssetDatabase.CreateAsset(mesh, LargeFolder + mesh.name + ".asset");
                    continue;
                }
                var bank = Bank(mesh.name);
                AssetDatabase.AddObjectToAsset(mesh, bank);
                touched.Add(bank);
            }
            foreach (var bank in touched)
            {
                EditorUtility.SetDirty(bank);
                AssetDatabase.SaveAssetIfDirty(bank);
            }
            return fresh.Count;
        }

        static MeshBank Bank(string meshName)
        {
            string path = BankPath(meshName);
            var bank = AssetDatabase.LoadAssetAtPath<MeshBank>(path);
            if (bank != null) return bank;
            bank = ScriptableObject.CreateInstance<MeshBank>();
            AssetDatabase.CreateAsset(bank, path);
            return bank;
        }

        /// <summary>The bank a mesh lives in: by the first hex digit of its hash.</summary>
        public static string BankPath(string meshName) => Folder + "Bank_" + (meshName.Length > 1 ? meshName[1] : '0') + ".asset";

        static IEnumerable<string> BankPaths() =>
            AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))
                ? AssetDatabase.FindAssets("t:MeshBank", new[] { Folder.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath)
                : Enumerable.Empty<string>();

        static IEnumerable<string> LargePaths() =>
            AssetDatabase.IsValidFolder(LargeFolder.TrimEnd('/'))
                ? AssetDatabase.FindAssets("t:Mesh", new[] { LargeFolder.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath)
                : Enumerable.Empty<string>();

        static void Load()
        {
            if (index != null) return;
            index = new Dictionary<string, Mesh>();
            foreach (var path in BankPaths())
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                    index[mesh.name] = mesh;
            foreach (var path in LargePaths())
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh != null) index[mesh.name] = mesh;
            }
            foreach (var mesh in pending.Where(m => m != null)) index[mesh.name] = mesh;
        }

        /// <summary>Forgets what was loaded (the catalogue changed on disk).</summary>
        public static void Reload() => index = null;

        static ulong Hash(string key)
        {
            unchecked
            {
                ulong h = 14695981039346656037ul;
                foreach (char c in key) h = (h ^ c) * 1099511628211ul;
                return h;
            }
        }

        /// <summary>A hash of what a mesh draws: its vertices (to a tenth of a millimetre), normals, UVs and triangles per submesh.</summary>
        public static ulong ContentHash(Mesh mesh)
        {
            unchecked
            {
                ulong h = 14695981039346656037ul;
                void Add(long v) { h = (h ^ (ulong)v) * 1099511628211ul; }
                foreach (var v in mesh.vertices) { Add(Mathf.RoundToInt(v.x * 1e4f)); Add(Mathf.RoundToInt(v.y * 1e4f)); Add(Mathf.RoundToInt(v.z * 1e4f)); }
                foreach (var n in mesh.normals) { Add(Mathf.RoundToInt(n.x * 1e3f)); Add(Mathf.RoundToInt(n.y * 1e3f)); Add(Mathf.RoundToInt(n.z * 1e3f)); }
                foreach (var u in mesh.uv) { Add(Mathf.RoundToInt(u.x * 1e4f)); Add(Mathf.RoundToInt(u.y * 1e4f)); }
                Add(mesh.subMeshCount);
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    Add(-1);
                    foreach (int i in mesh.GetTriangles(s)) Add(i);
                }
                return h;
            }
        }

        // ------------------------------------------------------------------ cleaning

        static bool cleanQueued;

        /// <summary>Cleans the catalogue once the editor is idle (after a save): what nothing uses any more goes.</summary>
        public static void CleanSoon()
        {
            if (cleanQueued) return;
            cleanQueued = true;
            EditorApplication.delayCall += () =>
            {
                cleanQueued = false;
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                int removed = Clean();
                if (removed > 0) Debug.Log($"[Catalog] {removed} mesh(es) nothing uses any more removed");
            };
        }

        [MenuItem("HotPatata/Level/Clean Mesh Catalog", priority = 40)]
        static void CleanMenu() => Debug.Log($"[Catalog] cleaned: {Clean()} unused mesh(es) removed");

        /// <summary>
        /// Removes the meshes nothing uses: no saved scene, prefab or generated asset (<paramref name="files"/>), and no object of an open
        /// scene or prefab. Returns how many.
        /// </summary>
        public static int Clean(bool files = true)
        {
            Flush();
            var banks = BankPaths().ToList();
            var large = LargePaths().ToList();
            if (banks.Count == 0 && large.Count == 0) return 0;
            var guids = new HashSet<string>(banks.Concat(large).Select(AssetDatabase.AssetPathToGUID));
            var used = new HashSet<(string, long)>();
            if (files)
                foreach (var path in Users())
                    foreach (var reference in ReferencesIn(path, guids)) used.Add(reference);
            foreach (var mesh in OpenMeshes())
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long id) && guids.Contains(guid)) used.Add((guid, id));

            int removed = 0;
            foreach (var path in banks)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                bool changed = false;
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToList())
                {
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out _, out long id) || used.Contains((guid, id))) continue;
                    Object.DestroyImmediate(mesh, true);
                    removed++;
                    changed = true;
                }
                if (!changed) continue;
                var bank = AssetDatabase.LoadAssetAtPath<MeshBank>(path);
                EditorUtility.SetDirty(bank);
                AssetDatabase.SaveAssetIfDirty(bank);
            }
            foreach (var path in large)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (used.Any(u => u.Item1 == guid)) continue;
                AssetDatabase.DeleteAsset(path);
                removed++;
            }
            if (removed > 0) Reload();
            return removed;
        }

        /// <summary>The files that may use catalogue meshes: every scene and prefab, and the generated assets (not meshes) outside the catalogue.</summary>
        static IEnumerable<string> Users() =>
            AssetDatabase.FindAssets("t:Scene t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Concat(Directory.Exists("Assets/Art/Generated")
                    ? Directory.GetFiles("Assets/Art/Generated", "*.asset", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/'))
                        .Where(p => !p.StartsWith(Folder) && AssetDatabase.GetMainAssetTypeAtPath(p) != typeof(Mesh))
                    : Enumerable.Empty<string>())
                .Distinct();

        static readonly Regex Reference = new Regex(@"fileID: (-?\d+), guid: ([0-9a-f]{32})", RegexOptions.Compiled);
        static readonly Dictionary<string, (DateTime time, List<(string, long)> refs)> scanned = new Dictionary<string, (DateTime, List<(string, long)>)>();

        /// <summary>The (file guid, object id) pairs a YAML file references among <paramref name="guids"/> (read again only when it changed).</summary>
        static IEnumerable<(string, long)> ReferencesIn(string path, HashSet<string> guids)
        {
            if (!File.Exists(path)) return Enumerable.Empty<(string, long)>();
            var time = File.GetLastWriteTimeUtc(path);
            if (!scanned.TryGetValue(path, out var entry) || entry.time != time)
            {
                var refs = new List<(string, long)>();
                string text = File.ReadAllText(path);
                if (text.StartsWith("%YAML"))
                    foreach (Match m in Reference.Matches(text))
                        refs.Add((m.Groups[2].Value, long.Parse(m.Groups[1].Value)));
                entry = (time, refs.Distinct().ToList());
                scanned[path] = entry;
            }
            return entry.refs.Where(r => guids.Contains(r.Item1));
        }

        /// <summary>The meshes the open scenes and the open prefab draw and collide with (saved or not).</summary>
        static IEnumerable<Mesh> OpenMeshes()
        {
            var roots = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) roots.AddRange(scene.GetRootGameObjects());
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null) roots.Add(stage.prefabContentsRoot);
            foreach (var root in roots)
            {
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null) yield return f.sharedMesh;
                foreach (var c in root.GetComponentsInChildren<MeshCollider>(true)) if (c.sharedMesh != null) yield return c.sharedMesh;
            }
        }
    }
}
