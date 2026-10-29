using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Bakes the ground rules into a layered terrain's splat (ARCHITECTURE §27.4, docs/LEVEL_AUTHORING.md §26): the eight weights of every
    /// texel from <see cref="GroundRules"/> (slope, height, curvature, water, walk lines, rock, north, noise), written into the alphamaps,
    /// except where the author painted by hand. A texel is painted when its weights differ from the last bake's by more than
    /// <see cref="PaintedEpsilon"/> (or, at the first bake, from the flat ground's): it keeps them, and the painted mask remembers it until
    /// <see cref="ResetPainted"/> gives it back to the rules. Menu HotPatata/Ground/Bake Ground Rules, the Level inspector's button, and on
    /// save for a level that opts in.
    /// </summary>
    public static class GroundBaker
    {
        public const int Layers = 8;
        public const float PaintedEpsilon = 0.03f;
        /// <summary>Distances are measured up to this (m); farther counts as far.</summary>
        public const float DistanceCap = 40f;

        /// <summary>
        /// What other pieces add to the measures (L2 trails and rivers, L3 cliffs' feet): called with the terrain's heights and the masks of
        /// water, walk lines and rock (1 = there) to fill.
        /// </summary>
        public static readonly List<Action<Scene, Terrain, Inputs>> Sources = new List<Action<Scene, Terrain, Inputs>>();

        /// <summary>The measures of a bake, one grid each at the alphamap's resolution.</summary>
        public sealed class Inputs
        {
            public GroundField heights, water, underWater, walk, rock;
        }

        /// <summary>True when <paramref name="terrain"/> is drawn with the layered ground (eight layers in order).</summary>
        public static bool IsLayered(Terrain terrain)
        {
            var layers = terrain.terrainData != null ? terrain.terrainData.terrainLayers : null;
            if (layers == null || layers.Length != Layers) return false;
            for (int i = 0; i < Layers; i++)
                if (layers[i] == null || AssetDatabase.GetAssetPath(layers[i]) != GroundLayerBuilder.LayerPath(i)) return false;
            return true;
        }

        [MenuItem("HotPatata/Ground/Bake Ground Rules", priority = 0)]
        static void BakeMenu()
        {
            var scene = SceneManager.GetActiveScene();
            int baked = 0, painted = 0;
            foreach (var t in LevelTerrain.Terrains(scene).Where(IsLayered))
            {
                painted += Bake(t);
                baked++;
            }
            Debug.Log(baked == 0 ? "[Ground] no layered terrain in the open level (Level inspector: Use the layered ground)."
                                 : $"[Ground] rules baked on {baked} terrain(s); {painted} painted texel(s) kept.");
        }

        public static string AutoPath(Terrain t) => Folder(t) + "GroundAuto.asset";
        public static string PaintedPath(Terrain t) => Folder(t) + "GroundPainted.asset";

        static string Folder(Terrain t)
        {
            string data = AssetDatabase.GetAssetPath(t.terrainData);
            return string.IsNullOrEmpty(data) ? LevelMeshStore.LevelFolder(t.gameObject.scene) + "Terrain/" : Path.GetDirectoryName(data).Replace('\\', '/') + "/";
        }

        static GroundLayerMap LoadMap(string path) => AssetDatabase.LoadAssetAtPath<GroundLayerMap>(path);

        static GroundLayerMap SaveMap(string path, int res, int ch, byte[] bytes)
        {
            var map = LoadMap(path);
            if (map == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                map = ScriptableObject.CreateInstance<GroundLayerMap>();
                map.Write(res, ch, bytes);
                AssetDatabase.CreateAsset(map, path);
                return map;
            }
            map.Write(res, ch, bytes);
            return map;
        }

        /// <summary>The painted mask of <paramref name="terrain"/> (1 where painted), or null when it was never baked.</summary>
        public static byte[] Painted(Terrain terrain)
        {
            var map = LoadMap(PaintedPath(terrain));
            return map != null && map.Resolution == terrain.terrainData.alphamapResolution ? map.Read() : null;
        }

        /// <summary>Bakes the rules into <paramref name="terrain"/>'s splat (painted texels kept). Returns how many painted texels were kept.</summary>
        public static int Bake(Terrain terrain, GroundRules rules = null)
        {
            if (!IsLayered(terrain)) throw new InvalidOperationException($"{terrain.name} is not drawn with the layered ground");
            rules ??= GroundRules.Load();
            var data = terrain.terrainData;
            int res = data.alphamapResolution;
            var current = data.GetAlphamaps(0, 0, res, res);
            var auto = Auto(terrain, rules);

            // what the author painted: differs from the last bake (or from the flat first layer before any bake)
            var lastMap = LoadMap(AutoPath(terrain));
            byte[] last = lastMap != null && lastMap.Resolution == res && lastMap.Channels == Layers ? lastMap.Read() : null;
            var paintedMap = LoadMap(PaintedPath(terrain));
            byte[] painted = paintedMap != null && paintedMap.Resolution == res ? paintedMap.Read() : new byte[res * res];
            int kept = 0;
            Undo.RegisterCompleteObjectUndo(data, "Bake ground rules");
            var result = new float[res, res, Layers];
            var autoBytes = new byte[res * res * Layers];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    int cell = z * res + x;
                    if (painted[cell] == 0)
                        for (int l = 0; l < Layers; l++)
                        {
                            float before = last != null ? last[cell * Layers + l] / 255f : (l == 0 ? 1f : 0f);
                            if (Mathf.Abs(current[z, x, l] - before) > PaintedEpsilon) { painted[cell] = 1; break; }
                        }
                    bool keep = painted[cell] != 0;
                    if (keep) kept++;
                    for (int l = 0; l < Layers; l++)
                    {
                        float a = auto[z, x, l];
                        autoBytes[cell * Layers + l] = (byte)Mathf.RoundToInt(a * 255f);
                        result[z, x, l] = keep ? current[z, x, l] : a;
                    }
                }
            // what is written is what the next bake compares with: the bytes, read back
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    int cell = z * res + x;
                    if (painted[cell] != 0) continue;
                    float sum = 0f;
                    for (int l = 0; l < Layers; l++) sum += autoBytes[cell * Layers + l];
                    for (int l = 0; l < Layers; l++) result[z, x, l] = sum > 0f ? autoBytes[cell * Layers + l] / sum : (l == 0 ? 1f : 0f);
                    for (int l = 0; l < Layers; l++) autoBytes[cell * Layers + l] = (byte)Mathf.RoundToInt(result[z, x, l] * 255f);
                }
            data.SetAlphamaps(0, 0, result);
            EditorUtility.SetDirty(data);
            SaveMap(AutoPath(terrain), res, Layers, autoBytes);
            SaveMap(PaintedPath(terrain), res, 1, painted);
            AssetDatabase.SaveAssets();
            Refresh(terrain);
            return kept;
        }

        /// <summary>
        /// Gives the texels within <paramref name="radius"/> of <paramref name="center"/> back to the rules: un-painted, and drawn with the
        /// last bake's weights. Returns the texels reset.
        /// </summary>
        public static int ResetPainted(Terrain terrain, Vector3 center, float radius)
        {
            var data = terrain.terrainData;
            int res = data.alphamapResolution;
            var lastMap = LoadMap(AutoPath(terrain));
            var paintedMap = LoadMap(PaintedPath(terrain));
            if (lastMap == null || paintedMap == null || lastMap.Resolution != res || paintedMap.Resolution != res) return 0;
            var last = lastMap.Read();
            var painted = paintedMap.Read();
            var field = new GroundField(res, LevelTerrain.Area(terrain));
            var c = field.CellOf(new Vector2(center.x, center.z));
            int r = Mathf.CeilToInt(radius / field.Cell) + 1;
            int x0 = Mathf.Clamp((int)c.x - r, 0, res - 1), x1 = Mathf.Clamp((int)c.x + r, 0, res - 1);
            int z0 = Mathf.Clamp((int)c.y - r, 0, res - 1), z1 = Mathf.Clamp((int)c.y + r, 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            Undo.RegisterCompleteObjectUndo(new Object[] { data, paintedMap }, "Reset painted ground");
            var maps = data.GetAlphamaps(x0, z0, w, h);
            int reset = 0;
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    int cell = z * res + x;
                    if (painted[cell] == 0 || Vector2.Distance(field.World(x, z), new Vector2(center.x, center.z)) > radius) continue;
                    painted[cell] = 0;
                    for (int l = 0; l < Layers; l++) maps[z - z0, x - x0, l] = last[cell * Layers + l] / 255f;
                    reset++;
                }
            data.SetAlphamaps(x0, z0, maps);
            paintedMap.Write(res, 1, painted);
            EditorUtility.SetDirty(data);
            Refresh(terrain);
            return reset;
        }

        /// <summary>Re-binds the control maps (and the painted overlay when it is shown).</summary>
        public static void Refresh(Terrain terrain)
        {
            var splat = terrain.GetComponent<GroundSplat>();
            if (splat == null) return;
            if (splat.ShowingPainted) splat.ShowPainted(PaintedTexture(terrain), true);
            else splat.Apply();
        }

        /// <summary>The painted mask as a texture (white where painted), for the Scene view's overlay.</summary>
        public static Texture2D PaintedTexture(Terrain terrain)
        {
            var painted = Painted(terrain);
            int res = terrain.terrainData.alphamapResolution;
            var tex = new Texture2D(res, res, TextureFormat.R8, false, true) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new byte[res * res];
            if (painted != null)
                for (int i = 0; i < px.Length; i++) px[i] = painted[i] != 0 ? (byte)255 : (byte)0;
            tex.SetPixelData(px, 0);
            tex.Apply(false, false);
            return tex;
        }

        // ------------------------------------------------------------------ the rules

        /// <summary>The rules' weights over <paramref name="terrain"/>'s alphamap (z, x, layer).</summary>
        public static float[,,] Auto(Terrain terrain, GroundRules rules)
        {
            var inputs = Measure(terrain, rules);
            var data = terrain.terrainData;
            int res = data.alphamapResolution;
            var h = inputs.heights;
            float cell = h.Cell;
            // the level's floor: the median of the ground
            var sorted = h.Values.Where((_, i) => i % 37 == 0).OrderBy(v => v).ToList();
            float floor = sorted.Count > 0 ? sorted[sorted.Count / 2] : terrain.transform.position.y;
            // curvature: the ground's height under the mean of its neighbours within the radius (+ a hollow), blurred
            var mean = new GroundField(res, h.Area);
            Array.Copy(h.Values, mean.Values, h.Values.Length);
            mean.Blur(Mathf.Max(1, Mathf.RoundToInt(rules.curvatureRadius / cell)), 2);
            var waterD = GroundField.Distance(inputs.underWater, DistanceCap);
            var walkD = GroundField.Distance(inputs.walk, DistanceCap);
            var rockD = GroundField.Distance(inputs.rock, DistanceCap);
            var shore = GroundField.Distance(inputs.water, DistanceCap);
            var result = new float[res, res, Layers];
            var weights = new float[Layers];
            var noise = new float[Layers];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float hc = h[x, z];
                    float dx = (h[Mathf.Min(res - 1, x + 1), z] - h[Mathf.Max(0, x - 1), z]) / ((Mathf.Min(res - 1, x + 1) - Mathf.Max(0, x - 1)) * cell);
                    float dz = (h[x, Mathf.Min(res - 1, z + 1)] - h[x, Mathf.Max(0, z - 1)]) / ((Mathf.Min(res - 1, z + 1) - Mathf.Max(0, z - 1)) * cell);
                    var normal = new Vector3(-dx, 1f, -dz).normalized;
                    var world = h.World(x, z);
                    var t = new GroundRules.Texel
                    {
                        slope = Mathf.Acos(Mathf.Clamp01(normal.y)) * Mathf.Rad2Deg,
                        height = hc - floor,
                        curvature = Mathf.Clamp((mean[x, z] - hc) / 0.75f, -1f, 1f),
                        underWater = inputs.underWater[x, z] > 0.5f,
                        water = Mathf.Min(waterD[x, z], shore[x, z]),
                        walk = walkD[x, z],
                        rock = rockD[x, z],
                        north = normal.z / Mathf.Max(0.05f, new Vector2(normal.x, normal.z).magnitude) * Mathf.Clamp01((1f - normal.y) * 6f),
                    };
                    for (int l = 0; l < Layers; l++) noise[l] = Mathf.PerlinNoise(world.x / rules.noiseScale + l * 17.31f + 101.7f, world.y / rules.noiseScale - l * 11.73f + 37.1f);
                    rules.Weigh(t, weights, noise);
                    for (int l = 0; l < Layers; l++) result[z, x, l] = weights[l];
                }
            return result;
        }

        /// <summary>The measures of a bake over <paramref name="terrain"/>: its heights, the water, the walk lines near the ground, the rock.</summary>
        public static Inputs Measure(Terrain terrain, GroundRules rules)
        {
            var data = terrain.terrainData;
            int res = data.alphamapResolution;
            var area = LevelTerrain.Area(terrain);
            var inputs = new Inputs
            {
                heights = Heights(terrain, res),
                water = new GroundField(res, area),
                underWater = new GroundField(res, area),
                walk = new GroundField(res, area),
                rock = new GroundField(res, area),
            };
            var scene = terrain.gameObject.scene;
            var h = inputs.heights;

            foreach (var water in TimberLinks.Pieces(scene).OfType<WaterShape>())
            {
                var t = water.transform;
                var turn = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
                var half = water.Size * 0.5f;
                var poly = new[] { new Vector3(-half.x, 0, -half.y), new Vector3(half.x, 0, -half.y), new Vector3(half.x, 0, half.y), new Vector3(-half.x, 0, half.y) }
                    .Select(c => t.position + turn * c).Select(c => new Vector2(c.x, c.z)).ToList();
                float surface = t.position.y;
                inputs.water.Polygon(poly, (x, z) =>
                {
                    inputs.water[x, z] = 1f;
                    if (h[x, z] < surface) inputs.underWater[x, z] = 1f;
                });
            }

            foreach (var output in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelPieceOutput>(true)))
            {
                var line = output.WalkLine;
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    Vector3 a = line[i], b = line[i + 1];
                    inputs.walk.Segment(new Vector2(a.x, a.z), new Vector2(b.x, b.z), 0.6f, (x, z, s) =>
                    {
                        if (Mathf.Abs(Mathf.Lerp(a.y, b.y, s) - h[x, z]) <= rules.walkLineReach) inputs.walk[x, z] = 1f;
                    });
                }
            }

            foreach (var cliff in TimberLinks.Pieces(scene).OfType<CliffShape>())
            {
                var outline = cliff.WorldOutline();
                float foot = cliff.transform.position.y - cliff.Sink;
                inputs.rock.Polygon(outline, (x, z) =>
                {
                    if (h[x, z] < cliff.transform.position.y + cliff.Height && h[x, z] > foot - 2f) inputs.rock[x, z] = 1f;
                });
            }
            foreach (var shell in TimberLinks.Pieces(scene).OfType<RockShellShape>())
            {
                var foot = LevelRock.Footprint(shell);
                var pts = shell.WorldPoints();
                if (pts.Count == 0) continue;
                float reach = shell.Width * 0.5f + shell.Thickness + shell.Roughness + 1f;
                var min = new Vector2(pts.Min(p => p.x) - reach, pts.Min(p => p.z) - reach);
                var max = new Vector2(pts.Max(p => p.x) + reach, pts.Max(p => p.z) + reach);
                var c0 = h.CellOf(min);
                var c1 = h.CellOf(max);
                for (int z = Mathf.Max(0, (int)c0.y); z <= Mathf.Min(res - 1, (int)c1.y + 1); z++)
                    for (int x = Mathf.Max(0, (int)c0.x); x <= Mathf.Min(res - 1, (int)c1.x + 1); x++)
                        if (foot(h.World(x, z), out float caveFloor) && caveFloor > h[x, z] - 4f && caveFloor < h[x, z] + 2f) inputs.rock[x, z] = 1f;
            }

            foreach (var source in Sources) source(scene, terrain, inputs);
            return inputs;
        }

        /// <summary>The ground's height (world) at each alphamap texel.</summary>
        public static GroundField Heights(Terrain terrain, int res)
        {
            var data = terrain.terrainData;
            int hr = data.heightmapResolution;
            var raw = data.GetHeights(0, 0, hr, hr);
            var field = new GroundField(res, LevelTerrain.Area(terrain));
            float y0 = terrain.transform.position.y, span = data.size.y;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float fx = x / (float)(res - 1) * (hr - 1), fz = z / (float)(res - 1) * (hr - 1);
                    int ix = Mathf.Min((int)fx, hr - 2), iz = Mathf.Min((int)fz, hr - 2);
                    float tx = fx - ix, tz = fz - iz;
                    float v = Mathf.Lerp(Mathf.Lerp(raw[iz, ix], raw[iz, ix + 1], tx), Mathf.Lerp(raw[iz + 1, ix], raw[iz + 1, ix + 1], tx), tz);
                    field[x, z] = y0 + v * span;
                }
            return field;
        }
    }
}
