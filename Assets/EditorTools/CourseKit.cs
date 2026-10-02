using System;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace HotPatata.Editor
{
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

        /// <summary>A Platform_Basic box: pivot at its centre, scale = size.</summary>
        public static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Material mat, Quaternion? rotation = null)
        {
            var go = Place(parent, PlatformsDir + "Platform_Basic", name, center, rotation ?? Quaternion.identity);
            go.transform.localScale = size;
            if (mat != null) go.GetComponentInChildren<Renderer>().sharedMaterial = mat;
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
