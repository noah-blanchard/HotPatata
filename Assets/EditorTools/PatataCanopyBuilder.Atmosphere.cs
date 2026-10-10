using System.IO;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HotPatata.Editor
{
    public static partial class PatataCanopyBuilder
    {
        const string DapplePath = "Assets/Art/Nature/Generated/LeafDapple.png";
        const float DappleTile = 26f;     // metres per repeat of the leaf cookie
        const float DappleShade = 0.62f;  // the light left in the deepest leaf shadow (subtle: the decks stay readable)

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
            var data = sun.GetComponent<UniversalAdditionalLightData>() ?? sun.gameObject.AddComponent<UniversalAdditionalLightData>();
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
            var rng = new System.Random(5);
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
