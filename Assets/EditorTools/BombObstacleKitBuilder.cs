using System;
using System.Collections.Generic;
using System.IO;
using HotPatata;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Builds the #68 bomb-obstacle kit (PROJECT_SPEC §7.3, §13.13-§13.17): icons, materials and prefabs for fuse zones,
    /// laser curtains, bomb gates, pressure plates, actuators, the checkpoint arch, tubes and cannons. Idempotent: rerun
    /// it after changing a builder. Also adds <see cref="BombZoneSweep"/> to the Bomb prefab. The Resize helpers keep
    /// prefab instances resizable with plain property overrides (no added or removed children).
    /// </summary>
    public static class BombObstacleKitBuilder
    {
        const string IconDir = "Assets/Art/Textures/Icons/";
        const string BombPrefab = "Assets/Prefabs/Bomb/Bomb.prefab";

        public const string ZoneForbidden = GameplayDir + "Zone_Forbidden";
        public const string ZoneHot = GameplayDir + "Zone_Hot";
        public const string ZoneCold = GameplayDir + "Zone_Cold";
        public const string LaserCurtain = GameplayDir + "LaserCurtain";
        public const string GateRing = GameplayDir + "BombGate_Ring";
        public const string GateArch = GameplayDir + "BombGate_Arch";
        public const string Plate = GameplayDir + "PressurePlate";
        public const string Door = ObstaclesDir + "Actuator_Door";
        public const string Bridge = PlatformsDir + "Actuator_Bridge";
        public const string Lift = PlatformsDir + "Actuator_Lift";

        public static readonly Vector3 DoorSize = new Vector3(4f, 3.6f, 0.5f);

        [MenuItem("HotPatata/Course/Build Bomb Obstacle Prefabs")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            BuildIcons();
            BuildMaterials();
            MakePrefab(ZoneForbidden + ".prefab", true, () => BuildZone("Zone_Forbidden", FuseZoneKind.Forbidden));
            MakePrefab(ZoneHot + ".prefab", true, () => BuildZone("Zone_Hot", FuseZoneKind.Hot));
            MakePrefab(ZoneCold + ".prefab", true, () => BuildZone("Zone_Cold", FuseZoneKind.Cold));
            MakePrefab(LaserCurtain + ".prefab", true, BuildCurtain);
            MakePrefab(GateRing + ".prefab", true, BuildGateRing);
            MakePrefab(GateArch + ".prefab", true, BuildGateArch);
            MakePrefab(Plate + ".prefab", true, BuildPlate);
            MakePrefab(Door + ".prefab", true, () => BuildActuator("Actuator_Door", DoorSize, new Vector3(0f, DoorSize.y + 0.4f, 0f),
                                                                   0.8f, Mat("Greybox_Hazard"), "Hazard", lethal: true));
            MakePrefab(Bridge + ".prefab", true, () => BuildActuator("Actuator_Bridge", new Vector3(3f, 0.5f, 8f), new Vector3(0f, 0f, 8f),
                                                                     1.2f, Mat("Greybox_Moving"), "Environment", lethal: false));
            MakePrefab(Lift + ".prefab", true, () => BuildActuator("Actuator_Lift", new Vector3(3f, 0.5f, 3f), new Vector3(0f, 4f, 0f),
                                                                   2f, Mat("Greybox_Moving"), "Environment", lethal: false));
            EnsureBombComponents();
            AssetDatabase.SaveAssets();
            Debug.Log("[BombObstacleKitBuilder] bomb obstacle kit built");
        }

        // ------------------------------------------------------------------ bomb prefab

        static void EnsureBombComponents()
        {
            var root = PrefabUtility.LoadPrefabContents(BombPrefab);
            try
            {
                if (root.GetComponent<BombZoneSweep>() == null) root.AddComponent<BombZoneSweep>();
                PrefabUtility.SaveAsPrefabAsset(root, BombPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ fuse zones

        static readonly Vector3 DefaultZoneSize = new Vector3(6f, 2.5f, 3f);

        static string KindName(FuseZoneKind kind) => kind.ToString();

        static GameObject BuildZone(string name, FuseZoneKind kind)
        {
            var root = new GameObject(name) { layer = Layer("Trigger") };
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var zone = root.AddComponent<Zone>();
            SetReference(zone, "volume", box);
            var fuse = root.AddComponent<FuseZone>();
            SetField(fuse, "kind", p => p.enumValueIndex = (int)kind);

            string k = KindName(kind);
            var t = root.transform;
            Shape("Floor", PrimitiveType.Cube, t, Vector3.zero, Quaternion.identity, Vector3.one, Mat($"Zone_{k}_Floor"), "Default");
            Shape("Field", PrimitiveType.Cube, t, Vector3.zero, Quaternion.identity, Vector3.one, Mat($"Zone_{k}_Field"), "Default");
            var icon = Mat($"Icon_{k}");
            Shape("Icon_Floor", PrimitiveType.Quad, t, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one, icon, "Default");
            Shape("Sign_Front", PrimitiveType.Quad, t, Vector3.zero, Quaternion.identity, Vector3.one, icon, "Default");
            Shape("Sign_Back", PrimitiveType.Quad, t, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), Vector3.one, icon, "Default");
            ResizeZone(root, DefaultZoneSize);
            return root;
        }

        /// <summary>
        /// Sizes a fuse zone (pivot on the floor, at the centre): the volume, the patterned floor, the tinted field, the
        /// floor icon and the two signs above it (facing along ±Z, the course direction).
        /// </summary>
        public static void ResizeZone(GameObject zone, Vector3 size)
        {
            var box = zone.GetComponent<BoxCollider>();
            box.size = size;
            box.center = new Vector3(0f, size.y / 2f, 0f);
            var t = zone.transform;
            Set(t.Find("Floor"), new Vector3(0f, 0.03f, 0f), new Vector3(size.x, 0.04f, size.z));
            Set(t.Find("Field"), new Vector3(0f, size.y / 2f, 0f), size);
            float icon = Mathf.Clamp(Mathf.Min(size.x, size.z) * 0.7f, 0.8f, 2.4f);
            Set(t.Find("Icon_Floor"), new Vector3(0f, 0.06f, 0f), Vector3.one * icon);
            Set(t.Find("Sign_Front"), new Vector3(0f, size.y + 0.6f, -size.z / 2f), Vector3.one * 1.1f);
            Set(t.Find("Sign_Back"), new Vector3(0f, size.y + 0.6f, size.z / 2f), Vector3.one * 1.1f);
        }

        // ------------------------------------------------------------------ laser curtain

        const int CurtainBeams = 8;

        static GameObject BuildCurtain()
        {
            var root = new GameObject("LaserCurtain") { layer = Layer("Trigger") };
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var zone = root.AddComponent<Zone>();
            SetReference(zone, "volume", box);
            var fuse = root.AddComponent<FuseZone>();
            SetField(fuse, "kind", p => p.enumValueIndex = (int)FuseZoneKind.Forbidden);
            root.AddComponent<BombBarrier>();

            var t = root.transform;
            var hazard = Mat("Greybox_Hazard");
            Cube("Post_L", t, Vector3.zero, Vector3.one, hazard, "Environment", keepCollider: true);
            Cube("Post_R", t, Vector3.zero, Vector3.one, hazard, "Environment", keepCollider: true);
            Cube("Lintel", t, Vector3.zero, Vector3.one, hazard, "Environment", keepCollider: true);
            var beam = Mat("Laser_Beam");
            for (int i = 0; i < CurtainBeams; i++)
                Shape($"Beam_{i + 1}", PrimitiveType.Cylinder, t, Vector3.zero, Quaternion.Euler(0f, 0f, 90f), Vector3.one, beam, "Default");
            Shape("Field", PrimitiveType.Cube, t, Vector3.zero, Quaternion.identity, Vector3.one, Mat("Zone_Forbidden_Field"), "Default");
            var icon = Mat("Icon_Forbidden");
            Shape("Sign_Front", PrimitiveType.Quad, t, Vector3.zero, Quaternion.identity, Vector3.one, icon, "Default");
            Shape("Sign_Back", PrimitiveType.Quad, t, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), Vector3.one, icon, "Default");
            ResizeCurtain(root, 4f, 3.2f);
            return root;
        }

        /// <summary>
        /// Sizes a laser curtain (pivot on the floor, at the centre; the curtain spans X, faces Z): the forbidden/barrier
        /// volume, the striped posts and lintel just outside it, the beams and the signs.
        /// </summary>
        public static void ResizeCurtain(GameObject curtain, float width, float height)
        {
            const float depth = 0.6f, post = 0.3f;
            var box = curtain.GetComponent<BoxCollider>();
            box.size = new Vector3(width, height, depth);
            box.center = new Vector3(0f, height / 2f, 0f);
            var t = curtain.transform;
            Set(t.Find("Post_L"), new Vector3(-(width + post) / 2f, (height + post) / 2f, 0f), new Vector3(post, height + post, depth));
            Set(t.Find("Post_R"), new Vector3((width + post) / 2f, (height + post) / 2f, 0f), new Vector3(post, height + post, depth));
            Set(t.Find("Lintel"), new Vector3(0f, height + post / 2f, 0f), new Vector3(width + 2f * post, post, depth));
            for (int i = 0; i < CurtainBeams; i++)
                Set(t.Find($"Beam_{i + 1}"), new Vector3(0f, height * (i + 1) / (CurtainBeams + 1), 0f), new Vector3(0.05f, width / 2f, 0.05f));
            Set(t.Find("Field"), new Vector3(0f, height / 2f, 0f), new Vector3(width, height, 0.04f));
            Set(t.Find("Sign_Front"), new Vector3(0f, height + post + 0.55f, -0.05f), Vector3.one * 0.9f);
            Set(t.Find("Sign_Back"), new Vector3(0f, height + post + 0.55f, 0.05f), Vector3.one * 0.9f);
        }

        // ------------------------------------------------------------------ bomb gates and plates

        /// <summary>A zone volume on <paramref name="root"/> with a <see cref="BombGate"/>, replicated.</summary>
        static BombGate AddGate(GameObject root, Vector3 size, Vector3 center, float holdSeconds)
        {
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            box.center = center;
            var zone = root.AddComponent<Zone>();
            SetReference(zone, "volume", box);
            var gate = root.AddComponent<BombGate>();
            SetField(gate, "holdSeconds", p => p.floatValue = holdSeconds);
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkBombGate>();
            return gate;
        }

        static void AddIndicator(GameObject root, MonoBehaviour source, IList<Renderer> lamps, Transform pressed = null)
        {
            var indicator = root.AddComponent<SignalIndicator>();
            SetReference(indicator, "source", source);
            SetField(indicator, "lamps", p =>
            {
                p.arraySize = lamps.Count;
                for (int i = 0; i < lamps.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
            });
            if (pressed != null) SetReference(indicator, "pressed", pressed);
        }

        /// <summary>
        /// A yellow ring facing Z (pivot at its centre) that the bomb must fly through; its rim is Environment, so
        /// clipping it explodes the bomb like any wall. It glows while active and pulses before it closes.
        /// </summary>
        static GameObject BuildGateRing()
        {
            const int segments = 12;
            const float radius = 1.3f;
            var root = new GameObject("BombGate_Ring") { layer = Layer("Trigger") };
            KinematicBody(root);
            var gate = AddGate(root, new Vector3(radius * 1.4f, radius * 1.4f, 0.6f), Vector3.zero, 8f);
            var lamps = new List<Renderer>();
            var mat = Mat("Greybox_Gate");
            float segLength = 2f * Mathf.PI * radius / segments * 1.08f;
            for (int i = 0; i < segments; i++)
            {
                var seg = new GameObject($"Segment_{i + 1}").transform;
                seg.SetParent(root.transform, false);
                seg.localRotation = Quaternion.Euler(0f, 0f, i * 360f / segments);
                lamps.Add(Cube("Visual", seg, new Vector3(0f, radius, 0f), new Vector3(segLength, 0.3f, 0.3f), mat, "Environment").GetComponent<Renderer>());
                Box("Collision", seg, new Vector3(0f, radius, 0f), new Vector3(segLength, 0.3f, 0.3f), "Environment");
            }
            AddIndicator(root, gate, lamps);
            return root;
        }

        public const float ArchWidth = 3.2f, ArchHeight = 3.4f;

        /// <summary>
        /// The checkpoint arch (PROJECT_SPEC §13.17): a yellow frame standing on the floor (pivot at the floor, centre),
        /// latched once the bomb flies through it until the section resets. Players walk through it too; only the
        /// flight counts. Wire it to a <see cref="Checkpoint"/>'s <c>claimGate</c>.
        /// </summary>
        static GameObject BuildGateArch()
        {
            const float post = 0.4f;
            var root = new GameObject("BombGate_Arch") { layer = Layer("Trigger") };
            var gate = AddGate(root, new Vector3(ArchWidth, ArchHeight, 0.6f), new Vector3(0f, ArchHeight / 2f, 0f), 0f);
            var mat = Mat("Greybox_Gate");
            var t = root.transform;
            var lamps = new List<Renderer>
            {
                Cube("Post_L", t, new Vector3(-(ArchWidth + post) / 2f, (ArchHeight + post) / 2f, 0f), new Vector3(post, ArchHeight + post, 0.6f), mat, "Environment", keepCollider: true).GetComponent<Renderer>(),
                Cube("Post_R", t, new Vector3((ArchWidth + post) / 2f, (ArchHeight + post) / 2f, 0f), new Vector3(post, ArchHeight + post, 0.6f), mat, "Environment", keepCollider: true).GetComponent<Renderer>(),
                Cube("Lintel", t, new Vector3(0f, ArchHeight + post / 2f, 0f), new Vector3(ArchWidth + 2f * post, post, 0.6f), mat, "Environment", keepCollider: true).GetComponent<Renderer>()
            };
            AddIndicator(root, gate, lamps);
            return root;
        }

        /// <summary>A floor pad (pivot on the floor, centre) held by any player standing on it, carrier included.</summary>
        static GameObject BuildPlate()
        {
            var root = new GameObject("PressurePlate") { layer = Layer("Trigger") };
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.4f, 1.4f, 2.4f);
            box.center = new Vector3(0f, 0.7f, 0f);
            var zone = root.AddComponent<Zone>();
            SetReference(zone, "volume", box);
            var plate = root.AddComponent<PressurePlate>();
            var t = root.transform;
            Shape("Rim", PrimitiveType.Cylinder, t, new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(2.8f, 0.02f, 2.8f), Mat("Greybox_Wall"), "Default");
            var pad = Shape("Pad", PrimitiveType.Cylinder, t, new Vector3(0f, 0.07f, 0f), Quaternion.identity, new Vector3(2.4f, 0.04f, 2.4f), Mat("Pad_Plate"), "Default");
            AddIndicator(root, plate, new[] { pad.GetComponent<Renderer>() }, pad.transform);
            return root;
        }

        // ------------------------------------------------------------------ actuators

        /// <summary>
        /// SignalActuator root / Platform (kinematic) / Visual + Collision (+ a lethal lower edge for doors, armed only
        /// while closing), Waypoint_Closed at the root, Waypoint_Open at <paramref name="travel"/>. Replicated.
        /// </summary>
        static GameObject BuildActuator(string name, Vector3 size, Vector3 travel, float seconds, Material mat, string layer, bool lethal)
        {
            var root = new GameObject(name);
            var platform = new GameObject("Platform");
            platform.transform.SetParent(root.transform, false);
            KinematicBody(platform);
            Cube("Visual", platform.transform, Vector3.zero, size, mat, layer);
            Box("Collision", platform.transform, Vector3.zero, size, layer);
            BoxCollider kill = null;
            if (lethal)
            {
                kill = Box("Kill", platform.transform, Vector3.zero, Vector3.one, "Trigger", trigger: true);
                kill.gameObject.AddComponent<KillZone>();
                kill.enabled = false;
            }
            var closed = new GameObject("Waypoint_Closed").transform;
            closed.SetParent(root.transform, false);
            var open = new GameObject("Waypoint_Open").transform;
            open.SetParent(root.transform, false);

            var actuator = root.AddComponent<SignalActuator>();
            SetReference(actuator, "platform", platform.transform);
            SetReference(actuator, "waypointClosed", closed);
            SetReference(actuator, "waypointOpen", open);
            SetField(actuator, "travelSeconds", p => p.floatValue = seconds);
            if (kill != null) SetReference(actuator, "lethalWhileClosing", kill);
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkSignalActuator>();
            ResizeActuator(root, size, travel);
            return root;
        }

        /// <summary>Resizes an actuator's moving part (and its lethal edge) and sets its open offset from the closed pose.</summary>
        public static void ResizeActuator(GameObject actuator, Vector3 size, Vector3 travel)
        {
            var t = actuator.transform;
            var platform = t.Find("Platform");
            platform.localPosition = Vector3.zero;
            platform.Find("Visual").localScale = size;
            platform.Find("Collision").GetComponent<BoxCollider>().size = size;
            var kill = platform.Find("Kill");
            if (kill != null)
            {
                kill.localPosition = new Vector3(0f, -size.y / 2f - 0.1f, 0f);   // reaches 0.25 m below the lower edge
                kill.GetComponent<BoxCollider>().size = new Vector3(size.x - 0.2f, 0.3f, size.z + 0.3f);
            }
            t.Find("Waypoint_Closed").localPosition = Vector3.zero;
            t.Find("Waypoint_Open").localPosition = travel;
        }

        /// <summary>Links an actuator to the one source that drives it.</summary>
        public static void Wire(GameObject actuator, MonoBehaviour source) => SetReference(actuator.GetComponent<SignalActuator>(), "source", source);

        static void Set(Transform t, Vector3 localPos, Vector3 scale)
        {
            t.localPosition = localPos;
            t.localScale = scale;
        }

        // ------------------------------------------------------------------ materials

        static void BuildMaterials()
        {
            // Floors: never colour alone (spec §19). Forbidden = hazard stripes, hot = checker, cold = plain + snowflakes.
            MakeMaterial("Zone_Forbidden_Floor", "Greybox_Hazard", new Color(1f, 0.3f, 0.34f), new Color(1f, 0.45f, 0.48f),
                         2, new Color(0.22f, 0.05f, 0.1f), 0.5f, 0.7f);
            MakeMaterial("Zone_Hot_Floor", "Greybox_Hazard", new Color(1f, 0.58f, 0.16f), new Color(1f, 0.72f, 0.3f),
                         1, new Color(0.6f, 0.2f, 0.05f), 0.75f, 0.35f);
            MakeMaterial("Zone_Cold_Floor", "Greybox_Hazard", new Color(0.62f, 0.9f, 1f), new Color(0.82f, 0.96f, 1f),
                         0, Color.black, 1f, 0f);
            MakeUnlitMaterial("Zone_Forbidden_Field", new Color(1f, 0.2f, 0.25f, 0.12f), null, false);
            MakeUnlitMaterial("Zone_Hot_Field", new Color(1f, 0.55f, 0.1f, 0.1f), null, false);
            MakeUnlitMaterial("Zone_Cold_Field", new Color(0.4f, 0.85f, 1f, 0.12f), null, false);
            MakeUnlitMaterial("Icon_Forbidden", new Color(1f, 0.4f, 0.4f, 1f), Icon("NoCarry"), false);
            MakeUnlitMaterial("Icon_Hot", new Color(1f, 0.7f, 0.25f, 1f), Icon("Flame"), false);
            MakeUnlitMaterial("Icon_Cold", new Color(0.65f, 0.93f, 1f, 1f), Icon("Snowflake"), false);
            MakeUnlitMaterial("Laser_Beam", new Color(1f, 0.18f, 0.2f, 0.9f), null, true);
            // Pressure plates: yellow and dark checker, reads as "step here" next to the plain floor.
            MakeMaterial("Pad_Plate", "Greybox_Hazard", new Color(1f, 0.83f, 0.25f), new Color(1f, 0.9f, 0.5f),
                         1, new Color(0.25f, 0.2f, 0.1f), 0.6f, 0.55f);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ icons (drawn in code, saved as PNG)

        static Texture2D Icon(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + name + ".png") ?? throw new InvalidOperationException("missing icon " + name);

        static void BuildIcons()
        {
            Directory.CreateDirectory(IconDir);
            DrawIcon("NoCarry", NoCarry);
            DrawIcon("Flame", Flame);
            DrawIcon("Snowflake", Snowflake);
            AssetDatabase.Refresh();
            foreach (var name in new[] { "NoCarry", "Flame", "Snowflake" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(IconDir + name + ".png");
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Rasterises a signed distance field (coordinates -1..1, negative inside) into a white glyph with a dark outline,
        /// so it reads on any background; the material tint colours the glyph.
        /// </summary>
        static void DrawIcon(string name, Func<Vector2, float> sdf)
        {
            const int n = 128;
            const float outline = 0.08f;
            float px = 2f / n;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
                    float d = sdf(p);
                    float inside = Mathf.Clamp01(0.5f - d / px);
                    float alpha = Mathf.Clamp01(0.5f - (d - outline) / px);
                    float c = Mathf.Lerp(0.08f, 1f, inside);
                    tex.SetPixel(x, y, new Color(c, c, c, alpha));
                }
            File.WriteAllBytes(IconDir + name + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        static float Ellipse(Vector2 p, Vector2 c, Vector2 radii)
        {
            Vector2 q = new Vector2((p.x - c.x) / radii.x, (p.y - c.y) / radii.y);
            return (q.magnitude - 1f) * Mathf.Min(radii.x, radii.y);
        }

        /// <summary>A potato crossed out inside a ring: "no carrying here".</summary>
        static float NoCarry(Vector2 p)
        {
            float ring = Mathf.Abs(p.magnitude - 0.74f) - 0.1f;
            Vector2 a = new Vector2(-0.52f, 0.52f), b = new Vector2(0.52f, -0.52f);
            float slash = Capsule(p, a, b, 0.1f);
            float potato = Mathf.Max(Ellipse(p, Vector2.zero, new Vector2(0.4f, 0.28f)), -Capsule(p, a, b, 0.2f));
            return Mathf.Min(ring, Mathf.Min(slash, potato));
        }

        /// <summary>A flame: a round base tapering to a point.</summary>
        static float Flame(Vector2 p)
        {
            float d = float.MaxValue;
            for (int i = 0; i <= 12; i++)
            {
                float k = i / 12f;
                var c = new Vector2(Mathf.Sin(k * 3.2f) * 0.1f * k, Mathf.Lerp(-0.35f, 0.72f, k));
                d = Mathf.Min(d, Circle(p, c, Mathf.Lerp(0.45f, 0.04f, k)));
            }
            return d;
        }

        /// <summary>A six-armed snowflake with a V branch on each arm.</summary>
        static float Snowflake(Vector2 p)
        {
            float d = float.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + Mathf.PI / 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var side = new Vector2(-dir.y, dir.x);
                d = Mathf.Min(d, Capsule(p, Vector2.zero, dir * 0.82f, 0.07f));
                Vector2 knot = dir * 0.5f;
                d = Mathf.Min(d, Capsule(p, knot, knot + (dir + side) * 0.2f, 0.06f));
                d = Mathf.Min(d, Capsule(p, knot, knot + (dir - side) * 0.2f, 0.06f));
            }
            return d;
        }
    }
}
