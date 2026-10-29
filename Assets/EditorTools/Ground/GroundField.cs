using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// A grid of values over a terrain (ARCHITECTURE §27.4): the ground tools' working surface. Cell (x, z) lies at the terrain's
    /// normalised (x / (n - 1), z / (n - 1)), as an alphamap or heightmap sample does. Rasterises lines and polygons, and turns a mask into a
    /// distance field in metres (a two-pass chamfer).
    /// </summary>
    public sealed class GroundField
    {
        public readonly int Size;
        public readonly Rect Area;
        public readonly float[] Values;

        public GroundField(int size, Rect area, float fill = 0f)
        {
            Size = size;
            Area = area;
            Values = new float[size * size];
            if (fill != 0f) Array.Fill(Values, fill);
        }

        public float Cell => Area.width / (Size - 1);

        public float this[int x, int z]
        {
            get => Values[z * Size + x];
            set => Values[z * Size + x] = value;
        }

        public Vector2 World(int x, int z) => new Vector2(Area.x + x * Area.width / (Size - 1), Area.y + z * Area.height / (Size - 1));

        public Vector2 CellOf(Vector2 world) => new Vector2((world.x - Area.x) / Area.width * (Size - 1), (world.y - Area.y) / Area.height * (Size - 1));

        /// <summary>Bilinear value at a world point (clamped to the grid).</summary>
        public float Sample(Vector2 world)
        {
            var c = CellOf(world);
            float fx = Mathf.Clamp(c.x, 0f, Size - 1.001f), fz = Mathf.Clamp(c.y, 0f, Size - 1.001f);
            int x = (int)fx, z = (int)fz;
            float tx = fx - x, tz = fz - z;
            return Mathf.Lerp(Mathf.Lerp(this[x, z], this[x + 1, z], tx), Mathf.Lerp(this[x, z + 1], this[x + 1, z + 1], tx), tz);
        }

        /// <summary>Calls <paramref name="visit"/> for every cell within <paramref name="radius"/> metres of the segment a→b.</summary>
        public void Segment(Vector2 a, Vector2 b, float radius, Action<int, int, float> visit)
        {
            var ca = CellOf(a);
            var cb = CellOf(b);
            float r = radius / Cell + 1f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ca.x, cb.x) - r)), x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(ca.x, cb.x) + r));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ca.y, cb.y) - r)), z1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(ca.y, cb.y) + r));
            var ab = b - a;
            float len2 = Mathf.Max(1e-6f, ab.sqrMagnitude);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = World(x, z);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = Vector2.Distance(p, a + ab * t);
                    if (d <= radius) visit(x, z, t);
                }
        }

        /// <summary>Calls <paramref name="visit"/> for every cell inside the closed polygon <paramref name="poly"/>.</summary>
        public void Polygon(IList<Vector2> poly, Action<int, int> visit)
        {
            if (poly.Count < 3) return;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var p in poly)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y);
            }
            var c0 = CellOf(new Vector2(minX, minZ));
            var c1 = CellOf(new Vector2(maxX, maxZ));
            for (int z = Mathf.Max(0, Mathf.FloorToInt(c0.y)); z <= Mathf.Min(Size - 1, Mathf.CeilToInt(c1.y)); z++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(c0.x)); x <= Mathf.Min(Size - 1, Mathf.CeilToInt(c1.x)); x++)
                    if (Poly2.Contains(poly, World(x, z))) visit(x, z);
        }

        /// <summary>
        /// The distance (m) from each cell to the nearest cell of <paramref name="mask"/> (0 on it), capped at <paramref name="cap"/>:
        /// a 3-4 chamfer, forward and backward.
        /// </summary>
        public static GroundField Distance(GroundField mask, float cap)
        {
            int n = mask.Size;
            var d = new GroundField(n, mask.Area, cap);
            float c = mask.Cell, diag = c * 1.41421356f;
            for (int i = 0; i < d.Values.Length; i++)
                if (mask.Values[i] > 0.5f) d.Values[i] = 0f;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float v = d[x, z];
                    if (x > 0) v = Mathf.Min(v, d[x - 1, z] + c);
                    if (z > 0)
                    {
                        v = Mathf.Min(v, d[x, z - 1] + c);
                        if (x > 0) v = Mathf.Min(v, d[x - 1, z - 1] + diag);
                        if (x < n - 1) v = Mathf.Min(v, d[x + 1, z - 1] + diag);
                    }
                    d[x, z] = v;
                }
            for (int z = n - 1; z >= 0; z--)
                for (int x = n - 1; x >= 0; x--)
                {
                    float v = d[x, z];
                    if (x < n - 1) v = Mathf.Min(v, d[x + 1, z] + c);
                    if (z < n - 1)
                    {
                        v = Mathf.Min(v, d[x, z + 1] + c);
                        if (x < n - 1) v = Mathf.Min(v, d[x + 1, z + 1] + diag);
                        if (x > 0) v = Mathf.Min(v, d[x - 1, z + 1] + diag);
                    }
                    d[x, z] = Mathf.Min(v, cap);
                }
            return d;
        }

        /// <summary>A box blur of <paramref name="radius"/> cells (rows then columns), <paramref name="passes"/> times.</summary>
        public void Blur(int radius, int passes)
        {
            if (radius <= 0) return;
            var tmp = new float[Values.Length];
            for (int pass = 0; pass < passes; pass++)
            {
                for (int z = 0; z < Size; z++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int x = -radius; x <= radius; x++)
                        if (x >= 0 && x < Size) { sum += this[x, z]; count++; }
                    for (int x = 0; x < Size; x++)
                    {
                        tmp[z * Size + x] = sum / count;
                        int add = x + radius + 1, sub = x - radius;
                        if (add < Size) { sum += this[add, z]; count++; }
                        if (sub >= 0) { sum -= this[sub, z]; count--; }
                    }
                }
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int z = -radius; z <= radius; z++)
                        if (z >= 0 && z < Size) { sum += tmp[z * Size + x]; count++; }
                    for (int z = 0; z < Size; z++)
                    {
                        this[x, z] = sum / count;
                        int add = z + radius + 1, sub = z - radius;
                        if (add < Size) { sum += tmp[add * Size + x]; count++; }
                        if (sub >= 0) { sum -= tmp[sub * Size + x]; count--; }
                    }
                }
            }
        }
    }
}
