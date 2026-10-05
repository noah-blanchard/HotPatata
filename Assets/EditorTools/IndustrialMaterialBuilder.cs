using System.IO;
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
