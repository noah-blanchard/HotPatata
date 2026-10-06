using HotPatata;
using UnityEditor;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Builds the generated kit prefabs (conveyor, elevator, piston, crusher, sweeper, windmill, hoop) and their materials.
    /// <see cref="KayKitKitBuilder"/> calls it before skinning the kit; <see cref="BombObstacleKitBuilder"/> builds the #68 ones.
    /// </summary>
    public static class KitPrefabBuilder
    {
        [MenuItem("HotPatata/Kit/Build Generated Obstacle Prefabs")]
        public static void BuildPrefabs() => BuildPrefabs(overwrite: true);

        // ------------------------------------------------------------------ materials

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
                       () => BuildMoverPrefab("Platform_Elevator", new Vector3(3f, 0.5f, 3f), new Vector3(0f, 7f, 0f), KitRole.Mover, 2.3f, 0.3f));
            MakePrefab(ObstaclesDir + "Obstacle_Piston.prefab", overwrite,
                       () => BuildMoverPrefab("Obstacle_Piston", new Vector3(8f, 1f, 6f), new Vector3(0f, 3f, 0f), KitRole.Mover, 1.2f, 0.4f));
            MakePrefab(ObstaclesDir + "Obstacle_Crusher.prefab", overwrite, BuildCrusherPrefab);
            MakePrefab(ObstaclesDir + "Obstacle_Sweeper.prefab", overwrite, BuildSweeperPrefab);
            MakePrefab(ObstaclesDir + "Obstacle_Windmill.prefab", overwrite, BuildWindmillPrefab);
            MakePrefab(ObstaclesDir + "Obstacle_Hoop.prefab", overwrite, BuildHoopPrefab);
            AssetDatabase.SaveAssets();
        }

        static GameObject BuildConveyorPrefab()
        {
            var root = new GameObject("Platform_Conveyor") { layer = Layer("Environment") };
            root.AddComponent<BoxCollider>();
            root.transform.localScale = new Vector3(4f, 1f, 12f);
            var visual = Cube("Visual", root.transform, Vector3.zero, Vector3.one, KitRole.Belt, "Environment");
            Skin(visual, KitRole.Belt, flip: true);   // the default belt carries forward (+Z)
            var conveyor = root.AddComponent<Conveyor>();
            SetField(conveyor, "beltRenderers", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = visual.GetComponent<Renderer>();
            });
            return root;
        }

        /// <summary>MovingPlatform root / Platform (kinematic) / Visual + Collision, Waypoint_A at the root, Waypoint_B at <paramref name="travel"/>.</summary>
        public static GameObject BuildMoverPrefab(string name, Vector3 size, Vector3 travel, KitRole role, float speed, float dwell, string layer = "Environment")
        {
            var root = new GameObject(name);
            var platform = new GameObject("Platform");
            platform.transform.SetParent(root.transform, false);
            KinematicBody(platform);
            Cube("Visual", platform.transform, Vector3.zero, size, role, layer);
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
                                        KitRole.Hazard, 2f, 0.45f, "Hazard");
            var platform = root.GetComponent<MovingPlatform>().Platform;
            var kill = Box("Kill", platform, new Vector3(0f, -0.8f, 0f), new Vector3(6.7f, 0.3f, 3.8f), "Trigger", trigger: true);
            kill.gameObject.AddComponent<KillZone>();
            return root;
        }

        static GameObject BuildSweeperPrefab()
        {
            // Pivot on the floor; the bar sits 0.2..0.7 m up (a jump clears it, a slide does not).
            var root = new GameObject("Obstacle_Sweeper");
            KinematicBody(root);
            Cube("Visual", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(13.6f, 0.5f, 0.5f), KitRole.Hazard, "Hazard");
            Box("Collision", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(13.6f, 0.5f, 0.5f), "Hazard");
            var kill = Box("Kill", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(13.8f, 0.7f, 0.7f), "Trigger", trigger: true);
            kill.gameObject.AddComponent<KillZone>();
            var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Hub";
            Object.DestroyImmediate(hub.GetComponent<Collider>());
            hub.transform.SetParent(root.transform, false);
            hub.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            hub.transform.localScale = new Vector3(1f, 0.45f, 1f);
            Skin(hub, KitRole.Gate, unitBox: CylinderBox);
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
            var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Hub";
            hub.layer = Layer("Hazard");
            Object.DestroyImmediate(hub.GetComponent<Collider>());
            hub.transform.SetParent(root.transform, false);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hub.transform.localScale = new Vector3(0.8f, 0.25f, 0.8f);
            Skin(hub, KitRole.Gate, unitBox: CylinderBox);
            Box("HubCollision", root.transform, Vector3.zero, new Vector3(0.8f, 0.8f, 0.5f), "Hazard");
            for (int i = 0; i < 3; i++)
            {
                var arm = new GameObject($"Blade_{i + 1}").transform;
                arm.SetParent(root.transform, false);
                arm.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
                Cube("Visual", arm, new Vector3(0f, 1.1f, 0f), new Vector3(0.7f, 1.6f, 0.3f), KitRole.Hazard, "Hazard");
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
            float segLength = 2f * Mathf.PI * radius / segments * 1.08f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 360f / segments;
                var seg = new GameObject($"Segment_{i + 1}").transform;
                seg.SetParent(root.transform, false);
                seg.localRotation = Quaternion.Euler(-a, 0f, 0f);
                Cube("Visual", seg, new Vector3(0f, radius, 0f), new Vector3(0.25f, 0.3f, segLength), KitRole.Gate, "Environment");
                Box("Collision", seg, new Vector3(0f, radius, 0f), new Vector3(0.25f, 0.3f, segLength), "Environment");
            }
            var rot = root.AddComponent<RotatingObstacle>();
            SetField(rot, "axis", p => p.vector3Value = Vector3.up);
            SetField(rot, "degreesPerSecond", p => p.floatValue = 70f);
            return root;
        }
    }
}
