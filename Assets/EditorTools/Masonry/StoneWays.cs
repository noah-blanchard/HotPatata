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
    /// Draws the stone pieces (ARCHITECTURE §27.7, docs/LEVEL_AUTHORING.md §31): stone stairs, paving, stepping stones, stone bridges,
    /// arches and portals, columns, plinths and towers. Stones are <see cref="MasonryKit.Block"/>s in one mesh per piece; what is walked
    /// is a walking proxy (a smooth ramp, a flat floor), what stops a body an invisible box; timber parts go through the timber kit.
    /// Built in the world (a piece's output sits at the origin). Called by <see cref="LevelPieceBuilder"/>.
    /// </summary>
    public static class StoneWays
    {
        const int Stone = MasonryKit.Stone, Fill = MasonryKit.CoreSlot;
        const int Plank = TimberKit.Plank, Aged = TimberKit.PlankAged, Wood = TimberKit.Timber;

        public static string StoneMaterial(StoneWear wear) => wear == StoneWear.Mossy ? "Nature_MossyRock" : wear == StoneWear.New ? "Nature_Limestone" : "Nature_Granite";

        // ------------------------------------------------------------------ helpers

        static float R(Random rng, float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());

        static Random Rng(LevelPiece piece, string name, int salt) => new Random(TimberKit.Seed(piece.PieceId ?? name, piece.Seed + salt));

        /// <summary>The ground under <paramref name="p"/> (searched from a little above it), or <paramref name="fallback"/>.</summary>
        static float Ground(Vector3 p, float fallback, float reach = 30f)
        {
            float g = SceneSite.GroundAt(new Vector2(p.x, p.z), p.y + 0.5f, reach);
            return float.IsNaN(g) ? fallback : g;
        }

        /// <summary>The stones drawn (no collider): stone, a fill behind them, plaster.</summary>
        static GameObject Draw(Transform root, string name, MeshBuilder mb, StoneWear wear)
        {
            if (mb.Triangles == 0) return null;
            var mesh = OrganicAssets.Save(mb.ToMesh(name + " stone", out var slots));
            var go = new GameObject(name + " stone");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var names = new[] { StoneMaterial(wear), "Nature_CaveRock", "Nature_Plaster" };
            go.AddComponent<MeshRenderer>().sharedMaterials = slots.Select(s => NatureMaterialBuilder.Load(names[s])).ToArray();
            go.AddComponent<CourseDecoration>();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>A walking proxy: the collider a walker treads (smooth), on Environment.</summary>
        static GameObject Proxy(Transform root, string name, MeshBuilder proxy, WalkableProxy.Kind kind, float tolerance)
        {
            var mesh = OrganicAssets.Save(proxy.ToSingleMesh(name + " ground"));
            var go = new GameObject(name + " ground") { layer = CourseKit.Layer("Environment") };
            go.transform.SetParent(root, false);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<WalkableProxy>().Configure(kind, tolerance);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static GameObject Solid(Transform root, string name, Vector3 center, Quaternion rot, Vector3 size) =>
            TimberBuildings.SolidInvisible(root, name, center, rot, size);

        /// <summary>A block laid between two heights over a point of a line: centre, turned to the line (x across, z along).</summary>
        static void Block(MeshBuilder mb, Vector3 at, Vector3 along, float y0, float y1, float across, float length, Random rng, float bevel = 0.04f, float rough = 0.02f, int slot = Stone)
        {
            if (y1 - y0 < 0.04f || length < 0.05f) return;
            var rot = Quaternion.LookRotation(along, Vector3.up);
            MasonryKit.Block(mb, new Vector3(at.x, (y0 + y1) * 0.5f, at.z), rot, new Vector3(across, y1 - y0, length), bevel, rough, rng.Next(), slot);
        }

        /// <summary>Courses of stones along a flat run from <paramref name="a"/> to <paramref name="b"/>, from y0 to y1 (a pier, a parapet, a side wall).</summary>
        static void Courses(MeshBuilder mb, Vector3 a, Vector3 b, float y0, float y1, float thickness, Random rng, float course = 0.34f)
        {
            var d = b - a;
            d.y = 0f;
            float length = d.magnitude;
            if (length < 0.05f) return;
            var along = d / length;
            int row = 0;
            for (float y = y0; y < y1 - 0.05f; row++)
            {
                float h = Mathf.Min(R(rng, course * 0.8f, course * 1.2f), y1 - y);
                float s = row % 2 == 0 ? 0f : -R(rng, 0.15f, 0.3f);
                while (s < length)
                {
                    float len = R(rng, 0.4f, 0.8f);
                    float s0 = Mathf.Max(0f, s), s1 = Mathf.Min(length, s + len);
                    s += len;
                    if (s1 - s0 < 0.1f) continue;
                    Block(mb, a + along * ((s0 + s1) * 0.5f), along, y + 0.015f, y + h - 0.015f, thickness * R(rng, 0.94f, 1.02f), s1 - s0 - 0.03f, rng);
                }
                y += h;
            }
        }

        // ------------------------------------------------------------------ stone stair

        /// <summary>A stone stair: its steps (drawn), a fill under them to the ground, side walls if built, a smooth ramp walked. Returns its walk line.</summary>
        public static Vector3[] Stair(StoneStairShape stair, Transform root, string name)
        {
            var control = stair.WorldPoints();
            if (control.Count < 2 || Curves.Length(control) < 0.5f) return null;
            var line = Curves.Resample(Curves.Spline(control, 0.3f), 0.1f);
            int n = line.Count;
            var s = new float[n];
            for (int i = 1; i < n; i++) s[i] = s[i - 1] + new Vector2(line[i].x - line[i - 1].x, line[i].z - line[i - 1].z).magnitude;
            float length = s[n - 1], rise = line[n - 1].y - line[0].y;
            int steps = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(rise) / stair.Rise));
            float going = length / steps;
            var rng = Rng(stair, name, 307);
            var mb = new MeshBuilder(MasonryKit.StoneSlots);
            float floor = Mathf.Min(line[0].y, line[n - 1].y) - 0.4f;
            (Vector3 p, Vector3 along) At(float sc)
            {
                int i = Mathf.Clamp(Array.BinarySearch(s, sc) is int k && k < 0 ? ~k - 1 : k, 0, n - 2);
                float f = Mathf.Clamp01((sc - s[i]) / Mathf.Max(1e-4f, s[i + 1] - s[i]));
                var d = line[i + 1] - line[i];
                d.y = 0f;
                return (Vector3.Lerp(line[i], line[i + 1], f), d.sqrMagnitude < 1e-6f ? Vector3.forward : d.normalized);
            }
            float half = stair.Width * 0.5f;
            for (int k = 0; k < steps; k++)
            {
                float sc = (k + 0.5f) * going;
                var (p, along) = At(sc);
                var right = Vector3.Cross(Vector3.up, along);
                // the tread's top: the line at its far end going up (its near end going down), so the ramp runs through the noses
                float top = rise >= 0f ? At(Mathf.Min(length, (k + 1) * going)).p.y : At(k * going).p.y;
                float ground = Ground(p, floor);
                float bottom = Mathf.Min(ground - 0.3f, top - 0.3f);
                switch (stair.Kind)
                {
                    case StoneStairKind.CutInRock:
                        Block(mb, p, along, bottom, top, stair.Width + 0.4f, going + 0.04f, rng, 0.05f, 0.04f);
                        break;
                    default:
                        // a flagstone tread (in two or three stones across), a rough fill under it
                        int across = Mathf.Max(1, Mathf.RoundToInt(stair.Width / 0.9f));
                        for (int c = 0; c < across; c++)
                        {
                            float x = -half + stair.Width * (c + 0.5f) / across;
                            Block(mb, p + right * x, along, top - 0.14f, top, stair.Width / across - 0.03f, going + 0.06f, rng, 0.03f, 0.008f);
                        }
                        Block(mb, p, along, bottom, top - 0.14f, stair.Width - 0.1f, going, rng, 0.02f, 0.03f, Fill);
                        break;
                }
                if (stair.Kind == StoneStairKind.Built && stair.SideWalls > 0f)
                    foreach (float e in new[] { -1f, 1f })
                    {
                        var c = p + right * e * (half + 0.25f);
                        Courses(mb, c - along * going * 0.5f, c + along * going * 0.5f, bottom, top + stair.SideWalls, 0.5f, rng, 0.3f);
                        Solid(root, $"{name} side {k}{(e < 0 ? "L" : "R")}", new Vector3(c.x, (bottom + top + stair.SideWalls) * 0.5f, c.z), Quaternion.LookRotation(along), new Vector3(0.5f, top + stair.SideWalls - bottom, going + 0.02f));
                    }
            }
            Draw(root, name, mb, stair.Wear);
            // the walk: a smooth ramp along the line
            var walk = line.Select((p, i) => new Vector3(p.x, line[0].y + rise * s[i] / Mathf.Max(1e-4f, length), p.z)).ToList();
            var proxy = new MeshBuilder();
            proxy.Sweep(Curves.Resample(walk, 0.25f), stair.Width, TimberKit.ProxyDepth, 0, MeshBuilder.Anchor.Top);
            Proxy(root, name, proxy, WalkableProxy.Kind.Walkway, 0.14f);
            return walk.ToArray();
        }

        // ------------------------------------------------------------------ paving

        /// <summary>Paving on the ground stands this proud of the piece's height (m).</summary>
        public const float OnGround = 0.06f;

        /// <summary>Paving over its outline: stones laid flat, a kerb, a retaining edge when raised; walked as a flat floor.</summary>
        public static void Paving(PavingShape paving, Transform root, string name)
        {
            var outline = Poly2.CounterClockwise(paving.WorldOutline());
            if (outline.Count < 3) return;
            // on the ground, a little proud of it (the ground is never quite flat under a floor)
            float top = paving.transform.position.y + Mathf.Max(paving.Raised, OnGround);
            var rng = Rng(paving, name, 331);
            var mb = new MeshBuilder(MasonryKit.StoneSlots);
            var box = Rect.MinMaxRect(outline.Min(p => p.x), outline.Min(p => p.y), outline.Max(p => p.x), outline.Max(p => p.y));
            var along = paving.transform.forward;
            along.y = 0f;
            along = along.sqrMagnitude < 1e-6f ? Vector3.forward : along.normalized;
            var right = Vector3.Cross(Vector3.up, along);
            float cell = paving.Kind == PavingKind.Cobbles ? 0.24f : paving.Kind == PavingKind.Slabs ? 0.5f : 0.75f;
            float reach = Mathf.Max(box.width, box.height);
            var c0 = box.center;
            int rows = Mathf.CeilToInt(reach / cell) + 1;
            for (int r = -rows; r <= rows; r++)
            {
                float offset = paving.Kind == PavingKind.Slabs && r % 2 != 0 ? 0.5f : paving.Kind == PavingKind.Flagstones ? R(rng, 0f, 0.4f) : 0f;
                float len = paving.Kind == PavingKind.Slabs ? 1f : cell;
                for (float u = -reach + offset; u < reach; u += len)
                {
                    float l = paving.Kind == PavingKind.Flagstones ? R(rng, 0.55f, 1.1f) : len;
                    var mid = new Vector3(c0.x, 0f, c0.y) + right * (r * cell) + along * (u + l * 0.5f);
                    if (!Poly2.Contains(outline, new Vector2(mid.x, mid.z)) || Poly2.DistanceToEdge(outline, new Vector2(mid.x, mid.z)) < cell * 0.4f) { u += l - len; continue; }
                    bool cobble = paving.Kind == PavingKind.Cobbles;
                    var rot = Quaternion.LookRotation(along, Vector3.up) * Quaternion.Euler(0f, cobble ? R(rng, -8f, 8f) : 0f, 0f);
                    MasonryKit.Block(mb, new Vector3(mid.x, top - 0.07f + R(rng, -0.008f, 0.008f), mid.z), rot,
                                     new Vector3(cell - (cobble ? 0.035f : 0.025f), 0.14f, l - (cobble ? 0.035f : 0.025f)), cobble ? 0.05f : 0.02f, 0.006f, rng.Next(), Stone, cobble ? 1 : 3);
                    u += l - len;
                }
            }
            // the bed under the stones, and the edge: a kerb, a retaining wall down to the ground when raised
            mb.Prism(outline, top - 0.4f, top - 0.1f, Fill, Fill);
            var ring = LevelRock.ResampleClosed(outline, 0.6f);
            for (int i = 0; i < ring.Count; i++)
            {
                var a = new Vector3(ring[i].x, top, ring[i].y);
                var b = new Vector3(ring[(i + 1) % ring.Count].x, top, ring[(i + 1) % ring.Count].y);
                var edge = b - a;
                if (edge.sqrMagnitude < 1e-4f) continue;
                var mid = (a + b) * 0.5f;
                var outward = Vector3.Cross(edge.normalized, Vector3.up);
                if (paving.Raised > 0.05f)
                {
                    float ground = Ground(mid + outward * 0.4f, top - paving.Raised);
                    Courses(mb, a + outward * 0.25f, b + outward * 0.25f, ground - 0.3f, top - (paving.EdgeStones ? 0f : 0.02f), 0.5f, rng, 0.3f);
                }
                if (paving.EdgeStones)
                    Block(mb, mid + outward * 0.15f, edge.normalized, top - 0.2f, top + 0.08f, 0.3f, edge.magnitude - 0.03f, rng, 0.03f, 0.01f);
            }
            Draw(root, name, mb, paving.Wear);
            var proxy = new MeshBuilder();
            float bottom = paving.Raised > 0.05f ? top - paving.Raised - 0.3f : top - 0.4f;
            proxy.Prism(outline, bottom, top, 0, 0);
            Proxy(root, name, proxy, paving.Raised > 0.05f ? WalkableProxy.Kind.Solid : WalkableProxy.Kind.Deck, 0.06f);
        }

        // ------------------------------------------------------------------ stepping stones

        /// <summary>Stepping stones along the line: each a flat-topped stone, solid, standing on what is under it. Returns their tops.</summary>
        public static Vector3[] SteppingStones(SteppingStonesShape shape, Transform root, string name)
        {
            var control = shape.WorldPoints();
            if (control.Count < 2) return null;
            var line = Curves.Spline(control, 0.2f);
            float length = Curves.Length(line);
            int count = Mathf.Max(2, Mathf.RoundToInt(length / shape.Spacing) + 1);
            var rng = Rng(shape, name, 353);
            var mb = new MeshBuilder(MasonryKit.StoneSlots);
            var tops = new List<Vector3>();
            for (int k = 0; k < count; k++)
            {
                var (p, tangent) = Curves.At(line, length * k / (count - 1));
                var flat = new Vector3(tangent.x, 0f, tangent.z);
                flat = flat.sqrMagnitude < 1e-6f ? Vector3.forward : flat.normalized;
                var right = Vector3.Cross(Vector3.up, flat);
                var at = p + right * R(rng, -shape.Jitter, shape.Jitter);
                float top = p.y + shape.Height;
                float bottom = Mathf.Min(Ground(at, p.y - 1f), top - 0.5f) - 0.3f;
                float size = shape.Size * R(rng, 0.85f, 1.15f);
                var rot = Quaternion.Euler(0f, R(rng, 0f, 360f), 0f);
                MasonryKit.Block(mb, new Vector3(at.x, (top + bottom) * 0.5f, at.z), rot, new Vector3(size, top - bottom, size * R(rng, 0.8f, 1f)), Mathf.Min(0.18f, size * 0.2f), 0.04f, rng.Next(), Stone);
                var go = Solid(root, $"{name} {k + 1}", new Vector3(at.x, (top + bottom) * 0.5f, at.z), rot, new Vector3(size * 0.86f, top - bottom, size * 0.7f));
                go.AddComponent<WalkableProxy>().Configure(WalkableProxy.Kind.Solid, 0.06f);
                tops.Add(new Vector3(at.x, top, at.z));
            }
            Draw(root, name, mb, StoneWear.Mossy);
            return tops.ToArray();
        }

        // ------------------------------------------------------------------ stone bridge

        /// <summary>
        /// A stone bridge: its deck along the ends (humped), arches on piers under it, parapets, abutments at its ends. The deck is walked,
        /// the parapets and the masonry under the deck stop a body, the arches stay open. Returns its walk line.
        /// </summary>
        public static Vector3[] Bridge(StoneBridgeShape bridge, Transform root, string name)
        {
            var (a, b) = bridge.WorldEnds();
            var flat = b - a;
            flat.y = 0f;
            float length = flat.magnitude;
            if (length < 2f) return null;
            var along = flat / length;
            var right = Vector3.Cross(Vector3.up, along);
            var rng = Rng(bridge, name, 379);
            var mb = new MeshBuilder(MasonryKit.StoneSlots);
            float Deck(float s) => Mathf.Lerp(a.y, b.y, s / length) + bridge.Hump * 4f * (s / length) * (1f - s / length);
            Vector3 At(float s) => a + along * s;
            const float Abutment = 1.2f, Pier = 1.2f, DeckDepth = 0.5f;
            int arches = bridge.Arches;
            float span = (length - 2f * Abutment - (arches - 1) * Pier) / arches;
            if (span < 1f) { arches = 1; span = length - 2f * Abutment; }
            // the arches: a span's soffit (the open curve under the deck) at s
            float Soffit(float s)
            {
                float x = s - Abutment;
                for (int k = 0; k < arches; k++)
                {
                    float s0 = k * (span + Pier), s1 = s0 + span;
                    if (x < s0 || x > s1) continue;
                    float mid = (s0 + s1) * 0.5f, half = span * 0.5f, rise = Mathf.Min(half, 0.45f * span + 0.5f);
                    float crown = Deck(Abutment + mid) - DeckDepth - 0.5f;
                    float spring = crown - rise;
                    float t = (x - mid) / half;
                    return spring + rise * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
                }
                return float.NaN;   // a pier or an abutment: solid to the ground
            }
            // the masonry under the deck in columns: down to the arch's soffit, or to the ground
            const float Column = 0.6f;
            for (float s = 0f; s < length - 1e-3f; s += Column)
            {
                float s1 = Mathf.Min(length, s + Column), mid = (s + s1) * 0.5f;
                float deckBottom = Deck(mid) - DeckDepth;
                float soffit = Soffit(mid);
                float bottom = float.IsNaN(soffit) ? Ground(At(mid) + Vector3.up * Deck(mid) - Vector3.up * At(mid).y, deckBottom - 6f) - 0.4f : soffit;
                if (deckBottom - bottom < 0.05f) continue;
                for (float y = bottom; y < deckBottom - 0.05f;)
                {
                    float h = Mathf.Min(R(rng, 0.3f, 0.45f), deckBottom - y);
                    var p = At(mid);
                    Block(mb, new Vector3(p.x, 0f, p.z), along, y + 0.015f, y + h - 0.015f, bridge.DeckWidth + 0.1f, s1 - s - 0.03f, rng);
                    y += h;
                }
                var c = At(mid);
                Solid(root, $"{name} body {Mathf.RoundToInt(s / Column)}", new Vector3(c.x, (bottom + deckBottom) * 0.5f, c.z), Quaternion.LookRotation(along), new Vector3(bridge.DeckWidth, deckBottom - bottom, s1 - s));
            }
            // voussoirs round each arch, on both faces
            for (int k = 0; k < arches; k++)
            {
                float s0 = Abutment + k * (span + Pier);
                int count = Mathf.Max(7, Mathf.RoundToInt(span * 2.4f) | 1);
                for (int v = 0; v < count; v++)
                {
                    float sa = s0 + span * v / count, sb = s0 + span * (v + 1) / count;
                    var pa = At(sa) + Vector3.up * (Soffit(Mathf.Clamp(sa, s0 + 0.01f, s0 + span - 0.01f)) - At(sa).y);
                    var pb = At(sb) + Vector3.up * (Soffit(Mathf.Clamp(sb, s0 + 0.01f, s0 + span - 0.01f)) - At(sb).y);
                    var tangent = pb - pa;
                    var normal = Vector3.Cross(right, tangent).normalized;
                    if (normal.y < 0f) normal = -normal;
                    foreach (float e in new[] { -1f, 1f })
                        MasonryKit.Block(mb, (pa + pb) * 0.5f + normal * 0.25f + right * e * (bridge.DeckWidth * 0.5f + 0.02f), Quaternion.LookRotation(tangent.normalized, normal),
                                         new Vector3(0.3f, 0.5f, tangent.magnitude - 0.02f), 0.03f, 0.012f, rng.Next(), Stone);
                }
            }
            // the deck: flagstones across, the parapets
            for (float s = 0f; s < length - 1e-3f; s += 0.7f)
            {
                float s1 = Mathf.Min(length, s + 0.7f), mid = (s + s1) * 0.5f;
                int across = Mathf.Max(2, Mathf.RoundToInt(bridge.DeckWidth / 0.9f));
                for (int c = 0; c < across; c++)
                {
                    float x = -bridge.DeckWidth * 0.5f + bridge.DeckWidth * (c + 0.5f) / across;
                    var p = At(mid) + right * x;
                    Block(mb, p, along, Deck(mid) - 0.16f, Deck(mid), bridge.DeckWidth / across - 0.03f, s1 - s - 0.03f, rng, 0.02f, 0.006f);
                }
                if (bridge.Parapet <= 0f) continue;
                foreach (float e in new[] { -1f, 1f })
                {
                    var p = At(mid) + right * e * (bridge.DeckWidth * 0.5f - 0.2f);
                    for (float y = Deck(mid); y < Deck(mid) + bridge.Parapet - 0.05f;)
                    {
                        float h = Mathf.Min(0.3f, Deck(mid) + bridge.Parapet - y);
                        Block(mb, p, along, y + 0.01f, y + h - 0.01f, 0.4f, s1 - s - 0.03f, rng);
                        y += h;
                    }
                    Solid(root, $"{name} parapet {Mathf.RoundToInt(s / 0.7f)}{(e < 0 ? "L" : "R")}", new Vector3(p.x, Deck(mid) + bridge.Parapet * 0.5f, p.z), Quaternion.LookRotation(along), new Vector3(0.4f, bridge.Parapet, s1 - s));
                }
            }
            Draw(root, name, mb, bridge.Wear);
            var walk = Enumerable.Range(0, 33).Select(i => { float s = length * i / 32f; var p = At(s); return new Vector3(p.x, Deck(s), p.z); }).ToList();
            var proxy = new MeshBuilder();
            proxy.Sweep(walk, bridge.DeckWidth - (bridge.Parapet > 0f ? 0.8f : 0f), TimberKit.ProxyDepth, 0, MeshBuilder.Anchor.Top);
            Proxy(root, name, proxy, WalkableProxy.Kind.Bridge, 0.06f);
            return walk.ToArray();
        }

        // ------------------------------------------------------------------ arches and portals

        /// <summary>
        /// An arch: piers of coursed stone, a round or pointed ring of voussoirs (a keystone), the spandrels filled to a flat top; a lintel
        /// of stone or log over its piers; or a log portal (posts, a beam, braces). Its piers and its top collide, the passage under it is open.
        /// </summary>
        public static void Arch(ArchShape arch, Transform root, string name)
        {
            var t = arch.transform;
            var rng = Rng(arch, name, 401);
            var mb = new MeshBuilder(MasonryKit.StoneSlots);
            float half = arch.Span * 0.5f, pw = arch.PierWidth, ph = arch.PierHeight;
            bool stone = arch.Kind == ArchKind.StoneRound || arch.Kind == ArchKind.StonePointed || arch.Kind == ArchKind.StoneLintel;
            float rise = arch.Kind == ArchKind.StoneRound ? Mathf.Min(arch.Rise, half) : arch.Kind == ArchKind.StonePointed ? arch.Rise : 0f;
            float topY = ph + rise + (arch.Kind == ArchKind.StoneLintel || arch.Kind == ArchKind.LogLintel ? 0.6f : 0.55f);
            float outer = half + pw;
            // a ruin: the right side falls away from its top down
            bool Fallen(float x, float y) => arch.Crest > 0f && x > outer * (1f - 2f * arch.Crest) && y > ph * (1.2f - arch.Crest);
            float Soffit(float x)
            {
                float u = Mathf.Abs(x) / half;
                if (u >= 1f) return ph;
                return arch.Kind == ArchKind.StonePointed ? ph + rise * Mathf.Pow(1f - u, 0.55f) : ph + rise * Mathf.Sqrt(1f - u * u);
            }
            Vector3 W(float x, float y, float z = 0f) => t.TransformPoint(new Vector3(x, y, z));
            var alongX = t.right;
            if (stone)
            {
                foreach (float e in new[] { -1f, 1f })
                {
                    float cx = e * (half + pw * 0.5f);
                    for (float y = 0f; y < ph - 0.02f;)
                    {
                        float h = Mathf.Min(R(rng, 0.32f, 0.44f), ph - y);
                        if (!Fallen(cx, y)) MasonryKit.Block(mb, W(cx, y + h * 0.5f), t.rotation, new Vector3(pw - 0.03f, h - 0.02f, arch.Depth), 0.04f, 0.015f, rng.Next(), Stone);
                        y += h;
                    }
                    if (!Fallen(cx, ph * 0.5f)) Solid(root, $"{name} pier {(e < 0 ? "L" : "R")}", W(cx, ph * 0.5f), t.rotation, new Vector3(pw, ph + 0.4f, arch.Depth));
                }
                if (arch.Kind == ArchKind.StoneLintel)
                    MasonryKit.Block(mb, W(0f, ph + 0.3f), t.rotation, new Vector3(2f * outer, 0.6f, arch.Depth + 0.05f), 0.05f, 0.015f, rng.Next(), Stone);
                else
                {
                    int count = Mathf.Max(7, Mathf.RoundToInt(arch.Span * 2.6f) | 1);
                    for (int v = 0; v < count; v++)
                    {
                        float x0 = -half + arch.Span * v / count, x1 = -half + arch.Span * (v + 1) / count;
                        var pa = new Vector3(x0, Soffit(x0), 0f);
                        var pb = new Vector3(x1, Soffit(x1), 0f);
                        var tangent = pb - pa;
                        var normal = new Vector3(-tangent.y, tangent.x, 0f).normalized;
                        if (normal.y < 0f) normal = -normal;
                        bool key = arch.Keystone && v == count / 2;
                        var c = (pa + pb) * 0.5f + normal * (key ? 0.35f : 0.27f);
                        if (Fallen(c.x, c.y)) continue;
                        MasonryKit.Block(mb, W(c.x, c.y, 0f), t.rotation * Quaternion.LookRotation(tangent.normalized, normal),
                                         new Vector3(arch.Depth + (key ? 0.1f : 0f), key ? 0.7f : 0.54f, tangent.magnitude - 0.02f), 0.03f, 0.012f, rng.Next(), Stone);
                    }
                }
                // the spandrels: courses from the ring's back up to a flat top, over the span and the piers
                for (float x = -outer; x < outer - 1e-3f; x += 0.5f)
                {
                    float x1 = Mathf.Min(outer, x + 0.5f), mid = (x + x1) * 0.5f;
                    float bottom = Mathf.Abs(mid) < half ? Soffit(mid) + (arch.Kind == ArchKind.StoneLintel ? 0.6f : 0.55f) : ph;
                    if (arch.Kind == ArchKind.StoneLintel) bottom = ph + 0.6f;
                    for (float y = bottom; y < topY - 0.04f;)
                    {
                        float h = Mathf.Min(R(rng, 0.28f, 0.4f), topY - y);
                        if (!Fallen(mid, y)) MasonryKit.Block(mb, W(mid, y + h * 0.5f), t.rotation, new Vector3(x1 - x - 0.03f, h - 0.02f, arch.Depth * 0.96f), 0.03f, 0.012f, rng.Next(), Stone);
                        y += h;
                    }
                    if (!Fallen(mid, topY - 0.1f))
                        Solid(root, $"{name} top {Mathf.RoundToInt((x + outer) * 2f)}", W(mid, (Soffit(mid) + topY) * 0.5f), t.rotation, new Vector3(x1 - x, topY - Soffit(mid), arch.Depth));
                }
                Draw(root, name, mb, arch.Wear);
                return;
            }
            // timber: a log lintel on stone piers, or a log portal
            TimberBuildings.Frame(root, name + " timber", tb =>
            {
                using var tag = ElementRecorder.Tag("Portal", TimberElementKind.Log, TimberWalk.None, solid: false);
                float r = Mathf.Min(0.3f, arch.Depth * 0.4f);
                foreach (float e in new[] { -1f, 1f })
                    tb.Tube(new[] { W(e * (half + r), -0.4f), W(e * (half + r), ph + r) }, new[] { r, r * 0.92f }, 10, Wood, true, e);
                tb.Tube(new[] { W(-outer - 0.3f, ph + r * 1.6f), W(outer + 0.3f, ph + r * 1.6f) }, new[] { r * 1.05f, r }, 10, Wood, true);
                if (arch.Kind == ArchKind.LogPortal)
                    foreach (float e in new[] { -1f, 1f })
                        tb.Beam(W(e * (half + r), ph - 1f), W(e * (half - 0.7f), ph + r * 0.6f), 0.18f, 0.18f, Wood);
            });
            foreach (float e in new[] { -1f, 1f })
                Solid(root, $"{name} post {(e < 0 ? "L" : "R")}", W(e * (half + 0.25f), ph * 0.5f), t.rotation, new Vector3(0.5f, ph + 0.4f, 0.5f));
            Solid(root, $"{name} beam", W(0f, ph + 0.5f), t.rotation, new Vector3(2f * outer + 0.6f, 0.6f, 0.6f));
        }

        // ------------------------------------------------------------------ columns and plinths

        /// <summary>A column: its base, its drums, its capital; broken, its top's drums lie at its foot. What stands collides, as do the fallen drums.</summary>
        public static void Column(ColumnShape column, Transform root, string name)
        {
            var t = column.transform;
            var rng = Rng(column, name, 433);
            var mb = new MeshBuilder(MasonryKit.StoneSlots);
            float r = column.Radius;
            var lean = Quaternion.AngleAxis(column.Lean, Quaternion.Euler(0f, R(rng, 0f, 360f), 0f) * Vector3.right);
            var foot = t.position;
            var rot = t.rotation;
            float y = 0f;
            Vector3 Up(float h) => foot + lean * (rot * (Vector3.up * h));
            if (column.Base)
            {
                MasonryKit.Block(mb, foot + Vector3.up * 0.18f, rot, new Vector3(r * 2.6f, 0.36f, r * 2.6f), 0.04f, 0.01f, rng.Next(), Stone);
                y = 0.36f;
            }
            float shaft = column.Height - y - (column.Capital ? 0.4f : 0f);
            int drums = Mathf.Max(1, Mathf.RoundToInt(shaft / Mathf.Max(0.5f, r * 1.6f)));
            float drum = shaft / drums;
            int standing = Mathf.Max(1, Mathf.CeilToInt(drums * (1f - column.Broken)));
            for (int k = 0; k < drums; k++)
            {
                float y0 = y + k * drum, y1 = y0 + drum - 0.02f;
                if (k < standing)
                {
                    if (column.Kind == ColumnKind.Square)
                        MasonryKit.Block(mb, Up((y0 + y1) * 0.5f), lean * rot, new Vector3(r * 2f, drum - 0.02f, r * 2f), 0.03f, 0.01f, rng.Next(), Stone);
                    else
                    {
                        float inset = column.Kind == ColumnKind.Drum ? 0.04f : 0f;
                        mb.Tube(new[] { Up(y0), Up(y0 + 0.03f), Up(y1 - 0.03f), Up(y1) }, new[] { r - inset, r, r, r - inset }, 16, Stone, true, k * 0.31f);
                    }
                }
                else
                {
                    // fallen: on its side at the foot, away from the lean
                    var away = Quaternion.Euler(0f, R(rng, 0f, 360f), 0f) * Vector3.forward;
                    var at = foot + away * R(rng, r * 3f, r * 3f + drums * 0.4f) + Vector3.up * (r * 0.9f);
                    var axis = Vector3.Cross(Vector3.up, away).normalized;
                    if (column.Kind == ColumnKind.Square)
                        MasonryKit.Block(mb, at, Quaternion.LookRotation(axis, Vector3.up), new Vector3(r * 2f, r * 2f, drum), 0.04f, 0.02f, rng.Next(), Stone);
                    else mb.Tube(new[] { at - axis * drum * 0.5f, at + axis * drum * 0.5f }, new[] { r, r * 0.97f }, 16, Stone, true);
                    Solid(root, $"{name} drum {k + 1}", at, Quaternion.LookRotation(axis, Vector3.up), new Vector3(r * 1.8f, r * 1.8f, drum));
                }
            }
            float standTop = y + standing * drum;
            if (column.Capital && standing == drums)
            {
                MasonryKit.Block(mb, Up(standTop + 0.2f), lean * rot, new Vector3(r * 2.7f, 0.4f, r * 2.7f), 0.05f, 0.01f, rng.Next(), Stone);
                standTop += 0.4f;
            }
            Solid(root, name + " shaft", Up(standTop * 0.5f), lean * rot, new Vector3(r * 2f, standTop, r * 2f));
            Draw(root, name, mb, column.Wear);
        }

        /// <summary>A plinth: its block of dressed stones and the steps round it, each tier solid and walked.</summary>
        public static void Plinth(PlinthShape plinth, Transform root, string name)
        {
            var t = plinth.transform;
            var rng = Rng(plinth, name, 457);
            var mb = new MeshBuilder(MasonryKit.StoneSlots);
            const float StepRise = 0.2f, StepOut = 0.4f;
            for (int k = plinth.Steps; k >= 0; k--)
            {
                float y0 = (plinth.Steps - k) * StepRise, y1 = k == 0 ? plinth.Steps * StepRise + plinth.Height : y0 + StepRise;
                if (k == 0) y0 = plinth.Steps * StepRise;
                float w = plinth.Width + 2f * StepOut * k, d = plinth.Depth + 2f * StepOut * k;
                int nx = Mathf.Max(1, Mathf.RoundToInt(w / 0.9f)), nz = Mathf.Max(1, Mathf.RoundToInt(d / 0.9f));
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                    {
                        if (k > 0 && i > 0 && i < nx - 1 && j > 0 && j < nz - 1) continue;   // a step is a ring round the next tier
                        var c = new Vector3(-w * 0.5f + w * (i + 0.5f) / nx, (y0 + y1) * 0.5f - 0.05f, -d * 0.5f + d * (j + 0.5f) / nz);
                        MasonryKit.Block(mb, t.TransformPoint(c), t.rotation, new Vector3(w / nx - 0.02f, y1 - y0 + 0.1f, d / nz - 0.02f), 0.025f, 0.006f, rng.Next(), Stone);
                    }
                var solid = Solid(root, $"{name} tier {plinth.Steps - k + 1}", t.TransformPoint(new Vector3(0f, (y1 - 0.3f) * 0.5f, 0f)), t.rotation, new Vector3(w, y1 + 0.3f, d));
                solid.AddComponent<WalkableProxy>().Configure(WalkableProxy.Kind.Solid, 0.06f);
            }
            Draw(root, name, mb, plinth.Wear);
        }

        // ------------------------------------------------------------------ towers

        /// <summary>
        /// A tower: its walls drawn as a closed wall (stones, openings and their kit), floors inside (walked, each with its hatch), and its
        /// top: crenellations, a parapet (its walls), a roof (a ceiling) or a ruined crest.
        /// </summary>
        public static void Tower(TowerShape tower, Transform root, string name)
        {
            MasonryKit.Build(tower, root, name);
            var t = tower.transform;
            var rng = Rng(tower, name, 479);
            float inner = tower.Radius - tower.Thickness;
            // the inner outline, finely, from the south point
            var ring = new List<Vector2>();
            int n = tower.Plan == TowerPlan.Square ? 16 : Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * inner / 0.8f), 12, 48);
            for (int i = 0; i < n; i++)
            {
                float a = -Mathf.PI * 0.5f + i * 2f * Mathf.PI / n;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (tower.Plan == TowerPlan.Square) dir /= Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
                ring.Add(dir * (inner + 0.05f));
            }
            // floors: sectors from the middle; the hatch takes the outer part of the sectors to the east
            float hatchAngle = tower.Hatch / Mathf.Max(0.5f, inner) * Mathf.Rad2Deg;
            float hatchIn = Mathf.Max(0.3f, inner - tower.Hatch - 0.2f) / inner;
            foreach (float h in tower.Floors.Where(h => h > 0.5f && h < tower.Height - 0.3f))
            {
                var collider = new MeshBuilder();
                TimberBuildings.Frame(root, $"{name} floor {h:F1}", tb =>
                {
                    using var tag = ElementRecorder.Tag("Floor", TimberElementKind.Plank, TimberWalk.None, solid: false);
                    for (int i = 0; i < n; i++)
                    {
                        var p0 = ring[i];
                        var p1 = ring[(i + 1) % n];
                        float mid = Mathf.Atan2((p0 + p1).y, (p0 + p1).x) * Mathf.Rad2Deg;
                        bool hatch = Mathf.Abs(Mathf.DeltaAngle(mid, 0f)) < hatchAngle * 0.5f;
                        var poly = hatch
                            ? new List<Vector2> { Vector2.zero, p0 * hatchIn, p1 * hatchIn }
                            : new List<Vector2> { Vector2.zero, p0, p1 };
                        var world = poly.Select(q => { var w = t.TransformPoint(new Vector3(q.x, 0f, q.y)); return new Vector2(w.x, w.z); }).ToList();
                        float y = t.position.y + h;
                        tb.Prism(world, y - 0.12f, y, Plank, Wood);
                        collider.Prism(world, y - 0.3f, y, 0, 0);
                    }
                    // joists under it
                    for (float x = -inner + 0.5f; x < inner; x += 1f)
                        tb.Beam(t.TransformPoint(new Vector3(x, h - 0.2f, -inner * 0.95f)), t.TransformPoint(new Vector3(x, h - 0.2f, inner * 0.95f)), 0.16f, 0.18f, Wood);
                });
                Proxy(root, $"{name} floor {h:F1}", collider, WalkableProxy.Kind.Deck, 0.08f);
            }
            float topY = t.position.y + tower.Height;
            var line = tower.WorldLine();
            switch (tower.Top)
            {
                case TowerTop.Crenellations:
                {
                    var mb = new MeshBuilder(MasonryKit.StoneSlots);
                    for (int i = 0; i + 1 < line.Count; i++)
                    {
                        var a = line[i];
                        var b = line[i + 1];
                        var d = b - a;
                        d.y = 0f;
                        float len = d.magnitude;
                        for (float s = 0.2f; s + 0.7f < len; s += 1.4f)
                        {
                            var c = a + d / len * (s + 0.35f);
                            Block(mb, c, d / len, topY, topY + 0.8f, tower.Thickness, 0.7f, rng, 0.03f, 0.012f);
                            Solid(root, $"{name} merlon {i}.{Mathf.RoundToInt(s)}", new Vector3(c.x, topY + 0.4f, c.z), Quaternion.LookRotation(d / len), new Vector3(tower.Thickness, 0.8f, 0.7f));
                        }
                    }
                    Draw(root, name + " top", mb, tower.Wear);
                    break;
                }
                case TowerTop.ConeRoof:
                case TowerTop.PyramidRoof:
                {
                    float rise = tower.Radius * 1.2f;
                    var roof = new MeshBuilder();
                    var go = TimberBuildings.Frame(root, name + " roof", tb =>
                    {
                        var apex = t.position + Vector3.up * (tower.Height + rise);
                        int sides = tower.Top == TowerTop.PyramidRoof ? 4 : 24;
                        float eave = tower.Radius + 0.45f;
                        for (int i = 0; i < sides; i++)
                        {
                            float a0 = (i + (sides == 4 ? 0.5f : 0f)) * 2f * Mathf.PI / sides, a1 = (i + 1 + (sides == 4 ? 0.5f : 0f)) * 2f * Mathf.PI / sides;
                            float k = sides == 4 ? Mathf.Sqrt(2f) : 1f;
                            var e0 = t.TransformPoint(new Vector3(Mathf.Cos(a0) * eave * k, tower.Height - 0.25f, Mathf.Sin(a0) * eave * k));
                            var e1 = t.TransformPoint(new Vector3(Mathf.Cos(a1) * eave * k, tower.Height - 0.25f, Mathf.Sin(a1) * eave * k));
                            tb.Quad(e0, apex, apex, e1, new Vector2(0f, 0f), new Vector2(0.5f, rise), new Vector2(0.5f, rise), new Vector2(Vector3.Distance(e0, e1), 0f), Aged);
                            tb.Quad(e0, e1, apex, apex, new Vector2(0f, 0f), new Vector2(Vector3.Distance(e0, e1), 0f), new Vector2(0.5f, rise), new Vector2(0.5f, rise), Wood);
                            roof.Quad(e0, apex, apex, e1, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
                        }
                    });
                    var mesh = OrganicAssets.Save(roof.ToSingleMesh(name + " roof"));
                    var cover = new GameObject(name + " roof ceiling") { layer = CourseKit.Layer("Environment") };
                    cover.transform.SetParent(root, false);
                    cover.AddComponent<MeshCollider>().sharedMesh = mesh;
                    cover.AddComponent<CourseCeiling>();
                    break;
                }
            }
        }

        /// <summary>A walkway's stone piers (M21: <see cref="TimberSupport.StonePiers"/>): coursed stone from the ground up under it, solid.</summary>
        public static void Piers(Transform root, string name, IList<(Vector3 top, Vector3 across)> piers, float width, Func<Vector2, float> ground)
        {
            if (piers.Count == 0) return;
            var rng = new Random(TimberKit.Seed(name, 503));
            var mb = new MeshBuilder(MasonryKit.StoneSlots);
            for (int i = 0; i < piers.Count; i++)
            {
                var (top, across) = piers[i];
                float g = ground(new Vector2(top.x, top.z));
                if (float.IsNaN(g)) continue;
                var along = Vector3.Cross(across, Vector3.up).normalized;
                Courses(mb, top - across * (width * 0.5f + 0.2f), top + across * (width * 0.5f + 0.2f), g - 0.3f, top.y, 0.8f, rng, 0.32f);
                Solid(root, $"{name} pier {i + 1}", new Vector3(top.x, (g - 0.3f + top.y) * 0.5f, top.z), Quaternion.LookRotation(across), new Vector3(0.8f, top.y - g + 0.3f, width + 0.4f));
            }
            Draw(root, name + " piers", mb, StoneWear.Aged);
        }
    }
}
