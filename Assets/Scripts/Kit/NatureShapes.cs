using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HotPatata
{
    /// <summary>
    /// The generated shapes of the nature look (ARCHITECTURE §25.3), drawn by <see cref="KitSkin"/> for PatataWilds: a rough rock
    /// slab (<see cref="KitShape.RoughBox"/>), a bundle of logs (<see cref="KitShape.Logs"/>) and a planked box
    /// (<see cref="KitShape.Planks"/>). Like the bevel box, every vertex stays inside the kit box, so a visual never sticks out of
    /// the collider it stands for; the result only depends on the box size (deterministic, cached by KitSkin). UVs are in metres
    /// (the material's tile size scales them), with normals and tangents for the nature shader.
    /// </summary>
    public static class NatureShapes
    {
        // ------------------------------------------------------------------ noise

        static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ seed * 2654435761);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>3D value noise in 0..1.</summary>
        public static float Noise(Vector3 p, int seed)
        {
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = p.x - x0, fy = p.y - y0, fz = p.z - z0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);
            float L(int dx, int dy, int dz) => Hash(x0 + dx, y0 + dy, z0 + dz, seed);
            float a = Mathf.Lerp(Mathf.Lerp(L(0, 0, 0), L(1, 0, 0), fx), Mathf.Lerp(L(0, 1, 0), L(1, 1, 0), fx), fy);
            float b = Mathf.Lerp(Mathf.Lerp(L(0, 0, 1), L(1, 0, 1), fx), Mathf.Lerp(L(0, 1, 1), L(1, 1, 1), fx), fy);
            return Mathf.Lerp(a, b, fz);
        }

        static int SeedFor(Vector3 size) => Mathf.RoundToInt(size.x * 37f + size.y * 101f + size.z * 53f);

        sealed class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Indices = new List<int>();

            public Mesh ToMesh(string name, Vector3 scale, bool smooth)
            {
                Vector3 toLocal = new Vector3(1f / Mathf.Max(0.0001f, scale.x), 1f / Mathf.Max(0.0001f, scale.y), 1f / Mathf.Max(0.0001f, scale.z));
                var local = new List<Vector3>(Vertices.Count);
                foreach (var v in Vertices) local.Add(Vector3.Scale(v, toLocal));
                var mesh = new Mesh
                {
                    name = name,
                    hideFlags = HideFlags.HideAndDontSave,
                    indexFormat = local.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };
                mesh.SetVertices(local);
                mesh.SetUVs(0, Uvs);
                mesh.SetTriangles(Indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }

            /// <summary>A box of <paramref name="size"/> metres at <paramref name="center"/>, turned by <paramref name="rotation"/>; UVs in metres, along the box's longest side.</summary>
            public void Box(Vector3 center, Vector3 size, Quaternion rotation)
            {
                Vector3 h = size / 2f;
                for (int a = 0; a < 3; a++)
                {
                    int u = (a + 1) % 3, v = (a + 2) % 3;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
                        Vector3 P(float su, float sv) => Axis(a) * (s * h[a]) + Axis(u) * (su * h[u]) + Axis(v) * (sv * h[v]);
                        var quad = new[] { P(-1, -1), P(1, -1), P(1, 1), P(-1, 1) };
                        var normal = Axis(a) * s;
                        if (Vector3.Dot(Vector3.Cross(quad[1] - quad[0], quad[2] - quad[0]), normal) < 0f) System.Array.Reverse(quad);
                        int first = Vertices.Count;
                        // the longer of the face's two sides runs along U
                        bool uLong = size[u] >= size[v];
                        foreach (var p in quad)
                        {
                            Vertices.Add(center + rotation * p);
                            Uvs.Add(uLong ? new Vector2(p[u], p[v]) : new Vector2(p[v], p[u]));
                        }
                        Indices.Add(first); Indices.Add(first + 1); Indices.Add(first + 2);
                        Indices.Add(first); Indices.Add(first + 2); Indices.Add(first + 3);
                    }
                }
            }

            /// <summary>A cylinder along <paramref name="axis"/> (unit), <paramref name="length"/> long, closed at both ends; UVs in metres (around, along).</summary>
            public void Cylinder(Vector3 center, Vector3 axis, float radius, float length, int sides, float twist)
            {
                var side = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
                var b1 = Vector3.Cross(axis, side).normalized;
                var b2 = Vector3.Cross(axis, b1);
                Vector3 Ring(int i) { float a = (i / (float)sides) * Mathf.PI * 2f + twist; return (b1 * Mathf.Cos(a) + b2 * Mathf.Sin(a)) * radius; }
                Vector3 e0 = center - axis * (length / 2f), e1 = center + axis * (length / 2f);
                int first = Vertices.Count;
                float around = radius * Mathf.PI * 2f;
                for (int i = 0; i <= sides; i++)
                {
                    var r = Ring(i % sides);
                    float u = around * i / sides;
                    Vertices.Add(e0 + r); Uvs.Add(new Vector2(u, 0f));
                    Vertices.Add(e1 + r); Uvs.Add(new Vector2(u, length));
                }
                for (int i = 0; i < sides; i++)
                {
                    int a = first + i * 2;
                    Indices.Add(a); Indices.Add(a + 1); Indices.Add(a + 3);
                    Indices.Add(a); Indices.Add(a + 3); Indices.Add(a + 2);
                }
                foreach (var (end, dir) in new[] { (e0, -axis), (e1, axis) })
                {
                    int c = Vertices.Count;
                    Vertices.Add(end); Uvs.Add(Vector2.zero);
                    for (int i = 0; i < sides; i++)
                    {
                        var r = Ring(i);
                        Vertices.Add(end + r);
                        Uvs.Add(new Vector2(Vector3.Dot(r, b1), Vector3.Dot(r, b2)));
                    }
                    for (int i = 0; i < sides; i++)
                    {
                        int p0 = c + 1 + i, p1 = c + 1 + (i + 1) % sides;
                        bool facing = Vector3.Dot(Vector3.Cross(Vertices[p0] - end, Vertices[p1] - end), dir) > 0f;
                        Indices.Add(c);
                        Indices.Add(facing ? p0 : p1);
                        Indices.Add(facing ? p1 : p0);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ rock: slabs, cliffs, skirts

        /// <summary>How deep the walked-on top of a rock slab may sink below its box (never above it).</summary>
        public static float TopRelief(Vector3 size) => Mathf.Min(0.05f, size.y * 0.1f);

        /// <summary>
        /// How far a slab's sides may bulge OUT of its box, below its rim: up to 30 cm, less than a player's half width, so a
        /// player never sees the camera enter rock and a thrown bomb never meets rock it cannot see by more than a few centimetres.
        /// </summary>
        public static float OutReach(Vector3 size) => size.y > 0.8f ? 0.3f : 0.1f;

        /// <summary>How far a side may sink IN: deep on a thick box, a few centimetres on the ends of a thin wall (window edges).</summary>
        public static float InReach(Vector3 size) => Mathf.Min(0.6f, Mathf.Min(size.x, size.z) * 0.3f);

        /// <summary>The band under the top where the sides only sink (a rounded rim): nothing to step on that is not there.</summary>
        public static float RimDepth(Vector3 size) => Mathf.Min(0.5f, size.y * 0.4f);

        public static float EdgeRound(Vector3 size) => Mathf.Min(0.45f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.3f);

        enum RockStyle { Slab, Skirt, Crag }

        /// <summary>
        /// A rock slab (floors, ledges, steps, cliffs, buttresses): every face is a grid moved along the inward sum of the faces a
        /// vertex lies on by an amount computed from the position alone (so faces meet without cracks): broad bulges and strata
        /// in low-frequency noise, edges strongly rounded. The walked-on top only sinks (up to 5 cm); under a rounded rim the
        /// sides may sink deep or bulge out by up to <see cref="OutReach"/>; the bottom only sinks.
        /// </summary>
        public static Mesh RoughBox(Vector3 size, Vector3 scale) => Rock(size, scale, RockStyle.Slab);

        /// <summary>
        /// The rock mass under a raised slab (collider-free decoration): its top hides under the slab, then it widens with depth,
        /// up to 1.4 m out at 4 m down, and narrows again at its foot: a platform reads as an outcrop, not a floating box.
        /// </summary>
        public static Mesh Skirt(Vector3 size, Vector3 scale) => Rock(size, scale, RockStyle.Skirt);

        /// <summary>
        /// A crag (a rock spur against a cliff, never walked on): every edge rounded deep, the top domed, the sides hollowed: a
        /// spur reads as rock, not as a box. Sides bulge out by at most <see cref="OutReach"/>.
        /// </summary>
        public static Mesh Crag(Vector3 size, Vector3 scale) => Rock(size, scale, RockStyle.Crag);

        static Mesh Rock(Vector3 size, Vector3 scale, RockStyle style)
        {
            var b = new Builder();
            Vector3 h = size / 2f;
            int seed = SeedFor(size) + (style == RockStyle.Skirt ? 991 : style == RockStyle.Crag ? 557 : 0);
            float top = TopRelief(size), outReach = OutReach(size), inReach = InReach(size), rim = RimDepth(size), round = EdgeRound(size);
            bool thinWall = Mathf.Min(size.x, size.z) < 2.2f && size.y > 1.5f;
            int longAxis = size.x >= size.z ? 0 : 2;
            float big = Mathf.Max(2.5f, Mathf.Min(Mathf.Max(size.x, size.z), 14f));
            Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

            Vector3 Displace(Vector3 p)
            {
                Vector3 dir = Vector3.zero;
                float gapMin = float.MaxValue, gapSecond = float.MaxValue;
                int faces = 0, faceAxis = -1, faceSign = 0;
                for (int a = 0; a < 3; a++)
                {
                    float gap = h[a] - Mathf.Abs(p[a]);
                    if (gap < 1e-4f)
                    {
                        dir -= Axis(a) * Mathf.Sign(p[a]);
                        faces++;
                        faceAxis = a;
                        faceSign = p[a] > 0f ? 1 : -1;
                    }
                    if (gap < gapMin) { gapSecond = gapMin; gapMin = gap; }
                    else if (gap < gapSecond) gapSecond = gap;
                }
                if (dir == Vector3.zero) return p;
                dir.Normalize();
                // low frequencies make the forms (bulges, ledges), the high ones the grain; a slight vertical stretch reads as strata
                var q = new Vector3(p.x, p.y * 1.8f, p.z);
                float n = Noise(q / big * 1.6f, seed) * 0.5f + Noise(q * 0.45f, seed + 3) * 0.32f + Noise(q * 1.6f, seed + 7) * 0.18f;
                float below = h.y - p.y;                      // depth under the top
                bool onTop = faces == 1 && faceAxis == 1 && faceSign > 0;
                bool onBottom = faces == 1 && faceAxis == 1 && faceSign < 0;
                float d;
                if (style == RockStyle.Crag)
                {
                    float hollow = Mathf.Min(h.x, h.z) * 0.7f;
                    d = onTop ? Mathf.Min(h.y * 0.35f, 1.6f) * n : onBottom ? hollow * n : Mathf.Lerp(-outReach, hollow, n);
                    if (below < 0.6f || p.y < -h.y + 0.6f) d = Mathf.Max(d, 0f);   // never above the top nor under the foot
                    float cr = Mathf.Min(1.4f, Mathf.Min(h.x, h.z) * 0.9f);
                    float ce = gapSecond;
                    if (ce < cr) d += cr * (1f - ce / cr) * (1f - ce / cr);
                    return Move(p, dir, d);
                }
                if (onTop) d = top * n;
                else if (style == RockStyle.Skirt)
                {
                    // hidden under the slab at the top, wider and wider below, narrower again at the foot
                    float grow = Mathf.Min(1.4f, below * 0.35f);
                    float foot = Mathf.Clamp01((p.y + h.y) / Mathf.Max(0.5f, Mathf.Min(3f, size.y * 0.3f)));
                    float inset = 0.2f + (1f - foot) * Mathf.Min(h.x, h.z) * 0.6f;
                    d = onBottom ? Mathf.Min(h.x, h.z) * 0.4f * n : Mathf.Lerp(-grow, inset, n * 0.6f + (1f - foot) * 0.4f);
                    if (below < 0.25f) d = Mathf.Max(d, 0.2f);
                }
                else
                {
                    float sink = inReach;
                    float bulge = outReach;
                    if (thinWall && faces == 1 && faceAxis == longAxis) { sink = 0.05f; bulge = 0.05f; }   // the edges of windows and doorways stay true
                    d = onBottom ? sink * n : Mathf.Lerp(-bulge, sink, n);
                    if (below < rim || p.y < -h.y + 1e-3f) d = Mathf.Max(d, 0f);   // the rim and the foot only sink
                }
                // round the edges: a vertex close to another face sinks towards the inside
                float r = onTop || below < rim ? Mathf.Min(round, rim) : round;
                float e = gapSecond;
                if (e < r) d += r * (1f - e / r) * (1f - e / r);
                return Move(p, dir, d);
            }

            // inwards along the faces' inward sum, never past the box's middle; outwards only sideways, so nothing rises or sinks
            Vector3 Move(Vector3 p, Vector3 dir, float d)
            {
                if (d >= 0f)
                {
                    float limit = float.MaxValue;
                    for (int a = 0; a < 3; a++)
                        if (Mathf.Abs(dir[a]) > 1e-3f) limit = Mathf.Min(limit, h[a] * 0.9f / Mathf.Abs(dir[a]));
                    return p + dir * Mathf.Min(d, limit);
                }
                var side = new Vector3(dir.x, 0f, dir.z);
                return side.sqrMagnitude < 1e-6f ? p : p + side.normalized * d;
            }

            for (int a = 0; a < 3; a++)
            {
                int u = (a + 1) % 3, v = (a + 2) % 3;
                float cell = style == RockStyle.Skirt ? 0.8f : 0.55f;
                int nu = Mathf.Clamp(Mathf.CeilToInt(size[u] / cell), 3, 40), nv = Mathf.Clamp(Mathf.CeilToInt(size[v] / cell), 3, 40);
                for (int s = -1; s <= 1; s += 2)
                {
                    int first = b.Vertices.Count;
                    for (int j = 0; j <= nv; j++)
                        for (int i = 0; i <= nu; i++)
                        {
                            float fu = -h[u] + size[u] * i / nu, fv = -h[v] + size[v] * j / nv;
                            Vector3 p = Axis(a) * (s * h[a]) + Axis(u) * fu + Axis(v) * fv;
                            b.Vertices.Add(Displace(p));
                            b.Uvs.Add(new Vector2(fu, fv));
                        }
                    var normal = Axis(a) * s;
                    for (int j = 0; j < nv; j++)
                        for (int i = 0; i < nu; i++)
                        {
                            int i0 = first + j * (nu + 1) + i, i1 = i0 + 1, i2 = i0 + nu + 1, i3 = i2 + 1;
                            bool flip = Vector3.Dot(Vector3.Cross(Axis(u), Axis(v)), normal) < 0f;
                            if (!flip) { b.Indices.Add(i0); b.Indices.Add(i1); b.Indices.Add(i3); b.Indices.Add(i0); b.Indices.Add(i3); b.Indices.Add(i2); }
                            else { b.Indices.Add(i0); b.Indices.Add(i3); b.Indices.Add(i1); b.Indices.Add(i0); b.Indices.Add(i2); b.Indices.Add(i3); }
                        }
                }
            }
            return b.ToMesh($"KitSkin {style} {size}", scale, true);
        }

        // ------------------------------------------------------------------ boulder

        /// <summary>
        /// A boulder filling its box (stepping stones, rocks, pillars, cairns): a rounded superellipsoid knocked in by noise, its
        /// top flattened to the box's top (it can be walked on) and its foot to the bottom. Never outside the box.
        /// </summary>
        public static Mesh Boulder(Vector3 size, Vector3 scale)
        {
            int seed = SeedFor(size) + 17;
            int rings = Mathf.Clamp(Mathf.CeilToInt(size.y * 4f) + 8, 10, 22), segments = Mathf.Clamp(Mathf.CeilToInt((size.x + size.z) * 2.5f) + 12, 16, 40);
            const float e = 2.6f;
            var pts = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int r = 0; r <= rings; r++)
            {
                float theta = r / (float)rings * Mathf.PI;
                for (int k = 0; k <= segments; k++)
                {
                    float phi = k / (float)segments * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    float denom = Mathf.Pow(Mathf.Abs(d.x), e) + Mathf.Pow(Mathf.Abs(d.y), e) + Mathf.Pow(Mathf.Abs(d.z), e);
                    var q = d / Mathf.Pow(Mathf.Max(1e-5f, denom), 1f / e);
                    var w = Vector3.Scale(q, size) * 0.5f;
                    float n = Noise(w * 0.5f + Vector3.one * 3.1f, seed) * 0.6f + Noise(w * 1.7f, seed + 5) * 0.4f;
                    q *= 1f - 0.2f * n;
                    if (q.y > 0.78f) q.y = 0.78f + (q.y - 0.78f) * 0.2f;      // a flat top
                    if (q.y < -0.85f) q.y = -0.85f + (q.y + 0.85f) * 0.3f;    // a flat foot
                    pts.Add(q);
                    uvs.Add(new Vector2(phi * (size.x + size.z) * 0.25f, theta * size.y * 0.5f));
                }
            }
            // stretch so the top meets the box's top and the foot its bottom exactly; never wider than the box
            float maxY = float.MinValue, minY = float.MaxValue, maxX = 0f, maxZ = 0f;
            foreach (var q in pts) { maxY = Mathf.Max(maxY, q.y); minY = Mathf.Min(minY, q.y); maxX = Mathf.Max(maxX, Mathf.Abs(q.x)); maxZ = Mathf.Max(maxZ, Mathf.Abs(q.z)); }
            var b = new Builder();
            foreach (var q in pts)
            {
                float y = Mathf.Lerp(-1f, 1f, (q.y - minY) / Mathf.Max(1e-4f, maxY - minY));
                b.Vertices.Add(Vector3.Scale(new Vector3(q.x / Mathf.Max(1f, maxX), y, q.z / Mathf.Max(1f, maxZ)), size) * 0.5f);
            }
            b.Uvs.AddRange(uvs);
            for (int r = 0; r < rings; r++)
                for (int k = 0; k < segments; k++)
                {
                    int a = r * (segments + 1) + k, c = a + segments + 1;
                    b.Indices.Add(a); b.Indices.Add(a + 1); b.Indices.Add(c);
                    b.Indices.Add(a + 1); b.Indices.Add(c + 1); b.Indices.Add(c);
                }
            return b.ToMesh($"KitSkin Boulder {size}", scale, true);
        }

        // ------------------------------------------------------------------ logs

        /// <summary>The axis the logs of a box run along: upright for a wall-like box (a palisade), else its longest side.</summary>
        public static int LogAxis(Vector3 size)
        {
            float thinHorizontal = Mathf.Min(size.x, size.z);
            if (size.y > 1.2f && thinHorizontal < size.y * 0.35f && Mathf.Max(size.x, size.z) > thinHorizontal * 1.8f) return 1;
            return size.x >= size.y && size.x >= size.z ? 0 : size.z >= size.y ? 2 : 1;
        }

        /// <summary>
        /// A bundle of logs filling the box: rows and columns of cylinders along <see cref="LogAxis"/>, each slightly thinner than its
        /// cell and turned by its own amount, so the bark never lines up. Rafts, palisades, rams, swinging logs, posts.
        /// </summary>
        public static Mesh Logs(Vector3 size, Vector3 scale)
        {
            var b = new Builder();
            int axis = LogAxis(size);
            int c1 = (axis + 1) % 3, c2 = (axis + 2) % 3;
            Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            const float target = 0.42f;
            int n1 = Mathf.Max(1, Mathf.RoundToInt(size[c1] / target)), n2 = Mathf.Max(1, Mathf.RoundToInt(size[c2] / target));
            // a cell is square in section: the thinner side decides the log's diameter, the other side gets more logs
            float cell1 = size[c1] / n1, cell2 = size[c2] / n2;
            int seed = SeedFor(size);
            int sides = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(cell1, cell2) * 28f), 8, 16);
            for (int i = 0; i < n1; i++)
                for (int j = 0; j < n2; j++)
                {
                    float r = Mathf.Min(cell1, cell2) / 2f * (0.9f + 0.08f * Hash(i, j, 1, seed));
                    // a little sideways jitter, never out of the cell
                    float j1 = (cell1 / 2f - r) * (Hash(i, j, 2, seed) * 2f - 1f), j2 = (cell2 / 2f - r) * (Hash(i, j, 3, seed) * 2f - 1f);
                    float len = size[axis] * (0.97f + 0.03f * Hash(i, j, 4, seed));
                    var center = Axis(c1) * (-size[c1] / 2f + cell1 * (i + 0.5f) + j1) + Axis(c2) * (-size[c2] / 2f + cell2 * (j + 0.5f) + j2);
                    b.Cylinder(center, Axis(axis), r, len, sides, Hash(i, j, 5, seed) * 6.283f);
                }
            return b.ToMesh($"KitSkin Logs {size}", scale, false);
        }

        // ------------------------------------------------------------------ planks

        /// <summary>
        /// A planked box: boards with narrow gaps along the longest side on top, horizontal boards on the four sides, a plain board
        /// underneath. Boards sit a few millimetres apart and lower or higher by a hair, never out of the box. Bridges, boardwalks,
        /// rotten decks, lifts, crates.
        /// </summary>
        public static Mesh Planks(Vector3 size, Vector3 scale)
        {
            var b = new Builder();
            int seed = SeedFor(size);
            Vector3 h = size / 2f;
            const float board = 0.26f, gap = 0.018f, thick = 0.06f;
            bool alongX = size.x >= size.z;
            float across = alongX ? size.z : size.x, along = alongX ? size.x : size.z;
            int count = Mathf.Max(1, Mathf.RoundToInt(across / board));
            float w = across / count;
            float deck = Mathf.Min(thick, size.y * 0.5f);
            for (int i = 0; i < count; i++)
            {
                float c = -across / 2f + w * (i + 0.5f);
                float sink = 0.012f * Hash(i, 0, 1, seed);
                float t = deck - sink;
                var center = new Vector3(alongX ? 0f : c, h.y - sink - t / 2f, alongX ? c : 0f);
                var dims = new Vector3(alongX ? along : w - gap, t, alongX ? w - gap : along);
                b.Box(center, dims, Quaternion.identity);
            }
            float body = size.y - deck;
            if (body > 0.02f)
            {
                // the sides: horizontal boards, flush with the box, gaps between them
                int rows = Mathf.Max(1, Mathf.RoundToInt(body / board));
                float rh = body / rows;
                float skin = Mathf.Min(0.04f, Mathf.Min(size.x, size.z) * 0.1f);
                for (int r = 0; r < rows; r++)
                {
                    float y = -h.y + rh * (r + 0.5f);
                    float bh = rh - gap;
                    b.Box(new Vector3(0f, y, h.z - skin / 2f), new Vector3(size.x, bh, skin), Quaternion.identity);
                    b.Box(new Vector3(0f, y, -h.z + skin / 2f), new Vector3(size.x, bh, skin), Quaternion.identity);
                    b.Box(new Vector3(h.x - skin / 2f, y, 0f), new Vector3(skin, bh, size.z - 2f * skin), Quaternion.identity);
                    b.Box(new Vector3(-h.x + skin / 2f, y, 0f), new Vector3(skin, bh, size.z - 2f * skin), Quaternion.identity);
                }
                // a dark core behind the gaps: never see through the box
                b.Box(new Vector3(0f, -deck / 2f, 0f), new Vector3(size.x - 2f * skin - 0.01f, body - 0.01f, size.z - 2f * skin - 0.01f), Quaternion.identity);
            }
            return b.ToMesh($"KitSkin Planks {size}", scale, false);
        }
    }
}
