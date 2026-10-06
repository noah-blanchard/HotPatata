using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (ARCHITECTURE §25.1): draws a kit visual's box with KayKit pieces. The box is this object's
    /// scale times <see cref="unitBox"/>, the same box the old greybox cube showed, so colliders, builders and resize
    /// helpers are unchanged. The box is tiled with the family's best-fitting piece (a few percent of stretch when the
    /// box is off the KayKit grid) and drawn as one combined mesh through this object's <see cref="MeshRenderer"/>, so
    /// effects that tint that renderer (belt scroll, gate lamps) keep working. The mesh is built on enable, cached per
    /// size and never saved: the <see cref="MeshFilter"/> is serialized empty and only filled at run time (and in the
    /// editor, without recording a prefab override).
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class KitSkin : MonoBehaviour
    {
        /// <summary>Cost of one more piece against stretch (sum of |ln(cell / native)| per axis): favours big pieces.</summary>
        const float PieceCost = 0.03f;

        [SerializeField] KitPalette palette;
        [SerializeField] KitShape shape;
        [SerializeField] KitColor color = KitColor.Green;
        [SerializeField, Tooltip("This object's box in its own space: (1,1,1) for a unit cube, (1,2,1) for a cylinder primitive.")]
        Vector3 unitBox = Vector3.one;
        [SerializeField, Tooltip("Turn every piece 180° about Y (arrow tiles pointing the other way).")] bool flip;

        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        MeshFilter filter;
        Vector3 builtScale;

        public KitShape Shape => shape;
        public KitColor Color => color;
        public Vector3 UnitBox => unitBox;
        public bool Flip => flip;

        /// <summary>Editor builders: sets everything at once and redraws.</summary>
        public void Configure(KitPalette kitPalette, KitShape kitShape, KitColor kitColor, Vector3 box, bool flipped)
        {
            palette = kitPalette;
            shape = kitShape;
            color = kitColor;
            unitBox = box;
            flip = flipped;
            Rebuild();
        }

        void OnEnable() => Rebuild();

#if UNITY_EDITOR
        void OnValidate()
        {
            if (isActiveAndEnabled) UnityEditor.EditorApplication.delayCall += () => { if (this != null) Rebuild(); };
        }

        void Update()
        {
            if (!Application.isPlaying && transform.lossyScale != builtScale) Rebuild();
        }
#endif

        /// <summary>The box in metres.</summary>
        public Vector3 Size => Vector3.Scale(Abs(transform.lossyScale), unitBox);

        public void Rebuild()
        {
            if (palette == null && !IsGenerated(shape)) return;
            if (filter == null) filter = GetComponent<MeshFilter>();
            builtScale = transform.lossyScale;
            filter.sharedMesh = MeshFor(palette, shape, color, unitBox, Abs(builtScale), flip);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>Shapes drawn from code, with no palette piece (the bevel box, and the nature shapes of <see cref="NatureShapes"/>).</summary>
        public static bool IsGenerated(KitShape shape) =>
            shape == KitShape.BevelBox || shape == KitShape.RoughBox || shape == KitShape.Logs || shape == KitShape.Planks;

        // ------------------------------------------------------------------ layout

        /// <summary>One piece of a tiled box, in metres, centred on the box.</summary>
        public struct Tile
        {
            public Mesh Mesh;
            public Vector3 Center;
            public Vector3 Cell;      // the cell the piece is stretched to fill
            public bool Turned;       // turned 90° about Y (the piece's X runs along the box's Z)
        }

        /// <summary>
        /// Tiles a <paramref name="size"/> box (metres) with one of <paramref name="pieces"/>: the piece, turn and counts
        /// with the least stretch plus <see cref="PieceCost"/> per piece. Pipes keep their round section and repeat along Y.
        /// </summary>
        public static List<Tile> Layout(IReadOnlyList<Mesh> pieces, KitShape shape, Vector3 size)
        {
            var tiles = new List<Tile>();
            if (pieces == null || pieces.Count == 0) return tiles;
            Mesh best = null;
            bool bestTurned = false;
            Vector3Int bestCount = Vector3Int.one;
            float bestCost = float.MaxValue;
            foreach (var mesh in pieces)
            {
                Vector3 n = mesh.bounds.size;
                for (int turn = 0; turn < (shape == KitShape.Pipe ? 1 : 2); turn++)
                {
                    bool turned = turn == 1;
                    var native = turned ? new Vector3(n.z, n.y, n.x) : n;
                    Vector3Int count;
                    if (shape == KitShape.Pipe)
                    {
                        float section = (size.x / native.x + size.z / native.z) / 2f;   // keep the pipe round
                        count = new Vector3Int(1, Mathf.Max(1, Mathf.RoundToInt(size.y / (native.y * section))), 1);
                    }
                    else count = new Vector3Int(Fit(size.x, native.x), Fit(size.y, native.y), Fit(size.z, native.z));
                    // Timber panels are a surface, never a stack of hidden layers inside the floor slab.
                    if (shape == KitShape.Floor) count.y = 1;
                    float cost = Stretch(size.x / count.x, native.x) + Stretch(size.z / count.z, native.z)
                                 + (shape == KitShape.Pipe ? 0f : Stretch(size.y / count.y, native.y) + PieceCost * count.x * count.y * count.z);
                    if (cost >= bestCost) continue;
                    bestCost = cost;
                    best = mesh;
                    bestTurned = turned;
                    bestCount = count;
                }
            }
            var cell = new Vector3(size.x / bestCount.x, size.y / bestCount.y, size.z / bestCount.z);
            for (int x = 0; x < bestCount.x; x++)
                for (int y = 0; y < bestCount.y; y++)
                    for (int z = 0; z < bestCount.z; z++)
                        tiles.Add(new Tile
                        {
                            Mesh = best,
                            Center = new Vector3((x + 0.5f) * cell.x, (y + 0.5f) * cell.y, (z + 0.5f) * cell.z) - size / 2f,
                            Cell = cell,
                            Turned = bestTurned
                        });
            return tiles;
        }

        static int Fit(float length, float native) => Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(0.01f, native)));

        static float Stretch(float cell, float native) => Mathf.Abs(Mathf.Log(Mathf.Max(0.001f, cell) / Mathf.Max(0.001f, native)));

        // ------------------------------------------------------------------ mesh

        static Mesh MeshFor(KitPalette palette, KitShape shape, KitColor color, Vector3 unitBox, Vector3 scale, bool flip)
        {
            Vector3 size = Vector3.Scale(scale, unitBox);
            // A platform box standing on edge (a piston gate, a door) reads as a wall: rounded blocks, same colour.
            if (shape == KitShape.Platform && size.y > Mathf.Min(size.x, size.z)) shape = KitShape.Barrier;
            string key = $"{(palette == null ? 0 : palette.GetInstanceID())}|{shape}|{color}|{flip}|{Q(size)}|{Q(unitBox)}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            if (shape == KitShape.BevelBox) return Cache[key] = BevelMesh(size, scale);
            if (shape == KitShape.RoughBox) return Cache[key] = NatureShapes.RoughBox(size, scale);
            if (shape == KitShape.Logs) return Cache[key] = NatureShapes.Logs(size, scale);
            if (shape == KitShape.Planks) return Cache[key] = NatureShapes.Planks(size, scale);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();
            var srcVerts = new List<Vector3>();
            var srcNormals = new List<Vector3>();
            var srcUvs = new List<Vector2>();
            var srcIndices = new List<int>();
            Vector3 toLocal = new Vector3(1f / Mathf.Max(0.0001f, scale.x), 1f / Mathf.Max(0.0001f, scale.y), 1f / Mathf.Max(0.0001f, scale.z));

            foreach (var tile in Layout(palette.Meshes(shape, color), shape, size))
            {
                var mesh = tile.Mesh;
                var b = mesh.bounds;
                Vector3 n = b.size;
                var yaw = Quaternion.Euler(0f, (tile.Turned ? 90f : 0f) + (flip ? 180f : 0f), 0f);
                // the piece is scaled in its own space, then turned into the cell
                var stretch = tile.Turned
                    ? new Vector3(tile.Cell.z / n.x, tile.Cell.y / n.y, tile.Cell.x / n.z)
                    : new Vector3(tile.Cell.x / n.x, tile.Cell.y / n.y, tile.Cell.z / n.z);
                mesh.GetVertices(srcVerts);
                mesh.GetNormals(srcNormals);
                mesh.GetUVs(0, srcUvs);
                int baseIndex = vertices.Count;
                for (int i = 0; i < srcVerts.Count; i++)
                {
                    Vector3 metres = tile.Center + yaw * Vector3.Scale(srcVerts[i] - b.center, stretch);
                    vertices.Add(Vector3.Scale(metres, toLocal));
                    if (i < srcNormals.Count)
                    {
                        // inverse transpose of the stretch, then of the metres-to-local map
                        Vector3 nm = (yaw * new Vector3(srcNormals[i].x / stretch.x, srcNormals[i].y / stretch.y, srcNormals[i].z / stretch.z)).normalized;
                        normals.Add(Vector3.Scale(nm, scale).normalized);
                    }
                    uvs.Add(i < srcUvs.Count ? srcUvs[i] : Vector2.zero);
                }
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    mesh.GetTriangles(srcIndices, s);
                    foreach (int index in srcIndices) indices.Add(baseIndex + index);
                }
            }

            var combined = new Mesh
            {
                name = $"KitSkin {shape} {color} {Q(size)}",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            combined.SetVertices(vertices);
            if (normals.Count == vertices.Count) combined.SetNormals(normals);
            else combined.RecalculateNormals();
            combined.SetUVs(0, uvs);
            combined.SetTriangles(indices, 0);
            combined.RecalculateBounds();
            Cache[key] = combined;
            return combined;
        }

        // ------------------------------------------------------------------ bevel box

        /// <summary>Chamfer of a <see cref="KitShape.BevelBox"/> in metres: a tenth of its thinnest side, kept between 2 and 6 cm.</summary>
        public static float ChamferFor(Vector3 size)
        {
            float thinnest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            return Mathf.Min(Mathf.Clamp(thinnest * 0.1f, 0.02f, 0.06f), thinnest * 0.4f);
        }

        /// <summary>
        /// A chamfered box of <paramref name="size"/> metres, drawn in this object's local space (<paramref name="scale"/> is its
        /// lossy scale). Every vertex lies inside the box and the faces reach its full extent, so a bevelled visual never
        /// sticks out of the collider it stands for. Flat normals (the 45° strips are what catches the light); the shader
        /// maps textures from the position, so the UVs are only a metre-scaled planar fallback.
        /// </summary>
        static Mesh BevelMesh(Vector3 size, Vector3 scale)
        {
            Vector3 h = size / 2f;
            float c = ChamferFor(size);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();
            Vector3 toLocal = new Vector3(1f / Mathf.Max(0.0001f, scale.x), 1f / Mathf.Max(0.0001f, scale.y), 1f / Mathf.Max(0.0001f, scale.z));

            void Add(Vector3 normal, params Vector3[] points)
            {
                // wind the polygon so it faces along the normal
                if (Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), normal) < 0f) System.Array.Reverse(points);
                int first = vertices.Count;
                Vector3 n = Vector3.Scale(normal.normalized, scale).normalized;
                Vector3 a = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                foreach (var p in points)
                {
                    vertices.Add(Vector3.Scale(p, toLocal));
                    normals.Add(n);
                    uvs.Add(a.y >= a.x && a.y >= a.z ? new Vector2(p.x, p.z) : a.x >= a.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
                }
                for (int i = 1; i < points.Length - 1; i++) { indices.Add(first); indices.Add(first + i); indices.Add(first + i + 1); }
            }

            Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

            for (int a = 0; a < 3; a++)
            {
                int u = (a + 1) % 3, v = (a + 2) % 3;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 P(float su, float sv) => Axis(a) * (s * h[a]) + Axis(u) * (su * (h[u] - c)) + Axis(v) * (sv * (h[v] - c));
                    Add(Axis(a) * s, P(-1, -1), P(1, -1), P(1, 1), P(-1, 1));
                }
            }
            for (int a = 0; a < 3; a++)
            {
                int b = (a + 1) % 3, t = (a + 2) % 3;
                for (int sa = -1; sa <= 1; sa += 2)
                    for (int sb = -1; sb <= 1; sb += 2)
                    {
                        Vector3 A(float st) => Axis(a) * (sa * h[a]) + Axis(b) * (sb * (h[b] - c)) + Axis(t) * (st * (h[t] - c));
                        Vector3 B(float st) => Axis(a) * (sa * (h[a] - c)) + Axis(b) * (sb * h[b]) + Axis(t) * (st * (h[t] - c));
                        Add(Axis(a) * sa + Axis(b) * sb, A(-1), A(1), B(1), B(-1));
                    }
            }
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        Add(new Vector3(sx, sy, sz),
                            new Vector3(sx * h.x, sy * (h.y - c), sz * (h.z - c)),
                            new Vector3(sx * (h.x - c), sy * h.y, sz * (h.z - c)),
                            new Vector3(sx * (h.x - c), sy * (h.y - c), sz * h.z));

            var mesh = new Mesh { name = $"KitSkin BevelBox {Q(size)}", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static string Q(Vector3 v) => $"{Mathf.RoundToInt(v.x * 100f)},{Mathf.RoundToInt(v.y * 100f)},{Mathf.RoundToInt(v.z * 100f)}";
    }
}
