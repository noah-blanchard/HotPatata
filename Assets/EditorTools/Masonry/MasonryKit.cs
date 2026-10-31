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
    /// Draws a wall (ARCHITECTURE §27.7, docs/LEVEL_AUTHORING.md §30): stone laid in courses along its line (dry-stone, mortared, ruined,
    /// retaining; a mixed wall's base), timber through the timber kit (palisade, planks, stacked logs, wattle, a half-timbered frame:
    /// it freezes like the rest of the timber), fences (rails, pickets, rope on posts, a hedge), its cap and its openings' frames; its
    /// collision as invisible boxes along the line, from its foot to its top, cut round its openings (<see cref="Solids"/>); and the
    /// kit in its openings (<see cref="PlaceKit"/>). Called by <see cref="LevelPieceBuilder"/>.
    /// </summary>
    public static class MasonryKit
    {
        // the stone mesh's slots: the stones, the core between them (mortar, or a dark fill in a dry-stone wall), plaster panels
        public const int Stone = 0, CoreSlot = 1, Plaster = 2, StoneSlots = 3;
        const int Plank = TimberKit.Plank, Aged = TimberKit.PlankAged, Wood = TimberKit.Timber, Iron = TimberKit.Iron, Rope = TimberKit.Rope;

        /// <summary>A collision box is at most this long (m); where a ruined top steps, as short as its steps.</summary>
        public const float MaxSolid = 4f;
        const float Column = 0.25f;   // the collision's sampling along the line (m)

        public static string StoneMaterial(WallShape wall) => wall.Wear == StoneWear.Mossy ? "Nature_MossyRock"
            : wall.Style is WallStyle.Mortared or WallStyle.HalfTimbered or WallStyle.StoneBaseTimberTop or WallStyle.StoneTimberCap
                ? (wall.Wear == StoneWear.New ? "Nature_Limestone" : "Nature_Cliff")
                : (wall.Wear == StoneWear.New ? "Nature_Limestone" : "Nature_Granite");

        public static string CoreMaterial(WallShape wall) => wall.Style == WallStyle.Mortared || wall.Family == WallFamily.Mixed ? "Nature_Plaster" : "Nature_CaveRock";

        // ------------------------------------------------------------------ the line

        /// <summary>One straight run of the wall's line: its ends, where it starts along the wall, its length, along and outward (right).</summary>
        public struct Run
        {
            public Vector3 a, b, along, right;
            public float start, length;

            public Vector3 At(float s, float up) => a + along * (s - start) + Vector3.up * (Mathf.Lerp(a.y, b.y, Mathf.Clamp01((s - start) / Mathf.Max(1e-4f, length))) - a.y + up);
        }

        public static List<Run> Runs(WallShape wall)
        {
            var line = wall.WorldLine();
            var runs = new List<Run>();
            float s = 0f;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var d = line[i + 1] - line[i];
                var flat = new Vector3(d.x, 0f, d.z);
                if (flat.magnitude < 0.05f) continue;
                runs.Add(new Run
                {
                    a = line[i], b = line[i + 1], start = s, length = flat.magnitude,
                    along = flat.normalized, right = Vector3.Cross(Vector3.up, flat.normalized),
                });
                s += flat.magnitude;
            }
            return runs;
        }

        // ------------------------------------------------------------------ heights

        /// <summary>The wall's top above its foot at <paramref name="s"/> metres along it: its height, lowered by a ruined crest and its breaches.</summary>
        public static float Top(WallShape wall, float s)
        {
            float top = Full(wall);
            if (wall.Style == WallStyle.Ruined && wall.Crest > 0f)
            {
                // a ragged top: low noise, its lowest at height - crest
                float n = Mathf.PerlinNoise(s * 0.22f + wall.Seed * 7.1f, 3.3f) * 0.75f + Mathf.PerlinNoise(s * 0.9f, wall.Seed * 1.7f) * 0.25f;
                top -= wall.Crest * Mathf.Clamp01(n * 1.25f);
            }
            foreach (var b in wall.Breaches)
            {
                float d = Mathf.Abs(s - b.along);
                if (d < b.width * 0.5f) top = Mathf.Min(top, Full(wall) - b.depth * (0.5f + 0.5f * Mathf.Cos(d / (b.width * 0.5f) * Mathf.PI)));
            }
            return Mathf.Max(0.2f, top);
        }

        /// <summary>The wall's top where nothing lowers it: its height (a hedge is a fence's height at most).</summary>
        public static float Full(WallShape wall) => wall.Style == WallStyle.Hedge ? Mathf.Min(wall.Height, WallShape.FenceHeight + 0.6f) : wall.Height;

        /// <summary>The wall's locked height: its lowest top along it (a breach, a ruined crest), what a lock or a climb is judged by.</summary>
        public static float LockedHeight(WallShape wall)
        {
            float length = wall.Length;
            if (length <= 0f) return 0f;
            float low = float.MaxValue;
            for (float s = 0f; s <= length; s += Column) low = Mathf.Min(low, SolidTop(wall, s));
            return low;
        }

        /// <summary>The height its collision reaches: a fence to its top rail, a hedge to a fence's height, any other wall to its top.</summary>
        public static float SolidTop(WallShape wall, float s) => wall.Style == WallStyle.Hedge ? WallShape.FenceHeight : Top(wall, s);

        /// <summary>What a player does at a wall this high: mantle it, climb it (a jump and a mantle), or not (a lock).</summary>
        public static string Reads(float height) => height < WallShape.MantleHeight ? "mantled" : height < WallShape.LockHeight ? "climbed (a jump and a mantle)" : "a lock (no climb)";

        // ------------------------------------------------------------------ openings

        /// <summary>The top of opening <paramref name="o"/> above the wall's foot at <paramref name="s"/>, or -1 outside it (a breach: up to the sky).</summary>
        public static float OpeningTop(WallOpening o, float s)
        {
            float dx = Mathf.Abs(s - o.along), half = o.width * 0.5f;
            if (dx >= half) return -1f;
            switch (o.shape)
            {
                case OpeningShape.Breach: return float.MaxValue;
                case OpeningShape.RoundArch:
                {
                    float spring = Mathf.Max(0.3f, o.height - half);
                    return o.sill + spring + Mathf.Sqrt(Mathf.Max(0f, half * half - dx * dx)) * Mathf.Min(1f, (o.height - spring) / half);
                }
                case OpeningShape.PointedArch:
                {
                    float rise = Mathf.Min(o.height * 0.5f, o.width * 0.75f), spring = o.height - rise;
                    return o.sill + spring + rise * Mathf.Pow(1f - dx / half, 0.55f);
                }
                default: return o.sill + o.height;
            }
        }

        /// <summary>Where opening <paramref name="o"/> is open at <paramref name="up"/> over the foot (an arch narrows toward its crown).</summary>
        public static (float l, float r) OpeningSpan(WallOpening o, float up)
        {
            float half = o.width * 0.5f, reach = 0f;
            for (float d = 0f; d < half; d += 0.05f)
            {
                if (OpeningTop(o, o.along + d) <= up) break;
                reach = d + 0.05f;
            }
            reach = Mathf.Min(half, reach);
            return (o.along - reach, o.along + reach);
        }

        /// <summary>True when (s, up) is in an opening of the wall.</summary>
        public static bool InOpening(WallShape wall, float s, float up)
        {
            foreach (var o in wall.Openings)
            {
                float top = OpeningTop(o, s);
                if (top >= 0f && up > o.sill && up < top) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the build

        public static void Build(WallShape wall, Transform root, string name)
        {
            var runs = Runs(wall);
            if (runs.Count == 0) return;
            var rng = new Random(TimberKit.Seed(wall.PieceId ?? name, wall.Seed + 211));
            var stone = new MeshBuilder(StoneSlots);
            float full = Full(wall);
            switch (wall.Family)
            {
                case WallFamily.Stone:
                    foreach (var run in runs) Courses(wall, stone, run, 0f, full, rng);
                    if (wall.Cap == WallCap.Coping) Coping(wall, stone, runs, rng);
                    break;
                case WallFamily.Mixed:
                {
                    float band = wall.Style == WallStyle.StoneTimberCap ? full - 0.25f : Mathf.Min(1.2f, full * 0.45f);
                    if (wall.Style == WallStyle.HalfTimbered) band = Mathf.Min(0.6f, full * 0.25f);
                    foreach (var run in runs) Courses(wall, stone, run, 0f, band, rng);
                    TimberBuildings.Frame(root, name + " timber", mb =>
                    {
                        foreach (var run in runs)
                        {
                            if (wall.Style == WallStyle.StoneBaseTimberTop) Planks(wall, mb, run, band, full, rng);
                            else if (wall.Style == WallStyle.HalfTimbered) HalfTimber(wall, mb, stone, run, band, full);
                            else using (ElementRecorder.Tag("Cap", TimberElementKind.Beam, TimberWalk.None, solid: false))
                                mb.Beam(run.At(run.start, full - 0.12f), run.At(run.start + run.length, full - 0.12f), wall.Thickness + 0.1f, 0.24f, Wood);
                        }
                    });
                    break;
                }
                case WallFamily.Timber:
                    TimberBuildings.Frame(root, name + " timber", mb =>
                    {
                        foreach (var run in runs)
                            switch (wall.Style)
                            {
                                case WallStyle.Palisade: Palisade(wall, mb, run, rng); break;
                                case WallStyle.StackedLogs: StackedLogs(wall, mb, run, rng); break;
                                case WallStyle.Wattle: Wattle(wall, mb, run, rng); break;
                                default: Planks(wall, mb, run, 0f, full, rng); break;
                            }
                        if (wall.Cap == WallCap.Timber)
                            using (ElementRecorder.Tag("Cap", TimberElementKind.Beam, TimberWalk.None, solid: false))
                                foreach (var run in runs) mb.Beam(run.At(run.start, full + 0.08f), run.At(run.start + run.length, full + 0.08f), wall.Thickness + 0.06f, 0.16f, Wood);
                    });
                    break;
                case WallFamily.Fence:
                    if (wall.Style == WallStyle.Hedge) Hedge(wall, root, name, runs, rng);
                    else TimberBuildings.Frame(root, name + " fence", mb => { foreach (var run in runs) Fence(wall, mb, run, rng); });
                    break;
            }
            Frames(wall, root, name, stone, runs, rng);
            if (stone.Triangles > 0)
            {
                var mesh = OrganicAssets.Save(stone.ToMesh(name + " stone", out var slots));
                var go = new GameObject(name + " stone") { layer = CourseKit.Layer("Environment") };
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var names = new[] { StoneMaterial(wall), CoreMaterial(wall), "Nature_Plaster" };
                go.AddComponent<MeshRenderer>().sharedMaterials = slots.Select(sl => NatureMaterialBuilder.Load(names[sl])).ToArray();
                go.AddComponent<CourseDecoration>();
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            Solids(wall, root, name, runs);
        }

        // ------------------------------------------------------------------ stone

        /// <summary>
        /// Courses of stones along <paramref name="run"/> from <paramref name="y0"/> to <paramref name="y1"/> over its foot (cut by its top and
        /// its openings), staggered, each its own size; a core behind them (mortar or a dark fill) so no light shows through the joints.
        /// </summary>
        static void Courses(WallShape wall, MeshBuilder mb, Run run, float y0, float y1, Random rng)
        {
            bool mortared = wall.Style == WallStyle.Mortared || wall.Family == WallFamily.Mixed;
            float t = wall.Thickness;
            float y = y0 - 0.15f;
            int course = 0;
            while (y < y1 - 0.08f)
            {
                float h = mortared ? 0.3f + R(rng, -0.03f, 0.03f) : R(rng, 0.22f, 0.42f);
                h = Mathf.Min(h, y1 - y);
                float s = run.start - (course % 2 == 0 ? 0f : R(rng, 0.15f, 0.35f));
                while (s < run.start + run.length)
                {
                    float len = mortared ? 0.6f + R(rng, -0.08f, 0.08f) : R(rng, 0.35f, 0.85f);
                    float s0 = Mathf.Max(s, run.start), s1 = Mathf.Min(s + len, run.start + run.length);
                    s += len;
                    if (s1 - s0 < 0.12f) continue;
                    float mid = (s0 + s1) * 0.5f;
                    float top = Mathf.Min(y + h, Mathf.Min(y1, Top(wall, mid)));
                    if (top - y < 0.08f) continue;
                    // cut by an opening: the part left of it, the part right of it (an arch's stones are laid by its frame)
                    var o = wall.Openings.FirstOrDefault(op => op.width > 0f && OpeningTop(op, mid) >= 0f && y + 0.02f < OpeningTop(op, mid) && top > op.sill);
                    if (o.width > 0f)
                    {
                        var (l, r) = OpeningSpan(o, y + 0.02f);
                        if (s0 < l - 0.12f) StoneAt(mb, run, s0, l, y, top, t, mortared, rng);
                        if (s1 > r + 0.12f) StoneAt(mb, run, r, s1, y, top, t, mortared, rng);
                        // under a window's sill and over a flat lintel the course carries on
                        if (o.sill > y + 0.05f) StoneAt(mb, run, Mathf.Max(s0, l), Mathf.Min(s1, r), y, Mathf.Min(top, o.sill), t, mortared, rng);
                        continue;
                    }
                    StoneAt(mb, run, s0, s1, y, top, t, mortared, rng);
                }
                // the core: a slab inside the stones, its top under the course's
                Core(wall, mb, run, y, y + h, t);
                y += h;
                course++;
            }
        }

        static void Core(WallShape wall, MeshBuilder mb, Run run, float y0, float y1, float t)
        {
            float s = run.start, end = run.start + run.length;
            while (s < end)
            {
                float s1 = Mathf.Min(end, s + 0.5f), mid = (s + s1) * 0.5f;
                float top = Mathf.Min(y1, Top(wall, mid) - 0.05f);
                if (top > y0 && !InOpening(wall, mid, (y0 + top) * 0.5f))
                {
                    var c = run.At(mid, (y0 + top) * 0.5f);
                    mb.Box(c, Quaternion.LookRotation(run.right, Vector3.up), new Vector3(s1 - s, top - y0, t * 0.82f), CoreSlot);
                }
                s = s1;
            }
        }

        static void StoneAt(MeshBuilder mb, Run run, float s0, float s1, float y0, float y1, float t, bool dressed, Random rng)
        {
            float gap = dressed ? 0.025f : 0.04f;
            float len = s1 - s0 - gap, h = y1 - y0 - gap;
            if (len < 0.08f || h < 0.06f) return;
            var size = new Vector3(t * R(rng, 0.92f, 1.04f), h, len);
            var c = run.At((s0 + s1) * 0.5f, (y0 + y1) * 0.5f) + run.right * R(rng, -0.03f, 0.03f);
            var rot = Quaternion.LookRotation(run.along, Vector3.up) * Quaternion.Euler(R(rng, -2f, 2f), R(rng, -3f, 3f), R(rng, -2f, 2f));
            Block(mb, c, rot, size, dressed ? 0.025f : 0.06f, dressed ? 0.01f : 0.035f, rng.Next(), Stone);
        }

        /// <summary>
        /// A stone: a box (x across, y up, z along) with its edges bevelled by <paramref name="bevel"/> and each face pushed by up to
        /// <paramref name="rough"/>. Its faces are laid in quads (faceted, as cut stone reads).
        /// </summary>
        public static void Block(MeshBuilder mb, Vector3 center, Quaternion rot, Vector3 size, float bevel, float rough, int seed, int slot)
        {
            var half = size * 0.5f;
            float b = Mathf.Min(bevel, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.45f);
            var inner = half - Vector3.one * b;
            const int N = 3;
            Vector3 P(Vector3 cube)
            {
                var p = Vector3.Scale(cube, half);
                var clamped = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                var d = p - clamped;
                var q = clamped + (d.sqrMagnitude > 1e-8f ? d.normalized * b : Vector3.zero);
                float n = Mathf.PerlinNoise(q.x * 3.1f + seed % 97, q.z * 3.1f + q.y * 2.3f + seed % 89) - 0.5f;
                return center + rot * (q + q.normalized * n * rough * 2f);
            }
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var n in axes)
            {
                var u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                var v = Vector3.Cross(n, u);
                for (int i = 0; i < N; i++)
                    for (int j = 0; j < N; j++)
                    {
                        float u0 = -1f + 2f * i / N, u1 = -1f + 2f * (i + 1) / N, v0 = -1f + 2f * j / N, v1 = -1f + 2f * (j + 1) / N;
                        var a = P(n + u * u0 + v * v0);
                        var bb = P(n + u * u1 + v * v0);
                        var c = P(n + u * u1 + v * v1);
                        var d = P(n + u * u0 + v * v1);
                        // world metres for the rock's texture, the face's own plane
                        Vector2 UV(Vector3 w) => new Vector2(Vector3.Dot(w, rot * u), Vector3.Dot(w, rot * v)) * 0.5f;
                        var face = rot * n;
                        if (Vector3.Dot(Vector3.Cross(bb - a, c - a), face) >= 0f) mb.Quad(a, bb, c, d, UV(a), UV(bb), UV(c), UV(d), slot);
                        else mb.Quad(a, d, c, bb, UV(a), UV(d), UV(c), UV(bb), slot);
                    }
            }
        }

        /// <summary>Flat coping stones over the top, a little wider than the wall.</summary>
        static void Coping(WallShape wall, MeshBuilder mb, List<Run> runs, Random rng)
        {
            foreach (var run in runs)
                for (float s = run.start; s < run.start + run.length - 0.1f; s += 0.7f)
                {
                    float s1 = Mathf.Min(s + 0.7f, run.start + run.length), mid = (s + s1) * 0.5f;
                    if (InOpening(wall, mid, Top(wall, mid) - 0.05f) && wall.Openings.Any(o => o.shape == OpeningShape.Breach && Mathf.Abs(mid - o.along) < o.width * 0.5f)) continue;
                    var c = run.At(mid, Top(wall, mid) + 0.08f);
                    Block(mb, c, Quaternion.LookRotation(run.along, Vector3.up), new Vector3(wall.Thickness + 0.14f, 0.16f, s1 - s - 0.03f), 0.03f, 0.01f, rng.Next(), Stone);
                }
        }

        // ------------------------------------------------------------------ timber

        static float R(Random rng, float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());

        /// <summary>The spans of <paramref name="run"/> between its openings that reach down to <paramref name="y"/> (a log or a board there is cut round them).</summary>
        static IEnumerable<(float s0, float s1)> Spans(WallShape wall, Run run, float y)
        {
            float s = run.start, end = run.start + run.length;
            foreach (var o in wall.Openings.Where(o => o.along + o.width * 0.5f > run.start && o.along - o.width * 0.5f < end && y > o.sill && y < OpeningTop(o, o.along)).OrderBy(o => o.along))
            {
                float l = o.along - o.width * 0.5f;
                if (l > s) yield return (s, l);
                s = Mathf.Max(s, o.along + o.width * 0.5f);
            }
            if (end > s) yield return (s, end);
        }

        /// <summary>Sharpened logs side by side, two rails behind them.</summary>
        static void Palisade(WallShape wall, MeshBuilder mb, Run run, Random rng)
        {
            float r = Mathf.Clamp(wall.Thickness * 0.5f, 0.1f, 0.2f), full = Full(wall);
            using (ElementRecorder.Tag("Logs", TimberElementKind.Post, TimberWalk.None, solid: false))
                for (float s = run.start + r; s < run.start + run.length; s += r * 2f)
                {
                    var o = wall.Openings.FirstOrDefault(op => op.width > 0f && OpeningTop(op, s) >= 0f);
                    float bottom = 0f, top = full + R(rng, -0.15f, 0.2f);
                    if (o.width > 0f)
                    {
                        if (o.sill < 0.3f) { bottom = Mathf.Min(OpeningTop(o, s), 50f); if (bottom > top - 0.3f) continue; }
                        else top = o.sill;
                    }
                    var foot = run.At(s, bottom - (bottom > 0f ? 0f : 0.3f));
                    var head = run.At(s, top);
                    bool sharp = wall.Cap != WallCap.Timber;
                    mb.Tube(new[] { foot, head, head + Vector3.up * (sharp ? 0.35f : 0.02f) }, new[] { r * 0.95f, r * 0.9f, sharp ? 0.03f : r * 0.85f }, 8, Wood, true, s * 0.37f);
                }
            using (ElementRecorder.Tag("Rails", TimberElementKind.Beam, TimberWalk.None, solid: false))
                foreach (float y in new[] { 0.45f, full - 0.6f })
                    foreach (var (s0, s1) in Spans(wall, run, y))
                        mb.Beam(run.At(s0, y) - run.right * (r + 0.07f), run.At(s1, y) - run.right * (r + 0.07f), 0.14f, 0.18f, Wood);
        }

        /// <summary>Boards upright between <paramref name="y0"/> and <paramref name="y1"/>, a rail at the bottom and the top, posts at the ends and round openings.</summary>
        static void Planks(WallShape wall, MeshBuilder mb, Run run, float y0, float y1, Random rng)
        {
            var rot = Quaternion.LookRotation(run.right, Vector3.up);
            using (ElementRecorder.Tag("Boards", TimberElementKind.Plank, TimberWalk.None, solid: false))
                for (float s = run.start + 0.12f; s < run.start + run.length; s += 0.24f)
                {
                    var o = wall.Openings.FirstOrDefault(op => op.width > 0f && OpeningTop(op, s) >= 0f);
                    void Board(float a, float b)
                    {
                        if (b - a < 0.05f) return;
                        mb.Box(run.At(s, (a + b) * 0.5f), rot, new Vector3(0.23f, b - a, 0.05f), rng.NextDouble() < 0.25 ? Aged : Plank, new Vector2(s * 0.31f, 0f));
                    }
                    if (o.width > 0f)
                    {
                        Board(y0 - 0.05f, Mathf.Max(y0, o.sill));
                        Board(Mathf.Min(y1, OpeningTop(o, s)), y1);
                    }
                    else Board(y0 - 0.05f, y1);
                }
            using (ElementRecorder.Tag("Rails", TimberElementKind.Beam, TimberWalk.None, solid: false))
                foreach (float y in new[] { y0 + 0.25f, y1 - 0.25f })
                    foreach (var (s0, s1) in Spans(wall, run, y))
                        mb.Beam(run.At(s0, y) - run.right * 0.1f, run.At(s1, y) - run.right * 0.1f, 0.12f, 0.16f, Wood);
            using (ElementRecorder.Tag("Posts", TimberElementKind.Post, TimberWalk.None, solid: false))
            {
                mb.Beam(run.At(run.start, y0 - 0.3f), run.At(run.start, y1 + 0.08f), 0.2f, 0.2f, Wood);
                mb.Beam(run.At(run.start + run.length, y0 - 0.3f), run.At(run.start + run.length, y1 + 0.08f), 0.2f, 0.2f, Wood);
            }
        }

        /// <summary>Logs laid one on another, their ends past the corners.</summary>
        static void StackedLogs(WallShape wall, MeshBuilder mb, Run run, Random rng)
        {
            float r = Mathf.Clamp(wall.Thickness * 0.5f, 0.12f, 0.22f), full = Full(wall);
            using (ElementRecorder.Tag("Logs", TimberElementKind.Beam, TimberWalk.None, solid: false))
                for (float y = r * 0.8f; y < full; y += r * 1.8f)
                    foreach (var (s0, s1) in Spans(wall, run, y))
                    {
                        float e0 = Mathf.Approximately(s0, run.start) ? 0.3f : 0f, e1 = Mathf.Approximately(s1, run.start + run.length) ? 0.3f : 0f;
                        mb.Tube(new[] { run.At(s0 - e0, y), run.At(s1 + e1, y) }, new[] { r * R(rng, 0.9f, 1.05f), r * R(rng, 0.85f, 1f) }, 9, Wood, true, y);
                    }
        }

        /// <summary>Stakes, and rods woven in and out between them.</summary>
        static void Wattle(WallShape wall, MeshBuilder mb, Run run, Random rng)
        {
            float full = Full(wall);
            const float StakeEvery = 0.5f;
            using (ElementRecorder.Tag("Stakes", TimberElementKind.Post, TimberWalk.None, solid: false))
                for (float s = run.start; s <= run.start + run.length + 0.01f; s += StakeEvery)
                    if (!wall.Openings.Any(o => OpeningTop(o, s) >= 0f && o.sill < 0.3f))
                        mb.Tube(new[] { run.At(s, -0.3f), run.At(s, full + 0.15f) }, new[] { 0.045f, 0.035f }, 6, Wood);
            using (ElementRecorder.Tag("Rods", TimberElementKind.Rope, TimberWalk.None, solid: false))
                for (float y = 0.15f; y < full; y += 0.09f)
                    foreach (var (s0, s1) in Spans(wall, run, y))
                    {
                        int k = Mathf.Max(2, Mathf.CeilToInt((s1 - s0) / (StakeEvery * 0.5f)));
                        var path = new List<Vector3>();
                        var radii = new List<float>();
                        for (int i = 0; i <= k; i++)
                        {
                            float s = Mathf.Lerp(s0, s1, i / (float)k);
                            float weave = Mathf.Sin((s - run.start) / StakeEvery * Mathf.PI + y * 11f) * 0.055f;
                            path.Add(run.At(s, y) + run.right * weave);
                            radii.Add(0.022f);
                        }
                        mb.Tube(path, radii, 5, Wood);
                    }
        }

        /// <summary>A half-timbered wall over its stone band: sill, posts, braces and a plate, plaster in the panels.</summary>
        static void HalfTimber(WallShape wall, MeshBuilder mb, MeshBuilder stone, Run run, float y0, float y1)
        {
            const float Bay = 1.6f;
            int bays = Mathf.Max(1, Mathf.RoundToInt(run.length / Bay));
            float bay = run.length / bays;
            using (ElementRecorder.Tag("Frame", TimberElementKind.Beam, TimberWalk.None, solid: false))
            {
                foreach (var (s0, s1) in Spans(wall, run, y0 + 0.1f)) mb.Beam(run.At(s0, y0 + 0.1f), run.At(s1, y0 + 0.1f), wall.Thickness + 0.04f, 0.2f, Wood);
                foreach (var (s0, s1) in Spans(wall, run, y1 - 0.1f)) mb.Beam(run.At(s0, y1 - 0.1f), run.At(s1, y1 - 0.1f), wall.Thickness + 0.04f, 0.2f, Wood);
                for (int k = 0; k <= bays; k++)
                {
                    float s = run.start + k * bay;
                    if (wall.Openings.Any(o => OpeningTop(o, s) >= 0f && o.sill < 0.3f)) continue;
                    mb.Beam(run.At(s, y0), run.At(s, y1), 0.2f, wall.Thickness + 0.04f, Wood);
                    if (k < bays && k % 2 == 0 && !wall.Openings.Any(o => Mathf.Abs(s + bay * 0.5f - o.along) < o.width * 0.5f + bay * 0.5f))
                        mb.Beam(run.At(s, y0 + 0.1f), run.At(s + bay, y1 - 0.1f), 0.16f, wall.Thickness + 0.02f, Wood);
                }
            }
            // plaster panels, a little inside the timber's faces, cut round the openings
            for (float s = run.start; s < run.start + run.length; s += 0.4f)
            {
                float s1 = Mathf.Min(s + 0.4f, run.start + run.length), mid = (s + s1) * 0.5f;
                var o = wall.Openings.FirstOrDefault(op => op.width > 0f && OpeningTop(op, mid) >= 0f);
                void Panel(float a, float b)
                {
                    if (b - a < 0.05f) return;
                    stone.Box(run.At(mid, (a + b) * 0.5f), Quaternion.LookRotation(run.right, Vector3.up), new Vector3(s1 - s, b - a, wall.Thickness - 0.06f), Plaster);
                }
                if (o.width > 0f) { Panel(y0, Mathf.Max(y0, o.sill)); Panel(Mathf.Min(y1, OpeningTop(o, mid)), y1); }
                else Panel(y0, y1);
            }
        }

        // ------------------------------------------------------------------ fences

        static void Fence(WallShape wall, MeshBuilder mb, Run run, Random rng)
        {
            float h = Full(wall);
            int posts = Mathf.Max(1, Mathf.CeilToInt(run.length / wall.PostSpacing));
            var at = Enumerable.Range(0, posts + 1).Select(k => run.start + run.length * k / posts).ToList();
            using (ElementRecorder.Tag("Posts", TimberElementKind.Post, TimberWalk.None, solid: false))
                foreach (float s in at)
                    mb.Beam(run.At(s, -0.4f), run.At(s, h + 0.08f), 0.14f, 0.14f, Wood);
            var rails = wall.Style == WallStyle.RailFence ? new[] { 0.45f, h * 0.75f, h - 0.06f } : new[] { 0.25f, h - 0.2f };
            if (wall.Style == WallStyle.RopeOnPosts)
            {
                using (ElementRecorder.Tag("Rope", TimberElementKind.Rope, TimberWalk.None, solid: false))
                    for (int k = 0; k < posts; k++)
                        foreach (float y in new[] { h * 0.55f, h - 0.08f })
                        {
                            if (wall.Openings.Any(o => o.width > 0f && Mathf.Abs((at[k] + at[k + 1]) * 0.5f - o.along) < o.width * 0.5f)) continue;
                            var sag = Curves.Sag(run.At(at[k], y), run.At(at[k + 1], y), 0.12f, 10);
                            mb.Tube(sag, sag.Select(_ => 0.025f).ToList(), 5, Rope);
                        }
                return;
            }
            using (ElementRecorder.Tag("Rails", TimberElementKind.Beam, TimberWalk.None, solid: false))
                foreach (float y in rails)
                    foreach (var (s0, s1) in Spans(wall, run, y))
                        mb.Beam(run.At(s0, y) + run.right * 0.09f, run.At(s1, y) + run.right * 0.09f, 0.06f, 0.1f, wall.Style == WallStyle.RailFence ? Wood : Plank);
            if (wall.Style != WallStyle.Pickets) return;
            using (ElementRecorder.Tag("Pickets", TimberElementKind.Plank, TimberWalk.None, solid: false))
                for (float s = run.start + 0.08f; s < run.start + run.length; s += 0.13f)
                {
                    if (wall.Openings.Any(o => OpeningTop(o, s) >= 0f && o.sill < 0.3f)) continue;
                    float top = h + R(rng, -0.02f, 0.04f);
                    mb.Box(run.At(s, top * 0.5f) + run.right * 0.14f, Quaternion.LookRotation(run.right, Vector3.up), new Vector3(0.08f, top, 0.025f), Plank, new Vector2(s, 0f));
                }
        }

        /// <summary>A hedge: shrubs along the line, drawn only (its collision is the wall's box, a fence's height).</summary>
        static void Hedge(WallShape wall, Transform root, string name, List<Run> runs, Random rng)
        {
            var shrubs = new[] { "shrub_02", "shrub_03", "shrub_04" }.Select(PropLibraryBuilder.PrefabPath).Where(p => p != null)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p != null).ToArray();
            if (shrubs.Length == 0) return;
            var group = new GameObject(name + " hedge");
            group.transform.SetParent(root, false);
            float h = Full(wall);
            foreach (var run in runs)
                for (float s = run.start + 0.4f; s < run.start + run.length; s += 0.9f)
                {
                    if (wall.Openings.Any(o => OpeningTop(o, s) >= 0f && o.sill < 0.3f)) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(shrubs[rng.Next(shrubs.Length)], root.gameObject.scene);
                    go.transform.SetParent(group.transform, false);
                    go.transform.SetPositionAndRotation(run.At(s, 0f) + run.right * R(rng, -0.15f, 0.15f), Quaternion.Euler(0f, R(rng, 0f, 360f), 0f));
                    var bounds = go.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    float k = h / Mathf.Max(0.1f, bounds.size.y) * R(rng, 0.9f, 1.1f);
                    go.transform.localScale = new Vector3(k * Mathf.Max(1f, wall.Thickness / Mathf.Max(0.1f, bounds.size.x * k) * 1.2f), k, k);
                    foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                }
        }

        // ------------------------------------------------------------------ opening frames

        /// <summary>Each opening's frame: stone voussoirs round an arch or a lintel stone over a flat top, or timber jambs and a lintel.</summary>
        static void Frames(WallShape wall, Transform root, string name, MeshBuilder stone, List<Run> runs, Random rng)
        {
            var timber = wall.Openings.Where(o => o.frame == OpeningFrame.Timber && o.shape != OpeningShape.Breach).ToList();
            foreach (var o in wall.Openings.Where(o => o.frame == OpeningFrame.Stone && o.shape != OpeningShape.Breach))
            {
                var run = runs.FirstOrDefault(r => o.along >= r.start && o.along <= r.start + r.length);
                if (run.length <= 0f) continue;
                float t = wall.Thickness + 0.06f, half = o.width * 0.5f;
                var rot = Quaternion.LookRotation(run.along, Vector3.up);
                // a ruined top that falls below the frame takes the frame's stones with it
                if (Top(wall, o.along) < OpeningTop(o, o.along) + 0.3f) continue;
                if (o.shape == OpeningShape.Rect || o.shape == OpeningShape.Lintel)
                {
                    Block(stone, run.At(o.along, o.sill + o.height + 0.2f), rot, new Vector3(t, 0.4f, o.width + 0.7f), 0.03f, 0.01f, rng.Next(), Stone);
                }
                else
                {
                    // voussoirs: wedges along the arch's curve, each turned to it
                    int count = Mathf.Max(5, Mathf.RoundToInt(o.width * 3f) | 1);
                    for (int k = 0; k < count; k++)
                    {
                        float s0 = o.along - half + o.width * k / count, s1 = o.along - half + o.width * (k + 1) / count;
                        float y0 = OpeningTop(o, Mathf.Clamp(s0, o.along - half + 0.01f, o.along + half - 0.01f)), y1 = OpeningTop(o, Mathf.Clamp(s1, o.along - half + 0.01f, o.along + half - 0.01f));
                        var mid = run.At((s0 + s1) * 0.5f, (y0 + y1) * 0.5f);
                        var tangent = (run.At(s1, y1) - run.At(s0, y0));
                        var normal = Vector3.Cross(tangent, run.right).normalized;
                        if (normal.y < 0f) normal = -normal;
                        Block(stone, mid + normal * 0.22f, Quaternion.LookRotation(tangent.normalized, normal), new Vector3(t, 0.44f, tangent.magnitude - 0.02f), 0.03f, 0.012f, rng.Next(), Stone);
                    }
                }
                // quoins up the jambs
                foreach (float e in new[] { -1f, 1f })
                    for (float y = o.sill; y < o.sill + o.height - Mathf.Min(half, o.height * 0.4f) * (o.shape == OpeningShape.Rect || o.shape == OpeningShape.Lintel ? 0f : 1f) - 0.1f; y += 0.42f)
                        Block(stone, run.At(o.along + e * (half + 0.2f), y + 0.2f), rot, new Vector3(t, 0.38f, 0.38f + (Mathf.RoundToInt(y / 0.42f) % 2) * 0.2f), 0.03f, 0.01f, rng.Next(), Stone);
            }
            if (timber.Count == 0) return;
            TimberBuildings.Frame(root, name + " frames", mb =>
            {
                using var tag = ElementRecorder.Tag("Frames", TimberElementKind.Post, TimberWalk.None, solid: false);
                foreach (var o in timber)
                {
                    var run = runs.FirstOrDefault(r => o.along >= r.start && o.along <= r.start + r.length);
                    if (run.length <= 0f) continue;
                    float half = o.width * 0.5f + 0.11f, top = OpeningTop(o, o.along);
                    foreach (float e in new[] { -1f, 1f })
                        mb.Beam(run.At(o.along + e * half, o.sill - (o.sill > 0.05f ? 0.1f : 0.3f)), run.At(o.along + e * half, top + 0.2f), 0.2f, wall.Thickness + 0.06f, Wood);
                    mb.Beam(run.At(o.along - half - 0.3f, top + 0.12f), run.At(o.along + half + 0.3f, top + 0.12f), wall.Thickness + 0.08f, 0.24f, Wood);
                    if (o.sill > 0.05f) mb.Beam(run.At(o.along - half - 0.15f, o.sill - 0.06f), run.At(o.along + half + 0.15f, o.sill - 0.06f), wall.Thickness + 0.08f, 0.12f, Wood);
                }
            });
        }

        // ------------------------------------------------------------------ collision

        /// <summary>
        /// The wall's collision: invisible boxes along its line, each a column of the same solid intervals (from the foot to its solid
        /// top, less its openings), merged up to <see cref="MaxSolid"/> long.
        /// </summary>
        public static void Solids(WallShape wall, Transform root, string name, List<Run> runs)
        {
            float t = wall.Style == WallStyle.Hedge || wall.Family == WallFamily.Fence ? Mathf.Max(0.2f, wall.Thickness) : wall.Thickness;
            int n = 0;
            foreach (var run in runs)
            {
                List<(float y0, float y1)> open = null;
                float openStart = run.start;
                void Close(float end)
                {
                    if (open == null) return;
                    foreach (var (y0, y1) in open)
                    {
                        float mid = (openStart + end) * 0.5f;
                        var c = run.At(mid, (y0 + y1) * 0.5f);
                        TimberBuildings.SolidInvisible(root, $"{name} solid {++n}", c, Quaternion.LookRotation(run.along, Vector3.up), new Vector3(t, y1 - y0, end - openStart));
                    }
                }
                for (float s = run.start; s < run.start + run.length - 1e-3f; s += Column)
                {
                    float s1 = Mathf.Min(s + Column, run.start + run.length), mid = (s + s1) * 0.5f;
                    var here = Intervals(wall, mid);
                    bool same = open != null && here.Count == open.Count && here.Zip(open, (a, b) => Mathf.Abs(a.y0 - b.y0) < 0.06f && Mathf.Abs(a.y1 - b.y1) < 0.06f).All(x => x)
                                && s - openStart < MaxSolid - 1e-3f;
                    if (same) continue;
                    Close(s);
                    open = here;
                    openStart = s;
                }
                Close(run.start + run.length);
            }
        }

        /// <summary>The solid heights of the wall's column at <paramref name="s"/>: from under its foot to its solid top, less its openings.</summary>
        static List<(float y0, float y1)> Intervals(WallShape wall, float s)
        {
            var list = new List<(float, float)> { (-0.3f, SolidTop(wall, s)) };
            foreach (var o in wall.Openings)
            {
                float top = OpeningTop(o, s);
                if (top < 0f) continue;
                var next = new List<(float, float)>();
                foreach (var (y0, y1) in list)
                {
                    if (o.sill > y0) next.Add((y0, Mathf.Min(y1, o.sill)));
                    if (top < y1) next.Add((Mathf.Max(y0, top), y1));
                }
                list = next.Where(i => i.Item2 - i.Item1 > 0.05f).ToList();
            }
            return list;
        }

        // ------------------------------------------------------------------ the kit in the openings

        /// <summary>The kit prefab a passage places when its opening names no look (paths without ".prefab").</summary>
        public static string DefaultLook(OpeningPassage passage) => passage switch
        {
            OpeningPassage.BombOnly => BombObstacleKitBuilder.Screen,
            OpeningPassage.RunnersOnly => BombObstacleKitBuilder.LaserCurtain,
            OpeningPassage.KitDoor => BombObstacleKitBuilder.Door,
            _ => null,
        };

        public static string LookOf(WallOpening o)
        {
            var path = string.IsNullOrEmpty(o.look) ? DefaultLook(o.passage) : o.look;
            return path != null && path.EndsWith(".prefab") ? path.Substring(0, path.Length - ".prefab".Length) : path;
        }

        /// <summary>
        /// Places (once) and fits the kit of each opening with a passage: in the level's Kit group, at the opening, sized to it. A kit
        /// already placed is moved and resized, never made again (its wiring holds); one of another look is left and reported.
        /// </summary>
        public static List<string> PlaceKit(WallShape wall)
        {
            var notes = new List<string>();
            var runs = Runs(wall);
            if (runs.Count == 0) return notes;
            var settings = LevelScenes.SettingsOf(wall.gameObject.scene);
            var parent = settings != null ? settings.Group(LevelGroup.Kit) : null;
            bool changed = false;
            for (int i = 0; i < wall.Openings.Count; i++)
            {
                var o = wall.Openings[i];
                if (o.passage == OpeningPassage.Free) continue;
                var run = runs.FirstOrDefault(r => o.along >= r.start && o.along <= r.start + r.length + 1e-3f);
                if (run.length <= 0f) { notes.Add($"opening {i + 1} is past the wall's end"); continue; }
                string look = LookOf(o);
                var rot = Quaternion.LookRotation(run.right, Vector3.up);
                var foot = run.At(o.along, o.sill);
                if (o.kit == null)
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(look + ".prefab") == null) { notes.Add($"opening {i + 1}: no kit prefab at {look}"); continue; }
                    var siblings = parent != null ? parent.Cast<Transform>().Select(t => t.name).ToArray() : Array.Empty<string>();
                    o.kit = CourseKit.Place(parent, look, ObjectNames.GetUniqueName(siblings, $"{wall.name} {o.passage}"), foot, rot);
                    Undo.RegisterCreatedObjectUndo(o.kit, "Wall kit");
                    wall.Openings[i] = o;
                    changed = true;
                }
                else
                {
                    var source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(o.kit);
                    if (!string.IsNullOrEmpty(source) && source != look + ".prefab")
                        notes.Add($"opening {i + 1}: its kit {o.kit.name} is not its look any more (Replace kit in the wall's inspector)");
                }
                Fit(o, o.kit, foot, rot, wall.Thickness);
            }
            if (changed) EditorUtility.SetDirty(wall);
            return notes;
        }

        /// <summary>Moves and sizes a placed kit to its opening: a screen and a curtain span it, a door fills it and opens up (or aside, as its look slides).</summary>
        public static void Fit(WallOpening o, GameObject kit, Vector3 foot, Quaternion rot, float thickness)
        {
            float w = o.width, h = o.shape == OpeningShape.Rect || o.shape == OpeningShape.Lintel || o.shape == OpeningShape.Breach ? o.height : Mathf.Max(1f, o.height - Mathf.Min(o.width * 0.5f, o.height * 0.4f) * 0.4f);
            Undo.RecordObject(kit.transform, "Fit kit");
            if (kit.GetComponent<SignalActuator>() is SignalActuator actuator)
            {
                var size = new Vector3(w, h, Mathf.Clamp(thickness * 0.6f, 0.25f, 0.6f));
                var before = actuator.Platform.GetComponentsInChildren<BoxCollider>(true).First(c => !c.isTrigger && c.GetComponent<KillZone>() == null).size;
                var open = actuator.WaypointOpen.localPosition - actuator.WaypointClosed.localPosition;
                bool slides = Mathf.Abs(open.x) > Mathf.Abs(open.y);
                kit.transform.SetPositionAndRotation(foot + Vector3.up * h * 0.5f, rot);
                BombObstacleKitBuilder.ResizeActuator(kit, size, slides ? new Vector3(Mathf.Sign(open.x) * (w + 0.3f), 0f, 0f) : new Vector3(0f, h + 0.4f, 0f));
                // a look's own static frame (round a free-standing door) is the wall's to give here
                foreach (var frame in kit.GetComponentsInChildren<CustomVisual>(true).Where(v => v.transform.parent == kit.transform))
                    frame.gameObject.SetActive(false);
                // a hand-made look is drawn for the kit's own size: scaled to the new one
                foreach (var visual in actuator.Platform.GetComponentsInChildren<CustomVisual>(true))
                {
                    var k = visual.transform.localScale;
                    visual.transform.localScale = new Vector3(k.x * size.x / Mathf.Max(0.01f, before.x), k.y * size.y / Mathf.Max(0.01f, before.y), k.z * size.z / Mathf.Max(0.01f, before.z));
                }
                foreach (var s in kit.GetComponentsInChildren<Component>(true).Where(c => c != null)) PrefabUtility.RecordPrefabInstancePropertyModifications(s);
                return;
            }
            kit.transform.SetPositionAndRotation(foot, rot);
            if (kit.GetComponentInChildren<BodyScreen>(true) != null) BombObstacleKitBuilder.ResizeScreen(kit, w, h);
            else BombObstacleKitBuilder.ResizeCurtain(kit, w, h);
            foreach (var t in kit.GetComponentsInChildren<Transform>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            foreach (var b in kit.GetComponentsInChildren<BoxCollider>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(b);
        }
    }
}
