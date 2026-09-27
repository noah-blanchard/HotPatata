using System;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// Builds the Factory / Slide obstacle prefabs and PrototypeCourse Acts 2-3 (z ~298-680) from kit prefabs.
    /// Act 1 (beats A-G) is hand-kept in the scene and only touched where it hands over to Act 2: CP3 loses its
    /// short fuse, the FinishZone moves to the new end, the KillZone grows. Idempotent: rerunning deletes and
    /// rebuilds <c>SectionRoot/Course/Act2</c>, <c>Act3</c> and <c>Backdrop/Extension</c> only.
    /// Beat layout: ARCHITECTURE.md §4.
    /// </summary>
    public static class CourseBuilder
    {
        const string ScenePath = "Assets/Scenes/PrototypeCourse.unity";
        const string MatDir = "Assets/Art/Materials/";
        const string PlatformsDir = "Assets/Prefabs/Platforms/";
        const string ObstaclesDir = "Assets/Prefabs/Obstacles/";
        const string GameplayDir = "Assets/Prefabs/Gameplay/";

        // ------------------------------------------------------------------ menu

        [MenuItem("HotPatata/Course/Build Obstacle Prefabs (Factory + Slide)")]
        public static void BuildPrefabs() => BuildPrefabs(overwrite: true);

        [MenuItem("HotPatata/Course/Build Acts 2-3")]
        public static void BuildActs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            BuildPrefabs(overwrite: false);
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);

            var course = GameObject.Find("SectionRoot/Course")?.transform;
            if (course == null) throw new InvalidOperationException("PrototypeCourse has no SectionRoot/Course");

            RebuildGroup(course, "Act2", BuildAct2);
            RebuildGroup(course, "Act3", BuildAct3);
            PatchAct1(course);
            ExtendBackdrop();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CourseBuilder] Acts 2-3 built");
        }

        static void RebuildGroup(Transform course, string name, Action<Transform> build)
        {
            var old = course.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(name).transform;
            root.SetParent(course, false);
            build(root);
        }

        // ------------------------------------------------------------------ Act 1 hand-over

        static void PatchAct1(Transform course)
        {
            // The final sprint is no longer the finale: normal fuse there, the short fuse moves to Act 3.
            SetField(course.Find("CP_03").GetComponent<Checkpoint>(), "holdFuseOverride", p => p.floatValue = 0f);

            var finish = course.Find("FinishZone");
            finish.position = new Vector3(0f, 9.6f, 671f);
            finish.SetAsLastSibling();

            var kill = GameObject.Find("SectionRoot/KillZone").transform;
            kill.position = new Vector3(0f, -14f, 340f);
            kill.localScale = new Vector3(240f, 4f, 800f);   // z -60..740
        }

        // ------------------------------------------------------------------ Act 2: Patata Factory (z ~298-481)

        static void BuildAct2(Transform act)
        {
            var floor = Mat("Greybox_Floor");
            var wall = Mat("Greybox_Wall");

            // CP4 on the old finish platform (G_Finish, top 8.4).
            AddCheckpoint(act, "CP_04", 4, new Vector3(0f, 8.4f, 291.6f), 0f);

            // --- H Conveyor Hall: three belts, the centre one runs backwards. Hurdles force lane changes.
            AddConveyor(act, "H_Belt_L", new Vector3(-5.5f, 7.9f, 319f), new Vector3(4f, 1f, 38f), 3f);
            AddConveyor(act, "H_Belt_C", new Vector3(0f, 7.9f, 319f), new Vector3(4f, 1f, 38f), -4f);
            AddConveyor(act, "H_Belt_R", new Vector3(5.5f, 7.9f, 319f), new Vector3(4f, 1f, 38f), 3f);
            Block(act, "H_Hurdle_L", new Vector3(-5.5f, 9.0f, 312f), new Vector3(4f, 1.2f, 0.6f), wall);
            Block(act, "H_Hurdle_R", new Vector3(5.5f, 9.0f, 326f), new Vector3(4f, 1.2f, 0.6f), wall);
            Block(act, "H_End", new Vector3(0f, 8.6f, 343.5f), new Vector3(16f, 2f, 8f), floor);   // top 9.6, +1.2 off the belts

            // --- I Piston Alley: five floor tiles rise and fall (9.6 <-> 12.6) in a wave. A raised tile blocks
            // the pass along the corridor; the receiver's tile moving is a vertical relay.
            for (int i = 0; i < 5; i++)
            {
                float z = 352f + i * 7.5f;   // tiles 6 m long, 1.5 m gaps: z 349..385
                AddMover(act, "Obstacle_Piston", $"I_Piston_{i + 1}", new Vector3(0f, 9.1f, z), new Vector3(0f, 12.1f, z),
                         new Vector3(8f, 1f, 6f), speed: 1.2f, phase: i * 0.2f, dwell: 0.4f);
            }
            Block(act, "I_End", new Vector3(0f, 8.6f, 399.75f), new Vector3(16f, 2f, 26.5f), floor);   // z 386.5..413
            AddCheckpoint(act, "CP_05", 5, new Vector3(0f, 9.6f, 392f), 0f);

            // --- J Windmill Wall: an 11 m wall with a hole guarded by lethal windmill blades. The bomb goes through
            // the hole from balcony to balcony; players ride the elevators over the top and drop down.
            Block(act, "J_Step", new Vector3(0f, 10.3f, 406f), new Vector3(8f, 1.4f, 4f), floor);            // top 11.0
            Block(act, "J_Balcony", new Vector3(0f, 11.0f, 410.5f), new Vector3(8f, 2.8f, 5f), floor);        // top 12.4
            Block(act, "J_Wall_L", new Vector3(-6.65f, 15.1f, 414f), new Vector3(9.7f, 11f, 2f), wall);       // x -11.5..-1.8
            Block(act, "J_Wall_R", new Vector3(6.65f, 15.1f, 414f), new Vector3(9.7f, 11f, 2f), wall);
            Block(act, "J_Wall_Low", new Vector3(0f, 11.8f, 414f), new Vector3(3.6f, 4.4f, 2f), wall);        // hole y 14.0..17.6
            Block(act, "J_Wall_High", new Vector3(0f, 19.1f, 414f), new Vector3(3.6f, 3f, 2f), wall);         // top 20.6
            Place(act, ObstaclesDir + "Obstacle_Windmill", "J_Windmill", new Vector3(0f, 15.8f, 412.75f), Quaternion.identity);
            AddMover(act, "Platform_Elevator", "J_Elevator_L", new Vector3(-9.5f, 9.35f, 411.2f), new Vector3(-9.5f, 20.35f, 411.2f),
                     new Vector3(3f, 0.5f, 3f), speed: 2.75f, phase: 0f, dwell: 0.3f);
            AddMover(act, "Platform_Elevator", "J_Elevator_R", new Vector3(9.5f, 9.35f, 411.2f), new Vector3(9.5f, 20.35f, 411.2f),
                     new Vector3(3f, 0.5f, 3f), speed: 2.75f, phase: 0.5f, dwell: 0.3f);
            Block(act, "J_FarBalcony", new Vector3(0f, 11.0f, 417.5f), new Vector3(8f, 2.8f, 5f), floor);    // top 12.4
            Block(act, "J_FarStep", new Vector3(0f, 10.3f, 422f), new Vector3(8f, 1.4f, 4f), floor);         // top 11.0
            Block(act, "J_Far", new Vector3(0f, 8.6f, 424f), new Vector3(16f, 2f, 18f), floor);              // z 415..433, top 9.6

            // --- K Sweeper Pit: two knee-high lethal sweepers turning opposite ways; jump them while passing.
            Block(act, "K_Pit", new Vector3(0f, 8.6f, 450f), new Vector3(14f, 2f, 30f), floor);             // z 435..465
            AddRotator(act, ObstaclesDir + "Obstacle_Sweeper", "K_Sweeper_1", new Vector3(0f, 9.6f, 442.5f), 13.6f, 90f, 0f);
            AddRotator(act, ObstaclesDir + "Obstacle_Sweeper", "K_Sweeper_2", new Vector3(0f, 9.6f, 457.5f), 13.6f, -90f, 90f);

            // Exit corridor under the crusher: slide under it (or throw the bomb low beneath it) while it is down.
            Block(act, "K_Exit", new Vector3(0f, 8.6f, 474f), new Vector3(7f, 2f, 14f), floor);              // z 467..481
            Block(act, "K_Exit_Wall_L", new Vector3(-4f, 12.8f, 474f), new Vector3(1f, 6.4f, 6f), wall);
            Block(act, "K_Exit_Wall_R", new Vector3(4f, 12.8f, 474f), new Vector3(1f, 6.4f, 6f), wall);
            AddCrusher(act, "K_Crusher", new Vector3(0f, 9.6f, 474f), width: 6.9f, length: 4f, phase: 0f);
        }

        // ------------------------------------------------------------------ Act 3: The Climb & The Drop (z ~483-678)

        static void BuildAct3(Transform act)
        {
            var plat = Mat("Greybox_Platform");
            var wall = Mat("Greybox_Wall");

            // --- L Elevator Tower: 9.6 -> 30.6 with the team (and the bomb) together.
            Block(act, "L_Base", new Vector3(0f, 8.6f, 491f), new Vector3(16f, 2f, 16f), plat);             // z 483..499
            AddCheckpoint(act, "CP_06", 6, new Vector3(0f, 9.6f, 488f), 5f);
            var pad = Place(act, ObstaclesDir + "LaunchPad", "L_LaunchPad", new Vector3(4f, 9.6f, 497f), Quaternion.identity);
            SetField(pad.GetComponent<LaunchPad>(), "launchHeight", p => p.floatValue = 8f);
            AddMover(act, "Platform_Elevator", "L_Elevator_1", new Vector3(-4f, 9.35f, 500.7f), new Vector3(-4f, 16.35f, 500.7f),
                     new Vector3(3f, 0.5f, 3f), speed: 2.3f, phase: 0f, dwell: 0.3f);
            Block(act, "L1", new Vector3(0f, 15.6f, 506.5f), new Vector3(14f, 2f, 8f), plat);               // z 502.5..510.5, top 16.6
            AddMover(act, "Platform_Elevator", "L_Elevator_2", new Vector3(3f, 16.35f, 512.15f), new Vector3(3f, 23.35f, 512.15f),
                     new Vector3(3f, 0.5f, 3f), speed: 2.3f, phase: 0.25f, dwell: 0.3f);
            Block(act, "L2", new Vector3(0f, 22.6f, 517.9f), new Vector3(9f, 2f, 8.2f), plat);              // z 513.8..522, top 23.6
            AddRotator(act, ObstaclesDir + "Obstacle_Sweeper", "L2_Sweeper", new Vector3(0f, 23.6f, 517.9f), 8f, 120f, 0f);

            // Two ways up to the summit: an elevator (left) or a mantle staircase (right, 1.4 m steps).
            AddMover(act, "Platform_Elevator", "L_Elevator_3", new Vector3(-2.75f, 23.35f, 523.65f), new Vector3(-2.75f, 30.35f, 523.65f),
                     new Vector3(3f, 0.5f, 3f), speed: 2.3f, phase: 0.5f, dwell: 0.3f);
            Block(act, "L3_Landing", new Vector3(-3f, 29.6f, 529.7f), new Vector3(6f, 2f, 8.6f), plat);      // z 525.4..534, top 30.6
            float[] steps = { 25.0f, 26.4f, 27.8f, 29.2f };
            for (int i = 0; i < steps.Length; i++)
            {
                float top = steps[i], bottom = 20f;
                Block(act, $"L_Stair_{i + 1}", new Vector3(2.5f, (top + bottom) / 2f, 523.5f + i * 3f),
                      new Vector3(4f, top - bottom, 3f), plat);
            }
            Block(act, "M_Summit", new Vector3(0f, 29.6f, 541f), new Vector3(14f, 2f, 14f), plat);          // z 534..548, top 30.6
            AddCheckpoint(act, "CP_07", 7, new Vector3(0f, 30.6f, 541f), 4.5f);

            // --- M Mega Slide: 68 m (horizontal) at 18 deg, 30.6 -> 8.4, two lanes split by a low divider.
            // Hoop gates spin over the divider; two tall divider stretches block passes for a while.
            const float slope = 18f, topY = 30.6f, bottomY = 8.4f, startZ = 548f;
            float run = (topY - bottomY) / Mathf.Tan(slope * Mathf.Deg2Rad);   // ~68.3 m
            float length = run / Mathf.Cos(slope * Mathf.Deg2Rad);
            var rot = Quaternion.Euler(slope, 0f, 0f);
            Vector3 up = rot * Vector3.up, fwd = rot * Vector3.forward;
            Vector3 top0 = new Vector3(0f, topY, startZ);
            Vector3 OnSlope(float x, float along, float height) => top0 + Vector3.right * x + fwd * along + up * height;

            var slideMat = Mat("Greybox_Slide");
            Block(act, "M_Lane_L", OnSlope(-2.5f, length / 2f, -0.5f), new Vector3(4f, 1f, length), slideMat, rot);
            Block(act, "M_Lane_R", OnSlope(2.5f, length / 2f, -0.5f), new Vector3(4f, 1f, length), slideMat, rot);
            Block(act, "M_Divider", OnSlope(0f, length / 2f, -0.2f), new Vector3(1f, 1.6f, length), wall, rot);          // 0.6 m above the lanes
            Block(act, "M_Rail_L", OnSlope(-4.75f, length / 2f, 0f), new Vector3(0.5f, 2.4f, length), wall, rot);        // 1.2 m rails
            Block(act, "M_Rail_R", OnSlope(4.75f, length / 2f, 0f), new Vector3(0.5f, 2.4f, length), wall, rot);
            Block(act, "M_Divider_High_1", OnSlope(0f, 25f, 1.25f), new Vector3(1f, 2.5f, 10f), wall, rot);             // 2.5 m above
            Block(act, "M_Divider_High_2", OnSlope(0f, 50f, 1.25f), new Vector3(1f, 2.5f, 10f), wall, rot);
            float[] hoops = { 12f, 37.5f, 62f };
            for (int i = 0; i < hoops.Length; i++)
                AddRotator(act, ObstaclesDir + "Obstacle_Hoop", $"M_Hoop_{i + 1}", OnSlope(0f, hoops[i], 2.6f), 0f, i % 2 == 0 ? 70f : -70f, i * 40f);

            // --- N Factory Finale: run-out under a crusher, a forward belt through three piston gates, the podium.
            float endZ = startZ + run;
            Block(act, "N_Runout", new Vector3(0f, 7.4f, (endZ + 630f) / 2f), new Vector3(12f, 2f, 630f - endZ), plat);   // top 8.4
            Block(act, "N_Crusher_Wall_L", new Vector3(-5.5f, 11.6f, 624f), new Vector3(1f, 6.4f, 6f), wall);
            Block(act, "N_Crusher_Wall_R", new Vector3(5.5f, 11.6f, 624f), new Vector3(1f, 6.4f, 6f), wall);
            AddCrusher(act, "N_Crusher", new Vector3(0f, 8.4f, 624f), width: 9.9f, length: 4f, phase: 0.5f);
            AddConveyor(act, "N_Belt", new Vector3(0f, 7.9f, 647f), new Vector3(8f, 1f, 30f), 4f);                  // z 632..662
            for (int i = 0; i < 3; i++)
            {
                float z = 640f + i * 8f;
                // Up: the gate stands 3.5 m over the belt; down: it hides just under the belt surface.
                AddMover(act, "Obstacle_Piston", $"N_Gate_{i + 1}", new Vector3(0f, 10.15f, z), new Vector3(0f, 6.45f, z),
                         new Vector3(8f, 3.5f, 1f), speed: 2.5f, phase: i / 3f, dwell: 0.35f);
            }
            Block(act, "N_Podium", new Vector3(0f, 8.6f, 671f), new Vector3(14f, 2f, 14f), plat);                    // z 664..678, top 9.6
        }

        // ------------------------------------------------------------------ backdrop

        static void ExtendBackdrop()
        {
            var backdrop = GameObject.Find("Backdrop")?.transform;
            if (backdrop == null) return;
            var old = backdrop.Find("Extension");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var ext = new GameObject("Extension").transform;
            ext.SetParent(backdrop, false);

            var rng = new Random(4242);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var island = Mat("Backdrop_Island");
            var trunk = Mat("Backdrop_Trunk");
            var leaves = Mat("Backdrop_Leaves");
            var cloud = Mat("Backdrop_Cloud");

            for (int i = 0; i < 16; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var root = new GameObject($"Island_X{i}").transform;
                root.SetParent(ext, false);
                root.SetPositionAndRotation(new Vector3(side * R(45f, 125f), R(-22f, 30f), R(400f, 760f)), Quaternion.Euler(0f, R(0f, 360f), 0f));
                float w = R(16f, 26f);
                Prim(root, PrimitiveType.Sphere, Vector3.zero, new Vector3(w, w * 0.275f, w * 0.85f), island);
                Prim(root, PrimitiveType.Sphere, new Vector3(0f, -w * 0.225f, 0f), new Vector3(w * 0.65f, w * 0.55f, w * 0.55f), island);
                int trees = rng.Next(1, 5);
                for (int t = 0; t < trees; t++)
                {
                    var p = new Vector3(R(-w * 0.3f, w * 0.3f), 0f, R(-w * 0.25f, w * 0.25f));
                    float h = R(0.8f, 1.5f);
                    Prim(root, PrimitiveType.Cylinder, p + Vector3.up * (w * 0.14f + h), new Vector3(0.5f, h, 0.5f), trunk);
                    float s = R(2.5f, 3.1f);
                    Prim(root, PrimitiveType.Sphere, p + Vector3.up * (w * 0.14f + 2f * h + s * 0.3f), new Vector3(s, s * 0.9f, s), leaves);
                }
            }
            for (int i = 0; i < 26; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var root = new GameObject("Cloud").transform;
                root.SetParent(ext, false);
                root.position = new Vector3(side * R(30f, 170f), R(-40f, 55f), R(380f, 780f));
                int puffs = rng.Next(3, 6);
                for (int k = 0; k < puffs; k++)
                {
                    float s = R(4.5f, 8f);
                    Prim(root, PrimitiveType.Sphere, new Vector3(R(-7f, 7f), R(-0.6f, 1.8f), R(-2.5f, 1.2f)), new Vector3(s, s * 0.72f, s), cloud);
                }
            }
        }

        static void Prim(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());   // decoration only: never in the way of a pass
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ course helpers

        static GameObject Place(Transform parent, string prefabPathNoExt, string name, Vector3 position, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPathNoExt + ".prefab")
                         ?? throw new InvalidOperationException("missing prefab " + prefabPathNoExt);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        /// <summary>A Platform_Basic box: pivot at its centre, scale = size.</summary>
        static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Material mat, Quaternion? rotation = null)
        {
            var go = Place(parent, PlatformsDir + "Platform_Basic", name, center, rotation ?? Quaternion.identity);
            go.transform.localScale = size;
            if (mat != null) go.GetComponentInChildren<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void AddCheckpoint(Transform parent, string name, int id, Vector3 position, float fuse)
        {
            var go = Place(parent, GameplayDir + "Checkpoint", name, position, Quaternion.identity);
            var cp = go.GetComponent<Checkpoint>();
            SetField(cp, "id", p => p.intValue = id);
            SetField(cp, "holdFuseOverride", p => p.floatValue = fuse);
        }

        static void AddConveyor(Transform parent, string name, Vector3 center, Vector3 size, float speed)
        {
            var go = Place(parent, PlatformsDir + "Platform_Conveyor", name, center, Quaternion.identity);
            go.transform.localScale = size;
            SetField(go.GetComponent<Conveyor>(), "speed", p => p.floatValue = speed);
        }

        /// <summary>A MovingPlatform-based prefab: its Platform child is resized to <paramref name="size"/>.</summary>
        static void AddMover(Transform parent, string prefabName, string name, Vector3 a, Vector3 b, Vector3 size,
                             float speed, float phase, float dwell)
        {
            string dir = prefabName.StartsWith("Platform_") ? PlatformsDir : ObstaclesDir;
            var go = Place(parent, dir + prefabName, name, a, Quaternion.identity);
            var t = go.transform;
            t.Find("Waypoint_A").position = a;
            t.Find("Waypoint_B").position = b;
            var platform = t.Find("Platform");
            platform.position = a;
            platform.Find("Visual").localScale = size;
            platform.Find("Collision").GetComponent<BoxCollider>().size = size;
            var mp = go.GetComponent<MovingPlatform>();
            SetField(mp, "speed", p => p.floatValue = speed);
            SetField(mp, "startPhase", p => p.floatValue = phase);
            SetField(mp, "motion", p => p.enumValueIndex = (int)MovingPlatform.Motion.Dwell);
            SetField(mp, "dwellFraction", p => p.floatValue = dwell);
        }

        /// <summary>
        /// A crusher slab whose underside stops <see cref="CrusherLowClearance"/> above <paramref name="floor"/>: a
        /// crouched or sliding player (1.0 m) fits, a standing one (1.8 m) is caught by the lethal strip.
        /// </summary>
        static void AddCrusher(Transform parent, string name, Vector3 floor, float width, float length, float phase)
        {
            const float thickness = 1.5f;
            float low = floor.y + CrusherLowClearance + thickness / 2f;
            float high = floor.y + 4.2f + thickness / 2f;
            var a = new Vector3(floor.x, high, floor.z);
            var b = new Vector3(floor.x, low, floor.z);
            var go = Place(parent, ObstaclesDir + "Obstacle_Crusher", name, a, Quaternion.identity);
            var t = go.transform;
            t.Find("Waypoint_A").position = a;
            t.Find("Waypoint_B").position = b;
            var platform = t.Find("Platform");
            platform.position = a;
            var size = new Vector3(width, thickness, length);
            platform.Find("Visual").localScale = size;
            platform.Find("Collision").GetComponent<BoxCollider>().size = size;
            var kill = platform.Find("Kill");
            kill.localPosition = new Vector3(0f, -thickness / 2f - 0.05f, 0f);
            kill.GetComponent<BoxCollider>().size = new Vector3(width - 0.2f, 0.3f, length - 0.2f);   // reaches 0.2 m below the slab
            SetField(go.GetComponent<MovingPlatform>(), "startPhase", p => p.floatValue = phase);
        }

        const float CrusherLowClearance = 1.45f;

        /// <summary>A RotatingObstacle prefab; a non-zero <paramref name="length"/> resizes a sweeper's bar.</summary>
        static void AddRotator(Transform parent, string prefabPath, string name, Vector3 position, float length, float degreesPerSecond, float phase)
        {
            var go = Place(parent, prefabPath, name, position, Quaternion.identity);
            var rot = go.GetComponent<RotatingObstacle>();
            SetField(rot, "degreesPerSecond", p => p.floatValue = degreesPerSecond);
            SetField(rot, "phaseDegrees", p => p.floatValue = phase);
            if (length > 0f) ResizeSweeper(go.transform, length);
        }

        static void ResizeSweeper(Transform sweeper, float length)
        {
            var v = sweeper.Find("Visual");
            v.localScale = new Vector3(length, v.localScale.y, v.localScale.z);
            var c = sweeper.Find("Collision").GetComponent<BoxCollider>();
            c.size = new Vector3(length, c.size.y, c.size.z);
            var k = sweeper.Find("Kill").GetComponent<BoxCollider>();
            k.size = new Vector3(length + 0.2f, k.size.y, k.size.z);
        }

        static void SetField(Object target, string field, Action<SerializedProperty> set)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} not found");
            set(p);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ materials

        static Material Mat(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat") ?? throw new InvalidOperationException("missing material " + name);

        static Material MakeMaterial(string name, string template, Color baseColor, Color top, int pattern, Color patternColor,
                                     float patternScale, float patternStrength)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                AssetDatabase.CopyAsset(MatDir + template + ".mat", path);
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_TopColor", top);
            mat.SetFloat("_Pattern", pattern);
            mat.SetColor("_PatternColor", patternColor);
            mat.SetFloat("_PatternScale", patternScale);
            mat.SetFloat("_PatternStrength", patternStrength);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void BuildMaterials()
        {
            // Belts: dark rubber with bright diagonal stripes that scroll with the belt.
            MakeMaterial("Greybox_Conveyor", "Greybox_Hazard", new Color(0.28f, 0.27f, 0.34f), new Color(0.36f, 0.35f, 0.42f),
                         2, new Color(1f, 0.82f, 0.25f), 0.6f, 0.75f);
            // Gates (windmill hub, hoops): sunny yellow, clearly "aim here".
            MakeMaterial("Greybox_Gate", "Greybox_Narrow", new Color(1f, 0.83f, 0.2f), new Color(1f, 0.93f, 0.6f),
                         0, Color.black, 1f, 0f);
            // Slide lanes: light, glossy-looking, with a coarse checker to read speed.
            MakeMaterial("Greybox_Slide", "Greybox_Platform", new Color(0.3f, 0.72f, 1f), new Color(0.45f, 0.82f, 1f),
                         1, new Color(0.1f, 0.1f, 0.1f), 3f, 0.14f);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ prefabs

        static void BuildPrefabs(bool overwrite)
        {
            BuildMaterials();
            MakePrefab(PlatformsDir + "Platform_Conveyor.prefab", overwrite, BuildConveyorPrefab);
            MakePrefab(PlatformsDir + "Platform_Elevator.prefab", overwrite,
                       () => BuildMoverPrefab("Platform_Elevator", new Vector3(3f, 0.5f, 3f), new Vector3(0f, 7f, 0f), Mat("Greybox_Moving"), 2.3f, 0.3f));
            MakePrefab(ObstaclesDir + "Obstacle_Piston.prefab", overwrite,
                       () => BuildMoverPrefab("Obstacle_Piston", new Vector3(8f, 1f, 6f), new Vector3(0f, 3f, 0f), Mat("Greybox_Moving"), 1.2f, 0.4f));
            MakePrefab(ObstaclesDir + "Obstacle_Crusher.prefab", overwrite, BuildCrusherPrefab);
            MakePrefab(ObstaclesDir + "Obstacle_Sweeper.prefab", overwrite, BuildSweeperPrefab);
            MakePrefab(ObstaclesDir + "Obstacle_Windmill.prefab", overwrite, BuildWindmillPrefab);
            MakePrefab(ObstaclesDir + "Obstacle_Hoop.prefab", overwrite, BuildHoopPrefab);
            AssetDatabase.SaveAssets();
        }

        static void MakePrefab(string path, bool overwrite, Func<GameObject> build)
        {
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var go = build();
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static int Layer(string name) => LayerMask.NameToLayer(name);

        static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, string layer, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer = Layer(layer);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static BoxCollider Box(string name, Transform parent, Vector3 localPos, Vector3 size, string layer, bool trigger = false)
        {
            var go = new GameObject(name) { layer = Layer(layer) };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = trigger;
            return box;
        }

        static void KinematicBody(GameObject go)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        static GameObject BuildConveyorPrefab()
        {
            var root = new GameObject("Platform_Conveyor") { layer = Layer("Environment") };
            root.AddComponent<BoxCollider>();
            root.transform.localScale = new Vector3(4f, 1f, 12f);
            var visual = Cube("Visual", root.transform, Vector3.zero, Vector3.one, Mat("Greybox_Conveyor"), "Environment");
            var conveyor = root.AddComponent<Conveyor>();
            SetField(conveyor, "beltRenderers", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = visual.GetComponent<Renderer>();
            });
            return root;
        }

        /// <summary>MovingPlatform root / Platform (kinematic) / Visual + Collision, Waypoint_A at the root, Waypoint_B at <paramref name="travel"/>.</summary>
        static GameObject BuildMoverPrefab(string name, Vector3 size, Vector3 travel, Material mat, float speed, float dwell, string layer = "Environment")
        {
            var root = new GameObject(name);
            var platform = new GameObject("Platform");
            platform.transform.SetParent(root.transform, false);
            KinematicBody(platform);
            Cube("Visual", platform.transform, Vector3.zero, size, mat, layer);
            Box("Collision", platform.transform, Vector3.zero, size, layer);
            var a = new GameObject("Waypoint_A").transform;
            a.SetParent(root.transform, false);
            var b = new GameObject("Waypoint_B").transform;
            b.SetParent(root.transform, false);
            b.localPosition = travel;

            var mp = root.AddComponent<MovingPlatform>();
            SetField(mp, "platform", p => p.objectReferenceValue = platform.transform);
            SetField(mp, "waypointA", p => p.objectReferenceValue = a);
            SetField(mp, "waypointB", p => p.objectReferenceValue = b);
            SetField(mp, "speed", p => p.floatValue = speed);
            SetField(mp, "motion", p => p.enumValueIndex = (int)MovingPlatform.Motion.Dwell);
            SetField(mp, "dwellFraction", p => p.floatValue = dwell);
            return root;
        }

        static GameObject BuildCrusherPrefab()
        {
            // Waypoint_A = up (open), Waypoint_B = down (1.45 m clearance). Striped: it is lethal.
            var root = BuildMoverPrefab("Obstacle_Crusher", new Vector3(6.9f, 1.5f, 4f), new Vector3(0f, -2.75f, 0f),
                                        Mat("Greybox_Hazard"), 2f, 0.45f, "Hazard");
            var platform = root.transform.Find("Platform");
            var kill = Box("Kill", platform, new Vector3(0f, -0.8f, 0f), new Vector3(6.7f, 0.3f, 3.8f), "Trigger", trigger: true);
            kill.gameObject.AddComponent<KillZone>();
            return root;
        }

        static GameObject BuildSweeperPrefab()
        {
            // Pivot on the floor; the bar sits 0.2..0.7 m up (a jump clears it, a slide does not).
            var root = new GameObject("Obstacle_Sweeper");
            KinematicBody(root);
            var hazard = Mat("Greybox_Hazard");
            Cube("Visual", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(13.6f, 0.5f, 0.5f), hazard, "Hazard");
            Box("Collision", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(13.6f, 0.5f, 0.5f), "Hazard");
            var kill = Box("Kill", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(13.8f, 0.7f, 0.7f), "Trigger", trigger: true);
            kill.gameObject.AddComponent<KillZone>();
            var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Hub";
            Object.DestroyImmediate(hub.GetComponent<Collider>());
            hub.transform.SetParent(root.transform, false);
            hub.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            hub.transform.localScale = new Vector3(1f, 0.45f, 1f);
            hub.GetComponent<Renderer>().sharedMaterial = Mat("Greybox_Gate");
            var rot = root.AddComponent<RotatingObstacle>();
            SetField(rot, "axis", p => p.vector3Value = Vector3.up);
            SetField(rot, "degreesPerSecond", p => p.floatValue = 90f);
            return root;
        }

        static GameObject BuildWindmillPrefab()
        {
            // Three lethal blades on a hub, turning in the wall's plane (axis = forward).
            var root = new GameObject("Obstacle_Windmill");
            KinematicBody(root);
            var hazard = Mat("Greybox_Hazard");
            var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Hub";
            hub.layer = Layer("Hazard");
            Object.DestroyImmediate(hub.GetComponent<Collider>());
            hub.transform.SetParent(root.transform, false);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hub.transform.localScale = new Vector3(0.8f, 0.25f, 0.8f);
            hub.GetComponent<Renderer>().sharedMaterial = Mat("Greybox_Gate");
            Box("HubCollision", root.transform, Vector3.zero, new Vector3(0.8f, 0.8f, 0.5f), "Hazard");
            for (int i = 0; i < 3; i++)
            {
                var arm = new GameObject($"Blade_{i + 1}").transform;
                arm.SetParent(root.transform, false);
                arm.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
                Cube("Visual", arm, new Vector3(0f, 1.1f, 0f), new Vector3(0.7f, 1.6f, 0.3f), hazard, "Hazard");
                Box("Collision", arm, new Vector3(0f, 1.1f, 0f), new Vector3(0.7f, 1.6f, 0.3f), "Hazard");
                var kill = Box("Kill", arm, new Vector3(0f, 1.1f, 0f), new Vector3(0.9f, 1.8f, 0.6f), "Trigger", trigger: true);
                kill.gameObject.AddComponent<KillZone>();
            }
            var rot = root.AddComponent<RotatingObstacle>();
            SetField(rot, "axis", p => p.vector3Value = Vector3.forward);
            SetField(rot, "degreesPerSecond", p => p.floatValue = 90f);
            return root;
        }

        static GameObject BuildHoopPrefab()
        {
            // A ring (radius 1.6) in the local YZ plane, spinning about up: face-on it lets a sideways pass through,
            // edge-on its sides block the pass line. Environment, so the bomb explodes on it like on any wall.
            const int segments = 10;
            const float radius = 1.6f;
            var root = new GameObject("Obstacle_Hoop");
            KinematicBody(root);
            var mat = Mat("Greybox_Gate");
            float segLength = 2f * Mathf.PI * radius / segments * 1.08f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 360f / segments;
                var seg = new GameObject($"Segment_{i + 1}").transform;
                seg.SetParent(root.transform, false);
                seg.localRotation = Quaternion.Euler(-a, 0f, 0f);
                Cube("Visual", seg, new Vector3(0f, radius, 0f), new Vector3(0.25f, 0.3f, segLength), mat, "Environment");
                Box("Collision", seg, new Vector3(0f, radius, 0f), new Vector3(0.25f, 0.3f, segLength), "Environment");
            }
            var rot = root.AddComponent<RotatingObstacle>();
            SetField(rot, "axis", p => p.vector3Value = Vector3.up);
            SetField(rot, "degreesPerSecond", p => p.floatValue = 70f);
            return root;
        }
    }
}
