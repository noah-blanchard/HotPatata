using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The industrial material library (ARCHITECTURE §25.2): concrete floors (plain, steel plate, tile, rough grit), walls (plaster,
    /// brick, sheet panel, block), ceilings (concrete, panel), painted, raw and rusty metal, rubber, plus the lamps, hazard stripes
    /// and slot-coloured pipes. One folder of textures per material under <c>Assets/Art/Textures/Industrial</c>.
    /// <para>
    /// Materials are created only when missing, so what you set in the Inspector survives every rebuild; <c>Assign Industrial
    /// Textures</c> and <c>Reset Industrial Placeholders</c> are the explicit ways to refill them. A material whose own folder is
    /// empty borrows the texture set of its <i>fallback</i> folder with its own tint, scale and brightness, so the library already
    /// reads as varied surfaces before any download: drop a real set in a material's folder and run the menu again.
    /// </para>
    /// </summary>
    public static class IndustrialMaterialBuilder
    {
        public const string Dir = CourseKit.MatDir + "Industrial/";
        public const string ShaderName = "HotPatata/Industrial";
        const string TextureDir = IndustrialTextureImporter.Folder;

        public struct Surface
        {
            public string name, folder, fallback;
            public Color placeholder;                // the flat colour while no texture at all is found
            public Color fallbackTint;               // the tint over a borrowed set
            public Color ownTint;                    // the tint over the surface's own set (white unless the set needs recolouring)
            public float fallbackBrightness, ownBrightness, saturation;
            public float tile, smoothMax, variation, macro, grunge;
            public bool antiTile;
            public float metalScale;                 // how much of a borrowed or own metalness map to keep (a ceiling panel is not a mirror)
        }

        static Surface S(string name, string folder, string fallback, Color placeholder, Color fallbackTint, float fallbackBrightness, float ownBrightness,
                         float tile, float smoothMax, bool antiTile, float grunge, float saturation = 1f, Color? ownTint = null, float metalScale = 1f) =>
            new Surface { name = name, folder = folder, fallback = fallback, placeholder = placeholder, fallbackTint = fallbackTint, fallbackBrightness = fallbackBrightness,
                          ownBrightness = ownBrightness, ownTint = ownTint ?? Color.white, metalScale = metalScale, tile = tile, smoothMax = smoothMax, antiTile = antiTile, grunge = grunge, saturation = saturation,
                          variation = 0.14f, macro = 0.3f };

        static Color C(float r, float g, float b) => new Color(r, g, b);

        /// <summary>Every textured material of the library, with the folder that feeds it and the set it borrows until that folder has one.</summary>
        public static readonly Surface[] Library =
        {
            // legacy, generic
            S("Industrial_Concrete", "Concrete", null, C(0.58f, 0.57f, 0.55f), Color.white, 1.8f, 1.8f, 2f, 0.9f, true, 0.35f),
            // PaintedMetal004 is a saturated red paint: kept as a grey wear map recoloured by the tint (steel), so the structure reads cold
            // against the warm lamps and never takes hazard red (spec section 19).
            S("Industrial_PaintedMetal", "PaintedMetal", null, C(0.36f, 0.42f, 0.48f), C(0.55f, 0.64f, 0.74f), 2.2f, 2.2f, 1f, 0.9f, false, 0.2f, 0f, C(0.55f, 0.64f, 0.74f)),
            S("Industrial_RawMetal", "RawMetal", null, C(0.66f, 0.67f, 0.69f), Color.white, 1f, 1f, 1f, 0.9f, true, 0.2f),
            S("Industrial_Rubber", "Rubber", null, C(0.22f, 0.22f, 0.23f), Color.white, 1.5f, 1f, 0.5f, 0.9f, false, 0.2f),
            // floors
            S("Industrial_Floor_Concrete", "FloorConcrete", "Concrete", C(0.55f, 0.55f, 0.53f), Color.white, 1.8f, 1.5f, 2.5f, 0.9f, true, 0.5f),
            S("Industrial_Floor_Plate", "FloorPlate", "RawMetal", C(0.55f, 0.57f, 0.6f), C(0.8f, 0.84f, 0.9f), 1.2f, 1f, 1f, 0.9f, false, 0.3f, 1f, null, 0.55f),
            S("Industrial_Floor_Tile", "FloorTile", "Concrete", C(0.6f, 0.62f, 0.64f), C(0.85f, 0.92f, 1f), 2f, 1f, 1.5f, 0.95f, false, 0.35f),
            S("Industrial_Floor_Grit", "FloorGrit", "Concrete", C(0.4f, 0.39f, 0.38f), C(0.66f, 0.64f, 0.6f), 2.3f, 1.4f, 3.5f, 0.8f, true, 0.7f),
            // walls
            S("Industrial_Wall_Plaster", "WallPlaster", "Concrete", C(0.62f, 0.58f, 0.52f), C(1f, 0.93f, 0.8f), 1.9f, 1.5f, 3f, 0.85f, true, 0.55f),
            S("Industrial_Wall_Brick", "WallBrick", "Concrete", C(0.55f, 0.32f, 0.25f), C(0.95f, 0.5f, 0.36f), 1.9f, 1.4f, 1.6f, 0.8f, false, 0.55f),
            S("Industrial_Wall_Panel", "WallPanel", "PaintedMetal", C(0.35f, 0.45f, 0.42f), C(0.55f, 0.7f, 0.66f), 2.4f, 1.2f, 1.6f, 0.9f, false, 0.4f, 0f, null, 0.3f),
            S("Industrial_Wall_Block", "WallBlock", "Concrete", C(0.5f, 0.52f, 0.55f), C(0.72f, 0.76f, 0.8f), 1.9f, 1.4f, 1f, 0.8f, false, 0.5f),
            // ceilings
            S("Industrial_Ceiling_Concrete", "CeilingConcrete", "Concrete", C(0.4f, 0.4f, 0.43f), C(0.55f, 0.55f, 0.6f), 2.6f, 1.6f, 3f, 0.8f, true, 0.7f),
            S("Industrial_Ceiling_Panel", "CeilingPanel", "RawMetal", C(0.42f, 0.44f, 0.47f), C(0.62f, 0.64f, 0.68f), 3.2f, 1.6f, 1.4f, 0.9f, false, 0.45f, 1f, null, 0.1f),
            // metal
            S("Industrial_Metal_Rust", "MetalRust", "RawMetal", C(0.5f, 0.3f, 0.2f), C(0.85f, 0.48f, 0.28f), 1.3f, 1f, 1f, 0.7f, true, 0.6f, 1f, null, 0.7f),
        };

        // Accents are Material Variants of the painted metal: they inherit its textures and only override the tint.
        const string YellowName = "Industrial_PaintedMetal_Yellow", SafetyName = "Industrial_PaintedMetal_Safety", HazardName = "Industrial_Hazard";
        public const string LampName = "Industrial_Lamp", GlowName = "Industrial_Glow";
        public static string PipeName(int slot) => "Industrial_Pipe_" + slot;
        // The tube and cannon slot colours (one colour and one pip count per mouth-exit pair, BombObstacleKitBuilder): kept, in paint.
        static readonly Color[] PipeColors = { new Color(0.25f, 0.85f, 0.45f), new Color(0.62f, 0.45f, 1f), new Color(1f, 0.6f, 0.8f) };
        static readonly Color Yellow = new Color(0.72f, 0.60f, 0.22f);   // muted industrial yellow (structure, frames, railings)
        static readonly Color Safety = new Color(0.86f, 0.88f, 0.90f);   // pale cool edge: landing edges, never a warm gameplay hue

        [MenuItem("HotPatata/Course/Build Industrial Materials")]
        public static void BuildAll() => Ensure(false);

        [MenuItem("HotPatata/Course/Reset Industrial Placeholders")]
        public static void ResetPlaceholders()
        {
            if (!EditorUtility.DisplayDialog("Reset Industrial Placeholders",
                    "Resets the industrial materials to their placeholder values and refills them from the texture folders.", "Reset", "Cancel")) return;
            Ensure(true);
        }

        /// <summary>Creates what is missing (everything when <paramref name="reset"/>); never touches an existing material otherwise.</summary>
        public static void Ensure(bool reset)
        {
            var shader = Shader.Find(ShaderName) ?? throw new System.InvalidOperationException("missing shader " + ShaderName);
            Directory.CreateDirectory(Dir.TrimEnd('/'));
            IndustrialTextureFactory.EnsurePlaceholders();
            // one empty folder per material, so you can see where each texture set goes
            foreach (var surface in Library) Directory.CreateDirectory(TextureDir + surface.folder);
            AssetDatabase.Refresh();
            foreach (var surface in Library)
            {
                string path = Dir + surface.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && !reset) continue;
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                Assign(mat, surface, true);
            }
            var painted = AssetDatabase.LoadAssetAtPath<Material>(Dir + "Industrial_PaintedMetal.mat");
            EnsureLamp(shader, reset, LampName, new Color(4.2f, 2.6f, 1.1f));
            EnsureLamp(shader, reset, GlowName, new Color(2.6f, 0.75f, 0.2f));
            EnsureVariant(YellowName, painted, Yellow, reset);
            EnsureVariant(SafetyName, painted, Safety, reset);
            EnsureVariant(HazardName, painted, new Color(0.95f, 0.22f, 0.14f), reset, m =>
            {
                m.SetFloat("_StripeStrength", 0.92f);
                m.SetColor("_StripeColor", new Color(0.03f, 0.03f, 0.03f));
                m.SetFloat("_StripeScale", 0.45f);
            });
            for (int i = 0; i < PipeColors.Length; i++)
                EnsureVariant(PipeName(i + 1), painted, PipeColors[i], reset);
            AssetDatabase.SaveAssets();
            Debug.Log("[IndustrialMaterialBuilder] industrial materials ready in " + Dir);
        }

        static void EnsureLamp(Shader shader, bool reset, string name, Color emission)
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
            mat.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(mat);
        }

        static void EnsureVariant(string name, Material parent, Color tint, bool reset, System.Action<Material> extra = null)
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
            extra?.Invoke(mat);
            EditorUtility.SetDirty(mat);
        }

        // ------------------------------------------------------------------ your textures

        [MenuItem("HotPatata/Course/Assign Industrial Textures")]
        public static void AssignTextures()
        {
            Ensure(false);
            foreach (var surface in Library)
                Assign(AssetDatabase.LoadAssetAtPath<Material>(Dir + surface.name + ".mat"), surface, false);
            AssetDatabase.SaveAssets();
        }

        public struct TextureSet
        {
            public Texture2D color, normal, rough, gloss, metal, ao;
            public bool normalIsDirectX;
        }

        static Texture2D Find(string folder, params string[] suffixes)
        {
            if (!AssetDatabase.IsValidFolder(TextureDir + folder)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureDir + folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), @"[_-](1|2|4|8|16)k$", "");
                if (suffixes.Any(s => name.EndsWith("_" + s) || name.EndsWith("-" + s))) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        /// <summary>The maps found in a texture folder, by file name (see <see cref="IndustrialTextureImporter"/>).</summary>
        public static TextureSet FindSet(string folder)
        {
            var set = new TextureSet
            {
                color = Find(folder, "basecolor", "albedo", "color", "diff", "diffuse"),
                normal = Find(folder, "normalgl", "nor_gl", "normal_gl", "normal"),
                rough = Find(folder, "roughness", "rough"),
                metal = Find(folder, "metalness", "metallic", "metal"),
                ao = Find(folder, "ambientocclusion", "ao", "occlusion")
            };
            if (set.normal == null) { set.normal = Find(folder, "normaldx", "nor_dx", "normal_dx"); set.normalIsDirectX = set.normal != null; }
            if (set.rough == null) set.gloss = Find(folder, "gloss", "smoothness");
            return set;
        }

        /// <summary>
        /// Puts the texture set of the surface's folder on its material, or the fallback set under the fallback tint, and sets the
        /// scale, anti-tiling and grunge the surface asks for. Explicit menu or a new material only: no rebuild calls it.
        /// </summary>
        public static void Assign(Material mat, Surface surface, bool quiet)
        {
            if (mat == null) return;
            var own = FindSet(surface.folder);
            bool hasOwn = own.color != null;
            var set = hasOwn || surface.fallback == null ? own : FindSet(surface.fallback);
            mat.SetFloat("_TileSize", surface.tile);
            mat.SetFloat("_VariationStrength", surface.variation);
            mat.SetFloat("_MacroStrength", surface.macro);
            mat.SetFloat("_AntiTile", surface.antiTile ? 1f : 0f);
            if (surface.antiTile) mat.EnableKeyword("_ANTITILE"); else mat.DisableKeyword("_ANTITILE");
            mat.SetFloat("_BaseSaturation", surface.saturation);
            mat.SetColor("_ShadeColor", new Color(0.95f, 0.92f, 0.9f));   // a neutral sky fill: interiors have no violet sky
            AssignGrunge(mat, surface);
            if (set.color == null)
            {
                mat.SetColor("_BaseColor", surface.placeholder);   // no texture at all: a flat colour with the procedural variation
                mat.SetFloat("_BaseBrightness", 1f);
                mat.SetFloat("_SmoothnessMin", 0.05f);
                mat.SetFloat("_SmoothnessMax", 0.4f);
                EditorUtility.SetDirty(mat);
                return;
            }
            mat.SetTexture("_BaseMap", set.color);
            mat.SetTexture("_BumpMap", set.normal);
            mat.SetFloat("_FlipNormalY", set.normalIsDirectX ? 1f : 0f);
            mat.SetTexture("_GlossMap", set.rough != null ? set.rough : set.gloss);
            mat.SetFloat("_GlossIsRoughness", set.rough != null ? 1f : 0f);
            mat.SetTexture("_MetallicMap", set.metal);
            mat.SetTexture("_OcclusionMap", set.ao);
            mat.SetColor("_BaseColor", hasOwn ? surface.ownTint : surface.fallbackTint);
            mat.SetFloat("_BaseBrightness", hasOwn ? surface.ownBrightness : surface.fallbackBrightness);
            if (set.rough != null || set.gloss != null) { mat.SetFloat("_SmoothnessMin", 0f); mat.SetFloat("_SmoothnessMax", surface.smoothMax); }
            mat.SetFloat("_Metallic", set.metal != null ? surface.metalScale : 0f);
            EditorUtility.SetDirty(mat);
            if (!quiet)
                Debug.Log($"[IndustrialMaterialBuilder] {surface.name}: {(hasOwn ? surface.folder : surface.fallback + " (borrowed)")} color={set.color.name} normal={set.normal?.name} rough={(set.rough ?? set.gloss)?.name} metal={set.metal?.name} ao={set.ao?.name}");
        }

        static void AssignGrunge(Material mat, Surface surface)
        {
            var guids = AssetDatabase.IsValidFolder(TextureDir + "Grunge") ? AssetDatabase.FindAssets("t:Texture2D", new[] { TextureDir + "Grunge" }) : new string[0];
            if (guids.Length == 0 || surface.grunge <= 0f) { mat.SetFloat("_GrungeStrength", 0f); return; }
            int index = Mathf.Abs(surface.name.GetHashCode()) % guids.Length;
            mat.SetTexture("_GrungeMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids.OrderBy(g => g).ElementAt(index))));
            mat.SetFloat("_GrungeStrength", surface.grunge);
            mat.SetFloat("_GrungeSize", 7f + (Mathf.Abs(surface.name.GetHashCode()) % 7));
        }
    }
}
