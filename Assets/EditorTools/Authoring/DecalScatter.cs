using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// Decals (ARCHITECTURE §27.5): moss, mud, leaf and wet patches as alpha meshes laid on the surface (a grid cast onto the ground or a
    /// rock face), drawn with HotPatata/Nature in its decal mode (<c>_DECAL</c>: alpha blended, drawn after the opaque, pulled toward the
    /// camera, fogged, casting no shadow), never URP's screen-space decals (they would skip the height mist). The patches are cut from Poly
    /// Haven CC0 surfaces (<c>concrete_moss</c> keeping its greenest texels, <c>brown_mud_02</c>, <c>dry_decay_leaves</c>), four a kind, their
    /// alpha a noise blob times the surface's own height so their edges break along its relief; the wet patch is the shader's only (a darker,
    /// glossier blob). Placed round a cliff's foot (<see cref="CliffShape.Decals"/>) and by hand from the palette (a <see cref="DecalShape"/>).
    /// </summary>
    public static class DecalScatter
    {
        public const string Folder = "Assets/Art/Generated/Ground/";
        const int Cell = 512, Atlas = Cell * 2;
        /// <summary>The decal's grid: this many quads a side, cast onto the surface.</summary>
        const int Grid = 8;

        static readonly (DecalKind kind, string folder)[] Sources =
        {
            (DecalKind.Moss, "DecalMoss"), (DecalKind.Mud, "DecalMud"), (DecalKind.Leaves, "DecalLeaves"), (DecalKind.Wet, null),
        };

        public static string MaterialName(DecalKind kind) => "Nature_Decal_" + kind;
        static string AlbedoPath(DecalKind kind) => Folder + "Decal_" + kind + ".asset";
        static string NormalPath(DecalKind kind) => Folder + "Decal_" + kind + "_Normal.asset";

        /// <summary>The decal material of <paramref name="kind"/> (its textures and material made when missing).</summary>
        public static Material Material(DecalKind kind)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(NatureMaterialBuilder.Dir + MaterialName(kind) + ".mat");
            if (mat != null && AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath(kind)) != null) return mat;
            Build(kind);
            return NatureMaterialBuilder.Load(MaterialName(kind));
        }

        [MenuItem("HotPatata/Look/Build Decals", priority = 25)]
        public static void BuildAll()
        {
            foreach (var (kind, _) in Sources) Build(kind);
            AssetDatabase.SaveAssets();
            Debug.Log("[Decals] moss, mud, leaf and wet patches built.");
        }

        static void Build(DecalKind kind)
        {
            Directory.CreateDirectory(Folder.TrimEnd('/'));
            string folder = Sources.First(s => s.kind == kind).folder;
            var albedo = new Color32[Atlas * Atlas];
            var normal = new Color32[Atlas * Atlas];
            Color32[] diff = null, nor = null, disp = null;
            int w = 0, h = 0;
            if (folder != null)
            {
                string dir = NatureAssetImporter.TextureFolder + folder;
                (diff, w, h) = Read(dir, "diff");
                (nor, _, _) = Read(dir, "nor_gl");
                (disp, _, _) = Read(dir, "disp");
            }
            for (int v = 0; v < 4; v++)
            {
                int ox = (v % 2) * Cell, oy = (v / 2) * Cell;
                // each variant a crop of the surface at its own offset
                int sx = diff != null ? (v * 389 + 101) % Mathf.Max(1, w - Cell) : 0, sy = diff != null ? (v * 613 + 57) % Mathf.Max(1, h - Cell) : 0;
                for (int y = 0; y < Cell; y++)
                    for (int x = 0; x < Cell; x++)
                    {
                        float u = (x + 0.5f) / Cell * 2f - 1f, t = (y + 0.5f) / Cell * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + t * t);
                        float n = Mathf.PerlinNoise(x * 0.012f + v * 13.1f, y * 0.012f + v * 7.3f) * 0.65f + Mathf.PerlinNoise(x * 0.04f + v * 3.3f, y * 0.04f) * 0.35f;
                        float blob = Mathf.Clamp01((1f - r) * 1.6f + (n - 0.5f) * 1.2f);
                        Color32 c, nn;
                        float height = 0.5f;
                        if (diff != null)
                        {
                            int i = (sy + y) * w + sx + x;
                            c = diff[i];
                            nn = nor[i];
                            height = disp[i].r / 255f;
                        }
                        else
                        {
                            c = new Color32(70, 64, 56, 255);
                            nn = new Color32(128, 128, 255, 255);
                        }
                        float alpha = Mathf.Clamp01((blob - 0.35f) * 3f + (height - 0.5f) * 1.2f);
                        if (kind == DecalKind.Moss)
                        {
                            // keep the greenest texels
                            float green = (c.g - Mathf.Max(c.r, c.b)) / 255f;
                            alpha *= Mathf.Clamp01(green * 6f + 0.15f);
                        }
                        alpha = Mathf.SmoothStep(0f, 1f, alpha);
                        int o = (oy + y) * Atlas + ox + x;
                        albedo[o] = new Color32(c.r, c.g, c.b, (byte)(alpha * 255f));
                        normal[o] = nn;
                    }
            }
            Save(AlbedoPath(kind), albedo, false);
            Save(NormalPath(kind), normal, true);
            MakeMaterial(kind);
        }

        static (Color32[], int, int) Read(string dir, string suffix)
        {
            var file = Directory.Exists(dir) ? Directory.GetFiles(dir).FirstOrDefault(f => !f.EndsWith(".meta") && Path.GetFileNameWithoutExtension(f).Contains("_" + suffix + "_")) : null;
            if (file == null) return (null, 0, 0);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            tex.LoadImage(File.ReadAllBytes(file));
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            Object.DestroyImmediate(tex);
            return (px, w, h);
        }

        static void Save(string path, Color32[] pixels, bool linear)
        {
            var tex = new Texture2D(Atlas, Atlas, TextureFormat.RGBA32, true, linear) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            EditorUtility.CompressTexture(tex, TextureFormat.BC7, TextureCompressionQuality.Normal);
            var old = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (old == null) AssetDatabase.CreateAsset(tex, path);
            else
            {
                EditorUtility.CopySerialized(tex, old);
                Object.DestroyImmediate(tex);
            }
        }

        static void MakeMaterial(DecalKind kind)
        {
            string path = NatureMaterialBuilder.Dir + MaterialName(kind) + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("HotPatata/Nature"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath(kind)));
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath(kind)));
            mat.SetFloat("_TileSize", 1f);
            mat.SetFloat("_Triplanar", 0f);
            mat.DisableKeyword("_MAPPING_TRIPLANAR");
            mat.EnableKeyword("_DECAL");
            mat.SetFloat("_Decal", 1f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_OffsetFactor", -1f);
            mat.SetFloat("_OffsetUnits", -2f);
            mat.SetFloat("_MacroStrength", 0f);
            mat.SetFloat("_VariationStrength", 0.08f);
            mat.SetFloat("_EnvReflection", kind == DecalKind.Wet ? 1.4f : 0.5f);
            mat.SetFloat("_SmoothnessMax", kind == DecalKind.Wet ? 0.92f : kind == DecalKind.Mud ? 0.6f : 0.35f);
            mat.SetFloat("_SmoothnessMin", kind == DecalKind.Wet ? 0.8f : 0.05f);
            mat.SetColor("_BaseColor", kind == DecalKind.Wet ? new Color(0.35f, 0.33f, 0.3f, 0.75f) : Color.white);
            mat.renderQueue = (int)RenderQueue.AlphaTest + 60;
            mat.SetShaderPassEnabled("ShadowCaster", false);
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.SetShaderPassEnabled("DepthNormals", false);
            EditorUtility.SetDirty(mat);
        }

        // ------------------------------------------------------------------ the patch

        /// <summary>
        /// A decal of <paramref name="size"/> metres at <paramref name="center"/>, laid along <paramref name="normal"/>: a grid cast onto the
        /// level's surfaces (Environment) under it, a vertex that finds nothing fading out. Returns the drawn object, or null.
        /// </summary>
        public static GameObject Lay(Transform parent, string name, Vector3 center, Vector3 normal, float size, DecalKind kind, int variant, float yaw, Scene scene)
        {
            normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            var turn = Quaternion.AngleAxis(yaw, normal) * Quaternion.FromToRotation(Vector3.up, normal);
            Vector3 tangent = turn * Vector3.right, bitangent = turn * Vector3.forward;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var norms = new List<Vector3>();
            float cu = (variant % 2) * 0.5f, cv = (variant / 2) * 0.5f;
            int env = LayerMask.GetMask("Environment");
            for (int j = 0; j <= Grid; j++)
                for (int i = 0; i <= Grid; i++)
                {
                    float u = i / (float)Grid, v = j / (float)Grid;
                    var p = center + tangent * ((u - 0.5f) * size) + bitangent * ((v - 0.5f) * size);
                    var from = p + normal * 1.5f;
                    bool hit = false;
                    RaycastHit best = default;
                    float d = float.MaxValue;
                    foreach (var h in Physics.RaycastAll(from, -normal, 3f, env, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider.gameObject.scene != scene || h.distance >= d) continue;
                        d = h.distance;
                        best = h;
                        hit = true;
                    }
                    verts.Add(hit ? best.point + best.normal * 0.02f : p);
                    norms.Add(hit ? best.normal : normal);
                    colors.Add(new Color(1f, 1f, 1f, hit ? 1f : 0f));
                    uvs.Add(new Vector2(cu + u * 0.5f, cv + v * 0.5f));
                }
            var tris = new List<int>();
            for (int j = 0; j < Grid; j++)
                for (int i = 0; i < Grid; i++)
                {
                    int a = j * (Grid + 1) + i, b = a + 1, c = a + Grid + 1, e = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, e });
                }
            if (colors.All(c => c.a == 0f)) return null;
            var mesh = new Mesh { name = name + " decal" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<MeshFilter>().sharedMesh = OrganicAssets.Save(mesh);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Material(kind);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            return go;
        }

        /// <summary>A hand-placed decal piece: laid under the piece, along its down.</summary>
        public static void BuildDecal(DecalShape decal, Transform root, string name)
        {
            Physics.SyncTransforms();
            var t = decal.transform;
            Lay(root, name, t.position, t.up, decal.Size, decal.Kind, decal.Variant, 0f, decal.gameObject.scene);
        }

        /// <summary>Patches round a cliff's foot (<paramref name="ring"/>, its outline): moss, mud, leaves and wet ground on the ground there.</summary>
        public static void AroundFoot(Scene scene, Transform root, string name, List<Vector2> ring, float amount, Random rng)
        {
            float perimeter = 0f;
            for (int i = 0; i < ring.Count; i++) perimeter += Vector2.Distance(ring[i], ring[(i + 1) % ring.Count]);
            int count = Mathf.RoundToInt(amount * perimeter / 5f);
            var group = new GameObject("Decals");
            group.transform.SetParent(root, false);
            var keep = PalettePlacer.Keepout.Of(scene);
            for (int n = 0; n < count; n++)
            {
                int i = rng.Next(ring.Count);
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                var outward = Poly2.Outward(b - a).normalized;
                var q = Vector2.Lerp(a, b, (float)rng.NextDouble()) + outward * (0.4f + (float)rng.NextDouble() * 2.6f);
                float ground = LevelNature.GroundAt(scene, q);
                if (float.IsNaN(ground)) continue;
                var at = new Vector3(q.x, ground, q.y);
                if (!keep.Allows(at)) continue;
                double roll = rng.NextDouble();
                var kind = roll < 0.35 ? DecalKind.Moss : roll < 0.6 ? DecalKind.Mud : roll < 0.9 ? DecalKind.Leaves : DecalKind.Wet;
                Lay(group.transform, $"{name} {kind} {n}", at, Vector3.up, 1.2f + (float)rng.NextDouble() * 2.4f, kind, rng.Next(4), (float)rng.NextDouble() * 360f, scene);
            }
        }

        // ------------------------------------------------------------------ the palette

        static GameObject Place(PlaceContext c, DecalKind kind, string label)
        {
            var at = c.Ground(0f);
            var piece = (DecalShape)PaletteCatalog.NewPiece(c, typeof(DecalShape), label, at + c.Normal * 0.01f, LevelGroup.Props);
            Undo.RecordObject(piece, label);
            piece.Kind = kind;
            piece.Variant = c.Seed & 3;
            piece.transform.rotation = Quaternion.FromToRotation(Vector3.up, c.Normal) * Quaternion.Euler(0f, c.Seed % 360, 0f);
            LevelPieceBuilder.Rebuild(piece);
            return piece.gameObject;
        }

        [PaletteItem("ground.decal-moss", "Ground/Decals", "Moss Patch", Piece = typeof(DecalShape), Kind = PaletteKind.Prop, Snap = PaletteSnap.Ground | PaletteSnap.Floor | PaletteSnap.Normal,
                     Icon = "d_TerrainInspector.TerrainToolSplat", Tags = "decal moss patch green")]
        static GameObject Moss(PlaceContext c) => Place(c, DecalKind.Moss, "Moss patch");

        [PaletteItem("ground.decal-mud", "Ground/Decals", "Mud Patch", Piece = typeof(DecalShape), Kind = PaletteKind.Prop, Snap = PaletteSnap.Ground | PaletteSnap.Floor | PaletteSnap.Normal,
                     Icon = "d_TerrainInspector.TerrainToolSplat", Tags = "decal mud patch dirt")]
        static GameObject Mud(PlaceContext c) => Place(c, DecalKind.Mud, "Mud patch");

        [PaletteItem("ground.decal-leaves", "Ground/Decals", "Leaf Patch", Piece = typeof(DecalShape), Kind = PaletteKind.Prop, Snap = PaletteSnap.Ground | PaletteSnap.Floor | PaletteSnap.Normal,
                     Icon = "d_TerrainInspector.TerrainToolSplat", Tags = "decal leaves patch autumn")]
        static GameObject Leaves(PlaceContext c) => Place(c, DecalKind.Leaves, "Leaf patch");

        [PaletteItem("ground.decal-wet", "Ground/Decals", "Wet Patch", Piece = typeof(DecalShape), Kind = PaletteKind.Prop, Snap = PaletteSnap.Ground | PaletteSnap.Floor | PaletteSnap.Normal,
                     Icon = "d_TerrainInspector.TerrainToolSplat", Tags = "decal wet puddle damp")]
        static GameObject Wet(PlaceContext c) => Place(c, DecalKind.Wet, "Wet patch");
    }
}
