using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// A mesh under construction for the organic kit (ARCHITECTURE §25.5): flat-shaded boxes, closed prisms, tubes, rectangular
    /// sweeps and lathes, each into one of several submeshes (material slots). UVs are in metres and the grain runs along each
    /// piece (its V axis), so a wood material with its grain along V (Poly Haven's rough_wood) reads as boards and beams of the
    /// right scale; materials divide them by their tile size.
    /// </summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        readonly List<List<int>> sub = new List<List<int>>();

        public MeshBuilder(int submeshes = 1)
        {
            for (int i = 0; i < submeshes; i++) sub.Add(new List<int>());
        }

        public int Triangles => sub.Sum(s => s.Count) / 3;

        List<int> Sub(int s)
        {
            while (sub.Count <= s) sub.Add(new List<int>());
            return sub[s];
        }

        int Vertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            V.Add(p);
            N.Add(n);
            UV.Add(uv);
            return V.Count - 1;
        }

        /// <summary>A flat quad a-b-c-d (counter-clockwise seen from its front), with its own normal and UVs.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, int s)
        {
            var n = Vector3.Cross(b - a, d - a).normalized;
            int i = Vertex(a, n, ua), j = Vertex(b, n, ub), k = Vertex(c, n, uc), l = Vertex(d, n, ud);
            var t = Sub(s);
            t.Add(i); t.Add(j); t.Add(k);   // Unity's front face: cross(b - a, c - a) is its normal
            t.Add(i); t.Add(k); t.Add(l);
        }

        /// <summary>
        /// A box of <paramref name="size"/> (x across, y up, z along) at <paramref name="center"/>: its grain runs along its local Z
        /// (a plank's or a beam's length). <paramref name="uvShift"/> moves the texture so neighbouring boards differ.
        /// </summary>
        public void Box(Vector3 center, Quaternion rotation, Vector3 size, int s, Vector2 uvShift = default)
        {
            var h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + rotation * new Vector3(x * h.x, y * h.y, z * h.z);
            Vector2 U(float u, float v) => new Vector2(u, v) + uvShift;
            float sx = size.x, sy = size.y, sz = size.z;
            // top and bottom: u across, v along
            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), U(0, 0), U(0, sz), U(sx, sz), U(sx, 0), s);
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), U(0, 0), U(sx, 0), U(sx, sz), U(0, sz), s);
            // the sides along the grain: u up the face, v along
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), U(0, 0), U(sy, 0), U(sy, sz), U(0, sz), s);
            Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), U(0, 0), U(0, sz), U(sy, sz), U(sy, 0), s);
            // the end grain: a small patch of the texture
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), U(0, 0), U(sx, 0), U(sx, sy), U(0, sy), s);
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), U(0, 0), U(0, sy), U(sx, sy), U(sx, 0), s);
        }

        /// <summary>A box from <paramref name="a"/> to <paramref name="b"/> (its length), <paramref name="width"/> × <paramref name="height"/> across, its up as near world up as it can be.</summary>
        public void Beam(Vector3 a, Vector3 b, float width, float height, int s, Vector2 uvShift = default)
        {
            var along = b - a;
            if (along.sqrMagnitude < 1e-6f) return;
            var dir = along.normalized;
            var up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            Box((a + b) * 0.5f, Quaternion.LookRotation(dir, up), new Vector3(width, height, along.magnitude), s, uvShift);
        }

        /// <summary>
        /// A closed prism: the polygon (x, z, counter-clockwise or not) from <paramref name="bottom"/> to the height given by
        /// <paramref name="top"/> at each corner. Top in <paramref name="topSub"/>, sides and bottom in <paramref name="sideSub"/>.
        /// </summary>
        public void Prism(IList<Vector2> polygon, Func<Vector2, float> top, Func<Vector2, float> bottom, int topSub, int sideSub)
        {
            var poly = Poly2.CounterClockwise(polygon);
            var tris = Poly2.Triangulate(poly);
            // top (faces up) and bottom (faces down)
            var tt = Sub(topSub);
            int first = V.Count;
            foreach (var p in poly) Vertex(new Vector3(p.x, top(p), p.y), Vector3.up, p);
            for (int i = 0; i < tris.Count; i += 3) { tt.Add(first + tris[i]); tt.Add(first + tris[i + 2]); tt.Add(first + tris[i + 1]); }
            var bt = Sub(sideSub);
            first = V.Count;
            foreach (var p in poly) Vertex(new Vector3(p.x, bottom(p), p.y), Vector3.down, p);
            for (int i = 0; i < tris.Count; i += 3) { bt.Add(first + tris[i]); bt.Add(first + tris[i + 1]); bt.Add(first + tris[i + 2]); }
            // sides, outward
            float run = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                float len = Vector2.Distance(a, b);
                var a0 = new Vector3(a.x, bottom(a), a.y);
                var b0 = new Vector3(b.x, bottom(b), b.y);
                var a1 = new Vector3(a.x, top(a), a.y);
                var b1 = new Vector3(b.x, top(b), b.y);
                Quad(b0, a0, a1, b1, new Vector2(run + len, b0.y), new Vector2(run, a0.y), new Vector2(run, a1.y), new Vector2(run + len, b1.y), sideSub);
                run += len;
            }
        }

        public void Prism(IList<Vector2> polygon, float bottom, float top, int topSub, int sideSub) =>
            Prism(polygon, _ => top, _ => bottom, topSub, sideSub);

        /// <summary>
        /// A tapered tube through <paramref name="points"/> (radius per point) with rotation-minimising frames, so it never twists.
        /// UVs in metres: u around, v along. <paramref name="caps"/> closes both ends (a solid for a collider).
        /// </summary>
        public void Tube(IList<Vector3> points, IList<float> radii, int sides, int s, bool caps = false, float uvShift = 0f)
        {
            if (points.Count < 2) return;
            var frames = Curves.Frames(points);
            var t = Sub(s);
            int first = V.Count;
            float along = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) along += Vector3.Distance(points[i], points[i - 1]);
                float around = Mathf.PI * 2f * Mathf.Max(radii[i], 0.02f);
                for (int k = 0; k <= sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    var radial = frames[i].normal * Mathf.Cos(a) + frames[i].binormal * Mathf.Sin(a);
                    Vertex(points[i] + radial * radii[i], radial, new Vector2(around * k / sides + uvShift, along));
                }
            }
            for (int i = 0; i < points.Count - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = first + i * (sides + 1) + k, b = a + sides + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(a + 1); t.Add(b + 1); t.Add(b);
                }
            if (!caps) return;
            foreach (int end in new[] { 0, points.Count - 1 })
            {
                var n = end == 0 ? -frames[0].tangent : frames[end].tangent;
                int center = Vertex(points[end], n, Vector2.zero);
                int ring = V.Count;
                for (int k = 0; k <= sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    var radial = frames[end].normal * Mathf.Cos(a) + frames[end].binormal * Mathf.Sin(a);
                    Vertex(points[end] + radial * radii[end], n, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radii[end]);
                }
                for (int k = 0; k < sides; k++)
                {
                    if (end == 0) { t.Add(center); t.Add(ring + k + 1); t.Add(ring + k); }
                    else { t.Add(center); t.Add(ring + k); t.Add(ring + k + 1); }
                }
            }
        }

        public enum Anchor { Center, Top, Bottom }

        /// <summary>
        /// A rectangular section (<paramref name="width"/> across, level; <paramref name="height"/> up) swept along a path: a curved
        /// rim board, a stringer, a bridge's deck. The section stays level across (its side axis is horizontal), the anchor puts the
        /// path on its top face, centre or bottom. Hard edges; closed ends when <paramref name="caps"/>.
        /// </summary>
        public void Sweep(IList<Vector3> path, float width, float height, int s, Anchor anchor = Anchor.Center, bool caps = true, float uvShift = 0f)
        {
            if (path.Count < 2) return;
            int n = path.Count;
            var side = new Vector3[n];
            var up = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var tangent = (i == 0 ? path[1] - path[0] : i == n - 1 ? path[n - 1] - path[n - 2] : path[i + 1] - path[i - 1]).normalized;
                var flat = new Vector3(tangent.x, 0f, tangent.z);
                side[i] = flat.sqrMagnitude < 1e-6f ? Vector3.right : Vector3.Cross(Vector3.up, flat.normalized).normalized;
                up[i] = Vector3.Cross(tangent, side[i]).normalized;
                if (up[i].y < 0f) up[i] = -up[i];
            }
            float offset = anchor == Anchor.Top ? -height * 0.5f : anchor == Anchor.Bottom ? height * 0.5f : 0f;
            Vector3 C(int i, float sx, float sy) => path[i] + up[i] * (offset + sy * height * 0.5f) + side[i] * sx * width * 0.5f;
            float along = uvShift;
            for (int i = 0; i < n - 1; i++)
            {
                float len = Vector3.Distance(path[i], path[i + 1]);
                float v0 = along, v1 = along + len;
                // top, bottom, right, left (u across the face, v along)
                Quad(C(i, -1, 1), C(i + 1, -1, 1), C(i + 1, 1, 1), C(i, 1, 1), new Vector2(0, v0), new Vector2(0, v1), new Vector2(width, v1), new Vector2(width, v0), s);
                Quad(C(i, -1, -1), C(i, 1, -1), C(i + 1, 1, -1), C(i + 1, -1, -1), new Vector2(0, v0), new Vector2(width, v0), new Vector2(width, v1), new Vector2(0, v1), s);
                Quad(C(i, 1, -1), C(i, 1, 1), C(i + 1, 1, 1), C(i + 1, 1, -1), new Vector2(0, v0), new Vector2(height, v0), new Vector2(height, v1), new Vector2(0, v1), s);
                Quad(C(i, -1, -1), C(i + 1, -1, -1), C(i + 1, -1, 1), C(i, -1, 1), new Vector2(0, v0), new Vector2(0, v1), new Vector2(height, v1), new Vector2(height, v0), s);
                along = v1;
            }
            if (!caps) return;
            Quad(C(0, -1, -1), C(0, -1, 1), C(0, 1, 1), C(0, 1, -1), Vector2.zero, new Vector2(0, height), new Vector2(width, height), new Vector2(width, 0), s);
            Quad(C(n - 1, -1, -1), C(n - 1, 1, -1), C(n - 1, 1, 1), C(n - 1, -1, 1), Vector2.zero, new Vector2(width, 0), new Vector2(width, height), new Vector2(0, height), s);
        }

        /// <summary>A solid of revolution round local up: <paramref name="profile"/> is (radius, height) from bottom to top (a bell, a drum, a lantern).</summary>
        public void Lathe(Vector3 origin, Quaternion rotation, IList<Vector2> profile, int sides, int s)
        {
            var t = Sub(s);
            int first = V.Count;
            float run = 0f;
            for (int i = 0; i < profile.Count; i++)
            {
                if (i > 0) run += Vector2.Distance(profile[i], profile[i - 1]);
                var d = i == 0 ? profile[1] - profile[0] : i == profile.Count - 1 ? profile[i] - profile[i - 1] : profile[i + 1] - profile[i - 1];
                var normal2 = new Vector2(d.y, -d.x).normalized;   // outward for a profile going up
                for (int k = 0; k <= sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var p = radial * profile[i].x + Vector3.up * profile[i].y;
                    var nrm = (radial * normal2.x + Vector3.up * normal2.y).normalized;
                    Vertex(origin + rotation * p, rotation * nrm, new Vector2(a * Mathf.Max(0.05f, profile[i].x), run));
                }
            }
            for (int i = 0; i < profile.Count - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = first + i * (sides + 1) + k, b = a + sides + 1;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
        }

        /// <summary>The mesh, with only the slots that got triangles; <paramref name="slots"/> says which material slot each submesh is.</summary>
        public Mesh ToMesh(string name, out int[] slots)
        {
            var used = Enumerable.Range(0, sub.Count).Where(i => sub[i].Count > 0).ToArray();
            slots = used;
            var mesh = new Mesh { name = name, indexFormat = V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            mesh.SetUVs(0, UV);
            mesh.subMeshCount = Mathf.Max(1, used.Length);
            for (int i = 0; i < used.Length; i++) mesh.SetTriangles(sub[used[i]], i);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The mesh in one submesh (a collider).</summary>
        public Mesh ToSingleMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            mesh.SetTriangles(sub.SelectMany(s => s).ToList(), 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Plane polygons in (x, z) as Vector2 (x, y = z): area, triangulation, scanlines, offsets, smoothing.</summary>
    public static class Poly2
    {
        public static float SignedArea(IList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                var q = p[i];
                var r = p[(i + 1) % p.Count];
                a += q.x * r.y - r.x * q.y;
            }
            return a * 0.5f;
        }

        public static List<Vector2> CounterClockwise(IList<Vector2> p)
        {
            var list = p.ToList();
            if (SignedArea(list) < 0f) list.Reverse();
            return list;
        }

        public static bool Contains(IList<Vector2> p, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
                if ((p[i].y > point.y) != (p[j].y > point.y) &&
                    point.x < (p[j].x - p[i].x) * (point.y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            return inside;
        }

        /// <summary>Ear clipping of a simple polygon (counter-clockwise): triangle indices, counter-clockwise in (x, z).</summary>
        public static List<int> Triangulate(IList<Vector2> p)
        {
            var result = new List<int>();
            var idx = Enumerable.Range(0, p.Count).ToList();
            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int k = 0; k < idx.Count; k++)
                {
                    int i0 = idx[(k + idx.Count - 1) % idx.Count], i1 = idx[k], i2 = idx[(k + 1) % idx.Count];
                    var a = p[i0];
                    var b = p[i1];
                    var c = p[i2];
                    if (Cross(b - a, c - b) <= 1e-7f) continue;   // reflex or flat
                    bool empty = true;
                    foreach (int j in idx)
                    {
                        if (j == i0 || j == i1 || j == i2) continue;
                        if (InTriangle(p[j], a, b, c)) { empty = false; break; }
                    }
                    if (!empty) continue;
                    result.Add(i0); result.Add(i1); result.Add(i2);
                    idx.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (!clipped)
                {
                    // degenerate leftovers (collinear runs): drop the flattest vertex
                    int worst = 0;
                    float flat = float.MaxValue;
                    for (int k = 0; k < idx.Count; k++)
                    {
                        var a = p[idx[(k + idx.Count - 1) % idx.Count]];
                        var b = p[idx[k]];
                        var c = p[idx[(k + 1) % idx.Count]];
                        float cr = Mathf.Abs(Cross(b - a, c - b));
                        if (cr < flat) { flat = cr; worst = k; }
                    }
                    idx.RemoveAt(worst);
                }
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 >= 0f && d2 >= 0f && d3 >= 0f;
        }

        /// <summary>The parts of the line through <paramref name="origin"/> along <paramref name="dir"/> inside the polygon, as (t0, t1) along dir.</summary>
        public static List<(float t0, float t1)> Scan(IList<Vector2> p, Vector2 origin, Vector2 dir)
        {
            var ts = new List<float>();
            var normal = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < p.Count; i++)
            {
                var a = p[i];
                var b = p[(i + 1) % p.Count];
                float da = Vector2.Dot(a - origin, normal), db = Vector2.Dot(b - origin, normal);
                if ((da > 0f) == (db > 0f)) continue;
                float f = da / (da - db);
                var hit = Vector2.Lerp(a, b, f);
                ts.Add(Vector2.Dot(hit - origin, dir));
            }
            ts.Sort();
            var spans = new List<(float, float)>();
            for (int i = 0; i + 1 < ts.Count; i += 2) spans.Add((ts[i], ts[i + 1]));
            return spans;
        }

        /// <summary>Moves every edge outward by <paramref name="distance"/> (negative = inward), corners mitred and capped.</summary>
        public static List<Vector2> Offset(IList<Vector2> polygon, float distance)
        {
            var p = CounterClockwise(polygon);
            var result = new List<Vector2>();
            for (int i = 0; i < p.Count; i++)
            {
                var prev = p[(i + p.Count - 1) % p.Count];
                var cur = p[i];
                var next = p[(i + 1) % p.Count];
                var n0 = Outward(cur - prev);
                var n1 = Outward(next - cur);
                var bis = (n0 + n1).normalized;
                float cos = Mathf.Max(0.35f, Vector2.Dot(bis, n1));
                result.Add(cur + bis * distance / cos);
            }
            return result;
        }

        /// <summary>The outward normal of an edge of a counter-clockwise polygon.</summary>
        public static Vector2 Outward(Vector2 edge) => new Vector2(edge.y, -edge.x).normalized;

        /// <summary>Chaikin corner cutting (closed): rounds a polygon's corners, <paramref name="passes"/> times.</summary>
        public static List<Vector2> Smooth(IList<Vector2> polygon, int passes, float cut = 0.25f)
        {
            var p = polygon.ToList();
            for (int pass = 0; pass < passes; pass++)
            {
                var q = new List<Vector2>();
                for (int i = 0; i < p.Count; i++)
                {
                    var a = p[i];
                    var b = p[(i + 1) % p.Count];
                    q.Add(Vector2.Lerp(a, b, cut));
                    q.Add(Vector2.Lerp(a, b, 1f - cut));
                }
                p = q;
            }
            return p;
        }

        /// <summary>Drops corners closer than <paramref name="tolerance"/> to the line of their neighbours (Douglas-Peucker on a ring).</summary>
        public static List<Vector2> Simplify(IList<Vector2> polygon, float tolerance)
        {
            var p = polygon.ToList();
            bool changed = true;
            while (changed && p.Count > 3)
            {
                changed = false;
                for (int i = 0; i < p.Count && p.Count > 3; i++)
                {
                    var a = p[(i + p.Count - 1) % p.Count];
                    var b = p[i];
                    var c = p[(i + 1) % p.Count];
                    var ac = c - a;
                    float d = ac.sqrMagnitude < 1e-8f ? Vector2.Distance(a, b) : Mathf.Abs(Cross(ac.normalized, b - a));
                    if (d < tolerance) { p.RemoveAt(i); changed = true; i--; }
                }
            }
            return p;
        }

        /// <summary>A closed outline round <paramref name="center"/>: radius by angle (radians), sampled every <paramref name="step"/> radians.</summary>
        public static List<Vector2> Radial(Vector2 center, Func<float, float> radius, int samples = 48)
        {
            var p = new List<Vector2>();
            for (int i = 0; i < samples; i++)
            {
                float a = i / (float)samples * Mathf.PI * 2f;
                p.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius(a));
            }
            return p;
        }

        /// <summary>
        /// A partial ring round a trunk: from angle <paramref name="a0"/> to <paramref name="a1"/> (radians, counter-clockwise in x,
        /// z), between the inner radius (the trunk's cut-out) and an outer radius that may vary along it.
        /// </summary>
        public static List<Vector2> Arc(Vector2 center, float inner, Func<float, float> outer, float a0, float a1, int samples = 24)
        {
            var p = new List<Vector2>();
            for (int i = 0; i <= samples; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)samples);
                p.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outer(a));
            }
            for (int i = samples; i >= 0; i--)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)samples);
                p.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * inner);
            }
            return p;
        }

        /// <summary>The perimeter of a closed polygon as a polyline from vertex 0 back to it.</summary>
        public static float Perimeter(IList<Vector2> p)
        {
            float l = 0f;
            for (int i = 0; i < p.Count; i++) l += Vector2.Distance(p[i], p[(i + 1) % p.Count]);
            return l;
        }

        /// <summary>The smallest distance from <paramref name="point"/> to the polygon's edges.</summary>
        public static float DistanceToEdge(IList<Vector2> p, Vector2 point)
        {
            float best = float.MaxValue;
            for (int i = 0; i < p.Count; i++)
            {
                var a = p[i];
                var b = p[(i + 1) % p.Count];
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(point, a + ab * t));
            }
            return best;
        }
    }

    /// <summary>Curves for the organic kit: centripetal Catmull-Rom splines, resampling, sag, rotation-minimising frames.</summary>
    public static class Curves
    {
        public struct Frame
        {
            public Vector3 tangent, normal, binormal;
        }

        /// <summary>A centripetal Catmull-Rom spline through every control point, sampled about every <paramref name="step"/> metres.</summary>
        public static List<Vector3> Spline(IList<Vector3> control, float step)
        {
            if (control.Count < 3) return Resample(control, step);
            var result = new List<Vector3> { control[0] };
            for (int i = 0; i < control.Count - 1; i++)
            {
                var p0 = i == 0 ? control[0] * 2f - control[1] : control[i - 1];
                var p1 = control[i];
                var p2 = control[i + 1];
                var p3 = i + 2 < control.Count ? control[i + 2] : control[i + 1] * 2f - control[i];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
                for (int k = 1; k <= n; k++) result.Add(CatmullRom(p0, p1, p2, p3, k / (float)n));
            }
            return result;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float K(Vector3 a, Vector3 b) => Mathf.Max(1e-4f, Mathf.Pow(Vector3.Distance(a, b), 0.5f));
            float t0 = 0f, t1 = t0 + K(p0, p1), t2 = t1 + K(p1, p2), t3 = t2 + K(p2, p3);
            float u = Mathf.Lerp(t1, t2, t);
            var a1 = (t1 - u) / (t1 - t0) * p0 + (u - t0) / (t1 - t0) * p1;
            var a2 = (t2 - u) / (t2 - t1) * p1 + (u - t1) / (t2 - t1) * p2;
            var a3 = (t3 - u) / (t3 - t2) * p2 + (u - t2) / (t3 - t2) * p3;
            var b1 = (t2 - u) / (t2 - t0) * a1 + (u - t0) / (t2 - t0) * a2;
            var b2 = (t3 - u) / (t3 - t1) * a2 + (u - t1) / (t3 - t1) * a3;
            return (t2 - u) / (t2 - t1) * b1 + (u - t1) / (t2 - t1) * b2;
        }

        public static float Length(IList<Vector3> p)
        {
            float l = 0f;
            for (int i = 1; i < p.Count; i++) l += Vector3.Distance(p[i - 1], p[i]);
            return l;
        }

        /// <summary>The point and direction <paramref name="distance"/> metres along a polyline (clamped).</summary>
        public static (Vector3 point, Vector3 tangent) At(IList<Vector3> p, float distance)
        {
            for (int i = 1; i < p.Count; i++)
            {
                float seg = Vector3.Distance(p[i - 1], p[i]);
                if (distance <= seg || i == p.Count - 1)
                {
                    float f = seg < 1e-6f ? 0f : Mathf.Clamp01(distance / seg);
                    return (Vector3.Lerp(p[i - 1], p[i], f), (p[i] - p[i - 1]).normalized);
                }
                distance -= seg;
            }
            return (p[p.Count - 1], (p[p.Count - 1] - p[p.Count - 2]).normalized);
        }

        /// <summary>A polyline resampled at an even spacing close to <paramref name="step"/>, ends kept.</summary>
        public static List<Vector3> Resample(IList<Vector3> p, float step)
        {
            float length = Length(p);
            int n = Mathf.Max(1, Mathf.RoundToInt(length / step));
            var result = new List<Vector3>();
            for (int i = 0; i <= n; i++) result.Add(At(p, length * i / n).point);
            return result;
        }

        /// <summary>A hanging span from <paramref name="a"/> to <paramref name="b"/> dipping <paramref name="sag"/> metres at mid span (a parabola, close to a catenary for a light sag).</summary>
        public static List<Vector3> Sag(Vector3 a, Vector3 b, float sag, int segments)
        {
            var result = new List<Vector3>();
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                result.Add(Vector3.Lerp(a, b, t) + Vector3.down * (4f * sag * t * (1f - t)));
            }
            return result;
        }

        /// <summary>The slope (degrees) of a sagging span at its ends, the steepest point a walker meets.</summary>
        public static float SagEndSlope(Vector3 a, Vector3 b, float sag)
        {
            var flat = b - a;
            flat.y = 0f;
            float span = flat.magnitude;
            float rise = Mathf.Abs(b.y - a.y);
            return Mathf.Atan((4f * sag + rise) / Mathf.Max(0.01f, span)) * Mathf.Rad2Deg;
        }

        /// <summary>Rotation-minimising frames (double reflection) along a polyline: a tube swept with them never twists.</summary>
        public static Frame[] Frames(IList<Vector3> p)
        {
            int n = p.Count;
            var frames = new Frame[n];
            Vector3 T(int i) => (i == 0 ? p[1] - p[0] : i == n - 1 ? p[n - 1] - p[n - 2] : p[i + 1] - p[i - 1]).normalized;
            var t0 = T(0);
            var r0 = Vector3.Cross(t0, Mathf.Abs(Vector3.Dot(t0, Vector3.up)) < 0.95f ? Vector3.up : Vector3.right).normalized;
            frames[0] = new Frame { tangent = t0, normal = r0, binormal = Vector3.Cross(t0, r0) };
            for (int i = 0; i < n - 1; i++)
            {
                var v1 = p[i + 1] - p[i];
                float c1 = Vector3.Dot(v1, v1);
                if (c1 < 1e-10f) { frames[i + 1] = frames[i]; continue; }
                var rL = frames[i].normal - (2f / c1) * Vector3.Dot(v1, frames[i].normal) * v1;
                var tL = frames[i].tangent - (2f / c1) * Vector3.Dot(v1, frames[i].tangent) * v1;
                var t1 = T(i + 1);
                var v2 = t1 - tL;
                float c2 = Vector3.Dot(v2, v2);
                var r1 = c2 < 1e-10f ? rL : rL - (2f / c2) * Vector3.Dot(v2, rL) * v2;
                r1 = Vector3.ProjectOnPlane(r1, t1).normalized;
                frames[i + 1] = new Frame { tangent = t1, normal = r1, binormal = Vector3.Cross(t1, r1) };
            }
            return frames;
        }
    }

    /// <summary>
    /// The generated mesh assets of one build (a course's decks, stairs, bridges and their proxies): <see cref="Begin"/> a folder,
    /// <see cref="Save"/> meshes into it (an existing asset of the same name is refilled, so its GUID and the scene's reference stay),
    /// <see cref="End"/> deletes the ones this build no longer made. <see cref="BeginInMemory"/> builds without writing: the meshes stay
    /// in memory (an authored level's pieces while they are edited, ARCHITECTURE §27) until they are saved into a folder.
    /// </summary>
    public static class OrganicAssets
    {
        static string directory;
        static bool memory;
        static readonly HashSet<string> used = new HashSet<string>();

        public static string Directory => directory;

        public static void Begin(string folder)
        {
            directory = folder.EndsWith("/") ? folder : folder + "/";
            memory = false;
            System.IO.Directory.CreateDirectory(directory.TrimEnd('/'));
            used.Clear();
        }

        /// <summary>A build whose meshes are named as usual but not written (<see cref="Save"/> returns them as they are).</summary>
        public static void BeginInMemory()
        {
            directory = null;
            memory = true;
            used.Clear();
        }

        /// <summary>Saves (or refills) <paramref name="mesh"/> as <c>&lt;folder&gt;&lt;name&gt;.asset</c>; a name already used in this build gets a suffix.</summary>
        public static Mesh Save(Mesh mesh)
        {
            if (directory == null && !memory) throw new InvalidOperationException("OrganicAssets.Begin first");
            string name = Sanitize(mesh.name);
            string unique = name;
            for (int i = 2; used.Contains(unique); i++) unique = name + "_" + i;
            used.Add(unique);
            mesh.name = unique;
            if (memory) return mesh;
            string path = directory + unique + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Deletes the meshes of the folder this build did not save.</summary>
        public static int End()
        {
            memory = false;
            if (directory == null) return 0;
            int deleted = 0;
            foreach (var file in System.IO.Directory.GetFiles(directory, "*.asset"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (used.Contains(name)) continue;
                if (AssetDatabase.GetMainAssetTypeAtPath(file.Replace('\\', '/')) != typeof(Mesh)) continue;   // only the meshes this kit writes
                AssetDatabase.DeleteAsset(file.Replace('\\', '/'));
                deleted++;
            }
            directory = null;
            return deleted;
        }

        static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace(' ', '_').Replace('\'', '_');
        }
    }
}
