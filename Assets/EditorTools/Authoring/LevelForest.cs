using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// Plants an authored level's <see cref="ForestArea"/> (ARCHITECTURE §27), the way PatataRanger's forest is planted: giants in groves
    /// with clearings between them (a slow noise decides where), an understory, and ground cover; drawn instanced (an in-memory
    /// <see cref="FoliageSet"/> on a <see cref="FoliageInstancer"/>, written beside the piece's meshes on save), nothing collides.
    /// Nothing grows near a walkway, a bridge or a deck (the area's path clearance), inside anything of the level (timber, a tree, the
    /// kit), on a pass arc, on rock too steep to hold it, or too near a giant tree. Deterministic (the area's seed).
    /// </summary>
    public static class LevelForest
    {
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

        /// <summary>True when anything of the level (timber, a tree, a solid, a zone; not the ground, not a fall) lies inside the box.</summary>
        public static bool HitsTheLevel(Bounds b)
        {
            foreach (var c in Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
            {
                if (c is TerrainCollider || c.GetComponentInParent<KillZone>() != null) continue;
                var proxy = c.GetComponent<WalkableProxy>();
                if (proxy != null && proxy.ProxyKind == WalkableProxy.Kind.Terrain) continue;
                return true;
            }
            return false;
        }

        /// <summary>The level's walked lines and edges in plan (walkways, bridges, deck and house outlines), as segments.</summary>
        public static List<(Vector2 a, Vector2 b)> Paths(Scene scene)
        {
            var segments = new List<(Vector2, Vector2)>();
            void Line(IList<Vector3> pts, bool closed)
            {
                for (int i = 0; i + 1 < pts.Count; i++) segments.Add((new Vector2(pts[i].x, pts[i].z), new Vector2(pts[i + 1].x, pts[i + 1].z)));
                if (closed && pts.Count > 2) segments.Add((new Vector2(pts[pts.Count - 1].x, pts[pts.Count - 1].z), new Vector2(pts[0].x, pts[0].z)));
            }
            foreach (var piece in TimberLinks.Pieces(scene))
                switch (piece)
                {
                    case TimberWalkShape walk: Line(walk.WorldControl(), false); break;
                    case RopeBridgeShape bridge: var (a, b) = bridge.WorldEnds(); Line(new[] { a, b }, false); break;
                    case TimberDeckShape deck: Line(deck.WorldOutline().Select(p => new Vector3(p.x, 0f, p.y)).ToList(), true); break;
                    case RailingShape railing: Line(railing.WorldPoints(), false); break;
                    case TrailShape trail: Line(GroundOutputs.TrailLine(trail), false); break;
                    case HouseShape house:
                        var t = house.transform;
                        var turn = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
                        float hx = house.Size.x * 0.5f + 1f, hz = house.Size.y * 0.5f + 1f;
                        Line(new[] { t.position + turn * new Vector3(-hx, 0, -hz), t.position + turn * new Vector3(hx, 0, -hz), t.position + turn * new Vector3(hx, 0, hz),
                                     t.position + turn * new Vector3(-hx, 0, hz) }, true);
                        break;
                }
            return segments;
        }

        /// <summary>The level's water surfaces in plan (each as its centre, half size and turn), with a metre of bank.</summary>
        public static List<(Vector2 center, Vector2 half, float yaw)> Waters(Scene scene)
        {
            var list = TimberLinks.Pieces(scene).OfType<WaterShape>()
                .Select(w => (new Vector2(w.transform.position.x, w.transform.position.z), w.Size * 0.5f + Vector2.one, w.transform.eulerAngles.y)).ToList();
            // a river's water: a box along each stretch of its bed (with a metre of bank)
            foreach (var river in TimberLinks.Pieces(scene).OfType<RiverShape>().Where(r => r.Water))
            {
                if (!GroundShapes.Rivers.TryGetValue(river.PieceId ?? "", out var bed)) continue;
                for (int i = 0; i + 4 < bed.bed.Length; i += 4)
                {
                    Vector2 a = new Vector2(bed.bed[i].x, bed.bed[i].z), b = new Vector2(bed.bed[i + 4].x, bed.bed[i + 4].z);
                    var d = b - a;
                    list.Add(((a + b) * 0.5f, new Vector2(bed.waterHalf + 1f, d.magnitude * 0.5f + 0.5f), Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg));
                }
            }
            return list;
        }

        /// <summary>True on water (or its bank).</summary>
        public static bool OnWater(List<(Vector2 center, Vector2 half, float yaw)> waters, Vector2 q)
        {
            foreach (var (center, half, yaw) in waters)
            {
                var local = Quaternion.Euler(0f, -yaw, 0f) * new Vector3(q.x - center.x, 0f, q.y - center.y);
                if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.y) return true;
            }
            return false;
        }

        public static float Distance(List<(Vector2 a, Vector2 b)> segments, Vector2 q)
        {
            float best = float.MaxValue;
            foreach (var (a, b) in segments)
            {
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
            }
            return best;
        }

        static float Noise(float x, float z, float scale, int seed) => NatureShapes.Noise(new Vector3(x * scale, 0.37f, z * scale), seed);

        /// <summary>Plants <paramref name="area"/> under <paramref name="root"/>; returns how many instances.</summary>
        public static int Plant(ForestArea area, Transform root)
        {
            var scene = area.gameObject.scene;
            var poly = area.WorldOutline();
            if (poly.Count < 3) return 0;
            var set = ScriptableObject.CreateInstance<FoliageSet>();
            set.name = area.name + " foliage";
            var bins = new Bins();
            NatureDressing.IndexArcs(NatureDressing.PassSamples());
            var rng = new Random(TimberKit.Seed(area.name, area.Seed));
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            T Pick<T>(T[] options) => options[rng.Next(options.Length)];
            int seed = TimberKit.Seed(area.name, area.Seed + 1) & 0xffff;

            // ---- batches (the same kinds as PatataRanger's forest)
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

            // ---- where things may grow
            var bounds = new Rect(poly.Min(p => p.x), poly.Min(p => p.y), 0f, 0f);
            bounds.xMax = poly.Max(p => p.x);
            bounds.yMax = poly.Max(p => p.y);
            var paths = Paths(scene).Where(s => Near(bounds, s, 40f)).ToList();
            var heroes = TimberLinks.Pieces(scene).OfType<HeroTreeShape>().Select(h => new Vector2(h.transform.position.x, h.transform.position.z)).ToList();
            bool NearHero(float x, float z, float r) => heroes.Any(h => Vector2.Distance(h, new Vector2(x, z)) < r);
            float Ground(float x, float z) => LevelNature.GroundAt(scene, new Vector2(x, z));
            float SlopeAt(float x, float z)
            {
                float dx = Ground(x + 0.7f, z) - Ground(x - 0.7f, z), dz = Ground(x, z + 0.7f) - Ground(x, z - 0.7f);
                return new Vector2(dx, dz).magnitude / 1.4f;
            }
            float shift = (area.Clearings - 0.3f) * 0.5f;
            float Grove(float x, float z) => Noise(x, z, 0.016f, seed) * 0.75f + Noise(x, z, 0.05f, seed + 1) * 0.25f - shift;
            bool Inside(float x, float z) => Poly2.Contains(poly, new Vector2(x, z));
            var waters = Waters(scene);
            float PathDistance(float x, float z) => OnWater(waters, new Vector2(x, z)) ? 0f : paths.Count == 0 ? float.MaxValue : Distance(paths, new Vector2(x, z));

            // ---- giants: in groves, kept off the paths' air and the giant trees
            var boles = new List<Vector3>();
            float giantRate = area.Walked ? 0f : area.Giants * 2f;
            for (float x = bounds.xMin + 6f; x < bounds.xMax - 2f; x += 12f)
                for (float z = bounds.yMin + 6f; z < bounds.yMax - 2f; z += 12f)
                {
                    float px = x + R(-5f, 5f), pz = z + R(-5f, 5f);
                    float g = Grove(px, pz);
                    if (rng.NextDouble() > Mathf.InverseLerp(0.12f, 0.48f, g) * giantRate) continue;
                    if (!Inside(px, pz) || SlopeAt(px, pz) > 0.85f || NearHero(px, pz, 14f) || PathDistance(px, pz) < area.PathClearance + 6f) continue;
                    var parts = giants[rng.Next(giants.Count)];
                    float yaw = R(0f, 360f), scale = R(0.5f, 0.78f);
                    var at = new Vector3(px, Ground(px, pz) - 0.6f, pz);
                    var boleBox = WorldBox(parts[1], at, yaw, scale);
                    float boleR = Mathf.Max(boleBox.extents.x, boleBox.extents.z);
                    if (boles.Any(b => new Vector2(b.x - px, b.z - pz).magnitude < boleR + b.y + 2.5f)) continue;
                    if (parts.Any(part => NatureDressing.TouchesAnArc(part, at, yaw, scale))) continue;
                    if (HitsTheLevel(boleBox) || HitsTheLevel(WorldBox(parts[2], at, yaw, scale))) continue;
                    foreach (var part in parts) bins.Add(part, at, yaw, scale);
                    boles.Add(new Vector3(px, boleR, pz));
                }

            // ---- understory: younger trees, more of them in the groves
            float underRate = area.Walked ? 0f : area.Understory / 0.6f;
            for (float x = bounds.xMin + 2f; x < bounds.xMax; x += 5f)
                for (float z = bounds.yMin + 2f; z < bounds.yMax; z += 5f)
                {
                    float px = x + R(-2.2f, 2.2f), pz = z + R(-2.2f, 2.2f);
                    float g = Grove(px, pz);
                    if (rng.NextDouble() > Mathf.Lerp(0.35f, 0.92f, Mathf.InverseLerp(0.2f, 0.6f, g)) * underRate) continue;
                    if (!Inside(px, pz) || SlopeAt(px, pz) > 0.9f || NearHero(px, pz, 5f) || PathDistance(px, pz) < area.PathClearance) continue;
                    if (boles.Any(b => new Vector2(b.x - px, b.z - pz).magnitude < b.y + 1.5f)) continue;
                    var batch = Pick(understory[rng.NextDouble() < 0.1 ? 3 : rng.Next(3)]);
                    float yaw = R(0f, 360f), scale = R(1.1f, 2.2f);
                    var at = new Vector3(px, Ground(px, pz) - 0.3f, pz);
                    if (NatureDressing.TouchesAnArc(batch, at, yaw, scale) || HitsTheLevel(WorldBox(batch, at, yaw, scale))) continue;
                    bins.Add(batch, at, yaw, scale);
                }

            // ---- the ground: ferns and grass, bushes, rocks half sunk, logs, stumps, moss
            float coverRate = area.GroundCover / 0.7f;
            for (float x = bounds.xMin + 1f; x < bounds.xMax; x += 2.4f)
                for (float z = bounds.yMin + 1f; z < bounds.yMax; z += 2.4f)
                {
                    float px = x + R(-1.4f, 1.4f), pz = z + R(-1.4f, 1.4f);
                    if (!Inside(px, pz) || PathDistance(px, pz) < 1.6f) continue;
                    float slope = SlopeAt(px, pz);
                    double roll = rng.NextDouble() / Mathf.Max(0.05f, coverRate);
                    FoliageSet.Batch batch;
                    float scale;
                    var at = new Vector3(px, Ground(px, pz), pz);
                    if (area.Walked)
                    {
                        // the way: only what a player walks through, kept low
                        if (slope > 0.75f || roll >= 0.58) continue;
                        if (roll < 0.3) { batch = Pick(ferns); scale = R(1.3f, 2.6f); }
                        else if (roll < 0.42) { batch = Pick(grass); scale = R(1.2f, 2.2f); }
                        else if (roll < 0.47) { batch = Pick(bushes); scale = R(0.7f, 1.2f); }
                        else { batch = Pick(moss); scale = R(3f, 7f); }
                    }
                    else if (slope > 0.75f)
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
                    if (NatureDressing.TouchesAnArc(batch, at, yaw, scale) || HitsTheLevel(WorldBox(batch, at, yaw, scale))) continue;
                    bins.Add(batch, at, yaw, scale);
                }
            bins.Pack();
            // empty batches are left out of the set
            set.batches.RemoveAll(b => b.cells.Count == 0);
            var go = new GameObject("Foliage");
            go.transform.SetParent(root, false);
            go.AddComponent<FoliageInstancer>().Configure(set);
            return bins.Count;
        }

        static bool Near(Rect r, (Vector2 a, Vector2 b) s, float margin)
        {
            var grown = new Rect(r.xMin - margin, r.yMin - margin, r.width + 2f * margin, r.height + 2f * margin);
            return grown.Contains(s.a) || grown.Contains(s.b) || grown.Contains((s.a + s.b) * 0.5f);
        }
    }
}
