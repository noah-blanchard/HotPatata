using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// The generated vegetation of PatataWilds (ARCHITECTURE §25.3): Poly Haven's scanned trees are far too heavy for a forest at
    /// 60 fps (100 MB to 1 GB, millions of triangles), so trees are grown here from a seed, with Poly Haven's bark textures and
    /// the needle and leaf atlases of its saplings and shrubs: pines and firs (whorls of branches dressed in needle cards),
    /// broadleaf trees (limbs ending in leaf clusters), dead snags, bushes, plus grass and fern clumps and mossy rocks. Every
    /// tree has three levels of detail (at most 10k, 2.5k and 600 triangles), wind weights in its vertex colour (red trunk,
    /// green branch, blue leaf) and two submeshes (bark, leaves). Meshes are saved as assets, so a rebuild gives the same forest;
    /// hero trees also get prefabs with an LOD group. Menu <c>HotPatata/Nature/Build Vegetation</c>.
    /// </summary>
    public static class NatureTreeBuilder
    {
        public const string MeshDir = "Assets/Art/Models/Nature/Generated/";
        public const string PrefabDir = "Assets/Prefabs/Nature/";
        public const int Variants = 3;

        public enum Species { Pine, Fir, Broadleaf, Snag, Bush }

        public static readonly int[] LodBudget = { 10000, 2500, 600 };

        public static string MeshPath(string kind, int variant, int lod) => $"{MeshDir}{kind}_{variant}_LOD{lod}.asset";
        public static string PrefabPath(Species s, int variant) => $"{PrefabDir}Tree_{s}_{variant}.prefab";

        [MenuItem("HotPatata/Nature/Build Vegetation")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(MeshDir.TrimEnd('/'));
            Directory.CreateDirectory(PrefabDir.TrimEnd('/'));
            foreach (Species s in System.Enum.GetValues(typeof(Species)))
                for (int v = 0; v < Variants; v++)
                {
                    var lods = new Mesh[3];
                    for (int lod = 0; lod < 3; lod++) lods[lod] = Save(Grow(s, v, lod), MeshPath(s.ToString(), v, lod));
                    TreePrefab(s, v, lods);
                }
            for (int v = 0; v < Variants; v++)
            {
                Save(GrassClump(v), MeshPath("Grass", v, 0));
                Save(FernClump(v), MeshPath("Fern", v, 0));
                Save(Rock(v, 0), MeshPath("Rock", v, 0));
                Save(Rock(v, 1), MeshPath("Rock", v, 1));
            }
            BuildGiants();
            AssetDatabase.SaveAssets();
            Debug.Log("[NatureTreeBuilder] vegetation meshes and tree prefabs ready in " + MeshDir);
        }

        // ------------------------------------------------------------------ giants (PatataCanopy, M13.7)

        /// <summary>The canopy's giants: a broad oak and a redwood, 70-95 m tall, drawn instanced (no prefab).</summary>
        public enum Giant { Oak, Redwood }

        /// <summary>A giant comes in three meshes sharing one origin (the trunk's foot), so each part's box stays tight: the roots on the
        /// ground, the bare bole, the crown high above (its box starts where the limbs leave the trunk).</summary>
        public static readonly string[] GiantParts = { "Roots", "Bole", "Crown" };

        public static string GiantKind(Giant g, int part) => g + GiantParts[part];

        /// <summary>The deck trunks (the posts under the decks): this tall at scale 1, three girths, no crown (the deck is its top).</summary>
        public const float DeckTrunkLength = 50f;
        public static readonly float[] DeckTrunkRadius = { 0.8f, 1.2f, 1.6f };

        public static (string bark, string leaves) MaterialsFor(Giant g) =>
            g == Giant.Oak ? ("Nature_BarkBrown", "Nature_Leaves_Broad") : ("Nature_PineBark", "Nature_Leaves_Fir");

        /// <summary>The giants' and the deck trunks' meshes (three levels each; the deck trunks two).</summary>
        [MenuItem("HotPatata/Nature/Build Canopy Giants")]
        public static void BuildGiants()
        {
            Directory.CreateDirectory(MeshDir.TrimEnd('/'));
            foreach (Giant g in System.Enum.GetValues(typeof(Giant)))
                for (int v = 0; v < Variants; v++)
                    for (int lod = 0; lod < 3; lod++)
                    {
                        var parts = GrowGiant(g, v, lod);
                        for (int p = 0; p < parts.Length; p++)
                            if (p > 0 || lod < 2)   // the roots have two levels (hidden in the mist beyond)
                                Save(parts[p].ToMesh($"{GiantKind(g, p)}_{v}_LOD{lod}", p < 2), MeshPath(GiantKind(g, p), v, lod));
                    }
            for (int v = 0; v < DeckTrunkRadius.Length; v++)
                for (int lod = 0; lod < 2; lod++)
                    Save(DeckTrunk(v, lod).ToMesh($"DeckTrunk_{v}_LOD{lod}", true), MeshPath("DeckTrunk", v, lod));
            AssetDatabase.SaveAssets();
        }

        /// <summary>A giant's roots, bole and crown (the same seed, so the parts meet).</summary>
        static Builder[] GrowGiant(Giant g, int variant, int lod)
        {
            var rng = new Random(4000 + (int)g * 131 + variant * 17);
            bool oak = g == Giant.Oak;
            float height = oak ? R(rng, 80f, 88f) : R(rng, 86f, 95f);
            float crownStart = height * (oak ? R(rng, 0.68f, 0.72f) : R(rng, 0.6f, 0.64f));
            float baseRadius = oak ? R(rng, 2f, 2.4f) : R(rng, 2.2f, 2.7f);
            var roots = new Builder { Height = height };
            var bole = new Builder { Height = height };
            var crown = new Builder { Height = height };
            int sides = lod == 0 ? 16 : lod == 1 ? 9 : 5;

            // the bole: straight with a slow lean and burls, from the ground into the crown
            int segments = lod == 0 ? 18 : lod == 1 ? 9 : 5;
            float top = oak ? crownStart + 6f : height;
            var trunk = new List<Vector3>();
            var lean = new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)).normalized * R(rng, 0.004f, 0.012f);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments, y = t * top;
                trunk.Add(new Vector3(lean.x * y * t, y, lean.z * y * t));
            }
            var radii = new List<float>();
            float topRadius = oak ? baseRadius * 0.55f : 0.35f;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float r = Mathf.Lerp(baseRadius, topRadius, Mathf.Pow(t, oak ? 0.9f : 0.75f));
                r *= 1f + 0.07f * Mathf.Sin(t * 23f + variant) * (1f - t);           // burls
                r *= 1f + 0.55f * Mathf.Max(0f, 0.06f - t) / 0.06f;                 // the flare at the foot
                radii.Add(r);
            }
            bole.Tube(trunk, radii, sides, false);

            // buttress roots spreading on the ground
            int rootCount = lod == 2 ? 0 : oak ? 7 : 6;
            for (int k = 0; k < rootCount; k++)
            {
                float yaw = k * 360f / rootCount + R(rng, -14f, 14f);
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float reach = baseRadius + R(rng, 3.5f, 5.5f);
                var a = dir * baseRadius * 0.55f + Vector3.up * R(rng, 6f, 8f);
                var b = dir * (baseRadius + 1.2f) + Vector3.up * 2.2f;
                var c = dir * reach + Vector3.up * -0.6f;
                roots.Tube(new[] { a, b, c }, new[] { 0.95f, 0.75f, 0.25f }, lod == 0 ? 7 : 4, false);
            }

            var center = new Vector3(0f, (crownStart + height) * 0.5f, 0f);
            if (oak)
            {
                // heavy limbs from the top of the bole, out and up, each ending in clusters of leaves
                int limbs = lod == 2 ? 5 : rng.Next(6, 9);
                for (int l = 0; l < limbs; l++)
                {
                    float yaw = l * 360f / limbs + R(rng, -18f, 18f);
                    float rise = R(rng, 22f, 48f);
                    var dir = Quaternion.Euler(-rise, yaw, 0f) * Vector3.forward;
                    var start = TrunkPoint(trunk, R(rng, crownStart, top - 1f) / top);
                    float length = R(rng, 11f, 16f);
                    var end = start + dir * length;
                    var mid = Vector3.Lerp(start, end, 0.5f) + Vector3.up * R(rng, 0.5f, 1.8f);
                    crown.Tube(new[] { start, mid, end }, new[] { baseRadius * 0.42f, baseRadius * 0.28f, baseRadius * 0.1f }, lod == 0 ? 8 : lod == 1 ? 5 : 3, true);
                    var clusters = new List<Vector3> { end, Vector3.Lerp(mid, end, 0.4f) + Vector3.up * 1.5f };
                    if (lod < 2)
                        for (int s = 0; s < 2; s++)
                        {
                            // a secondary branch off the limb
                            var from = Vector3.Lerp(start, end, R(rng, 0.35f, 0.7f));
                            var sdir = Quaternion.Euler(-R(rng, 10f, 40f), yaw + R(rng, -60f, 60f), 0f) * Vector3.forward;
                            var tip = from + sdir * R(rng, 5f, 8f);
                            if (lod == 0) crown.Tube(new[] { from, Vector3.Lerp(from, tip, 0.5f) + Vector3.up * 0.4f, tip }, new[] { 0.35f, 0.22f, 0.08f }, 5, true);
                            clusters.Add(tip);
                        }
                    foreach (var c in clusters) LeafCluster(crown, rng, c, R(rng, 3.8f, 5.2f), center, lod, lod == 0 ? 30 : lod == 1 ? 12 : 4, lod == 2 ? 7f : lod == 1 ? 3.4f : 2.6f);
                }
                // a cap of leaves over the top, so the crown reads as one dome from below
                LeafCluster(crown, rng, new Vector3(0f, height - 3f, 0f), 6f, center, lod, lod == 0 ? 36 : lod == 1 ? 14 : 5, lod == 2 ? 9f : 3.2f);
            }
            else
            {
                // a redwood: short drooping branches in whorls up the top third, dressed in needle cards
                float step = lod == 0 ? 1.7f : lod == 1 ? 3.2f : 6f;
                for (float y = crownStart; y < height - 1.5f; y += step * R(rng, 0.85f, 1.15f))
                {
                    float u = (y - crownStart) / (height - crownStart);
                    float length = Mathf.Lerp(7.5f, 2f, u) * R(rng, 0.85f, 1.15f);
                    int count = lod == 2 ? 3 : 5;
                    float yawStart = R(rng, 0f, 360f);
                    for (int k = 0; k < count; k++)
                    {
                        float yaw = yawStart + k * 360f / count + R(rng, -14f, 14f);
                        var dir = Quaternion.Euler(R(rng, 4f, 20f), yaw, 0f) * Vector3.forward;   // drooping
                        var start = TrunkPoint(trunk, y / top);
                        var tip = start + dir * length;
                        if (lod == 0) crown.Tube(new[] { start, Vector3.Lerp(start, tip, 0.5f) + Vector3.up * 0.3f, tip }, new[] { 0.22f, 0.14f, 0.05f }, 4, true);
                        int cards = lod == 2 ? 1 : Mathf.Max(1, Mathf.CeilToInt(length / (lod == 0 ? 1.4f : 2.6f)));
                        for (int c = 0; c < cards; c++)
                        {
                            float t = (c + 0.4f) / cards;
                            var p = Vector3.Lerp(start, tip, t);
                            float w = (lod == 2 ? length * 1.4f : lod == 1 ? 3.6f : 2.8f) * (1f - t * 0.3f);
                            crown.CrossCard(p, (tip - start).normalized, lod == 2 ? length : 2.6f, w, center, R(rng, -30f, 30f), t);
                        }
                    }
                }
                crown.CrossCard(new Vector3(trunk[trunk.Count - 1].x, height - 2.5f, trunk[trunk.Count - 1].z), Vector3.up, 3.5f, 2.2f, center, 0f, 0.3f);
            }
            return new[] { roots, bole, crown };
        }

        /// <summary>A ball of leaf cards around <paramref name="c"/>, facing out of the crown.</summary>
        static void LeafCluster(Builder b, Random rng, Vector3 c, float radius, Vector3 crown, int lod, int cards, float size)
        {
            for (int k = 0; k < cards; k++)
            {
                var p = c + Random3(rng) * radius * 0.75f;
                var up = (p - c + Vector3.up * 0.8f).normalized;
                b.Card(p - up * size * 0.5f, up, Quaternion.AngleAxis(R(rng, 0f, 180f), up) * Vector3.Cross(up, Vector3.forward).normalized, size, size, crown, 1f, 0.85f);
            }
        }

        /// <summary>A deck trunk: a straight bark column with a flare and buttress roots at its foot (it never sways: it holds a deck).</summary>
        static Builder DeckTrunk(int variant, int lod)
        {
            var rng = new Random(6100 + variant * 7);
            var b = new Builder { Height = 1e5f };   // no sway weight
            float r0 = DeckTrunkRadius[variant], length = DeckTrunkLength;
            int segments = lod == 0 ? 12 : 5, sides = lod == 0 ? 12 : 6;
            var points = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                points.Add(new Vector3(Mathf.Sin(t * 3.1f + variant) * 0.15f, t * length, Mathf.Cos(t * 2.3f + variant) * 0.15f));
                float r = Mathf.Lerp(r0, r0 * 0.72f, t) * (1f + 0.05f * Mathf.Sin(t * 17f + variant));
                r *= 1f + 0.6f * Mathf.Max(0f, 0.08f - t) / 0.08f;
                radii.Add(r);
            }
            b.Tube(points, radii, sides, false);
            int rootCount = lod == 0 ? 5 : 3;
            for (int k = 0; k < rootCount; k++)
            {
                var dir = Quaternion.Euler(0f, k * 360f / rootCount + R(rng, -20f, 20f), 0f) * Vector3.forward;
                b.Tube(new[] { dir * r0 * 0.5f + Vector3.up * 3.5f, dir * (r0 + 0.8f) + Vector3.up * 1.2f, dir * (r0 + R(rng, 2f, 3f)) + Vector3.down * 0.5f },
                       new[] { r0 * 0.4f, r0 * 0.3f, r0 * 0.12f }, lod == 0 ? 6 : 4, false);
            }
            return b;
        }

        public static (string bark, string leaves) MaterialsFor(Species s) => s switch
        {
            Species.Pine => ("Nature_PineBark", "Nature_Leaves_Pine"),
            Species.Fir => ("Nature_BarkDark", "Nature_Leaves_Fir"),
            Species.Broadleaf => ("Nature_BarkBrown", "Nature_Leaves_Broad"),
            Species.Snag => ("Nature_RoughWood", "Nature_Leaves_Broad"),
            _ => ("Nature_BarkBrown", "Nature_Leaves_Broad"),
        };

        /// <summary>The materials of a tree mesh, one per submesh: bark then leaves, or leaves alone (bushes).</summary>
        public static Material[] MaterialsFor(Species s, Mesh mesh)
        {
            var (bark, leaves) = MaterialsFor(s);
            if (mesh.subMeshCount == 1) return new[] { NatureMaterialBuilder.Load(s == Species.Bush ? leaves : bark) };
            return new[] { NatureMaterialBuilder.Load(bark), NatureMaterialBuilder.Load(leaves) };
        }

        static Mesh Save(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void TreePrefab(Species s, int variant, Mesh[] lods)
        {
            var materials = MaterialsFor(s, lods[0]);
            var root = new GameObject($"Tree_{s}_{variant}");
            try
            {
                var group = root.AddComponent<LODGroup>();
                var levels = new LOD[lods.Length];
                float[] heights = { 0.3f, 0.09f, 0.012f };
                for (int i = 0; i < lods.Length; i++)
                {
                    var child = new GameObject("LOD" + i);
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = lods[i];
                    var r = child.AddComponent<MeshRenderer>();
                    r.sharedMaterials = materials;
                    r.shadowCastingMode = i < 2 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                    levels[i] = new LOD(heights[i], new Renderer[] { r });
                }
                group.SetLODs(levels);
                group.RecalculateBounds();
                root.AddComponent<CourseDecoration>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(s, variant));
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ------------------------------------------------------------------ mesh building

        sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int>[] Sub = { new List<int>(), new List<int>() };
            public float Height = 1f;

            public int Triangles => (Sub[0].Count + Sub[1].Count) / 3;

            /// <summary>A tapered tube through <paramref name="points"/> (radii per point), bark submesh; red = height, green = along a branch.</summary>
            public void Tube(IList<Vector3> points, IList<float> radii, int sides, bool branch)
            {
                int first = V.Count;
                float along = 0f;
                var firstDir = (points[1] - points[0]).normalized;
                // one reference for the whole tube, so the rings never turn against each other (no twist)
                var reference = Mathf.Abs(Vector3.Dot(firstDir, Vector3.right)) < 0.9f ? Vector3.right : Vector3.forward;
                for (int i = 0; i < points.Count; i++)
                {
                    var dir = (i < points.Count - 1 ? points[i + 1] - points[i] : points[i] - points[i - 1]).normalized;
                    var b1 = Vector3.ProjectOnPlane(reference, dir).normalized;
                    var b2 = Vector3.Cross(dir, b1);
                    if (i > 0) along += Vector3.Distance(points[i], points[i - 1]);
                    float around = Mathf.PI * 2f * Mathf.Max(radii[0], 0.05f);
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = k / (float)sides * Mathf.PI * 2f;
                        var radial = b1 * Mathf.Cos(a) + b2 * Mathf.Sin(a);
                        V.Add(points[i] + radial * radii[i]);
                        N.Add(radial);
                        UV.Add(new Vector2(around * k / sides, along));
                        float h = Mathf.Clamp01(points[i].y / Height);
                        C.Add(new Color(h * h, branch ? i / (float)(points.Count - 1) : 0f, 0f, 1f));
                    }
                }
                for (int i = 0; i < points.Count - 1; i++)
                    for (int k = 0; k < sides; k++)
                    {
                        int a = first + i * (sides + 1) + k, b = a + sides + 1;
                        Sub[0].Add(a); Sub[0].Add(a + 1); Sub[0].Add(b);
                        Sub[0].Add(a + 1); Sub[0].Add(b + 1); Sub[0].Add(b);
                    }
            }

            /// <summary>A leaf card: a quad from <paramref name="basePoint"/> along <paramref name="up"/>, <paramref name="width"/> across <paramref name="side"/>; normals bent out from <paramref name="crown"/>.</summary>
            public void Card(Vector3 basePoint, Vector3 up, Vector3 side, float length, float width, Vector3 crown, float uvRepeat = 1f, float branchWeight = 1f, Rect? uvRect = null)
            {
                int first = V.Count;
                var s = side.normalized * width * 0.5f;
                var u = up.normalized * length;
                Vector3[] corners = { basePoint - s, basePoint + s, basePoint + s + u, basePoint - s + u };
                var r = uvRect ?? new Rect(0f, 0f, uvRepeat, 1f);
                Vector2[] uvs = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
                for (int i = 0; i < 4; i++)
                {
                    V.Add(corners[i]);
                    var outward = corners[i] - crown;
                    N.Add((outward.normalized + Vector3.up * 0.35f).normalized);
                    UV.Add(uvs[i]);
                    float h = Mathf.Clamp01(corners[i].y / Height);
                    C.Add(new Color(h * h, branchWeight, i >= 2 ? 1f : 0.35f, 1f));
                }
                Sub[1].Add(first); Sub[1].Add(first + 1); Sub[1].Add(first + 2);
                Sub[1].Add(first); Sub[1].Add(first + 2); Sub[1].Add(first + 3);
            }

            /// <summary>Two crossed cards along <paramref name="up"/>.</summary>
            public void CrossCard(Vector3 basePoint, Vector3 up, float length, float width, Vector3 crown, float twist, float branchWeight = 1f)
            {
                var side = Vector3.Cross(up, Vector3.up);
                if (side.sqrMagnitude < 1e-3f) side = Vector3.right;
                side = Quaternion.AngleAxis(twist, up) * side.normalized;
                Card(basePoint, up, side, length, width, crown, 1f, branchWeight);
                Card(basePoint, up, Vector3.Cross(up, side), length, width, crown, 1f, branchWeight);
            }

            public Mesh ToMesh(string name, bool barkOnly = false)
            {
                var mesh = new Mesh { name = name, indexFormat = V.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.SetVertices(V);
                mesh.SetNormals(N);
                mesh.SetUVs(0, UV);
                mesh.SetColors(C);
                if (barkOnly)
                {
                    // a giant's roots or bole: bark alone, one submesh
                    mesh.subMeshCount = 1;
                    mesh.SetTriangles(Sub[0], 0);
                }
                else if (Sub[0].Count == 0)
                {
                    // ground cover and bushes: leaves only, one submesh (one material)
                    mesh.subMeshCount = 1;
                    mesh.SetTriangles(Sub[1], 0);
                }
                else
                {
                    mesh.subMeshCount = 2;
                    mesh.SetTriangles(Sub[0], 0);
                    mesh.SetTriangles(Sub[1], 1);
                }
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        static float R(Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ------------------------------------------------------------------ trees

        public static Mesh Grow(Species species, int variant, int lod)
        {
            var rng = new Random(1000 + (int)species * 97 + variant * 13);
            var b = new Builder();
            switch (species)
            {
                case Species.Pine:
                case Species.Fir: Conifer(b, rng, species == Species.Fir, lod); break;
                case Species.Broadleaf: Broadleaf(b, rng, lod); break;
                case Species.Snag: Snag(b, rng, lod); break;
                default: Bush(b, rng, lod); break;
            }
            return b.ToMesh($"{species}_{variant}_LOD{lod}");
        }

        static List<Vector3> Trunk(Random rng, float height, int segments, float lean)
        {
            var points = new List<Vector3>();
            var drift = new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)).normalized * lean;
            float phase = R(rng, 0f, 6.28f);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                points.Add(new Vector3(drift.x * t * t * height + Mathf.Sin(t * 5f + phase) * 0.04f, t * height, drift.z * t * t * height));
            }
            return points;
        }

        static List<float> Taper(int count, float baseRadius, float topRadius, float flare)
        {
            var radii = new List<float>();
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                radii.Add(Mathf.Lerp(baseRadius, topRadius, Mathf.Pow(t, 0.8f)) * (1f + flare * Mathf.Max(0f, 0.15f - t) / 0.15f));
            }
            return radii;
        }

        static void Conifer(Builder b, Random rng, bool fir, int lod)
        {
            float height = fir ? R(rng, 10f, 15f) : R(rng, 13f, 19f);
            b.Height = height;
            float baseRadius = fir ? R(rng, 0.22f, 0.32f) : R(rng, 0.25f, 0.38f);
            var trunk = Trunk(rng, height, lod == 0 ? 9 : 5, fir ? 0.02f : 0.05f);
            b.Tube(trunk, Taper(trunk.Count, baseRadius, 0.03f, 0.5f), lod == 0 ? 10 : lod == 1 ? 6 : 4, false);
            var crownCenter = new Vector3(0f, height * (fir ? 0.5f : 0.7f), 0f);
            float crownStart = height * (fir ? 0.12f : 0.42f);
            float maxLength = fir ? R(rng, 2.6f, 3.3f) : R(rng, 2.2f, 2.9f);
            if (lod == 2)
            {
                // a cone of crossed cards: the crown's silhouette in the distance
                for (int k = 0; k < 4; k++)
                {
                    float yaw = k * 45f + R(rng, -8f, 8f);
                    var side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                    float span = height - crownStart;
                    for (int tier = 0; tier < 3; tier++)
                    {
                        float y0 = crownStart + span * tier / 3f, y1 = crownStart + span * (tier + 1) / 3f;
                        float w = maxLength * 2f * (1f - tier / 3f) * (fir ? 1f : 0.8f);
                        b.Card(new Vector3(0f, y0, 0f), Vector3.up, side, y1 - y0 + 0.6f, w, crownCenter, Mathf.Max(1f, w / 1.5f), 0.5f);
                    }
                }
                return;
            }
            float step = fir ? 0.45f : 0.5f;
            if (lod == 1) step *= 1.8f;
            int whorl = 0;
            for (float y = crownStart; y < height - 0.4f; y += step * R(rng, 0.85f, 1.15f), whorl++)
            {
                float u = (y - crownStart) / Mathf.Max(0.1f, height - crownStart);
                float length = maxLength * Mathf.Pow(1f - u, fir ? 0.85f : 0.7f) + 0.35f;
                int count = fir ? 6 : 6;
                float yawStart = R(rng, 0f, 360f);
                float droop = fir ? R(rng, -22f, -10f) : Mathf.Lerp(-8f, 12f, u);
                for (int k = 0; k < count; k++)
                {
                    float yaw = yawStart + k * 360f / count + R(rng, -12f, 12f);
                    var dir = Quaternion.Euler(-droop, yaw, 0f) * Vector3.forward;
                    var start = TrunkPoint(trunk, y / height);
                    var tip = start + dir * length + Vector3.down * length * 0.12f;
                    var mid = Vector3.Lerp(start, tip, 0.5f) + Vector3.down * length * 0.04f;
                    if (lod == 0) b.Tube(new[] { start, mid, tip }, new[] { 0.05f, 0.035f, 0.015f }, 4, true);
                    float cardLength = lod == 0 ? (fir ? 1.3f : 1.5f) : 2f;
                    int cards = Mathf.Max(1, Mathf.CeilToInt(length / (lod == 0 ? 0.55f : 1.1f)));
                    for (int c = 0; c < cards; c++)
                    {
                        float t = (c + 0.3f) / cards;
                        var p = Vector3.Lerp(start, tip, t);
                        float w = (lod == 0 ? (fir ? 1.3f : 1.5f) : 1.8f) * (1f - t * 0.3f);
                        b.CrossCard(p, (tip - start).normalized, cardLength * (1f - t * 0.3f), w, crownCenter, R(rng, -30f, 30f), t);
                    }
                }
            }
            // a tuft at the top
            var top = trunk[trunk.Count - 1];
            b.CrossCard(top + Vector3.down * 0.9f, Vector3.up, 1.4f, 0.8f, crownCenter, 0f, 0.3f);
        }

        static Vector3 TrunkPoint(List<Vector3> trunk, float t)
        {
            float f = Mathf.Clamp01(t) * (trunk.Count - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), trunk.Count - 2);
            return Vector3.Lerp(trunk[i], trunk[i + 1], f - i);
        }

        static void Broadleaf(Builder b, Random rng, int lod)
        {
            float height = R(rng, 9f, 13f);
            b.Height = height;
            float fork = height * R(rng, 0.38f, 0.5f);
            var trunk = Trunk(rng, fork, lod == 0 ? 5 : 3, 0.08f);
            float baseRadius = R(rng, 0.28f, 0.4f);
            b.Tube(trunk, Taper(trunk.Count, baseRadius, baseRadius * 0.62f, 0.6f), lod == 0 ? 10 : lod == 1 ? 6 : 4, false);
            var crown = new Vector3(0f, height * 0.72f, 0f);
            int limbs = rng.Next(3, 5);
            for (int l = 0; l < limbs; l++)
            {
                float yaw = l * 360f / limbs + R(rng, -20f, 20f);
                var dir = Quaternion.Euler(-R(rng, 35f, 55f), yaw, 0f) * Vector3.forward;
                var start = trunk[trunk.Count - 1];
                var end = start + dir * R(rng, 3.2f, 4.6f);
                var mid = Vector3.Lerp(start, end, 0.5f) + Vector3.up * 0.4f;
                var limb = new[] { start, mid, end };
                b.Tube(limb, new[] { baseRadius * 0.55f, baseRadius * 0.35f, baseRadius * 0.15f }, lod == 0 ? 7 : lod == 1 ? 5 : 3, true);
                int clusters = lod == 0 ? 3 : lod == 1 ? 2 : 1;
                for (int c = 0; c < clusters; c++)
                {
                    var center = c == 0 ? end : Vector3.Lerp(mid, end, R(rng, 0.2f, 0.7f)) + Random3(rng) * 0.8f;
                    float radius = R(rng, 1.4f, 2.1f);
                    int cards = lod == 0 ? 14 : lod == 1 ? 6 : 3;
                    for (int k = 0; k < cards; k++)
                    {
                        var p = center + Random3(rng) * radius * 0.7f;
                        var up = (p - center + Vector3.up * 0.6f).normalized;
                        float size = lod == 2 ? radius * 1.6f : lod == 1 ? 1.9f : 1.35f;
                        b.Card(p - up * size * 0.5f, up, Quaternion.AngleAxis(R(rng, 0f, 180f), up) * Vector3.Cross(up, Vector3.forward).normalized, size, size, crown, 1f, 0.8f);
                    }
                }
            }
        }

        static Vector3 Random3(Random rng)
        {
            var v = new Vector3(R(rng, -1f, 1f), R(rng, -1f, 1f), R(rng, -1f, 1f));
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        static void Snag(Builder b, Random rng, int lod)
        {
            float height = R(rng, 6f, 10f);
            b.Height = height;
            var trunk = Trunk(rng, height, lod == 0 ? 7 : 4, 0.07f);
            b.Tube(trunk, Taper(trunk.Count, R(rng, 0.24f, 0.34f), 0.05f, 0.4f), lod == 0 ? 9 : lod == 1 ? 6 : 4, false);
            if (lod == 2) return;
            int branches = lod == 0 ? rng.Next(5, 8) : 3;
            for (int i = 0; i < branches; i++)
            {
                float t = R(rng, 0.35f, 0.85f);
                var start = TrunkPoint(trunk, t);
                var dir = Quaternion.Euler(-R(rng, 10f, 50f), R(rng, 0f, 360f), 0f) * Vector3.forward;
                float length = R(rng, 1f, 2.6f) * (1.1f - t);
                var tip = start + dir * length;
                b.Tube(new[] { start, Vector3.Lerp(start, tip, 0.5f) + Random3(rng) * 0.15f, tip }, new[] { 0.06f, 0.04f, 0.012f }, lod == 0 ? 5 : 3, true);
            }
        }

        static void Bush(Builder b, Random rng, int lod)
        {
            b.Height = 1.8f;
            float radius = R(rng, 0.9f, 1.4f);
            var center = new Vector3(0f, radius * 0.55f, 0f);
            int cards = lod == 0 ? 30 : lod == 1 ? 12 : 5;
            for (int k = 0; k < cards; k++)
            {
                var dir = Random3(rng);
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.1f;
                var p = center + dir.normalized * radius * R(rng, 0.2f, 0.75f) - Vector3.up * 0.3f;
                var up = (dir.normalized + Vector3.up * 0.8f).normalized;
                float size = lod == 0 ? R(rng, 0.9f, 1.3f) : lod == 1 ? 1.5f : radius * 1.8f;
                b.Card(new Vector3(p.x, Mathf.Max(0f, p.y - size * 0.4f), p.z), up, Quaternion.AngleAxis(R(rng, 0f, 180f), up) * Vector3.Cross(up, Vector3.forward).normalized,
                       size, size, center, 1f, 0.6f);
            }
        }

        // ------------------------------------------------------------------ ground cover and rocks

        /// <summary>A clump of grass: crossed blades from the grass atlas, the tips swaying (blue = 1 at the top).</summary>
        public static Mesh GrassClump(int variant)
        {
            var rng = new Random(500 + variant);
            var b = new Builder { Height = 0.8f };
            int blades = 5 + variant;
            for (int i = 0; i < blades; i++)
            {
                var p = new Vector3(R(rng, -0.35f, 0.35f), 0f, R(rng, -0.35f, 0.35f));
                float h = R(rng, 0.45f, 0.8f);
                var up = (Vector3.up + new Vector3(R(rng, -0.25f, 0.25f), 0f, R(rng, -0.25f, 0.25f))).normalized;
                var side = Quaternion.Euler(0f, R(rng, 0f, 180f), 0f) * Vector3.right;
                b.Card(p, up, side, h, R(rng, 0.45f, 0.7f), p + Vector3.down * 0.5f, 1f, 1f);
            }
            return b.ToMesh($"Grass_{variant}_LOD0");
        }

        /// <summary>The centres (U) of the four fronds laid side by side in Poly Haven's fern_02 atlas, base at the bottom.</summary>
        static readonly float[] FernFronds = { 0.165f, 0.385f, 0.575f, 0.755f };

        /// <summary>A fern: fronds from the fern atlas (one frond per card) leaning out from the centre.</summary>
        public static Mesh FernClump(int variant)
        {
            var rng = new Random(700 + variant);
            var b = new Builder { Height = 0.9f };
            int fronds = 6 + variant;
            for (int i = 0; i < fronds; i++)
            {
                float yaw = i * 360f / fronds + R(rng, -15f, 15f);
                var outDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var up = (outDir * R(rng, 0.8f, 1.3f) + Vector3.up).normalized;
                var side = Vector3.Cross(up, Vector3.up).normalized;
                float strip = FernFronds[(i + variant) % FernFronds.Length];
                b.Card(Vector3.zero, up, side, R(rng, 0.7f, 1f), R(rng, 0.3f, 0.4f), Vector3.down * 0.3f, 1f, 1f, new Rect(strip - 0.075f, 0.03f, 0.15f, 0.94f));
            }
            return b.ToMesh($"Fern_{variant}_LOD0");
        }

        /// <summary>A rock: a sphere pushed in and out by noise, flattened underneath (triplanar material, no UVs needed).</summary>
        public static Mesh Rock(int variant, int lod)
        {
            int rings = lod == 0 ? 10 : 6, segments = lod == 0 ? 16 : 9;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int seed = 900 + variant * 31;
            var stretch = new Vector3(1f + variant * 0.15f, 0.62f + variant * 0.06f, 0.85f);
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings, theta = v * Mathf.PI;
                for (int s = 0; s <= segments; s++)
                {
                    float u = s / (float)segments, phi = u * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    float n = NatureShapes.Noise(d * 1.6f + Vector3.one * seed, seed) * 0.35f + NatureShapes.Noise(d * 4f, seed + 3) * 0.12f;
                    var p = Vector3.Scale(d * (0.8f + n), stretch);
                    if (p.y < -0.15f) p.y = -0.15f - (p.y + 0.15f) * 0.15f;   // a flat underside that sits in the ground
                    verts.Add(p + Vector3.up * 0.12f);
                    uvs.Add(new Vector2(u, v));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s, b = a + segments + 1;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b);
                    tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                }
            var mesh = new Mesh { name = $"Rock_{variant}_LOD{lod}" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
