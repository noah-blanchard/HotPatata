using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Puts the whole kit in KayKit Platformer Pack pieces (ARCHITECTURE §25.1): the palette of tile meshes, the KayKit
    /// materials, the hand-made classic prefabs, then the generated ones (<see cref="CourseBuilder"/>,
    /// <see cref="BombObstacleKitBuilder"/>), and the hand-placed boxes of PrototypeCourse Act 1 and PassSandbox. Colliders,
    /// sizes and layers never change. Idempotent. Rebuild the courses afterwards (menu HotPatata/Course/Rebuild All Courses).
    /// </summary>
    public static class KayKitKitBuilder
    {
        public const string PackDir = "Assets/Art/Models/Map/KayKit_Platformer_Pack_1.0_FREE/";
        public const string ModelDir = PackDir + "Assets/fbx(unity)/";
        const string AtlasPath = PackDir + "Textures/platformer_texture.png";

        static readonly (KitColor color, string folder, string suffix)[] Colors =
        {
            (KitColor.Blue, "blue", "_blue"), (KitColor.Green, "green", "_green"), (KitColor.Red, "red", "_red"),
            (KitColor.Yellow, "yellow", "_yellow"), (KitColor.Neutral, "neutral", "")
        };

        static readonly (KitShape shape, Regex name)[] Families =
        {
            (KitShape.Platform, new Regex(@"^platform_\d+x\d+x\d+$")),
            (KitShape.Barrier, new Regex(@"^barrier_\d+x1x\d+$")),
            (KitShape.Arrow, new Regex(@"^platform_arrow_\d+x\d+x\d+$")),
            (KitShape.Pipe, new Regex(@"^pipe_straight_A$"))
        };

        [MenuItem("HotPatata/Course/Build KayKit Kit")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            BuildPalette();
            BuildMaterials();
            ReskinHandMadePrefabs();
            CourseBuilder.BuildPrefabs();
            BombObstacleKitBuilder.BuildAll();
            MigrateHandPlacedBoxes();
            AssetDatabase.SaveAssets();
            Debug.Log("[KayKitKitBuilder] KayKit kit built");
        }

        [MenuItem("HotPatata/Course/Rebuild All Courses")]
        public static void RebuildCourses()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            CourseBuilder.BuildActs();
            PlaytestCourseBuilder.Build();
            BombObstacleKitBuilder.BuildSandboxDemo();
            PatataParkBuilder.Build();
            Debug.Log("[KayKitKitBuilder] all courses rebuilt");
        }

        // ------------------------------------------------------------------ models

        /// <summary>A model of the pack, e.g. ("spring_pad", Blue) or ("pillar_2x2x4", Neutral).</summary>
        public static GameObject Model(string name, KitColor color)
        {
            var (_, folder, suffix) = Colors.First(c => c.color == color);
            string path = $"{ModelDir}{folder}/{name}{suffix}.fbx";
            return AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException("missing KayKit model " + path);
        }

        public static bool HasColor(string name, KitColor color)
        {
            var (_, folder, suffix) = Colors.First(c => c.color == color);
            return File.Exists($"{ModelDir}{folder}/{name}{suffix}.fbx");
        }

        public static Mesh ModelMesh(string name, KitColor color) => Model(name, color).GetComponentInChildren<MeshFilter>().sharedMesh;

        // ------------------------------------------------------------------ palette

        static void BuildPalette()
        {
            var pieces = new List<KitPalette.Piece>();
            foreach (var (color, folder, suffix) in Colors)
                foreach (var file in Directory.GetFiles(ModelDir + folder, "*.fbx").OrderBy(f => f))
                {
                    string path = file.Replace(Path.DirectorySeparatorChar, '/');
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (suffix.Length > 0)
                    {
                        if (!name.EndsWith(suffix)) continue;
                        name = name.Substring(0, name.Length - suffix.Length);
                    }
                    foreach (var (shape, regex) in Families)
                    {
                        if (!regex.IsMatch(name)) continue;
                        MakeReadable(path);
                        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        var filter = model.GetComponentInChildren<MeshFilter>();
                        var t = filter.transform;
                        if (t.localRotation != Quaternion.identity || t.lossyScale != Vector3.one)
                            Debug.LogWarning($"[KayKitKitBuilder] {name}: mesh transform is not identity, the skin ignores it");
                        pieces.Add(new KitPalette.Piece { shape = shape, color = color, mesh = filter.sharedMesh });
                    }
                }

            Directory.CreateDirectory(Path.GetDirectoryName(PalettePath));
            var palette = AssetDatabase.LoadAssetAtPath<KitPalette>(PalettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<KitPalette>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }
            palette.SetPieces(pieces.ToArray());
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            Debug.Log($"[KayKitKitBuilder] palette: {pieces.Count} pieces");
        }

        /// <summary>KitSkin combines the pack's meshes at runtime, so a build needs them readable.</summary>
        static void MakeReadable(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer.isReadable) return;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ materials

        static void BuildMaterials()
        {
            var atlasImporter = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            if (atlasImporter.mipmapEnabled)
            {
                atlasImporter.mipmapEnabled = false;   // a palette: mips would blend the colour columns at a distance
                atlasImporter.SaveAndReimport();
            }
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            // Plain: the pack's own colours on the toon ramp. Hazard: striped so it never relies on red alone (spec §19).
            // Belt: faint stripes that scroll with the belt (Conveyor sets _PatternScroll), under the chevrons.
            KitMat(KitMaterial, atlas, 0, Color.black, 1f, 0f);
            KitMat(KitHazardMaterial, atlas, 2, new Color(0.28f, 0.08f, 0.14f), 0.5f, 0.45f);
            KitMat(KitBeltMaterial, atlas, 2, new Color(0.08f, 0.12f, 0.3f), 0.6f, 0.3f);
            AssetDatabase.SaveAssets();
        }

        static void KitMat(string name, Texture atlas, int pattern, Color patternColor, float patternScale, float patternStrength)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                AssetDatabase.CopyAsset(MatDir + "Greybox_Platform.mat", path);   // the kit's toon settings (ramp, shade, rim)
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            mat.SetTexture("_BaseMap", atlas);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_TopBlend", 0f);
            mat.SetFloat("_EdgeWidth", 0f);
            mat.SetColor("_ShadeColor", new Color(0.72f, 0.72f, 0.84f));   // lighter, less violet than the greybox: keeps the pack's greys grey
            mat.SetFloat("_Pattern", pattern);
            mat.SetColor("_PatternColor", patternColor);
            mat.SetFloat("_PatternScale", patternScale);
            mat.SetFloat("_PatternStrength", patternStrength);
            EditorUtility.SetDirty(mat);
        }

        // ------------------------------------------------------------------ hand-made prefabs (no builder)

        static void ReskinHandMadePrefabs()
        {
            EditPrefab(PlatformsDir + "Platform_Basic", root => Skin(root.transform.Find("Visual").gameObject, KitRole.Ground));
            EditPrefab(PlatformsDir + "Platform_Narrow", root => Skin(root.transform.Find("Visual").gameObject, KitRole.Ground));
            EditPrefab(PlatformsDir + "Platform_Moving", root => Skin(root.transform.Find("Platform/Visual").gameObject, KitRole.Mover));
            EditPrefab(PlatformsDir + "Platform_Falling", root => Skin(root.transform.Find("Body/Visual").gameObject, KitRole.Falling));
            EditPrefab(ObstaclesDir + "Obstacle_RotatingBar", root => Skin(root.transform.Find("Visual").gameObject, KitRole.Hazard));
            EditPrefab(ObstaclesDir + "LaunchPad", root =>
            {
                // A squat KayKit spring pad filling the old 3 m pad (no collider: players stand on the floor under it).
                var visual = root.transform.Find("Visual");
                var mesh = ModelMesh("spring_pad", KitColor.Blue);
                var filter = visual.GetComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                visual.GetComponent<MeshRenderer>().sharedMaterial = Mat(KitMaterial);
                Vector3 n = mesh.bounds.size;
                visual.localPosition = new Vector3(0f, -mesh.bounds.min.y * 0.35f / n.y, 0f);
                visual.localScale = new Vector3(3f / n.x, 0.35f / n.y, 3f / n.z);
            });
        }

        static void EditPrefab(string pathNoExt, Action<GameObject> edit)
        {
            string path = pathNoExt + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ hand-placed boxes

        static readonly string[] HandPlacedScenes = { "Assets/Scenes/PrototypeCourse.unity", "Assets/Scenes/PassSandbox.unity" };

        /// <summary>
        /// Platform_Basic / Platform_Narrow instances placed by hand carried a greybox material override; each becomes the
        /// matching role (the material is compared by reference) and the override goes. Generated courses are rebuilt instead.
        /// </summary>
        static void MigrateHandPlacedBoxes()
        {
            var roles = new Dictionary<Material, KitRole>
            {
                { Mat("Greybox_Floor"), KitRole.Ground }, { Mat("Greybox_Platform"), KitRole.Ground }, { Mat("Greybox_Narrow"), KitRole.Ground },
                { Mat("Greybox_Wall"), KitRole.Wall }, { Mat("Greybox_Gate"), KitRole.Gate }, { Mat("Greybox_Slide"), KitRole.Slide },
                { Mat("Greybox_Hazard"), KitRole.Hazard }, { Mat("Greybox_Moving"), KitRole.Mover }, { Mat("Greybox_Falling"), KitRole.Falling }
            };
            var boxes = new[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>(PlatformsDir + "Platform_Basic.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(PlatformsDir + "Platform_Narrow.prefab")
            };
            foreach (var path in HandPlacedScenes)
            {
                var scene = EditorSceneManager.OpenScene(path);
                int migrated = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                        if (!boxes.Contains(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject))) continue;
                        var renderer = t.Find("Visual").GetComponent<MeshRenderer>();
                        var mat = renderer.sharedMaterial;
                        if (mat == null || !roles.TryGetValue(mat, out var role)) continue;
                        if (role == KitRole.Ground) PrefabUtility.RevertObjectOverride(renderer, InteractionMode.AutomatedAction);   // the prefab's own look
                        else Skin(renderer.gameObject, role);
                        migrated++;
                    }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[KayKitKitBuilder] {path}: {migrated} hand-placed boxes now KayKit");
            }
        }
    }
}
