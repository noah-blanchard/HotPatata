using System;
using System.Collections.Generic;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>What a kit box is for; each role has one KayKit look (<see cref="CourseKit.Look"/>, ARCHITECTURE §25.1).</summary>
    public enum KitRole { Ground, Wall, Mover, Falling, Belt, Slide, Hazard, Gate, Floor, Ceiling, Brick, Frame, Stairs, Pillar, Truss, Railing, Roof, Grating, Rubber, Accent, Lamp, Glow }

    /// <summary>
    /// Shared editor helpers for the course and kit builders (<see cref="CourseBuilder"/>,
    /// <c>PlaytestCourseBuilder</c>, <c>BombObstacleKitBuilder</c>): placing kit prefabs, sizing movers,
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

        /// <summary>The look set the kit draws with: KayKit everywhere, Industrial (ARCHITECTURE §25.2) only where a builder asks.</summary>
        public enum LookSet { KayKit, Industrial }

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

        public const string IndustrialDir = "Industrial/";
        public const string ConcreteMaterial = IndustrialDir + "Industrial_Concrete", PaintedMetalMaterial = IndustrialDir + "Industrial_PaintedMetal",
            PaintedMetalYellowMaterial = IndustrialDir + "Industrial_PaintedMetal_Yellow", PaintedMetalSafetyMaterial = IndustrialDir + "Industrial_PaintedMetal_Safety",
            LampMaterial = IndustrialDir + "Industrial_Lamp", GlowMaterial = IndustrialDir + "Industrial_Glow", RawMetalMaterial = IndustrialDir + "Industrial_RawMetal", RubberMaterial = IndustrialDir + "Industrial_Rubber";

        /// <summary>One look per role of the course: the piece family, the KayKit colour and the material, in the current look set.</summary>
        public static (KitShape shape, KitColor color, string material) Look(KitRole role) =>
            currentLookSet == LookSet.Industrial ? IndustrialLook(role) : KayKitLook(role);

        /// <summary>
        /// The industrial look (ARCHITECTURE §25.2): bevelled boxes in concrete, painted metal, raw metal and rubber. Hazards and
        /// falling platforms keep their KayKit look (the hazard stripes are what stops them relying on colour alone, spec §19).
        /// </summary>
        static (KitShape shape, KitColor color, string material) IndustrialLook(KitRole role) => role switch
        {
            KitRole.Ground or KitRole.Wall or KitRole.Floor or KitRole.Ceiling or KitRole.Brick or KitRole.Stairs or KitRole.Roof
                => (KitShape.BevelBox, KitColor.Neutral, ConcreteMaterial),
            KitRole.Mover or KitRole.Pillar or KitRole.Truss => (KitShape.BevelBox, KitColor.Neutral, PaintedMetalMaterial),
            KitRole.Frame or KitRole.Railing or KitRole.Gate => (KitShape.BevelBox, KitColor.Neutral, PaintedMetalYellowMaterial),
            KitRole.Accent => (KitShape.BevelBox, KitColor.Neutral, PaintedMetalSafetyMaterial),
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

        // ------------------------------------------------------------------ backdrop

        public static void Prim(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
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

        /// <summary>
        /// Floating islands (with trees) and clouds on both sides of the course, collider-free and at |x| >= 30 m so they
        /// never enter a pass path (ARCHITECTURE §4). Deterministic for a given seed.
        /// </summary>
        public static void BuildBackdrop(Transform parent, int seed, int islands, float islandZMin, float islandZMax,
                                         int clouds, float cloudZMin, float cloudZMax)
        {
            var rng = new Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var island = Mat("Backdrop_Island");
            var trunk = Mat("Backdrop_Trunk");
            var leaves = Mat("Backdrop_Leaves");
            var cloud = Mat("Backdrop_Cloud");

            for (int i = 0; i < islands; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var root = new GameObject($"Island_X{i}").transform;
                root.SetParent(parent, false);
                root.SetPositionAndRotation(new Vector3(side * R(45f, 125f), R(-22f, 30f), R(islandZMin, islandZMax)), Quaternion.Euler(0f, R(0f, 360f), 0f));
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
            for (int i = 0; i < clouds; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var root = new GameObject("Cloud").transform;
                root.SetParent(parent, false);
                root.position = new Vector3(side * R(30f, 170f), R(-40f, 55f), R(cloudZMin, cloudZMax));
                int puffs = rng.Next(3, 6);
                for (int k = 0; k < puffs; k++)
                {
                    float s = R(4.5f, 8f);
                    Prim(root, PrimitiveType.Sphere, new Vector3(R(-7f, 7f), R(-0.6f, 1.8f), R(-2.5f, 1.2f)), new Vector3(s, s * 0.72f, s), cloud);
                }
            }
        }
    }
}
