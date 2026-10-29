using System;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// The layered ground's assets (ARCHITECTURE §27.4): the eight layers' texture arrays (albedo with height in A; normal XY, occlusion,
    /// roughness), made from the Poly Haven folders of Assets/Art/Textures/Nature; the eight <see cref="TerrainLayer"/>s in their fixed
    /// order (so the Paint Texture brushes show the right names and pictures; the shader ignores their textures); and the material
    /// <see cref="MaterialName"/> on HotPatata/NatureGround. Menu HotPatata/Look/Build Ground Layers (rebuilds everything).
    /// </summary>
    public static class GroundLayerBuilder
    {
        public const string Folder = "Assets/Art/Materials/Ground/";
        public const string MaterialName = "Nature_Ground";
        /// <summary>The arrays are large: kept under Art/Generated (Git LFS).</summary>
        public const string ArrayFolder = "Assets/Art/Generated/Ground/";
        public const string AlbedoPath = ArrayFolder + "GroundAlbedo.asset", NormalPath = ArrayFolder + "GroundNormal.asset";
        public const string ShaderName = "HotPatata/NatureGround";
        /// <summary>A layer's texture size in the arrays.</summary>
        public const int Size = 1024;

        /// <summary>One layer of the ground: its name, the texture folder it comes from, how it is mapped and its tile size (m).</summary>
        public readonly struct Layer
        {
            public readonly string Name, Folder;
            public readonly bool Triplanar;
            public readonly float Tile, Brightness;

            public Layer(string name, string folder, bool triplanar, float tile, float brightness = 1f)
            {
                Name = name;
                Folder = folder;
                Triplanar = triplanar;
                Tile = tile;
                Brightness = brightness;
            }
        }

        /// <summary>The ground's layers, in their fixed order (the splat's channels: 0-3 the first control map, 4-7 the second).</summary>
        public static readonly Layer[] Layers =
        {
            new Layer("Leaf litter", "LeafLitter", false, 3f),
            new Layer("Grass", "Grass", false, 3f, 1.45f),
            new Layer("Trail", "Trail", false, 3f),
            new Layer("Gravel", "Gravel", false, 2.5f),
            new Layer("Mud", "Mud", false, 3f),
            new Layer("Riverbed", "Riverbed", false, 3f),
            new Layer("Scree", "RocksGround", true, 4f),
            new Layer("Rock", "RockFace", true, 6f),
        };

        public const int LeafLitter = 0, Grass = 1, Trail = 2, Gravel = 3, Mud = 4, Riverbed = 5, Scree = 6, Rock = 7;

        public static string LayerPath(int i) => $"{Folder}Layer_{i}_{Layers[i].Folder}.terrainlayer";

        /// <summary>The ground's terrain layers in order (made if missing).</summary>
        public static TerrainLayer[] TerrainLayers()
        {
            if (Enumerable.Range(0, Layers.Length).Any(i => AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath(i)) == null)) BuildLayers();
            return Enumerable.Range(0, Layers.Length).Select(i => AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath(i))).ToArray();
        }

        /// <summary>The ground material (everything made if missing).</summary>
        public static Material Material()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(NatureMaterialBuilder.Dir + MaterialName + ".mat");
            if (mat != null && AssetDatabase.LoadAssetAtPath<Texture2DArray>(AlbedoPath) != null) return mat;
            Build();
            return AssetDatabase.LoadAssetAtPath<Material>(NatureMaterialBuilder.Dir + MaterialName + ".mat");
        }

        [MenuItem("HotPatata/Look/Build Ground Layers", priority = 24)]
        public static void Build()
        {
            Directory.CreateDirectory(Folder.TrimEnd('/'));
            Directory.CreateDirectory(ArrayFolder.TrimEnd('/'));
            try
            {
                EditorUtility.DisplayProgressBar("Ground layers", "Texture arrays", 0f);
                BuildArrays();
                EditorUtility.DisplayProgressBar("Ground layers", "Terrain layers", 0.8f);
                BuildLayers();
                BuildMaterial();
                AssetDatabase.SaveAssets();
            }
            finally { EditorUtility.ClearProgressBar(); }
            Debug.Log($"[Ground] {Layers.Length} layers built: {AlbedoPath}, {NormalPath}, {MaterialName}.");
        }

        // ------------------------------------------------------------------ arrays

        static void BuildArrays()
        {
            int mips = (int)Mathf.Log(Size, 2) + 1;
            var albedo = new Texture2DArray(Size, Size, Layers.Length, TextureFormat.BC7, true, false) { name = "GroundAlbedo" };
            var normal = new Texture2DArray(Size, Size, Layers.Length, TextureFormat.BC7, true, true) { name = "GroundNormal" };
            for (int i = 0; i < Layers.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Ground layers", Layers[i].Name, 0.8f * i / Layers.Length);
                string dir = NatureAssetImporter.TextureFolder + Layers[i].Folder;
                var diff = Read(dir, "diff", new Color32(128, 110, 90, 255));
                var disp = Read(dir, "disp", new Color32(128, 128, 128, 255));
                var nor = Read(dir, "nor_gl", new Color32(128, 128, 255, 255));
                var ao = Read(dir, "ao", new Color32(255, 255, 255, 255));
                var rough = Read(dir, "rough", new Color32(220, 220, 220, 255));
                var a = new Color32[Size * Size];
                var n = new Color32[Size * Size];
                for (int p = 0; p < a.Length; p++)
                {
                    a[p] = new Color32(diff[p].r, diff[p].g, diff[p].b, disp[p].r);
                    n[p] = new Color32(nor[p].r, nor[p].g, ao[p].r, rough[p].r);
                }
                Copy(albedo, i, Compress(a, false), mips);
                Copy(normal, i, Compress(n, true), mips);
            }
            foreach (var arr in new[] { albedo, normal })
            {
                arr.wrapMode = TextureWrapMode.Repeat;
                arr.filterMode = FilterMode.Trilinear;
                arr.anisoLevel = 8;
                arr.Apply(false, false);
            }
            Save(albedo, AlbedoPath);
            Save(normal, NormalPath);
        }

        static void Save(Texture2DArray arr, string path)
        {
            var old = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (old == null)
            {
                AssetDatabase.CreateAsset(arr, path);
                return;
            }
            EditorUtility.CopySerialized(arr, old);
            Object.DestroyImmediate(arr);
            EditorUtility.SetDirty(old);
        }

        static Texture2D Compress(Color32[] pixels, bool linear)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true, linear);
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            EditorUtility.CompressTexture(tex, TextureFormat.BC7, TextureCompressionQuality.Normal);
            return tex;
        }

        static void Copy(Texture2DArray arr, int slice, Texture2D tex, int mips)
        {
            for (int m = 0; m < mips; m++) arr.SetPixelData(tex.GetPixelData<byte>(m), m, slice);
            Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// The map of <paramref name="dir"/> ending in <paramref name="suffix"/>, read from its file (whatever its import settings) and
        /// boxed down to <see cref="Size"/>; <paramref name="fallback"/> everywhere when the folder has none.
        /// </summary>
        static Color32[] Read(string dir, string suffix, Color32 fallback)
        {
            var file = Directory.Exists(dir)
                ? Directory.GetFiles(dir).Where(f => !f.EndsWith(".meta")).FirstOrDefault(f =>
                    System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(f).ToLowerInvariant(), @"[_-](1|2|4|8|16)k$", "").EndsWith("_" + suffix))
                : null;
            var result = new Color32[Size * Size];
            if (file == null)
            {
                for (int p = 0; p < result.Length; p++) result[p] = fallback;
                return result;
            }
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                src.LoadImage(File.ReadAllBytes(file));
                var px = src.GetPixels32();
                int w = src.width, h = src.height;
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        // a box over the source texels this one covers (2k -> 1k: four)
                        int x0 = x * w / Size, x1 = Mathf.Max(x0 + 1, (x + 1) * w / Size), y0 = y * h / Size, y1 = Mathf.Max(y0 + 1, (y + 1) * h / Size);
                        int r = 0, g = 0, b = 0, a = 0, count = 0;
                        for (int yy = y0; yy < y1; yy++)
                            for (int xx = x0; xx < x1; xx++)
                            {
                                var c = px[yy * w + xx];
                                r += c.r; g += c.g; b += c.b; a += c.a;
                                count++;
                            }
                        result[y * Size + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), (byte)(a / count));
                    }
            }
            finally { Object.DestroyImmediate(src); }
            return result;
        }

        // ------------------------------------------------------------------ terrain layers and the material

        static void BuildLayers()
        {
            Directory.CreateDirectory(Folder.TrimEnd('/'));
            for (int i = 0; i < Layers.Length; i++)
            {
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath(i));
                bool made = layer == null;
                if (made) layer = new TerrainLayer();
                layer.name = $"{i} {Layers[i].Name}";
                string dir = NatureAssetImporter.TextureFolder + Layers[i].Folder;
                layer.diffuseTexture = AssetDatabase.FindAssets("t:Texture2D", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.Contains("_diff")).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault();
                layer.tileSize = new Vector2(Layers[i].Tile, Layers[i].Tile);
                if (made) AssetDatabase.CreateAsset(layer, LayerPath(i));
                else EditorUtility.SetDirty(layer);
            }
        }

        static void BuildMaterial()
        {
            string path = NatureMaterialBuilder.Dir + MaterialName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(ShaderName) ?? throw new InvalidOperationException("missing shader " + ShaderName);
            if (mat == null)
            {
                mat = new Material(shader) { name = MaterialName };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_GroundAlbedo", AssetDatabase.LoadAssetAtPath<Texture2DArray>(AlbedoPath));
            mat.SetTexture("_GroundNormal", AssetDatabase.LoadAssetAtPath<Texture2DArray>(NormalPath));
            mat.SetVector("_LayerTile0", new Vector4(Layers[0].Tile, Layers[1].Tile, Layers[2].Tile, Layers[3].Tile));
            mat.SetVector("_LayerTile1", new Vector4(Layers[4].Tile, Layers[5].Tile, Layers[6].Tile, Layers[7].Tile));
            mat.SetVector("_LayerBright0", new Vector4(Layers[0].Brightness, Layers[1].Brightness, Layers[2].Brightness, Layers[3].Brightness));
            mat.SetVector("_LayerBright1", new Vector4(Layers[4].Brightness, Layers[5].Brightness, Layers[6].Brightness, Layers[7].Brightness));
            mat.SetVector("_LayerTriplanar0", new Vector4(Tri(0), Tri(1), Tri(2), Tri(3)));
            mat.SetVector("_LayerTriplanar1", new Vector4(Tri(4), Tri(5), Tri(6), Tri(7)));
            mat.EnableKeyword("_ANTITILE");
            mat.SetFloat("_AntiTile", 1f);
            mat.EnableKeyword("_TERRAIN_HOLES");
            mat.SetFloat("_TerrainHoles", 1f);
            EditorUtility.SetDirty(mat);
        }

        static float Tri(int i) => Layers[i].Triplanar ? 1f : 0f;
    }
}
