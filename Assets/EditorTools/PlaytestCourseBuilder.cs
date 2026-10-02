using System;
using System.Collections.Generic;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using static HotPatata.Editor.BombObstacleKitBuilder;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Builds <c>PlaytestCourse</c> (MVP_TASKS M10.4): a ~10 minute course along +Z (~820 m, 9 checkpoints) that plays the
    /// classic kit and every #68 bomb obstacle, each taught alone before it is combined. The scene is created once as a
    /// copy of PrototypeCourse (run manager, bomb, lighting, look, spawner and test rig), then <c>SectionRoot/Course</c>,
    /// the backdrop, the kill zone and the finish are rebuilt from scratch on every run (idempotent). Rebuild after
    /// rebuilding the kit prefabs. Beat layout: ARCHITECTURE.md §4.
    /// </summary>
    public static class PlaytestCourseBuilder
    {
        public const string ScenePath = "Assets/Scenes/PlaytestCourse.unity";
        const string SourceScene = "Assets/Scenes/PrototypeCourse.unity";

        const float Window = 2.4f, WindowBottom = 1f, WindowTop = 3f;   // the standard throw window in a wall
        const float Passage = 3f, PassageHeight = 3.5f;                  // a laser-curtain passage for runners (curtain + posts)

        static Material floorMat, wallMat, platMat;

        [MenuItem("HotPatata/Course/Build Playtest Course")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && !AssetDatabase.CopyAsset(SourceScene, ScenePath))
                throw new InvalidOperationException("could not create " + ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath);

            floorMat = Mat("Greybox_Floor");
            wallMat = Mat("Greybox_Wall");
            platMat = Mat("Greybox_Platform");

            var section = GameObject.Find("SectionRoot")?.transform ?? throw new InvalidOperationException("no SectionRoot");
            RebuildGroup(section, "Course", course =>
            {
                RebuildGroup(course, "Act1_WarmUp", BuildAct1);
                RebuildGroup(course, "Act2_HotAndCold", BuildAct2);
                RebuildGroup(course, "Act3_Switchboard", BuildAct3);
                RebuildGroup(course, "Act4_GrandFinale", BuildAct4);
            });

            var kill = section.Find("KillZone");
            kill.position = new Vector3(0f, -24f, 400f);
            kill.localScale = new Vector3(260f, 4f, 920f);   // z -60..860

            var backdrop = GameObject.Find("Backdrop")?.transform;
            if (backdrop != null)
            {
                for (int i = backdrop.childCount - 1; i >= 0; i--) Object.DestroyImmediate(backdrop.GetChild(i).gameObject);
                BuildBackdrop(backdrop, 6868, 46, -60f, 880f, 80, -80f, 900f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PlaytestCourseBuilder] PlaytestCourse built");
        }

        // ------------------------------------------------------------------ Act 1: Warm-up, the classic kit (z -3..196)

        static void BuildAct1(Transform act)
        {
            // --- A Start Court: flat and wide, the pair starts 6 m apart (spawns live on SectionRoot).
            Floor(act, "A_StartCourt", -2f, 30f, 0f);
            Block(act, "A_BackWall", new Vector3(0f, 4f, -2.5f), new Vector3(16f, 8f, 1f), wallMat);

            // --- B First Gap: a 4.5 m pit, broad landing.
            Floor(act, "B_Landing", 34.5f, 50f, 0f, 14f);

            // --- C Stair Relay: two 1.2 m steps, then a 1.4 m mantle.
            Floor(act, "C1", 50f, 58f, 1.2f, 12f);
            Floor(act, "C2", 58f, 66f, 2.4f, 12f);
            Floor(act, "C3", 66f, 74f, 3.8f, 12f);
            Floor(act, "CP1_Platform", 74f, 86f, 3.8f, 14f);
            AddCheckpoint(act, "CP_01", 1, new Vector3(0f, 3.8f, 80f), 0f);

            // --- D Moving Pair: two platforms sliding across a 12 m gap in opposite phase.
            AddMover(act, "Platform_Moving", "D_Moving_1", new Vector3(-4f, 3.55f, 89f), new Vector3(4f, 3.55f, 89f),
                     new Vector3(3f, 0.5f, 3f), speed: 2f, phase: 0f, dwell: 0.25f);
            AddMover(act, "Platform_Moving", "D_Moving_2", new Vector3(4f, 3.55f, 94f), new Vector3(-4f, 3.55f, 94f),
                     new Vector3(3f, 0.5f, 3f), speed: 2f, phase: 0f, dwell: 0.25f);
            Floor(act, "D_End", 98f, 110f, 3.8f, 14f);

            // --- E Conveyor Hall: side belts carry forward, the centre one back; hurdles force lane changes.
            AddConveyor(act, "E_Belt_L", new Vector3(-5.5f, 3.3f, 128f), new Vector3(4f, 1f, 36f), 3f);
            AddConveyor(act, "E_Belt_C", new Vector3(0f, 3.3f, 128f), new Vector3(4f, 1f, 36f), -4f);
            AddConveyor(act, "E_Belt_R", new Vector3(5.5f, 3.3f, 128f), new Vector3(4f, 1f, 36f), 3f);
            Block(act, "E_Hurdle_L", new Vector3(-5.5f, 4.4f, 121f), new Vector3(4f, 1.2f, 0.6f), wallMat);
            Block(act, "E_Hurdle_R", new Vector3(5.5f, 4.4f, 135f), new Vector3(4f, 1.2f, 0.6f), wallMat);
            Floor(act, "E_End", 146f, 158f, 3.8f, 16f);
            AddCheckpoint(act, "CP_02", 2, new Vector3(0f, 3.8f, 152f), 0f);

            // --- F Sweeper Pit: two knee-high lethal sweepers turning opposite ways; jump them while passing.
            Floor(act, "F_Pit", 158f, 188f, 3.8f, 14f);
            AddRotator(act, ObstaclesDir + "Obstacle_Sweeper", "F_Sweeper_1", new Vector3(0f, 3.8f, 165.5f), 13.6f, 90f, 0f);
            AddRotator(act, ObstaclesDir + "Obstacle_Sweeper", "F_Sweeper_2", new Vector3(0f, 3.8f, 180.5f), 13.6f, -90f, 90f);
            Floor(act, "F_End", 188f, 196f, 3.8f, 14f);
        }

        // ------------------------------------------------------------------ Act 2: Hot & Cold, zones and curtains (z 196..374)

        static void BuildAct2(Transform act)
        {
            const float y = 3.8f;

            // --- H Forbidden Strips: the carrier may not cross; walk over, throw the bomb across. 4 m, then 6 m.
            Floor(act, "H_Court", 196f, 232f, y, 16f, platMat);
            Zone(act, ZoneForbidden, "H_Strip_1", new Vector3(0f, y, 206f), new Vector3(16f, 2.5f, 4f));
            Zone(act, ZoneForbidden, "H_Strip_2", new Vector3(0f, y, 223f), new Vector3(16f, 2.5f, 6f));

            // --- I Laser Window: the bomb through the window, the runners through the curtains on both sides.
            Floor(act, "I_Court", 232f, 266f, y, 16f, platMat);
            LaserWall(act, "I_Wall", 243f, y, 7f, windowX: 0f, passages: new[] { -6.5f, 6.5f });
            AddCheckpoint(act, "CP_03", 3, new Vector3(0f, y, 260f), 0f);

            // --- J Hot Corridor: the fuse burns x2 among two sweepers; two cold pockets off the path to breathe.
            Floor(act, "J_Court", 266f, 314f, y, 12f, platMat);
            Zone(act, ZoneHot, "J_HotZone", new Vector3(0f, y, 286f), new Vector3(12f, 3f, 32f));
            AddRotator(act, ObstaclesDir + "Obstacle_Sweeper", "J_Sweeper_1", new Vector3(0f, y, 278f), 11.6f, 100f, 0f);
            AddRotator(act, ObstaclesDir + "Obstacle_Sweeper", "J_Sweeper_2", new Vector3(0f, y, 294f), 11.6f, -100f, 90f);
            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0f ? "L" : "R";
                Block(act, $"J_ColdLedge_{s}", new Vector3(side * 8.5f, y - 1f, 286f), new Vector3(3f, 2f, 3.5f), platMat);
                Zone(act, ZoneCold, $"J_ColdPocket_{s}", new Vector3(side * 8.5f, y, 286f), new Vector3(3f, 2.5f, 3.5f));
            }

            // --- K Laser Slalom: three walls, the window alternates sides; the last window is a spinning hoop.
            Floor(act, "K_Court", 314f, 384f, y, 16f, platMat);
            LaserWall(act, "K_Wall_1", 324f, y, 7f, windowX: -4f, passages: new[] { 5.5f });
            LaserWall(act, "K_Wall_2", 336f, y, 7f, windowX: 4f, passages: new[] { -5.5f });
            LaserWall(act, "K_Wall_3", 348f, y, 7f, windowX: 0f, passages: new[] { -6f, 6f }, hoop: true);
            AddCheckpoint(act, "CP_04", 4, new Vector3(0f, y, 368f), 0f);
        }

        // ------------------------------------------------------------------ Act 3: Switchboard, gates and plates (z 374..540)

        static void BuildAct3(Transform act)
        {
            const float y = 3.8f;

            // --- L Gate Bridge: a pass through the ring extends a bridge over a 12 m gap for 8 s.
            //     (The near ledge is the end of K_Court, z ..384; the bridge hides inside it.)
            var ringL = Gate(act, "L_Gate", new Vector3(-3f, y, 378f), 2f, 8f);
            Actuator(act, Bridge, "L_Bridge", new Vector3(0f, y - 0.3f, 378f), new Vector3(3f, 0.5f, 12f), new Vector3(0f, 0f, 12f), ringL);
            Floor(act, "L_FarLedge", 396f, 434f, y, 16f);

            // --- M The Lock: a plate holds the door open; the doorway is a laser curtain, so the bomb goes through the
            //     window. Then the plate holder climbs over the wall (no carrying up there) while the catcher waits in a
            //     cold pocket.
            const float wallZ = 414f, wallH = 6.5f;
            Wall(act, "M_Wall", wallZ, y, wallH, -11f, 11f, new[]
            {
                new Hole(-4.5f - Window / 2f, -4.5f + Window / 2f, WindowBottom, WindowTop),
                new Hole(0f, DoorSize.x, 0f, DoorSize.y)
            });
            var curtain = Place(act, LaserCurtain, "M_DoorCurtain", new Vector3(DoorSize.x / 2f, y, wallZ), Quaternion.identity);
            ResizeCurtain(curtain, DoorSize.x - 0.6f, DoorSize.y - 0.3f);
            var plate = Place(act, Plate, "M_Plate", new Vector3(-6.5f, y, 404f), Quaternion.identity).GetComponent<PressurePlate>();
            Actuator(act, Door, "M_Door", new Vector3(DoorSize.x / 2f, y + DoorSize.y / 2f, wallZ + 0.75f), DoorSize,
                     new Vector3(0f, DoorSize.y + 0.4f, 0f), plate);
            float[] steps = { 1.3f, 2.6f, 3.9f, 5.2f };
            for (int i = 0; i < steps.Length; i++)
                Block(act, $"M_Stair_{i + 1}", new Vector3(6.5f, y + steps[i] / 2f, 406.5f + i * 2f), new Vector3(3f, steps[i], 2f), wallMat);
            Zone(act, ZoneForbidden, "M_WallTop_NoCarry", new Vector3(6.5f, y + wallH, wallZ), new Vector3(3f, 2f, 1.4f));
            Zone(act, ZoneCold, "M_ColdPocket", new Vector3(-4.5f, y, 419f), new Vector3(4f, 2.5f, 3f));
            AddCheckpoint(act, "CP_05", 5, new Vector3(0f, y, 428f), 0f);

            // --- N Shutter and Crusher: the window's shutter opens on a rhythm; runners slide under a crusher in a tunnel.
            Floor(act, "N_Court", 434f, 470f, y, 16f);
            Wall(act, "N_Wall", 450f, y, 6f, -11f, 11f, new[]
            {
                new Hole(-3f - Window / 2f, -3f + Window / 2f, WindowBottom, WindowTop),
                new Hole(2.5f, 5.5f, 0f, 2.6f)
            });
            AddMover(act, "Obstacle_Piston", "N_Shutter", new Vector3(-3f, y + 2f, 449.35f), new Vector3(-3f, y + 4.6f, 449.35f),
                     new Vector3(2.8f, 2.4f, 0.3f), speed: 1.2f, phase: 0f, dwell: 0.5f);
            AddCrusher(act, "N_Crusher", new Vector3(4f, y, 450f), width: 2.9f, length: 1.2f, phase: 0.25f);

            // --- CP6 arch: throw through the arch across a 4 m gap to a teammate already on the pad.
            ArchCheckpoint(act, "CP_06", 6, new Vector3(0f, y, 480f), 0f, new Vector3(0f, y, 475.2f));
            Floor(act, "O_Base", 474f, 498f, y, 16f);

            // --- O Switch Chain: gate 1 raises a lift up the 5 m cliff, gate 2 extends the bridge beyond it.
            var ring1 = Gate(act, "O_Gate_1", new Vector3(-3f, y, 490f), 2.2f, 12f);
            Actuator(act, Lift, "O_Lift", new Vector3(4f, y + 0.05f, 493.5f), new Vector3(4f, 0.5f, 4f), new Vector3(0f, 5f, 0f), ring1);   // rests on the floor
            const float up = 8.8f;
            Block(act, "O_UpperLedge", new Vector3(0f, (up + 1.8f) / 2f, 503f), new Vector3(16f, up - 1.8f, 14f), floorMat);   // z 496..510
            var ring2 = Gate(act, "O_Gate_2", new Vector3(3f, up, 503f), 2.2f, 8f);
            Actuator(act, Bridge, "O_Bridge", new Vector3(0f, up - 0.3f, 504f), new Vector3(3f, 0.5f, 12f), new Vector3(0f, 0f, 12f), ring2);
            Floor(act, "O_FarLedge", 522f, 540f, up, 16f);
            AddCheckpoint(act, "CP_07", 7, new Vector3(0f, up, 530f), 0f);
        }

        // ------------------------------------------------------------------ Act 4: Grand Finale, tubes and cannon (z 540..822)

        static void BuildAct4(Transform act)
        {
            const float y = 8.8f;

            // --- P Tube Intro: the bomb goes through the tube, the runners through the curtain.
            Floor(act, "P_Near", 540f, 558f, y, 16f, platMat);
            LaserWall(act, "P_Wall", 558f, y, 7f, windowX: null, passages: new[] { 5f });
            Floor(act, "P_Far", 558f, 582f, y, 16f, platMat);
            var intro = Place(act, Tube, "P_Tube", new Vector3(-3f, y + 1.6f, 552f), Quaternion.identity);
            ConfigureTube(intro, new[] { new TubeSlot(new Vector3(-3f, y + 1.6f, 552f), 0f, new Vector3(-3f, y + 4.5f, 562f), new Vector3(-1.5f, y, 571f), 1f) });

            // --- Q Tube Junction: three lanes the bomb cannot cross; three mouths, one per lane, each landing on its lane's pad.
            Floor(act, "Q_Split", 582f, 594f, y, 18f, platMat);
            var junction = Place(act, Tube, "Q_Tube", new Vector3(0f, y + 1.6f, 591f), Quaternion.identity);
            var slots = new List<TubeSlot>();
            float[] lanes = { -6f, 0f, 6f };
            foreach (float x in lanes)
                slots.Add(new TubeSlot(new Vector3(x, y + 1.6f, 591f), 0f, new Vector3(x, y + 7f, 597f), new Vector3(x, y, 621f), 1.6f));
            ConfigureTube(junction, slots);
            foreach (float x in new[] { -9.3f, -3f, 3f, 9.3f })
                Block(act, $"Q_LaneWall_{x:+0;-0}", new Vector3(x, y + 3f, 609f), new Vector3(0.6f, 6f, 30f), wallMat);   // z 594..624
            // left lane: falling platforms over a pit
            Floor(act, "Q_Left_In", 594f, 600f, y, 5.7f, platMat, -6f);
            Place(act, PlatformsDir + "Platform_Falling", "Q_Left_Falling_1", new Vector3(-6f, y - 0.25f, 603.5f), Quaternion.identity);
            Place(act, PlatformsDir + "Platform_Falling", "Q_Left_Falling_2", new Vector3(-6f, y - 0.25f, 610.5f), Quaternion.identity);
            Floor(act, "Q_Left_Out", 614f, 624f, y, 5.7f, platMat, -6f);
            // centre lane: a sweeper
            Floor(act, "Q_Centre", 594f, 624f, y, 5.4f, platMat);
            AddRotator(act, ObstaclesDir + "Obstacle_Sweeper", "Q_Centre_Sweeper", new Vector3(0f, y, 609f), 5.2f, 100f, 0f);
            // right lane: a belt running backwards
            Floor(act, "Q_Right_In", 594f, 598f, y, 5.7f, platMat, 6f);
            AddConveyor(act, "Q_Right_Belt", new Vector3(6f, y - 0.5f, 609f), new Vector3(5.7f, 1f, 22f), -3.5f);
            Floor(act, "Q_Right_Out", 620f, 624f, y, 5.7f, platMat, 6f);
            Floor(act, "Q_Merge", 624f, 654f, y, 18f, platMat);
            AddCheckpoint(act, "CP_08", 8, new Vector3(0f, y, 632f), 5f);

            // --- R Cannon Canyon: the cannon fires the bomb 44 m over the chasm onto the far pad while the team rides
            //     two shuttles; the carrier can wait in the cold pocket beside the cannon.
            var cannon = Place(act, Cannon, "R_Cannon", new Vector3(-4f, y, 648f), Quaternion.identity);
            ConfigureCannon(cannon, new Vector3(0f, y, 692f), 2.2f);
            Zone(act, ZoneCold, "R_ColdPocket", new Vector3(-6.8f, y, 648f), new Vector3(2.4f, 2.5f, 4f));
            AddMover(act, "Platform_Moving", "R_Shuttle_1", new Vector3(0f, y - 0.25f, 656.5f), new Vector3(0f, y - 0.25f, 667f),
                     new Vector3(4f, 0.5f, 4f), speed: 3.5f, phase: 0f, dwell: 0.35f);
            AddMover(act, "Platform_Moving", "R_Shuttle_2", new Vector3(0f, y - 0.25f, 671f), new Vector3(0f, y - 0.25f, 681.5f),
                     new Vector3(4f, 0.5f, 4f), speed: 3.5f, phase: 0.5f, dwell: 0.35f);
            Floor(act, "R_Far", 684f, 702f, y, 16f, platMat);

            // --- S Mega Slide: 12 m down at 18 degrees, two lanes, hoops over the divider, a hot zone on the lower half.
            const float slope = 18f, bottomY = -3.2f, startZ = 702f;
            float run = (y - bottomY) / Mathf.Tan(slope * Mathf.Deg2Rad);   // ~36.9 m
            float length = run / Mathf.Cos(slope * Mathf.Deg2Rad);
            var rot = Quaternion.Euler(slope, 0f, 0f);
            Vector3 upAxis = rot * Vector3.up, fwd = rot * Vector3.forward;
            Vector3 top0 = new Vector3(0f, y, startZ);
            Vector3 OnSlope(float x, float along, float height) => top0 + Vector3.right * x + fwd * along + upAxis * height;
            var slideMat = Mat("Greybox_Slide");
            Block(act, "S_Lane_L", OnSlope(-2.5f, length / 2f, -0.5f), new Vector3(4f, 1f, length), slideMat, rot);
            Block(act, "S_Lane_R", OnSlope(2.5f, length / 2f, -0.5f), new Vector3(4f, 1f, length), slideMat, rot);
            Block(act, "S_Divider", OnSlope(0f, length / 2f, -0.2f), new Vector3(1f, 1.6f, length), wallMat, rot);
            Block(act, "S_Rail_L", OnSlope(-4.75f, length / 2f, 0f), new Vector3(0.5f, 2.4f, length), wallMat, rot);
            Block(act, "S_Rail_R", OnSlope(4.75f, length / 2f, 0f), new Vector3(0.5f, 2.4f, length), wallMat, rot);
            float[] hoops = { 10f, 24f };
            for (int i = 0; i < hoops.Length; i++)
                AddRotator(act, ObstaclesDir + "Obstacle_Hoop", $"S_Hoop_{i + 1}", OnSlope(0f, hoops[i], 2.6f), 0f, i % 2 == 0 ? 70f : -70f, i * 40f);
            var hot = Place(act, ZoneHot, "S_HotZone", OnSlope(0f, length * 0.72f, 0f), rot);
            ResizeZone(hot, new Vector3(9f, 3f, length * 0.5f));

            // --- T Finale: run-out, a forward belt through two piston gates, the CP9 arch, then a last gate opens the
            //     podium door.
            float endZ = startZ + run;
            Floor(act, "T_Runout", endZ, 762f, bottomY, 12f, platMat);
            AddConveyor(act, "T_Belt", new Vector3(0f, bottomY - 0.5f, 774f), new Vector3(8f, 1f, 24f), 4f);   // z 762..786
            for (int i = 0; i < 2; i++)
            {
                float z = 770f + i * 9f;
                AddMover(act, "Obstacle_Piston", $"T_Gate_{i + 1}", new Vector3(0f, bottomY + 1.75f, z), new Vector3(0f, bottomY - 1.95f, z),
                         new Vector3(8f, 3.5f, 1f), speed: 2.5f, phase: i / 2f, dwell: 0.35f);
            }
            Floor(act, "T_Podium", 790f, 822f, bottomY, 16f, platMat);
            ArchCheckpoint(act, "CP_09", 9, new Vector3(0f, bottomY, 796f), 5f, new Vector3(0f, bottomY, 791.2f));
            var ringT = Gate(act, "T_Gate", new Vector3(3f, bottomY, 801f), 2.2f, 10f);
            Wall(act, "T_PodiumWall", 808f, bottomY, 6f, -8f, 8f, new[] { new Hole(-DoorSize.x / 2f, DoorSize.x / 2f, 0f, DoorSize.y) });
            Actuator(act, Door, "T_Door", new Vector3(0f, bottomY + DoorSize.y / 2f, 808.75f), DoorSize, new Vector3(0f, DoorSize.y + 0.4f, 0f), ringT);
            Place(act, GameplayDir + "FinishZone", "FinishZone", new Vector3(0f, bottomY, 815f), Quaternion.identity);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A floor slab 2 m thick whose top is at <paramref name="top"/>, from z0 to z1.</summary>
        static GameObject Floor(Transform parent, string name, float z0, float z1, float top, float width = 16f, Material mat = null, float x = 0f) =>
            Block(parent, name, new Vector3(x, top - 1f, (z0 + z1) / 2f), new Vector3(width, 2f, z1 - z0), mat ?? floorMat);

        static GameObject Zone(Transform parent, string prefab, string name, Vector3 floorCenter, Vector3 size)
        {
            var go = Place(parent, prefab, name, floorCenter, Quaternion.identity);
            ResizeZone(go, size);
            return go;
        }

        /// <summary>A bomb-gate ring centred at <paramref name="height"/> above <paramref name="floor"/>, on a thin pillar.</summary>
        static BombGate Gate(Transform parent, string name, Vector3 floor, float height, float holdSeconds)
        {
            var gate = Place(parent, GateRing, name, floor + Vector3.up * height, Quaternion.identity).GetComponent<BombGate>();
            SetField(gate, "holdSeconds", p => p.floatValue = holdSeconds);
            float pillar = height - 1.45f;   // up to the underside of the ring
            if (pillar > 0.1f)
                Block(parent, name + "_Pillar", floor + Vector3.up * (pillar / 2f), new Vector3(0.35f, pillar, 0.35f), Mat("Greybox_Gate"));
            return gate;
        }

        static GameObject Actuator(Transform parent, string prefab, string name, Vector3 closedCenter, Vector3 size, Vector3 travel, MonoBehaviour source)
        {
            var go = Place(parent, prefab, name, closedCenter, Quaternion.identity);
            ResizeActuator(go, size, travel);
            Wire(go, source);
            return go;
        }

        static void ArchCheckpoint(Transform parent, string name, int id, Vector3 pad, float fuse, Vector3 archFloor)
        {
            var cp = AddCheckpoint(parent, name, id, pad, fuse).GetComponent<Checkpoint>();
            var arch = Place(parent, GateArch, name + "_Arch", archFloor, Quaternion.identity).GetComponent<BombGate>();
            SetReference(cp, "claimGate", arch);
        }

        /// <summary>An opening in a wall, in wall space: x across the course, y above the wall's floor.</summary>
        struct Hole
        {
            public float X0, X1, Y0, Y1;

            public Hole(float x0, float x1, float y0, float y1)
            {
                X0 = x0;
                X1 = x1;
                Y0 = y0;
                Y1 = y1;
            }
        }

        /// <summary>A 1 m thick wall across the course at <paramref name="z"/>, from x0 to x1, with rectangular holes.</summary>
        static void Wall(Transform parent, string name, float z, float floorY, float height, float x0, float x1, IList<Hole> holes)
        {
            var xs = new List<float> { x0, x1 };
            foreach (var h in holes)
            {
                xs.Add(h.X0);
                xs.Add(h.X1);
            }
            xs.Sort();
            int piece = 0;
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                float a = xs[i], b = xs[i + 1];
                if (b - a < 0.01f) continue;
                float mid = (a + b) / 2f;
                Hole? hole = null;
                foreach (var h in holes)
                    if (mid > h.X0 && mid < h.X1) hole = h;
                void Piece(float y0, float y1)
                {
                    if (y1 - y0 < 0.01f) return;
                    Block(parent, $"{name}_{++piece}", new Vector3(mid, floorY + (y0 + y1) / 2f, z), new Vector3(b - a, y1 - y0, 1f), wallMat);
                }
                if (hole == null) Piece(0f, height);
                else
                {
                    Piece(0f, hole.Value.Y0);
                    Piece(hole.Value.Y1, height);
                }
            }
        }

        /// <summary>
        /// A wall with a throw window (or a spinning hoop in a bigger opening) and laser-curtain passages for the runners
        /// (PROJECT_SPEC §13.13). It reaches 3 m past the 16 m court on each side so going round is no easier than the window.
        /// </summary>
        static void LaserWall(Transform parent, string name, float z, float floorY, float height, float? windowX, float[] passages, bool hoop = false)
        {
            var holes = new List<Hole>();
            if (windowX.HasValue)
            {
                float w = hoop ? 3.6f : Window;
                holes.Add(hoop ? new Hole(windowX.Value - w / 2f, windowX.Value + w / 2f, 0.6f, 4.2f)
                               : new Hole(windowX.Value - w / 2f, windowX.Value + w / 2f, WindowBottom, WindowTop));
            }
            foreach (float x in passages) holes.Add(new Hole(x - Passage / 2f, x + Passage / 2f, 0f, PassageHeight));
            Wall(parent, name, z, floorY, height, -11f, 11f, holes);

            for (int i = 0; i < passages.Length; i++)
            {
                var curtain = Place(parent, LaserCurtain, $"{name}_Curtain_{i + 1}", new Vector3(passages[i], floorY, z), Quaternion.identity);
                ResizeCurtain(curtain, Passage - 0.6f, PassageHeight - 0.3f);
            }
            if (hoop && windowX.HasValue)
                AddRotator(parent, ObstaclesDir + "Obstacle_Hoop", name + "_Hoop", new Vector3(windowX.Value, floorY + 2.4f, z), 0f, 70f, 90f);
        }
    }
}
