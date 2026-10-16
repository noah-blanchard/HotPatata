using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// The organic timber kit (ARCHITECTURE §25.5): decks that hug their trunks, walkways that turn into stairs where they climb,
    /// sagging rope bridges and railings, built from real boards, joists, bearers, posts, knee braces, iron bands and rope, each
    /// drawn piece paired with a closed, smoothed collider (<see cref="WalkableProxy"/>) the player walks on. Everything is placed in
    /// world space under a parent and its meshes go to the folder opened with <see cref="OrganicAssets.Begin"/>.
    /// <para>
    /// The kit is level-design geometry, not a gameplay system: no rules, no state. Railings collide with box colliders (a rail
    /// is a wall for the body and the bomb), like the rest of the kit.
    /// </para>
    /// </summary>
    public static class TimberKit
    {
        // material slots of every timber mesh
        public const int Plank = 0, PlankAged = 1, Timber = 2, Iron = 3, Rope = 4, Slots = 5;

        public const string PlankMaterial = "Nature_DeckPlank", PlankAgedMaterial = "Nature_DeckPlankAged", TimberMaterial = "Nature_Timber",
            IronMaterial = "Nature_Iron", RopeMaterial = "Nature_Rope";

        public static readonly string[] SlotMaterials = { PlankMaterial, PlankAgedMaterial, TimberMaterial, IronMaterial, RopeMaterial };

        public const float BoardThickness = 0.05f;   // a deck board
        public const float ProxyDepth = 0.4f;        // a walking proxy's slab under its surface
        public const float RailHeight = 1.05f;       // a handrail's top above the walking surface
        public const float MaxFlightSlope = 35f;     // a stair flight's pitch at most (the controller climbs 50°)

        /// <summary>A trunk the timber leans on: its axis at ground (x, z) and its radius at deck heights.</summary>
        public struct Trunk
        {
            public Vector2 center;
            public float radius;

            public Trunk(Vector2 center, float radius)
            {
                this.center = center;
                this.radius = radius;
            }

            public Trunk(Vector3 center, float radius) : this(new Vector2(center.x, center.z), radius) { }
        }

        /// <summary>Where a piece finds the ground, the trunks that hold it and the rope anchors above it.</summary>
        public struct Site
        {
            /// <summary>The ground's height at (x, z), or NaN where there is none (a gorge, the void).</summary>
            public Func<Vector2, float> ground;
            public IList<Trunk> trunks;
            /// <summary>Limbs or beams above the piece a hanger can be tied to (world points).</summary>
            public IList<Vector3> anchors;
            /// <summary>Posts taller than this are not built (a brace or a hanger holds the piece instead).</summary>
            public float maxPost;

            public float GroundAt(Vector2 p) => ground == null ? float.NaN : ground(p);
        }

        public struct Opening
        {
            public Vector2 at;
            public float width;

            public Opening(Vector2 at, float width)
            {
                this.at = at;
                this.width = width;
            }

            public Opening(Vector3 at, float width) : this(new Vector2(at.x, at.z), width) { }
        }

        /// <summary>The walking surface the last <see cref="Walkway"/> laid (its flights straightened): what a walker treads.</summary>
        public static List<Vector3> LastWalkSurface { get; private set; } = new List<Vector3>();

        public static Material[] Materials(int[] slots) => slots.Select(s => NatureMaterialBuilder.Load(SlotMaterials[s])).ToArray();

        /// <summary>A mesh for drawn timber, every material slot, telling the <see cref="ElementRecorder"/> what it lays when one listens.</summary>
        public static MeshBuilder Visual() => new MeshBuilder(Slots) { Recorder = ElementRecorder.Active };

        // ------------------------------------------------------------------ deck

        public struct DeckSpec
        {
            public float grain;            // the boards' direction, degrees from +Z toward +X
            public float boardWidth;       // 0.2 by default
            public bool rails;
            public IList<Opening> openings;   // where the rail leaves the edge open (stairs, bridges, gaps)
            public Func<Vector2, bool> railSkip;   // edge points that get no rail (a ring deck's seam)
            public bool held;                      // something else holds it (a house's walls): no posts, braces or hangers
            public int seed;
            public TimberSupport support;          // Auto: the kit chooses (HoldDeck); else that support, where it can stand
            public float agedShare;                // the share of aged boards (0: the kit's own, 28 %)
        }

        /// <summary>
        /// A deck of boards on joists and bearers inside <paramref name="outline"/> (x, z, any orientation), its walking surface at
        /// the height <paramref name="top"/> gives (flat, tilted or gently warped). Bearers rest on posts down to the ground, on knee
        /// braces to a nearby trunk (with an iron band round it) or on hangers from anchors above. The edge gets a rim board and,
        /// when asked, a railing broken at the openings and wherever a trunk stands at the edge.
        /// </summary>
        public static GameObject Deck(Transform parent, string name, IList<Vector2> outline, Func<Vector2, float> top, DeckSpec spec, Site site)
        {
            var poly = Poly2.CounterClockwise(outline);
            var rng = new Random(Seed(name, spec.seed));
            var vis = Visual();
            float w = spec.boardWidth > 0f ? spec.boardWidth : 0.2f;
            float g = spec.grain * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Sin(g), Mathf.Cos(g));
            var across = new Vector2(along.y, -along.x);
            var origin = poly.Aggregate(Vector2.zero, (s, p) => s + p) / poly.Count;

            // boards
            var boards = ElementRecorder.Tag("Boards", TimberElementKind.Plank, TimberWalk.Slab);
            float aMin = poly.Min(p => Vector2.Dot(p - origin, across)), aMax = poly.Max(p => Vector2.Dot(p - origin, across));
            int row = 0;
            for (float c = aMin + w * 0.5f; c < aMax - w * 0.3f; c += w + 0.012f, row++)
            {
                var lineOrigin = origin + across * c;
                foreach (var (t0, t1) in BoardSpans(poly, lineOrigin, along, w))
                {
                    float t = t0 + 0.02f;
                    float end = t1 - 0.02f;
                    bool first = true;   // the first board of a row is cut short at random: the joints stagger
                    while (end - t > 0.15f)
                    {
                        float len = Mathf.Min(end - t, first ? R(rng, 0.6f, 3.2f) : R(rng, 2.2f, 4f));
                        first = false;
                        if (end - (t + len) < 0.5f) len = end - t;
                        var p0 = lineOrigin + along * t;
                        var p1 = lineOrigin + along * (t + len - 0.008f);
                        var a = new Vector3(p0.x, top(p0) - BoardThickness * 0.5f + R(rng, -0.003f, 0.003f), p0.y);
                        var b = new Vector3(p1.x, top(p1) - BoardThickness * 0.5f + R(rng, -0.003f, 0.003f), p1.y);
                        vis.Beam(a, b, w - 0.01f, BoardThickness, rng.NextDouble() < Share(spec.agedShare, 0.28) ? PlankAged : Plank, new Vector2(R(rng, 0f, 3f), R(rng, 0f, 3f)));
                        t += len;
                    }
                }
            }
            boards.Dispose();

            // rim board round the edge, under the boards' ends
            using var frame = ElementRecorder.Tag("Frame", TimberElementKind.Trim);
            var rimPath = Poly2.Offset(poly, -0.04f).Select(p => new Vector3(p.x, top(p) - BoardThickness, p.y)).ToList();
            rimPath.Add(rimPath[0]);
            vis.Sweep(rimPath, 0.06f, 0.26f, Timber, MeshBuilder.Anchor.Top);

            // joists across the boards, bearers along them
            using var beams = ElementRecorder.Tag(kind: TimberElementKind.Beam);
            float joistY = -BoardThickness, bearerY = joistY - 0.2f;
            var inner = Poly2.Offset(poly, -0.12f);
            float dMin = poly.Min(p => Vector2.Dot(p - origin, along)), dMax = poly.Max(p => Vector2.Dot(p - origin, along));
            for (float d = dMin + 0.3f; d < dMax - 0.2f; d += 0.62f)
                foreach (var (t0, t1) in Poly2.Scan(inner, origin + along * d, across))
                {
                    var p0 = origin + along * d + across * t0;
                    var p1 = origin + along * d + across * t1;
                    vis.Beam(new Vector3(p0.x, top(p0) + joistY - 0.09f, p0.y), new Vector3(p1.x, top(p1) + joistY - 0.09f, p1.y), 0.06f, 0.18f, Timber);
                }
            var bearerArea = Poly2.Offset(poly, -0.45f);
            var posts = new List<Vector3>();
            float span = aMax - aMin;
            int bearers = Mathf.Max(1, Mathf.CeilToInt((span - 0.9f) / 2.2f));
            for (int k = 0; k < bearers; k++)
            {
                float c = bearers == 1 ? (aMin + aMax) * 0.5f : Mathf.Lerp(aMin + 0.7f, aMax - 0.7f, k / (float)(bearers - 1));
                foreach (var (t0, t1) in Poly2.Scan(bearerArea, origin + across * c, along))
                {
                    if (t1 - t0 < 0.6f) continue;
                    var p0 = origin + across * c + along * t0;
                    var p1 = origin + across * c + along * t1;
                    vis.Beam(new Vector3(p0.x, top(p0) + bearerY - 0.11f, p0.y), new Vector3(p1.x, top(p1) + bearerY - 0.11f, p1.y), 0.14f, 0.22f, Timber);
                    int n = Mathf.Max(1, Mathf.CeilToInt((t1 - t0) / 3f));
                    for (int i = 0; i <= n; i++)
                    {
                        var q = Vector2.Lerp(p0, p1, i / (float)n);
                        posts.Add(new Vector3(q.x, top(q) + bearerY - 0.22f, q.y));
                    }
                }
            }
            if (!spec.held)
                using (ElementRecorder.Tag("Supports", TimberElementKind.Post))
                    HoldDeck(vis, poly, top, posts, site, rng, spec.support);

            var root = Finish(parent, name, vis, ProxySlab(poly, top), WalkableProxy.Kind.Deck, 0.08f);
            if (spec.rails) RailOutline(root.transform, name, poly, top, spec.openings, site.trunks, spec.railSkip);
            return root;
        }

        /// <summary>The parts of a board's line inside the deck, the board's two edges both inside too.</summary>
        static IEnumerable<(float, float)> BoardSpans(IList<Vector2> poly, Vector2 lineOrigin, Vector2 along, float width)
        {
            var side = new Vector2(along.y, -along.x) * (width * 0.45f);
            var left = Poly2.Scan(poly, lineOrigin + side, along);
            var right = Poly2.Scan(poly, lineOrigin - side, along);
            foreach (var (c0, c1) in Poly2.Scan(poly, lineOrigin, along))
            {
                float t0 = c0, t1 = c1;
                foreach (var edge in new[] { left, right })
                {
                    var overlap = edge.Where(s => s.t1 > t0 && s.t0 < t1).ToList();
                    if (overlap.Count == 0) { t1 = t0; break; }
                    t0 = Mathf.Max(t0, overlap.Min(s => s.t0));
                    t1 = Mathf.Min(t1, overlap.Max(s => s.t1));
                }
                if (t1 - t0 > 0.3f) yield return (t0, t1);
            }
        }

        /// <summary>A closed slab under a walking surface: the outline at the surface's height, <see cref="ProxyDepth"/> deep.</summary>
        static MeshBuilder ProxySlab(IList<Vector2> outline, Func<Vector2, float> top)
        {
            var proxy = new MeshBuilder();
            proxy.Prism(Poly2.Simplify(outline, 0.01f), top, p => top(p) - ProxyDepth, 0, 0);
            return proxy;
        }

        static float MaxPost(Site site) => site.maxPost > 0f ? site.maxPost : 4f;

        /// <summary>A share of aged boards: the one asked for, else the kit's own.</summary>
        static double Share(float asked, double own) => asked > 0f ? asked : own;

        /// <summary>
        /// The points a hanger was asked for and found no anchor above (no limb, no beam within reach): nothing holds the piece there.
        /// Cleared by <see cref="ClearReport"/>; the level tools report them.
        /// </summary>
        public static readonly List<Vector3> Unheld = new List<Vector3>();

        public static void ClearReport() => Unheld.Clear();

        /// <summary>
        /// Holds a deck up the way a builder would: short posts under a low deck; knee braces to the trunk it hugs (an iron band and a
        /// timber collar where they bear), hangers to the limbs above for the rim beyond the braces' reach; else one splayed, braced tower
        /// under its middle; else hangers. Never a post under every bearer of a high deck.
        /// </summary>
        static void HoldDeck(MeshBuilder vis, IList<Vector2> poly, Func<Vector2, float> top, IList<Vector3> posts, Site site, Random rng,
                             TimberSupport support = TimberSupport.Auto)
        {
            if (support == TimberSupport.None) return;
            var centroid = poly.Aggregate(Vector2.zero, (sum, q) => sum + q) / poly.Count;
            float y = top(centroid);
            float ground = site.GroundAt(centroid);
            float drop = float.IsNaN(ground) ? float.MaxValue : y - ground;
            bool auto = support == TimberSupport.Auto;
            if ((auto && drop <= 3.8f) || support == TimberSupport.Posts)
            {
                foreach (var post in posts)
                {
                    float g = site.GroundAt(new Vector2(post.x, post.z));
                    if (float.IsNaN(g) || post.y - g < 0.15f) continue;
                    vis.Beam(new Vector3(post.x, g - 0.35f, post.z), post, 0.2f, 0.2f, Timber, new Vector2(R(rng, 0f, 2f), 0f));
                }
                return;
            }
            // a trunk the deck hugs: knee braces from its rim to the trunk
            int hug = -1;
            float hugGap = float.MaxValue;
            if (site.trunks != null)
                for (int i = 0; i < site.trunks.Count; i++)
                {
                    float gap = poly.Min(q => Vector2.Distance(q, site.trunks[i].center)) - site.trunks[i].radius;
                    if (gap < hugGap) { hugGap = gap; hug = i; }
                }
            float underside = y - BoardThickness - 0.5f;
            if (hug >= 0 && hugGap < 4f && (auto || support == TimberSupport.Braces))
            {
                var t = site.trunks[hug];
                Band(vis, t, underside + 0.25f);
                var rim = Poly2.Offset(poly, -0.35f);
                float perimeter = Poly2.Perimeter(rim);
                int count = Mathf.Max(3, Mathf.RoundToInt(perimeter / 2.6f));
                var low = new List<float>();
                for (int k = 0; k < count; k++)
                {
                    var q = At(rim, perimeter * k / count);
                    float gap = Vector2.Distance(q, t.center) - t.radius;
                    var point = new Vector3(q.x, underside, q.y);
                    if (gap < 1.2f) continue;
                    if (gap <= 7.5f)
                    {
                        float d = Mathf.Clamp(gap * 0.85f, 1.2f, 5.5f);
                        var onTrunk = t.center + (q - t.center).normalized * (t.radius + 0.04f);
                        using var brace = ElementRecorder.Tag(kind: TimberElementKind.Brace);
                        vis.Beam(new Vector3(onTrunk.x, underside - d, onTrunk.y), point, 0.16f, 0.18f, Timber);
                        low.Add(underside - d);
                    }
                    else Hang(vis, point, site);
                }
                if (low.Count > 0) Band(vis, t, low.Average(), 0.12f, false);
                return;
            }
            if ((auto && drop <= MaxPost(site)) || (support == TimberSupport.Tower && drop < float.MaxValue))
            {
                // one tower under the middle: four splayed legs, girts, cross braces
                var b = new Bounds(new Vector3(centroid.x, 0f, centroid.y), Vector3.zero);
                foreach (var q in poly) b.Encapsulate(new Vector3(q.x, 0f, q.y));
                float hx = Mathf.Clamp(b.extents.x * 0.45f, 0.9f, 2.2f), hz = Mathf.Clamp(b.extents.z * 0.45f, 0.9f, 2.2f);
                Tower(vis, new Vector3(centroid.x, underside, centroid.y), hx, hz, site);
                return;
            }
            float around = Poly2.Perimeter(poly);
            foreach (var f in new[] { 0.15f, 0.4f, 0.65f, 0.9f })
            {
                var q = At(poly, around * f);
                Hang(vis, new Vector3(q.x, underside, q.y), site);
            }
        }

        static Vector2 At(IList<Vector2> ring, float distance)
        {
            for (int i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                float l = Vector2.Distance(a, b);
                if (distance <= l) return Vector2.Lerp(a, b, l < 1e-5f ? 0f : distance / l);
                distance -= l;
            }
            return ring[0];
        }

        /// <summary>A trestle bent under a walkway: two splayed legs from the ground to its stringers, a cap, girts and cross braces.</summary>
        static void Bent(MeshBuilder vis, Vector3 left, Vector3 right, Site site)
        {
            float gl = site.GroundAt(new Vector2(left.x, left.z)), gr = site.GroundAt(new Vector2(right.x, right.z));
            var side = right - left;
            side.y = 0f;
            side.Normalize();
            float drop = Mathf.Max(left.y - gl, right.y - gr);
            float splay = 0.07f * drop;
            var footL = new Vector3(left.x, gl - 0.35f, left.z) - side * splay;
            var footR = new Vector3(right.x, gr - 0.35f, right.z) + side * splay;
            using (ElementRecorder.Tag(kind: TimberElementKind.Post))
            {
                vis.Beam(footL, left, 0.22f, 0.22f, Timber);
                vis.Beam(footR, right, 0.22f, 0.22f, Timber);
            }
            using var braces = ElementRecorder.Tag(kind: TimberElementKind.Brace);
            vis.Beam(left - side * 0.35f + Vector3.down * 0.1f, right + side * 0.35f + Vector3.down * 0.1f, 0.2f, 0.24f, Timber);
            Vector3 L(float y) => Vector3.Lerp(footL, left, Mathf.InverseLerp(footL.y, left.y, y));
            Vector3 Rr(float y) => Vector3.Lerp(footR, right, Mathf.InverseLerp(footR.y, right.y, y));
            float prev = Mathf.Max(footL.y, footR.y) + 0.8f;
            for (float yy = prev + 3f; yy < Mathf.Min(left.y, right.y) - 0.6f; yy += 3f)
            {
                vis.Beam(L(yy), Rr(yy), 0.12f, 0.18f, Timber);
                vis.Beam(L(prev), Rr(yy), 0.1f, 0.14f, Timber);
                prev = yy;
            }
            vis.Beam(L(prev), Rr(Mathf.Min(left.y, right.y) - 0.15f), 0.1f, 0.14f, Timber);
        }

        /// <summary>A four-legged tower under a deck: splayed legs from the ground to its bearers, girts every 3 m, cross braces each face.</summary>
        public static void Tower(MeshBuilder vis, Vector3 topCenter, float hx, float hz, Site site)
        {
            var heads = new Vector3[4];
            var feet = new Vector3[4];
            var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
            for (int i = 0; i < 4; i++)
            {
                heads[i] = topCenter + new Vector3(corners[i].x * hx, 0f, corners[i].y * hz);
                float drop = topCenter.y - site.GroundAt(new Vector2(heads[i].x, heads[i].z));
                var f = heads[i] + new Vector3(corners[i].x, 0f, corners[i].y) * (0.06f * drop);
                feet[i] = new Vector3(f.x, site.GroundAt(new Vector2(f.x, f.z)) - 0.35f, f.z);
                using (ElementRecorder.Tag(kind: TimberElementKind.Post))
                    vis.Beam(feet[i], heads[i], 0.26f, 0.26f, Timber);
            }
            using var girts = ElementRecorder.Tag(kind: TimberElementKind.Brace);
            Vector3 Leg(int i, float y) => Vector3.Lerp(feet[i], heads[i], Mathf.InverseLerp(feet[i].y, heads[i].y, y));
            float bottom = feet.Max(f => f.y) + 0.8f;
            float prev = bottom;
            for (float yy = bottom + 3f; yy < topCenter.y - 0.3f; yy += 3f)
            {
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    vis.Beam(Leg(i, yy), Leg(j, yy), 0.12f, 0.18f, Timber);
                    vis.Beam(Leg(i, prev), Leg(j, yy), 0.1f, 0.14f, Timber);
                }
                prev = yy;
            }
            for (int i = 0; i < 4; i++) vis.Beam(Leg(i, topCenter.y - 0.1f), Leg((i + 1) % 4, topCenter.y - 0.1f), 0.18f, 0.22f, Timber);
        }

        /// <summary>A knee brace from <paramref name="point"/> to the nearest trunk within <paramref name="reach"/> (false if none).</summary>
        static bool BraceToTrunk(MeshBuilder vis, Vector3 point, Site site, float reach)
        {
            if (site.trunks == null || site.trunks.Count == 0) return false;
            var q = new Vector2(point.x, point.z);
            var t = site.trunks.OrderBy(k => Vector2.Distance(k.center, q) - k.radius).First();
            float gap = Vector2.Distance(t.center, q) - t.radius;
            if (gap > reach) return false;
            float d = Mathf.Clamp(gap * 0.85f, 1.2f, 5.5f);
            var on = t.center + (q - t.center).normalized * (t.radius + 0.04f);
            using var brace = ElementRecorder.Tag(kind: TimberElementKind.Brace);
            vis.Beam(new Vector3(on.x, point.y - d, on.y), point, 0.16f, 0.18f, Timber);
            return true;
        }

        /// <summary>A hanger rope from <paramref name="point"/> up to the nearest anchor above it (a limb, a beam), its iron stirrup.</summary>
        static void Hang(MeshBuilder vis, Vector3 point, Site site)
        {
            if (site.anchors == null || site.anchors.Count == 0) { Unheld.Add(point); return; }
            var q = new Vector2(point.x, point.z);
            var above = site.anchors.Where(a => a.y > point.y + 2f).ToList();
            if (above.Count == 0) { Unheld.Add(point); return; }
            var best = above.OrderBy(a => Vector2.Distance(new Vector2(a.x, a.z), q)).First();
            if (Vector2.Distance(new Vector2(best.x, best.z), q) > 22f) { Unheld.Add(point); return; }
            using var hanger = ElementRecorder.Tag(kind: TimberElementKind.Rope);
            vis.Tube(new[] { point, best }, new[] { 0.028f, 0.028f }, 6, Rope);
            using var stirrup = ElementRecorder.Tag(kind: TimberElementKind.Iron);
            vis.Beam(point + Vector3.down * 0.05f, point + Vector3.up * 0.25f, 0.06f, 0.06f, Iron);
        }

        /// <summary>An iron band round a trunk at <paramref name="y"/> (where a deck's beams are bolted to it).</summary>
        static void Band(MeshBuilder vis, Trunk t, float y, float height = 0.16f, bool collar = true)
        {
            var ring = new List<Vector3>();
            for (int i = 0; i <= 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                ring.Add(new Vector3(t.center.x + Mathf.Cos(a) * (t.radius + 0.03f), y, t.center.y + Mathf.Sin(a) * (t.radius + 0.03f)));
            }
            using (ElementRecorder.Tag(kind: TimberElementKind.Iron))
                vis.Sweep(ring, 0.04f, height, Iron, MeshBuilder.Anchor.Center, false);
            if (!collar) return;
            using var trim = ElementRecorder.Tag(kind: TimberElementKind.Trim);
            // a timber collar under the band: the deck's ledger bolted round the trunk
            var collarRing = ring.Select(p => p + (p - new Vector3(t.center.x, y, t.center.y)).normalized * 0.06f + Vector3.down * 0.2f).ToList();
            vis.Sweep(collarRing, 0.1f, 0.22f, Timber, MeshBuilder.Anchor.Center, false);
        }

        // ------------------------------------------------------------------ walkway (boardwalk, ramp, stair)

        public struct WalkSpec
        {
            public bool railLeft, railRight;
            public int seed;
            /// <summary>A walkway on the ground (a boardwalk on sleepers) rather than raised on posts.</summary>
            public bool onGround;
            /// <summary>Something else holds it (a stairwell's walls): no supports of its own.</summary>
            public bool held;
            /// <summary>Auto: the kit chooses; else that support (Tower means trestle bents), where it can stand.</summary>
            public TimberSupport support;
            /// <summary>The share of aged boards (0: the kit's own, 25 %).</summary>
            public float agedShare;
        }

        /// <summary>
        /// A walkway along <paramref name="path"/> (the walking surface's centre line, world points): boards across it where it is
        /// level or gently sloped, treads where it climbs (every flight's steps rise evenly, half a rise each side of its smooth
        /// ramp), two stringers under its edges, posts, braces or hangers under it, and rails on the sides asked for. Its collider is
        /// the smooth ramp, so a walker never catches a step.
        /// </summary>
        public static GameObject Walkway(Transform parent, string name, IList<Vector3> path, float width, WalkSpec spec, Site site)
        {
            var rng = new Random(Seed(name, spec.seed));
            var p = Curves.Resample(path, 0.1f);
            int n = p.Count;
            var s = new float[n];
            for (int i = 1; i < n; i++) s[i] = s[i - 1] + Flat(p[i] - p[i - 1]);
            // flights: runs where the surface climbs faster than a gentle ramp (grade over 0.2)
            var surface = p.Select(v => v).ToList();
            var steep = new bool[n];
            for (int i = 0; i < n - 1; i++) steep[i] = Mathf.Abs(p[i + 1].y - p[i].y) / Mathf.Max(0.001f, s[i + 1] - s[i]) > 0.2f;
            var flights = new List<(int i0, int i1)>();
            for (int i = 0; i < n - 1; i++)
            {
                if (!steep[i]) continue;
                int j = i;
                while (j < n - 1 && steep[j]) j++;
                flights.Add((i, j));
                i = j;
            }
            var vis = Visual();
            var sides = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var tangent = p[Mathf.Min(i + 1, n - 1)] - p[Mathf.Max(i - 1, 0)];
                var flat = new Vector3(tangent.x, 0f, tangent.z).normalized;
                sides[i] = Vector3.Cross(Vector3.up, flat).normalized;
            }
            // flights become straight ramps between their ends (the proxy), with even steps on them
            ElementRecorder.RunSurface(0, width, ProxyDepth);
            var treads = ElementRecorder.Tag("Boards", TimberElementKind.Plank, TimberWalk.Run, 0);
            bool[] inFlight = new bool[n];
            foreach (var (i0, i1) in flights)
            {
                float rise = p[i1].y - p[i0].y, run = s[i1] - s[i0];
                float pitch = Mathf.Atan2(Mathf.Abs(rise), run) * Mathf.Rad2Deg;
                if (pitch > MaxFlightSlope) Debug.LogWarning($"[TimberKit] {name}: a flight pitches {pitch:F0}° (more than {MaxFlightSlope}°)");
                for (int i = i0; i <= i1; i++)
                {
                    surface[i] = new Vector3(p[i].x, p[i0].y + rise * (s[i] - s[i0]) / Mathf.Max(0.001f, run), p[i].z);
                    inFlight[i] = true;
                }
                int steps = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(rise) / 0.18f));
                float stepRun = run / steps, stepRise = rise / steps;
                for (int k = 0; k < steps; k++)
                {
                    float sc = s[i0] + (k + 0.5f) * stepRun;
                    var (c, side) = AtS(p, s, sides, sc);
                    float y = p[i0].y + (k + 0.5f) * stepRise;
                    var fwd = Vector3.Cross(side, Vector3.up).normalized;
                    ElementRecorder.Order(Mathf.RoundToInt(sc * 100f));
                    // a tread of two boards, its front edge a little proud
                    for (int b = 0; b < 2; b++)
                    {
                        var center = new Vector3(c.x, y - BoardThickness * 0.5f, c.z) + fwd * ((b - 0.5f) * stepRun * 0.48f);
                        vis.Box(center, Quaternion.LookRotation(side, Vector3.up), new Vector3(stepRun * 0.46f, BoardThickness, width - 0.04f),
                                rng.NextDouble() < Share(spec.agedShare, 0.25) ? PlankAged : Plank, new Vector2(R(rng, 0f, 3f), R(rng, 0f, 3f)));
                    }
                    // the riser board under the tread's front
                    var riserTop = new Vector3(c.x, y - BoardThickness, c.z) - fwd * stepRun * 0.5f;
                    if (Mathf.Abs(stepRise) > 0.05f)
                        using (ElementRecorder.Tag(kind: TimberElementKind.Trim, walk: TimberWalk.None))
                            vis.Box(riserTop + Vector3.down * Mathf.Abs(stepRise) * 0.5f, Quaternion.LookRotation(side, Vector3.up),
                                new Vector3(0.03f, Mathf.Abs(stepRise) - 0.02f, width - 0.1f), Timber);
                }
            }
            // boards across the level and gently sloped parts
            float boardStep = 0.215f;
            for (float sc = boardStep * 0.5f; sc < s[n - 1]; sc += boardStep)
            {
                int i = Index(s, sc);
                if (inFlight[i] || inFlight[Mathf.Min(i + 1, n - 1)]) continue;
                var (c, side) = AtS(surface, s, sides, sc);
                var (ahead, _) = AtS(surface, s, sides, Mathf.Min(sc + 0.1f, s[n - 1]));
                var (behind, _) = AtS(surface, s, sides, Mathf.Max(sc - 0.1f, 0f));
                var fwd = (ahead - behind).normalized;
                var up = Vector3.Cross(fwd, side).normalized;
                if (up.y < 0f) up = -up;
                float len = width - 0.02f + R(rng, -0.03f, 0.03f);
                ElementRecorder.Order(Mathf.RoundToInt(sc * 100f));
                vis.Box(c - up * BoardThickness * 0.5f + side * R(rng, -0.02f, 0.02f), Quaternion.LookRotation(side, up),
                        new Vector3(boardStep - 0.015f, BoardThickness, len), rng.NextDouble() < Share(spec.agedShare, 0.25) ? PlankAged : Plank, new Vector2(R(rng, 0f, 3f), R(rng, 0f, 3f)));
            }
            treads.Dispose();
            // stringers under both edges, following the smooth surface
            var stringers = ElementRecorder.Tag("Frame", TimberElementKind.Beam);
            float inset = width * 0.5f - 0.08f;
            var left = surface.Select((v, i) => v - sides[i] * inset + Vector3.down * BoardThickness).ToList();
            var right = surface.Select((v, i) => v + sides[i] * inset + Vector3.down * BoardThickness).ToList();
            var leftS = Curves.Resample(left, 0.5f);
            var rightS = Curves.Resample(right, 0.5f);
            vis.Sweep(leftS, 0.08f, 0.26f, Timber, MeshBuilder.Anchor.Top);
            vis.Sweep(rightS, 0.08f, 0.26f, Timber, MeshBuilder.Anchor.Top);
            stringers.Dispose();
            using var supports = ElementRecorder.Tag("Supports", TimberElementKind.Post);

            // supports: sleepers where the walk lies on the ground, else a braced trestle bent about every 5.5 m, a brace to a trunk,
            // or hangers from the limbs above
            bool walkAuto = spec.support == TimberSupport.Auto;
            for (float sc = 0.6f; !spec.held && spec.support != TimberSupport.None && sc < s[n - 1] - 0.3f; sc += 5.5f)
            {
                var (c, side) = AtS(surface, s, sides, sc);
                var footL = c - side * inset + Vector3.down * (BoardThickness + 0.26f);
                var footR = c + side * inset + Vector3.down * (BoardThickness + 0.26f);
                float drop = Mathf.Min(footL.y - site.GroundAt(new Vector2(footL.x, footL.z)), footR.y - site.GroundAt(new Vector2(footR.x, footR.z)));
                if (spec.onGround || (walkAuto && !float.IsNaN(drop) && drop < 0.9f))
                {
                    using var sleeper = ElementRecorder.Tag(kind: TimberElementKind.Beam);
                    vis.Beam(c - side * (width * 0.5f + 0.1f) + Vector3.down * 0.42f, c + side * (width * 0.5f + 0.1f) + Vector3.down * 0.42f, 0.18f, 0.16f, Timber);
                    continue;
                }
                bool standing = !float.IsNaN(drop);
                if ((walkAuto && standing && drop <= MaxPost(site)) ||
                    (standing && (spec.support == TimberSupport.Posts || spec.support == TimberSupport.Tower)))
                {
                    Bent(vis, footL, footR, site);
                    continue;
                }
                bool braced = false;
                if (walkAuto || spec.support == TimberSupport.Braces)
                {
                    braced = BraceToTrunk(vis, footL, site, 7f);
                    braced |= BraceToTrunk(vis, footR, site, 7f);
                }
                if (!braced && spec.support != TimberSupport.Braces)
                {
                    Hang(vis, footL, site);
                    Hang(vis, footR, site);
                }
            }

            LastWalkSurface = surface;
            var proxy = new MeshBuilder();
            proxy.Sweep(Curves.Resample(surface, 0.25f), width, ProxyDepth, 0, MeshBuilder.Anchor.Top);
            float tolerance = flights.Count > 0 ? 0.12f : 0.08f;
            var root = Finish(parent, name, vis, proxy, WalkableProxy.Kind.Walkway, tolerance);
            if (spec.railLeft) Railing(root.transform, name + " rail L", surface.Select((v, i) => v - sides[i] * (width * 0.5f - 0.05f)).ToList(), rng.Next());
            if (spec.railRight) Railing(root.transform, name + " rail R", surface.Select((v, i) => v + sides[i] * (width * 0.5f - 0.05f)).ToList(), rng.Next());
            return root;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        static int Index(float[] s, float sc)
        {
            int i = Array.BinarySearch(s, sc);
            if (i < 0) i = ~i - 1;
            return Mathf.Clamp(i, 0, s.Length - 1);
        }

        static (Vector3 point, Vector3 side) AtS(IList<Vector3> p, float[] s, Vector3[] sides, float sc)
        {
            int i = Mathf.Min(Index(s, sc), p.Count - 2);
            float f = Mathf.Clamp01((sc - s[i]) / Mathf.Max(0.0001f, s[i + 1] - s[i]));
            return (Vector3.Lerp(p[i], p[i + 1], f), Vector3.Slerp(sides[i], sides[i + 1], f).normalized);
        }

        // ------------------------------------------------------------------ rope bridge

        public struct BridgeSpec
        {
            public bool sideWalls;       // colliders up to the hand ropes (no stepping off the side)
            public int seed;
            public float agedShare;      // the share of aged slats (0: the kit's own, 35 %)
        }

        /// <summary>
        /// A rope bridge from <paramref name="a"/> to <paramref name="b"/> (walking-surface points at its ends) dipping
        /// <paramref name="sag"/> at mid span: slats with gaps on two foot ropes, hand ropes on posts with hangers down to the foot
        /// ropes, back-stays to stakes. Its collider follows the sag; the steepest point is at its ends (keep it gentle, 18° or less).
        /// </summary>
        public static GameObject RopeBridge(Transform parent, string name, Vector3 a, Vector3 b, float width, float sag, BridgeSpec spec)
        {
            var rng = new Random(Seed(name, spec.seed));
            float length = Flat(b - a);
            int segments = Mathf.Max(4, Mathf.CeilToInt(length / 0.32f));
            var deck = Curves.Sag(a, b, sag, segments);
            if (Curves.SagEndSlope(a, b, sag) > 20f) Debug.LogWarning($"[TimberKit] {name}: the bridge's ends pitch {Curves.SagEndSlope(a, b, sag):F0}°");
            var dirFlat = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
            var side = Vector3.Cross(Vector3.up, dirFlat).normalized;
            var vis = Visual();
            // slats
            ElementRecorder.RunSurface(0, width, ProxyDepth * 0.75f);
            var slats = ElementRecorder.Tag("Slats", TimberElementKind.Plank, TimberWalk.Run, 0);
            for (int i = 0; i < deck.Count; i++)
            {
                if (i == 0 || i == deck.Count - 1) continue;
                ElementRecorder.Order(i);
                var tangent = (deck[Mathf.Min(i + 1, deck.Count - 1)] - deck[Mathf.Max(i - 1, 0)]).normalized;
                var up = Vector3.Cross(tangent, side).normalized;
                if (up.y < 0f) up = -up;
                var c = deck[i] - up * BoardThickness * 0.5f + side * R(rng, -0.03f, 0.03f);
                var rot = Quaternion.LookRotation(side, up) * Quaternion.Euler(0f, R(rng, -1.5f, 1.5f), 0f);
                vis.Box(c, rot, new Vector3(0.22f, BoardThickness, width + R(rng, 0.02f, 0.12f)), rng.NextDouble() < Share(spec.agedShare, 0.35) ? PlankAged : Plank,
                        new Vector2(R(rng, 0f, 3f), R(rng, 0f, 3f)));
            }
            slats.Dispose();
            // foot ropes under the slats' ends, hand ropes above
            var ropes = ElementRecorder.Tag("Ropes", TimberElementKind.Rope);
            float half = width * 0.5f;
            var footL = deck.Select(v => v - side * (half - 0.08f) + Vector3.down * 0.07f).ToList();
            var footR = deck.Select(v => v + side * (half - 0.08f) + Vector3.down * 0.07f).ToList();
            var handA = a + Vector3.up * RailHeight;
            var handB = b + Vector3.up * RailHeight;
            var handL = Curves.Sag(handA - side * (half + 0.05f), handB - side * (half + 0.05f), sag * 0.7f, segments / 2);
            var handR = Curves.Sag(handA + side * (half + 0.05f), handB + side * (half + 0.05f), sag * 0.7f, segments / 2);
            foreach (var rope in new[] { footL, footR }) vis.Tube(rope, rope.Select(_ => 0.035f).ToList(), 6, Rope);
            foreach (var rope in new[] { handL, handR }) vis.Tube(rope, rope.Select(_ => 0.03f).ToList(), 6, Rope);
            // hangers from the hand ropes to the foot ropes, every three slats, and a zig-zag net between them
            for (int i = 3; i < deck.Count - 2; i += 3)
            {
                float t = i / (float)(deck.Count - 1);
                foreach (var (foot, hand) in new[] { (footL, handL), (footR, handR) })
                {
                    var top = Curves.At(hand, Curves.Length(hand) * t).point;
                    vis.Tube(new[] { foot[i], top }, new[] { 0.016f, 0.016f }, 4, Rope);
                    if (i + 3 < deck.Count - 2)
                    {
                        var nextTop = Curves.At(hand, Curves.Length(hand) * ((i + 3) / (float)(deck.Count - 1))).point;
                        vis.Tube(new[] { foot[i], Vector3.Lerp(top, nextTop, 0.5f) + Vector3.down * 0.35f }, new[] { 0.012f, 0.012f }, 4, Rope);
                    }
                }
            }
            ropes.Dispose();
            // posts at both ends, back-stays to stakes behind them
            var ends = ElementRecorder.Tag("Posts", TimberElementKind.Post);
            foreach (var (end, back) in new[] { (a, -dirFlat), (b, dirFlat) })
                foreach (float sgn in new[] { -1f, 1f })
                {
                    var foot = end + side * sgn * (half + 0.12f);
                    vis.Beam(foot + Vector3.down * 0.9f, foot + Vector3.up * (RailHeight + 0.25f), 0.2f, 0.2f, Timber, new Vector2(R(rng, 0f, 2f), 0f));
                    var stake = foot + back * 1.4f + Vector3.down * 0.05f;
                    vis.Beam(stake + Vector3.down * 0.3f, stake + Vector3.up * 0.25f, 0.1f, 0.1f, Timber);
                    using var stay = ElementRecorder.Tag(kind: TimberElementKind.Rope);
                    vis.Tube(new[] { foot + Vector3.up * (RailHeight + 0.15f), stake + Vector3.up * 0.2f }, new[] { 0.03f, 0.03f }, 6, Rope);
                    vis.Tube(new[] { foot + Vector3.up * RailHeight, foot + Vector3.up * RailHeight - back * 0.25f }, new[] { 0.045f, 0.045f }, 6, Rope);
                }

            ends.Dispose();
            var proxy = new MeshBuilder();
            proxy.Sweep(deck, width, ProxyDepth * 0.75f, 0, MeshBuilder.Anchor.Top);
            var root = Finish(parent, name, vis, proxy, WalkableProxy.Kind.Bridge, 0.08f);
            if (spec.sideWalls)
                using (ElementRecorder.Tag("Sides"))
                foreach (float sgn in new[] { -1f, 1f })
                    WallChain(root.transform, name + (sgn < 0 ? " side L" : " side R"), deck.Select(v => v + side * sgn * (half + 0.06f)).ToList(), RailHeight + 0.05f);
            return root;
        }

        // ------------------------------------------------------------------ railings

        /// <summary>
        /// A railing along <paramref name="edge"/> (walking-surface points): posts about every 1.7 m (always at sharp corners), a
        /// hand rail and a mid rail following the edge, and a chain of box colliders up to the hand rail.
        /// </summary>
        public static GameObject Railing(Transform parent, string name, IList<Vector3> edge, int seed = 0)
        {
            var rng = new Random(Seed(name, seed));
            var line = Curves.Resample(edge, 0.25f);
            if (line.Count < 2 || Curves.Length(line) < 0.6f) return null;
            using var tag = ElementRecorder.Tag(name, TimberElementKind.Post, TimberWalk.None);
            var vis = Visual();
            float total = Curves.Length(line);
            int posts = Mathf.Max(1, Mathf.RoundToInt(total / R(rng, 1.55f, 1.85f)));
            for (int i = 0; i <= posts; i++)
            {
                var (pt, _) = Curves.At(line, total * i / posts);
                vis.Beam(pt + Vector3.down * 0.3f, pt + Vector3.up * (RailHeight + 0.06f), 0.11f, 0.11f, Timber, new Vector2(R(rng, 0f, 2f), 0f));
            }
            using var rails = ElementRecorder.Tag(kind: TimberElementKind.Rail);
            vis.Sweep(line.Select(v => v + Vector3.up * RailHeight).ToList(), 0.1f, 0.07f, Timber, MeshBuilder.Anchor.Top);
            vis.Sweep(line.Select(v => v + Vector3.up * 0.55f).ToList(), 0.05f, 0.09f, Timber, MeshBuilder.Anchor.Center);
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            AddVisual(root, name, vis);
            WallChain(root.transform, name + " body", line, RailHeight + 0.02f);
            return root;
        }

        /// <summary>A rail round a deck's outline, broken at the openings and where a trunk stands at the edge.</summary>
        static void RailOutline(Transform parent, string name, IList<Vector2> poly, Func<Vector2, float> top, IList<Opening> openings, IList<Trunk> trunks,
                                Func<Vector2, bool> skip = null)
        {
            var ring = Poly2.Offset(poly, -0.07f);
            // dense samples round the edge, each kept or cut
            var samples = new List<(Vector2 p, bool keep)>();
            for (int i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.2f));
                for (int k = 0; k < n; k++)
                {
                    var q = Vector2.Lerp(a, b, k / (float)n);
                    bool keep = true;
                    if (openings != null) foreach (var o in openings) if (Vector2.Distance(o.at, q) < o.width * 0.5f + 0.1f) keep = false;
                    if (trunks != null) foreach (var t in trunks) if (Vector2.Distance(t.center, q) < t.radius + 0.7f) keep = false;
                    if (skip != null && skip(q)) keep = false;
                    samples.Add((q, keep));
                }
            }
            if (samples.Count == 0) return;
            // start at a cut so every run is whole
            int start = samples.FindIndex(x => !x.keep);
            if (start < 0)
            {
                var whole = samples.Select(x => new Vector3(x.p.x, top(x.p), x.p.y)).ToList();
                whole.Add(whole[0]);
                Railing(parent, name + " rail", whole);
                return;
            }
            var run = new List<Vector3>();
            int part = 0;
            for (int k = 1; k <= samples.Count; k++)
            {
                var (q, keep) = samples[(start + k) % samples.Count];
                if (keep) run.Add(new Vector3(q.x, top(q), q.y));
                if ((!keep || k == samples.Count) && run.Count > 1)
                {
                    Railing(parent, $"{name} rail {++part}", run);
                    run = new List<Vector3>();
                }
                else if (!keep) run.Clear();
            }
        }

        /// <summary>Box colliders on Environment from the walking surface up <paramref name="height"/> along a line (a rail or a rope wall).</summary>
        static void WallChain(Transform parent, string name, IList<Vector3> line, float height)
        {
            var go = new GameObject(name) { layer = CourseKit.Layer("Environment") };
            go.transform.SetParent(parent, true);
            ElementRecorder.RecordWall(go, line, height);
            var pts = Curves.Resample(line, 1.2f);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                var flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                if (flat.sqrMagnitude < 1e-4f) continue;
                var child = new GameObject("Wall " + i) { layer = go.layer };
                child.transform.SetParent(go.transform, true);
                float baseY = Mathf.Min(a.y, b.y) - 0.1f;
                float topY = Mathf.Max(a.y, b.y) + height;
                child.transform.SetPositionAndRotation(new Vector3((a.x + b.x) * 0.5f, (baseY + topY) * 0.5f, (a.z + b.z) * 0.5f), Quaternion.LookRotation(flat.normalized));
                var box = child.AddComponent<BoxCollider>();
                box.size = new Vector3(0.12f, topY - baseY, flat.magnitude + 0.06f);
                GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.BatchingStatic);
            }
        }

        // ------------------------------------------------------------------ assembly

        /// <summary>The piece's root: its drawn timber (one renderer, a submesh per material) and its walking proxy.</summary>
        static GameObject Finish(Transform parent, string name, MeshBuilder vis, MeshBuilder proxy, WalkableProxy.Kind kind, float tolerance)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            AddVisual(root, name, vis);
            AddProxy(root, name, proxy, kind, tolerance);
            return root;
        }

        public static void AddVisual(GameObject root, string name, MeshBuilder vis)
        {
            if (vis.Triangles == 0) return;
            var mesh = OrganicAssets.Save(vis.ToMesh(name + " timber", out var slots));
            var go = new GameObject("Visual");
            go.transform.SetParent(root.transform, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = Materials(slots);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        public static void AddProxy(GameObject root, string name, MeshBuilder proxy, WalkableProxy.Kind kind, float tolerance)
        {
            var mesh = OrganicAssets.Save(proxy.ToSingleMesh(name + " ground"));
            var go = new GameObject("Ground") { layer = CourseKit.Layer("Environment") };
            go.transform.SetParent(root.transform, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            go.AddComponent<WalkableProxy>().Configure(kind, tolerance);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        static float R(Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>A stable seed from a piece's name (string hashes differ between runtimes).</summary>
        public static int Seed(string name, int salt)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in name) h = (h ^ c) * 16777619;
                return (int)(h ^ (uint)salt * 2654435761u);
            }
        }
    }
}
