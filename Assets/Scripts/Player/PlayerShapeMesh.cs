using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A solid per <see cref="PlayerShape"/> for the carrier indicator. Each one shows its glyph from any side as it
    /// spins around Y: sphere ●, upright pyramid ▲, upright cube ■, octahedron ◆. Built once, shared by every player.
    /// All fit a unit cube centred on the origin, like the built-in cube the indicator used before.
    /// </summary>
    public static class PlayerShapeMesh
    {
        static readonly Dictionary<PlayerShape, Mesh> Cache = new Dictionary<PlayerShape, Mesh>();

        public static Mesh For(PlayerShape shape)
        {
            if (Cache.TryGetValue(shape, out var mesh) && mesh != null) return mesh;
            mesh = Build(shape);
            mesh.name = "PlayerShape_" + shape;
            mesh.hideFlags = HideFlags.DontSave;
            Cache[shape] = mesh;
            return mesh;
        }

        static readonly Dictionary<PlayerShape, Mesh> FlatCache = new Dictionary<PlayerShape, Mesh>();

        /// <summary>
        /// The shape as a flat glyph lying in XZ, facing up, fitting a unit square (●, ▲ pointing forward, ■, ◆): the
        /// slot ring at a player's feet, read from above and from the side. A flattened solid would not do (a pyramid
        /// seen from above is a square).
        /// </summary>
        public static Mesh Flat(PlayerShape shape)
        {
            if (FlatCache.TryGetValue(shape, out var mesh) && mesh != null) return mesh;
            int sides = shape switch { PlayerShape.Triangle => 3, PlayerShape.Square => 4, PlayerShape.Diamond => 4, _ => 32 };
            float start = shape switch
            {
                PlayerShape.Triangle => 90f,   // a point forward (+Z)
                PlayerShape.Square => 45f,     // edges along the axes
                _ => 90f                       // diamond: points along the axes
            };
            float radius = shape == PlayerShape.Square ? 0.5f * Mathf.Sqrt(2f) : 0.5f;
            var vertices = new Vector3[sides + 1];
            var normals = new Vector3[sides + 1];
            var triangles = new int[sides * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float a = (start + 360f * i / sides) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                if (shape == PlayerShape.Diamond) vertices[i + 1].x *= 0.7f;   // taller than wide, as on the chips
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + (i + 1) % sides;   // clockwise seen from above: the face looks up
                triangles[i * 3 + 2] = 1 + i;
            }
            var colors = new Color[sides + 1];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.up;
                colors[i] = Color.white;   // unlit shaders that multiply by vertex colour
            }
            mesh = new Mesh { name = "PlayerShapeFlat_" + shape, vertices = vertices, normals = normals, colors = colors, triangles = triangles, hideFlags = HideFlags.DontSave };
            mesh.RecalculateBounds();
            FlatCache[shape] = mesh;
            return mesh;
        }

        static Mesh Build(PlayerShape shape)
        {
            switch (shape)
            {
                case PlayerShape.Triangle:
                {
                    // Square pyramid: its side view is a triangle whichever way it has turned.
                    var apex = new Vector3(0f, 0.5f, 0f);
                    Vector3 a = new Vector3(-0.5f, -0.5f, -0.5f), b = new Vector3(0.5f, -0.5f, -0.5f);
                    Vector3 c = new Vector3(0.5f, -0.5f, 0.5f), d = new Vector3(-0.5f, -0.5f, 0.5f);
                    return Faceted(new[] { apex, a, b, apex, b, c, apex, c, d, apex, d, a, a, b, c, a, c, d });
                }
                case PlayerShape.Square:
                {
                    Vector3 v(float x, float y, float z) => new Vector3(x, y, z) * 0.5f;
                    Vector3[] q =
                    {
                        v(-1, -1, -1), v(1, -1, -1), v(1, 1, -1), v(-1, 1, -1),
                        v(-1, -1, 1), v(1, -1, 1), v(1, 1, 1), v(-1, 1, 1)
                    };
                    int[] f = { 0, 1, 2, 0, 2, 3, 1, 5, 6, 1, 6, 2, 5, 4, 7, 5, 7, 6, 4, 0, 3, 4, 3, 7, 3, 2, 6, 3, 6, 7, 0, 4, 5, 0, 5, 1 };
                    var tris = new Vector3[f.Length];
                    for (int i = 0; i < f.Length; i++) tris[i] = q[f[i]];
                    return Faceted(tris);
                }
                case PlayerShape.Diamond:
                {
                    // Octahedron, taller than wide so it reads as ◆ rather than a spinning square.
                    Vector3 top = new Vector3(0f, 0.5f, 0f), bottom = new Vector3(0f, -0.5f, 0f);
                    Vector3[] ring = { new Vector3(0.35f, 0f, 0f), new Vector3(0f, 0f, 0.35f), new Vector3(-0.35f, 0f, 0f), new Vector3(0f, 0f, -0.35f) };
                    var tris = new List<Vector3>();
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 p = ring[i], n = ring[(i + 1) % 4];
                        tris.AddRange(new[] { top, p, n, bottom, p, n });
                    }
                    return Faceted(tris.ToArray());
                }
                default:
                    return Sphere(16, 12);
            }
        }

        /// <summary>
        /// One triangle per three vertices, flat normals (hard edges read better in the toon shading). Every shape is
        /// convex around the origin, so each triangle is turned to face outward whatever order it was listed in.
        /// </summary>
        static Mesh Faceted(Vector3[] vertices)
        {
            var triangles = new int[vertices.Length];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[i], b = vertices[i + 1], c = vertices[i + 2];
                bool outward = Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) > 0f;
                triangles[i] = i;
                triangles[i + 1] = outward ? i + 1 : i + 2;
                triangles[i + 2] = outward ? i + 2 : i + 1;
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh Sphere(int longitude, int latitude)
        {
            var vertices = new List<Vector3>();
            for (int y = 0; y <= latitude; y++)
            {
                float theta = Mathf.PI * y / latitude;
                for (int x = 0; x <= longitude; x++)
                {
                    float phi = 2f * Mathf.PI * x / longitude;
                    vertices.Add(0.5f * new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi)));
                }
            }
            var triangles = new List<int>();
            for (int y = 0; y < latitude; y++)
                for (int x = 0; x < longitude; x++)
                {
                    int i = y * (longitude + 1) + x, below = i + longitude + 1;
                    triangles.AddRange(new[] { i, i + 1, below, i + 1, below + 1, below });
                }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.normals = vertices.ConvertAll(v => v.normalized).ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
