using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// The timber catalogue (ARCHITECTURE §27.1): one mesh per shape, shared by every element of that shape in every level and prefab,
    /// kept as sub-assets of sixteen <see cref="TimberMeshBank"/> files in <see cref="Folder"/>. A mesh is named after its shape's key
    /// (a hash), made the first time an element asks for it and written at once (a prefab may hold it). Meshes nothing uses any more
    /// are removed by HotPatata/Level/Clean Timber Catalog.
    /// </summary>
    public static class TimberCatalog
    {
        public const string DefaultFolder = "Assets/Art/Generated/Timber/";
        static string folder = DefaultFolder;

        /// <summary>Where the banks are (the tests point it at a folder of their own).</summary>
        public static string Folder
        {
            get => folder;
            set
            {
                folder = value.EndsWith("/") ? value : value + "/";
                Reload();
            }
        }
        const int Banks = 16;

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

        /// <summary>The catalogue name of a key: "t" and 16 hex digits of its hash.</summary>
        public static string NameOf(string key) => "t" + Hash(key).ToString("x16");

        /// <summary>Writes the meshes made since the last flush into their banks; returns how many.</summary>
        public static int Flush()
        {
            var fresh = pending.Where(m => m != null && !EditorUtility.IsPersistent(m)).ToList();
            pending.Clear();
            if (fresh.Count == 0) return 0;
            Directory.CreateDirectory(Folder.TrimEnd('/'));
            var touched = new HashSet<TimberMeshBank>();
            foreach (var mesh in fresh)
            {
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

        static TimberMeshBank Bank(string meshName)
        {
            string path = BankPath(meshName);
            var bank = AssetDatabase.LoadAssetAtPath<TimberMeshBank>(path);
            if (bank != null) return bank;
            bank = ScriptableObject.CreateInstance<TimberMeshBank>();
            AssetDatabase.CreateAsset(bank, path);
            return bank;
        }

        /// <summary>The bank a mesh lives in: by the first hex digit of its hash.</summary>
        public static string BankPath(string meshName) => Folder + "Bank_" + (meshName.Length > 1 ? meshName[1] : '0') + ".asset";

        static void Load()
        {
            if (index != null) return;
            index = new Dictionary<string, Mesh>();
            if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) return;
            foreach (var guid in AssetDatabase.FindAssets("t:TimberMeshBank", new[] { Folder.TrimEnd('/') }))
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Mesh>())
                    index[mesh.name] = mesh;
            foreach (var mesh in pending.Where(m => m != null)) index[mesh.name] = mesh;
        }

        /// <summary>Forgets what was loaded (the banks changed on disk).</summary>
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

        // ------------------------------------------------------------------ cleaning

        [MenuItem("HotPatata/Level/Clean Timber Catalog", priority = 40)]
        static void CleanMenu()
        {
            int removed = Clean();
            Debug.Log($"[Timber] catalogue cleaned: {removed} unused mesh(es) removed");
        }

        /// <summary>Removes the catalogue's meshes no scene, prefab or open scene uses; returns how many.</summary>
        public static int Clean()
        {
            if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) return 0;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            }
            var used = new HashSet<Object>();
            var users = AssetDatabase.FindAssets("t:Scene t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            foreach (var user in users)
            {
                if (user.EndsWith(".unity"))
                {
                    // a scene lists its dependencies by file: read the meshes it references from the open scene or its YAML
                    foreach (var mesh in MeshesReferencedBy(user)) used.Add(mesh);
                }
                else
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(user);
                    if (prefab == null) continue;
                    foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null) used.Add(f.sharedMesh);
                    foreach (var c in prefab.GetComponentsInChildren<MeshCollider>(true)) if (c.sharedMesh != null) used.Add(c.sharedMesh);
                }
            }
            int removed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:TimberMeshBank", new[] { Folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToList())
                {
                    if (used.Contains(mesh)) continue;
                    Object.DestroyImmediate(mesh, true);
                    removed++;
                }
                var bank = AssetDatabase.LoadAssetAtPath<TimberMeshBank>(path);
                EditorUtility.SetDirty(bank);
                AssetDatabase.SaveAssetIfDirty(bank);
            }
            Reload();
            return removed;
        }

        /// <summary>The catalogue meshes a scene file references (its YAML names them by the bank's guid and the mesh's file id).</summary>
        static IEnumerable<Mesh> MeshesReferencedBy(string scenePath)
        {
            string text = File.ReadAllText(scenePath);
            foreach (var guid in AssetDatabase.FindAssets("t:TimberMeshBank", new[] { Folder.TrimEnd('/') }))
            {
                if (!text.Contains(guid)) continue;
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Mesh>())
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out _, out long id) && text.Contains("fileID: " + id + ", guid: " + guid))
                        yield return mesh;
            }
        }
    }
}
