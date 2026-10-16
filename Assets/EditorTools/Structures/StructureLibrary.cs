using System.IO;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The structure library (ARCHITECTURE §27.1): frozen structures kept as prefabs in <see cref="Folder"/>, sorted by kind in its
    /// subfolders, dragged into any level from the Project window. A library prefab belongs to the author: edit it in Prefab Mode,
    /// make variants of it; nothing regenerates it.
    /// </summary>
    public static class StructureLibrary
    {
        public const string Folder = "Assets/Prefabs/Structures/";

        /// <summary>Asks where in the library to save <paramref name="structure"/>, saves it there and links the scene's copy to it.</summary>
        public static void SaveDialog(FrozenStructure structure)
        {
            Directory.CreateDirectory(Folder.TrimEnd('/'));
            string path = EditorUtility.SaveFilePanelInProject("Save to the structure library", structure.name, "prefab",
                                                               "A folder of the library for its kind (Shelters, Gantries, Signs...), and its name.", Folder);
            if (string.IsNullOrEmpty(path)) return;
            var prefab = Save(structure, path);
            if (prefab != null) Debug.Log($"[Timber] {structure.name} saved to the library: {path}", prefab);
        }

        /// <summary>Saves <paramref name="structure"/> as the prefab at <paramref name="path"/> (its scene copy becomes an instance of it).</summary>
        public static GameObject Save(FrozenStructure structure, string path)
        {
            TimberElementWatcher.Flush();
            TimberCatalog.Flush();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(structure.gameObject, path, InteractionMode.UserAction, out bool saved);
            return saved ? prefab : null;
        }
    }
}
