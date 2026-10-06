using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// First-import settings for the PatataWilds assets fetched from Poly Haven (ARCHITECTURE §25.3, tools/Fetch-PolyHaven.ps1):
    /// texture sets and model textures from their file name suffix, HDRIs as reflection cubemaps, models without
    /// materials, cameras, lights or colliders, and ambient loops as compressed mono. Applied on a file's first import only,
    /// so a setting changed by hand afterwards is never overwritten.
    /// </summary>
    public class NatureAssetImporter : AssetPostprocessor
    {
        public const string TextureFolder = "Assets/Art/Textures/Nature/";
        public const string ModelFolder = "Assets/Art/Models/Nature/";
        public const string HdriFolder = "Assets/Art/Sky/HDRI/";
        public const string AmbientFolder = "Assets/Audio/Ambient/";

        public enum NatureMapKind { Unknown, Color, Normal, Linear, Alpha }

        /// <summary>The kind of a Nature texture from its Poly Haven name: <c>fern_02_alpha_1k.png</c> is an Alpha (cut-out) map.</summary>
        public static NatureMapKind Classify(string path)
        {
            string name = System.Text.RegularExpressions.Regex.Replace(System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), @"[_-](1|2|4|8|16)k$", "");
            if (name.EndsWith("_alpha") || name.EndsWith("_opacity")) return NatureMapKind.Alpha;
            if (name.EndsWith("_mask")) return NatureMapKind.Linear;
            switch (IndustrialTextureImporter.Classify(path))
            {
                case IndustrialTextureImporter.MapKind.Color: return NatureMapKind.Color;
                case IndustrialTextureImporter.MapKind.Normal: return NatureMapKind.Normal;
                case IndustrialTextureImporter.MapKind.Linear: return NatureMapKind.Linear;
                default: return NatureMapKind.Unknown;   // DirectX normals and packed ARM maps are never fetched
            }
        }

        static bool IsNatureTexture(string path) =>
            path.StartsWith(TextureFolder) || (path.StartsWith(ModelFolder) && path.Contains("/textures/"));

        void OnPreprocessTexture()
        {
            if (!assetImporter.importSettingsMissing) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(HdriFolder))
            {
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.TextureCube;
                importer.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;   // glossy reflections and the sky
                importer.SetTextureSettings(settings);
                return;
            }
            if (!IsNatureTexture(assetPath)) return;
            var kind = Classify(assetPath);
            if (kind == NatureMapKind.Unknown) return;
            importer.textureType = kind == NatureMapKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = kind == NatureMapKind.Color;
            importer.alphaSource = kind == NatureMapKind.Alpha ? TextureImporterAlphaSource.FromGrayScale : TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.anisoLevel = kind == NatureMapKind.Alpha ? 2 : 8;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (kind == NatureMapKind.Alpha)
            {
                importer.mipMapsPreserveCoverage = true;   // thin needles and blades keep their coverage in the distance
                importer.alphaTestReferenceValue = 0.5f;
            }
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelFolder) || !assetImporter.importSettingsMissing) return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;   // materials come from NatureMaterialBuilder
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.bakeAxisConversion = true;
            importer.isReadable = false;
            ApplyMeshLods(importer);
        }

        /// <summary>Scanned props are heavy (20k to 150k triangles): Unity's mesh LODs thin them out with distance.</summary>
        public static void ApplyMeshLods(ModelImporter importer)
        {
            importer.generateMeshLods = true;
            importer.maximumMeshLod = 6;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ModelFolder)) return;
            // A collider left on a model would explode a thrown bomb (spec §6): decoration never collides.
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AmbientFolder) || !assetImporter.importSettingsMissing) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            var s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.5f;
            importer.defaultSampleSettings = s;
        }
    }
}
