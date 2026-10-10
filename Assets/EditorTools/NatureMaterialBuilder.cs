using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The nature material library of PatataWilds (ARCHITECTURE §25.3): ground, rock and cliff (triplanar, in metres), bark,
    /// planks and logs (the generated meshes' UVs, in metres), foliage cards (cut-out atlases with wind) and the Poly Haven props,
    /// plus the gameplay variants (charred hazard bands, the pale rotten planks of a falling platform, the blazed logs of a tube
    /// slot, lantern glass and embers). Every texture comes from the Poly Haven sets fetched by <c>tools/Fetch-PolyHaven.ps1</c>.
    /// <para>
    /// Materials are created when missing and filled from their folder; <c>Assign Nature Textures</c> refills them all (after a
    /// new download or a change to this table). Every material uses <c>HotPatata/Nature</c>.
    /// </para>
    /// </summary>
    public static class NatureMaterialBuilder
    {
        public const string Dir = CourseKit.MatDir + "Nature/";
        public const string ShaderName = "HotPatata/Nature";
        const string TextureRoot = NatureAssetImporter.TextureFolder;
        const string ModelRoot = NatureAssetImporter.ModelFolder;

        public enum Mapping { Triplanar, UV }

        public struct Surface
        {
            public string name, folder;         // folder under Textures/Nature (or <model id>/textures for props and foliage)
            public string mapPrefix;            // props and foliage: only maps whose name holds this (fir_sapling has branches and twigs)
            public bool model;                  // the folder is a model's texture folder
            public Mapping mapping;
            public float tile, smoothMax, brightness, saturation, variation, macro;
            public Color tint;
            public bool antiTile;
            public string top;                  // a top layer from this folder (moss, grass on faces looking up); null = none
            public float topCoverage;
            public bool foliage;                // cut-out, both faces, wind, light through
            public float wind;
        }

        static Surface Ground(string name, string folder, float tile, float brightness = 1f, Color? tint = null) =>
            new Surface { name = name, folder = folder, mapping = Mapping.Triplanar, tile = tile, smoothMax = 0.35f, brightness = brightness, saturation = 1f,
                          variation = 0.14f, macro = 0.35f, tint = tint ?? Color.white, antiTile = true };

        static Surface Rock(string name, string folder, float tile, string top, float coverage, float brightness = 1f, Color? tint = null) =>
            new Surface { name = name, folder = folder, mapping = Mapping.Triplanar, tile = tile, smoothMax = 0.45f, brightness = brightness, saturation = 1f,
                          variation = 0.16f, macro = 0.4f, tint = tint ?? Color.white, antiTile = true, top = top, topCoverage = coverage };

        static Surface Wood(string name, string folder, float tile, float brightness = 1f, Color? tint = null, bool antiTile = false) =>
            new Surface { name = name, folder = folder, mapping = Mapping.UV, tile = tile, smoothMax = 0.4f, brightness = brightness, saturation = 1f,
                          variation = 0.1f, macro = 0.15f, tint = tint ?? Color.white, antiTile = antiTile };

        static Surface Prop(string name, string model, float brightness = 1f) =>
            new Surface { name = name, folder = model + "/textures", mapPrefix = model + "_", model = true, mapping = Mapping.UV, tile = 1f, smoothMax = 0.45f,
                          brightness = brightness, saturation = 1f, variation = 0.06f, macro = 0f, tint = Color.white };

        static Surface Leaves(string name, string model, string prefix, float wind, Color? tint = null) =>
            new Surface { name = name, folder = model + "/textures", mapPrefix = prefix, model = true, mapping = Mapping.UV, tile = 1f, smoothMax = 0.35f,
                          brightness = 1f, saturation = 1f, variation = 0.12f, macro = 0f, tint = tint ?? Color.white, foliage = true, wind = wind };

        static Surface Spray(string name, NatureTextureFactory.Spray spray, float wind) =>
            new Surface { name = name, folder = "Generated", mapPrefix = NatureTextureFactory.SprayPrefix(spray), mapping = Mapping.UV, tile = 1f, smoothMax = 0.3f,
                          brightness = 1f, saturation = 1f, variation = 0.12f, macro = 0f, tint = Color.white, foliage = true, wind = wind };

        /// <summary>Every textured material of the library and the folder that feeds it.</summary>
        public static readonly Surface[] Library =
        {
            // ground (triplanar, hex tiled on the floor projection)
            Ground("Nature_ForestFloor", "ForestFloor", 3.5f),
            Ground("Nature_Trail", "Trail", 3f),
            Ground("Nature_RockPath", "RockPath", 2.5f),
            Ground("Nature_Gravel", "Gravel", 2f),
            Ground("Nature_Riverbed", "Riverbed", 2f),
            Ground("Nature_Grass", "Grass", 3f),
            Ground("Nature_GrassPath", "GrassPath", 3f),
            Ground("Nature_LeafLitter", "LeafLitter", 2.5f),
            Ground("Nature_ForestLeaves", "ForestLeaves", 2.5f),
            Ground("Nature_Mud", "Mud", 2.5f, 0.9f),
            // rock and cliff (triplanar, moss or grass on the faces that look up)
            Rock("Nature_MossyRock", "MossyRock", 3f, "Grass", 0.3f),
            Rock("Nature_RockPitted", "RockPitted", 2.5f, null, 0f),
            Rock("Nature_RockFace", "RockFace", 4f, "ForestLeaves", 0.25f),
            Rock("Nature_Cliff", "Cliff", 6f, "Grass", 0.35f),
            Rock("Nature_RockWall", "RockWall", 3f, null, 0f),
            Rock("Nature_RocksGround", "RocksGround", 2.5f, null, 0f),
            Rock("Nature_StoneWall", "StoneWall", 2.5f, null, 0f),
            // wood (UVs in metres on the generated logs and planks)
            Wood("Nature_PineBark", "PineBark", 1.6f),
            Wood("Nature_BarkBrown", "BarkBrown", 1.6f),
            Wood("Nature_BarkDark", "BarkDark", 1.6f),
            Wood("Nature_BarkWillow", "BarkWillow", 1.6f),
            Wood("Nature_RoughWood", "RoughWood", 1.4f),
            Wood("Nature_Planks", "Planks", 2f),
            Wood("Nature_PlanksClean", "PlanksClean", 2f),
            Wood("Nature_Gate", "Gate", 2f),
            Wood("Nature_LogWall", "LogWall", 2.5f),
            Wood("Nature_MossWood", "MossWood", 1.6f),
            // Poly Haven props (their own UVs)
            Prop("Nature_Prop_Boulder", "boulder_01"),
            Prop("Nature_Prop_RockMoss", "rock_moss_set_01"),
            Prop("Nature_Prop_RockFace", "rock_face_01"),
            Prop("Nature_Prop_Mountainside", "mountainside"),
            Prop("Nature_Prop_DeadTrunk", "dead_tree_trunk_02"),
            Prop("Nature_Prop_Stump", "tree_stump_01"),
            Prop("Nature_Prop_FirePit", "stone_fire_pit"),
            Prop("Nature_Prop_Lantern", "wooden_lantern_01"),
            Prop("Nature_Prop_Rock07", "rock_07"),
            Prop("Nature_Prop_Rock09", "rock_09"),
            Prop("Nature_Prop_Stone01", "stone_01"),
            Prop("Nature_Prop_Boulder02", "namaqualand_boulder_02"),
            Prop("Nature_Prop_Boulder04", "namaqualand_boulder_04"),
            Prop("Nature_Prop_RockFace02", "rock_face_02"),
            Prop("Nature_Prop_RockMoss2", "rock_moss_set_02"),
            Prop("Nature_Prop_Branches", "dry_branches_medium_01"),
            Prop("Nature_Prop_Stump2", "tree_stump_02"),
            Prop("Nature_Prop_DeadTrunk1", "dead_tree_trunk"),
            Prop("Nature_Prop_Roots", "root_cluster_01"),
            // foliage (cut-out atlases, both faces, wind)
            Spray("Nature_Leaves_Fir", NatureTextureFactory.Spray.Fir, 1f),
            Spray("Nature_Leaves_Pine", NatureTextureFactory.Spray.Pine, 1f),
            Leaves("Nature_Leaves_Broad", "shrub_03", "shrub_03_", 1.2f, new Color(0.92f, 1f, 0.85f)),
            Leaves("Nature_Fern", "fern_02", "fern_02_", 0.8f),
            Leaves("Nature_Shrub02", "shrub_02", "shrub_02_", 1f),
            Leaves("Nature_Shrub04", "shrub_04", "shrub_04_", 1f),
            Leaves("Nature_Weed", "weed_plant_02", "weed_plant_02_", 1.2f),
            Leaves("Nature_Nettle", "nettle_plant", "nettle_plant_", 1.1f),
            Leaves("Nature_Moss", "moss_01", "moss_01_", 0.15f),
            Spray("Nature_GrassBlades", NatureTextureFactory.Spray.Grass, 1.4f),
        };

        // gameplay variants (Material Variants: they inherit their parent's textures and override a few values)
        public const string HazardName = "Nature_Hazard", FallingName = "Nature_Falling", BeltName = "Nature_LogDrive",
            LanternName = "Nature_LanternGlass", EmberName = "Nature_Ember", BlazeName = "Nature_Blaze", LeafCanopyName = "Nature_LeafCanopy";
        public static string PipeName(int slot) => "Nature_Pipe_" + slot;
        // the tube and cannon slot colours, as in the KayKit and industrial kits (each mouth-exit pair: a colour and a pip count)
        static readonly Color[] PipeColors = { new Color(0.25f, 0.85f, 0.45f), new Color(0.62f, 0.45f, 1f), new Color(1f, 0.6f, 0.8f) };

        public static string Path(string name) => Dir + name + ".mat";

        [MenuItem("HotPatata/Nature/Build Nature Materials")]
        public static void BuildAll() => Ensure(false);

        [MenuItem("HotPatata/Nature/Assign Nature Textures")]
        public static void AssignAll() => Ensure(true);

        /// <summary>Creates what is missing and fills it; refills every material when <paramref name="refill"/>.</summary>
        public static void Ensure(bool refill)
        {
            var shader = Shader.Find(ShaderName) ?? throw new System.InvalidOperationException("missing shader " + ShaderName);
            Directory.CreateDirectory(Dir.TrimEnd('/'));
            NatureTextureFactory.EnsureTextures();
            AssetDatabase.Refresh();
            foreach (var surface in Library)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(Path(surface.name));
                bool created = mat == null;
                if (created)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, Path(surface.name));
                }
                if (created || refill) Assign(mat, surface);
            }
            var roughWood = Load("Nature_RoughWood");
            Variant(HazardName, roughWood, refill, m =>
            {
                // a log stained rust-red with charred black bands: a hazard never relies on colour alone (spec §19)
                m.SetColor("_BaseColor", new Color(1.15f, 0.5f, 0.32f));
                m.SetFloat("_BaseSaturation", 0.6f);
                m.SetFloat("_StripeStrength", 0.92f);
                m.SetColor("_StripeColor", new Color(0.035f, 0.025f, 0.02f));
                m.SetFloat("_StripeScale", 0.45f);
            });
            Variant(FallingName, Load("Nature_Planks"), refill, m =>
            {
                // rotten planks, pale with yellow lichen: always the same look, so a falling platform is learnt once
                m.SetColor("_BaseColor", new Color(1.2f, 1.08f, 0.72f));
                m.SetFloat("_BaseSaturation", 0.75f);
            });
            Variant(BeltName, Load("Nature_PineBark"), refill, m => m.SetColor("_BaseColor", new Color(0.95f, 0.9f, 0.85f)));
            Variant(LeafCanopyName, Load("Nature_ForestLeaves"), refill, m =>
            {
                // PatataCanopy's leaf roofs seen from under them: a dense mat of green leaves (it reads as solid: the bomb never passes)
                m.SetColor("_BaseColor", new Color(0.24f, 0.33f, 0.17f));   // the shade behind the leaf cards of the underside
                m.SetFloat("_BaseSaturation", 1.1f);
                m.SetFloat("_TileSize", 2f);
            });
            Variant(BlazeName, roughWood, refill, m =>
            {
                // a painted trail blaze (wayfinding): bright, unlit-looking paint on wood
                m.SetColor("_BaseColor", new Color(1.6f, 1.6f, 1.55f));
                m.SetFloat("_BaseSaturation", 0f);
            });
            for (int i = 0; i < PipeColors.Length; i++)
            {
                var c = PipeColors[i];
                Variant(PipeName(i + 1), roughWood, refill, m =>
                {
                    m.SetColor("_BaseColor", new Color(c.r * 1.6f, c.g * 1.6f, c.b * 1.6f));
                    m.SetFloat("_BaseSaturation", 0.15f);
                });
            }
            Emissive(shader, LanternName, new Color(0.35f, 0.28f, 0.18f), new Color(3.2f, 1.9f, 0.75f), refill);
            Emissive(shader, EmberName, new Color(0.08f, 0.05f, 0.04f), new Color(3.4f, 1.1f, 0.25f), refill);
            AssetDatabase.SaveAssets();
            Debug.Log("[NatureMaterialBuilder] nature materials ready in " + Dir);
        }

        public static Material Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(Path(name)) ?? throw new System.InvalidOperationException("missing material " + name + " (run HotPatata/Nature/Build Nature Materials)");

        static void Variant(string name, Material parent, bool refill, System.Action<Material> set)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Path(name));
            if (mat != null && !refill) return;
            if (mat == null)
            {
                mat = new Material(parent.shader);
                AssetDatabase.CreateAsset(mat, Path(name));
            }
            mat.parent = parent;
            set(mat);
            EditorUtility.SetDirty(mat);
        }

        static void Emissive(Shader shader, string name, Color baseColor, Color emission, bool refill)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Path(name));
            if (mat != null && !refill) return;
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, Path(name));
            }
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_MacroStrength", 0f);
            mat.SetFloat("_VariationStrength", 0f);
            mat.SetFloat("_TileSize", 1f);
            mat.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(mat);
        }

        // ------------------------------------------------------------------ textures

        static Texture2D FindMap(Surface surface, params string[] suffixes)
        {
            string folder = (surface.model ? ModelRoot : TextureRoot) + surface.folder;
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                string name = System.Text.RegularExpressions.Regex.Replace(System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), @"[_-](1|2|4|8|16)k$", "");
                if (surface.mapPrefix != null && !name.StartsWith(surface.mapPrefix)) continue;
                if (suffixes.Any(s => name.EndsWith("_" + s))) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        /// <summary>Fills <paramref name="mat"/> from the surface's folder and sets its mapping, scale and options.</summary>
        public static void Assign(Material mat, Surface surface)
        {
            var color = FindMap(surface, "diff", "diffuse", "basecolor", "color");
            var normal = FindMap(surface, "nor_gl", "normalgl", "normal");
            var rough = FindMap(surface, "rough", "roughness");
            var ao = FindMap(surface, "ao", "ambientocclusion");
            mat.SetTexture("_BaseMap", color);
            mat.SetTexture("_BumpMap", normal);
            mat.SetTexture("_RoughnessMap", rough);
            mat.SetTexture("_OcclusionMap", ao);
            mat.SetColor("_BaseColor", color != null ? surface.tint : new Color(0.45f, 0.42f, 0.36f));
            mat.SetFloat("_BaseBrightness", surface.brightness);
            mat.SetFloat("_BaseSaturation", surface.saturation);
            mat.SetFloat("_TileSize", surface.tile);
            mat.SetFloat("_SmoothnessMin", 0.02f);
            mat.SetFloat("_SmoothnessMax", rough != null ? surface.smoothMax : 0.25f);
            mat.SetFloat("_VariationStrength", surface.variation);
            mat.SetFloat("_MacroStrength", surface.macro);
            Keyword(mat, "_Triplanar", "_MAPPING_TRIPLANAR", surface.mapping == Mapping.Triplanar);
            Keyword(mat, "_AntiTile", "_ANTITILE", surface.antiTile);

            Texture2D topColor = null, topNormal = null;
            bool top = surface.top != null && FindTop(surface.top, out topColor, out topNormal);
            Keyword(mat, "_TopLayer", "_TOPLAYER", top);
            if (top)
            {
                mat.SetTexture("_TopMap", topColor);
                mat.SetTexture("_TopBumpMap", topNormal);
                mat.SetFloat("_TopCoverage", surface.topCoverage);
                mat.SetFloat("_TopTileSize", 3f);
            }

            var alpha = surface.foliage ? FindMap(surface, "alpha", "opacity") : null;
            Keyword(mat, "_AlphaClip", "_ALPHATEST_ON", alpha != null);
            mat.SetTexture("_AlphaMap", alpha);
            mat.SetFloat("_Cull", surface.foliage ? (float)UnityEngine.Rendering.CullMode.Off : (float)UnityEngine.Rendering.CullMode.Back);
            mat.SetFloat("_Translucency", surface.foliage ? 0.8f : 0f);
            Keyword(mat, "_Wind", "_WIND", surface.wind > 0f);
            mat.SetFloat("_WindStrength", surface.wind);
            mat.SetFloat("_WindVertexColor", surface.foliage ? 1f : 0f);
            mat.enableInstancing = true;   // FoliageInstancer draws plants and rocks instanced
            if (surface.foliage) mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            else mat.renderQueue = -1;
            EditorUtility.SetDirty(mat);
        }

        static bool FindTop(string folder, out Texture2D color, out Texture2D normal)
        {
            var s = new Surface { folder = folder };
            color = FindMap(s, "diff", "diffuse", "basecolor", "color");
            normal = FindMap(s, "nor_gl", "normalgl", "normal");
            return color != null;
        }

        static void Keyword(Material mat, string property, string keyword, bool on)
        {
            mat.SetFloat(property, on ? 1f : 0f);
            if (on) mat.EnableKeyword(keyword); else mat.DisableKeyword(keyword);
        }
    }
}
