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

        // ------------------------------------------------------------------ rough rock slab

        /// <summary>How deep a rough slab's surface may sink below its box: the top barely (it is walked on), the sides more.</summary>
        public static float TopRelief(Vector3 size) => Mathf.Min(0.05f, size.y * 0.1f);

        /// <summary>Sides sink up to 16 cm; a tall, thick-enough box (a cliff) sinks up to 45 cm, in broad bulges.</summary>
        public static float SideRelief(Vector3 size) =>
            size.y > 2.5f ? Mathf.Min(0.45f, Mathf.Min(size.x, size.z) * 0.45f) : Mathf.Min(0.16f, Mathf.Min(size.x, size.z) * 0.12f);

        public static float EdgeRound(Vector3 size) => Mathf.Min(0.12f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.2f);

        /// <summary>
        /// A rough slab: each face is a grid pushed inwards by a 3D noise (shallow on the top, deeper on the sides) and rounded along
        /// the edges. The push is computed from the position alone, along the inward sum of the faces a vertex lies on, so the faces
        /// meet without cracks and nothing leaves the box.
        /// </summary>
        public static Mesh RoughBox(Vector3 size, Vector3 scale)
        {
            var b = new Builder();
            Vector3 h = size / 2f;
            int seed = SeedFor(size);
            float top = TopRelief(size), sideDepth = SideRelief(size), round = EdgeRound(size);
            Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

            Vector3 Displace(Vector3 p)
            {
                Vector3 dir = Vector3.zero;
                float gapMin = float.MaxValue, gapSecond = float.MaxValue;
                bool onTop = false;
                for (int a = 0; a < 3; a++)
                {
                    float gap = h[a] - Mathf.Abs(p[a]);
                    if (gap < 1e-4f)
                    {
                        dir -= Axis(a) * Mathf.Sign(p[a]);
                        if (a == 1 && p.y > 0f) onTop = true;
                    }
                    if (gap < gapMin) { gapSecond = gapMin; gapMin = gap; }
                    else if (gap < gapSecond) gapSecond = gap;
                }
                if (dir == Vector3.zero) return p;
                dir.Normalize();
                float n = size.y > 2.5f
                    ? Noise(p * 0.28f, seed) * 0.5f + Noise(p * 0.9f, seed + 3) * 0.3f + Noise(p * 2.7f, seed + 7) * 0.2f   // cliffs: broad bulges and ledges
                    : Noise(p * 0.9f, seed) * 0.65f + Noise(p * 2.7f, seed + 7) * 0.35f;
                float depth = onTop && Mathf.Abs(dir.y) > 0.99f ? top * n : sideDepth * n;
                // round the edges: a vertex close to another face sinks towards the box's inside
                float e = gapSecond;
                if (e < round) depth += round * (1f - e / round) * (1f - e / round);
                return p + dir * depth;
            }

            for (int a = 0; a < 3; a++)
            {
                int u = (a + 1) % 3, v = (a + 2) % 3;
                int nu = Mathf.Clamp(Mathf.CeilToInt(size[u] / 0.6f), 2, 28), nv = Mathf.Clamp(Mathf.CeilToInt(size[v] / 0.6f), 2, 28);
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
                            // wind each quad to face outwards
                            bool flip = Vector3.Dot(Vector3.Cross(Axis(u), Axis(v)), normal) < 0f;
                            if (!flip) { b.Indices.Add(i0); b.Indices.Add(i1); b.Indices.Add(i3); b.Indices.Add(i0); b.Indices.Add(i3); b.Indices.Add(i2); }
                            else { b.Indices.Add(i0); b.Indices.Add(i3); b.Indices.Add(i1); b.Indices.Add(i0); b.Indices.Add(i2); b.Indices.Add(i3); }
                        }
                }
            }
            return b.ToMesh($"KitSkin RoughBox {size}", scale, true);
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
