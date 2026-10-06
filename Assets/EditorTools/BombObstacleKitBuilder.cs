using System;
using System.Collections.Generic;
using System.IO;
using HotPatata;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        public const string Screen = ObstaclesDir + "Obstacle_BodyScreen";
        public const string Switch = GameplayDir + "Actuator_Switch";
        public const string PlateHandsFree = KitVariantBuilder.VariantsDir + "PressurePlate_HandsFree";
        public const string Tube = ObstaclesDir + "Obstacle_Tube";
        public const string Cannon = ObstaclesDir + "Obstacle_Cannon";
        const string TuningPath = "Assets/ScriptableObjects/Tuning/GameTuning.asset";
        const string MixerPath = "Assets/Audio/HotPatataMixer.mixer";
        public const int TubeSlots = 3;

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
                                                                   0.8f, KitRole.Hazard, "Hazard", lethal: true));
            MakePrefab(Bridge + ".prefab", true, () => BuildActuator("Actuator_Bridge", new Vector3(3f, 0.5f, 8f), new Vector3(0f, 0f, 8f),
                                                                     1.2f, KitRole.Mover, "Environment", lethal: false));
            MakePrefab(Lift + ".prefab", true, () => BuildActuator("Actuator_Lift", new Vector3(3f, 0.5f, 3f), new Vector3(0f, 4f, 0f),
                                                                   2f, KitRole.Mover, "Environment", lethal: false));
            MakePrefab(Tube + ".prefab", true, BuildTube);
            MakePrefab(Cannon + ".prefab", true, BuildCannon);
            MakePrefab(Screen + ".prefab", true, BuildScreen);
            MakePrefab(Switch + ".prefab", true, BuildSwitch);
            BuildHandsFreePlate();
            EnsureBombComponents();
            AssetDatabase.SaveAssets();
            Debug.Log("[BombObstacleKitBuilder] bomb obstacle kit built");
        }

        // ------------------------------------------------------------------ PassSandbox demo

        const string SandboxScene = "Assets/Scenes/PassSandbox.unity";

        /// <summary>
        /// Rebuilds <c>SectionRoot/KitDemo/BombObstacles</c> in PassSandbox, in the free north-west corner (clear of the test
        /// fixtures at z -14 and of the pass range): a forbidden strip, hot and cold zones, a window between two laser
        /// curtains, and a ring that raises a lift. Play them with the F4 bot.
        /// </summary>
        [MenuItem("HotPatata/Course/Build Sandbox Bomb Obstacles")]
        public static void BuildSandboxDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.OpenScene(SandboxScene);
            var kitDemo = GameObject.Find("SectionRoot/KitDemo")?.transform ?? throw new InvalidOperationException("PassSandbox has no SectionRoot/KitDemo");
            RebuildGroup(kitDemo, "BombObstacles", demo =>
            {
                ResizeZone(Place(demo, ZoneForbidden, "Demo_ForbiddenStrip", new Vector3(-14f, 0f, 8.5f), Quaternion.identity), new Vector3(8f, 2.5f, 1.5f));
                ResizeZone(Place(demo, ZoneHot, "Demo_HotZone", new Vector3(-18f, 0f, 11.5f), Quaternion.identity), new Vector3(3f, 2.5f, 3f));
                ResizeZone(Place(demo, ZoneCold, "Demo_ColdZone", new Vector3(-18f, 0f, 15.5f), Quaternion.identity), new Vector3(3f, 2.5f, 3f));
                ResizeCurtain(Place(demo, LaserCurtain, "Demo_Curtain_L", new Vector3(-15.2f, 0f, 13f), Quaternion.identity), 2f, 3.2f);
                ResizeCurtain(Place(demo, LaserCurtain, "Demo_Curtain_R", new Vector3(-10.8f, 0f, 13f), Quaternion.identity), 2f, 3.2f);
                var gate = Place(demo, GateRing, "Demo_Gate", new Vector3(-14f, 1.8f, 17f), Quaternion.identity).GetComponent<BombGate>();
                SetField(gate, "holdSeconds", p => p.floatValue = 6f);
                var lift = Place(demo, Lift, "Demo_Lift", new Vector3(-10.5f, 0.3f, 17.5f), Quaternion.identity);
                ResizeActuator(lift, new Vector3(2.5f, 0.5f, 2.5f), new Vector3(0f, 2.5f, 0f));
                Wire(lift, gate);
            });
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BombObstacleKitBuilder] PassSandbox bomb obstacles built");
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
            Cube("Post_L", t, Vector3.zero, Vector3.one, KitRole.Hazard, "Environment", keepCollider: true);
            Cube("Post_R", t, Vector3.zero, Vector3.one, KitRole.Hazard, "Environment", keepCollider: true);
            Cube("Lintel", t, Vector3.zero, Vector3.one, KitRole.Hazard, "Environment", keepCollider: true);
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
            // Just outside a 1 m wall's faces, so the signs show when the curtain closes a passage in a wall.
            Set(t.Find("Sign_Front"), new Vector3(0f, height + post + 0.55f, -0.56f), Vector3.one * 0.9f);
            Set(t.Find("Sign_Back"), new Vector3(0f, height + post + 0.55f, 0.56f), Vector3.one * 0.9f);
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
            float segLength = 2f * Mathf.PI * radius / segments * 1.08f;
            for (int i = 0; i < segments; i++)
            {
                var seg = new GameObject($"Segment_{i + 1}").transform;
                seg.SetParent(root.transform, false);
                seg.localRotation = Quaternion.Euler(0f, 0f, i * 360f / segments);
                lamps.Add(Cube("Visual", seg, new Vector3(0f, radius, 0f), new Vector3(segLength, 0.3f, 0.3f), KitRole.Gate, "Environment").GetComponent<Renderer>());
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
            const KitRole mat = KitRole.Gate;
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

        /// <summary>
        /// The hands-free plate (PROJECT_SPEC §13.19): a Prefab Variant of the plate whose carrier does not count, drawn in blue
        /// with a "throw first" glyph so the rule reads before anyone steps on it.
        /// </summary>
        static void BuildHandsFreePlate()
        {
            Directory.CreateDirectory(KitVariantBuilder.VariantsDir);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Plate + ".prefab"));
            try
            {
                go.name = "PressurePlate_HandsFree";
                SetField(go.GetComponent<PressurePlate>(), "countCarrier", p => p.boolValue = false);
                var pad = go.transform.Find("Pad").GetComponent<Renderer>();
                pad.sharedMaterial = Mat("Pad_HandsFree");
                PrefabUtility.RecordPrefabInstancePropertyModifications(pad);
                Shape("Icon", PrimitiveType.Quad, go.transform, new Vector3(0f, 0.1f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one * 1.7f,
                      Mat("Icon_HandsFree"), "Default");
                PrefabUtility.SaveAsPrefabAsset(go, PlateHandsFree + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ------------------------------------------------------------------ body screens (brambles, nets) and switches

        const int ScreenStrandsAcross = 9, ScreenStrandsUp = 6;
        const float ScreenDepth = 0.3f, ScreenPost = 0.35f;

        /// <summary>
        /// A body screen (PROJECT_SPEC §13.18; pivot on the floor, centre; spans X, faces Z): a frame of posts and a lintel
        /// (Environment, like any wall: the bomb explodes on them) around the <c>Screen</c>, a <see cref="BodyScreen"/> whose solid
        /// box is on the BodyScreen layer (players only) and whose vine strands, faint field and "bomb through" signs show
        /// what it is. A switch targets <c>Screen</c>, so the frame stays when it parts.
        /// </summary>
        static GameObject BuildScreen()
        {
            var root = new GameObject("Obstacle_BodyScreen");
            var t = root.transform;
            var frame = new GameObject("Frame").transform;
            frame.SetParent(t, false);
            Cube("Post_L", frame, Vector3.zero, Vector3.one, KitRole.Frame, "Environment", keepCollider: true);
            Cube("Post_R", frame, Vector3.zero, Vector3.one, KitRole.Frame, "Environment", keepCollider: true);
            Cube("Lintel", frame, Vector3.zero, Vector3.one, KitRole.Frame, "Environment", keepCollider: true);

            var screen = new GameObject("Screen") { layer = Layer(BodyScreen.LayerName) };
            screen.transform.SetParent(t, false);
            screen.AddComponent<BodyScreen>();
            Box("Collision", screen.transform, Vector3.zero, Vector3.one, BodyScreen.LayerName);
            var strand = Mat("Screen_Strand");
            for (int i = 0; i < ScreenStrandsAcross; i++)
                Shape($"Strand_V_{i + 1}", PrimitiveType.Cylinder, screen.transform, Vector3.zero, Quaternion.identity, Vector3.one, strand, "Default");
            for (int i = 0; i < ScreenStrandsUp; i++)
                Shape($"Strand_H_{i + 1}", PrimitiveType.Cylinder, screen.transform, Vector3.zero, Quaternion.Euler(0f, 0f, 90f), Vector3.one, strand, "Default");
            Shape("Field", PrimitiveType.Cube, screen.transform, Vector3.zero, Quaternion.identity, Vector3.one, Mat("Screen_Field"), "Default");
            var icon = Mat("Icon_Screen");
            Shape("Sign_Front", PrimitiveType.Quad, screen.transform, Vector3.zero, Quaternion.identity, Vector3.one, icon, "Default");
            Shape("Sign_Back", PrimitiveType.Quad, screen.transform, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), Vector3.one, icon, "Default");
            ResizeScreen(root, 4f, 3.6f);
            return root;
        }

        /// <summary>
        /// Sizes a body screen (pivot on the floor, centre): the players-only box fills <paramref name="width"/> ×
        /// <paramref name="height"/> between the posts, under the lintel; the strands, field and signs follow.
        /// </summary>
        public static void ResizeScreen(GameObject screen, float width, float height)
        {
            var t = screen.transform;
            var frame = t.Find("Frame");
            Set(frame.Find("Post_L"), new Vector3(-(width + ScreenPost) / 2f, (height + ScreenPost) / 2f, 0f), new Vector3(ScreenPost, height + ScreenPost, ScreenDepth));
            Set(frame.Find("Post_R"), new Vector3((width + ScreenPost) / 2f, (height + ScreenPost) / 2f, 0f), new Vector3(ScreenPost, height + ScreenPost, ScreenDepth));
            Set(frame.Find("Lintel"), new Vector3(0f, height + ScreenPost / 2f, 0f), new Vector3(width + 2f * ScreenPost, ScreenPost, ScreenDepth));
            var s = t.Find("Screen");
            var box = s.Find("Collision").GetComponent<BoxCollider>();
            box.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            box.size = new Vector3(width, height, ScreenDepth);
            for (int i = 0; i < ScreenStrandsAcross; i++)
                Set(s.Find($"Strand_V_{i + 1}"), new Vector3(width * ((i + 1f) / (ScreenStrandsAcross + 1) - 0.5f), height / 2f, 0f), new Vector3(0.07f, height / 2f, 0.07f));
            for (int i = 0; i < ScreenStrandsUp; i++)
                Set(s.Find($"Strand_H_{i + 1}"), new Vector3(0f, height * (i + 1f) / (ScreenStrandsUp + 1), 0f), new Vector3(0.07f, width / 2f, 0.07f));
            Set(s.Find("Field"), new Vector3(0f, height / 2f, 0f), new Vector3(width, height, 0.04f));
            Set(s.Find("Sign_Front"), new Vector3(0f, Mathf.Min(height - 0.8f, 2.6f), -0.2f), Vector3.one * 0.9f);
            Set(s.Find("Sign_Back"), new Vector3(0f, Mathf.Min(height - 0.8f, 2.6f), 0.2f), Vector3.one * 0.9f);
        }

        /// <summary>
        /// A <see cref="SignalSwitch"/> with no look of its own (PROJECT_SPEC §13.19): wire a source and its targets in the course
        /// (<see cref="WireSwitch"/>). Replicated like any actuator.
        /// </summary>
        static GameObject BuildSwitch()
        {
            var root = new GameObject("Actuator_Switch");
            var sw = root.AddComponent<SignalSwitch>();
            SetField(sw, "travelSeconds", p => p.floatValue = 0.25f);
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkSignalActuator>();
            return root;
        }

        /// <summary>Links a switch to its one source and the objects it turns on and off (<paramref name="activeWhenOpen"/>: on while the source is active).</summary>
        public static void WireSwitch(GameObject sw, MonoBehaviour source, bool activeWhenOpen, params GameObject[] targets)
        {
            var s = sw.GetComponent<SignalSwitch>();
            SetReference(s, "source", source);
            SetField(s, "activeWhenOpen", p => p.boolValue = activeWhenOpen);
            SetField(s, "targets", p =>
            {
                p.arraySize = targets.Length;
                for (int i = 0; i < targets.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            });
        }

        // ------------------------------------------------------------------ actuators

        /// <summary>
        /// SignalActuator root / Platform (kinematic) / Visual + Collision (+ a lethal lower edge for doors, armed only
        /// while closing), Waypoint_Closed at the root, Waypoint_Open at <paramref name="travel"/>. Replicated.
        /// </summary>
        static GameObject BuildActuator(string name, Vector3 size, Vector3 travel, float seconds, KitRole role, string layer, bool lethal)
        {
            var root = new GameObject(name);
            var platform = new GameObject("Platform");
            platform.transform.SetParent(root.transform, false);
            KinematicBody(platform);
            Cube("Visual", platform.transform, Vector3.zero, size, role, layer);
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
            var a = actuator.GetComponent<SignalActuator>();
            var platform = a.Platform;
            platform.localPosition = Vector3.zero;
            platform.Find("Visual").localScale = size;
            platform.Find("Collision").GetComponent<BoxCollider>().size = size;
            var kill = a.LethalEdge != null ? a.LethalEdge.transform : null;
            if (kill != null)
            {
                kill.localPosition = new Vector3(0f, -size.y / 2f - 0.1f, 0f);   // reaches 0.25 m below the lower edge
                kill.GetComponent<BoxCollider>().size = new Vector3(size.x - 0.2f, 0.3f, size.z + 0.3f);
            }
            a.WaypointClosed.localPosition = Vector3.zero;
            a.WaypointOpen.localPosition = travel;
        }

        // ------------------------------------------------------------------ transit: tubes and cannons

        static GameTuning Tuning => AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath);

        /// <summary>The mixer's SFX group, so the transit tone follows the effects volume.</summary>
        static UnityEngine.Audio.AudioMixerGroup SfxGroup
        {
            get
            {
                var groups = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(MixerPath)?.FindMatchingGroups("Master/SFX");
                return groups != null && groups.Length > 0 ? groups[0] : null;
            }
        }

        /// <summary>A ring of <paramref name="segments"/> collision blocks in the local XY plane (facing Z), on <paramref name="layer"/>.</summary>
        static void Ring(Transform parent, float radius, int segments, float thickness, string layer)
        {
            float segLength = 2f * Mathf.PI * radius / segments * 1.1f;
            for (int i = 0; i < segments; i++)
            {
                var seg = new GameObject($"Ring_{i + 1}").transform;
                seg.SetParent(parent, false);
                seg.localRotation = Quaternion.Euler(0f, 0f, i * 360f / segments);
                Box("Collision", seg, new Vector3(0f, radius, 0f), new Vector3(segLength, thickness, thickness), layer);
            }
        }

        // ------------------------------------------------------------------ KayKit pipes (ARCHITECTURE §25.1)

        /// <summary>
        /// Pipe pieces at this scale have a 1 m radius: the pipe_end flange (0.85..1.15 m) then lines up with a tube mouth's
        /// collision ring (0.9..1.2 m), and a pipe_90 elbow turns in 2 m along and 2 m across.
        /// </summary>
        public const float PipeScale = 1f;
        const float FlangeLength = 1f, ElbowReach = 2f;

        /// <summary>A single KayKit pipe model (pipe_end, pipe_90_A) in a slot's pipe material; <paramref name="localRot"/> turns the model's +Y.</summary>
        static GameObject PipeModel(string name, string model, Transform parent, Vector3 localPos, Quaternion localRot, float scale, Material mat,
                                    string layer, bool collider)
        {
            var mesh = KayKitKitBuilder.ModelMesh(model, KitColor.Blue);
            var go = new GameObject(name) { layer = Layer(layer) };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = mesh.bounds.center;
                box.size = mesh.bounds.size;
            }
            return go;
        }

        /// <summary>A straight pipe run (KayKit pipe_straight pieces along its Y), with a capsule collider; placed by <see cref="PlaceStraight"/>.</summary>
        static GameObject PipeStraight(string name, Transform parent, Material mat)
        {
            var go = new GameObject(name) { layer = Layer("Environment") };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<CapsuleCollider>();   // radius 0.5, height 2 along Y: the cylinder box, scaled
            Skin(go, KitShape.Pipe, KitColor.Blue, mat, CylinderBox);
            return go;
        }

        static void PlaceStraight(Transform straight, Vector3 from, Vector3 to)
        {
            float length = Vector3.Distance(from, to);
            bool used = length > 0.05f;
            straight.gameObject.SetActive(used);
            if (!used) return;
            straight.SetPositionAndRotation((from + to) / 2f, Quaternion.FromToRotation(Vector3.up, (to - from) / length));
            straight.localScale = new Vector3(2f * PipeScale, length / 2f, 2f * PipeScale);
            // A capsule shorter than its diameter turns into a ball that overhangs the run (and the exit arc): never wider
            // than the run is long. The elbows cover the joints.
            var capsule = straight.GetComponent<CapsuleCollider>();
            capsule.radius = Mathf.Min(0.5f, length / (4f * PipeScale));
        }

        /// <summary>Places a pipe_90 elbow entering at <paramref name="entry"/> along <paramref name="dirIn"/>; returns where it comes out (along <paramref name="dirOut"/>).</summary>
        static Vector3 PlaceElbow(Transform elbow, bool used, Vector3 entry, Vector3 dirIn, Vector3 dirOut)
        {
            elbow.gameObject.SetActive(used);
            if (!used) return entry;
            elbow.SetPositionAndRotation(entry, Quaternion.LookRotation(dirOut, dirIn));   // the model enters along +Y, leaves along +Z
            return entry + (dirIn + dirOut) * ElbowReach * PipeScale;
        }

        /// <summary>The receiver pad on the floor: a disc in the slot's colour with its pip symbol (collider-free).</summary>
        static Transform Pad(Transform parent, string name, int slot)
        {
            var pad = new GameObject(name).transform;
            pad.SetParent(parent, false);
            Shape("Disc", PrimitiveType.Cylinder, pad, new Vector3(0f, 0.04f, 0f), Quaternion.identity, new Vector3(1.8f, 0.03f, 1.8f), Mat($"Tube_Slot_{slot}"), "Default");
            Shape("Icon", PrimitiveType.Quad, pad, new Vector3(0f, 0.08f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one * 1.2f, Mat($"Icon_Pips{slot}"), "Default");
            return pad;
        }

        /// <summary>A transit root: BombTransit + exit lamps presentation, replicated.</summary>
        static BombTransit AddTransit(GameObject root, float delay)
        {
            var transit = root.AddComponent<BombTransit>();
            SetField(transit, "delay", p => p.floatValue = delay);
            SetReference(transit, "tuning", Tuning);
            var presentation = root.AddComponent<TransitPresentation>();
            SetReference(presentation, "output", SfxGroup);
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkBombTransit>();
            return transit;
        }

        static void AddMouth(Transform mouth, BombTransit transit, int exit, Vector3 size, Vector3 center)
        {
            mouth.gameObject.layer = Layer("Trigger");
            var box = mouth.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            box.center = center;
            var zone = mouth.gameObject.AddComponent<Zone>();
            SetReference(zone, "volume", box);
            var m = mouth.gameObject.AddComponent<TransitMouth>();
            SetReference(m, "transit", transit);
            SetField(m, "exit", p => p.intValue = exit);
        }

        static void SetExits(BombTransit transit, Transform[] holds, Transform[] muzzles, Transform[] pads, float flightTime)
        {
            SetField(transit, "exits", p =>
            {
                p.arraySize = holds.Length;
                for (int i = 0; i < holds.Length; i++)
                {
                    var e = p.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("hold").objectReferenceValue = holds[i];
                    e.FindPropertyRelative("muzzle").objectReferenceValue = muzzles[i];
                    e.FindPropertyRelative("pad").objectReferenceValue = pads[i];
                    e.FindPropertyRelative("flightTime").floatValue = flightTime;
                }
            });
        }

        static void SetLamps(GameObject root, IList<Renderer> lamps) =>
            SetField(root.GetComponent<TransitPresentation>(), "exitLamps", p =>
            {
                p.arraySize = lamps.Count;
                for (int i = 0; i < lamps.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
            });

        /// <summary>
        /// A tube with up to three mouth/exit pairs (PROJECT_SPEC §13.16). Slot N: Mouth_N (a ring facing the thrower, its
        /// capture volume reaching 1.3 m in front), Pipe_N (the straight pipe the bomb travels in, out of sight), Exit_N
        /// (nozzle, lamp, muzzle) and Pad_N (the receiver pad). Each slot has its colour and pip symbol. Place it with
        /// <see cref="ConfigureTube"/>; unused slots are switched off.
        /// </summary>
        static GameObject BuildTube()
        {
            var root = new GameObject("Obstacle_Tube");
            var transit = AddTransit(root, 1.2f);
            var holds = new Transform[TubeSlots];
            var muzzles = new Transform[TubeSlots];
            var pads = new Transform[TubeSlots];
            var lamps = new List<Renderer>();
            for (int s = 1; s <= TubeSlots; s++)
            {
                var pipeMat = Mat($"Tube_Pipe_{s}");
                var mouth = new GameObject($"Mouth_{s}").transform;
                mouth.SetParent(root.transform, false);
                AddMouth(mouth, transit, s - 1, new Vector3(2.2f, 2.2f, 1.6f), new Vector3(0f, 0f, -0.5f));
                Ring(mouth, 1.05f, 12, 0.3f, "Environment");
                // The flange's rim sits on the ring, facing the thrower; the pipe leaves from its base, 1 m behind.
                PipeModel("Flange", "pipe_end", mouth, new Vector3(0f, 0f, FlangeLength * PipeScale), Quaternion.Euler(-90f, 0f, 0f), PipeScale,
                          pipeMat, "Environment", collider: false);
                Shape("Sign", PrimitiveType.Quad, mouth, new Vector3(0f, 1.9f, -0.2f), Quaternion.identity, Vector3.one * 1.1f, Mat($"Icon_Pips{s}"), "Default");
                var hold = new GameObject("Hold").transform;
                hold.SetParent(mouth, false);
                hold.localPosition = new Vector3(0f, 0f, 1.2f);
                holds[s - 1] = hold;

                // The route: straight, elbow, straight, elbow, straight (ConfigureTube switches off what a route does not use).
                var pipe = new GameObject($"Pipe_{s}").transform;
                pipe.SetParent(root.transform, false);
                PipeStraight("Straight_1", pipe, pipeMat);
                PipeModel("Elbow_1", "pipe_90_A", pipe, Vector3.zero, Quaternion.identity, PipeScale, pipeMat, "Environment", collider: true);
                PipeStraight("Straight_2", pipe, pipeMat);
                PipeModel("Elbow_2", "pipe_90_A", pipe, Vector3.zero, Quaternion.identity, PipeScale, pipeMat, "Environment", collider: true);
                PipeStraight("Straight_3", pipe, pipeMat);

                var exit = new GameObject($"Exit_{s}").transform;
                exit.SetParent(root.transform, false);
                PipeModel("Nozzle", "pipe_end", exit, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), PipeScale, pipeMat, "Environment", collider: false);
                lamps.Add(Shape("Lamp", PrimitiveType.Cylinder, exit, new Vector3(0f, 0f, 0.97f * FlangeLength * PipeScale), Quaternion.Euler(90f, 0f, 0f),
                                new Vector3(1.7f * PipeScale, 0.04f, 1.7f * PipeScale), Mat("Tube_Lamp"), "Default").GetComponent<Renderer>());
                Shape("Sign", PrimitiveType.Quad, exit, new Vector3(0f, 1.9f, 0.5f), Quaternion.identity, Vector3.one * 0.9f, Mat($"Icon_Pips{s}"), "Default");
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(exit, false);
                muzzle.localPosition = new Vector3(0f, 0f, 0.9f);
                muzzles[s - 1] = muzzle;

                pads[s - 1] = Pad(root.transform, $"Pad_{s}", s);
            }
            SetExits(transit, holds, muzzles, pads, 1.1f);
            SetLamps(root, lamps);
            ConfigureTube(root, new[]
            {
                new TubeSlot(new Vector3(0f, 1.6f, 0f), 0f, new Vector3(0f, 5f, 8f), new Vector3(0f, 0f, 14f), 1.1f)
            });
            return root;
        }

        /// <summary>One mouth-to-exit route of a tube, in world space.</summary>
        public struct TubeSlot
        {
            public Vector3 Mouth;      // centre of the mouth ring
            public float MouthYaw;     // degrees; 0 = the mouth faces -Z (thrown into while travelling +Z)
            public Vector3 Exit;       // where the bomb comes out
            public Vector3 Pad;        // the receiver pad, on the floor
            public float FlightTime;   // seconds from the exit to catch height above the pad

            public TubeSlot(Vector3 mouth, float mouthYaw, Vector3 exit, Vector3 pad, float flightTime)
            {
                Mouth = mouth;
                MouthYaw = mouthYaw;
                Exit = exit;
                Pad = pad;
                FlightTime = flightTime;
            }
        }

        /// <summary>
        /// Lays out a tube's routes (1..3; the other slots are switched off): mouth, KayKit pipe from the mouth to the exit,
        /// exit flange turned along the launch direction of its fixed arc, and the receiver pad. The pipe climbs straight up
        /// behind the mouth and runs overhead to the exit (two elbows) when the exit is high and far enough; else it runs
        /// low, then climbs into the exit (one elbow); else it is one straight pipe.
        /// </summary>
        public static void ConfigureTube(GameObject tube, IList<TubeSlot> slots)
        {
            var t = tube.transform;
            var transit = tube.GetComponent<BombTransit>();
            var tuning = Tuning;
            float g = ThrowBallistics.Gravity(tuning);
            for (int s = 1; s <= TubeSlots; s++)
            {
                bool used = s <= slots.Count;
                foreach (var part in new[] { $"Mouth_{s}", $"Pipe_{s}", $"Exit_{s}", $"Pad_{s}" })
                    t.Find(part).gameObject.SetActive(used);
                if (!used) continue;

                var slot = slots[s - 1];
                var mouth = t.Find($"Mouth_{s}");
                mouth.SetPositionAndRotation(slot.Mouth, Quaternion.Euler(0f, slot.MouthYaw, 0f));
                t.Find($"Pad_{s}").position = slot.Pad;

                var exit = t.Find($"Exit_{s}");
                Vector3 aim = slot.Pad + Vector3.up * tuning.catchCenterHeight;
                Vector3 v = BombTransit.ExitVelocity(slot.Exit, aim, g, slot.FlightTime);
                exit.SetPositionAndRotation(slot.Exit - v.normalized * 0.9f, Quaternion.LookRotation(v.normalized));   // muzzle lands on slot.Exit

                RoutePipe(t.Find($"Pipe_{s}"), mouth, exit.position);

                SetField(transit, "exits", p => p.GetArrayElementAtIndex(s - 1).FindPropertyRelative("flightTime").floatValue = slot.FlightTime);
            }
        }

        /// <summary>The pipe from a mouth's flange base to an exit (see <see cref="ConfigureTube"/>).</summary>
        static void RoutePipe(Transform pipe, Transform mouth, Vector3 exit)
        {
            float reach = ElbowReach * PipeScale;
            Vector3 start = mouth.TransformPoint(new Vector3(0f, 0f, FlangeLength * PipeScale));
            Vector3 along = mouth.forward;
            along.y = 0f;
            along.Normalize();
            float rise = exit.y - start.y;
            Vector3 riser = start + along * reach;   // where a riser right behind the mouth would stand
            Vector3 over = exit - riser;
            over.y = 0f;
            Transform s1 = pipe.Find("Straight_1"), e1 = pipe.Find("Elbow_1"), s2 = pipe.Find("Straight_2"),
                      e2 = pipe.Find("Elbow_2"), s3 = pipe.Find("Straight_3");

            if (rise >= 2f * reach && over.magnitude >= reach)
            {
                // Up behind the mouth, then overhead into the exit.
                Vector3 toward = over.normalized;
                PlaceStraight(s1, start, start);
                Vector3 up0 = PlaceElbow(e1, true, start, along, Vector3.up);
                Vector3 up1 = new Vector3(up0.x, exit.y - reach, up0.z);
                PlaceStraight(s2, up0, up1);
                Vector3 run0 = PlaceElbow(e2, true, up1, Vector3.up, toward);
                PlaceStraight(s3, run0, exit);
                return;
            }
            Vector3 flatToExit = exit - start;
            flatToExit.y = 0f;
            if (rise >= reach && flatToExit.magnitude >= reach)
            {
                // Low along the floor, then up into the exit.
                Vector3 toward = flatToExit.normalized;
                Vector3 bend = new Vector3(exit.x, start.y, exit.z) - toward * reach;
                PlaceStraight(s1, start, bend);
                Vector3 up0 = PlaceElbow(e1, true, bend, toward, Vector3.up);
                PlaceStraight(s2, up0, exit);
                PlaceElbow(e2, false, exit, Vector3.up, toward);
                PlaceStraight(s3, exit, exit);
                return;
            }
            PlaceStraight(s1, start, exit);
            PlaceElbow(e1, false, start, along, Vector3.up);
            PlaceStraight(s2, exit, exit);
            PlaceElbow(e2, false, exit, Vector3.up, along);
            PlaceStraight(s3, exit, exit);
        }

        /// <summary>
        /// A cannon (PROJECT_SPEC §13.16): an open basket (the mouth: throw the bomb in from above or from any side, its
        /// capture volume reaches 2 m over the rim) and a barrel that fires it far and high after a short delay, onto the
        /// receiver pad. Place it with <see cref="ConfigureCannon"/>.
        /// </summary>
        static GameObject BuildCannon()
        {
            const float basket = 2.4f, wall = 0.25f, rim = 0.8f;
            var root = new GameObject("Obstacle_Cannon");
            var transit = AddTransit(root, 0.35f);
            var t = root.transform;

            var mouth = new GameObject("Basket").transform;
            mouth.SetParent(t, false);
            AddMouth(mouth, transit, 0, new Vector3(basket, 2.6f, basket), new Vector3(0f, rim + 1.1f, 0f));
            Cube("Floor", mouth, new Vector3(0f, 0.1f, 0f), new Vector3(basket + 2f * wall, 0.2f, basket + 2f * wall), KitRole.Wall, "Environment", keepCollider: true);
            for (int i = 0; i < 4; i++)
            {
                var side = Quaternion.Euler(0f, i * 90f, 0f);
                var w = Cube($"Wall_{i + 1}", mouth, side * new Vector3(0f, rim / 2f, (basket + wall) / 2f), new Vector3(basket + 2f * wall, rim, wall), KitRole.Gate, "Environment", keepCollider: true);
                w.transform.localRotation = side;
            }
            Shape("Sign", PrimitiveType.Quad, mouth, new Vector3(0f, rim + 2.4f, 0f), Quaternion.identity, Vector3.one * 1.1f, Mat("Icon_Pips1"), "Default");
            Shape("Sign_Back", PrimitiveType.Quad, mouth, new Vector3(0f, rim + 2.4f, 0f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 1.1f, Mat("Icon_Pips1"), "Default");
            var hold = new GameObject("Hold").transform;
            hold.SetParent(mouth, false);
            hold.localPosition = new Vector3(0f, 0.55f, 0f);

            var exit = new GameObject("Exit_1").transform;
            exit.SetParent(t, false);
            // A KayKit pipe barrel (1 m across) ending in a flange at the muzzle.
            var pipeMat = Mat("Tube_Pipe_1");
            var barrel = Shape("Barrel", PrimitiveType.Cylinder, exit, new Vector3(0f, 0f, -1.1f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1f, 1.1f, 1f), pipeMat, "Environment");
            barrel.AddComponent<CapsuleCollider>();
            Skin(barrel, KitShape.Pipe, KitColor.Blue, pipeMat, CylinderBox);
            PipeModel("Flange", "pipe_end", exit, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), 0.5f, pipeMat, "Environment", collider: false);
            var lamp = Shape("Lamp", PrimitiveType.Cylinder, exit, new Vector3(0f, 0f, 0.48f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.85f, 0.04f, 0.85f), Mat("Tube_Lamp"), "Default");
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(exit, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.7f);

            var pad = Pad(t, "Pad_1", 1);
            SetExits(transit, new[] { hold }, new[] { muzzle }, new[] { pad }, 2f);
            SetLamps(root, new[] { lamp.GetComponent<Renderer>() });
            ConfigureCannon(root, new Vector3(0f, 0f, 24f), 2f);
            return root;
        }

        /// <summary>
        /// Aims a cannon (root on the floor at the basket) at a receiver pad: the barrel stands beside the basket, turned
        /// along the launch direction of the fixed arc that reaches catch height above the pad after <paramref name="flightTime"/>.
        /// </summary>
        public static void ConfigureCannon(GameObject cannon, Vector3 pad, float flightTime)
        {
            var t = cannon.transform;
            var tuning = Tuning;
            Vector3 flat = pad - t.position;
            flat.y = 0f;
            Quaternion yaw = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat.normalized) : Quaternion.identity;
            t.Find("Basket").localRotation = Quaternion.Inverse(t.rotation) * yaw;
            t.Find("Pad_1").position = pad;
            Vector3 muzzle = t.position + yaw * new Vector3(0f, 2.6f, 2.2f);
            Vector3 v = BombTransit.ExitVelocity(muzzle, pad + Vector3.up * tuning.catchCenterHeight, ThrowBallistics.Gravity(tuning), flightTime);
            t.Find("Exit_1").SetPositionAndRotation(muzzle - v.normalized * 0.7f, Quaternion.LookRotation(v.normalized));
            SetField(cannon.GetComponent<BombTransit>(), "exits", p => p.GetArrayElementAtIndex(0).FindPropertyRelative("flightTime").floatValue = flightTime);
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
            // Tube / cannon slots: a colour AND a pip count (1, 2, 3) per mouth-exit pair, away from hazard red and player colours.
            var slotColors = new[] { new Color(0.25f, 0.85f, 0.45f), new Color(0.62f, 0.45f, 1f), new Color(1f, 0.6f, 0.8f) };
            for (int s = 1; s <= TubeSlots; s++)
            {
                Color c = slotColors[s - 1];
                MakeMaterial($"Tube_Slot_{s}", "Greybox_Hazard", c, Color.Lerp(c, Color.white, 0.3f), 0, Color.black, 1f, 0f);
                MakeUnlitMaterial($"Icon_Pips{s}", Color.Lerp(c, Color.white, 0.25f), Icon($"Pips{s}"), false);
                PipeMaterial($"Tube_Pipe_{s}", c);
            }
            MakeMaterial("Tube_Lamp", "Greybox_Hazard", new Color(0.95f, 0.92f, 0.8f), Color.white, 0, Color.black, 1f, 0f);
            // Pressure plates: yellow and dark checker, reads as "step here" next to the plain floor.
            MakeMaterial("Pad_Plate", "Greybox_Hazard", new Color(1f, 0.83f, 0.25f), new Color(1f, 0.9f, 0.5f),
                         1, new Color(0.25f, 0.2f, 0.1f), 0.6f, 0.55f);
            // Hands-free plates: blue and white checker plus the "throw first" glyph, never the yellow of a plain plate.
            MakeMaterial("Pad_HandsFree", "Greybox_Hazard", new Color(0.35f, 0.62f, 1f), new Color(0.6f, 0.8f, 1f),
                         1, new Color(0.92f, 0.96f, 1f), 0.6f, 0.5f);
            MakeUnlitMaterial("Icon_HandsFree", new Color(0.95f, 0.97f, 1f, 1f), Icon("ThrowFirst"), false);
            // Body screens: vine-green strands over a faint green field, and the "bomb through" sign: the same cue in every look.
            MakeMaterial("Screen_Strand", "Greybox_Hazard", new Color(0.3f, 0.58f, 0.22f), new Color(0.45f, 0.72f, 0.3f),
                         0, Color.black, 1f, 0f);
            MakeUnlitMaterial("Screen_Field", new Color(0.35f, 0.9f, 0.4f, 0.1f), null, false);
            MakeUnlitMaterial("Icon_Screen", new Color(0.6f, 1f, 0.55f, 1f), Icon("BombThrough"), false);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The KayKit pipe material of a slot: the pack's texture through the toon suit tint, so the pipe's coloured parts
        /// take the slot colour and its grey and white bands stay (ARCHITECTURE §25.1).
        /// </summary>
        static void PipeMaterial(string name, Color color)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                AssetDatabase.CopyAsset(MatDir + KitMaterial + ".mat", path);
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_SuitTint", 1f);
            EditorUtility.SetDirty(mat);
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
            DrawIcon("BombThrough", BombThrough);
            DrawIcon("ThrowFirst", ThrowFirst);
            for (int s = 1; s <= TubeSlots; s++)
            {
                int pips = s;
                DrawIcon($"Pips{s}", p => Pips(p, pips));
            }
            AssetDatabase.Refresh();
            foreach (var name in new[] { "NoCarry", "Flame", "Snowflake", "BombThrough", "ThrowFirst", "Pips1", "Pips2", "Pips3" })
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

        /// <summary>A potato flying through two bars, motion line behind it: "the bomb goes through, you don't" (body screens).</summary>
        static float BombThrough(Vector2 p)
        {
            float bars = Mathf.Min(Capsule(p, new Vector2(-0.05f, -0.72f), new Vector2(-0.05f, 0.72f), 0.07f),
                                   Capsule(p, new Vector2(0.22f, -0.72f), new Vector2(0.22f, 0.72f), 0.07f));
            float potato = Ellipse(p, new Vector2(0.5f, 0.05f), new Vector2(0.28f, 0.2f));
            float trail = Mathf.Min(Capsule(p, new Vector2(-0.8f, 0.12f), new Vector2(-0.3f, 0.12f), 0.05f),
                                    Capsule(p, new Vector2(-0.7f, -0.08f), new Vector2(-0.35f, -0.08f), 0.05f));
            return Mathf.Min(bars, Mathf.Min(potato, trail));
        }

        /// <summary>A potato and an arrow leaving it up and away: "pass it first" (hands-free plates).</summary>
        static float ThrowFirst(Vector2 p)
        {
            float potato = Ellipse(p, new Vector2(-0.35f, -0.35f), new Vector2(0.32f, 0.23f));
            Vector2 tip = new Vector2(0.6f, 0.6f);
            float shaft = Capsule(p, new Vector2(-0.05f, 0f), tip, 0.08f);
            float head = Mathf.Min(Capsule(p, tip, tip + new Vector2(-0.38f, 0f), 0.08f), Capsule(p, tip, tip + new Vector2(0f, -0.38f), 0.08f));
            return Mathf.Min(potato, Mathf.Min(shaft, head));
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

        /// <summary><paramref name="count"/> big dots (1, 2 or 3) inside a ring: the symbol of a tube's mouth, exit and pad.</summary>
        static float Pips(Vector2 p, int count)
        {
            float d = Mathf.Abs(p.magnitude - 0.8f) - 0.07f;
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.PI / 2f + i * 2f * Mathf.PI / count;
                Vector2 c = count == 1 ? Vector2.zero : new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.36f;
                d = Mathf.Min(d, Circle(p, c, count == 1 ? 0.34f : 0.22f));
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
