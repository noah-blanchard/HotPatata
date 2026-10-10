using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Random = System.Random;

namespace HotPatata.Editor
{
    public static partial class PatataCanopyBuilder
    {
        const string DapplePath = "Assets/Art/Nature/Generated/LeafDapple.png";
        const float DappleTile = 26f;     // metres per repeat of the leaf cookie
        const float DappleShade = 0.72f;  // the light left in the deepest leaf shadow (subtle: the decks stay readable)
        const string LeafSpritePath = "Assets/Art/Nature/Generated/LeafSprite.png";
        const string ShaftMeshPath = "Assets/Art/Nature/Generated/LightShaft_{0}.asset";
        const string CanopyPresetDir = "Assets/Settings/Look/TimeOfDay/Canopy";
        static readonly float[] ShaftWidths = { 0.08f, 0.11f, 0.14f };   // a shaft's width, as a fraction of its length

        // ------------------------------------------------------------------ atmosphere (decoration and presentation only)

        /// <summary>
        /// The canopy's air (ARCHITECTURE §25.3, M13.7): light shafts hanging from the crowns beside the course (HotPatata/LightShaft,
        /// strongest with a low sun), dust motes drifting in them, leaves falling from the aisle's giants, and fireflies beside and
        /// under the decks of the last two acts, out from sunset (<see cref="AtmosphereFade"/>). Every shaft and emitter stands beside
        /// the course and clear of every intended pass arc, whatever the time of day (the shafts follow the sun). Nothing collides.
        /// </summary>
        static string Atmosphere(Transform group)
        {
            var root = new GameObject("Atmosphere").transform;
            root.SetParent(group, false);
            var rng = new Random(909);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var arcs = new ArcIndex(NatureDressing.PassSamples(), PassCorridor.DecorationClearance + 0.5f);
            var axes = ShaftAxes();
            var shaftMaterial = ShaftMaterial();
            var meshes = ShaftWidths.Select((w, i) => ShaftMesh(i, w)).ToArray();
            var (motes, leafFall, fireflies) = AtmospherePrefabs();
            int shafts = 0, leaves = 0, flies = 0;
            for (int k = 0; k < Footprints.Count; k++)
            {
                var f = Footprints[k];
                var rot = Quaternion.Euler(0, f.yaw, 0);
                int act = SectionActs[k];
                var section = new GameObject("Atmosphere " + (k + 1)).transform;
                section.SetParent(root, false);
                foreach (int side in new[] { -1, 1 })
                {
                    // shafts from the crowns beside the course, motes drifting in them
                    for (float lz = -Hub + R(0f, 14f); lz < f.length + Hub; lz += R(18f, 30f))
                    {
                        float length = R(34f, 48f);
                        int w = rng.Next(ShaftWidths.Length);
                        var top = f.origin + rot * new Vector3(side * R(17f, 30f), 0f, lz);
                        top.y = f.topY + R(16f, 24f);
                        float radius = ShaftWidths[w] * length * 0.68f;
                        if (axes.Any(axis => arcs.NearSegment(top, top + axis * length, radius))) continue;
                        var go = new GameObject("Light shaft");
                        go.transform.SetParent(section, false);
                        go.transform.position = top;
                        go.transform.localScale = Vector3.one * length;
                        go.AddComponent<MeshFilter>().sharedMesh = meshes[w];
                        var r = go.AddComponent<MeshRenderer>();
                        r.sharedMaterial = shaftMaterial;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = false;
                        shafts++;
                        // dust in the upper part of the beam (the beam's lean changes with the day: a box around its top half)
                        var at = top + Vector3.down * length * 0.3f;
                        var size = new Vector3(radius * 2.5f, length * 0.45f, radius * 2.5f);
                        if (arcs.NearBox(at, size * 0.5f)) continue;
                        Spawn(section, motes, at, size, rot);
                    }
                    // leaves falling from the giants beside the course
                    for (float lz = -Hub + R(0f, 20f); lz < f.length + Hub; lz += R(26f, 40f))
                    {
                        var at = f.origin + rot * new Vector3(side * R(17f, 24f), 0f, lz);
                        at.y = f.topY + R(6f, 12f);
                        var size = new Vector3(8f, 2f, 14f);
                        if (arcs.NearBox(at + Vector3.down * 8f, new Vector3(8f, 12f, 10f))) continue;
                        Spawn(section, leafFall, at, size, rot);
                        leaves++;
                    }
                    // fireflies from sunset: beside the decks and down in the mist
                    if (act < 4) continue;
                    for (float lz = -Hub + R(0f, 10f); lz < f.length + Hub; lz += R(16f, 24f))
                    {
                        bool low = rng.NextDouble() < 0.5;
                        var at = f.origin + rot * new Vector3(side * R(13f, 22f), 0f, lz);
                        at.y = f.floorY + (low ? R(-24f, -14f) : R(-6f, 2f));
                        var size = new Vector3(9f, low ? 12f : 7f, 16f);
                        if (arcs.NearBox(at, size * 0.5f)) continue;
                        Spawn(section, fireflies, at, size, rot);
                        flies++;
                    }
                }
            }
            return $"{shafts} light shafts, {leaves} leaf falls, {flies} firefly swarms";
        }

