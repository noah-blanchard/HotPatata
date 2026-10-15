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
    /// The prop library (ARCHITECTURE §27, M18.3b): every CC0 model of Assets/Art/Models/Nature as a prefab at its real size, standing on
    /// its base, drawn with the nature library's material for it (<see cref="NatureMaterialBuilder.Library"/>: fogged, lit like the
    /// courses), collider-free with a <see cref="PropSolid"/> to make it solid; sorted by kind in Assets/Prefabs/Props. Plus ready-made
    /// sets (a workbench, a woodpile, a rope corner). Menu HotPatata/Level/Build Prop Library; drag a prop from the Project window.
    /// </summary>
    public static class PropLibraryBuilder
    {
        public const string Folder = "Assets/Prefabs/Props/";
        public const string SetFolder = Folder + "Sets/";
        public const string MeshFolder = "Assets/Art/Generated/PropSets/";

        /// <summary>Each model's kind (its folder) and, for the scans brought to a unit height, its real height (m).</summary>
        static readonly Dictionary<string, (string kind, float height)> Models = new Dictionary<string, (string, float)>
        {
            { "boulder_01", ("Rocks", 0f) }, { "namaqualand_boulder_02", ("Rocks", 0f) }, { "namaqualand_boulder_04", ("Rocks", 0f) },
            { "rock_07", ("Rocks", 0f) }, { "rock_09", ("Rocks", 0f) }, { "stone_01", ("Rocks", 0f) }, { "rock_face_01", ("Rocks", 0f) },
            { "rock_face_02", ("Rocks", 0f) }, { "rock_moss_set_01", ("Rocks", 0f) }, { "rock_moss_set_02", ("Rocks", 0f) }, { "mountainside", ("Rocks", 0f) },
            { "dead_tree_trunk", ("Wood", 0f) }, { "dead_tree_trunk_02", ("Wood", 0f) }, { "dry_branches_medium_01", ("Wood", 0f) },
            { "root_cluster_01", ("Wood", 0f) }, { "root_cluster_02", ("Wood", 0f) }, { "single_root", ("Wood", 0f) }, { "tree_stump_01", ("Wood", 0f) },
            { "tree_stump_02", ("Wood", 0f) }, { "modular_wooden_pier", ("Wood", 0f) },
            { "anthurium_botany_01", ("Plants", 0f) }, { "calathea_orbifolia_01", ("Plants", 0f) }, { "fern_02", ("Plants", 0f) }, { "moss_01", ("Plants", 0f) },
            { "nettle_plant", ("Plants", 0f) }, { "pachira_aquatica_01", ("Plants", 0f) }, { "shrub_02", ("Plants", 0f) }, { "shrub_03", ("Plants", 0f) },
            { "shrub_04", ("Plants", 0f) }, { "weed_plant_02", ("Plants", 0f) },
            { "wicker_basket_01", ("Containers", 0f) }, { "wicker_basket_02", ("Containers", 0f) }, { "wine_barrel_01", ("Containers", 0f) },
            { "wooden_barrels_01", ("Containers", 0f) }, { "wooden_bucket_01", ("Containers", 0f) }, { "wooden_crate_01", ("Containers", 0f) },
            { "wooden_crate_02", ("Containers", 0f) },
            { "wooden_ladder_02", ("Household", 0f) }, { "wooden_stool_01", ("Household", 0f) }, { "hatchet", ("Household", 0f) }, { "wooden_bowl_01", ("Household", 0f) },
            { "carved_wooden_plate", ("Household", 0f) }, { "jug_01", ("Household", 0f) }, { "brass_pot_01", ("Household", 0f) }, { "planter_pot_clay", ("Household", 0f) },
            { "ceramic_vase_01", ("Household", 0f) }, { "ceramic_vase_03", ("Household", 0f) }, { "antique_ceramic_vase_01", ("Household", 0f) },
            { "stone_fire_pit", ("Household", 0f) },
            { "Lantern_01", ("Lanterns", 0f) }, { "brass_diya_lantern", ("Lanterns", 0f) }, { "wooden_lantern_01", ("Lanterns", 0f) },
            { "aztec_chalchiuhtlicue", ("Ruins", 1.6f) }, { "ballgame_palma", ("Ruins", 1.2f) }, { "face_urn", ("Ruins", 0.62f) }, { "maya_head_helmet", ("Ruins", 1f) },
            { "maya_relief_pillar", ("Ruins", 2.4f) }, { "maya_stela_fragment", ("Ruins", 1.6f) }, { "olmec_face_fragment", ("Ruins", 1.2f) },
            { "olmec_standing_figure", ("Ruins", 1.6f) }, { "palenque_relief_wall", ("Ruins", 1.6f) }, { "precolumbian_bowl", ("Ruins", 0.28f) },
            { "duck_nest_eggs", ("Wildlife", 0.32f) }, { "tree_frog", ("Wildlife", 0.12f) },
        };

        public static string PrefabPath(string model) => Models.TryGetValue(model, out var m) ? $"{Folder}{m.kind}/{model}.prefab" : null;

        [MenuItem("HotPatata/Level/Build Prop Library", priority = 60)]
        public static void BuildAll()
        {
            NatureMaterialBuilder.Ensure(false);
            int made = 0;
            var missing = new List<string>();
            foreach (var (model, (kind, height)) in Models)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(NatureAssetImporter.ModelPath(model)) == null) { missing.Add(model); continue; }
                Directory.CreateDirectory(Folder + kind);
                BuildProp(model, PrefabPath(model), height);
                made++;
            }
            BuildSets();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Props] {made} props and 3 sets in {Folder}" + (missing.Count > 0 ? $"; missing models (fetch them first): {string.Join(", ", missing)}" : ""));
        }

        /// <summary>One prop: the model at its real size (or <paramref name="height"/>), on its base, nature materials, no collider.</summary>
        static void BuildProp(string model, string path, float height)
        {
            var root = new GameObject(model);
            try
            {
                root.AddComponent<PropSolid>();
                var art = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NatureAssetImporter.ModelPath(model)));
                art.name = "Model";
                art.transform.SetParent(root.transform, false);
                foreach (var c in art.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var r in art.GetComponentsInChildren<Renderer>(true))
                {
                    var material = MaterialFor(model, r);
                    if (material != null) r.sharedMaterials = r.sharedMaterials.Select(_ => material).ToArray();
                }
                var bounds = Bounds(art);
                if (height > 0f && bounds.size.y > 1e-4f)
                {
                    art.transform.localScale *= height / bounds.size.y;
                    bounds = Bounds(art);
                }
                art.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>
        /// The nature library's material for one part of a model: the entry for that model whose texture set the part's name holds
        /// (wooden_barrels_01's barrels and pieces, pachira's bark and leaves), else its only (or first) entry.
        /// </summary>
        public static Material MaterialFor(string model, Renderer part)
        {
            var entries = NatureMaterialBuilder.Library.Where(s => s.model && s.folder == model + "/textures").ToList();
            if (entries.Count == 0) return null;
            string name = (part.name + " " + (part.GetComponent<MeshFilter>()?.sharedMesh?.name ?? "")).ToLowerInvariant();
            foreach (var e in entries)
            {
                string set = e.mapPrefix.TrimEnd('_');
                if (set.StartsWith(model.ToLowerInvariant())) set = set.Substring(model.Length).Trim('_');
                if (set.Length > 0 && (name.Contains(set) || name.Contains(set.TrimEnd('s')))) return NatureMaterialBuilder.Load(e.name);
            }
            return NatureMaterialBuilder.Load(entries[0].name);
        }

        // ------------------------------------------------------------------ sets

        const int Plank = TimberKit.Plank, Aged = TimberKit.PlankAged, Wood = TimberKit.Timber, Iron = TimberKit.Iron, Rope = TimberKit.Rope;

        static void BuildSets()
        {
            Directory.CreateDirectory(SetFolder.TrimEnd('/'));
            OrganicAssets.Begin(MeshFolder);
            try
            {
                Set("Workbench", true, root =>
                {
                    Timber(root, "Bench", mb =>
                    {
                        for (int k = 0; k < 4; k++) mb.Box(new Vector3(0f, 0.86f, -0.33f + k * 0.22f), Quaternion.identity, new Vector3(1.9f, 0.06f, 0.21f), k % 3 == 1 ? Aged : Plank);
                        foreach (var x in new[] { -0.82f, 0.82f })
                            foreach (var z in new[] { -0.34f, 0.34f }) mb.Beam(new Vector3(x, 0f, z), new Vector3(x, 0.83f, z), 0.1f, 0.1f, Wood);
                        for (int k = 0; k < 3; k++) mb.Box(new Vector3(0f, 0.25f, -0.25f + k * 0.25f), Quaternion.identity, new Vector3(1.7f, 0.04f, 0.22f), Aged);
                        mb.Beam(new Vector3(-0.82f, 0.4f, -0.34f), new Vector3(0.82f, 0.4f, -0.34f), 0.06f, 0.08f, Wood);
                    });
                    Prop(root, "hatchet", new Vector3(-0.5f, 0.89f, 0.05f), Quaternion.Euler(0f, 0f, 90f));
                    Prop(root, "wooden_bowl_01", new Vector3(0.45f, 0.89f, -0.1f), Quaternion.identity);
                    Prop(root, "jug_01", new Vector3(0.1f, 0.89f, 0.2f), Quaternion.Euler(0f, 40f, 0f));
                    Prop(root, "wicker_basket_01", new Vector3(0.3f, 0.27f, 0f), Quaternion.Euler(0f, 10f, 0f));
                    Prop(root, "wooden_stool_01", new Vector3(0.4f, 0f, 0.85f), Quaternion.Euler(0f, 25f, 0f));
                });
                Set("Woodpile", true, root =>
                {
                    Timber(root, "Logs", mb =>
                    {
                        for (int row = 0; row < 4; row++)
                            for (int k = 0; k < 7 - row; k++)
                            {
                                var c = new Vector3(-1.1f + k * 0.36f + row * 0.18f, 0.17f + row * 0.31f, 0f);
                                mb.Tube(new[] { c + Vector3.back * 0.7f, c + Vector3.forward * 0.7f }, new[] { 0.16f, 0.16f }, 8, Wood, true, k * 0.3f + row);
                            }
                        foreach (var x in new[] { -1.45f, 1.45f }) mb.Beam(new Vector3(x, -0.1f, 0f), new Vector3(x, 1.2f, 0f), 0.12f, 0.12f, Wood);
                    });
                    Prop(root, "tree_stump_02", new Vector3(2.4f, 0f, 0.6f), Quaternion.identity, 0.55f);
                    Prop(root, "hatchet", new Vector3(2.4f, 0.32f, 0.6f), Quaternion.Euler(-20f, 30f, 0f));
                });
                Set("Rope corner", false, root =>
                {
                    Timber(root, "Coils", mb =>
                    {
                        foreach (var (at, turns) in new[] { (new Vector3(0f, 0f, 0f), 6), (new Vector3(0.75f, 0f, 0.3f), 4) })
                            for (int k = 0; k < turns; k++)
                            {
                                var ring = new List<Vector3>();
                                for (int i = 0; i <= 16; i++)
                                {
                                    float a = i / 16f * Mathf.PI * 2f;
                                    ring.Add(at + new Vector3(Mathf.Cos(a) * (0.32f - k * 0.01f), 0.04f + k * 0.06f, Mathf.Sin(a) * (0.32f - k * 0.01f)));
                                }
                                mb.Tube(ring, ring.Select(_ => 0.035f).ToList(), 5, Rope);
                            }
                    });
                    Prop(root, "wooden_crate_02", new Vector3(-0.2f, 0f, 0.9f), Quaternion.Euler(0f, 8f, 0f));
                    Prop(root, "wooden_crate_01", new Vector3(-0.1f, 0.46f, 0.9f), Quaternion.Euler(0f, -12f, 0f));
                    Prop(root, "wooden_bucket_01", new Vector3(1.2f, 0f, 1f), Quaternion.identity);
                    Prop(root, "wicker_basket_02", new Vector3(-1f, 0f, 0.1f), Quaternion.Euler(0f, 30f, 0f));
                });
            }
            finally { OrganicAssets.End(); }
        }

        /// <summary>One set; <paramref name="solid"/>: it is in the way (a box round it).</summary>
        static void Set(string name, bool solid, Action<Transform> build)
        {
            var root = new GameObject(name);
            try
            {
                var prop = root.AddComponent<PropSolid>();
                build(root.transform);
                if (solid) PropSolidEditor.SetSolid(prop, true, false);
                PrefabUtility.SaveAsPrefabAsset(root, SetFolder + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void Timber(Transform parent, string name, Action<MeshBuilder> build)
        {
            var mb = new MeshBuilder(TimberKit.Slots);
            build(mb);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = OrganicAssets.Save(mb.ToMesh("PropSet " + parent.name + " " + name, out var slots));
            go.AddComponent<MeshRenderer>().sharedMaterials = TimberKit.Materials(slots);
        }

        /// <summary>A library prop nested in a set (a prefab instance: the set follows the prop's own prefab).</summary>
        static void Prop(Transform parent, string model, Vector3 at, Quaternion rotation, float scale = 1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(model));
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = at;
            go.transform.localRotation = rotation;
            go.transform.localScale = Vector3.one * scale;
        }
    }
}
