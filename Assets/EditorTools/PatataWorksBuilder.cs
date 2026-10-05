using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using static HotPatata.Editor.CourseKit;
using static HotPatata.Editor.BombObstacleKitBuilder;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>Deterministic factory layout, PROJECT_SPEC §15b. Build locally along +Z, then turn each room as a unit.</summary>
    public static class PatataWorksBuilder
    {
        public const string ScenePath = "Assets/Scenes/PatataWorks.unity";
        const string NetworkPrefab = "Assets/Prefabs/Network/NetworkManager.prefab";
        const float HalfWidth = 12f;
        static GameTuning Tune => AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/ScriptableObjects/Tuning/GameTuning.asset");
        static readonly List<Vector3> Route = new();
        static readonly List<bool> Outdoors = new();

        [MenuItem("HotPatata/Course/Build PatataWorks")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null &&
                !AssetDatabase.CopyAsset("Assets/Scenes/PlaytestCourse.unity", ScenePath))
                throw new InvalidOperationException("Could not create " + ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var run = Object.FindFirstObjectByType<RunManager>();
            if (run == null) throw new InvalidOperationException("Course template has no RunManager");
            // The template's run, spawns, bomb, camera and networking are retained; generated geometry is replaced.
            var section = Object.FindObjectsByType<PlayerSpawn>(FindObjectsSortMode.None)
                .First(s => s.GetComponentInParent<Checkpoint>() == null).transform.parent;
            while (section.parent != null) section = section.parent;
            foreach (var child in section.Cast<Transform>().ToArray())
                if (child.GetComponentsInChildren<Checkpoint>(true).Length > 0 || child.GetComponent<KillZone>() != null)
                    Object.DestroyImmediate(child.gameObject);
            foreach (var decoration in scene.GetRootGameObjects().Where(g => g.GetComponentsInChildren<Renderer>(true).Length > 0 &&
                         g.GetComponentsInChildren<MonoBehaviour>(true).Length == 0).ToArray())
                Object.DestroyImmediate(decoration);
            var previous = section.GetComponent<CourseRoute>();
            if (previous != null) Object.DestroyImmediate(previous);
            Route.Clear(); Outdoors.Clear();
            RebuildGroup(section, "PatataWorks", root =>
            {
                Room(root, "00 Loading dock", Vector3.zero, 0, 48, 0, 11, Dock);
                Room(root, "01 Atrium", new Vector3(0, 0, 48), 0, 64, 14, 26, Atrium);
                Room(root, "02 Sorting hall", new Vector3(0, 14, 112), 90, 96, 0, 11, Sorting);
                Room(root, "03 Silo catwalks", new Vector3(96, 14, 112), 0, 64, 4, 12, Silos);
                Room(root, "04 Cold storage", new Vector3(96, 18, 176), 270, 168, 0, 11, Cold);
                Room(root, "05 Chute", new Vector3(-72, 18, 176), 180, 72, -28, 12, Chute);
                Room(root, "06a Furnace intake", new Vector3(-72, -10, 104), 270, 48, 0, 11, Furnace);
                Room(root, "06b Furnace loop", new Vector3(-120, -10, 104), 180, 64, 0, 11, FurnaceLoop);
                Room(root, "06c Boiler approach", new Vector3(-120, -10, 40), 90, 48, 0, 11, BoilerApproach);
                Room(root, "07 Boiler shaft", new Vector3(-72, -10, 40), 180, 24, 34, 48, Shaft);
                Room(root, "08 Roof", new Vector3(-72, 24, 16), 90, 96, 0, 0, Roof, true);
                Room(root, "08 Roof podium", new Vector3(24, 24, 16), 0, 32, 0, 0, Podium, true);
                // The facade encloses the connected factory wings and the spaces between them.
                Block(root, "Factory foundation", new Vector3(-12, -20, 88), new Vector3(244, 4, 208), KitRole.Brick);
                foreach (float x in new[] { -134f, 110f })
                    Block(root, "Outer facade", new Vector3(x, 2, 88), new Vector3(1, 40, 208), KitRole.Brick);
                foreach (float z in new[] { -16f, 192f })
                    Block(root, "Outer facade", new Vector3(-12, 2, z), new Vector3(244, 40, 1), KitRole.Brick);
                Kill(root, new Vector3(-12, -16, 88), new Vector3(244, 2, 208));
            });
            section.gameObject.AddComponent<CourseRoute>().Configure(Route.ToArray(), Outdoors.ToArray());
            LookBuilder.ApplyToScene(scene);
            Physics.SyncTransforms();
            ValidatePasses();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Register();
            AssetDatabase.SaveAssets();
            Debug.Log("[PatataWorksBuilder] Factory built: nine checkpoints, 52 m climb, 28 m descent.");
        }

        static void Room(Transform root, string name, Vector3 origin, float yaw, float length, float endHeight,
                         float ceiling, Action<Transform> contents, bool outdoors = false)
        {
            var room = new GameObject(name).transform;
            room.SetParent(root, false);
            contents(room);
            if (!outdoors)
            {
                float bottom = Mathf.Min(0, endHeight) - 4;
                // Side windows admit sunset. Ends stay open to the adjoining room and its turning landing.
                for (float z = 12; z < length - 12; z += 12)
                {
                    float span = Mathf.Min(12, length - 12 - z);
                    foreach (float x in new[] { -HalfWidth, HalfWidth })
                    {
                        Block(room, "Brick sill", new Vector3(x, bottom + 3, z + span / 2), new Vector3(1, 6, span), KitRole.Brick);
                        Block(room, "Brick lintel", new Vector3(x, ceiling - 2, z + span / 2), new Vector3(1, 4, span), KitRole.Brick);
                        Block(room, "Window mullion", new Vector3(x, (bottom + ceiling) / 2, z),
                            new Vector3(1, ceiling - bottom, 1), KitRole.Frame);
                    }
                }
                float roofEnd = name == "06c Boiler approach" ? length - 12 : length + 12;
                // Narrow roof-light slots above the window side; the central pass lane remains covered.
                Block(room, "Ceiling", new Vector3(-2.25f, ceiling + 0.5f, (roofEnd - 12) / 2), new Vector3(20.5f, 1, roofEnd + 12), KitRole.Ceiling).AddComponent<CourseCeiling>();
                Block(room, "Ceiling edge", new Vector3(11.25f, ceiling + 0.5f, (roofEnd - 12) / 2), new Vector3(2.5f, 1, roofEnd + 12), KitRole.Ceiling).AddComponent<CourseCeiling>();
                for (float z = -12; z < roofEnd; z += 24)
                {
                    float span = Mathf.Min(20, roofEnd - z);
                    Block(room, "Roof light frame", new Vector3(9, ceiling + 0.5f, z + span / 2), new Vector3(2, 1, span), KitRole.Frame).AddComponent<CourseCeiling>();
                }
                for (float z = 12; z < length; z += 24)
                {
                    var truss = Cube("Roof truss", room, new Vector3(0, ceiling - 0.6f, z), new Vector3(23, 1, 0.5f), KitRole.Truss, "Default");
                    truss.AddComponent<CourseDecoration>();
                    LookBuilder.FactoryLamp(room, new Vector3(0, Mathf.Min(ceiling - 2, endHeight + 6), z), name.Contains("Furnace"));
                }
            }
            // Route arrows use the existing pack, outside the throw windows.
            for (float z = 12; z < length; z += 24)
            {
                var sign = new GameObject("Route arrow").transform;
                sign.SetParent(room, false);
                sign.gameObject.AddComponent<CourseDecoration>();
                PatataParkBuilder.Prop(sign, "signage_arrow_stand", KitColor.Yellow,
                    new Vector3(10, Mathf.Lerp(0, endHeight, z / length), z), 180, Mat(KitMaterial));
            }
            room.SetPositionAndRotation(origin, Quaternion.Euler(0, yaw, 0));
            if (!outdoors)
                for (float z = 12; z < length; z += 24)
                    foreach (float x in new[] { -10.5f, 10.5f })
                    {
                        var foot = room.TransformPoint(new Vector3(x, Mathf.Min(0, endHeight) - 1, z));
                        float height = foot.y + 18;
                        if (height > 0)
                            Block(root, "Foundation column", new Vector3(foot.x, -18 + height / 2, foot.z), new Vector3(2, height, 2), KitRole.Pillar);
                    }
            Route.Add(origin); Outdoors.Add(outdoors);
            Route.Add(room.TransformPoint(new Vector3(0, endHeight, length))); Outdoors.Add(outdoors);
        }

        static void Floor(Transform p, string name, float a, float b, float y = 0, float width = 24, float x = 0) =>
            Block(p, name, new Vector3(x, y - 0.5f, (a + b) / 2), new Vector3(width, 1, b - a), KitRole.Floor);

        static void Ramp(Transform p, string name, Vector3 a, Vector3 b, float width, bool slide = false)
        {
            var rotation = Quaternion.LookRotation(b - a, Vector3.up);
            Block(p, name, (a + b) / 2 - rotation * Vector3.up * 0.5f,
                new Vector3(width, 1, Vector3.Distance(a, b)), slide ? KitRole.Slide : KitRole.Stairs, rotation, slide);
        }

        static void Pass(Transform p, string name, Vector3 from, Vector3 to, PassCorridor.ArcKind kind = PassCorridor.ArcKind.Normal,
                         bool timed = false, float opening = 0, float flight = 0)
        {
            var go = new GameObject("Pass " + name);
            go.transform.SetParent(p, false);
            go.transform.position = from;
            go.AddComponent<PassCorridor>().Configure(to, kind, flight, timed, opening);
        }

        static void CP(Transform p, int id, Vector3 position, float yaw = 0)
        {
            var go = AddCheckpoint(p, $"CP_{id:00}", id, position, id >= 7 ? 5f : 0);
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        static void Kill(Transform p, Vector3 position, Vector3 size)
        {
            var go = Place(p, GameplayDir + "KillZone", "Pit recovery", position, Quaternion.identity);
            go.transform.localScale = size;
        }

        static void Dock(Transform p)
        {
            Floor(p, "Loading dock", -12, 60);
            foreach (float z in new[] { 14f, 26f })
                foreach (float x in new[] { -8f, 8f })
                    Block(p, "Cargo", new Vector3(x, 1.5f, z), new Vector3(4, 3, 4), KitRole.Brick);
            var gate = Gate(p, "Dock gate", new Vector3(0, 0, 32), 2.5f, 8);
            Doorway(p, "Dock door", 40, 0, gate);
            Pass(p, "Dock warmup", new Vector3(-3, 1.5f, 10), new Vector3(3, 1.5f, 18));
            Pass(p, "Dock gate", new Vector3(0, 1.5f, 28), new Vector3(0, 1.5f, 36), opening: 2.4f);
            CP(p, 1, new Vector3(0, 0, 46));
        }

        static void Doorway(Transform p, string name, float z, float y, MonoBehaviour source)
        {
            Wall(p, name + " wall", z, y, 9, -12, 12, new[] { new Hole(-3, 3, 0, 5) });
            Actuator(p, Door, name, new Vector3(0, y + 2.5f, z), new Vector3(6, 5, 0.8f), new Vector3(0, 5.5f, 0), source);
        }

        static void Atrium(Transform p)
        {
            Floor(p, "Atrium entry", -12, 12);
            Ramp(p, "West stair ramp", new Vector3(-7, 0, 12), new Vector3(-7, 7, 30), 6);
            Floor(p, "Middle landing", 30, 38, 7);
            Ramp(p, "East stair ramp", new Vector3(7, 7, 38), new Vector3(7, 14, 56), 6);
            Floor(p, "Top landing", 56, 76, 14);
            var plate = Place(p, Plate, "Atrium receiver plate", new Vector3(-5, 7, 33), Quaternion.identity).GetComponent<PressurePlate>();
            var lift = Actuator(p, Lift, "Atrium plate lift", new Vector3(5, -0.25f, 23), new Vector3(6, 0.5f, 10), new Vector3(0, 7, 0), plate);
            SetField(lift.GetComponent<SignalActuator>(), "travelSeconds", v => v.floatValue = 2.5f);
            var gate = Gate(p, "Atrium return gate", new Vector3(-1, 7, 33), 2.5f, 10);
            gate.transform.rotation = Quaternion.Euler(0, 90, 0);
            // A return door between the lift and the public stairs; the stairs always let the plate holder rejoin with two players.
            Actuator(p, Door, "Atrium return door", new Vector3(7, 9.5f, 37), new Vector3(6, 5, 0.8f), new Vector3(0, 5.5f, 0), gate);
            Pass(p, "Atrium upward relay", new Vector3(-7, 5.8f, 23), new Vector3(-5, 8.5f, 33));
            Pass(p, "Atrium return ring", new Vector3(-5, 8.5f, 33), new Vector3(5, 8.5f, 33), opening: 2.4f);
            Pass(p, "Atrium top", new Vector3(7, 12.5f, 48), new Vector3(0, 15.5f, 58));
            Kill(p, new Vector3(0, -5, 34), new Vector3(24, 2, 44));
            CP(p, 2, new Vector3(0, 14, 62), 90);
        }

        static void Sorting(Transform p)
        {
            Floor(p, "Sorting entry", -12, 14);
            Floor(p, "Sorting exit", 78, 108);
            foreach (float x in new[] { -6f, 6f })
            {
                AddConveyor(p, "Sorting belt", new Vector3(x, -0.5f, 46), new Vector3(8, 1, 64), x < 0 ? 2 : -2);
                Zone(p, ZoneForbidden, "No carry inspection", new Vector3(x, 0, 52), new Vector3(8, 3, 3));
            }
            for (float z = 20; z < 76; z += 16)
                Block(p, "Lane divider", new Vector3(0, 3, z), new Vector3(1, 6, 8), KitRole.Brick);
            var tube = Place(p, Tube, "Sorting two routes", Vector3.zero, Quaternion.identity);
            ConfigureTube(tube, new[] {
                new TubeSlot(new Vector3(-6, 1.6f, 46), 0, new Vector3(6, 4, 57), new Vector3(6, 0.3f, 64), 0.9f),
                new TubeSlot(new Vector3(6, 1.6f, 46), 0, new Vector3(-6, 4, 57), new Vector3(-6, 0.3f, 64), 0.9f) });
            foreach (float x in new[] { -6f, 6f })
                Floor(p, "Sorting receiver platform", 60, 68, 0.3f, 8, x);
            TransitPasses(p, tube);
            var gate = Gate(p, "Sorting timed gate", new Vector3(0, 0, 81), 2.5f, 8);
            Doorway(p, "Sorting timed door", 90, 0, gate);
            AddMover(p, "Obstacle_Piston", "Sorting press", new Vector3(-6, 5, 72), new Vector3(-6, 1.7f, 72), new Vector3(7, 2, 2), 1, 0.2f, 0.4f);
            Pass(p, "Sorting window", new Vector3(-6, 1.5f, 30), new Vector3(6, 1.5f, 30), opening: 8);
            Pass(p, "Sorting ring", new Vector3(0, 1.5f, 77), new Vector3(0, 1.5f, 85), opening: 2.4f);
            Kill(p, new Vector3(0, -4, 46), new Vector3(24, 2, 64));
            CP(p, 3, new Vector3(0, 0, 95), -90);
        }

        static void Silos(Transform p)
        {
            Floor(p, "Silo entry", -12, 12);
            Ramp(p, "Silo climb", new Vector3(0, 0, 12), new Vector3(0, 4, 24), 8);
            Floor(p, "Silo low throw deck", 24, 36, 4, 10);
            Block(p, "Low throw ceiling", new Vector3(0, 8.5f, 30), new Vector3(12, 1, 12), KitRole.Ceiling).AddComponent<CourseCeiling>();
            for (int i = 0; i < 3; i++)
                Place(p, PlatformsDir + "Platform_Falling", "Silo falling grate", new Vector3(0, 3.75f, 39 + i * 6), Quaternion.identity);
            AddMover(p, "Platform_Moving", "Silo crane", new Vector3(-3, 3.5f, 53), new Vector3(3, 3.5f, 58), new Vector3(8, 1, 6), 2, 0, 0.25f);
            Floor(p, "Silo exit", 60, 76, 4);
            foreach (float x in new[] { -9f, 9f })
                for (float z = 24; z <= 60; z += 18)
                    Block(p, "Silo column", new Vector3(x, 2, z), new Vector3(3, 16, 3), KitRole.Pillar);
            Pass(p, "Low silo throw", new Vector3(0, 5.5f, 26), new Vector3(0, 5.5f, 34), PassCorridor.ArcKind.Low);
            Pass(p, "Falling relay", new Vector3(0, 5.5f, 39), new Vector3(0, 5.5f, 49), timed: true);
            Kill(p, new Vector3(0, -4, 42), new Vector3(24, 2, 48));
            CP(p, 4, new Vector3(0, 4, 63), -90);
        }

        static void Cold(Transform p)
        {
            Floor(p, "Cold storage", -12, 180);
            // The long left lane snakes through cold pockets. The straight right lane spends fuse in hot pipes.
            for (int i = 0; i < 6; i++)
            {
                float z = 24 + i * 20;
                Zone(p, ZoneCold, "Cold pocket", new Vector3(-6, 0, z), new Vector3(8, 3, 12));
                Zone(p, ZoneHot, "Hot shortcut", new Vector3(6, 0, z), new Vector3(7, 3, 12));
                Block(p, "Storage shelf", new Vector3(i % 2 == 0 ? -9 : -2, 2, z + 8), new Vector3(6, 4, 2), KitRole.Brick);
                Block(p, "Lane wall", new Vector3(0, 3, z), new Vector3(1, 6, 12), KitRole.Brick);
                Pass(p, "Cold handoff " + i, new Vector3(-6, 1.5f, z - 4), new Vector3(-6, 1.5f, z + 4));
            }
            LaserWall(p, "Cold laser window", 150, 0, 9, -4, new[] { 6f });
            Pass(p, "Cold laser", new Vector3(-4, 1.5f, 147), new Vector3(-4, 1.5f, 153), PassCorridor.ArcKind.Low, opening: 2);
            CP(p, 5, new Vector3(0, 0, 166), -90);
        }

        static void Chute(Transform p)
        {
            Floor(p, "Chute top", -12, 12);
            Ramp(p, "Mega slide", new Vector3(0, 0, 12), new Vector3(0, -28, 60), 14, true);
            Floor(p, "Chute catch court", 60, 84, -28);
            var tube = Place(p, Tube, "Chute tube", Vector3.zero, Quaternion.identity);
            ConfigureTube(tube, new[] { new TubeSlot(new Vector3(-5, 1.6f, 9), 0, new Vector3(-5, -24, 62), new Vector3(-5, -28, 69), 0.9f) });
            SetField(tube.GetComponent<BombTransit>(), "delay", v => v.floatValue = 4f);
            TransitPasses(p, tube);
            for (int i = 0; i < 3; i++)
            {
                float z = 24 + 12 * i, y = -(z - 12) * 28 / 48;
                Zone(p, ZoneForbidden, "Chute pass strip", new Vector3(0, y, z), new Vector3(14, 3, 2));
            }
            Kill(p, new Vector3(0, -34, 40), new Vector3(24, 2, 72));
            CP(p, 6, new Vector3(0, -28, 71), 90);
        }

        static void Furnace(Transform p)
        {
            Floor(p, "Furnace intake", -12, 60);
            Zone(p, ZoneHot, "Furnace heat", new Vector3(0, 0, 26), new Vector3(23, 4, 22));
            AddCrusher(p, "Furnace press", new Vector3(0, 0, 24), 10, 3, 0.2f);
            Pass(p, "Furnace press timing", new Vector3(0, 1.5f, 19), new Vector3(0, 1.5f, 29), timed: true);
            for (float z = 18; z < 44; z += 16)
                Block(p, "Boiler casing", new Vector3(-9, 4, z), new Vector3(4, 8, 8), KitRole.Hazard);
        }

        static void FurnaceLoop(Transform p)
        {
            Floor(p, "Furnace split court", -12, 76);
            Wall(p, "Furnace wall", 32, 0, 10, -12, 12, new[] { new Hole(-7, -3, 1, 5), new Hole(4, 8, 0, 2.6f) });
            Place(p, ObstaclesDir + "Obstacle_Windmill", "Furnace windmill", new Vector3(-5, 3, 31.2f), Quaternion.identity);
            AddCrusher(p, "Furnace tunnel press", new Vector3(6, 0, 32), 3.8f, 2, 0.4f);
            Pass(p, "Windmill window", new Vector3(-5, 2.2f, 27), new Vector3(-5, 2.2f, 37), PassCorridor.ArcKind.Low, true, 4);
            Zone(p, ZoneCold, "Furnace respite", new Vector3(-5, 0, 42), new Vector3(6, 3, 6));
        }

        static void BoilerApproach(Transform p)
        {
            Floor(p, "Boiler approach", -12, 60);
            AddRotator(p, ObstaclesDir + "Obstacle_Sweeper", "Boiler sweeper", new Vector3(0, 0, 24), 18, 75, 0);
            Pass(p, "Boiler relay", new Vector3(-4, 1.5f, 32), new Vector3(4, 1.5f, 38));
            CP(p, 7, new Vector3(0, 0, 46), 90);
        }

        static void Shaft(Transform p)
        {
            Floor(p, "Shaft base", -12, 36);
            // Broad lifts share fixed side landings. Either runner can climb first; no plate must be held below.
            for (int i = 0; i < 4; i++)
            {
                float low = i * 8.5f, high = (i + 1) * 8.5f;
                float x = i % 2 == 0 ? -7 : 7, z = 4 + i * 5;
                AddMover(p, "Platform_Moving", "Boiler rising platform " + i,
                    new Vector3(x, low - 0.5f, z), new Vector3(x, high - 0.5f, z), new Vector3(7, 1, 7), 4, 0, 0.3f);
                Floor(p, "Boiler west landing " + i, z + 4, 36, high, 8, -7);
                Floor(p, "Boiler east landing " + i, z + 4, 36, high, 8, 7);
                Floor(p, "Boiler landing bridge " + i, 32, 36, high, 22);
            }
            Floor(p, "Shaft upper catch court", 22, 28, 34);
            Floor(p, "Shaft upper bridge", 32, 36, 34);
            // Both runners relay up the first three lifts. The cannon on the penultimate landing covers the final rise:
            // nobody must hold a six-second fuse at the bottom while the only receiver climbs all 34 metres.
            var cannon = Place(p, Cannon, "Boiler cannon", new Vector3(0, 25.5f, 34), Quaternion.identity);
            ConfigureCannon(cannon, new Vector3(0, 34, 25), 1.6f);
            TransitPasses(p, cannon);
            CP(p, 8, new Vector3(0, 34, 27), -90);
        }

        static void Roof(Transform p)
        {
            // Match the shaft's launch opening in the roof slab above the penultimate landing.
            Floor(p, "Shaft roof doorway west", -12, -2, 0, 16, 4);
            Floor(p, "Shaft roof doorway east", 2, 12, 0, 16, 4);
            Floor(p, "Cannon opening north edge", -2, 2, 0, 8, 0);
            Floor(p, "Cannon opening south edge", -2, 2, 0, 4, 10);
            Floor(p, "Factory roof", 12, 108);
            foreach (float z in new[] { 22f, 46f, 70f })
            {
                Block(p, "Chimney", new Vector3(-8, 4, z), new Vector3(4, 8, 4), KitRole.Brick);
                Block(p, "Chimney", new Vector3(8, 4, z), new Vector3(4, 8, 4), KitRole.Brick);
                Pass(p, "Sunset relay " + z, new Vector3(-3, 1.5f, z), new Vector3(3, 1.5f, z + 8));
            }
            foreach (float x in new[] { -11.5f, 11.5f })
                Block(p, "Roof railing", new Vector3(x, 0.6f, 48), new Vector3(0.5f, 1.2f, 72), KitRole.Railing);
            ArchCheckpoint(p, "CP_09", 9, new Vector3(0, 0, 89), 5, new Vector3(0, 0, 84));
            Pass(p, "Roof checkpoint arch", new Vector3(0, 1.5f, 80), new Vector3(0, 1.5f, 89), opening: 4);
        }

        static void Podium(Transform p)
        {
            Floor(p, "Roof finish", -12, 40);
            Place(p, GameplayDir + "FinishZone", "FinishZone", new Vector3(0, 0, 28), Quaternion.identity);
            var sign = new GameObject("PatataWorks sign").transform;
            sign.SetParent(p, false);
            sign.gameObject.AddComponent<CourseDecoration>();
            PatataParkBuilder.Prop(sign, "signage_finish_wide", KitColor.Neutral, new Vector3(0, 0, 34), 180, Mat(KitMaterial));
            var board = new GameObject("Factory name board");
            board.transform.SetParent(p, false);
            board.transform.localPosition = new Vector3(0, 10, 34);
            var doc = board.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Resources/HotPatataWorldPanel.asset");
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/World/PatataWorksSign.uxml");
            doc.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            doc.worldSpaceSize = new Vector2(3200, 800);
            doc.pivot = Pivot.Center;
            foreach (float x in new[] { -9f, 9f })
                Block(p, "Sign support", new Vector3(x, 6, 34), new Vector3(1, 12, 1), KitRole.Pillar);
        }

        static void TransitPasses(Transform parent, GameObject go)
        {
            var transit = go.GetComponent<BombTransit>();
            for (int i = 0; i < transit.ExitCount; i++)
            {
                var e = transit.GetExit(i);
                if (!e.pad.gameObject.activeInHierarchy) continue;
                Pass(parent, go.name + " exit " + i, e.muzzle.position, transit.AimPoint(i), PassCorridor.ArcKind.Fixed, flight: e.flightTime);
            }
        }

        public static void ValidatePasses()
        {
            var samples = new List<Vector3>();
            foreach (var corridor in Object.FindObjectsByType<PassCorridor>(FindObjectsSortMode.None))
            {
                if (!corridor.TrySample(Tune, samples)) throw new InvalidOperationException("Unreachable " + corridor.name);
                if (corridor.Opening > 0 && corridor.Opening < Tune.catchRadius + PassCorridor.OpeningMargin)
                    throw new InvalidOperationException("Opening too small: " + corridor.name);
                foreach (var point in samples)
                    foreach (var hit in Physics.RaycastAll(point, Vector3.up, PassCorridor.CeilingMargin,
                        LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore))
                        if (hit.collider.GetComponentInParent<CourseCeiling>() != null)
                            throw new InvalidOperationException($"Ceiling clearance: {corridor.name} at {point} hits {hit.collider.name}");
            }
        }

        static void Register()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            var root = PrefabUtility.LoadPrefabContents(NetworkPrefab);
            try
            {
                var so = new SerializedObject(root.GetComponent<NetworkBootstrap>());
                var names = so.FindProperty("gameplayScenes");
                var counts = so.FindProperty("sceneCheckpoints");
                int existing = -1;
                for (int i = 0; i < names.arraySize; i++)
                    if (names.GetArrayElementAtIndex(i).stringValue == "PatataWorks") existing = i;
                if (existing < 0)
                {
                    names.InsertArrayElementAtIndex(0); counts.InsertArrayElementAtIndex(0);
                }
                else { names.MoveArrayElement(existing, 0); counts.MoveArrayElement(existing, 0); }
                names.GetArrayElementAtIndex(0).stringValue = "PatataWorks";
                counts.GetArrayElementAtIndex(0).intValue = 9;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, NetworkPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
