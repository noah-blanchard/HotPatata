using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// A grid of bytes kept beside a level's terrain (ARCHITECTURE §27.4), compressed: the ground's last automatic weights
    /// (<c>GroundAuto</c>, eight per texel) or the texels the author painted by hand (<c>GroundPainted</c>, one per texel).
    /// </summary>
    public sealed class GroundLayerMap : ScriptableObject
    {
        [SerializeField] int resolution, channels;
        [SerializeField, HideInInspector] byte[] data = Array.Empty<byte>();

        public int Resolution => resolution;
        public int Channels => channels;

        public byte[] Read()
        {
            if (data.Length == 0) return new byte[resolution * resolution * channels];
            using var input = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress);
            var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }

        public void Write(int res, int ch, byte[] bytes)
        {
            resolution = res;
            channels = ch;
            var output = new MemoryStream();
            using (var zip = new DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal)) zip.Write(bytes, 0, bytes.Length);
            data = output.ToArray();
            EditorUtility.SetDirty(this);
        }
    }
}
