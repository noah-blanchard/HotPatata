using System.IO;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Wall signage of the industrial look (ARCHITECTURE §25.2): a warning triangle, an electrical warning, a direction arrow, an
    /// information disc and the stencilled zone numbers painted at each room's entrance. Drawn procedurally once into
    /// <c>Assets/Art/Textures/Industrial/Signs</c> (RGBA, imported as decals) and shown with <see cref="IndustrialDecals.MaterialFor"/>.
    /// A file you drop there under the same name replaces the drawing. No sign uses the red of a real hazard (spec §19).
    /// </summary>
    public static class IndustrialSigns
    {
        const string Dir = IndustrialTextureImporter.Folder + "Signs/";

        public enum Sign { Warning, Electric, Arrow, Info }

        static readonly Color Yellow = new Color(0.93f, 0.74f, 0.17f), Ink = new Color(0.06f, 0.06f, 0.06f), Blue = new Color(0.15f, 0.34f, 0.55f),
            White = new Color(0.94f, 0.93f, 0.9f), Paint = new Color(0.93f, 0.86f, 0.62f);

        public static Material For(Sign sign) => IndustrialDecals.MaterialFor(Texture("Sign_" + sign, 256, 256, sign switch
        {
            Sign.Warning => (x, y) => Triangle(x, y, Exclamation(x, y)),
            Sign.Electric => (x, y) => Triangle(x, y, Polygon(x, y, Bolt)),
            Sign.Arrow => Arrow,
            _ => Info
        }));

        /// <summary>A stencilled two-digit zone number (00 to 99), aspect 2:1.</summary>
        public static Material Zone(int number) => IndustrialDecals.MaterialFor(Texture($"Zone_{number % 100:00}", 512, 256, (x, y) => Digits(x, y, number % 100)));

        static Texture2D Texture(string name, int width, int height, System.Func<float, float, Color> pixel)
        {
            string path = Dir + name + ".png";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Dir);
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var px = new Color[width * height];
                // 2 x 2 supersampling: clean edges on the shapes
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        var sum = Color.clear;
                        for (int s = 0; s < 4; s++)
                        {
                            var c = pixel((x + 0.25f + 0.5f * (s % 2)) / height, (y + 0.25f + 0.5f * (s / 2)) / height);
                            sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                        }
                        px[y * width + x] = sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a / 4f) : Color.clear;
                    }
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ shapes (x, y in units of the texture's height)

        static bool InBox(float x, float y, float cx, float cy, float hx, float hy) => Mathf.Abs(x - cx) <= hx && Mathf.Abs(y - cy) <= hy;
        static bool InCircle(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

        static readonly Vector2[] Bolt = { new(0.55f, 0.78f), new(0.39f, 0.46f), new(0.5f, 0.46f), new(0.43f, 0.2f), new(0.63f, 0.53f), new(0.52f, 0.53f), new(0.62f, 0.78f) };
        static readonly Vector2[] Outer = { new(0.5f, 0.95f), new(0.04f, 0.1f), new(0.96f, 0.1f) };
        static readonly Vector2[] Inner = { new(0.5f, 0.82f), new(0.14f, 0.16f), new(0.86f, 0.16f) };
        static readonly Vector2[] ArrowShape = { new(0.16f, 0.41f), new(0.56f, 0.41f), new(0.56f, 0.24f), new(0.86f, 0.5f), new(0.56f, 0.76f), new(0.56f, 0.59f), new(0.16f, 0.59f) };

        /// <summary>Even-odd point in polygon.</summary>
        static bool Polygon(float x, float y, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > y) != (poly[j].y > y) && x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        static bool Exclamation(float x, float y) => InBox(x, y, 0.5f, 0.5f, 0.045f, 0.17f) || InCircle(x, y, 0.5f, 0.26f, 0.05f);

        static Color Triangle(float x, float y, bool symbol)
        {
            if (!Polygon(x, y, Outer)) return Color.clear;
            return Polygon(x, y, Inner) && !symbol ? Yellow : Ink;
        }

        static Color Arrow(float x, float y)
        {
            if (!InBox(x, y, 0.5f, 0.5f, 0.46f, 0.3f)) return Color.clear;
            if (!InBox(x, y, 0.5f, 0.5f, 0.43f, 0.27f)) return White;
            return Polygon(x, y, ArrowShape) ? White : Blue;
        }

        static Color Info(float x, float y)
        {
            if (!InCircle(x, y, 0.5f, 0.5f, 0.45f)) return Color.clear;
            if (!InCircle(x, y, 0.5f, 0.5f, 0.41f)) return White;
            return InCircle(x, y, 0.5f, 0.71f, 0.065f) || InBox(x, y, 0.5f, 0.42f, 0.055f, 0.17f) ? White : Blue;
        }

        // segments a b c d e f g, as in a seven-segment display; the gaps between them are the stencil's bridges
        static readonly int[] Segments = { 0b1111110, 0b0110000, 0b1101101, 0b1111001, 0b0110011, 0b1011011, 0b1011111, 0b1110000, 0b1111111, 0b1111011 };

        static Color Digits(float x, float y, int number)
        {
            int[] digits = { number / 10, number % 10 };
            for (int i = 0; i < 2; i++)
            {
                float cx = 0.62f + i * 0.76f;
                int mask = Segments[digits[i]];
                bool on = false;
                if ((mask & 0b1000000) != 0) on |= InBox(x, y, cx, 0.86f, 0.18f, 0.055f);        // a
                if ((mask & 0b0100000) != 0) on |= InBox(x, y, cx + 0.25f, 0.68f, 0.055f, 0.13f); // b
                if ((mask & 0b0010000) != 0) on |= InBox(x, y, cx + 0.25f, 0.32f, 0.055f, 0.13f); // c
                if ((mask & 0b0001000) != 0) on |= InBox(x, y, cx, 0.14f, 0.18f, 0.055f);        // d
                if ((mask & 0b0000100) != 0) on |= InBox(x, y, cx - 0.25f, 0.32f, 0.055f, 0.13f); // e
                if ((mask & 0b0000010) != 0) on |= InBox(x, y, cx - 0.25f, 0.68f, 0.055f, 0.13f); // f
                if ((mask & 0b0000001) != 0) on |= InBox(x, y, cx, 0.5f, 0.18f, 0.055f);         // g
                if (!on) continue;
                // worn paint: patchy alpha
                float wear = Mathf.PerlinNoise(x * 18f + 3.1f, y * 18f + 7.7f) * 0.6f + Mathf.PerlinNoise(x * 60f, y * 60f) * 0.4f;
                return new Color(Paint.r, Paint.g, Paint.b, Mathf.Clamp01(0.55f + (wear - 0.35f) * 1.6f) * 0.92f);
            }
            return Color.clear;
        }
    }
}
