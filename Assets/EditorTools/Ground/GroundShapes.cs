using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// What the land pieces do to the ground (ARCHITECTURE §27.4): each is a <see cref="Contribution"/> the height stack applies in the
    /// level's order (hierarchy order) over the author's sculpt. A hill or a hollow adds, a ridge adds along a line, a plateau and a trail set
    /// the ground toward a height by a weight, a river or a ravine carves its bed (never higher than the ground). Later tools add their
    /// own (<see cref="Extra"/>: a cliff's talus). A trail's profile and a river's bed are kept from the last composition
    /// (<see cref="Trails"/>, <see cref="Rivers"/>) for their outputs and the splat's bake.
    /// </summary>
    public static class GroundShapes
    {
        /// <summary>One piece's effect on the ground.</summary>
        public sealed class Contribution
        {
            public string Id, Signature;
            public Rect Bounds;
            /// <summary>Applies the piece to <paramref name="running"/> (world heights) over the cells of <paramref name="cells"/>.</summary>
            public Action<GroundField, RectInt> Apply;
        }

        /// <summary>A trail's last profile: its line (world) and how deep it cut or filled at worst.</summary>
        public sealed class TrailProfile
        {
            public Vector3[] line = Array.Empty<Vector3>();
            public float worstCut;
        }

        /// <summary>A river's last bed: its line (world, the bed's floor), the water's surface along it and the water's half width.</summary>
        public sealed class RiverBed
        {
            public Vector3[] bed = Array.Empty<Vector3>();
            public float[] surface = Array.Empty<float>();
            public float waterHalf, half;
        }

        public static readonly Dictionary<string, TrailProfile> Trails = new Dictionary<string, TrailProfile>();
        public static readonly Dictionary<string, RiverBed> Rivers = new Dictionary<string, RiverBed>();

        /// <summary>Contributions other tools add (in their scene).</summary>
        public static readonly List<Func<Scene, IEnumerable<Contribution>>> Extra = new List<Func<Scene, IEnumerable<Contribution>>>();

        /// <summary>The land pieces of <paramref name="scene"/>, in the order they apply (the hierarchy's), then the extra ones.</summary>
        public static List<Contribution> Of(Scene scene)
        {
            var list = new List<Contribution>();
            foreach (var piece in TimberLinks.Pieces(scene))
            {
                if (!piece.isActiveAndEnabled) continue;
                switch (piece)
                {
                    case LandShape land: list.Add(Land(land)); break;
                    case TrailShape trail: list.Add(Trail(trail)); break;
                    case RiverShape river: list.Add(River(river)); break;
                }
            }
            foreach (var extra in Extra) list.AddRange(extra(scene));
            return list.Where(c => c != null).ToList();
        }

        /// <summary>True for a piece whose effect is on the ground (rebuilt through the stack).</summary>
        public static bool IsLand(LevelPiece piece) => piece is LandShape || piece is TrailShape || piece is RiverShape;

        // ------------------------------------------------------------------ helpers

        static float Noise(Vector2 p, float scale, int seed) =>
            (Mathf.PerlinNoise(p.x / scale + seed * 0.731f + 13.7f, p.y / scale - seed * 0.413f + 41.3f) - 0.5f) * 2f;

        static Rect Box(IEnumerable<Vector2> pts, float margin)
        {
            var list = pts.ToList();
            if (list.Count == 0) return Rect.zero;
            return Rect.MinMaxRect(list.Min(p => p.x) - margin, list.Min(p => p.y) - margin, list.Max(p => p.x) + margin, list.Max(p => p.y) + margin);
        }

        /// <summary>The cells of <paramref name="field"/> inside both <paramref name="cells"/> and the world rectangle <paramref name="bounds"/>.</summary>
        public static IEnumerable<(int x, int z)> Cells(GroundField field, RectInt cells, Rect bounds)
        {
            var c0 = field.CellOf(bounds.min);
            var c1 = field.CellOf(bounds.max);
            int x0 = Mathf.Max(cells.xMin, Mathf.FloorToInt(c0.x)), x1 = Mathf.Min(cells.xMax - 1, Mathf.CeilToInt(c1.x));
            int z0 = Mathf.Max(cells.yMin, Mathf.FloorToInt(c0.y)), z1 = Mathf.Min(cells.yMax - 1, Mathf.CeilToInt(c1.y));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++) yield return (x, z);
        }

        /// <summary>The distance from <paramref name="p"/> to the polyline <paramref name="line"/>, and the index and fraction of the nearest segment.</summary>
        public static float DistanceToLine(IList<Vector2> line, Vector2 p, out int segment, out float t)
        {
            float best = float.MaxValue;
            segment = 0;
            t = 0f;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var ab = line[i + 1] - line[i];
                float u = Mathf.Clamp01(Vector2.Dot(p - line[i], ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float d = Vector2.Distance(p, line[i] + ab * u);
                if (d < best)
                {
                    best = d;
                    segment = i;
                    t = u;
                }
            }
            return best;
        }

        static float SignedDistance(IList<Vector2> poly, Vector2 p) => Poly2.Contains(poly, p) ? -Poly2.DistanceToEdge(poly, p) : Poly2.DistanceToEdge(poly, p);

        public static float Shape(LandProfile profile, float s)
        {
            s = Mathf.Clamp01(s);
            switch (profile)
            {
                case LandProfile.Steep: return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s * 1.8f));
                case LandProfile.Terraced:
                    const int steps = 3;
                    float f = s * steps;
                    float i = Mathf.Floor(f);
                    return Mathf.Min(1f, (i + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f - i - 0.35f) / 0.3f))) / steps);
                default: return Mathf.SmoothStep(0f, 1f, s);
            }
        }

        /// <summary>A line through <paramref name="pts"/> (smooth), every <paramref name="step"/> metres, on the flat.</summary>
        public static List<Vector2> Line(IList<Vector2> pts, float step)
        {
            if (pts.Count < 2) return pts.ToList();
            var spline = Curves.Spline(pts.Select(p => new Vector3(p.x, 0f, p.y)).ToList(), step * 0.5f);
            return Curves.Resample(spline, step).Select(p => new Vector2(p.x, p.z)).ToList();
        }

        static string Sig(LevelPiece piece) => LevelPieceBuilder.Signature(piece);

        // ------------------------------------------------------------------ land shapes

        static Contribution Land(LandShape shape)
        {
            var pts = shape.WorldPoints();
            if (pts.Count < (shape.Closed ? 3 : 2)) return null;
            float reach = shape.Falloff + shape.OutlineNoise + 1f;
            var c = new Contribution { Id = shape.PieceId, Signature = Sig(shape), Bounds = Box(pts, reach) };
            float area = shape.Closed ? Mathf.Abs(Poly2.SignedArea(pts)) : 0f;
            float inner = Mathf.Max(1f, Mathf.Sqrt(area / Mathf.PI));
            float top = shape.transform.position.y + shape.Height;
            int seed = shape.Seed;
            c.Apply = (running, cells) =>
            {
                foreach (var (x, z) in Cells(running, cells, c.Bounds))
                {
                    var p = running.World(x, z);
                    float n = Noise(p, shape.NoiseScale, seed), n2 = Noise(p, shape.NoiseScale * 0.37f, seed + 7);
                    float d = (shape.Closed ? SignedDistance(pts, p) : DistanceToLine(pts, p, out _, out _)) - n * shape.OutlineNoise;
                    switch (shape.Kind)
                    {
                        case LandKind.Hill:
                        case LandKind.Hollow:
                        {
                            float w = Shape(shape.Profile, (shape.Falloff - d) / (shape.Falloff + inner));
                            if (w <= 0f) break;
                            float h = shape.Kind == LandKind.Hollow ? -Mathf.Abs(shape.Height) : shape.Height;
                            running[x, z] += h * w + shape.SurfaceNoise * n2 * w;
                            break;
                        }
                        case LandKind.Ridge:
                        {
                            float w = Shape(shape.Profile, 1f - d / shape.Falloff);
                            if (w <= 0f) break;
                            running[x, z] += shape.Height * w + shape.SurfaceNoise * n2 * w;
                            break;
                        }
                        case LandKind.Plateau:
                        {
                            float w = d <= 0f ? 1f : Shape(shape.Profile, 1f - d / shape.Falloff);
                            if (w <= 0f) break;
                            running[x, z] = Mathf.Lerp(running[x, z], top + shape.SurfaceNoise * n2 * 0.3f, w);
                            break;
                        }
                    }
                }
            };
            return c;
        }

        // ------------------------------------------------------------------ trails

        static Contribution Trail(TrailShape trail)
        {
            var pts = trail.WorldPoints();
            if (pts.Count < 2) return null;
            float reach = trail.Width * 0.5f + trail.Bank + trail.EdgeNoise + 1f;
            var c = new Contribution { Id = trail.PieceId, Signature = Sig(trail), Bounds = Box(pts, reach) };
            int seed = trail.Seed;
            c.Apply = (running, cells) =>
            {
                var line = Line(pts, 0.5f);
                // the profile: the ground along the line, smoothed, then held to the grade (forward and back)
                var ground = line.Select(running.Sample).ToArray();
                var h = new float[line.Count];
                int r = 6;
                for (int i = 0; i < h.Length; i++)
                {
                    float s = 0f;
                    int n = 0;
                    for (int k = Mathf.Max(0, i - r); k <= Mathf.Min(h.Length - 1, i + r); k++) { s += ground[k]; n++; }
                    h[i] = s / n;
                }
                float g = trail.MaxGrade / 100f;
                for (int i = 1; i < h.Length; i++)
                {
                    float ds = Vector2.Distance(line[i], line[i - 1]) * g;
                    h[i] = Mathf.Clamp(h[i], h[i - 1] - ds, h[i - 1] + ds);
                }
                for (int i = h.Length - 2; i >= 0; i--)
                {
                    float ds = Vector2.Distance(line[i], line[i + 1]) * g;
                    h[i] = Mathf.Clamp(h[i], h[i + 1] - ds, h[i + 1] + ds);
                }
                Trails[trail.PieceId] = new TrailProfile
                {
                    line = line.Select((p, i) => new Vector3(p.x, h[i], p.y)).ToArray(),
                    worstCut = Enumerable.Range(0, h.Length).Select(i => Mathf.Abs(h[i] - ground[i])).DefaultIfEmpty(0f).Max(),
                };
                float half = trail.Width * 0.5f;
                foreach (var (x, z) in Cells(running, cells, c.Bounds))
                {
                    var p = running.World(x, z);
                    float d = DistanceToLine(line, p, out int seg, out float t);
                    float edge = half + Noise(p, 6f, seed) * trail.EdgeNoise;
                    float w = d <= edge ? 1f : trail.Bank <= 0f ? 0f : Mathf.SmoothStep(0f, 1f, 1f - (d - edge) / trail.Bank);
                    if (w <= 0f) continue;
                    float target = Mathf.Lerp(h[seg], h[Mathf.Min(seg + 1, h.Length - 1)], t);
                    running[x, z] = Mathf.Lerp(running[x, z], target, w * trail.Flatten);
                }
            };
            return c;
        }

        // ------------------------------------------------------------------ rivers and ravines

        static Contribution River(RiverShape river)
        {
            var pts = river.WorldPoints();
            if (pts.Count < 2) return null;
            float reach = river.Width * 0.5f + river.Bank + river.BankNoise + 1f;
            var c = new Contribution { Id = river.PieceId, Signature = Sig(river), Bounds = Box(pts, reach) };
            int seed = river.Seed;
            c.Apply = (running, cells) =>
            {
                var line = Line(pts, 0.5f);
                var ground = line.Select(running.Sample).ToArray();
                var bed = new float[line.Count];
                bed[0] = ground[0] - river.Depth;
                for (int i = 1; i < bed.Length; i++)
                    bed[i] = Mathf.Min(bed[i - 1] - Vector2.Distance(line[i], line[i - 1]) * river.Slope / 100f, ground[i] - river.Depth);
                float half = river.Width * 0.5f;
                float fill = Mathf.Clamp01((river.Depth - river.WaterDrop) / river.Depth);
                float waterHalf = river.Water ? half * (river.Profile == RiverProfile.U ? Mathf.Pow(fill, 0.25f) : fill) : 0f;
                Rivers[river.PieceId] = new RiverBed
                {
                    bed = line.Select((p, i) => new Vector3(p.x, bed[i], p.y)).ToArray(),
                    surface = bed.Select(b => b + river.Depth - river.WaterDrop).ToArray(),
                    waterHalf = waterHalf,
                    half = half,
                };
                foreach (var (x, z) in Cells(running, cells, c.Bounds))
                {
                    var p = running.World(x, z);
                    float d = DistanceToLine(line, p, out int seg, out float t);
                    float edge = half + Noise(p, 9f, seed) * river.BankNoise;
                    float b = Mathf.Lerp(bed[seg], bed[Mathf.Min(seg + 1, bed.Length - 1)], t);
                    float carve;
                    if (d <= edge)
                    {
                        float u = Mathf.Clamp01(d / Mathf.Max(0.1f, edge));
                        carve = b + river.Depth * (river.Profile == RiverProfile.U ? u * u * u * u : u);
                    }
                    else if (river.Bank > 0f && d <= edge + river.Bank)
                        carve = Mathf.Lerp(b + river.Depth, running[x, z], Mathf.SmoothStep(0f, 1f, (d - edge) / river.Bank));
                    else continue;
                    running[x, z] = Mathf.Min(running[x, z], carve);
                }
            };
            return c;
        }
    }
}