        /// <summary>Every intended pass arc, by 4 m cell, for quick clearance tests.</summary>
        sealed class ArcIndex
        {
            const float Cell = 4f;
            readonly Dictionary<(int, int, int), List<Vector3>> cells = new Dictionary<(int, int, int), List<Vector3>>();
            readonly float clearance;

            public ArcIndex(List<Vector3> samples, float clearance)
            {
                this.clearance = clearance;
                foreach (var p in samples)
                {
                    var key = (Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell));
                    if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Vector3>();
                    list.Add(p);
                }
            }

            IEnumerable<Vector3> Around(Vector3 min, Vector3 max)
            {
                for (int x = Mathf.FloorToInt(min.x / Cell); x <= Mathf.FloorToInt(max.x / Cell); x++)
                    for (int y = Mathf.FloorToInt(min.y / Cell); y <= Mathf.FloorToInt(max.y / Cell); y++)
                        for (int z = Mathf.FloorToInt(min.z / Cell); z <= Mathf.FloorToInt(max.z / Cell); z++)
                            if (cells.TryGetValue((x, y, z), out var list))
                                foreach (var p in list) yield return p;
            }

            /// <summary>True when an arc comes within the clearance plus <paramref name="radius"/> of the segment a-b.</summary>
            public bool NearSegment(Vector3 a, Vector3 b, float radius)
            {
                float reach = radius + clearance;
                var pad = Vector3.one * reach;
                foreach (var p in Around(Vector3.Min(a, b) - pad, Vector3.Max(a, b) + pad))
                {
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                    if ((a + ab * t - p).sqrMagnitude < reach * reach) return true;
                }
                return false;
            }

            /// <summary>True when an arc comes within the clearance of the box (centre, half size; any turn about Y: its circumscribed square).</summary>
            public bool NearBox(Vector3 center, Vector3 half)
            {
                float flat = new Vector2(half.x, half.z).magnitude;
                var pad = new Vector3(flat + clearance, half.y + clearance, flat + clearance);
                return Around(center - pad, center + pad).Any();
            }
        }

        /// <summary>The shafts' directions over the day (one per moment of the Canopy day; a high sun when the day is not written yet).</summary>
        static List<Vector3> ShaftAxes()
        {
            var presets = AssetDatabase.FindAssets("t:TimeOfDayPreset", new[] { CanopyPresetDir }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TimeOfDayPreset>).ToList();
            var travel = presets.Count > 0 ? presets.Select(p => p.SunRotation * Vector3.forward).ToList() : new List<Vector3> { Vector3.down };
            float vertical = ShaftMaterial().GetFloat("_Vertical");
            return travel.Select(d => (d + Vector3.down * vertical).normalized).ToList();
        }

