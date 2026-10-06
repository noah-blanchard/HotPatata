using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// Procedural placeholder textures for the industrial look (ARCHITECTURE §25.2), written once when their folder has no texture:
    /// tileable grunge masks (<c>Grunge</c>: dirt and streaks, linear) and alpha decals (<c>Decals</c>: stains, oil, cracks, scuffs, drips).
    /// They make the anti-repetition layers work with no download; drop real masks and decals next to them (delete these if you like).
    /// </summary>
    public static class IndustrialTextureFactory
    {
        const string Root = IndustrialTextureImporter.Folder;

        public static void EnsurePlaceholders()
        {
            bool wrote = false;
            wrote |= Ensure("Grunge", "Grunge_Placeholder_A", 256, (x, y) => Grunge(x, y, 0f, 5, 0.58f));
            wrote |= Ensure("Grunge", "Grunge_Placeholder_B", 256, (x, y) => Grunge(x, y, 31.7f, 3, 0.5f));
            wrote |= Ensure("Grunge", "Grunge_Placeholder_C", 256, (x, y) => Grunge(x, y, 77.1f, 8, 0.65f));
            wrote |= EnsureDecal("Decal_Placeholder_Stain", Stain(false));
            wrote |= EnsureDecal("Decal_Placeholder_Oil", Stain(true));
            wrote |= EnsureDecal("Decal_Placeholder_Crack", Cracks());
            wrote |= EnsureDecal("Decal_Placeholder_Scuff", Scuffs());
            wrote |= EnsureDecal("Decal_Placeholder_Drip", Drip());
            if (wrote) AssetDatabase.Refresh();
        }

        static bool Ensure(string folder, string name, int size, System.Func<float, float, Color> pixel)
        {
            string dir = Root + folder + "/";
            string path = dir + name + ".png";
            if (File.Exists(path)) return false;
            // Real textures in the folder mean the owner does not want the placeholders back.
            if (Directory.Exists(dir) && Directory.GetFiles(dir).Any(f => !f.EndsWith(".meta") && !Path.GetFileName(f).Contains("_Placeholder_"))) return false;
            Directory.CreateDirectory(dir);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) tex.SetPixel(x, y, pixel((x + 0.5f) / size, (y + 0.5f) / size));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return true;
        }

        static bool EnsureDecal(string name, Texture2D tex)
        {
            string dir = Root + "Decals/";
            string path = dir + name + ".png";
            bool exists = File.Exists(path);
            if (!exists && Directory.Exists(dir) && Directory.GetFiles(dir).Any(f => !f.EndsWith(".meta") && !Path.GetFileName(f).Contains("_Placeholder_")))
            {
                Object.DestroyImmediate(tex);
                return false;
            }
            if (exists) { Object.DestroyImmediate(tex); return false; }
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return true;
        }

        /// <summary>GLSL-style smoothstep: 0 at <paramref name="a"/>, 1 at <paramref name="b"/> (either order), smooth between.</summary>
        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ noise

        /// <summary>Tileable value in 0..1 (blends four Perlin samples across the seam).</summary>
        static float Tile(float x, float y, float frequency, float seed) => Tile(x, y, frequency, frequency, seed);

        /// <summary>Tileable value in 0..1 with its own (whole) frequency on each axis: long streaks need a stretched noise.</summary>
        static float Tile(float x, float y, float frequencyX, float frequencyY, float seed)
        {
            float fx = x * frequencyX, fy = y * frequencyY;
            float a = Mathf.PerlinNoise(fx + seed, fy + seed);
            float b = Mathf.PerlinNoise(fx - frequencyX + seed, fy + seed);
            float c = Mathf.PerlinNoise(fx + seed, fy - frequencyY + seed);
            float d = Mathf.PerlinNoise(fx - frequencyX + seed, fy - frequencyY + seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, x), Mathf.Lerp(c, d, x), y);
        }

        /// <summary>A grunge mask: 1 = clean, 0 = dirt; blotches plus long vertical streaks.</summary>
        static Color Grunge(float x, float y, float seed, int octaves, float coverage)
        {
            float n = 0f, amp = 0.5f, freq = 3f, total = 0f;
            for (int i = 0; i < octaves; i++) { n += amp * Tile(x, y, Mathf.Round(freq), seed + i * 13.1f); total += amp; amp *= 0.55f; freq *= 2f; }
            n /= total;
            float streak = Tile(x, y, 24f, 2f, seed + 5f);   // long vertical streaks, tileable like the rest (no seam at each repeat)
            float dirt = Mathf.Clamp01((coverage - n) * 3.2f + (streak - 0.55f) * 0.9f);
            float clean = 1f - dirt;
            return new Color(clean, clean, clean, 1f);
        }

        // ------------------------------------------------------------------ decals (RGBA, premultiplied-free, 256 px)

        static Texture2D NewDecal(out Color[] pixels)
        {
            var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            pixels = new Color[256 * 256];
            return tex;
        }

        static Texture2D Finish(Texture2D tex, Color[] pixels)
        {
            // fade the alpha to zero at the border: a clamped decal must never show the edge of its quad
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float border = Mathf.Min(Mathf.Min(x, 255 - x), Mathf.Min(y, 255 - y)) / 255f;
                    pixels[y * 256 + x].a *= Smooth(0f, 0.08f, border);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D Stain(bool oil)
        {
            var tex = NewDecal(out var px);
            var tint = oil ? new Color(0.03f, 0.03f, 0.035f) : new Color(0.1f, 0.075f, 0.05f);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float u = x / 255f * 2f - 1f, v = y / 255f * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float n = Mathf.PerlinNoise(u * 2.4f + 9f, v * 2.4f + 9f) * 0.6f + Mathf.PerlinNoise(u * 7f, v * 7f) * 0.4f;
                    float edge = r + (n - 0.5f) * 0.7f;
                    float a = Smooth(1f, 0.45f, edge) * (oil ? 0.85f : 0.6f);
                    float ring = Smooth(0.15f, 0f, Mathf.Abs(edge - 0.62f)) * 0.25f;   // a darker tide line at the edge
                    px[y * 256 + x] = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(a + ring));
                }
            return Finish(tex, px);
        }

        static Texture2D Cracks()
        {
            var tex = NewDecal(out var px);
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.04f, 0.04f, 0.04f, 0f);
            var rng = new Random(11);
            void Walk(float x, float y, float angle, int steps, float width)
            {
                for (int s = 0; s < steps; s++)
                {
                    angle += (float)(rng.NextDouble() - 0.5) * 0.7f;
                    x += Mathf.Cos(angle) * 2f; y += Mathf.Sin(angle) * 2f;
                    if (x < 3 || y < 3 || x > 252 || y > 252) return;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float a = Mathf.Clamp01(1f - d / (width + 0.4f)) * 0.9f;
                            int idx = ((int)y + dy) * 256 + (int)x + dx;
                            if (a > px[idx].a) px[idx] = new Color(0.04f, 0.04f, 0.04f, a);
                        }
                    if (rng.NextDouble() < 0.05 && width > 0.8f) Walk(x, y, angle + (rng.NextDouble() < 0.5 ? 0.9f : -0.9f), steps - s - 10, width * 0.7f);
                }
            }
            Walk(128, 128, 0.4f, 70, 1.6f);
            Walk(128, 128, 3.5f, 60, 1.4f);
            return Finish(tex, px);
        }

        static Texture2D Scuffs()
        {
            var tex = NewDecal(out var px);
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.8f, 0.8f, 0.78f, 0f);
            var rng = new Random(23);
            for (int k = 0; k < 14; k++)
            {
                float x = 30 + (float)rng.NextDouble() * 190, y = 70 + (float)rng.NextDouble() * 110, a = 0.35f + (float)rng.NextDouble() * 0.5f - 0.2f;
                int len = 30 + rng.Next(70);
                for (int s = 0; s < len; s++)
                {
                    float px0 = x + Mathf.Cos(a) * s, py0 = y + Mathf.Sin(a) * s * 0.3f + Mathf.Sin(s * 0.15f) * 2f;
                    int ix = (int)px0, iy = (int)py0;
                    if (ix < 1 || iy < 1 || ix > 254 || iy > 254) break;
                    float fade = Mathf.Sin(s / (float)len * Mathf.PI);
                    int idx = iy * 256 + ix;
                    px[idx] = new Color(0.8f, 0.8f, 0.78f, Mathf.Max(px[idx].a, 0.5f * fade));
                }
            }
            return Finish(tex, px);
        }

        static Texture2D Drip()
        {
            var tex = NewDecal(out var px);
            var tint = new Color(0.16f, 0.09f, 0.05f);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float u = x / 255f, v = y / 255f;   // v = 0 at the bottom of the streak
                    float wobble = (Mathf.PerlinNoise(v * 4f, 3.3f) - 0.5f) * 0.18f;
                    float width = Mathf.Lerp(0.05f, 0.22f, v) * (0.7f + Mathf.PerlinNoise(u * 3f, v * 12f) * 0.6f);
                    float a = Smooth(width, 0f, Mathf.Abs(u - 0.5f - wobble));
                    float fade = Smooth(0f, 0.25f, v) * (0.35f + 0.65f * v);   // strongest at the top, gone at the foot
                    float streak = Mathf.PerlinNoise(u * 40f, v * 3f);
                    px[y * 256 + x] = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(a * fade * (0.55f + streak * 0.5f)) * 0.8f);
                }
            return Finish(tex, px);
        }
    }
}
