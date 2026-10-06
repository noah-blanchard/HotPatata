using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// The land around PatataWilds and its vegetation (ARCHITECTURE §25.3), built after the sections:
    /// <list type="bullet">
    /// <item>a terrain of 6 m cells over the whole course and 150 m beyond, its vertices at the top of the nearest cliffs and
    /// rising into hills away from them; a vertex that falls inside a section is pulled onto its cliff line, and no triangle is
    /// kept over a section. Collider-free, saved as mesh assets (the scene stays small);</item>
    /// <item>a <see cref="FoliageSet"/> drawn by a <see cref="FoliageInstancer"/>: forests, bushes and rocks on the terrain (the
    /// act sets the species), grass and ferns on the walkable floors, kept off triggers, movers, checkpoints and the pass arcs;</item>
    /// <item>a few scanned Poly Haven boulders, cliff faces and dead trunks on the terrain near the cliffs.</item>
    /// </list>
    /// Deterministic (fixed seeds). Visual only: nothing here collides.
    /// </summary>
    public static class NatureDressing
    {
        public const string GeneratedDir = "Assets/Art/Nature/Generated/";
        public const string FoliagePath = GeneratedDir + "PatataWilds_Foliage.asset";
        const float Cell = 6f, Margin = 150f, Half = 12f, Wall = 13.2f;

        sealed class Room
        {
            public Vector3 origin;
            public Quaternion rotation, inverse;
            public float length, top, floor;
            public int act;

            public Vector3 Local(Vector3 world) => inverse * (world - origin);

            /// <summary>Signed distance in XZ to the footprint (negative inside), and the nearest point on its outline.</summary>
            public float Distance(Vector3 world, out Vector3 nearest)
            {
                var l = Local(world);
                float x0 = -Wall, x1 = Wall, z0 = -Half - 0.2f, z1 = length + Half + 0.2f;
                float dx = Mathf.Max(x0 - l.x, l.x - x1), dz = Mathf.Max(z0 - l.z, l.z - z1);
                float d = dx > 0 || dz > 0 ? new Vector2(Mathf.Max(dx, 0), Mathf.Max(dz, 0)).magnitude : Mathf.Max(dx, dz);
                // nearest outline point
                var c = new Vector3(Mathf.Clamp(l.x, x0, x1), 0, Mathf.Clamp(l.z, z0, z1));
                if (d < 0)
                {
                    float toX0 = l.x - x0, toX1 = x1 - l.x, toZ0 = l.z - z0, toZ1 = z1 - l.z;
                    float m = Mathf.Min(Mathf.Min(toX0, toX1), Mathf.Min(toZ0, toZ1));
                    c = l;
                    if (m == toX0) c.x = x0; else if (m == toX1) c.x = x1; else if (m == toZ0) c.z = z0; else c.z = z1;
                }
                nearest = origin + rotation * c;
                return d;
            }
        }

        public static string Dress(Transform section, string groupName, IReadOnlyList<PatataWildsBuilder.Footprint> footprints)
        {
            var group = section.Find(groupName);
            var rooms = footprints.Select(f => new Room
            {
                origin = f.origin,
                rotation = Quaternion.Euler(0, f.yaw, 0),
                inverse = Quaternion.Inverse(Quaternion.Euler(0, f.yaw, 0)),
                length = f.length,
                top = f.topY,
                floor = f.floorY,
                act = f.act
            }).ToList();
            var root = new GameObject("Dressing").transform;
            root.SetParent(group, false);
            Directory.CreateDirectory(GeneratedDir.TrimEnd('/'));
            int skirts = Skirts(root, group);
            var terrain = Terrain(root, rooms, out var heights);
            var foliage = Scatter(root, rooms, heights);
            int props = HeroProps(root, rooms, heights);
            AssetDatabase.SaveAssets();
            return $"{skirts} rock skirts, terrain {terrain} chunks, {foliage} plants and rocks, {props} scanned props";
        }

        // ------------------------------------------------------------------ terrain

        sealed class Heightfield
        {
            public float x0, z0;
            public int nx, nz;
            public Vector3[,] points;       // world positions (snapped onto cliff lines where inside a section)
            public bool[,] inside;
            public int[,] act;

            /// <summary>The ground height at a world XZ, by bilinear interpolation; false off the field or inside a section.</summary>
            public bool Sample(float x, float z, out float y)
            {
                y = 0f;
                float fx = (x - x0) / Cell, fz = (z - z0) / Cell;
                int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz);
                if (i < 0 || j < 0 || i >= nx - 1 || j >= nz - 1) return false;
                if (inside[i, j] || inside[i + 1, j] || inside[i, j + 1] || inside[i + 1, j + 1]) return false;
                float u = fx - i, v = fz - j;
                y = Mathf.Lerp(Mathf.Lerp(points[i, j].y, points[i + 1, j].y, u), Mathf.Lerp(points[i, j + 1].y, points[i + 1, j + 1].y, u), v);
                return true;
            }
        }

        static float Noise(float x, float z, int seed) =>
            NatureShapes.Noise(new Vector3(x * 0.012f, 0.5f, z * 0.012f), seed) * 0.6f + NatureShapes.Noise(new Vector3(x * 0.045f, 1.5f, z * 0.045f), seed + 5) * 0.4f;

        static int Terrain(Transform root, List<Room> rooms, out Heightfield heightfield)
        {
            var field = heightfield = new Heightfield();
            var corners = new List<Vector3>();
            foreach (var r in rooms)
                foreach (var c in new[] { new Vector3(-Wall, 0, -Half), new Vector3(Wall, 0, -Half), new Vector3(-Wall, 0, r.length + Half), new Vector3(Wall, 0, r.length + Half) })
                    corners.Add(r.origin + r.rotation * c);
            float minX = corners.Min(c => c.x) - Margin, maxX = corners.Max(c => c.x) + Margin;
            float minZ = corners.Min(c => c.z) - Margin, maxZ = corners.Max(c => c.z) + Margin;
            field.x0 = minX;
            field.z0 = minZ;
            field.nx = Mathf.CeilToInt((maxX - minX) / Cell) + 1;
            field.nz = Mathf.CeilToInt((maxZ - minZ) / Cell) + 1;
            field.points = new Vector3[field.nx, field.nz];
            field.inside = new bool[field.nx, field.nz];
            field.act = new int[field.nx, field.nz];
            for (int i = 0; i < field.nx; i++)
                for (int j = 0; j < field.nz; j++)
                {
                    var p = new Vector3(minX + i * Cell, 0, minZ + j * Cell);
                    float wsum = 0f, hsum = 0f, best = float.MaxValue;
                    Room nearestRoom = null;
                    Vector3 nearestPoint = p;
                    foreach (var r in rooms)
                    {
                        float d = r.Distance(p, out var outline);
                        if (d < best) { best = d; nearestRoom = r; nearestPoint = outline; }
                    }
                    if (best < 0f)
                    {
                        // inside a section: onto its cliff line, at the cliff's top
                        field.inside[i, j] = true;
                        field.points[i, j] = new Vector3(nearestPoint.x, nearestRoom.top - 0.25f, nearestPoint.z);
                        field.act[i, j] = nearestRoom.act;
                        continue;
                    }
                    foreach (var r in rooms)
                    {
                        float d = Mathf.Max(0f, r.Distance(p, out _));
                        if (d > 120f) continue;
                        float w = 1f / ((d + 3f) * (d + 3f) * (d + 3f));
                        float rise = 16f * (1f - Mathf.Exp(-d / 40f));
                        wsum += w;
                        hsum += w * (r.top - 0.25f + rise);
                    }
                    float h = wsum > 0f ? hsum / wsum : nearestRoom.top + 16f;
                    h += (Noise(p.x, p.z, 41) - 0.5f) * 14f * Mathf.Clamp01(best / 30f);
                    field.points[i, j] = new Vector3(p.x, h, p.z);
                    field.act[i, j] = nearestRoom.act;
                }
            // chunks of 16 x 16 cells, a mesh asset each
            const int chunk = 16;
            int count = 0;
            var terrainRoot = new GameObject("Terrain").transform;
            terrainRoot.SetParent(root, false);
            foreach (var old in Directory.GetFiles(GeneratedDir, "PatataWilds_Terrain_*.asset")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
            for (int ci = 0; ci < field.nx - 1; ci += chunk)
                for (int cj = 0; cj < field.nz - 1; cj += chunk)
                {
                    var verts = new List<Vector3>();
                    var tris = new List<int>();
                    var acts = new int[6];
                    var index = new Dictionary<(int, int), int>();
                    int V(int i, int j)
                    {
                        if (!index.TryGetValue((i, j), out int k)) { k = verts.Count; verts.Add(field.points[i, j]); index[(i, j)] = k; }
                        return k;
                    }
                    for (int i = ci; i < Mathf.Min(ci + chunk, field.nx - 1); i++)
                        for (int j = cj; j < Mathf.Min(cj + chunk, field.nz - 1); j++)
                            foreach (var t in new[] { (i, j, i, j + 1, i + 1, j + 1), (i, j, i + 1, j + 1, i + 1, j) })
                            {
                                var a = field.points[t.Item1, t.Item2];
                                var b = field.points[t.Item3, t.Item4];
                                var c = field.points[t.Item5, t.Item6];
                                if (field.inside[t.Item1, t.Item2] && field.inside[t.Item3, t.Item4] && field.inside[t.Item5, t.Item6]) continue;
                                var centroid = (a + b + c) / 3f;
                                if (rooms.Any(r => r.Distance(centroid, out _) < -0.1f)) continue;   // never a roof over a section
                                if (Vector3.Cross(b - a, c - a).y <= 0.0001f) continue;             // folded by the snapping
                                tris.Add(V(t.Item1, t.Item2)); tris.Add(V(t.Item3, t.Item4)); tris.Add(V(t.Item5, t.Item6));
                                acts[Mathf.Clamp(field.act[t.Item1, t.Item2], 0, 5)]++;
                            }
                    if (tris.Count == 0) continue;
                    var center = verts.Aggregate(Vector3.zero, (s, v) => s + v) / verts.Count;
                    var local = verts.Select(v => v - center).ToList();
                    var mesh = new Mesh { name = $"PatataWilds_Terrain_{ci}_{cj}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.SetVertices(local);
                    mesh.SetTriangles(tris, 0);
                    mesh.RecalculateNormals();
                    mesh.RecalculateTangents();
                    mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh, $"{GeneratedDir}{mesh.name}.asset");
                    var go = new GameObject(mesh.name);
                    go.transform.SetParent(terrainRoot, false);
                    go.transform.position = center;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    int act = System.Array.IndexOf(acts, acts.Max());
                    r.sharedMaterial = NatureMaterialBuilder.Load(act <= 2 ? "Nature_ForestFloor" : act <= 4 ? "Nature_RocksGround" : "Nature_Grass");
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    go.AddComponent<NatureTerrain>();
                    count++;
                }
            return count;
        }

        // ------------------------------------------------------------------ rock skirts

        /// <summary>
        /// Under every raised rock slab (a ledge over a gorge, a floor over water, a ramp), a collider-free rock mass that widens
        /// with depth down to what lies below (capped at 14 m): platforms read as outcrops, never as floating boxes. Kept clear of
        /// the pass arcs.
        /// </summary>
        static int Skirts(Transform root, Transform group)
        {
            var parent = new GameObject("Rock skirts").transform;
            parent.SetParent(root, false);
            var samples = PassSamples();
            int count = 0;
            foreach (var skin in group.GetComponentsInChildren<KitSkin>())
            {
                if (skin.Shape != KitShape.RoughBox) continue;
                var box = skin.transform.parent != null ? skin.transform.parent.GetComponent<BoxCollider>() : null;
                if (box == null || box.isTrigger || !IndustrialKit.IsStatic(box) || box.gameObject.layer != LayerMask.NameToLayer("Environment")) continue;
                var t = box.transform;
                if (Vector3.Dot(t.up, Vector3.up) < 0.996f) continue;   // level floors only (not cliffs; a tilted ramp's mass would rise over its path)
                var size = Vector3.Scale(box.size, new Vector3(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y), Mathf.Abs(t.lossyScale.z)));
                if (size.y > 2.5f || size.x < 1.5f || size.z < 1.5f) continue;   // solid steps reach the ground already
                var bottom = t.TransformPoint(box.center - Vector3.up * box.size.y * 0.5f);
                float below = float.MaxValue;
                foreach (var offset in new[] { Vector3.zero, new Vector3(0.35f, 0, 0.35f), new Vector3(-0.35f, 0, 0.35f), new Vector3(0.35f, 0, -0.35f), new Vector3(-0.35f, 0, -0.35f) })
                {
                    var from = bottom + t.rotation * Vector3.Scale(offset, size) - Vector3.up * 0.02f;
                    float d = 30f;
                    foreach (var hit in Physics.RaycastAll(from, Vector3.down, 30f, LayerMask.GetMask("Environment", "Hazard"), QueryTriggerInteraction.Ignore))
                        if (hit.collider != box && hit.distance < d) d = hit.distance;
                    below = Mathf.Min(below, d);
                }
                if (below < 1.2f) continue;   // it lies on something
                float depth = Mathf.Clamp(below + 0.6f, 2f, 14f);
                var center = bottom - t.up * (depth / 2f - 0.05f);
                var reach = new Bounds(center, new Vector3(size.x + 3.4f, depth, size.z + 3.4f));
                reach.Expand(0.6f);
                if (samples.Any(p => reach.Contains(p))) continue;
                var go = new GameObject("Rock skirt");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(center, t.rotation);
                go.transform.localScale = new Vector3(size.x, depth, size.z);
                go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = skin.GetComponent<Renderer>().sharedMaterial;
                go.AddComponent<KitSkin>().Configure(CourseKit.Palette, KitShape.Skirt, KitColor.Neutral, Vector3.one, false);
                go.AddComponent<CourseDecoration>();
                go.isStatic = true;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Each mesh of a scanned Poly Haven model as its own variant: the mesh and the matrix that stands it on its base,
        /// centred, in metres (the model's own scale and turn applied); its height in metres.
        /// </summary>
        static List<(Mesh mesh, Matrix4x4 bake, float height)> Variants(string model)
        {
            var result = new List<(Mesh, Matrix4x4, float)>();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{NatureAssetImporter.ModelFolder}{model}/{model}_1k.fbx");
            if (asset == null) return result;
            foreach (var filter in asset.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var m = asset.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = mesh.bounds;
                var world = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
                for (int c = 0; c < 8; c++)
                    world.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
                var bake = Matrix4x4.Translate(new Vector3(-world.center.x, -world.min.y, -world.center.z)) * m;
                result.Add((mesh, bake, world.size.y));
            }
            return result;
        }

        /// <summary>Batches for every variant of a scanned model, sized so a scale of 1 is <paramref name="height"/> metres tall.</summary>
        static FoliageSet.Batch[] ModelBatches(FoliageSet set, string model, string material, float height, float[] distances, int[] meshLods, bool[] shadows,
                                              float minScale, float maxScale)
        {
            var mat = NatureMaterialBuilder.Load(material);
            var batches = new List<FoliageSet.Batch>();
            foreach (var (mesh, bake, h) in Variants(model))
            {
                float k = height / Mathf.Max(0.01f, h);
                var b = Batch(set, $"{model} {batches.Count}", Enumerable.Repeat(mesh, distances.Length).ToArray(), new[] { mat }, distances, shadows, minScale, maxScale);
                b.meshTransform = Matrix4x4.Scale(Vector3.one * k) * bake;
                b.meshLods = meshLods;
                b.shadowDistance = 45f;
                batches.Add(b);
            }
            return batches.ToArray();
        }

        // ------------------------------------------------------------------ scatter

        static FoliageSet.Batch Batch(FoliageSet set, string name, Mesh[] lods, Material[] materials, float[] distances, bool[] shadows, float minScale, float maxScale)
        {
            var b = new FoliageSet.Batch { name = name, lods = lods, materials = materials, lodDistances = distances, lodShadows = shadows, minScale = minScale, maxScale = maxScale };
            set.batches.Add(b);
            return b;
        }

        static Mesh M(string kind, int variant, int lod) => AssetDatabase.LoadAssetAtPath<Mesh>(NatureTreeBuilder.MeshPath(kind, variant, lod));

        // every intended pass arc, by 2 m cell: no plant or rock may come within the decoration clearance of one
        static Dictionary<(int, int, int), List<Vector3>> arcCells;

        static void IndexArcs(List<Vector3> samples)
        {
            arcCells = new Dictionary<(int, int, int), List<Vector3>>();
            foreach (var p in samples)
            {
                var key = (Mathf.FloorToInt(p.x / 2f), Mathf.FloorToInt(p.y / 2f), Mathf.FloorToInt(p.z / 2f));
                if (!arcCells.TryGetValue(key, out var list)) arcCells[key] = list = new List<Vector3>();
                list.Add(p);
            }
        }

        static bool TouchesAnArc(FoliageSet.Batch batch, Vector3 p, float yaw, float scale)
        {
            if (arcCells == null) return false;
            var local = batch.LocalBounds(0);
            var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
            var world = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int c = 0; c < 8; c++)
                world.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
            world.Expand(2f * (PassCorridor.DecorationClearance + 0.1f));
            for (int x = Mathf.FloorToInt(world.min.x / 2f); x <= Mathf.FloorToInt(world.max.x / 2f); x++)
                for (int y = Mathf.FloorToInt(world.min.y / 2f); y <= Mathf.FloorToInt(world.max.y / 2f); y++)
                    for (int z = Mathf.FloorToInt(world.min.z / 2f); z <= Mathf.FloorToInt(world.max.z / 2f); z++)
                        if (arcCells.TryGetValue((x, y, z), out var list) && list.Any(a => world.Contains(a))) return true;
            return false;
        }

        static void Add(Dictionary<FoliageSet.Batch, Dictionary<(int, int), List<(Vector3, float, float)>>> bins, FoliageSet.Batch batch, Vector3 p, float yaw, float scale)
        {
            if (TouchesAnArc(batch, p, yaw, scale)) return;
            if (!bins.TryGetValue(batch, out var cells)) bins[batch] = cells = new Dictionary<(int, int), List<(Vector3, float, float)>>();
            var key = (Mathf.FloorToInt(p.x / FoliageSet.CellSize), Mathf.FloorToInt(p.z / FoliageSet.CellSize));
            if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<(Vector3, float, float)>();
            list.Add((p, yaw, scale));
        }

        static int Scatter(Transform root, List<Room> rooms, Heightfield field)
        {
            var set = AssetDatabase.LoadAssetAtPath<FoliageSet>(FoliagePath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<FoliageSet>();
                AssetDatabase.CreateAsset(set, FoliagePath);
            }
            set.batches.Clear();
            var trees = new Dictionary<NatureTreeBuilder.Species, FoliageSet.Batch[]>();
            foreach (NatureTreeBuilder.Species s in System.Enum.GetValues(typeof(NatureTreeBuilder.Species)))
            {
                var batches = new FoliageSet.Batch[NatureTreeBuilder.Variants];
                for (int v = 0; v < NatureTreeBuilder.Variants; v++)
                {
                    var lods = new[] { M(s.ToString(), v, 0), M(s.ToString(), v, 1), M(s.ToString(), v, 2) };
                    bool bush = s == NatureTreeBuilder.Species.Bush;
                    batches[v] = Batch(set, $"{s} {v}", lods, NatureTreeBuilder.MaterialsFor(s, lods[0]),
                                       bush ? new[] { 30f, 70f, 140f } : new[] { 45f, 120f, 420f }, new[] { true, true, false }, 0.8f, 1.25f);
                    batches[v].shadowDistance = bush ? 30f : 60f;
                }
                trees[s] = batches;
            }
            var rocks = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                Batch(set, "Rock " + v, new[] { M("Rock", v, 0), M("Rock", v, 1) }, new[] { NatureMaterialBuilder.Load("Nature_MossyRock") }, new[] { 60f, 200f }, new[] { true, false }, 0.6f, 2.4f)).ToArray();
            var grass = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                Batch(set, "Grass " + v, new[] { M("Grass", v, 0) }, new[] { NatureMaterialBuilder.Load("Nature_GrassBlades") }, new[] { 32f }, new[] { false }, 0.8f, 1.3f)).ToArray();
            var ferns = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                Batch(set, "Fern " + v, new[] { M("Fern", v, 0) }, new[] { NatureMaterialBuilder.Load("Nature_Fern") }, new[] { 34f }, new[] { false }, 0.8f, 1.4f)).ToArray();
            // scanned Poly Haven models, drawn at their own reduced mesh LODs
            float[] closeRange = { 22f, 55f }, mid = { 40f, 110f, 260f }, far = { 60f, 160f, 420f };
            int[] lodsNear = { 2, 4 }, lodsMid = { 2, 4, 6 };
            bool[] shadowNear = { false, false }, shadowMid = { true, true, false };
            var bigRocks = ModelBatches(set, "namaqualand_boulder_02", "Nature_Prop_Boulder02", 1.6f, far, lodsMid, shadowMid, 0.7f, 2.2f)
                .Concat(ModelBatches(set, "namaqualand_boulder_04", "Nature_Prop_Boulder04", 2.2f, far, lodsMid, shadowMid, 0.6f, 2f))
                .Concat(ModelBatches(set, "boulder_01", "Nature_Prop_Boulder", 1.4f, far, lodsMid, shadowMid, 0.6f, 2f))
                .Concat(ModelBatches(set, "rock_moss_set_01", "Nature_Prop_RockMoss", 1.1f, far, lodsMid, shadowMid, 0.6f, 2f))
                .Concat(ModelBatches(set, "rock_moss_set_02", "Nature_Prop_RockMoss2", 1f, far, lodsMid, shadowMid, 0.6f, 2f)).ToArray();
            var cliffRocks = ModelBatches(set, "rock_face_02", "Nature_Prop_RockFace02", 4.5f, far, lodsMid, shadowMid, 0.7f, 1.8f)
                .Concat(ModelBatches(set, "rock_face_01", "Nature_Prop_RockFace", 4f, far, lodsMid, shadowMid, 0.7f, 1.8f)).ToArray();
            var pebbles = ModelBatches(set, "rock_07", "Nature_Prop_Rock07", 0.22f, closeRange, lodsNear, shadowNear, 0.7f, 1.6f)
                .Concat(ModelBatches(set, "rock_09", "Nature_Prop_Rock09", 0.16f, closeRange, lodsNear, shadowNear, 0.7f, 1.8f))
                .Concat(ModelBatches(set, "stone_01", "Nature_Prop_Stone01", 0.2f, closeRange, lodsNear, shadowNear, 0.7f, 1.6f)).ToArray();
            var footRocks = ModelBatches(set, "rock_07", "Nature_Prop_Rock07", 0.5f, mid, lodsMid, shadowMid, 0.7f, 1.6f)
                .Concat(ModelBatches(set, "stone_01", "Nature_Prop_Stone01", 0.45f, mid, lodsMid, shadowMid, 0.7f, 1.6f)).ToArray();
            var undergrowth = ModelBatches(set, "weed_plant_02", "Nature_Weed", 0.4f, closeRange, lodsNear, shadowNear, 0.7f, 1.4f)
                .Concat(ModelBatches(set, "nettle_plant", "Nature_Nettle", 0.55f, closeRange, lodsNear, shadowNear, 0.7f, 1.4f))
                .Concat(ModelBatches(set, "shrub_04", "Nature_Shrub04", 0.6f, closeRange, lodsNear, shadowNear, 0.7f, 1.4f)).ToArray();
            var moss = ModelBatches(set, "moss_01", "Nature_Moss", 0.08f, new[] { 26f }, new[] { -1 }, new[] { false }, 1.5f, 4f);
            var shrubs = ModelBatches(set, "shrub_02", "Nature_Shrub02", 1.6f, mid, lodsMid, shadowMid, 0.7f, 1.4f);
            var deadwood = ModelBatches(set, "dry_branches_medium_01", "Nature_Prop_Branches", 0.3f, closeRange, lodsNear, shadowNear, 0.8f, 1.6f);
            var logs = ModelBatches(set, "tree_stump_02", "Nature_Prop_Stump2", 0.7f, mid, lodsMid, shadowMid, 0.8f, 1.5f)
                .Concat(ModelBatches(set, "dead_tree_trunk", "Nature_Prop_DeadTrunk1", 0.5f, mid, lodsMid, shadowMid, 0.8f, 1.4f))
                .Concat(ModelBatches(set, "root_cluster_01", "Nature_Prop_Roots", 1.2f, mid, lodsMid, shadowMid, 0.7f, 1.3f)).ToArray();
            FoliageSet.Batch Pick(FoliageSet.Batch[] options, Random r) => options[r.Next(options.Length)];

            // the grass and ferns thin out before they stop, with no pop
            foreach (var mat in new[] { "Nature_GrassBlades", "Nature_Fern" })
            {
                var m = NatureMaterialBuilder.Load(mat);
                m.SetFloat("_FadeStart", 22f);
                m.SetFloat("_FadeEnd", 31f);
                EditorUtility.SetDirty(m);
            }

            var bins = new Dictionary<FoliageSet.Batch, Dictionary<(int, int), List<(Vector3, float, float)>>>();
            IndexArcs(PassSamples());
            var rng = new Random(2026);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // forests on the terrain
            for (int i = 0; i < field.nx - 1; i++)
                for (int j = 0; j < field.nz - 1; j++)
                {
                    float x = field.x0 + (i + R(0.1f, 0.9f)) * Cell, z = field.z0 + (j + R(0.1f, 0.9f)) * Cell;
                    if (!field.Sample(x, z, out float y)) continue;
                    var p = new Vector3(x, y, z);
                    float near = rooms.Min(r => r.Distance(p, out _));
                    if (near < 2.5f) continue;
                    int act = field.act[i, j];
                    double roll = rng.NextDouble();
                    float density = act == 5 ? 0.32f : act >= 3 ? 0.45f : 0.62f;
                    if (roll < density)
                    {
                        var species = act == 1 ? (rng.NextDouble() < 0.55 ? NatureTreeBuilder.Species.Fir : NatureTreeBuilder.Species.Broadleaf)
                                    : act == 2 ? (rng.NextDouble() < 0.6 ? NatureTreeBuilder.Species.Broadleaf : NatureTreeBuilder.Species.Pine)
                                    : act == 5 ? (rng.NextDouble() < 0.75 ? NatureTreeBuilder.Species.Pine : NatureTreeBuilder.Species.Snag)
                                    : (rng.NextDouble() < 0.7 ? NatureTreeBuilder.Species.Pine : NatureTreeBuilder.Species.Fir);
                        if (rng.NextDouble() < 0.05) species = NatureTreeBuilder.Species.Snag;
                        Add(bins, trees[species][rng.Next(NatureTreeBuilder.Variants)], p - Vector3.up * 0.2f, R(0, 360), R(0.8f, 1.25f));
                    }
                    else if (roll < density + 0.12f) Add(bins, trees[NatureTreeBuilder.Species.Bush][rng.Next(NatureTreeBuilder.Variants)], p, R(0, 360), R(0.8f, 1.25f));
                    else if (roll < density + 0.18f) Add(bins, rocks[rng.Next(rocks.Length)], p - Vector3.up * 0.3f, R(0, 360), R(0.8f, 2.4f));
                    else if (roll < density + 0.25f) Add(bins, Pick(bigRocks, rng), p - Vector3.up * 0.25f, R(0, 360), R(0.7f, 2f));
                    else if (roll < density + 0.30f) Add(bins, Pick(logs, rng), p - Vector3.up * 0.05f, R(0, 360), R(0.8f, 1.4f));
                    else if (roll < density + 0.36f) Add(bins, Pick(shrubs, rng), p, R(0, 360), R(0.7f, 1.3f));
                    // the cliff rim: big rock faces half sunk along the top of the cliffs
                    if (near < 8f && rng.NextDouble() < 0.35)
                    {
                        // never overhanging a section: the rock's reach stays behind the cliff line
                        var rimRock = Pick(cliffRocks, rng);
                        float rimScale = R(0.7f, 1.6f);
                        var lb = rimRock.LocalBounds(0);
                        float radius = new Vector2(lb.extents.x, lb.extents.z).magnitude * rimScale;
                        if (near > radius - 0.8f) Add(bins, rimRock, p - Vector3.up * R(0.8f, 2f), R(0, 360), rimScale);
                    }
                }

            // grass and ferns on the walkable floors
            var passSamples = PassSamples();
            foreach (var r in rooms)
            {
                float step = 1.6f;
                for (float lx = -Half + 0.8f; lx < Half; lx += step)
                    for (float lz = -Half; lz < r.length + Half; lz += step)
                    {
                        var start = r.origin + r.rotation * new Vector3(lx + R(-0.5f, 0.5f), 80f, lz + R(-0.5f, 0.5f));
                        if (!GroundHit(start, out var hit)) continue;
                        if (hit.collider.isTrigger || hit.collider.gameObject.layer != LayerMask.NameToLayer("Environment")) continue;
                        if (hit.normal.y < 0.97f || !IndustrialKit.IsStatic(hit.collider)) continue;
                        if (hit.collider.GetComponentInParent<CourseCeiling>() != null) continue;
                        if (r.Distance(hit.point, out _) > -0.5f) continue;   // only inside this section
                        if (NearGameplay(hit.point)) continue;
                        if (NearPass(passSamples, hit.point, 1.4f, 1f)) continue;
                        double roll = rng.NextDouble();
                        float grassChance = r.act == 3 ? 0.18f : r.act == 4 ? 0.22f : 0.36f;
                        float fernChance = r.act <= 1 ? 0.18f : r.act == 3 ? 0.1f : 0.05f;
                        if (roll < grassChance) Add(bins, grass[rng.Next(grass.Length)], hit.point, R(0, 360), R(0.8f, 1.3f));
                        else if (roll < grassChance + fernChance) Add(bins, ferns[rng.Next(ferns.Length)], hit.point, R(0, 360), R(0.8f, 1.4f));
                        else if (roll < grassChance + fernChance + 0.12f) Add(bins, Pick(pebbles, rng), hit.point - Vector3.up * 0.03f, R(0, 360), R(0.7f, 1.6f));
                        else if (roll < grassChance + fernChance + 0.22f) Add(bins, Pick(moss, rng), hit.point - Vector3.up * 0.01f, R(0, 360), R(1.5f, 4f));
                        else if (roll < grassChance + fernChance + 0.28f) Add(bins, Pick(undergrowth, rng), hit.point, R(0, 360), R(0.7f, 1.3f));
                        else if (roll < grassChance + fernChance + 0.31f) Add(bins, Pick(deadwood, rng), hit.point, R(0, 360), R(0.8f, 1.5f));
                    }
            }

            // bushes and small rocks at the foot of the cliffs, inside the sections: the cliffs never meet the floor in a hard line
            foreach (var r in rooms)
                foreach (int side in new[] { -1, 1 })
                    for (float lz = -Half; lz < r.length + Half; lz += 1.7f)
                    {
                        var start = r.origin + r.rotation * new Vector3(side * R(10.7f, 11.6f), 80f, lz + R(-0.8f, 0.8f));
                        if (!GroundHit(start, out var hit)) continue;
                        if (hit.collider.isTrigger || hit.normal.y < 0.97f || !IndustrialKit.IsStatic(hit.collider)) continue;
                        if (hit.collider.GetComponentInParent<CourseCeiling>() != null || r.Distance(hit.point, out _) > -0.5f) continue;
                        if (NearGameplay(hit.point) || NearPass(passSamples, hit.point, 2.2f, 1.8f)) continue;
                        double roll = rng.NextDouble();
                        if (roll < 0.22) Add(bins, trees[NatureTreeBuilder.Species.Bush][rng.Next(NatureTreeBuilder.Variants)], hit.point, R(0, 360), R(0.8f, 1.1f));
                        else if (roll < 0.40) Add(bins, Pick(footRocks, rng), hit.point - Vector3.up * 0.1f, R(0, 360), R(0.7f, 1.6f));
                        else if (roll < 0.52) Add(bins, rocks[rng.Next(rocks.Length)], hit.point - Vector3.up * 0.15f, R(0, 360), R(0.6f, 1.2f));
                        else if (roll < 0.66) Add(bins, Pick(undergrowth, rng), hit.point, R(0, 360), R(0.8f, 1.5f));
                        else if (roll < 0.78) Add(bins, Pick(moss, rng), hit.point, R(0, 360), R(2f, 4f));
                        else if (roll < 0.86) Add(bins, Pick(deadwood, rng), hit.point, R(0, 360), R(0.8f, 1.6f));
                        else if (roll < 0.90) Add(bins, Pick(logs, rng), hit.point - Vector3.up * 0.05f, R(0, 360), R(0.6f, 0.9f));
                    }

            // scanned rock faces sunk into the cliffs: their bulk stays behind the cliff, only their relief shows (at most 30 cm in
            // front of it, so the cliff never seems further than it is), and they break the cliff's top line
            foreach (var r in rooms)
            {
                float roomYaw = r.rotation.eulerAngles.y;
                foreach (int side in new[] { -1, 1 })
                    for (float lz = -Half + R(0f, 3f); lz < r.length + Half; lz += R(3.5f, 7f))
                    {
                        var batch = Pick(cliffRocks, rng);
                        float localYaw = R(0f, 360f), scale = R(1.1f, 1.8f);
                        var bounds = batch.LocalBounds(0);
                        var toCentre = new Vector3(-side, 0, 0);
                        float m = float.MinValue, lowest = float.MaxValue;
                        var turn = Quaternion.Euler(0, localYaw, 0);
                        for (int c = 0; c < 8; c++)
                        {
                            var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                            var turned = turn * corner;
                            m = Mathf.Max(m, Vector3.Dot(turned, toCentre));
                            lowest = Mathf.Min(lowest, turned.y);
                        }
                        float ox = side * (Half - 0.3f + scale * m);
                        var footLocal = new Vector3(side * 11.4f, 0, lz);
                        if (!GroundHit(r.origin + r.rotation * (footLocal + Vector3.up * 80f), out var foot)) continue;
                        // only where a cliff really stands behind (not in a corner owned by the next section)
                        var probe = foot.point + Vector3.up * 2.5f - r.rotation * new Vector3(side * 2f, 0, 0);
                        if (!Physics.Raycast(probe, r.rotation * new Vector3(side, 0, 0), out var cliff, 4.5f, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore)
                            || !IndustrialKit.IsStatic(cliff.collider)) continue;
                        var origin = r.origin + r.rotation * new Vector3(ox, 0, lz);
                        origin.y = foot.point.y - R(0.4f, 1.5f) - lowest * scale;
                        Add(bins, batch, origin, roomYaw + localYaw, scale);
                    }
            }

            int total = 0;
            foreach (var (batch, cells) in bins)
                foreach (var ((cx, cz), list) in cells)
                {
                    float minY = list.Min(t => t.Item1.y), maxY = list.Max(t => t.Item1.y);
                    var cell = new FoliageSet.Cell { origin = new Vector3(cx * FoliageSet.CellSize, minY, cz * FoliageSet.CellSize), height = Mathf.Max(0.01f, maxY - minY) };
                    FoliageSet.Pack(batch, cell, list);
                    batch.cells.Add(cell);
                    total += list.Count;
                }
            EditorUtility.SetDirty(set);
            var go = new GameObject("Foliage");
            go.transform.SetParent(root, false);
            go.AddComponent<FoliageInstancer>().Configure(set);
            return total;
        }

        /// <summary>The first thing below <paramref name="start"/> that is not a cave roof (triggers included, so zones are found and refused).</summary>
        static bool GroundHit(Vector3 start, out RaycastHit hit)
        {
            hit = default;
            var hits = Physics.RaycastAll(start, Vector3.down, 160f, LayerMask.GetMask("Environment", "Hazard", "Trigger"), QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<CourseCeiling>() != null || h.distance >= best) continue;
                // a boulder or a crag is a rock, not ground: nothing grows on its collider's top
                var shape = h.collider.transform.Find("Visual")?.GetComponent<KitSkin>();
                if (shape != null && (shape.Shape == KitShape.Boulder || shape.Shape == KitShape.Crag)) { best = h.distance; found = false; continue; }
                best = h.distance;
                hit = h;
                found = true;
            }
            return found;
        }

        /// <summary>Every intended pass arc, sampled (plants keep clear of them, ARCHITECTURE §25.3).</summary>
        public static List<Vector3> PassSamples()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/ScriptableObjects/Tuning/GameTuning.asset");
            var all = new List<Vector3>();
            var samples = new List<Vector3>();
            foreach (var corridor in Object.FindObjectsByType<PassCorridor>(FindObjectsSortMode.None))
                if (corridor.TrySample(tuning, samples, 0.05f)) all.AddRange(samples);
            return all;
        }

        /// <summary>
        /// True when a plant <paramref name="height"/> tall and <paramref name="radius"/> wide standing at <paramref name="floor"/> would
        /// come within the decoration clearance (with a margin) of an intended pass arc.
        /// </summary>
        static bool NearPass(List<Vector3> samples, Vector3 floor, float height, float radius)
        {
            float reach = radius + PassCorridor.DecorationClearance + 0.4f;
            foreach (var s in samples)
            {
                if (s.y < floor.y - 0.5f || s.y > floor.y + height + PassCorridor.DecorationClearance + 0.4f) continue;
                float dx = s.x - floor.x, dz = s.z - floor.z;
                if (dx * dx + dz * dz < reach * reach) return true;
            }
            return false;
        }

        /// <summary>True near a gameplay volume or a moving piece: a checkpoint, a zone, a plate, a pad, the finish, a mover.</summary>
        static bool NearGameplay(Vector3 point)
        {
            foreach (var c in Physics.OverlapSphere(point + Vector3.up * 0.6f, 1.6f, ~0, QueryTriggerInteraction.Collide))
            {
                if (c.GetComponentInParent<Checkpoint>() != null || c.GetComponentInParent<Zone>() != null || c.GetComponentInParent<FinishZone>() != null
                    || c.GetComponentInParent<LaunchPad>() != null || c.GetComponentInParent<PlayerSpawn>() != null || c.GetComponentInParent<BombTransit>() != null
                    || c.GetComponentInParent<KillZone>() != null || !IndustrialKit.IsStatic(c))
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ scanned props

        static int HeroProps(Transform root, List<Room> rooms, Heightfield field)
        {
            var props = new GameObject("Scanned props").transform;
            props.SetParent(root, false);
            var rng = new Random(77);
            int count = 0;
            (string model, string material, float height)[] kinds =
            {
                ("boulder_01", "Nature_Prop_Boulder", 2.6f),
                ("rock_moss_set_01", "Nature_Prop_RockMoss", 1.6f),
                ("rock_face_01", "Nature_Prop_RockFace", 4.5f),
                ("dead_tree_trunk_02", "Nature_Prop_DeadTrunk", 1.4f),
                ("tree_stump_01", "Nature_Prop_Stump", 0.9f),
            };
            foreach (var r in rooms)
                for (int k = 0; k < 3; k++)
                {
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    var local = new Vector3(side * (Wall + 2.5f + (float)rng.NextDouble() * 5f), 0, (float)rng.NextDouble() * r.length);
                    var world = r.origin + r.rotation * local;
                    if (!field.Sample(world.x, world.z, out float y)) continue;
                    world.y = y;
                    if (rooms.Any(o => o.Distance(world, out _) < 1.5f)) continue;
                    var kind = kinds[rng.Next(kinds.Length)];
                    var go = NatureKit.Prop(props, kind.model, kind.material, props.InverseTransformPoint(world), (float)rng.NextDouble() * 360f, kind.height);
                    go.isStatic = true;
                    count++;
                }
            return count;
        }
    }
}
