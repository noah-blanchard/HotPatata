using System.IO;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The few PatataWilds textures no download provides (ARCHITECTURE §25.3), drawn in code so they need no third-party file:
    /// a tileable ripple normal map and a tileable foam noise for <c>HotPatata/Water</c>, and the sprays the generated trees and
    /// grass are dressed in (fir and pine needles, grass blades: a colour and a cut-out mask; Poly Haven's sapling textures are
    /// unwrapped needle geometry, not cards). Written once (a file already there is kept, so it can be replaced by hand) under
    /// <c>Assets/Art/Textures/Nature/Generated</c>; the importer reads their suffixes.
    /// </summary>
    public static class NatureTextureFactory
    {
        public const string Folder = NatureAssetImporter.TextureFolder + "Generated/";
        public const string WaveNormal = Folder + "water_waves_nor_gl.png";
        public const string FoamNoise = Folder + "water_foam_mask.png";
        const int Size = 512;

        public enum Spray { Fir, Pine, Grass }

        public static string SprayPrefix(Spray spray) => spray switch { Spray.Fir => "needles_fir_", Spray.Pine => "needles_pine_", _ => "grass_blades_" };

        public static void EnsureTextures()
        {
            Directory.CreateDirectory(Folder.TrimEnd('/'));
            bool wrote = false;
            if (!File.Exists(WaveNormal)) { Write(WaveNormal, Waves()); wrote = true; }
            if (!File.Exists(FoamNoise)) { Write(FoamNoise, Foam()); wrote = true; }
            foreach (Spray spray in System.Enum.GetValues(typeof(Spray)))
            {
                string colour = Folder + SprayPrefix(spray) + "diff.png", alpha = Folder + SprayPrefix(spray) + "alpha.png";
                if (File.Exists(colour) && File.Exists(alpha)) continue;
                var (c, a) = DrawSpray(spray);
                Write(colour, c);
                Write(alpha, a);
                wrote = true;
            }
            if (wrote) AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ sprays (needles, blades)

        sealed class Canvas
        {
            public readonly int Size;
            public readonly Color[] Colour;
            public readonly float[] Alpha;

            public Canvas(int size, Color background)
            {
                Size = size;
                Colour = new Color[size * size];
                Alpha = new float[size * size];
                for (int i = 0; i < Colour.Length; i++) Colour[i] = background;
            }

            /// <summary>An anti-aliased stroke from a to b (pixels), width w0 at a tapering to w1 at b, coloured c0 to c1.</summary>
            public void Stroke(Vector2 a, Vector2 b, float w0, float w1, Color c0, Color c1)
            {
                float pad = Mathf.Max(w0, w1) + 2f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - pad)), x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + pad));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - pad)), y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + pad));
                var ab = b - a;
                float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                        float d = Vector2.Distance(p, a + ab * t);
                        float half = Mathf.Lerp(w0, w1, t) * 0.5f;
                        float cover = Mathf.Clamp01(half + 0.5f - d);
                        if (cover <= 0f) continue;
                        int i = y * Size + x;
                        Colour[i] = Color.Lerp(Colour[i], Color.Lerp(c0, c1, t), Alpha[i] <= 0f ? 1f : cover);
                        Alpha[i] = Mathf.Max(Alpha[i], cover);
                    }
            }

            public (Texture2D colour, Texture2D alpha) ToTextures()
            {
                var c = new Texture2D(Size, Size, TextureFormat.RGB24, false, false);
                var a = new Texture2D(Size, Size, TextureFormat.RGB24, false, true);
                c.SetPixels(Colour);
                var grey = new Color[Alpha.Length];
                for (int i = 0; i < Alpha.Length; i++) grey[i] = new Color(Alpha[i], Alpha[i], Alpha[i]);
                a.SetPixels(grey);
                c.Apply();
                a.Apply();
                return (c, a);
            }
        }

        static (Texture2D colour, Texture2D alpha) DrawSpray(Spray spray)
        {
            var rng = new System.Random(spray == Spray.Fir ? 11 : spray == Spray.Pine ? 23 : 37);
            float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            Color G(float r, float g, float b) => new Color(r, g, b);
            const int size = 512;
            if (spray == Spray.Grass)
            {
                var grass = new Canvas(size, G(0.22f, 0.3f, 0.1f));
                for (int i = 0; i < 46; i++)
                {
                    float x = R(14f, size - 14f), h = R(240f, 500f), bend = R(-70f, 70f), w = R(7f, 12f);
                    bool dry = rng.NextDouble() < 0.18;
                    var baseColour = dry ? G(0.3f, 0.28f, 0.12f) : G(0.09f, 0.18f, 0.05f);
                    var tip = dry ? G(0.62f, 0.56f, 0.3f) : G(0.42f, 0.52f, 0.18f);
                    var prev = new Vector2(x, 0f);
                    const int segments = 8;
                    for (int s = 1; s <= segments; s++)
                    {
                        float t = s / (float)segments;
                        var next = new Vector2(x + bend * t * t, h * t);
                        grass.Stroke(prev, next, w * (1f - (t - 1f / segments)) + 1f, w * (1f - t) + 1f,
                                     Color.Lerp(baseColour, tip, t - 1f / segments), Color.Lerp(baseColour, tip, t));
                        prev = next;
                    }
                }
                return grass.ToTextures();
            }

            bool fir = spray == Spray.Fir;
            var canvas = new Canvas(size, fir ? G(0.1f, 0.17f, 0.11f) : G(0.16f, 0.22f, 0.1f));
            var stemColour = G(0.3f, 0.22f, 0.14f);
            void Needles(Vector2 from, Vector2 to, float length, float angle, float spacing, float width)
            {
                var dir = (to - from).normalized;
                float total = Vector2.Distance(from, to);
                for (float d = spacing * 0.5f; d < total; d += spacing * R(0.8f, 1.2f))
                {
                    var p = from + dir * d;
                    float shrink = Mathf.Lerp(1f, 0.55f, d / total);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float a = (angle + R(-8f, 8f)) * side * Mathf.Deg2Rad;
                        var n = new Vector2(dir.x * Mathf.Cos(a) - dir.y * Mathf.Sin(a), dir.x * Mathf.Sin(a) + dir.y * Mathf.Cos(a));
                        var dark = fir ? G(0.05f, 0.13f, 0.08f) : G(0.1f, 0.18f, 0.06f);
                        var light = fir ? G(0.2f, 0.36f, 0.22f) : G(0.36f, 0.46f, 0.18f);
                        canvas.Stroke(p, p + n * length * shrink * R(0.85f, 1.1f), width, width * 0.5f, dark, light);
                    }
                }
            }
            var root = new Vector2(size * 0.5f, 6f);
            var top = new Vector2(size * 0.5f + R(-20f, 20f), size - 10f);
            canvas.Stroke(root, top, 7f, 2f, stemColour, stemColour);
            if (fir)
            {
                // a flat fir spray: a main stem, side shoots, short needles combed to both sides
                Needles(root, top, 34f, 62f, 6f, 3.4f);
                for (int k = 0; k < 7; k++)
                {
                    float t = 0.12f + k * 0.11f;
                    var from = Vector2.Lerp(root, top, t);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var to = from + new Vector2(side * R(110f, 190f) * (1f - t * 0.6f), R(70f, 120f));
                        canvas.Stroke(from, to, 4f, 1.5f, stemColour, stemColour);
                        Needles(from, to, 28f, 60f, 5.5f, 3f);
                    }
                }
            }
            else
            {
                // a pine shoot: long needles in tufts all along the stem, pointing up and out
                for (float t = 0.05f; t < 0.98f; t += 0.035f)
                {
                    var p = Vector2.Lerp(root, top, t);
                    for (int k = 0; k < 7; k++)
                    {
                        float a = R(-75f, 75f) * Mathf.Deg2Rad;
                        var n = new Vector2(Mathf.Sin(a), Mathf.Cos(a) * 0.9f + 0.25f).normalized;
                        float length = R(110f, 170f) * Mathf.Lerp(1f, 0.6f, t);
                        canvas.Stroke(p, p + n * length, 3.2f, 1.2f, G(0.1f, 0.18f, 0.06f), G(0.38f, 0.48f, 0.18f));
                    }
                }
            }
            return canvas.ToTextures();
        }

        static void Write(string path, Texture2D texture)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        // Periodic value noise: the lattice wraps every `period` cells, so the texture tiles seamlessly.
        static float Lattice(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static float Noise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Mathf.Lerp(Lattice(x0, y0, period, seed), Lattice(x0 + 1, y0, period, seed), fx);
            float b = Mathf.Lerp(Lattice(x0, y0 + 1, period, seed), Lattice(x0 + 1, y0 + 1, period, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float total = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                total += Noise(u, v, basePeriod << o, seed + o * 17) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return total / norm;
        }

        static Texture2D Waves()
        {
            var heights = new float[Size, Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (float)x / Size, v = (float)y / Size;
                    // stretched along V (the flow): ripples are longer than they are wide
                    heights[x, y] = Fbm(u, v, 4, 5, 11) + 0.35f * Mathf.Sin((v * 6f + Fbm(u, v, 3, 3, 5) * 2f) * Mathf.PI * 2f) * 0.1f;
                }
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false, true);
            const float strength = 6f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = heights[(x + 1) % Size, y] - heights[(x - 1 + Size) % Size, y];
                    float dy = heights[x, (y + 1) % Size] - heights[x, (y - 1 + Size) % Size];
                    var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    tex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f));
                }
            tex.Apply();
            return tex;
        }

        static Texture2D Foam()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false, true);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (float)x / Size, v = (float)y / Size;
                    float f = Fbm(u, v, 6, 4, 23);
                    float cells = Fbm(u, v, 16, 2, 41);
                    float foam = Mathf.SmoothStep(0.35f, 0.75f, f * 0.7f + cells * 0.5f);
                    tex.SetPixel(x, y, new Color(foam, foam, foam));
                }
            tex.Apply();
            return tex;
        }
    }
}
