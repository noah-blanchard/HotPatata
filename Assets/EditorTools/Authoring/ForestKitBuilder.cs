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
    /// The kit's forest looks (ARCHITECTURE §27, docs/OBSTACLES.md §4.1, M18.3b): Prefab Variants of kit elements drawn in the
    /// ranger's timber (planks, logs, iron, rope; the organic kit's materials), ready for an authored level or a nature course. Each
    /// keeps its system, colliders and gameplay cues (icons, a zone's colour, an hourglass's sand, a curtain's field: a gameplay
    /// element always reads the same, PROJECT_SPEC §19); only the kit's construction (KayKit rims, posts, lintels, boxes) is hidden
    /// and drawn again under a <see cref="CustomVisual"/> (renderers only). Menu HotPatata/Kit/Build Forest Looks; never edit the
    /// variants by hand: change this builder and rebuild. Meshes in Assets/Art/Generated/ForestKit/.
    /// </summary>
    public static class ForestKitBuilder
    {
        public const string Folder = "Assets/Prefabs/Variants/Forest/";
        public const string MeshFolder = "Assets/Art/Generated/ForestKit/";

        public const string Winch = Folder + "Forest_Winch.prefab";
        public const string Lever = Folder + "Forest_Lever.prefab";
        public const string Drawbridge = Folder + "Forest_Drawbridge.prefab";
        public const string Cage = Folder + "Forest_Cage.prefab";
        public const string SignalRing = Folder + "Forest_SignalRing.prefab";
        public const string Bell = Folder + "Forest_BellArch.prefab";
        public const string Chute = Folder + "Forest_Chute.prefab";
        public const string Net = Folder + "Forest_Net.prefab";
        public const string Embers = Folder + "Forest_Embers.prefab";
        public const string Sparks = Folder + "Forest_Sparks.prefab";

        const int Plank = TimberKit.Plank, Aged = TimberKit.PlankAged, Wood = TimberKit.Timber, Iron = TimberKit.Iron, Rope = TimberKit.Rope;

        /// <summary>Every forest variant and the kit prefab it is a variant of.</summary>
        public static IEnumerable<(string variant, string basePath)> Variants() => Looks().Select(l => (l.variant, l.basePath));

        /// <summary>Every forest look: its variant, the kit prefab it is a variant of, how it is drawn.</summary>
        static IEnumerable<(string variant, string basePath, Action<GameObject> dress)> Looks() => new (string, string, Action<GameObject>)[]
        {
            (Winch, KitVariantBuilder.VariantsDir + "PressurePlate_Hourglass.prefab", DressWinch),
            (Lever, KitVariantBuilder.VariantsDir + "PressurePlate_HandsFree.prefab", DressLever),
            (Drawbridge, KitVariantBuilder.DrawbridgePath, DressDrawbridge),
            (Cage, CourseKit.PlatformsDir + "Actuator_Lift.prefab", DressCage),
            (SignalRing, CourseKit.GameplayDir + "BombGate_Ring.prefab", DressRing),
            (Bell, CourseKit.GameplayDir + "BombGate_Arch.prefab", DressBell),
            (Chute, CourseKit.ObstaclesDir + "Obstacle_Tube.prefab", DressChute),
            (Net, CourseKit.ObstaclesDir + "Obstacle_BodyScreen.prefab", DressNet),
            (Embers, CourseKit.GameplayDir + "Zone_Hot.prefab", DressEmbers),
            (Sparks, CourseKit.GameplayDir + "LaserCurtain.prefab", DressSparks),
        };

        [MenuItem("HotPatata/Kit/Build Forest Looks")]
        public static void BuildAll()
        {
            NatureMaterialBuilder.Ensure(false);
            Directory.CreateDirectory(Folder.TrimEnd('/'));
            OrganicAssets.Begin(MeshFolder);
            try
            {
                foreach (var (variant, basePath, dress) in Looks()) MakeVariant(basePath, variant, dress);
            }
            finally { OrganicAssets.End(); }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ForestKit] {Looks().Count()} forest looks built in {Folder}");
        }

        static void MakeVariant(string basePath, string variantPath, Action<GameObject> dress)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(basePath), preview);
                instance.name = Path.GetFileNameWithoutExtension(variantPath);
                dress(instance);
                PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Hides a kit renderer: its object when it holds nothing else, else only its renderer (its collider stays).</summary>
        static void Hide(Transform t)
        {
            if (t == null) return;
            var r = t.GetComponent<Renderer>();
            if (t.GetComponent<Collider>() != null || t.GetComponentsInChildren<Collider>(true).Length > 0) { if (r != null) r.enabled = false; }
            else t.gameObject.SetActive(false);
        }

        /// <summary>A hand-made visual under <paramref name="parent"/>: renderers only, under a <see cref="CustomVisual"/>.</summary>
        static Transform Visual(Transform parent, string name, Vector3 localPosition, Action<MeshBuilder> build)
        {
            var root = new GameObject(name, typeof(CustomVisual)).transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            var mb = new MeshBuilder(TimberKit.Slots);
            build(mb);
            if (mb.Triangles > 0)
            {
                var mesh = OrganicAssets.Save(mb.ToMesh("ForestKit " + name, out var slots));
                var art = new GameObject("Timber");
                art.transform.SetParent(root, false);
                art.AddComponent<MeshFilter>().sharedMesh = mesh;
                art.AddComponent<MeshRenderer>().sharedMaterials = TimberKit.Materials(slots);
            }
            return root;
        }

        static List<Vector3> Circle(Vector3 center, float radius, int samples, Vector3 axisA, Vector3 axisB)
        {
            var list = new List<Vector3>();
            for (int i = 0; i <= samples; i++)
            {
                float a = i / (float)samples * Mathf.PI * 2f;
                list.Add(center + (axisA * Mathf.Cos(a) + axisB * Mathf.Sin(a)) * radius);
            }
            return list;
        }

        /// <summary>A rim of short planks round a plate's pad (its radius), lying on the ground.</summary>
        static void PlankRim(MeshBuilder mb, float radius)
        {
            int boards = Mathf.CeilToInt(2f * Mathf.PI * radius / 0.32f);
            for (int k = 0; k < boards; k++)
            {
                float a = k / (float)boards * 360f;
                var rot = Quaternion.Euler(0f, a, 0f);
                mb.Box(rot * new Vector3(0f, 0.03f, radius), rot, new Vector3(0.3f, 0.06f, 0.36f), k % 4 == 1 ? Aged : Plank, new Vector2(k * 0.37f, 0f));
            }
        }

        static void Post(MeshBuilder mb, Vector3 foot, float height, float size = 0.18f) =>
            mb.Beam(foot + Vector3.down * 0.1f, foot + Vector3.up * height, size, size, Wood);

        static void Log(MeshBuilder mb, Vector3 a, Vector3 b, float radius, float shift = 0f) =>
            mb.Tube(new[] { a, b }, new[] { radius, radius * 0.92f }, 9, Wood, true, shift);

        // ------------------------------------------------------------------ the looks

        /// <summary>A winch (the hourglass plate, hands-free: crank it with empty hands): a plank rim, a drum on its frame behind the pad.</summary>
        static void DressWinch(GameObject root)
        {
            Hide(root.transform.Find("Rim"));
            Visual(root.transform, "WinchVisual", Vector3.zero, mb =>
            {
                PlankRim(mb, 1.36f);
                var z = 2.15f;
                mb.Beam(new Vector3(-1.1f, 0.08f, z), new Vector3(1.1f, 0.08f, z), 0.24f, 0.16f, Wood);   // the sill the frame stands on
                foreach (float x in new[] { -0.9f, 0.9f })
                {
                    Post(mb, new Vector3(x, 0f, z), 1.3f, 0.2f);
                    mb.Beam(new Vector3(x, 0.1f, z - 0.55f), new Vector3(x, 0.95f, z), 0.1f, 0.12f, Wood);   // a strut
                }
                var drum = new Vector3(0f, 0.95f, z);
                mb.Tube(new[] { drum + Vector3.left * 0.82f, drum + Vector3.right * 0.82f }, new[] { 0.22f, 0.22f }, 14, Wood, true);
                for (int k = 0; k < 6; k++)
                {
                    float x = -0.55f + k * 0.2f;
                    var ring = Circle(drum + Vector3.right * x, 0.25f, 12, Vector3.up, Vector3.forward);
                    mb.Tube(ring, ring.Select(_ => 0.028f).ToList(), 5, Rope);
                }
                var hub = drum + Vector3.right * 1.05f;
                mb.Tube(new[] { drum, hub }, new[] { 0.05f, 0.05f }, 8, Iron, true);
                mb.Beam(hub, hub + new Vector3(0f, 0.42f, -0.12f), 0.06f, 0.06f, Iron);
                mb.Tube(new[] { hub + new Vector3(0f, 0.42f, -0.12f), hub + new Vector3(0.28f, 0.42f, -0.12f) }, new[] { 0.04f, 0.04f }, 8, Wood, true);
                mb.Tube(new[] { drum + new Vector3(0f, 0.2f, 0.1f), drum + new Vector3(0f, 2.4f, 1.2f) }, new[] { 0.03f, 0.03f }, 6, Rope);   // the rope away to what it works
            });
        }

        /// <summary>A lever (the hands-free plate: hold it with empty hands): a plank rim, a lever on its block at the pad's edge.</summary>
        static void DressLever(GameObject root)
        {
            Hide(root.transform.Find("Rim"));
            Visual(root.transform, "LeverVisual", Vector3.zero, mb =>
            {
                PlankRim(mb, 1.36f);
                var block = new Vector3(0f, 0.25f, 1.75f);
                mb.Box(block, Quaternion.identity, new Vector3(0.7f, 0.5f, 0.5f), Wood);
                foreach (float x in new[] { -0.22f, 0.22f }) mb.Box(block + new Vector3(x, 0.32f, 0f), Quaternion.identity, new Vector3(0.06f, 0.24f, 0.3f), Iron);
                var pivot = block + Vector3.up * 0.38f;
                var tip = pivot + new Vector3(0f, 1.05f, -0.75f);
                mb.Beam(pivot, tip, 0.1f, 0.1f, Wood);
                mb.Tube(new[] { tip, tip + new Vector3(0f, 0.08f, -0.06f) }, new[] { 0.07f, 0.06f }, 8, Iron, true);
                mb.Tube(new[] { pivot + Vector3.left * 0.26f, pivot + Vector3.right * 0.26f }, new[] { 0.04f, 0.04f }, 8, Iron, true);
            });
        }

        /// <summary>A drawbridge of planks on two stringers with iron straps (drawn on the moving deck, so it rises and falls with it).</summary>
        static void DressDrawbridge(GameObject root)
        {
            var bridge = root.GetComponent<SignalActuator>();
            var visual = bridge.Platform.GetComponentsInChildren<KitSkin>(true).FirstOrDefault();
            var box = bridge.Platform.GetComponentsInChildren<BoxCollider>(true).First(c => !c.isTrigger);
            if (visual != null) Hide(visual.transform);
            var size = box.size;
            Visual(bridge.Platform, "DrawbridgeVisual", box.transform.localPosition, mb => PlankDeck(mb, size, true));
        }

        /// <summary>Planks across a deck of <paramref name="size"/> (centred, its top at +y/2), two stringers under them, iron straps.</summary>
        static void PlankDeck(MeshBuilder mb, Vector3 size, bool straps)
        {
            float top = size.y / 2f;
            int boards = Mathf.CeilToInt(size.z / 0.26f);
            for (int k = 0; k < boards; k++)
            {
                float z = -size.z / 2f + (k + 0.5f) * size.z / boards;
                mb.Box(new Vector3(0f, top - 0.03f, z), Quaternion.identity, new Vector3(size.x - 0.02f, 0.06f, size.z / boards - 0.02f), k % 4 == 1 ? Aged : Plank,
                       new Vector2(k * 0.31f, k * 0.17f));
            }
            foreach (float x in new[] { -size.x / 2f + 0.18f, size.x / 2f - 0.18f })
                mb.Box(new Vector3(x, top - 0.06f - (size.y - 0.06f) / 2f, 0f), Quaternion.identity, new Vector3(0.22f, size.y - 0.06f, size.z - 0.04f), Wood);
            if (!straps) return;
            foreach (float z in new[] { -size.z * 0.3f, 0f, size.z * 0.3f })
                mb.Box(new Vector3(0f, top + 0.005f, z), Quaternion.identity, new Vector3(size.x - 0.1f, 0.02f, 0.08f), Iron);
        }

        /// <summary>A cage lift: a plank floor, four corner posts and a frame on top, a rope up to its pulley (open sides: nothing to lean on).</summary>
        static void DressCage(GameObject root)
        {
            var lift = root.GetComponent<SignalActuator>();
            var visual = lift.Platform.GetComponentsInChildren<KitSkin>(true).FirstOrDefault();
            var box = lift.Platform.GetComponentsInChildren<BoxCollider>(true).First(c => !c.isTrigger);
            if (visual != null) Hide(visual.transform);
            var size = box.size;
            Visual(lift.Platform, "CageVisual", box.transform.localPosition, mb =>
            {
                PlankDeck(mb, size, false);
                float hx = size.x / 2f - 0.1f, hz = size.z / 2f - 0.1f, top = size.y / 2f, h = 2.5f;
                var corners = new[] { new Vector3(-hx, top, -hz), new Vector3(hx, top, -hz), new Vector3(hx, top, hz), new Vector3(-hx, top, hz) };
                foreach (var c in corners) mb.Beam(c, c + Vector3.up * h, 0.14f, 0.14f, Wood);
                for (int i = 0; i < 4; i++) mb.Beam(corners[i] + Vector3.up * h, corners[(i + 1) % 4] + Vector3.up * h, 0.14f, 0.16f, Wood);
                mb.Beam(corners[0] + Vector3.up * h, corners[2] + Vector3.up * h, 0.1f, 0.12f, Wood);
                mb.Beam(corners[1] + Vector3.up * h, corners[3] + Vector3.up * h, 0.1f, 0.12f, Wood);
                var hook = new Vector3(0f, top + h, 0f);
                mb.Box(hook + Vector3.up * 0.08f, Quaternion.identity, new Vector3(0.22f, 0.16f, 0.22f), Iron);
                mb.Tube(new[] { hook + Vector3.up * 0.16f, hook + Vector3.up * 6f }, new[] { 0.035f, 0.035f }, 6, Rope);
            });
        }

        /// <summary>A signal ring: a bent branch bound with rope, hung on two ropes (the bomb is thrown through it).</summary>
        static void DressRing(GameObject root)
        {
            float radius = 1.3f;
            foreach (Transform segment in root.transform)
                foreach (var skin in segment.GetComponentsInChildren<KitSkin>(true)) Hide(skin.transform);
            Visual(root.transform, "RingVisual", Vector3.zero, mb =>
            {
                var ring = Circle(Vector3.zero, radius, 36, Vector3.right, Vector3.up);
                mb.Tube(ring, ring.Select((_, i) => 0.13f + 0.025f * Mathf.Sin(i * 1.7f)).ToList(), 9, Wood);
                for (int k = 0; k < 12; k++)
                {
                    float a = k / 12f * Mathf.PI * 2f;
                    var at = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                    var binding = Circle(at, 0.17f, 10, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f), Vector3.forward);
                    mb.Tube(binding, binding.Select(_ => 0.03f).ToList(), 5, Rope);
                }
                foreach (float x in new[] { -0.55f, 0.55f })
                {
                    var on = new Vector3(x, Mathf.Sqrt(radius * radius - x * x), 0f);
                    mb.Tube(new[] { on, on + new Vector3(x * 0.3f, 3.2f, 0f) }, new[] { 0.03f, 0.03f }, 6, Rope);
                }
            });
        }

        /// <summary>A bell arch: two log posts, a log lintel with knee braces, the bell hung over the middle (the bomb flies through below).</summary>
        static void DressBell(GameObject root)
        {
            foreach (var name in new[] { "Post_L", "Post_R", "Lintel" }) Hide(root.transform.Find(name));
            Visual(root.transform, "BellVisual", Vector3.zero, mb =>
            {
                foreach (float x in new[] { -1.8f, 1.8f })
                {
                    Log(mb, new Vector3(x, -0.3f, 0f), new Vector3(x, 3.85f, 0f), 0.21f, x);
                    mb.Beam(new Vector3(x, 2.9f, 0f), new Vector3(x * 0.55f, 3.5f, 0f), 0.12f, 0.14f, Wood);
                }
                Log(mb, new Vector3(-2.2f, 3.62f, 0f), new Vector3(2.2f, 3.62f, 0f), 0.2f, 1.3f);
                // a yoke on the lintel and the bell under it, above the opening
                mb.Beam(new Vector3(-0.5f, 3.85f, 0f), new Vector3(-0.35f, 4.6f, 0f), 0.12f, 0.12f, Wood);
                mb.Beam(new Vector3(0.5f, 3.85f, 0f), new Vector3(0.35f, 4.6f, 0f), 0.12f, 0.12f, Wood);
                mb.Beam(new Vector3(-0.5f, 4.6f, 0f), new Vector3(0.5f, 4.6f, 0f), 0.16f, 0.16f, Wood);
                var profile = new[] { new Vector2(0.02f, 0f), new Vector2(0.38f, 0f), new Vector2(0.34f, 0.08f), new Vector2(0.27f, 0.3f), new Vector2(0.22f, 0.5f), new Vector2(0.12f, 0.6f), new Vector2(0.02f, 0.62f) };
                mb.Lathe(new Vector3(0f, 3.88f, 0f), Quaternion.identity, profile, 18, Iron);
            });
        }

        /// <summary>
        /// A log chute: the tube's pipes keep their shape (each level lays them its own way) but are drawn as planks and timber, their
        /// ends as timber; the coloured pips and lamps that tell the chutes apart stay.
        /// </summary>
        static void DressChute(GameObject root)
        {
            var planks = NatureMaterialBuilder.Load(TimberKit.PlankAgedMaterial);
            var timber = NatureMaterialBuilder.Load(TimberKit.TimberMaterial);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null || !m.name.StartsWith("Tube_Pipe")) continue;   // the pips, lamps and slots are the cues
                r.sharedMaterials = r.sharedMaterials.Select(_ => r.GetComponent<KitSkin>() != null ? planks : timber).ToArray();
            }
        }

        /// <summary>A rope net between two log posts (the body screen: players stop, the bomb passes): the field that marks it stays.</summary>
        static void DressNet(GameObject root)
        {
            var frame = root.transform.Find("Frame");
            var screen = root.transform.Find("Screen");
            foreach (Transform t in frame) Hide(t);
            foreach (Transform t in screen) if (t.name.StartsWith("Strand_")) Hide(t);
            var collision = screen.GetComponentsInChildren<BoxCollider>(true).First();
            float w = collision.size.x, h = collision.size.y;
            Visual(frame, "NetFrameVisual", Vector3.zero, mb =>
            {
                foreach (float x in new[] { -(w / 2f + 0.18f), w / 2f + 0.18f }) Log(mb, new Vector3(x, -0.3f, 0f), new Vector3(x, h + 0.35f, 0f), 0.17f, x);
                Log(mb, new Vector3(-(w / 2f + 0.4f), h + 0.18f, 0f), new Vector3(w / 2f + 0.4f, h + 0.18f, 0f), 0.15f, 0.7f);
            });
            Visual(screen, "NetVisual", Vector3.zero, mb =>
            {
                for (float x = -w / 2f + 0.2f; x < w / 2f - 0.1f; x += 0.4f)
                    mb.Tube(new[] { new Vector3(x, 0.05f, 0f), new Vector3(x + 0.03f, h / 2f, 0.04f), new Vector3(x, h, 0f) }, new[] { 0.022f, 0.022f, 0.022f }, 5, Rope);
                for (float y = 0.25f; y < h - 0.05f; y += 0.42f)
                    mb.Tube(new[] { new Vector3(-w / 2f, y, 0f), new Vector3(0f, y - 0.04f, 0.04f), new Vector3(w / 2f, y, 0f) }, new[] { 0.022f, 0.022f, 0.022f }, 5, Rope);
            });
        }

        /// <summary>Embers (the hot zone: the fuse burns faster): charred logs and glowing coals on its floor, sparks rising; its colour stays.</summary>
        static void DressEmbers(GameObject root)
        {
            var box = root.GetComponent<BoxCollider>();
            var size = box.size;
            var visual = Visual(root.transform, "EmbersVisual", Vector3.zero, mb =>
            {
                var rng = new System.Random(7);
                float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
                for (int k = 0; k < 7; k++)
                {
                    var a = new Vector3(R(-size.x * 0.4f, size.x * 0.4f), 0.1f, R(-size.z * 0.4f, size.z * 0.4f));
                    var dir = Quaternion.Euler(0f, R(0f, 180f), 0f) * Vector3.forward;
                    Log(mb, a - dir * R(0.4f, 0.8f), a + dir * R(0.4f, 0.8f), R(0.08f, 0.13f), k);
                }
                for (int k = 0; k < 40; k++)
                    mb.Box(new Vector3(R(-size.x * 0.45f, size.x * 0.45f), 0.05f, R(-size.z * 0.45f, size.z * 0.45f)), Quaternion.Euler(R(0, 30), R(0, 360), R(0, 30)),
                           new Vector3(R(0.08f, 0.2f), R(0.05f, 0.1f), R(0.08f, 0.2f)), Iron);
            });
            var glow = NatureKit.ParticleMaterial("Nature_EmberZone", new Color(1f, 0.55f, 0.15f, 1f), true);   // its own: the shared spark material is the campfires'
            var sparks = NatureKit.Particles(visual, "Rising embers", glow, 1f, lifetime: new Vector2(1.2f, 2.6f), speed: new Vector2(0.3f, 1.1f), size: new Vector2(0.03f, 0.07f),
                                             rate: 6f * size.x * size.z / 6f, radius: 0.5f, gravity: -0.04f, start: new Color(1f, 0.65f, 0.25f, 1f), end: new Color(1f, 0.25f, 0.05f, 0f), noise: 0.5f);
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x * 0.9f, size.z * 0.9f, 0.1f);   // the system faces up: its x, y are the floor's x, z
            sparks.transform.localPosition = Vector3.up * 0.1f;
        }

        /// <summary>A curtain of sparks (the bomb cannot cross it): a timber frame with an iron rail, sparks streaming down; its field stays.</summary>
        static void DressSparks(GameObject root)
        {
            foreach (var name in new[] { "Post_L", "Post_R", "Lintel" }) Hide(root.transform.Find(name));
            foreach (Transform t in root.transform) if (t.name.StartsWith("Beam_")) Hide(t);
            var box = root.GetComponent<BoxCollider>();
            float w = box.size.x, h = box.size.y;
            var visual = Visual(root.transform, "SparksVisual", Vector3.zero, mb =>
            {
                foreach (float x in new[] { -(w / 2f + 0.15f), w / 2f + 0.15f })
                {
                    mb.Beam(new Vector3(x, -0.2f, 0f), new Vector3(x, h + 0.3f, 0f), 0.28f, 0.28f, Wood);
                    foreach (float y in new[] { 0.6f, h - 0.3f }) mb.Box(new Vector3(x, y, 0f), Quaternion.identity, new Vector3(0.32f, 0.08f, 0.32f), Iron);
                }
                mb.Beam(new Vector3(-(w / 2f + 0.4f), h + 0.15f, 0f), new Vector3(w / 2f + 0.4f, h + 0.15f, 0f), 0.3f, 0.3f, Wood);
                mb.Box(new Vector3(0f, h - 0.02f, 0f), Quaternion.identity, new Vector3(w, 0.08f, 0.1f), Iron);   // the rail the sparks fall from
            });
            var glow = NatureKit.ParticleMaterial("Nature_SparkCurtain", new Color(1f, 0.75f, 0.35f, 1f), true);
            var sparks = NatureKit.Particles(visual, "Falling sparks", glow, 1f, lifetime: new Vector2(0.5f, 0.9f), speed: new Vector2(0.2f, 0.8f), size: new Vector2(0.03f, 0.06f),
                                             rate: 60f * w / 4f, radius: 0.1f, gravity: 1.2f, start: new Color(1f, 0.85f, 0.45f, 1f), end: new Color(1f, 0.35f, 0.05f, 0f));
            sparks.transform.localPosition = new Vector3(0f, h - 0.05f, 0f);
            sparks.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // emit downwards
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(w * 0.95f, 0.06f, 0.05f);
            var main = sparks.main;
            main.maxParticles = 400;
        }
    }
}
