using System.IO;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Sets the import settings of the texture sets you drop under <c>Assets/Art/Textures/Industrial</c> (ARCHITECTURE §25.2),
    /// from the file name suffix (ambientCG and Poly Haven names are understood). Applied on a texture's first import only,
    /// so a setting you change by hand afterwards is never overwritten.
    /// </summary>
    public class IndustrialTextureImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Art/Textures/Industrial/";

        public enum MapKind { Unknown, Color, Normal, NormalDirectX, Linear, Decal, Grunge }

        static readonly string[] ColorSuffixes = { "basecolor", "albedo", "color", "diff", "diffuse" };
        static readonly string[] NormalSuffixes = { "normal", "normalgl", "nor_gl", "normal_gl" };
        static readonly string[] NormalDirectXSuffixes = { "normaldx", "nor_dx", "normal_dx" };
        static readonly string[] LinearSuffixes = { "roughness", "rough", "metalness", "metallic", "metal", "ao", "ambientocclusion", "occlusion", "gloss", "smoothness" };

        /// <summary>The kind of map a file is, from the end of its name (<c>Concrete034_2K-PNG_NormalGL.png</c> is a Normal).</summary>
        public static MapKind Classify(string path)
        {
            string folder = path.Replace('\\', '/');
            if (folder.Contains("/Decals/")) return MapKind.Decal;     // any file of the decals folder: RGBA, alpha is the shape
            if (folder.Contains("/Grunge/")) return MapKind.Grunge;    // any file of the grunge folder: a linear mask in R
            string name = System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), @"[_-](1|2|4|8|16)k$", "");   // Poly Haven: ..._diff_2k
            if (Ends(name, NormalDirectXSuffixes)) return MapKind.NormalDirectX;
            if (Ends(name, NormalSuffixes)) return MapKind.Normal;
            if (Ends(name, LinearSuffixes)) return MapKind.Linear;
            if (Ends(name, ColorSuffixes)) return MapKind.Color;
            return MapKind.Unknown;
        }

        static bool Ends(string name, string[] suffixes)
        {
            foreach (var s in suffixes)
                if (name.EndsWith("_" + s) || name.EndsWith("-" + s)) return true;
            return false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder) || !assetImporter.importSettingsMissing) return;
            var kind = Classify(assetPath);
            if (kind == MapKind.Unknown) return;   // displacement, opacity, previews: not used by the shader, left as imported
            var importer = (TextureImporter)assetImporter;
            if (kind == MapKind.Decal)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 2048;
                return;
            }
            importer.textureType = kind == MapKind.Normal || kind == MapKind.NormalDirectX ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = kind == MapKind.Color;   // linear for roughness, metallic, AO and grunge
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (kind == MapKind.NormalDirectX)
                Debug.LogWarning($"[IndustrialTextureImporter] {assetPath} is a DirectX normal map: tick Flip Normal Y on the material (or download the OpenGL one).");
        }
    }
}
