using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The terrain meshes of the nature courses (ARCHITECTURE §25.3): a grid of world points cut into chunks of 16 x 16 cells, a
    /// mesh asset each (collider-free, <see cref="NatureTerrain"/>), two triangles per cell unless <c>keep</c> refuses one, the
    /// chunk's material chosen from the most common tag of its triangles. Shared by PatataWilds (<see cref="NatureDressing"/>) and
    /// PatataCanopy's forest floor.
    /// </summary>
    public static class NatureTerrainMesh
    {
        const int Chunk = 16;

        /// <summary>Builds the chunks under <paramref name="parent"/>, replacing the assets named <paramref name="prefix"/>_*. Returns the chunk count.</summary>
        /// <param name="keep">Whether the triangle of grid vertices (i, j) x 3 is kept.</param>
        /// <param name="tag">A small category (0..7) of a grid vertex, counted per chunk to pick its material.</param>
        public static int Build(Transform parent, Vector3[,] points, Func<(int i, int j), (int i, int j), (int i, int j), bool> keep,
                                Func<int, int, int> tag, Func<int, Material> materialFor, string directory, string prefix)
        {
            int nx = points.GetLength(0), nz = points.GetLength(1);
            int count = 0;
            foreach (var old in Directory.GetFiles(directory, prefix + "_*.asset")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
            for (int ci = 0; ci < nx - 1; ci += Chunk)
                for (int cj = 0; cj < nz - 1; cj += Chunk)
                {
                    var verts = new List<Vector3>();
                    var tris = new List<int>();
                    var tags = new int[8];
                    var index = new Dictionary<(int, int), int>();
                    int V(int i, int j)
                    {
                        if (!index.TryGetValue((i, j), out int k)) { k = verts.Count; verts.Add(points[i, j]); index[(i, j)] = k; }
                        return k;
                    }
                    for (int i = ci; i < Mathf.Min(ci + Chunk, nx - 1); i++)
                        for (int j = cj; j < Mathf.Min(cj + Chunk, nz - 1); j++)
                            foreach (var t in new[] { ((i, j), (i, j + 1), (i + 1, j + 1)), ((i, j), (i + 1, j + 1), (i + 1, j)) })
                            {
                                if (!keep(t.Item1, t.Item2, t.Item3)) continue;
                                tris.Add(V(t.Item1.Item1, t.Item1.Item2)); tris.Add(V(t.Item2.Item1, t.Item2.Item2)); tris.Add(V(t.Item3.Item1, t.Item3.Item2));
                                tags[Mathf.Clamp(tag(t.Item1.Item1, t.Item1.Item2), 0, tags.Length - 1)]++;
                            }
                    if (tris.Count == 0) continue;
                    var center = verts.Aggregate(Vector3.zero, (s, v) => s + v) / verts.Count;
                    var local = verts.Select(v => v - center).ToList();
                    var mesh = new Mesh { name = $"{prefix}_{ci}_{cj}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.SetVertices(local);
                    mesh.SetTriangles(tris, 0);
                    mesh.RecalculateNormals();
                    mesh.RecalculateTangents();
                    mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh, $"{directory}{mesh.name}.asset");
                    var go = new GameObject(mesh.name);
                    go.transform.SetParent(parent, false);
                    go.transform.position = center;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = materialFor(Array.IndexOf(tags, tags.Max()));
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    go.AddComponent<NatureTerrain>();
                    count++;
                }
            return count;
        }

        /// <summary>
        /// A mist floor texture (RHalf, metres) of <paramref name="height"/> over a world rect, softened, for <see cref="MistField"/>:
        /// saved as an asset at <paramref name="path"/>. Returns the texture and its rect (corner x, z, size x, z).
        /// </summary>
        public static (Texture2D texture, Vector4 rect) BakeMistFloor(Func<float, float, float> height, Rect area, int size, int blurPasses, string path)
        {
            var values = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    values[y * size + x] = height(area.xMin + (x + 0.5f) / size * area.width, area.yMin + (y + 0.5f) / size * area.height);
            var tmp = new float[values.Length];
            for (int pass = 0; pass < blurPasses; pass++)
            {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float sum = 0f;
                        int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = Mathf.Clamp(x + dx, 0, size - 1), yy = Mathf.Clamp(y + dy, 0, size - 1);
                                sum += values[yy * size + xx];
                                n++;
                            }
                        tmp[y * size + x] = sum / n;
                    }
                (values, tmp) = (tmp, values);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null && (tex.width != size || tex.format != TextureFormat.RHalf)) { AssetDatabase.DeleteAsset(path); tex = null; }
            bool created = tex == null;
            if (created) tex = new Texture2D(size, size, TextureFormat.RHalf, false, true) { name = Path.GetFileNameWithoutExtension(path) };
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels(values.Select(v => new Color(v, 0f, 0f, 1f)).ToArray());
            tex.Apply(false, false);
            if (created) AssetDatabase.CreateAsset(tex, path);
            EditorUtility.SetDirty(tex);
            return (tex, new Vector4(area.xMin, area.yMin, area.width, area.height));
        }
    }
}
