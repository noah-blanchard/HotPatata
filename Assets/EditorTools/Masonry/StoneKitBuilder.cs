using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The stone set of kit door looks (docs/OBSTACLES.md §2 and §4.1, ARCHITECTURE §27.7): Prefab Variants of the door
    /// (<c>Actuator_Door</c>) in <see cref="Folder"/>, as <see cref="ForestKitBuilder"/> makes the forest's: an iron portcullis, a stone
    /// slab and a plank door (the last two also sliding). Each hides the kit's box and draws its own look under a
    /// <see cref="CustomVisual"/> on the moving part (and a static stone frame round it); the system, its colliders, its lethal lower edge
    /// and its cues are the base's.
    /// </summary>
    public static class StoneKitBuilder
    {
        public const string Folder = "Assets/Prefabs/Variants/Stone/";
        public const string Portcullis = Folder + "Stone_Portcullis.prefab";
        public const string Slab = Folder + "Stone_Slab.prefab";
        public const string SlabSliding = Folder + "Stone_SlabSliding.prefab";
        public const string PlankDoor = Folder + "Stone_PlankDoor.prefab";
        public const string PlankDoorSliding = Folder + "Stone_PlankDoorSliding.prefab";
        const int Plank = TimberKit.Plank, Aged = TimberKit.PlankAged, Wood = TimberKit.Timber, Iron = TimberKit.Iron;
        const float SlideMargin = 0.3f;

        static string Door => BombObstacleKitBuilder.Door + ".prefab";

        /// <summary>Every stone look: its variant, the kit prefab it is a variant of, how it is drawn, whether it slides.</summary>
        static IEnumerable<(string variant, string basePath, Action<GameObject> dress, bool slides)> Looks() => new (string, string, Action<GameObject>, bool)[]
        {
            (Portcullis, Door, DressPortcullis, false),
            (Slab, Door, DressSlab, false),
            (SlabSliding, Door, DressSlab, true),
            (PlankDoor, Door, DressPlankDoor, false),
            (PlankDoorSliding, Door, DressPlankDoor, true),
        };

        public static IEnumerable<string> Variants() => Looks().Select(l => l.variant);

        [MenuItem("HotPatata/Libraries/Build Stone Looks", priority = 3)]
        public static void BuildAll()
        {
            NatureMaterialBuilder.Ensure(false);
            Directory.CreateDirectory(Folder.TrimEnd('/'));
            foreach (var (variant, basePath, dress, slides) in Looks()) MakeVariant(basePath, variant, dress, slides);
            AssetDatabase.SaveAssets();
            MeshCatalog.CleanSoon();
            Debug.Log($"[StoneKit] {Looks().Count()} stone looks built in {Folder}");
        }

        /// <summary>Builds the looks that are missing (the palette and the walls ask for them).</summary>
        public static void EnsureBuilt()
        {
            if (Looks().All(l => File.Exists(l.variant))) return;
            BuildAll();
        }

        static void MakeVariant(string basePath, string variantPath, Action<GameObject> dress, bool slides)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(basePath), preview);
                instance.name = Path.GetFileNameWithoutExtension(variantPath);
                if (slides)
                {
                    var size = Box(instance).size;
                    BombObstacleKitBuilder.ResizeActuator(instance, size, new Vector3(size.x + SlideMargin, 0f, 0f));
                }
                dress(instance);
                MeshCatalog.Flush();
                PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        // ------------------------------------------------------------------ helpers

        static BoxCollider Box(GameObject root) =>
            root.GetComponent<SignalActuator>().Platform.GetComponentsInChildren<BoxCollider>(true).First(c => !c.isTrigger && c.GetComponent<KillZone>() == null);

        /// <summary>Hides the kit's box (its renderer only: its collider stays).</summary>
        static void HideKit(GameObject root)
        {
            foreach (var skin in root.GetComponent<SignalActuator>().Platform.GetComponentsInChildren<KitSkin>(true))
            {
                var r = skin.GetComponent<Renderer>();
                if (skin.GetComponent<Collider>() != null) { if (r != null) r.enabled = false; }
                else skin.gameObject.SetActive(false);
            }
        }

        /// <summary>A hand-made visual: timber (and iron) and stone meshes under a <see cref="CustomVisual"/>.</summary>
        static void Visual(Transform parent, string name, Vector3 localPosition, Action<MeshBuilder, MeshBuilder> build)
        {
            var root = new GameObject(name, typeof(CustomVisual)).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            var timber = new MeshBuilder(TimberKit.Slots);
            var stone = new MeshBuilder(MasonryKit.StoneSlots);
            build(timber, stone);
            if (timber.Triangles > 0)
            {
                var mesh = MeshCatalog.Store(timber.ToMesh("StoneKit " + name, out var slots));
                var art = new GameObject("Timber");
                art.transform.SetParent(root, false);
                art.AddComponent<MeshFilter>().sharedMesh = mesh;
                art.AddComponent<MeshRenderer>().sharedMaterials = TimberKit.Materials(slots);
            }
            if (stone.Triangles > 0)
            {
                var mesh = MeshCatalog.Store(stone.ToMesh("StoneKit " + name + " stone", out var slots));
                var art = new GameObject("Stone");
                art.transform.SetParent(root, false);
                art.AddComponent<MeshFilter>().sharedMesh = mesh;
                var names = new[] { "Nature_Granite", "Nature_CaveRock", "Nature_Plaster" };
                art.AddComponent<MeshRenderer>().sharedMaterials = slots.Select(s => NatureMaterialBuilder.Load(names[s])).ToArray();
            }
        }

        /// <summary>A static stone frame round the door's closed pose: two jambs of quoins and a lintel stone (on the root, it never moves).</summary>
        static void Frame(GameObject root, Vector3 size)
        {
            Visual(root.transform, "StoneFrame", Vector3.zero, (_, stone) =>
            {
                var rng = new System.Random(17);
                float t = size.z + 0.5f, w = 0.5f;
                foreach (float e in new[] { -1f, 1f })
                    for (float y = -size.y / 2f - 0.2f; y < size.y / 2f; y += 0.45f)
                    {
                        bool wide = Mathf.RoundToInt((y + 10f) / 0.45f) % 2 == 0;
                        float across = wide ? w + 0.18f : w;
                        Block(stone, new Vector3(e * (size.x / 2f + across / 2f + 0.04f), y + 0.22f, 0f), new Vector3(across, 0.43f, t), rng);
                    }
                Block(stone, new Vector3(0f, size.y / 2f + 0.3f, 0f), new Vector3(size.x + 2f * w + 0.4f, 0.55f, t + 0.06f), rng);
            });
        }

        static void Block(MeshBuilder mb, Vector3 center, Vector3 size, System.Random rng) =>
            // Block lays x across, y up, z along: turned so its z is the door's width
            MasonryKit.Block(mb, center, Quaternion.LookRotation(Vector3.right, Vector3.up), new Vector3(size.z, size.y, size.x), 0.04f, 0.015f, rng.Next(), MasonryKit.Stone);

        // ------------------------------------------------------------------ the looks

        /// <summary>An iron portcullis: bars and rails riveted together, spikes at its foot; in a stone frame.</summary>
        static void DressPortcullis(GameObject root)
        {
            var door = root.GetComponent<SignalActuator>();
            var box = Box(root);
            var size = box.size;
            HideKit(root);
            Visual(door.Platform, "PortcullisVisual", box.transform.localPosition, (mb, _) =>
            {
                int bars = Mathf.Max(4, Mathf.RoundToInt(size.x / 0.32f));
                for (int k = 0; k <= bars; k++)
                {
                    float x = -size.x / 2f + 0.06f + (size.x - 0.12f) * k / bars;
                    mb.Beam(new Vector3(x, -size.y / 2f - 0.12f, 0f), new Vector3(x, size.y / 2f, 0f), 0.06f, 0.06f, Iron);
                    // a spike at the foot
                    mb.Tube(new[] { new Vector3(x, -size.y / 2f + 0.02f, 0f), new Vector3(x, -size.y / 2f - 0.22f, 0f) }, new[] { 0.035f, 0.005f }, 4, Iron);
                }
                int rails = Mathf.Max(3, Mathf.RoundToInt(size.y / 0.5f));
                for (int k = 0; k < rails; k++)
                {
                    float y = -size.y / 2f + 0.25f + (size.y - 0.35f) * k / (rails - 1);
                    mb.Beam(new Vector3(-size.x / 2f, y, 0.05f), new Vector3(size.x / 2f, y, 0.05f), 0.05f, 0.08f, Iron);
                    for (int b = 0; b <= bars; b += 2)
                    {
                        float x = -size.x / 2f + 0.06f + (size.x - 0.12f) * b / bars;
                        mb.Box(new Vector3(x, y, 0.09f), Quaternion.identity, new Vector3(0.05f, 0.05f, 0.03f), Iron);
                    }
                }
            });
            Frame(root, size);
        }

        /// <summary>A dressed stone slab: a chamfered block with a carved band across it; in a stone frame.</summary>
        static void DressSlab(GameObject root)
        {
            var door = root.GetComponent<SignalActuator>();
            var box = Box(root);
            var size = box.size;
            HideKit(root);
            Visual(door.Platform, "SlabVisual", box.transform.localPosition, (_, stone) =>
            {
                var rng = new System.Random(5);
                MasonryKit.Block(stone, Vector3.zero, Quaternion.LookRotation(Vector3.right, Vector3.up), new Vector3(size.z, size.y, size.x), 0.08f, 0.012f, rng.Next(), MasonryKit.Stone);
                // the carved band: a raised course across both faces, and a smaller one under it
                foreach (float z in new[] { -1f, 1f })
                {
                    MasonryKit.Block(stone, new Vector3(0f, size.y * 0.18f, z * (size.z / 2f + 0.015f)), Quaternion.LookRotation(Vector3.right, Vector3.up), new Vector3(0.05f, 0.28f, size.x - 0.4f), 0.02f, 0.004f, rng.Next(), MasonryKit.Stone);
                    MasonryKit.Block(stone, new Vector3(0f, -size.y * 0.22f, z * (size.z / 2f + 0.015f)), Quaternion.LookRotation(Vector3.right, Vector3.up), new Vector3(0.04f, 0.14f, size.x - 0.9f), 0.015f, 0.004f, rng.Next(), MasonryKit.Stone);
                }
            });
            if (!Slides(root)) Frame(root, size);
        }

        /// <summary>A plank door: vertical planks, two ledges and a brace behind, iron straps and studs in front.</summary>
        static void DressPlankDoor(GameObject root)
        {
            var door = root.GetComponent<SignalActuator>();
            var box = Box(root);
            var size = box.size;
            HideKit(root);
            Visual(door.Platform, "PlankDoorVisual", box.transform.localPosition, (mb, _) =>
            {
                int planks = Mathf.Max(3, Mathf.RoundToInt(size.x / 0.24f));
                float pw = size.x / planks;
                for (int k = 0; k < planks; k++)
                {
                    float x = -size.x / 2f + pw * (k + 0.5f);
                    mb.Box(new Vector3(x, 0f, 0f), Quaternion.identity, new Vector3(pw - 0.012f, size.y - 0.02f * (k % 3), Mathf.Min(0.08f, size.z * 0.5f)), k % 4 == 1 ? Aged : Plank, new Vector2(k * 0.37f, 0f));
                }
                float back = -Mathf.Min(0.08f, size.z * 0.5f) / 2f - 0.04f, front = -back;
                foreach (float y in new[] { -size.y * 0.32f, size.y * 0.3f })
                    mb.Box(new Vector3(0f, y, back), Quaternion.identity, new Vector3(size.x - 0.1f, 0.18f, 0.06f), Wood);
                float a = Mathf.Atan2(size.y * 0.62f, size.x - 0.3f) * Mathf.Rad2Deg;
                mb.Box(new Vector3(0f, 0f, back), Quaternion.Euler(0f, 0f, a), new Vector3(Mathf.Sqrt((size.x - 0.3f) * (size.x - 0.3f) + size.y * 0.62f * size.y * 0.62f), 0.16f, 0.06f), Wood);
                foreach (float y in new[] { -size.y * 0.32f, size.y * 0.3f })
                {
                    mb.Box(new Vector3(-size.x * 0.12f, y, front), Quaternion.identity, new Vector3(size.x * 0.72f, 0.07f, 0.02f), Iron);
                    for (float x = -size.x * 0.44f; x < size.x * 0.24f; x += 0.22f)
                        mb.Box(new Vector3(x, y, front + 0.015f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.035f, 0.035f, 0.02f), Iron);
                }
            });
        }

        static bool Slides(GameObject root)
        {
            var a = root.GetComponent<SignalActuator>();
            var open = a.WaypointOpen.localPosition - a.WaypointClosed.localPosition;
            return Mathf.Abs(open.x) > Mathf.Abs(open.y);
        }
    }
}
