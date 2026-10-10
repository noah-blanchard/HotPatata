using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// The forest PatataCanopy stands in (ARCHITECTURE §4 and §25.3, M13.7), decoration only: a forest floor that follows the course
    /// (always <see cref="Drop"/> metres under each section's lowest floor), the posts under the decks grown into trunks that reach
    /// it, giant oaks and redwoods towering beside the course (their crowns overhang its edges high above the roofs, a strip of sky
    /// left over the open sections), more giants, an understory and the ground cover out to the haze, all drawn instanced
    /// (<see cref="FoliageSet"/>), and the mist floor (<see cref="MistField"/>). Nothing collides; nothing comes near a pass arc,
    /// a walkable floor or a moving piece. Deterministic.
    /// </summary>
    public static partial class PatataCanopyBuilder
    {
        const float Drop = 40f;              // the forest floor under a section's lowest floor
        const float TerrainCell = 8f, TerrainMargin = 280f;
        const float CrownClearance = 12f;    // a crown over a section stays this far above its highest floor or roof
        const float SkyStrip = 4f;           // over an open section no crown comes within this of its centre line
        const float AisleSpacing = 16f;      // between two giants along a section's side
        static string ForestFoliagePath => NatureDressing.GeneratedDir + "PatataCanopy_Foliage.asset";
        static string MistFloorPath => NatureDressing.GeneratedDir + "PatataCanopy_MistFloor.asset";

        /// <summary>A deck's trunk, asked for while its section is built (section space) and grown once the forest floor is known.</summary>
        struct SupportRequest
        {
            public Transform parent;
            public Vector3 localTop;
            public float thickness;
        }

        static readonly List<SupportRequest> Supports = new List<SupportRequest>();

        /// <summary>The forest floor: a heightfield of TerrainCell cells.</summary>
        sealed class Ground
        {
            public float x0, z0;
            public int nx, nz;
            public Vector3[,] points;

            public float Sample(float x, float z)
            {
                float fx = Mathf.Clamp((x - x0) / TerrainCell, 0f, nx - 1.001f), fz = Mathf.Clamp((z - z0) / TerrainCell, 0f, nz - 1.001f);
                int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz);
                float u = fx - i, v = fz - j;
                return Mathf.Lerp(Mathf.Lerp(points[i, j].y, points[i + 1, j].y, u), Mathf.Lerp(points[i, j + 1].y, points[i + 1, j + 1].y, u), v);
            }

            public float Lowest => points.Cast<Vector3>().Min(p => p.y);
        }

        static Ground ground;

        /// <summary>A section's footprint in world XZ (its decks, roofs and the void beside them), grown by <paramref name="grow"/>.</summary>
        static Rect FootRect(Footprint f, float grow = 0f)
        {
            var rot = Quaternion.Euler(0, f.yaw, 0);
            var a = f.origin + rot * new Vector3(-CoverHalf - grow, 0, -Hub - grow);
            var b = f.origin + rot * new Vector3(CoverHalf + grow, 0, f.length + Hub + grow);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
        }

        static float DistanceToRect(Rect r, float x, float z) =>
            new Vector2(Mathf.Max(0f, Mathf.Max(r.xMin - x, x - r.xMax)), Mathf.Max(0f, Mathf.Max(r.yMin - z, z - r.yMax))).magnitude;

        static float Noise(float x, float z, int seed) =>
            NatureShapes.Noise(new Vector3(x * 0.011f, 0.5f, z * 0.011f), seed) * 0.6f + NatureShapes.Noise(new Vector3(x * 0.04f, 1.5f, z * 0.04f), seed + 5) * 0.4f;

        /// <summary>The highest solid of each section (its top floor or roof): crowns stay CrownClearance above it.</summary>
        static void MeasureTops(IReadOnlyList<Transform> rooms)
        {
            for (int i = 0; i < Footprints.Count; i++)
            {
                var f = Footprints[i];
                f.topY = rooms[i].GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger && c.GetComponentInParent<KillZone>() == null)
                    .Select(c => c.bounds.max.y).DefaultIfEmpty(f.floorY).Max();
                Footprints[i] = f;
            }
        }

        static string Forest(Transform group)
        {
            var root = new GameObject("Forest").transform;
            root.SetParent(group, false);
            var rects = Footprints.Select(f => FootRect(f)).ToArray();
            float minX = rects.Min(r => r.xMin) - TerrainMargin, maxX = rects.Max(r => r.xMax) + TerrainMargin;
            float minZ = rects.Min(r => r.yMin) - TerrainMargin, maxZ = rects.Max(r => r.yMax) + TerrainMargin;

            // the floor: under each section its lowest floor less Drop, blended between them, gently rolling, rising a little away
            // from the course (a valley where the mist pools)
            var g = ground = new Ground { x0 = minX, z0 = minZ, nx = Mathf.CeilToInt((maxX - minX) / TerrainCell) + 1, nz = Mathf.CeilToInt((maxZ - minZ) / TerrainCell) + 1 };
            g.points = new Vector3[g.nx, g.nz];
            for (int i = 0; i < g.nx; i++)
                for (int j = 0; j < g.nz; j++)
                {
                    float x = minX + i * TerrainCell, z = minZ + j * TerrainCell;
                    float wsum = 0f, hsum = 0f, nearest = float.MaxValue;
                    for (int k = 0; k < rects.Length; k++)
                    {
                        float d = DistanceToRect(rects[k], x, z);
                        nearest = Mathf.Min(nearest, d);
                        if (d > 260f) continue;
                        float w = 1f / Mathf.Pow(d + 6f, 3f);
                        wsum += w;
                        hsum += w * (Footprints[k].floorY - Drop);
                    }
                    float h = wsum > 0f ? hsum / wsum : Footprints.Min(f => f.floorY) - Drop;
                    h += (Noise(x, z, 31) - 0.5f) * 2f * Mathf.Lerp(2.2f, 9f, Mathf.Clamp01(nearest / 90f));
                    h += 10f * (1f - Mathf.Exp(-nearest / 140f));
                    g.points[i, j] = new Vector3(x, h, z);
                }
            var terrainRoot = new GameObject("Forest floor").transform;
            terrainRoot.SetParent(root, false);
            int chunks = NatureTerrainMesh.Build(terrainRoot, g.points, (a, b, c) => true, (i, j) => 0,
                _ => NatureMaterialBuilder.Load("Nature_ForestFloor"), NatureDressing.GeneratedDir, "PatataCanopy_Terrain");

            // the mist lies on that floor
            var (mistFloor, mistRect) = NatureTerrainMesh.BakeMistFloor(g.Sample, Rect.MinMaxRect(minX, minZ, maxX, maxZ), 256, 3, MistFloorPath);
            var mist = new GameObject("Mist");
            mist.transform.SetParent(root, false);
            mist.AddComponent<MistField>().Configure(mistFloor, mistRect, 0.95f);

            string forest = Plant(root);
            return $"forest floor {chunks} chunks, {forest}";
        }

        // ------------------------------------------------------------------ the instanced forest

        static Mesh M(string kind, int variant, int lod) => AssetDatabase.LoadAssetAtPath<Mesh>(NatureTreeBuilder.MeshPath(kind, variant, lod));

        sealed class Bins
        {
            readonly Dictionary<FoliageSet.Batch, Dictionary<(int, int), List<(Vector3, float, float)>>> bins =
                new Dictionary<FoliageSet.Batch, Dictionary<(int, int), List<(Vector3, float, float)>>>();

            public int Count;

            public void Add(FoliageSet.Batch batch, Vector3 p, float yaw, float scale)
            {
                if (!bins.TryGetValue(batch, out var cells)) bins[batch] = cells = new Dictionary<(int, int), List<(Vector3, float, float)>>();
                var key = (Mathf.FloorToInt(p.x / FoliageSet.CellSize), Mathf.FloorToInt(p.z / FoliageSet.CellSize));
                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<(Vector3, float, float)>();
                list.Add((p, yaw, Mathf.Clamp(scale, batch.minScale, batch.maxScale)));
                Count++;
            }

            public void Pack()
            {
                foreach (var (batch, cells) in bins)
                    foreach (var ((cx, cz), list) in cells)
                    {
                        float minY = list.Min(t => t.Item1.y), maxY = list.Max(t => t.Item1.y);
                        var cell = new FoliageSet.Cell { origin = new Vector3(cx * FoliageSet.CellSize, minY, cz * FoliageSet.CellSize), height = Mathf.Max(0.01f, maxY - minY) };
                        FoliageSet.Pack(batch, cell, list);
                        batch.cells.Add(cell);
                    }
            }
        }

        /// <summary>A batch's world box for an instance (its nearest level's mesh, turned and scaled).</summary>
        static Bounds WorldBox(FoliageSet.Batch batch, Vector3 p, float yaw, float scale)
        {
            var local = batch.LocalBounds(0);
            var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
            var b = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int c = 0; c < 8; c++)
                b.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
            return b;
        }

        static bool OverlapsXZ(Rect r, Bounds b) => b.max.x > r.xMin && b.min.x < r.xMax && b.max.z > r.yMin && b.min.z < r.yMax;

        /// <summary>True when a solid, a zone or a gameplay volume (anything but a kill zone) lies inside <paramref name="b"/>.</summary>
        static bool HitsTheCourse(Bounds b)
        {
            foreach (var c in Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
                if (c.GetComponentInParent<KillZone>() == null) return true;
            return false;
        }

        static string Plant(Transform root)
        {
            var set = AssetDatabase.LoadAssetAtPath<FoliageSet>(ForestFoliagePath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<FoliageSet>();
                AssetDatabase.CreateAsset(set, ForestFoliagePath);
            }
            set.batches.Clear();
            var bins = new Bins();
            NatureDressing.IndexArcs(NatureDressing.PassSamples());
            var rng = new Random(1313);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var rects = Footprints.Select(f => FootRect(f)).ToArray();

            // ---- batches
            float[] giantNear = { 90f, 260f, 750f }, rootsRange = { 70f, 200f };
            bool[] giantShadows = { true, true, false };
            var giants = new Dictionary<NatureTreeBuilder.Giant, FoliageSet.Batch[][]>();
            foreach (NatureTreeBuilder.Giant kind in System.Enum.GetValues(typeof(NatureTreeBuilder.Giant)))
            {
                var (bark, leaves) = NatureTreeBuilder.MaterialsFor(kind);
                var perVariant = new FoliageSet.Batch[NatureTreeBuilder.Variants][];
                for (int v = 0; v < NatureTreeBuilder.Variants; v++)
                {
                    perVariant[v] = new FoliageSet.Batch[3];
                    for (int part = 0; part < 3; part++)
                    {
                        string k = NatureTreeBuilder.GiantKind(kind, part);
                        var lods = part == 0 ? new[] { M(k, v, 0), M(k, v, 1) } : new[] { M(k, v, 0), M(k, v, 1), M(k, v, 2) };
                        var materials = part == 2 ? new[] { NatureMaterialBuilder.Load(bark), NatureMaterialBuilder.Load(leaves) } : new[] { NatureMaterialBuilder.Load(bark) };
                        var batch = NatureDressing.Batch(set, $"{k} {v}", lods, materials, part == 0 ? rootsRange : giantNear,
                                                         part == 0 ? new[] { false, false } : giantShadows, 0.75f, 1.4f);
                        batch.shadowDistance = 140f;
                        perVariant[v][part] = batch;
                    }
                }
                giants[kind] = perVariant;
            }
            var deckTrunks = Enumerable.Range(0, NatureTreeBuilder.DeckTrunkRadius.Length).Select(v =>
            {
                var b = NatureDressing.Batch(set, "DeckTrunk " + v, new[] { M("DeckTrunk", v, 0), M("DeckTrunk", v, 1) }, new[] { NatureMaterialBuilder.Load("Nature_PineBark") },
                                             new[] { 110f, 420f }, new[] { true, false }, 0.6f, 1.6f);
                b.shadowDistance = 90f;
                return b;
            }).ToArray();
            var understory = new[] { NatureTreeBuilder.Species.Broadleaf, NatureTreeBuilder.Species.Fir, NatureTreeBuilder.Species.Pine, NatureTreeBuilder.Species.Snag }
                .ToDictionary(s => s, s => Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                {
                    var lods = new[] { M(s.ToString(), v, 0), M(s.ToString(), v, 1), M(s.ToString(), v, 2) };
                    var b = NatureDressing.Batch(set, $"{s} {v}", lods, NatureTreeBuilder.MaterialsFor(s, lods[0]), new[] { 70f, 170f, 420f }, new[] { true, false, false }, 1.4f, 2.8f);
                    b.shadowDistance = 60f;
                    return b;
                }).ToArray());
            var bushes = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
            {
                var lods = new[] { M("Bush", v, 0), M("Bush", v, 1), M("Bush", v, 2) };
                return NatureDressing.Batch(set, "Bush " + v, lods, NatureTreeBuilder.MaterialsFor(NatureTreeBuilder.Species.Bush, lods[0]), new[] { 55f, 120f, 220f }, new[] { false, false, false }, 1f, 2f);
            }).ToArray();
            var ferns = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                NatureDressing.Batch(set, "Fern " + v, new[] { M("Fern", v, 0) }, new[] { NatureMaterialBuilder.Load("Nature_Fern") }, new[] { 90f }, new[] { false }, 1.8f, 3.4f)).ToArray();
            var rocks = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                NatureDressing.Batch(set, "Rock " + v, new[] { M("Rock", v, 0), M("Rock", v, 1) }, new[] { NatureMaterialBuilder.Load("Nature_MossyRock") }, new[] { 70f, 220f }, new[] { false, false }, 0.8f, 3f)).ToArray();
            float[] mid = { 60f, 150f, 300f };
            int[] lodsMid = { 2, 4, 6 };
            bool[] noShadow = { false, false, false };
            var logs = NatureDressing.ModelBatches(set, "dead_tree_trunk", "Nature_Prop_DeadTrunk1", 1.2f, mid, lodsMid, noShadow, 1.2f, 2.4f)
                .Concat(NatureDressing.ModelBatches(set, "tree_stump_02", "Nature_Prop_Stump2", 1.4f, mid, lodsMid, noShadow, 1f, 2f))
                .Concat(NatureDressing.ModelBatches(set, "root_cluster_01", "Nature_Prop_Roots", 2.2f, mid, lodsMid, noShadow, 0.9f, 1.6f))
                .Concat(NatureDressing.ModelBatches(set, "rock_moss_set_01", "Nature_Prop_RockMoss", 1.6f, mid, lodsMid, noShadow, 0.8f, 2f)).ToArray();
            var moss = NatureDressing.ModelBatches(set, "moss_01", "Nature_Moss", 0.12f, new[] { 80f }, new[] { -1 }, new[] { false }, 3f, 7f);
            T Pick<T>(T[] options) => options[rng.Next(options.Length)];

            // ---- the posts under the decks, down to the floor
            int posts = 0;
            foreach (var s in Supports)
            {
                var top = s.parent.TransformPoint(s.localTop);
                float foot = ground.Sample(top.x, top.z) - 0.8f;
                float scale = (top.y + 0.3f - foot) / NatureTreeBuilder.DeckTrunkLength;
                int v = s.thickness < 1.6f ? 0 : s.thickness < 2.4f ? 1 : 2;
                bins.Add(deckTrunks[v], new Vector3(top.x, foot, top.z), R(0f, 360f), scale);
                posts++;
            }

            // ---- the giants
            var boles = new List<Vector3>();
            bool TryGiant(NatureTreeBuilder.Giant kind, int v, Vector3 at, float yaw, float scale)
            {
                var parts = giants[kind][v];
                scale = Mathf.Clamp(scale, parts[0].minScale, parts[0].maxScale);
                var rootsBox = WorldBox(parts[0], at, yaw, scale);
                var boleBox = WorldBox(parts[1], at, yaw, scale);
                var crownBox = WorldBox(parts[2], at, yaw, scale);
                float boleRadius = Mathf.Max(boleBox.extents.x, boleBox.extents.z);
                if (boles.Any(b => new Vector2(b.x - at.x, b.z - at.z).magnitude < boleRadius + b.y + 6f)) return false;
                for (int k = 0; k < Footprints.Count; k++)
                {
                    var f = Footprints[k];
                    // the bole never stands in a section's footprint (nor its void), whatever the height
                    if (OverlapsXZ(FootRect(f, 1f), boleBox)) return false;
                    if (!OverlapsXZ(rects[k], crownBox)) continue;
                    // a crown over a section: high above it, and a strip of sky left over an open one
                    if (crownBox.min.y < f.topY + CrownClearance) return false;
                    if (Outdoors[2 * k])
                    {
                        var inverse = Quaternion.Inverse(Quaternion.Euler(0, f.yaw, 0));
                        float lo = float.MaxValue, hi = float.MinValue;
                        for (int c = 0; c < 4; c++)
                        {
                            var corner = new Vector3((c & 1) == 0 ? crownBox.min.x : crownBox.max.x, 0, (c & 2) == 0 ? crownBox.min.z : crownBox.max.z);
                            float x = (inverse * (corner - f.origin)).x;
                            lo = Mathf.Min(lo, x);
                            hi = Mathf.Max(hi, x);
                        }
                        if (lo < SkyStrip && hi > -SkyStrip) return false;
                    }
                }
                foreach (var part in parts)
                    if (NatureDressing.TouchesAnArc(part, at, yaw, scale)) return false;
                if (HitsTheCourse(boleBox) || HitsTheCourse(crownBox)) return false;
                for (int part = 0; part < 3; part++) bins.Add(parts[part], at, yaw, scale);
                boles.Add(new Vector3(at.x, boleRadius, at.z));
                return true;
            }
            NatureTreeBuilder.Giant RandomGiant() => rng.NextDouble() < 0.58 ? NatureTreeBuilder.Giant.Oak : NatureTreeBuilder.Giant.Redwood;

            // beside the course: one every AisleSpacing along both sides, pushed out until it fits
            int aisle = 0;
            for (int k = 0; k < Footprints.Count; k++)
            {
                var f = Footprints[k];
                var rot = Quaternion.Euler(0, f.yaw, 0);
                foreach (int side in new[] { -1, 1 })
                    for (float lz = -Hub + R(0f, AisleSpacing * 0.5f); lz < f.length + Hub; lz += AisleSpacing * R(0.8f, 1.2f))
                    {
                        var kind = RandomGiant();
                        int v = rng.Next(NatureTreeBuilder.Variants);
                        float yaw = R(0f, 360f), scale = R(0.95f, 1.2f);
                        float boleHalf = Mathf.Max(giants[kind][v][1].LocalBounds(0).extents.x, giants[kind][v][1].LocalBounds(0).extents.z);
                        for (int attempt = 0; attempt < 6; attempt++)
                        {
                            var local = new Vector3(side * (CoverHalf + boleHalf * scale + 1.5f + attempt * 3.5f + R(0f, 1.5f)), 0f, lz);
                            var at = f.origin + rot * local;
                            at.y = ground.Sample(at.x, at.z) - 0.6f;
                            float s = scale * (1f + attempt * 0.05f);
                            if (TryGiant(kind, v, at, yaw, s)) { aisle++; break; }
                        }
                    }
            }

            // the forest beyond: giants every ~24 m out to the haze, the understory under them, the ground cover near the course
            int forest = 0, under = 0, cover = 0;
            float minX = ground.x0, minZ = ground.z0, maxX = ground.x0 + (ground.nx - 1) * TerrainCell, maxZ = ground.z0 + (ground.nz - 1) * TerrainCell;
            for (float x = minX + 12f; x < maxX - 12f; x += 24f)
                for (float z = minZ + 12f; z < maxZ - 12f; z += 24f)
                {
                    var at = new Vector3(x + R(-9f, 9f), 0f, z + R(-9f, 9f));
                    at.y = ground.Sample(at.x, at.z) - 0.6f;
                    if (rng.NextDouble() < 0.82 && TryGiant(RandomGiant(), rng.Next(NatureTreeBuilder.Variants), at, R(0f, 360f), R(0.8f, 1.25f))) forest++;
                }
            var understoryKinds = understory.Keys.ToArray();
            for (float x = minX + 6f; x < maxX - 6f; x += 12f)
                for (float z = minZ + 6f; z < maxZ - 6f; z += 12f)
                {
                    var at = new Vector3(x + R(-5f, 5f), 0f, z + R(-5f, 5f));
                    float near = rects.Min(r => DistanceToRect(r, at.x, at.z));
                    if (near > 170f || rng.NextDouble() > 0.62) continue;
                    at.y = ground.Sample(at.x, at.z) - 0.3f;
                    var species = understoryKinds[rng.NextDouble() < 0.08 ? 3 : rng.Next(3)];
                    var batch = Pick(understory[species]);
                    float yaw = R(0f, 360f), scale = R(1.4f, 2.8f);
                    var box = WorldBox(batch, at, yaw, scale);
                    if (boles.Any(b => new Vector2(b.x - at.x, b.z - at.z).magnitude < b.y + 2f)) continue;
                    bool fits = true;
                    for (int k = 0; k < Footprints.Count && fits; k++)
                        if (OverlapsXZ(FootRect(Footprints[k], 2f), box) && box.max.y > Footprints[k].floorY - 12f) fits = false;
                    if (!fits || NatureDressing.TouchesAnArc(batch, at, yaw, scale) || HitsTheCourse(box)) continue;
                    bins.Add(batch, at, yaw, scale);
                    under++;
                }
            for (float x = minX + 2.5f; x < maxX - 2.5f; x += 5f)
                for (float z = minZ + 2.5f; z < maxZ - 2.5f; z += 5f)
                {
                    var at = new Vector3(x + R(-2.2f, 2.2f), 0f, z + R(-2.2f, 2.2f));
                    float near = rects.Min(r => DistanceToRect(r, at.x, at.z));
                    if (near > 110f) continue;
                    at.y = ground.Sample(at.x, at.z);
                    double roll = rng.NextDouble();
                    FoliageSet.Batch batch;
                    float scale;
                    if (roll < 0.26) { batch = Pick(ferns); scale = R(1.8f, 3.4f); }
                    else if (roll < 0.36) { batch = Pick(bushes); scale = R(1f, 2f); }
                    else if (roll < 0.42) { batch = Pick(rocks); scale = R(0.8f, 3f); at.y -= 0.4f; }
                    else if (roll < 0.47) { batch = Pick(logs); scale = R(0.9f, 2f); }
                    else if (roll < 0.58) { batch = Pick(moss); scale = R(3f, 7f); }
                    else continue;
                    float yaw = R(0f, 360f);
                    if (boles.Any(b => new Vector2(b.x - at.x, b.z - at.z).magnitude < b.y + 0.5f)) continue;
                    if (NatureDressing.TouchesAnArc(batch, at, yaw, scale)) continue;
                    bins.Add(batch, at, yaw, scale);
                    cover++;
                }
            bins.Pack();
            EditorUtility.SetDirty(set);
            var go = new GameObject("Foliage");
            go.transform.SetParent(root, false);
            go.AddComponent<FoliageInstancer>().Configure(set);
            return $"{posts} deck trunks, {aisle} giants beside the course, {forest} in the forest, {under} understory trees, {cover} plants and rocks ({bins.Count} instances)";
        }
    }
}
