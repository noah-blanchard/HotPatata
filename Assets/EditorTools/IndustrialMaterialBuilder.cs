using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The industrial material set (ARCHITECTURE §25.2): concrete, painted metal, raw metal, rubber, and two painted-metal
    /// accent variants. Created only when missing, so the textures and settings you set in the Inspector survive every
    /// rebuild of the kit or of a course; <c>Reset Industrial Placeholders</c> is the one explicit way back to the defaults.
    /// Every material is usable without a texture (a flat colour with a faint procedural variation).
    /// </summary>
    public static class IndustrialMaterialBuilder
    {
        public const string Dir = CourseKit.MatDir + "Industrial/";
        public const string ShaderName = "HotPatata/Industrial";

        struct Spec
        {
            public string name;
            public Color color;
            public float smoothMin, smoothMax, metallic, tile, variation, macro;
        }

        static readonly Spec Concrete = new Spec { name = "Industrial_Concrete", color = new Color(0.58f, 0.57f, 0.55f), smoothMin = 0.05f, smoothMax = 0.35f, metallic = 0f, tile = 2f, variation = 0.10f, macro = 0.35f };
        static readonly Spec PaintedMetal = new Spec { name = "Industrial_PaintedMetal", color = new Color(0.36f, 0.42f, 0.48f), smoothMin = 0.2f, smoothMax = 0.65f, metallic = 0f, tile = 1f, variation = 0.06f, macro = 0.25f };
        static readonly Spec RawMetal = new Spec { name = "Industrial_RawMetal", color = new Color(0.66f, 0.67f, 0.69f), smoothMin = 0.25f, smoothMax = 0.7f, metallic = 1f, tile = 1f, variation = 0.12f, macro = 0.3f };
        static readonly Spec Rubber = new Spec { name = "Industrial_Rubber", color = new Color(0.22f, 0.22f, 0.23f), smoothMin = 0.05f, smoothMax = 0.25f, metallic = 0f, tile = 0.5f, variation = 0.05f, macro = 0.2f };

        // Accents are Material Variants of the painted metal: they inherit its textures and only override the tint.
        const string YellowName = "Industrial_PaintedMetal_Yellow", SafetyName = "Industrial_PaintedMetal_Safety";
        static readonly Color Yellow = new Color(0.72f, 0.60f, 0.22f);   // muted industrial yellow (structure, frames, railings)
        static readonly Color Safety = new Color(0.86f, 0.88f, 0.90f);   // pale cool edge: landing edges, never a warm gameplay hue

        [MenuItem("HotPatata/Course/Build Industrial Materials")]
        public static void BuildAll() => Ensure(false);

        [MenuItem("HotPatata/Course/Reset Industrial Placeholders")]
        public static void ResetPlaceholders()
        {
            if (!EditorUtility.DisplayDialog("Reset Industrial Placeholders",
                    "Resets the industrial materials to their placeholder values and clears every texture assigned to them.", "Reset", "Cancel")) return;
            Ensure(true);
        }

        /// <summary>Creates what is missing (everything when <paramref name="reset"/>); never touches an existing material otherwise.</summary>
        public static void Ensure(bool reset)
        {
            var shader = Shader.Find(ShaderName) ?? throw new System.InvalidOperationException("missing shader " + ShaderName);
            Directory.CreateDirectory(Dir.TrimEnd('/'));
            var concrete = EnsureBase(Concrete, shader, reset);
            var painted = EnsureBase(PaintedMetal, shader, reset);
            EnsureBase(RawMetal, shader, reset);
            EnsureBase(Rubber, shader, reset);
            EnsureLamp(shader, reset);
            EnsureLamp(shader, reset, GlowName, new Color(2.6f, 0.75f, 0.2f));
            EnsureVariant(YellowName, painted, Yellow, reset);
            EnsureVariant(SafetyName, painted, Safety, reset);
            AssetDatabase.SaveAssets();
            if (concrete != null) Debug.Log("[IndustrialMaterialBuilder] industrial materials ready in " + Dir);
        }

        static Material EnsureBase(Spec spec, Shader shader, bool reset)
        {
            string path = Dir + spec.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && !reset) return mat;
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
                foreach (var texture in new[] { "_BaseMap", "_BumpMap", "_GlossMap", "_MetallicMap", "_OcclusionMap" }) mat.SetTexture(texture, null);
            }
            mat.SetColor("_BaseColor", spec.color);
            mat.SetFloat("_TileSize", spec.tile);
            mat.SetFloat("_SmoothnessMin", spec.smoothMin);
            mat.SetFloat("_SmoothnessMax", spec.smoothMax);
            mat.SetFloat("_Metallic", spec.metallic);
            mat.SetFloat("_VariationStrength", spec.variation);
            mat.SetFloat("_MacroStrength", spec.macro);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public const string LampName = "Industrial_Lamp";

        /// <summary>The bulb of a lamp fitting: a dark shade that glows warm (emission, so it blooms above 1).</summary>
        public const string GlowName = "Industrial_Glow";

        static void EnsureLamp(Shader shader, bool reset, string name = LampName, Color? emission = null)
        {
            string path = Dir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && !reset) return;
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", new Color(0.12f, 0.1f, 0.08f));
            mat.SetFloat("_MacroStrength", 0f);
            mat.SetFloat("_VariationStrength", 0f);
            mat.SetColor("_EmissionColor", emission ?? new Color(4.2f, 2.6f, 1.1f));
            EditorUtility.SetDirty(mat);
        }

        // ------------------------------------------------------------------ your textures

        const string TextureDir = IndustrialTextureImporter.Folder;

        [MenuItem("HotPatata/Course/Assign Industrial Textures")]
        public static void AssignTextures()
        {
            Ensure(false);
            Assign("Industrial_Concrete", "Concrete");
            Assign("Industrial_PaintedMetal", "PaintedMetal");
            // PaintedMetal004 is a saturated red paint: kept as a grey wear map, recoloured by the tint (steel here, yellow in the variant)
            // so the structure reads cold against the warm lamps and never takes hazard red (spec §19).
            var painted = AssetDatabase.LoadAssetAtPath<Material>(Dir + "Industrial_PaintedMetal.mat");
            painted.SetFloat("_BaseSaturation", 0f);
            painted.SetFloat("_BaseBrightness", 2.2f);
            painted.SetColor("_BaseColor", new Color(0.55f, 0.64f, 0.74f));
            EditorUtility.SetDirty(painted);
            Assign("Industrial_RawMetal", "RawMetal");
            Assign("Industrial_Rubber", "Rubber");
            AssetDatabase.SaveAssets();
        }

        static Texture2D Find(string folder, params string[] suffixes)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureDir + folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), @"[_-](1|2|4|8|16)k$", "");
                if (suffixes.Any(s => name.EndsWith("_" + s) || name.EndsWith("-" + s))) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        /// <summary>
        /// Puts the texture set found in <c>Assets/Art/Textures/Industrial/&lt;folder&gt;</c> on a material (by file name, see
        /// <see cref="IndustrialTextureImporter"/>) and sets the values a textured material needs. Explicit menu only: no rebuild calls it.
        /// </summary>
        public static void Assign(string materialName, string folder)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Dir + materialName + ".mat");
            if (mat == null) return;
            var color = Find(folder, "basecolor", "albedo", "color", "diff", "diffuse");
            var normal = Find(folder, "normalgl", "nor_gl", "normal_gl", "normal");
            var normalDx = normal == null ? Find(folder, "normaldx", "nor_dx", "normal_dx") : null;
            var rough = Find(folder, "roughness", "rough");
            var gloss = rough == null ? Find(folder, "gloss", "smoothness") : null;
            var metal = Find(folder, "metalness", "metallic", "metal");
            var ao = Find(folder, "ambientocclusion", "ao", "occlusion");
            mat.SetTexture("_BaseMap", color);
            mat.SetTexture("_BumpMap", normal != null ? normal : normalDx);
            mat.SetFloat("_FlipNormalY", normalDx != null ? 1f : 0f);
            mat.SetTexture("_GlossMap", rough != null ? rough : gloss);
            mat.SetFloat("_GlossIsRoughness", rough != null ? 1f : 0f);
            mat.SetTexture("_MetallicMap", metal);
            mat.SetTexture("_OcclusionMap", ao);
            if (color != null) mat.SetColor("_BaseColor", Color.white);
            if (rough != null || gloss != null) { mat.SetFloat("_SmoothnessMin", 0f); mat.SetFloat("_SmoothnessMax", 0.9f); }
            if (metal != null) mat.SetFloat("_Metallic", 1f);
            EditorUtility.SetDirty(mat);
            Debug.Log($"[IndustrialMaterialBuilder] {materialName}: color={color?.name} normal={(normal ?? normalDx)?.name} rough={(rough ?? gloss)?.name} metal={metal?.name} ao={ao?.name}");
        }

        static void EnsureVariant(string name, Material parent, Color tint, bool reset)
        {
            string path = Dir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && !reset) return;
            if (mat == null)
            {
                mat = new Material(parent.shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.parent = parent;
            mat.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(mat);
        }
    }
}