        static Material ShaftMaterial()
        {
            string path = CourseKit.MatDir + "Nature/Nature_LightShaft.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("HotPatata/LightShaft"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Intensity", 0.2f);
            mat.SetColor("_BaseColor", new Color(1f, 0.95f, 0.82f, 1f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A unit shaft: a quad from its top (0, 0) down to (0, -1), <paramref name="width"/> wide; its bounds cover any lean.</summary>
        static Mesh ShaftMesh(int index, float width)
        {
            string path = string.Format(ShaftMeshPath, index);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh { name = "LightShaft_" + index };
            mesh.Clear();
            const int rows = 8;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= rows; i++)
            {
                float v = i / (float)rows;
                verts.Add(new Vector3(-width * 0.5f, -v, 0f)); uvs.Add(new Vector2(0f, v));
                verts.Add(new Vector3(width * 0.5f, -v, 0f)); uvs.Add(new Vector2(1f, v));
                if (i == rows) continue;
                int a = i * 2;
                tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(new Vector3(0f, -0.5f, 0f), Vector3.one * 2.2f);
            if (created) AssetDatabase.CreateAsset(mesh, path);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        const string AtmosphereDir = "Assets/Prefabs/Nature/";

        /// <summary>
        /// The three emitters as prefabs (Assets/Prefabs/Nature/Atmosphere_*), so the scene holds small instances rather than hundreds of
        /// particle systems. Each fills a unit box: an instance's scale is its box (the particles keep their size). Updated in place on
        /// every build, so their internal ids stay stable.
        /// </summary>
        static (GameObject motes, GameObject leaves, GameObject fireflies) AtmospherePrefabs()
        {
            var dot = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/SoftDot.png");
            var mote = CourseKit.MakeUnlitMaterial("Nature_Mote", new Color(1f, 0.95f, 0.8f, 0.5f), dot, true);
            var firefly = CourseKit.MakeUnlitMaterial("Nature_Firefly", new Color(2.2f, 2.6f, 0.9f, 1f), dot, true);
            var leaf = CourseKit.MakeUnlitMaterial("Nature_FallingLeaf", Color.white, LeafSprite(), false);
            var motes = EmitterPrefab("Atmosphere_Motes", go =>
            {
                Emitter(go, mote, new Vector2(8f, 14f), new Vector2(0.03f, 0.12f), new Vector2(0.05f, 0.11f), 5f, 80, 0.12f);
                Fade(go, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.45f), new Keyframe(2f, 1f), new Keyframe(3f, 0.8f), new Keyframe(4f, 0.15f)));
            });
            var leaves = EmitterPrefab("Atmosphere_FallingLeaves", go =>
            {
                LeafFall(Emitter(go, leaf, new Vector2(16f, 22f), new Vector2(0.4f, 0.8f), new Vector2(0.14f, 0.24f), 1.2f, 30, 0.6f));
            });
            var flies = EmitterPrefab("Atmosphere_Fireflies", go =>
            {
                Glow(Emitter(go, firefly, new Vector2(4f, 7f), new Vector2(0.1f, 0.3f), new Vector2(0.07f, 0.13f), 5.5f, 50, 0.5f));
                Fade(go, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(2.7f, 0f), new Keyframe(3.2f, 0.6f), new Keyframe(4f, 1f)));
            });
            return (motes, leaves, flies);
        }

        static void Fade(GameObject go, AnimationCurve curve) => (go.TryGetComponent(out AtmosphereFade fade) ? fade : go.AddComponent<AtmosphereFade>()).Configure(curve);

        static GameObject EmitterPrefab(string name, System.Action<GameObject> configure)
        {
            string path = AtmosphereDir + name + ".prefab";
            Directory.CreateDirectory(AtmosphereDir.TrimEnd('/'));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var fresh = new GameObject(name);
                configure(fresh);
                PrefabUtility.SaveAsPrefabAsset(fresh, path);
                Object.DestroyImmediate(fresh);
            }
            else
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    configure(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void Spawn(Transform parent, GameObject prefab, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(center, rotation);
            go.transform.localScale = size;
        }

        /// <summary>A looping emitter filling its unit box (scaled by its transform, the particles keep their size), drifting in a gentle noise.</summary>
        static ParticleSystem Emitter(GameObject go, Material material, Vector2 lifetime, Vector2 speed, Vector2 particleSize, float rate, int max, float noise)
        {
            var ps = go.TryGetComponent(out ParticleSystem existing) ? existing : go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize.x, particleSize.y);
            main.gravityModifier = 0f;
            main.maxParticles = max;
            main.scalingMode = ParticleSystemScalingMode.Shape;   // the transform's scale is the box, never the particles
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = Vector3.one;
            shape.randomDirectionAmount = 1f;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            colour.color = g;
            if (noise > 0f)
            {
                var n = ps.noise;
                n.enabled = true;
                n.strength = noise;
                n.frequency = 0.25f;
                n.scrollSpeed = 0.1f;
            }
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>Leaves: green to autumn colours, falling slowly while they turn and flutter.</summary>
        static void LeafFall(ParticleSystem ps)
        {
            var main = ps.main;
            var colours = new Gradient();
            colours.SetKeys(new[] { new GradientColorKey(new Color(0.42f, 0.55f, 0.24f), 0f), new GradientColorKey(new Color(0.7f, 0.62f, 0.25f), 0.6f), new GradientColorKey(new Color(0.62f, 0.36f, 0.16f), 1f) },
                            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(colours) { mode = ParticleSystemGradientMode.RandomColor };
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.75f, -0.45f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.2f);
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);
            spin.y = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            spin.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.alignment = ParticleSystemRenderSpace.Local;
        }

