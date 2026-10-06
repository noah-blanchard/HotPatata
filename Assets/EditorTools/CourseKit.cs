using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>What a kit box is for; each role has one KayKit look (<see cref="CourseKit.Look"/>, ARCHITECTURE §25.1).</summary>
    public enum KitRole { Ground, Wall, Mover, Falling, Belt, Slide, Hazard, Gate, Floor, Ceiling, Brick, Frame, Stairs, Pillar, Truss, Railing, Roof, Grating, Rubber, Accent, Lamp, Glow, Rust }

    /// <summary>
    /// Shared editor helpers for the course and kit builders (<see cref="KitPrefabBuilder"/>,
    /// <c>BombObstacleKitBuilder</c>, <c>IndustrialPlantBuilder</c>, <c>PatataWildsBuilder</c>): placing kit prefabs, sizing movers,
    /// writing serialized fields, materials, primitives and the collider-free backdrop.
    /// </summary>
    public static class CourseKit
    {
        public const string MatDir = "Assets/Art/Materials/";
        public const string PlatformsDir = "Assets/Prefabs/Platforms/";
        public const string ObstaclesDir = "Assets/Prefabs/Obstacles/";
        public const string GameplayDir = "Assets/Prefabs/Gameplay/";

        /// <summary>Crusher underside above the floor when down: a crouched or sliding player (1.0 m) fits, a standing one (1.8 m) does not.</summary>
        public const float CrusherLowClearance = 1.45f;

        // ------------------------------------------------------------------ the menu's level list

        /// <summary>Where a course goes in the Bootstrap level list: first, kept where it is (second when new), or last.</summary>
        public enum MenuSlot { First, Keep, Last }

        public const string NetworkPrefab = "Assets/Prefabs/Network/NetworkManager.prefab";

        /// <summary>
        /// Lists a course in the build settings and in the menu's level list (NetworkBootstrap) with its checkpoint count. One shared
        /// place, so builders never fight over the order: PatataWilds is first, PatataCanopy and the plant keep their places
        /// (a new one goes second), PassSandbox is last.
        /// </summary>
        public static void RegisterInMenu(string sceneName, string scenePath, int checkpoints, MenuSlot slot)
        {
            var scenes = UnityEditor.EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == scenePath)) scenes.Add(new UnityEditor.EditorBuildSettingsScene(scenePath, true));
            UnityEditor.EditorBuildSettings.scenes = scenes.ToArray();
            var root = PrefabUtility.LoadPrefabContents(NetworkPrefab);
            try
            {
                var so = new SerializedObject(root.GetComponent<NetworkBootstrap>());
                var names = so.FindProperty("gameplayScenes");
                var counts = so.FindProperty("sceneCheckpoints");
                counts.arraySize = Mathf.Max(counts.arraySize, names.arraySize);
                int index = -1;
                for (int i = 0; i < names.arraySize; i++)
                    if (names.GetArrayElementAtIndex(i).stringValue == sceneName) index = i;
                int target = slot == MenuSlot.First ? 0 : slot == MenuSlot.Last ? names.arraySize - (index >= 0 ? 1 : 0)
                           : index >= 0 ? index : Mathf.Min(1, names.arraySize);
                if (index < 0)
                {
                    names.InsertArrayElementAtIndex(Mathf.Min(target, names.arraySize));
                    counts.InsertArrayElementAtIndex(Mathf.Min(target, counts.arraySize));
                    index = Mathf.Min(target, names.arraySize - 1);
                }
                else if (index != target)
                {
                    names.MoveArrayElement(index, target);
                    counts.MoveArrayElement(index, target);
                    index = target;
                }
                names.GetArrayElementAtIndex(index).stringValue = sceneName;
                counts.GetArrayElementAtIndex(index).intValue = checkpoints;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, NetworkPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ------------------------------------------------------------------ course scenes

        /// <summary>The course a missing course scene is copied from (its run, spawns, bomb, camera and networking).</summary>
        public const string CourseTemplatePath = "Assets/Scenes/IndustrialPlant.unity";

        /// <summary>
        /// Opens <paramref name="scenePath"/> (copied from <see cref="CourseTemplatePath"/> when missing) and clears its generated
        /// geometry: the run, spawns, bomb, camera and networking stay. Returns the section to build in.
        /// </summary>
        public static UnityEngine.SceneManagement.Scene PrepareCourseScene(string scenePath, out Transform section)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null && !AssetDatabase.CopyAsset(CourseTemplatePath, scenePath))
                throw new InvalidOperationException("Could not create " + scenePath);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            var run = Object.FindFirstObjectByType<RunManager>();
            if (run == null) throw new InvalidOperationException("Course template has no RunManager");
            section = Object.FindObjectsByType<PlayerSpawn>(FindObjectsSortMode.None)
                .First(sp => sp.GetComponentInParent<Checkpoint>() == null).transform.parent;
            while (section.parent != null) section = section.parent;
            foreach (var child in section.Cast<Transform>().ToArray())
                if (child.GetComponentsInChildren<Checkpoint>(true).Length > 0 || child.GetComponent<KillZone>() != null)
                    Object.DestroyImmediate(child.gameObject);
            foreach (var decoration in scene.GetRootGameObjects().Where(g => g.GetComponentsInChildren<Renderer>(true).Length > 0 &&
                         g.GetComponentsInChildren<MonoBehaviour>(true).Length == 0).ToArray())
                Object.DestroyImmediate(decoration);
            var previous = section.GetComponent<CourseRoute>();
            if (previous != null) Object.DestroyImmediate(previous);
            return scene;
        }

        /// <summary>
        /// Every declared pass of the open scene is reachable, its opening is wider than the catch radius plus the margin, and
        /// no sampled point has a structural ceiling within <see cref="PassCorridor.CeilingMargin"/> above it. Throws otherwise.
        /// </summary>
        public static void ValidatePasses()
        {
            var tune = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/ScriptableObjects/Tuning/GameTuning.asset");
            var samples = new List<Vector3>();
            foreach (var corridor in Object.FindObjectsByType<PassCorridor>(FindObjectsSortMode.None))
            {
                if (!corridor.TrySample(tune, samples)) throw new InvalidOperationException("Unreachable " + corridor.name);
                if (corridor.Opening > 0 && corridor.Opening < tune.catchRadius + PassCorridor.OpeningMargin)
                    throw new InvalidOperationException("Opening too small: " + corridor.name);
                foreach (var point in samples)
                    foreach (var hit in Physics.RaycastAll(point, Vector3.up, PassCorridor.CeilingMargin,
                        LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore))
                        if (hit.collider.GetComponentInParent<CourseCeiling>() != null)
                            throw new InvalidOperationException($"Ceiling clearance: {corridor.name} at {point} hits {hit.collider.name}");
            }
        }

        // ------------------------------------------------------------------ groups

        public static void RebuildGroup(Transform course, string name, Action<Transform> build)
        {
            var old = course.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(name).transform;
            root.SetParent(course, false);
            build(root);
        }

        // ------------------------------------------------------------------ placing kit prefabs

        public static GameObject Place(Transform parent, string prefabPathNoExt, string name, Vector3 position, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPathNoExt + ".prefab")
                         ?? throw new InvalidOperationException("missing prefab " + prefabPathNoExt);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        /// <summary>A Platform_Basic box: pivot at its centre, scale = size, drawn in the KayKit pieces of <paramref name="role"/>.</summary>
        public static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, KitRole role, Quaternion? rotation = null,
                                       bool flip = false)
        {
            var go = Place(parent, PlatformsDir + "Platform_Basic", name, center, rotation ?? Quaternion.identity);
            go.transform.localScale = size;
            if (role != KitRole.Ground || flip || currentLookSet != LookSet.KayKit) Skin(go.transform.Find("Visual").gameObject, role, flip);
            return go;
        }

        public static GameObject AddCheckpoint(Transform parent, string name, int id, Vector3 position, float fuse)
        {
            var go = Place(parent, GameplayDir + "Checkpoint", name, position, Quaternion.identity);
            var cp = go.GetComponent<Checkpoint>();
            SetField(cp, "id", p => p.intValue = id);
            SetField(cp, "holdFuseOverride", p => p.floatValue = fuse);
            return go;
        }

        public static GameObject AddConveyor(Transform parent, string name, Vector3 center, Vector3 size, float speed)
        {
            var go = Place(parent, PlatformsDir + "Platform_Conveyor", name, center, Quaternion.identity);
            go.transform.localScale = size;
            SetField(go.GetComponent<Conveyor>(), "speed", p => p.floatValue = speed);
            Skin(go.transform.Find("Visual").gameObject, KitRole.Belt, flip: speed > 0f);   // the chevrons point the way it carries
            return go;
        }

        /// <summary>A MovingPlatform-based prefab: its Platform child is resized to <paramref name="size"/>.</summary>
        public static GameObject AddMover(Transform parent, string prefabName, string name, Vector3 a, Vector3 b, Vector3 size,
                                          float speed, float phase, float dwell)
        {
            string dir = prefabName.StartsWith("Platform_") ? PlatformsDir : ObstaclesDir;
            var go = Place(parent, dir + prefabName, name, a, Quaternion.identity);
            var mp = go.GetComponent<MovingPlatform>();
            mp.WaypointA.position = a;
            mp.WaypointB.position = b;
            var platform = mp.Platform;
            platform.position = a;
            platform.Find("Visual").localScale = size;   // a kit box (Visual + Collision); a variant with its own visual is placed as it is
            platform.Find("Collision").GetComponent<BoxCollider>().size = size;
            SetField(mp, "speed", p => p.floatValue = speed);
            SetField(mp, "startPhase", p => p.floatValue = phase);
            SetField(mp, "motion", p => p.enumValueIndex = (int)MovingPlatform.Motion.Dwell);
            SetField(mp, "dwellFraction", p => p.floatValue = dwell);
            return go;
        }

        /// <summary>
        /// A crusher slab whose underside stops <see cref="CrusherLowClearance"/> above <paramref name="floor"/>: a
        /// crouched or sliding player (1.0 m) fits, a standing one (1.8 m) is caught by the lethal strip.
        /// </summary>
        public static GameObject AddCrusher(Transform parent, string name, Vector3 floor, float width, float length, float phase)
        {
            const float thickness = 1.5f;
            float low = floor.y + CrusherLowClearance + thickness / 2f;
            float high = floor.y + 4.2f + thickness / 2f;
            var a = new Vector3(floor.x, high, floor.z);
            var b = new Vector3(floor.x, low, floor.z);
            var go = Place(parent, ObstaclesDir + "Obstacle_Crusher", name, a, Quaternion.identity);
            var mover = go.GetComponent<MovingPlatform>();
            mover.WaypointA.position = a;
            mover.WaypointB.position = b;
            var platform = mover.Platform;
            platform.position = a;
            var size = new Vector3(width, thickness, length);
            platform.Find("Visual").localScale = size;
            platform.Find("Collision").GetComponent<BoxCollider>().size = size;
            var kill = platform.Find("Kill");
            kill.localPosition = new Vector3(0f, -thickness / 2f - 0.05f, 0f);
            kill.GetComponent<BoxCollider>().size = new Vector3(width - 0.2f, 0.3f, length - 0.2f);   // reaches 0.2 m below the slab
            SetField(mover, "startPhase", p => p.floatValue = phase);
            return go;
        }

        /// <summary>A RotatingObstacle prefab; a non-zero <paramref name="length"/> resizes a sweeper's bar.</summary>
        public static GameObject AddRotator(Transform parent, string prefabPath, string name, Vector3 position, float length, float degreesPerSecond, float phase)
        {
            var go = Place(parent, prefabPath, name, position, Quaternion.identity);
            var rot = go.GetComponent<RotatingObstacle>();
            SetField(rot, "degreesPerSecond", p => p.floatValue = degreesPerSecond);
            SetField(rot, "phaseDegrees", p => p.floatValue = phase);
            if (length > 0f) ResizeSweeper(go.transform, length);
            return go;
        }

        public static void ResizeSweeper(Transform sweeper, float length)
        {
            var v = sweeper.Find("Visual");
            v.localScale = new Vector3(length, v.localScale.y, v.localScale.z);
            var c = sweeper.Find("Collision").GetComponent<BoxCollider>();
            c.size = new Vector3(length, c.size.y, c.size.z);
            var k = sweeper.Find("Kill").GetComponent<BoxCollider>();
            k.size = new Vector3(length + 0.2f, k.size.y, k.size.z);
        }

        public static void SetField(Object target, string field, Action<SerializedProperty> set)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} not found");
            set(p);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetReference(Object target, string field, Object value) => SetField(target, field, p => p.objectReferenceValue = value);

        // ------------------------------------------------------------------ course pieces (bomb obstacles, #68)

        public const float Window = 2.4f, WindowBottom = 1f, WindowTop = 3f;   // the standard throw window in a wall
        public const float Passage = 3f, PassageHeight = 3.5f;                  // a laser-curtain passage for runners (curtain + posts)

        public static GameObject Zone(Transform parent, string prefab, string name, Vector3 floorCenter, Vector3 size)
        {
            var go = Place(parent, prefab, name, floorCenter, Quaternion.identity);
            BombObstacleKitBuilder.ResizeZone(go, size);
            return go;
        }

        /// <summary>A bomb-gate ring centred at <paramref name="height"/> above <paramref name="floor"/>, on a thin pillar.</summary>
        public static BombGate Gate(Transform parent, string name, Vector3 floor, float height, float holdSeconds)
        {
            var gate = Place(parent, BombObstacleKitBuilder.GateRing, name, floor + Vector3.up * height, Quaternion.identity).GetComponent<BombGate>();
            SetField(gate, "holdSeconds", p => p.floatValue = holdSeconds);
            float pillar = height - 1.45f;   // up to the underside of the ring
            if (pillar > 0.1f)
                Block(parent, name + "_Pillar", floor + Vector3.up * (pillar / 2f), new Vector3(0.35f, pillar, 0.35f), KitRole.Gate);
            return gate;
        }

        public static GameObject Actuator(Transform parent, string prefab, string name, Vector3 closedCenter, Vector3 size, Vector3 travel, MonoBehaviour source)
        {
            var go = Place(parent, prefab, name, closedCenter, Quaternion.identity);
            BombObstacleKitBuilder.ResizeActuator(go, size, travel);
            BombObstacleKitBuilder.Wire(go, source);
            return go;
        }

        public static void ArchCheckpoint(Transform parent, string name, int id, Vector3 pad, float fuse, Vector3 archFloor)
        {
            var cp = AddCheckpoint(parent, name, id, pad, fuse).GetComponent<Checkpoint>();
            var arch = Place(parent, BombObstacleKitBuilder.GateArch, name + "_Arch", archFloor, Quaternion.identity).GetComponent<BombGate>();
            SetReference(cp, "claimGate", arch);
        }

        /// <summary>An opening in a wall, in wall space: x across the course, y above the wall's floor.</summary>
        public struct Hole
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
        public static void Wall(Transform parent, string name, float z, float floorY, float height, float x0, float x1, IList<Hole> holes)
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
                    Block(parent, $"{name}_{++piece}", new Vector3(mid, floorY + (y0 + y1) / 2f, z), new Vector3(b - a, y1 - y0, 1f), KitRole.Wall);
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
        public static void LaserWall(Transform parent, string name, float z, float floorY, float height, float? windowX, float[] passages, bool hoop = false)
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
                var curtain = Place(parent, BombObstacleKitBuilder.LaserCurtain, $"{name}_Curtain_{i + 1}", new Vector3(passages[i], floorY, z), Quaternion.identity);
                BombObstacleKitBuilder.ResizeCurtain(curtain, Passage - 0.6f, PassageHeight - 0.3f);
            }
            if (hoop && windowX.HasValue)
                AddRotator(parent, ObstaclesDir + "Obstacle_Hoop", name + "_Hoop", new Vector3(windowX.Value, floorY + 2.4f, z), 0f, 70f, 90f);
        }

        // ------------------------------------------------------------------ KayKit skin (ARCHITECTURE §25.1)

        /// <summary>A cylinder primitive's box in its own space (the unit box of a skinned cylinder).</summary>
        public static readonly Vector3 CylinderBox = new Vector3(1f, 2f, 1f);

        /// <summary>KayKit chevrons point to -Z; a flipped Arrow skin points them to +Z (belts and slides running forward).</summary>
        public const bool SlideFlip = true;

        public const string PalettePath = "Assets/ScriptableObjects/Kit/KayKitPalette.asset";
        public const string KitMaterial = "KayKit_Toon", KitHazardMaterial = "KayKit_Hazard", KitBeltMaterial = "KayKit_Belt";

        /// <summary>The look set the kit draws with: KayKit everywhere, Industrial (ARCHITECTURE §25.2) or Nature (§25.3) only where a builder asks.</summary>
        public enum LookSet { KayKit, Industrial, Nature }

        static LookSet currentLookSet = LookSet.KayKit;

        public static LookSet CurrentLookSet => currentLookSet;

        sealed class LookScope : IDisposable
        {
            readonly LookSet previous;
            public LookScope(LookSet set) { previous = currentLookSet; currentLookSet = set; }
            public void Dispose() => currentLookSet = previous;
        }

        /// <summary>Draws every <see cref="Look"/> inside the scope with <paramref name="set"/>: <c>using (UseLookSet(LookSet.Industrial)) { ... }</c>.</summary>
        public static IDisposable UseLookSet(LookSet set) => new LookScope(set);

        /// <summary>The concrete-like surfaces of a room (ARCHITECTURE §25.2): floors, walls and ceilings take their material from the current theme.</summary>
        public struct SurfaceTheme
        {
            public string Floor, Wall, Ceiling;
            public SurfaceTheme(string floor, string wall, string ceiling) { Floor = floor; Wall = wall; Ceiling = ceiling; }
        }

        static SurfaceTheme currentTheme = new SurfaceTheme(IndustrialDir + "Industrial_Concrete", IndustrialDir + "Industrial_Concrete", IndustrialDir + "Industrial_Concrete");

        public static SurfaceTheme CurrentTheme => currentTheme;

        sealed class ThemeScope : IDisposable
        {
            readonly SurfaceTheme previous;
            public ThemeScope(SurfaceTheme theme) { previous = currentTheme; currentTheme = theme; }
            public void Dispose() => currentTheme = previous;
        }

        /// <summary>Draws the floor, wall and ceiling roles inside the scope with the theme's materials (industrial and nature looks).</summary>
        public static IDisposable UseTheme(SurfaceTheme theme) => new ThemeScope(theme);

        public const string IndustrialDir = "Industrial/";
        public const string ConcreteMaterial = IndustrialDir + "Industrial_Concrete", PaintedMetalMaterial = IndustrialDir + "Industrial_PaintedMetal",
            PaintedMetalYellowMaterial = IndustrialDir + "Industrial_PaintedMetal_Yellow", PaintedMetalSafetyMaterial = IndustrialDir + "Industrial_PaintedMetal_Safety",
            LampMaterial = IndustrialDir + "Industrial_Lamp", GlowMaterial = IndustrialDir + "Industrial_Glow", RawMetalMaterial = IndustrialDir + "Industrial_RawMetal", RubberMaterial = IndustrialDir + "Industrial_Rubber";

        /// <summary>One look per role of the course: the piece family, the KayKit colour and the material, in the current look set.</summary>
        public static (KitShape shape, KitColor color, string material) Look(KitRole role) =>
            currentLookSet == LookSet.Industrial ? IndustrialLook(role) : currentLookSet == LookSet.Nature ? NatureLook(role) : KayKitLook(role);

        public const string NatureDir = "Nature/";

        /// <summary>
        /// The nature look (ARCHITECTURE §25.3, PatataWilds): rough rock slabs in the theme's ground, cliff and cave-roof materials,
        /// logs (rafts, posts, palisades, rams) and planks (bridges, decks). Hazards are rust-stained logs with charred bands (the
        /// bands stop them relying on colour alone, spec §19); falling platforms are pale rotten planks, always the same.
        /// </summary>
        static (KitShape shape, KitColor color, string material) NatureLook(KitRole role) => role switch
        {
            KitRole.Ground or KitRole.Floor or KitRole.Stairs or KitRole.Roof => (KitShape.RoughBox, KitColor.Neutral, currentTheme.Floor),
            KitRole.Wall or KitRole.Brick => (KitShape.RoughBox, KitColor.Neutral, currentTheme.Wall),
            KitRole.Ceiling => (KitShape.RoughBox, KitColor.Neutral, currentTheme.Ceiling),
            KitRole.Mover => (KitShape.Logs, KitColor.Neutral, NatureDir + "Nature_BarkBrown"),
            KitRole.Pillar => (KitShape.Logs, KitColor.Neutral, NatureDir + "Nature_PineBark"),
            KitRole.Truss or KitRole.Rust => (KitShape.Logs, KitColor.Neutral, NatureDir + "Nature_BarkDark"),
            KitRole.Frame or KitRole.Railing => (KitShape.Logs, KitColor.Neutral, NatureDir + "Nature_RoughWood"),
            KitRole.Gate => (KitShape.Logs, KitColor.Neutral, NatureDir + "Nature_RoughWood"),
            KitRole.Accent => (KitShape.Planks, KitColor.Neutral, NatureDir + NatureMaterialBuilder.BlazeName),
            KitRole.Hazard => (KitShape.Logs, KitColor.Neutral, NatureDir + NatureMaterialBuilder.HazardName),
            KitRole.Falling => (KitShape.Planks, KitColor.Neutral, NatureDir + NatureMaterialBuilder.FallingName),
            KitRole.Lamp => (KitShape.BevelBox, KitColor.Neutral, NatureDir + NatureMaterialBuilder.LanternName),
            KitRole.Glow => (KitShape.BevelBox, KitColor.Neutral, NatureDir + NatureMaterialBuilder.EmberName),
            KitRole.Slide => (KitShape.RoughBox, KitColor.Neutral, NatureDir + "Nature_Mud"),
            KitRole.Grating => (KitShape.Planks, KitColor.Neutral, NatureDir + "Nature_Planks"),
            KitRole.Belt => (KitShape.Logs, KitColor.Neutral, NatureDir + NatureMaterialBuilder.BeltName),
            KitRole.Rubber => (KitShape.Planks, KitColor.Neutral, NatureDir + "Nature_MossWood"),
            _ => KayKitLook(role)
        };

        /// <summary>
        /// The industrial look (ARCHITECTURE §25.2): bevelled boxes in the room theme's floor, wall and ceiling materials, painted, raw
        /// and rusty metal, and rubber. Hazards are striped red paint (the stripes stop them relying on colour alone, spec §19);
        /// falling platforms are yellow paint, as in KayKit.
        /// </summary>
        static (KitShape shape, KitColor color, string material) IndustrialLook(KitRole role) => role switch
        {
            KitRole.Ground or KitRole.Floor or KitRole.Stairs or KitRole.Roof => (KitShape.BevelBox, KitColor.Neutral, currentTheme.Floor),
            KitRole.Wall or KitRole.Brick => (KitShape.BevelBox, KitColor.Neutral, currentTheme.Wall),
            KitRole.Ceiling => (KitShape.BevelBox, KitColor.Neutral, currentTheme.Ceiling),
            KitRole.Rust => (KitShape.BevelBox, KitColor.Neutral, IndustrialDir + "Industrial_Metal_Rust"),
            KitRole.Mover or KitRole.Pillar or KitRole.Truss => (KitShape.BevelBox, KitColor.Neutral, PaintedMetalMaterial),
            KitRole.Frame or KitRole.Railing or KitRole.Gate => (KitShape.BevelBox, KitColor.Neutral, PaintedMetalYellowMaterial),
            KitRole.Accent => (KitShape.BevelBox, KitColor.Neutral, PaintedMetalSafetyMaterial),
            KitRole.Hazard => (KitShape.BevelBox, KitColor.Neutral, IndustrialDir + "Industrial_Hazard"),
            KitRole.Falling => (KitShape.BevelBox, KitColor.Neutral, PaintedMetalYellowMaterial),
            KitRole.Lamp => (KitShape.BevelBox, KitColor.Neutral, LampMaterial),
            KitRole.Glow => (KitShape.BevelBox, KitColor.Neutral, GlowMaterial),
            KitRole.Slide or KitRole.Grating => (KitShape.BevelBox, KitColor.Neutral, RawMetalMaterial),
            KitRole.Belt or KitRole.Rubber => (KitShape.BevelBox, KitColor.Neutral, RubberMaterial),
            _ => KayKitLook(role)
        };

        static (KitShape shape, KitColor color, string material) KayKitLook(KitRole role) => role switch
        {
            KitRole.Ground => (KitShape.Platform, KitColor.Green, KitMaterial),
            KitRole.Wall => (KitShape.Barrier, KitColor.Neutral, KitMaterial),
            KitRole.Mover => (KitShape.Platform, KitColor.Blue, KitMaterial),
            KitRole.Falling => (KitShape.Platform, KitColor.Yellow, KitMaterial),
            KitRole.Belt => (KitShape.Arrow, KitColor.Blue, KitBeltMaterial),
            KitRole.Slide => (KitShape.Arrow, KitColor.Green, KitMaterial),
            KitRole.Hazard => (KitShape.Barrier, KitColor.Red, KitHazardMaterial),
            KitRole.Gate => (KitShape.Barrier, KitColor.Yellow, KitMaterial),
            KitRole.Floor => (KitShape.Floor, KitColor.Neutral, KitMaterial),
            KitRole.Ceiling => (KitShape.Floor, KitColor.Neutral, KitMaterial),
            KitRole.Brick => (KitShape.Barrier, KitColor.Neutral, "KayKit_Brick"),
            KitRole.Frame => (KitShape.Barrier, KitColor.Yellow, KitMaterial),
            KitRole.Stairs => (KitShape.Platform, KitColor.Neutral, KitMaterial),
            KitRole.Pillar => (KitShape.Pillar, KitColor.Neutral, KitMaterial),
            KitRole.Truss => (KitShape.Strut, KitColor.Neutral, KitMaterial),
            KitRole.Railing => (KitShape.Barrier, KitColor.Neutral, KitMaterial),
            KitRole.Roof => (KitShape.Platform, KitColor.Neutral, KitMaterial),
            KitRole.Grating => (KitShape.Platform, KitColor.Neutral, KitMaterial),
            KitRole.Rubber => (KitShape.Platform, KitColor.Neutral, KitMaterial),
            KitRole.Accent => (KitShape.Barrier, KitColor.Yellow, KitMaterial),
            KitRole.Lamp => (KitShape.Barrier, KitColor.Yellow, KitMaterial),
            KitRole.Glow => (KitShape.Barrier, KitColor.Yellow, KitMaterial),
            KitRole.Rust => (KitShape.Barrier, KitColor.Neutral, KitMaterial),
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };

        public static KitPalette Palette =>
            AssetDatabase.LoadAssetAtPath<KitPalette>(PalettePath) ?? throw new InvalidOperationException("missing " + PalettePath + " (run HotPatata/Course/Build KayKit Kit)");

        /// <summary>Draws <paramref name="visual"/>'s box (its scale times <paramref name="unitBox"/>) in the pieces of <paramref name="role"/>.</summary>
        public static void Skin(GameObject visual, KitRole role, bool flip = false, Vector3? unitBox = null)
        {
            var (shape, color, material) = Look(role);
            Skin(visual, shape, color, Mat(material), unitBox, flip);
        }

        /// <summary>
        /// Swaps a primitive visual for a <see cref="KitSkin"/>: the mesh filter is emptied (the skin fills it at run time,
        /// unsaved), the renderer takes <paramref name="mat"/>. Written through serialized properties, so on a prefab instance
        /// it is a plain override. <paramref name="unitBox"/> is the visual's box in its own space ((1,2,1) for a cylinder).
        /// </summary>
        public static KitSkin Skin(GameObject visual, KitShape shape, KitColor color, Material mat, Vector3? unitBox = null, bool flip = false)
        {
            bool instance = PrefabUtility.IsPartOfPrefabInstance(visual);
            var filter = visual.GetComponent<MeshFilter>() ?? visual.AddComponent<MeshFilter>();
            if (!instance) filter.sharedMesh = null;
            var renderer = visual.GetComponent<MeshRenderer>() ?? visual.AddComponent<MeshRenderer>();
            if (renderer.sharedMaterial != mat) renderer.sharedMaterial = mat;
            if (instance) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            var skin = visual.GetComponent<KitSkin>() ?? visual.AddComponent<KitSkin>();
            var palette = Palette;
            var box = unitBox ?? Vector3.one;
            var so = new SerializedObject(skin);
            so.FindProperty("palette").objectReferenceValue = palette;
            so.FindProperty("shape").enumValueIndex = (int)shape;
            so.FindProperty("color").enumValueIndex = (int)color;
            so.FindProperty("unitBox").vector3Value = box;
            so.FindProperty("flip").boolValue = flip;
            so.ApplyModifiedPropertiesWithoutUndo();
            skin.Rebuild();
            return skin;
        }

        /// <summary>A collider-optional cube (see <see cref="Cube(string,Transform,Vector3,Vector3,Material,string,bool)"/>) drawn in the pieces of <paramref name="role"/>.</summary>
        public static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 scale, KitRole role, string layer, bool keepCollider = false)
        {
            var go = Cube(name, parent, localPos, scale, Mat(Look(role).material), layer, keepCollider);
            Skin(go, role);
            return go;
        }

        // ------------------------------------------------------------------ materials

        public static Material Mat(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat") ?? throw new InvalidOperationException("missing material " + name);

        /// <summary>A toon kit material copied from <paramref name="template"/> once, then (re)coloured.</summary>
        public static Material MakeMaterial(string name, string template, Color baseColor, Color top, int pattern, Color patternColor,
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

        /// <summary>
        /// A HotPatata/Particle material (unlit, both faces, no depth write): alpha blended, or additive for glowing
        /// beams. <paramref name="texture"/> may be null (a plain tinted field).
        /// </summary>
        public static Material MakeUnlitMaterial(string name, Color color, Texture texture, bool additive)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("HotPatata/Particle"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetTexture("_BaseMap", texture);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ prefab building blocks

        public static void MakePrefab(string path, bool overwrite, Func<GameObject> build)
        {
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var go = build();
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        public static int Layer(string name) => LayerMask.NameToLayer(name);

        public static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, string layer, bool keepCollider = false)
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

        /// <summary>A collider-free primitive of any type, with no shadow (signs, beams, icons).</summary>
        public static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 scale,
                                       Material mat, string layer)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer = Layer(layer);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static BoxCollider Box(string name, Transform parent, Vector3 localPos, Vector3 size, string layer, bool trigger = false)
        {
            var go = new GameObject(name) { layer = Layer(layer) };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = trigger;
            return box;
        }

        public static void KinematicBody(GameObject go)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }
}
