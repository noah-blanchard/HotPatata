using System;
using System.Collections.Generic;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using static HotPatata.Editor.BombObstacleKitBuilder;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// Builds <c>PatataPark</c>: a ~680 m course along +Z (9 checkpoints, 4 acts) laid out on the KayKit grid (floors in
    /// whole metres, mostly 4 and 6 m tiles), playing the classic kit and the #68 bomb obstacles in new combinations. The
    /// scene is created once as a copy of PlaytestCourse (run manager, bomb, lighting, look, spawner, test rig, start spawns
    /// on <c>SectionRoot</c>); every build rebuilds <c>SectionRoot/Course</c>, the KayKit backdrop and the kill zone
    /// (idempotent). Rebuild after rebuilding the kit. Beat layout: ARCHITECTURE.md §4.
    /// </summary>
    public static class PatataParkBuilder
    {
        public const string ScenePath = "Assets/Scenes/PatataPark.unity";
        const string SourceScene = "Assets/Scenes/PlaytestCourse.unity";

        [MenuItem("HotPatata/Course/Build Patata Park")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && !AssetDatabase.CopyAsset(SourceScene, ScenePath))
                throw new InvalidOperationException("could not create " + ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath);

            var section = GameObject.Find("SectionRoot")?.transform ?? throw new InvalidOperationException("no SectionRoot");
            RebuildGroup(section, "Course", course =>
            {
                RebuildGroup(course, "Act1_BlockHop", BuildAct1);
                RebuildGroup(course, "Act2_HotAndCold", BuildAct2);
                RebuildGroup(course, "Act3_Switchboard", BuildAct3);
                RebuildGroup(course, "Act4_SkyFinale", BuildAct4);
            });

            var kill = section.Find("KillZone");
            kill.position = new Vector3(0f, -16f, 330f);
            kill.localScale = new Vector3(280f, 4f, 780f);   // z -60..720

            var backdrop = GameObject.Find("Backdrop")?.transform;
            if (backdrop != null)
            {
                for (int i = backdrop.childCount - 1; i >= 0; i--) Object.DestroyImmediate(backdrop.GetChild(i).gameObject);
                BuildKayKitBackdrop(backdrop, 2468, 40, -60f, 720f);
                BuildBackdrop(backdrop, 1357, 0, 0f, 0f, 70, -80f, 740f);   // clouds only
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Register();
            Debug.Log("[PatataParkBuilder] PatataPark built");
        }

        const string NetworkManagerPrefab = "Assets/Prefabs/Network/NetworkManager.prefab";
        const int Checkpoints = 9;

        /// <summary>In the build, and first in the Bootstrap level list (<c>NetworkBootstrap.gameplayScenes</c>) with its 9 checkpoints.</summary>
        static void Register()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.SaveAssets();
            }

            string level = System.IO.Path.GetFileNameWithoutExtension(ScenePath);
            var root = PrefabUtility.LoadPrefabContents(NetworkManagerPrefab);
            try
            {
                var so = new SerializedObject(root.GetComponent<NetworkBootstrap>());
                var names = so.FindProperty("gameplayScenes");
                var counts = so.FindProperty("sceneCheckpoints");
                for (int i = 0; i < names.arraySize; i++)
                    if (names.GetArrayElementAtIndex(i).stringValue == level) return;
                names.InsertArrayElementAtIndex(0);
                names.GetArrayElementAtIndex(0).stringValue = level;
                counts.InsertArrayElementAtIndex(0);
                counts.GetArrayElementAtIndex(0).intValue = Checkpoints;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, NetworkManagerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ Act 1: Block Hop, the classic kit (floor 0 -> 6, z -6..199)

        static void BuildAct1(Transform act)
        {
            // --- A Start: a wide court, the pair starts 6 m apart (spawns live on SectionRoot).
            Island(act, "A_Start", -6f, 18f, 0f, 16f);
            Block(act, "A_BackWall", new Vector3(0f, 2f, -5.5f), new Vector3(16f, 4f, 1f), KitRole.Wall);

            // --- B Island Hop: three 6 m islands zig-zag up one metre each; teammates land on different islands, so pass.
            Island(act, "B_Hop_1", 21f, 27f, 0f, 6f, -3f);
            Island(act, "B_Hop_2", 29f, 35f, 1f, 6f, 3f);
            Island(act, "B_Hop_3", 37f, 43f, 2f, 6f, -3f);
            Island(act, "B_Landing", 46f, 58f, 2f);

            // --- C Ramp: 4 m up over 12 m (18°) to the first plateau.
            Ramp(act, "C_Ramp", new Vector3(0f, 2f, 58f), new Vector3(0f, 6f, 70f), 8f, KitRole.Ground);
            Island(act, "C_Plateau", 70f, 82f, 6f, 16f);
            AddCheckpoint(act, "CP_01", 1, new Vector3(0f, 6f, 76f), 0f);

            // --- D Falling Run: four falling platforms zig-zag over a 24 m pit; whoever lingers drops.
            float[] fallZ = { 85f, 91f, 97f, 103f };
            for (int i = 0; i < fallZ.Length; i++)
                Place(act, PlatformsDir + "Platform_Falling", $"D_Falling_{i + 1}", new Vector3(i % 2 == 0 ? -2f : 2f, 5.75f, fallZ[i]), Quaternion.identity);
            Island(act, "D_End", 106f, 118f, 6f);

            // --- E Piston Wave: five floor tiles rise and fall 3 m in a wave; a raised tile blocks the pass along the corridor.
            for (int i = 0; i < 5; i++)
            {
                float z = 121f + i * 7.5f;   // 6 m tiles, 1.5 m gaps: z 118..154
                AddMover(act, "Obstacle_Piston", $"E_Piston_{i + 1}", new Vector3(0f, 5.5f, z), new Vector3(0f, 8.5f, z),
                         new Vector3(8f, 1f, 6f), speed: 1.2f, phase: i * 0.2f, dwell: 0.4f);
            }
            Island(act, "E_End", 155f, 167f, 6f, 16f);
            AddCheckpoint(act, "CP_02", 2, new Vector3(0f, 6f, 161f), 0f);

            // --- F Windmill Wall: a sweeper, then an 8 m wall; the bomb goes through the windmill's hole, the runners through
            //     a low tunnel under a crusher (slide or crouch).
            Island(act, "F_Court", 167f, 199f, 6f, 16f);
            AddRotator(act, ObstaclesDir + "Obstacle_Sweeper", "F_Sweeper", new Vector3(0f, 6f, 174f), 15.6f, 90f, 0f);
            Wall(act, "F_Wall", 184f, 6f, 8f, -8f, 8f, new[]
            {
                new Hole(-4.8f, -1.2f, 2.4f, 6f),   // the windmill's hole, centred 4.2 m up
                new Hole(3f, 6f, 0f, 2.6f)          // the tunnel
            });
            Place(act, ObstaclesDir + "Obstacle_Windmill", "F_Windmill", new Vector3(-3f, 10.2f, 183.25f), Quaternion.identity);
            AddCrusher(act, "F_Crusher", new Vector3(4.5f, 6f, 184f), width: 2.9f, length: 1.2f, phase: 0.25f);
            AddCheckpoint(act, "CP_03", 3, new Vector3(0f, 6f, 194f), 0f);
        }

        // ------------------------------------------------------------------ Act 2: Hot & Cold, zones and curtains (z 199..319)

        static void BuildAct2(Transform act)
        {
            const float y = 6f;

            // --- G Forbidden Bridges: two bridges over a pit, each with a no-carry strip, staggered: the bomb crosses the
            //     6 m gap between the bridges twice.
            Island(act, "G_Bridge_L", 199f, 231f, y, 4f, -5f);
            Island(act, "G_Bridge_R", 199f, 231f, y, 4f, 5f);
            Zone(act, ZoneForbidden, "G_Strip_L", new Vector3(-5f, y, 206f), new Vector3(4f, 2.5f, 6f));
            Zone(act, ZoneForbidden, "G_Strip_R", new Vector3(5f, y, 222f), new Vector3(4f, 2.5f, 6f));
            Island(act, "G_End", 231f, 243f, y, 16f);

            // --- H Laser Slalom: three walls, the window alternates sides; the last window is a spinning hoop.
            Island(act, "H_Court", 243f, 291f, y, 16f);
            LaserWall(act, "H_Wall_1", 253f, y, 7f, windowX: -4f, passages: new[] { 5.5f });
            LaserWall(act, "H_Wall_2", 267f, y, 7f, windowX: 4f, passages: new[] { -5.5f });
            LaserWall(act, "H_Wall_3", 281f, y, 7f, windowX: 0f, passages: new[] { -6f, 6f }, hoop: true);
            AddCheckpoint(act, "CP_04", 4, new Vector3(0f, y, 287f), 0f);

            // --- I Hot Climb: three 1 m steps under a hot zone (fuse x2): pass ahead to whoever reached the top; a cold
            //     pocket waits up there, then a sweeper.
            for (int i = 0; i < 3; i++)
            {
                float top = y + 1f + i;
                Block(act, $"I_Step_{i + 1}", new Vector3(0f, (top + 4f) / 2f, 293f + i * 4f), new Vector3(12f, top - 4f, 4f), KitRole.Ground);
            }
            Zone(act, ZoneHot, "I_HotZone", new Vector3(0f, y, 297f), new Vector3(12f, 6f, 12f));
            Island(act, "I_Top", 303f, 319f, 10f);
            Zone(act, ZoneCold, "I_ColdPocket", new Vector3(-4f, 10f, 305f), new Vector3(4f, 2.5f, 4f));
            AddRotator(act, ObstaclesDir + "Obstacle_Sweeper", "I_Sweeper", new Vector3(0f, 10f, 311f), 11.6f, -100f, 0f);
        }

        // ------------------------------------------------------------------ Act 3: Switchboard, gates and plates (z 315..437)

        static void BuildAct3(Transform act)
        {
            const float y = 10f;

            // --- J Ring Bridge: a pass through the ring extends a bridge over a 12 m gap for 8 s (it hides in I_Top).
            var ringJ = Gate(act, "J_Gate", new Vector3(3f, y, 315f), 2f, 8f);
            Actuator(act, Bridge, "J_Bridge", new Vector3(0f, y - 0.55f, 313f), new Vector3(4f, 1f, 12f), new Vector3(0f, 0f, 12f), ringJ);
            Island(act, "J_Far", 331f, 367f, y, 16f);

            // --- K The Lock: a plate holds the door open, the doorway is a curtain, so the bomb takes the window; the plate
            //     holder climbs the 1 m stairs and mantles over the wall (no carrying on top) while the catcher waits in a
            //     cold pocket.
            const float wallZ = 351f, wallH = 6f;
            Wall(act, "K_Wall", wallZ, y, wallH, -11f, 11f, new[]
            {
                new Hole(-4.5f - Window / 2f, -4.5f + Window / 2f, WindowBottom, WindowTop),
                new Hole(0f, DoorSize.x, 0f, DoorSize.y)
            });
            var curtain = Place(act, LaserCurtain, "K_DoorCurtain", new Vector3(DoorSize.x / 2f, y, wallZ), Quaternion.identity);
            ResizeCurtain(curtain, DoorSize.x - 0.6f, DoorSize.y - 0.3f);
            var plate = Place(act, Plate, "K_Plate", new Vector3(-6.5f, y, 341f), Quaternion.identity).GetComponent<PressurePlate>();
            Actuator(act, Door, "K_Door", new Vector3(DoorSize.x / 2f, y + DoorSize.y / 2f, wallZ + 0.75f), DoorSize,
                     new Vector3(0f, DoorSize.y + 0.4f, 0f), plate);
            for (int i = 0; i < 5; i++)
            {
                float h = i + 1f;
                Block(act, $"K_Stair_{i + 1}", new Vector3(6.5f, y + h / 2f, 341.5f + i * 2f), new Vector3(3f, h, 2f), KitRole.Wall);
            }
            Zone(act, ZoneForbidden, "K_WallTop_NoCarry", new Vector3(6.5f, y + wallH, wallZ), new Vector3(3f, 2f, 1.4f));
            Zone(act, ZoneCold, "K_ColdPocket", new Vector3(-4.5f, y, 356f), new Vector3(4f, 2.5f, 3f));
            AddCheckpoint(act, "CP_05", 5, new Vector3(0f, y, 362f), 0f);

            // --- L Arch: throw through the arch across a 4 m gap to a teammate already on the pad.
            Island(act, "L_Base", 371f, 395f, y, 16f);
            ArchCheckpoint(act, "CP_06", 6, new Vector3(0f, y, 377f), 0f, new Vector3(0f, y, 372.2f));

            // --- M Lift Chain: ring 1 raises a lift up the 6 m cliff (12 s), ring 2 extends the bridge beyond it (8 s).
            var ring1 = Gate(act, "M_Gate_1", new Vector3(-3f, y, 387f), 2.2f, 12f);
            Actuator(act, Lift, "M_Lift", new Vector3(4f, y + 0.05f, 390.5f), new Vector3(4f, 0.5f, 4f), new Vector3(0f, 6f, 0f), ring1);   // rests on the floor
            const float up = 16f;
            Block(act, "M_Cliff", new Vector3(0f, (up + 4f) / 2f, 400f), new Vector3(16f, up - 4f, 14f), KitRole.Ground);   // z 393..407
            var ring2 = Gate(act, "M_Gate_2", new Vector3(3f, up, 400f), 2.2f, 8f);
            Actuator(act, Bridge, "M_Bridge", new Vector3(0f, up - 0.55f, 401f), new Vector3(4f, 1f, 12f), new Vector3(0f, 0f, 12f), ring2);
            Island(act, "M_Far", 419f, 437f, up, 16f);
            AddCheckpoint(act, "CP_07", 7, new Vector3(0f, up, 427f), 0f);
        }

        // ------------------------------------------------------------------ Act 4: Sky Finale, tubes, cannon, slide (z 437..674)

        static void BuildAct4(Transform act)
        {
            const float y = 16f;

            // --- N Tube Junction: three lanes the bomb cannot cross; one mouth per lane lands on that lane's pad.
            Island(act, "N_Split", 437f, 449f, y, 18f);
            var tube = Place(act, Tube, "N_Tube", new Vector3(0f, y + 1.6f, 446f), Quaternion.identity);
            var slots = new List<TubeSlot>();
            foreach (float x in new[] { -6f, 0f, 6f })
                slots.Add(new TubeSlot(new Vector3(x, y + 1.6f, 446f), 0f, new Vector3(x, y + 7f, 452f), new Vector3(x, y, 476f), 1.6f));
            ConfigureTube(tube, slots);
            foreach (float x in new[] { -9.3f, -3f, 3f, 9.3f })
                Block(act, $"N_LaneWall_{x:+0;-0}", new Vector3(x, y + 3f, 464f), new Vector3(0.6f, 6f, 30f), KitRole.Wall);   // z 449..479
            // left lane: falling platforms over a pit
            Island(act, "N_Left_In", 449f, 455f, y, 5.7f, -6f);
            Place(act, PlatformsDir + "Platform_Falling", "N_Left_Falling_1", new Vector3(-6f, y - 0.25f, 458.5f), Quaternion.identity);
            Place(act, PlatformsDir + "Platform_Falling", "N_Left_Falling_2", new Vector3(-6f, y - 0.25f, 465.5f), Quaternion.identity);
            Island(act, "N_Left_Out", 469f, 479f, y, 5.7f, -6f);
            // centre lane: a sweeper
            Island(act, "N_Centre", 449f, 479f, y, 5.4f);
            AddRotator(act, ObstaclesDir + "Obstacle_Sweeper", "N_Centre_Sweeper", new Vector3(0f, y, 464f), 5.2f, 100f, 0f);
            // right lane: a belt running backwards
            Island(act, "N_Right_In", 449f, 453f, y, 5.7f, 6f);
            AddConveyor(act, "N_Right_Belt", new Vector3(6f, y - 0.5f, 464f), new Vector3(5.7f, 1f, 22f), -3.5f);
            Island(act, "N_Right_Out", 475f, 479f, y, 5.7f, 6f);
            Island(act, "N_Merge", 479f, 509f, y, 18f);
            AddCheckpoint(act, "CP_08", 8, new Vector3(0f, y, 487f), 5f);

            // --- O Cannon Canyon: the cannon fires the bomb 44 m onto the far pad while the team rides two shuttles; the
            //     carrier can wait in the cold pocket beside the cannon.
            var cannon = Place(act, Cannon, "O_Cannon", new Vector3(-4f, y, 503f), Quaternion.identity);
            ConfigureCannon(cannon, new Vector3(0f, y, 547f), 2.2f);
            Zone(act, ZoneCold, "O_ColdPocket", new Vector3(-6.8f, y, 503f), new Vector3(2.4f, 2.5f, 4f));
            AddMover(act, "Platform_Moving", "O_Shuttle_1", new Vector3(0f, y - 0.25f, 511.5f), new Vector3(0f, y - 0.25f, 522f),
                     new Vector3(4f, 0.5f, 4f), speed: 3.5f, phase: 0f, dwell: 0.35f);
            AddMover(act, "Platform_Moving", "O_Shuttle_2", new Vector3(0f, y - 0.25f, 526f), new Vector3(0f, y - 0.25f, 536.5f),
                     new Vector3(4f, 0.5f, 4f), speed: 3.5f, phase: 0.5f, dwell: 0.35f);
            Island(act, "O_Far", 539f, 557f, y, 16f);

            // --- P Sky Slide: 12 m down at 18°, two lanes, hoops over the divider, a hot zone on the lower half.
            const float slope = 18f, bottomY = 4f, startZ = 557f;
            float run = (y - bottomY) / Mathf.Tan(slope * Mathf.Deg2Rad);   // ~36.9 m
            float length = run / Mathf.Cos(slope * Mathf.Deg2Rad);
            var rot = Quaternion.Euler(slope, 0f, 0f);
            Vector3 upAxis = rot * Vector3.up, fwd = rot * Vector3.forward;
            Vector3 top0 = new Vector3(0f, y, startZ);
            Vector3 OnSlope(float x, float along, float height) => top0 + Vector3.right * x + fwd * along + upAxis * height;
            Block(act, "P_Lane_L", OnSlope(-2.5f, length / 2f, -0.5f), new Vector3(4f, 1f, length), KitRole.Slide, rot, flip: SlideFlip);
            Block(act, "P_Lane_R", OnSlope(2.5f, length / 2f, -0.5f), new Vector3(4f, 1f, length), KitRole.Slide, rot, flip: SlideFlip);
            Block(act, "P_Divider", OnSlope(0f, length / 2f, -0.2f), new Vector3(1f, 1.6f, length), KitRole.Wall, rot);
            Block(act, "P_Rail_L", OnSlope(-4.75f, length / 2f, 0f), new Vector3(0.5f, 2.4f, length), KitRole.Wall, rot);
            Block(act, "P_Rail_R", OnSlope(4.75f, length / 2f, 0f), new Vector3(0.5f, 2.4f, length), KitRole.Wall, rot);
            float[] hoops = { 10f, 24f };
            for (int i = 0; i < hoops.Length; i++)
                AddRotator(act, ObstaclesDir + "Obstacle_Hoop", $"P_Hoop_{i + 1}", OnSlope(0f, hoops[i], 2.6f), 0f, i % 2 == 0 ? 70f : -70f, i * 40f);
            var hot = Place(act, ZoneHot, "P_HotZone", OnSlope(0f, length * 0.72f, 0f), rot);
            ResizeZone(hot, new Vector3(9f, 3f, length * 0.5f));

            // --- Q Finale: run-out, a forward belt through two piston gates, the CP9 arch, then a last ring opens the podium door.
            float endZ = startZ + run;
            Island(act, "Q_Runout", endZ, 614f, bottomY);
            AddConveyor(act, "Q_Belt", new Vector3(0f, bottomY - 0.5f, 626f), new Vector3(8f, 1f, 24f), 4f);   // z 614..638
            for (int i = 0; i < 2; i++)
            {
                float z = 622f + i * 9f;
                AddMover(act, "Obstacle_Piston", $"Q_Gate_{i + 1}", new Vector3(0f, bottomY + 1.75f, z), new Vector3(0f, bottomY - 1.95f, z),
                         new Vector3(8f, 3.5f, 1f), speed: 2.5f, phase: i / 2f, dwell: 0.35f);
            }
            Island(act, "Q_Podium", 642f, 674f, bottomY, 16f);
            ArchCheckpoint(act, "CP_09", 9, new Vector3(0f, bottomY, 648f), 5f, new Vector3(0f, bottomY, 643.2f));
            var ringQ = Gate(act, "Q_Gate", new Vector3(3f, bottomY, 653f), 2.2f, 10f);
            Wall(act, "Q_PodiumWall", 660f, bottomY, 6f, -8f, 8f, new[] { new Hole(-DoorSize.x / 2f, DoorSize.x / 2f, 0f, DoorSize.y) });
            Actuator(act, Door, "Q_Door", new Vector3(0f, bottomY + DoorSize.y / 2f, 660.75f), DoorSize, new Vector3(0f, DoorSize.y + 0.4f, 0f), ringQ);
            Place(act, GameplayDir + "FinishZone", "FinishZone", new Vector3(0f, bottomY, 667f), Quaternion.identity);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A floating island <paramref name="depth"/> thick whose top is at <paramref name="top"/>, from z0 to z1.</summary>
        static GameObject Island(Transform parent, string name, float z0, float z1, float top, float width = 12f, float x = 0f, float depth = 2f) =>
            Block(parent, name, new Vector3(x, top - depth / 2f, (z0 + z1) / 2f), new Vector3(width, depth, z1 - z0), KitRole.Ground);

        /// <summary>A 1 m thick ramp whose walking surface runs straight from <paramref name="bottom"/> to <paramref name="top"/> (centre line).</summary>
        static GameObject Ramp(Transform parent, string name, Vector3 bottom, Vector3 top, float width, KitRole role)
        {
            Vector3 along = top - bottom;
            var rot = Quaternion.LookRotation(along.normalized, Vector3.up);
            Vector3 center = (bottom + top) / 2f - rot * Vector3.up * 0.5f;
            return Block(parent, name, center, new Vector3(width, 1f, along.magnitude), role, rot);
        }

        // ------------------------------------------------------------------ KayKit backdrop

        static readonly string[] Bases = { "platform_6x6x4", "platform_6x6x2", "platform_4x4x4" };
        static readonly string[] Props = { "flag_A", "flag_B", "flag_C", "cone", "ball", "star", "heart", "diamond", "signage_arrow_stand", "spring_pad", "pipe_end" };
        static readonly KitColor[] BackdropColors = { KitColor.Green, KitColor.Blue, KitColor.Yellow, KitColor.Red };

        /// <summary>
        /// Floating KayKit islands on both sides of the course: a stack of platform pieces on a grey pillar, with props on
        /// top. Collider-free, shadow-free and at |x| >= 34 m so they never enter a pass path (ARCHITECTURE §4). Deterministic.
        /// </summary>
        internal static void BuildKayKitBackdrop(Transform parent, int seed, int islands, float zMin, float zMax)
        {
            var rng = new Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var mat = Mat(KitMaterial);
            for (int i = 0; i < islands; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var root = new GameObject($"KayKitIsland_{i}").transform;
                root.SetParent(parent, false);
                root.SetPositionAndRotation(new Vector3(side * R(34f, 120f), R(-20f, 30f), R(zMin, zMax)), Quaternion.Euler(0f, rng.Next(4) * 90f, 0f));
                root.localScale = Vector3.one * R(1.6f, 2.6f);
                var color = BackdropColors[rng.Next(BackdropColors.Length)];
                Prop(root, Bases[rng.Next(Bases.Length)], color, new Vector3(0f, -4f, 0f), 0f, mat);
                if (rng.NextDouble() < 0.6)
                    Prop(root, "platform_2x2x2", BackdropColors[rng.Next(BackdropColors.Length)], new Vector3(R(-1.5f, 1.5f), 0f, R(-1.5f, 1.5f)), 0f, mat);
                Prop(root, rng.NextDouble() < 0.5 ? "pillar_2x2x8" : "pillar_2x2x4", KitColor.Neutral, new Vector3(0f, -12f, 0f), 0f, mat);
                int props = rng.Next(1, 4);
                for (int k = 0; k < props; k++)
                    Prop(root, Props[rng.Next(Props.Length)], color, new Vector3(R(-2.2f, 2.2f), 0f, R(-2.2f, 2.2f)), rng.Next(4) * 90f, mat);
            }
        }

        internal static void Prop(Transform parent, string model, KitColor color, Vector3 localPos, float yaw, Material mat)
        {
            var source = KayKitKitBuilder.Model(model, KayKitKitBuilder.HasColor(model, color) ? color : KitColor.Neutral);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++) mats[m] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);   // decoration only
        }
    }
}