        /// <summary>Fireflies: a slow glow up and down over each life (never a flash).</summary>
        static void Glow(ParticleSystem ps)
        {
            var colour = ps.colorOverLifetime;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0.35f, 0.55f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            colour.color = g;
        }

        /// <summary>A leaf sprite (white, tinted per particle): an oval leaf with a midrib, soft at the edge. Generated once.</summary>
        static Texture2D LeafSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(LeafSpritePath);
            if (existing != null) return existing;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float half = 0.42f * Mathf.Sin(Mathf.Clamp01((v + 0.95f) / 1.9f) * Mathf.PI);   // the leaf's half width along its length
                    float inside = half - Mathf.Abs(u);
                    float a = Mathf.Clamp01(inside * 18f) * (v > -0.95f && v < 0.95f ? 1f : 0f);
                    float rib = 1f - 0.25f * Mathf.Clamp01(1f - Mathf.Abs(u) * 28f);
                    float shade = 0.85f + 0.15f * (1f - Mathf.Abs(u) / Mathf.Max(0.05f, half));
                    byte c = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(rib * shade));
                    pixels[y * n + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(LeafSpritePath));
            File.WriteAllBytes(LeafSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(LeafSpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(LeafSpritePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(LeafSpritePath);
        }

        /// <summary>
        /// The sun's leaf cookie (ARCHITECTURE §25.3): soft dappled light over the whole course, swaying with the wind
        /// (<see cref="LeafDapple"/>). Called by <see cref="PatataCanopyLook"/> after the day; the texture is generated once.
        /// </summary>
        public static void ApplyDapple(UnityEngine.SceneManagement.Scene scene)
        {
            var sun = NatureDayLook.Sun(scene);
            if (sun == null) return;
            var cookie = DappleTexture();
            sun.cookie = cookie;
            var data = sun.TryGetComponent(out UniversalAdditionalLightData extra) ? extra : sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            data.lightCookieSize = new Vector2(DappleTile, DappleTile);
            data.lightCookieOffset = Vector2.zero;
            if (sun.GetComponent<LeafDapple>() == null) sun.gameObject.AddComponent<LeafDapple>();
            EditorUtility.SetDirty(sun);
            EditorUtility.SetDirty(data);
        }

        /// <summary>A tiling pattern of soft leaf shadows: overlapping clusters of small ellipses, between DappleShade and full light.</summary>
        static Texture2D DappleTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(DapplePath);
            if (existing != null) return existing;
            const int n = 512;
            var shade = new float[n * n];
            var rng = new Random(5);
            float R() => (float)rng.NextDouble();
            for (int cluster = 0; cluster < 70; cluster++)
            {
                float cx = R() * n, cy = R() * n, spread = 18f + R() * 40f;
                int leaves = 12 + rng.Next(22);
                for (int l = 0; l < leaves; l++)
                {
                    float x = cx + (R() - 0.5f) * spread * 2f, y = cy + (R() - 0.5f) * spread * 2f;
                    float a = 4f + R() * 9f, b = a * (0.4f + R() * 0.3f), angle = R() * Mathf.PI;
                    float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
                    int reach = Mathf.CeilToInt(a * 1.6f);
                    for (int dy = -reach; dy <= reach; dy++)
                        for (int dx = -reach; dx <= reach; dx++)
                        {
                            float u = (dx * ca + dy * sa) / a, v = (-dx * sa + dy * ca) / b;
                            float d = u * u + v * v;
                            if (d >= 2.2f) continue;
                            int px = ((Mathf.RoundToInt(x) + dx) % n + n) % n, py = ((Mathf.RoundToInt(y) + dy) % n + n) % n;
                            float k = Mathf.Clamp01((2.2f - d) / 1.4f);
                            shade[py * n + px] = Mathf.Max(shade[py * n + px], k * k * (3f - 2f * k));
                        }
                }
            }
            // a soft blur, so the leaf edges read as a penumbra
            var blurred = new float[n * n];
            const int radius = 3;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int dy = -radius; dy <= radius; dy++)
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            sum += shade[((y + dy + n) % n) * n + (x + dx + n) % n];
                            count++;
                        }
                    blurred[y * n + x] = sum / count;
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[n * n];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)Mathf.RoundToInt(Mathf.Lerp(1f, DappleShade, Mathf.Clamp01(blurred[i])) * 255f);
                pixels[i] = new Color32(v, v, v, v);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(DapplePath));
            File.WriteAllBytes(DapplePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(DapplePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(DapplePath);
            importer.textureType = TextureImporterType.Cookie;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(DapplePath);
        }
    }
}
