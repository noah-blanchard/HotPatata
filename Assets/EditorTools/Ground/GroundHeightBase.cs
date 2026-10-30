using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// A level terrain's hand layer of heights (ARCHITECTURE §27.4): what the author sculpted, without the land shapes (the terrain shows
    /// it plus the shapes, <see cref="TerrainStack"/>), 16 bits a sample, compressed; and the shapes it was last composed with (their id,
    /// footprint and signature), so a shape deleted or moved gives its old ground back.
    /// </summary>
    public sealed class GroundHeightBase : ScriptableObject
    {
        [SerializeField] int resolution;
        [SerializeField, HideInInspector] byte[] data = Array.Empty<byte>();
        [SerializeField] string[] shapeIds = Array.Empty<string>();
        [SerializeField] Rect[] shapeBounds = Array.Empty<Rect>();
        [SerializeField] string[] shapeSignatures = Array.Empty<string>();

        public int Resolution => resolution;
        public string[] ShapeIds => shapeIds;
        public Rect[] ShapeBounds => shapeBounds;
        public string[] ShapeSignatures => shapeSignatures;

        /// <summary>The heights (normalised, as the terrain's), row by row.</summary>
        public float[] Read()
        {
            var heights = new float[resolution * resolution];
            if (data.Length == 0) return heights;
            using var input = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress);
            var raw = new MemoryStream();
            input.CopyTo(raw);
            var bytes = raw.ToArray();
            for (int i = 0; i < heights.Length; i++) heights[i] = (bytes[i * 2] | bytes[i * 2 + 1] << 8) / 65535f;
            return heights;
        }

        public void Write(int res, float[] heights)
        {
            resolution = res;
            var bytes = new byte[heights.Length * 2];
            for (int i = 0; i < heights.Length; i++)
            {
                int v = Mathf.Clamp(Mathf.RoundToInt(heights[i] * 65535f), 0, 65535);
                bytes[i * 2] = (byte)(v & 0xff);
                bytes[i * 2 + 1] = (byte)(v >> 8);
            }
            var output = new MemoryStream();
            using (var zip = new DeflateStream(output, System.IO.Compression.CompressionLevel.Fastest)) zip.Write(bytes, 0, bytes.Length);
            data = output.ToArray();
            EditorUtility.SetDirty(this);
        }

        public void WriteShapes(string[] ids, Rect[] bounds, string[] signatures)
        {
            shapeIds = ids;
            shapeBounds = bounds;
            shapeSignatures = signatures;
            EditorUtility.SetDirty(this);
        }
    }
}
