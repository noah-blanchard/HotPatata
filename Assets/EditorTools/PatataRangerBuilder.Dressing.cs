using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// PatataRanger's forest (ARCHITECTURE §25.5), planted after the post, decoration only and drawn instanced (<see cref="FoliageSet"/>):
    /// giants in groves with clearings between them (a slow noise decides where), an understory, and near the route ferns, bushes,
    /// mossy rocks, fallen logs and stumps. Nothing grows on a trail, in the ravine, on rock too steep to hold it, inside the timber,
    /// on a pass arc or too near the giants the post is built round. Deterministic.
    /// </summary>
    public static partial class PatataRangerBuilder
    {
        static string ForestFoliagePath => GeneratedDir + "PatataRanger_Foliage.asset";

        /// <summary>Instances binned in FoliageSet cells per batch, then packed.</summary>
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

        static Mesh M(string kind, int variant, int lod) => AssetDatabase.LoadAssetAtPath<Mesh>(NatureTreeBuilder.MeshPath(kind, variant, lod));

        static Bounds WorldBox(FoliageSet.Batch batch, Vector3 p, float yaw, float scale)
        {
            var local = batch.LocalBounds(0);
            var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
            var b = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int c = 0; c < 8; c++)
                b.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
            return b;
        }

        /// <summary>True when anything of the course (timber, a solid, a zone; not the ground, not a fall) lies inside the box.</summary>
        static bool HitsTheCourse(Bounds b)
        {
            foreach (var c in Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
            {
                if (c.GetComponentInParent<KillZone>() != null) continue;
                var proxy = c.GetComponent<WalkableProxy>();
                if (proxy != null && proxy.ProxyKind == WalkableProxy.Kind.Terrain) continue;
                return true;
            }
            return false;
        }

        /// <summary>How steep the ground is at (x, z): rise over run across a metre.</summary>
        static float SlopeAt(float x, float z)
        {
            float dx = GroundAt(x + 0.7f, z) - GroundAt(x - 0.7f, z), dz = GroundAt(x, z + 0.7f) - GroundAt(x, z - 0.7f);
            return new Vector2(dx, dz).magnitude / 1.4f;
        }

        /// <summary>The distance in plan to the nearest walked or built thing of the route.</summary>
        static float RouteDistance(float x, float z)
        {
            float best = TrailDistance(x, z);
            foreach (var r in Route) best = Mathf.Min(best, new Vector2(r.x - x, r.z - z).magnitude);
            return best;
        }

        static string Dressing(Transform forest)
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
            var rng = new Random(2027);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            T Pick<T>(T[] options) => options[rng.Next(options.Length)];

            // ---- batches
            var giants = new List<FoliageSet.Batch[]>();
            foreach (NatureTreeBuilder.Giant kind in System.Enum.GetValues(typeof(NatureTreeBuilder.Giant)))
            {
                var (bark, leaves) = NatureTreeBuilder.MaterialsFor(kind);
                for (int v = 0; v < NatureTreeBuilder.Variants; v++)
                {
                    var parts = new FoliageSet.Batch[3];
                    for (int part = 0; part < 3; part++)
                    {
                        string k = NatureTreeBuilder.GiantKind(kind, part);
                        var lods = part == 0 ? new[] { M(k, v, 0), M(k, v, 1) } : new[] { M(k, v, 0), M(k, v, 1), M(k, v, 2) };
                        var materials = part == 2 ? new[] { NatureMaterialBuilder.Load(bark), NatureMaterialBuilder.Load(leaves) } : new[] { NatureMaterialBuilder.Load(bark) };
                        var batch = NatureDressing.Batch(set, $"{k} {v}", lods, materials, part == 0 ? new[] { 60f, 150f } : new[] { 80f, 210f, 460f },
                                                         part == 0 ? new[] { false, false } : new[] { true, true, false }, 0.5f, 0.8f);
                        batch.shadowDistance = 110f;
                        parts[part] = batch;
                    }
                    giants.Add(parts);
                }
            }
            var understory = new[] { NatureTreeBuilder.Species.Broadleaf, NatureTreeBuilder.Species.Fir, NatureTreeBuilder.Species.Pine, NatureTreeBuilder.Species.Snag }
                .Select(s => Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                {
                    var lods = new[] { M(s.ToString(), v, 0), M(s.ToString(), v, 1), M(s.ToString(), v, 2) };
                    var b = NatureDressing.Batch(set, $"{s} {v}", lods, NatureTreeBuilder.MaterialsFor(s, lods[0]), new[] { 60f, 150f, 320f }, new[] { true, false, false }, 1.1f, 2.2f);
                    b.shadowDistance = 70f;
                    return b;
                }).ToArray()).ToArray();
            var bushes = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
            {
                var lods = new[] { M("Bush", v, 0), M("Bush", v, 1), M("Bush", v, 2) };
                return NatureDressing.Batch(set, "Bush " + v, lods, NatureTreeBuilder.MaterialsFor(NatureTreeBuilder.Species.Bush, lods[0]), new[] { 45f, 100f, 170f }, new[] { false, false, false }, 0.9f, 2f);
            }).ToArray();
            var ferns = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                NatureDressing.Batch(set, "Fern " + v, new[] { M("Fern", v, 0) }, new[] { NatureMaterialBuilder.Load("Nature_Fern") }, new[] { 70f }, new[] { false }, 1.6f, 3.2f)).ToArray();
            var grass = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                NatureDressing.Batch(set, "Grass " + v, new[] { M("Grass", v, 0) }, new[] { NatureMaterialBuilder.Load("Nature_GrassBlades") }, new[] { 45f }, new[] { false }, 1.2f, 2.2f)).ToArray();
            var rocks = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                NatureDressing.Batch(set, "Rock " + v, new[] { M("Rock", v, 0), M("Rock", v, 1) }, new[] { NatureMaterialBuilder.Load("Nature_MossyRock") }, new[] { 70f, 180f }, new[] { true, false }, 0.8f, 3.4f)).ToArray();
            float[] mid = { 55f, 130f, 240f };
            int[] lodsMid = { 2, 4, 6 };
            bool[] noShadow = { false, false, false };
            var logs = NatureDressing.ModelBatches(set, "dead_tree_trunk", "Nature_Prop_DeadTrunk1", 1.2f, mid, lodsMid, noShadow, 1.2f, 2.4f)
                .Concat(NatureDressing.ModelBatches(set, "tree_stump_02", "Nature_Prop_Stump2", 1.4f, mid, lodsMid, noShadow, 1f, 2f))
                .Concat(NatureDressing.ModelBatches(set, "root_cluster_01", "Nature_Prop_Roots", 2.2f, mid, lodsMid, noShadow, 0.9f, 1.6f)).ToArray();
            var boulders = NatureDressing.ModelBatches(set, "rock_moss_set_01", "Nature_Prop_RockMoss", 1.6f, mid, lodsMid, new[] { true, false, false }, 1f, 3.2f)
                .Concat(NatureDressing.ModelBatches(set, "boulder_01", "Nature_Prop_Boulder", 2.6f, mid, lodsMid, new[] { true, false, false }, 0.8f, 2.2f)).ToArray();
            var moss = NatureDressing.ModelBatches(set, "moss_01", "Nature_Moss", 0.12f, new[] { 80f }, new[] { -1 }, new[] { false }, 3f, 7f);

            var heroes = Giants().Select(g => new Vector2(g.foot.x, g.foot.z)).ToList();
            bool NearHero(float x, float z, float r) => heroes.Any(h => Vector2.Distance(h, new Vector2(x, z)) < r);
            bool Clearing(float x, float z) => new Vector2(x, z - 6f).magnitude < 11f || new Vector2(x - 2f, z - 108f).magnitude < 10f;
            float Grove(float x, float z) => Noise(x, z, 0.016f, 61) * 0.75f + Noise(x, z, 0.05f, 62) * 0.25f;   // 0..1: < 0.2 a clearing, > 0.5 a dense grove
            bool InRavine(float x, float z) => Mathf.Abs(z - RavineZ(x)) < RavineHalf(x) + 1.5f;

            // ---- giants: 45 to 65 m, in groves, kept off the route's air and off the post's trees
            int big = 0;
            var boles = new List<Vector3>();
            for (float x = ValleyMinX + 8f; x < ValleyMaxX - 8f; x += 12f)
                for (float z = ValleyMinZ + 8f; z < ValleyMaxZ - 8f; z += 12f)
                {
                    float px = x + R(-5f, 5f), pz = z + R(-5f, 5f);
                    float g = Grove(px, pz);
                    if (rng.NextDouble() > Mathf.InverseLerp(0.12f, 0.48f, g)) continue;
                    if (Clearing(px, pz) || InRavine(px, pz) || SlopeAt(px, pz) > 0.85f || NearHero(px, pz, 14f)) continue;
                    if (RouteDistance(px, pz) < 10f) continue;
                    var parts = giants[rng.Next(giants.Count)];
                    float yaw = R(0f, 360f), scale = R(0.5f, 0.78f);
                    var at = new Vector3(px, GroundAt(px, pz) - 0.6f, pz);
                    var boleBox = WorldBox(parts[1], at, yaw, scale);
                    float boleR = Mathf.Max(boleBox.extents.x, boleBox.extents.z);
                    if (boles.Any(b => new Vector2(b.x - px, b.z - pz).magnitude < boleR + b.y + 2.5f)) continue;
                    if (parts.Any(part => NatureDressing.TouchesAnArc(part, at, yaw, scale))) continue;
                    if (HitsTheCourse(boleBox) || HitsTheCourse(WorldBox(parts[2], at, yaw, scale))) continue;
                    foreach (var part in parts) bins.Add(part, at, yaw, scale);
                    boles.Add(new Vector3(px, boleR, pz));
                    big++;
                }

            // ---- understory: younger trees, more of them in the groves and at the clearings' edges
            int under = 0;
            for (float x = ValleyMinX + 4f; x < ValleyMaxX - 4f; x += 5f)
                for (float z = ValleyMinZ + 4f; z < ValleyMaxZ - 4f; z += 5f)
                {
                    float px = x + R(-2.2f, 2.2f), pz = z + R(-2.2f, 2.2f);
                    float g = Grove(px, pz);
                    if (rng.NextDouble() > Mathf.Lerp(0.35f, 0.92f, Mathf.InverseLerp(0.2f, 0.6f, g))) continue;
                    if (Clearing(px, pz) || InRavine(px, pz) || SlopeAt(px, pz) > 0.9f || NearHero(px, pz, 5f)) continue;
                    if (RouteDistance(px, pz) < 3.5f || boles.Any(b => new Vector2(b.x - px, b.z - pz).magnitude < b.y + 1.5f)) continue;
                    var species = rng.NextDouble() < 0.1 ? 3 : rng.Next(3);
                    var batch = Pick(understory[species]);
                    float yaw = R(0f, 360f), scale = R(1.1f, 2.2f);
                    var at = new Vector3(px, GroundAt(px, pz) - 0.3f, pz);
                    if (NatureDressing.TouchesAnArc(batch, at, yaw, scale) || HitsTheCourse(WorldBox(batch, at, yaw, scale))) continue;
                    bins.Add(batch, at, yaw, scale);
                    under++;
                }

            // ---- the ground near the route: ferns and grass in the light, bushes, rocks half sunk, logs, stumps, moss
            int cover = 0;
            for (float x = ValleyMinX + 2f; x < ValleyMaxX - 2f; x += 2.4f)
                for (float z = ValleyMinZ + 2f; z < ValleyMaxZ - 2f; z += 2.4f)
                {
                    float px = x + R(-1.4f, 1.4f), pz = z + R(-1.4f, 1.4f);
                    float near = RouteDistance(px, pz);
                    if (near > 120f || near < 1.6f || TrailDistance(px, pz) < 2.4f) continue;
                    if (InRavine(px, pz) && GroundAt(px, pz) < RavineFloor + 1f) continue;
                    float slope = SlopeAt(px, pz);
                    double roll = rng.NextDouble();
                    FoliageSet.Batch batch;
                    float scale;
                    var at = new Vector3(px, GroundAt(px, pz), pz);
                    if (slope > 0.75f)
                    {
                        if (roll > 0.12) continue;
                        batch = Pick(boulders);
                        scale = R(1f, 3f);
                        at.y -= 0.5f;
                    }
                    else if (roll < 0.22) { batch = Pick(ferns); scale = R(1.6f, 3.2f); }
                    else if (roll < 0.3) { batch = Pick(grass); scale = R(1.2f, 2.2f); }
                    else if (roll < 0.37) { batch = Pick(bushes); scale = R(0.9f, 2f); }
                    else if (roll < 0.42) { batch = Pick(rocks); scale = R(0.8f, 3.2f); at.y -= 0.35f; }
                    else if (roll < 0.46) { batch = Pick(logs); scale = R(1f, 2f); }
                    else if (roll < 0.49) { batch = Pick(boulders); scale = R(0.8f, 2.4f); at.y -= 0.4f; }
                    else if (roll < 0.58) { batch = Pick(moss); scale = R(3f, 7f); }
                    else continue;
                    float yaw = R(0f, 360f);
                    if (boles.Any(b => new Vector2(b.x - px, b.z - pz).magnitude < b.y + 0.5f) || NearHero(px, pz, 3.2f)) continue;
                    if (NatureDressing.TouchesAnArc(batch, at, yaw, scale)) continue;
                    if (HitsTheCourse(WorldBox(batch, at, yaw, scale))) continue;
                    bins.Add(batch, at, yaw, scale);
                    cover++;
                }
            bins.Pack();
            EditorUtility.SetDirty(set);
            var go = new GameObject("Foliage");
            go.transform.SetParent(forest, false);
            go.AddComponent<FoliageInstancer>().Configure(set);
            return $"{big} giants, {under} understory trees, {cover} plants and rocks ({bins.Count} instances)";
        }
    }
}
