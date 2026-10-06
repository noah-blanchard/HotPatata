using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The scattered vegetation and rocks of PatataWilds (ARCHITECTURE §25.3), as compact data instead of thousands of scene
    /// objects: per kind of plant (a batch: its meshes per level of detail, its materials, its distances), the instances are
    /// packed by 32 m cell, 8 bytes each (a position inside the cell to the centimetre, a yaw, a scale). Written by the editor's
    /// <c>NatureScatter</c>, drawn by <see cref="FoliageInstancer"/>. Visual only: nothing here collides.
    /// </summary>
    [CreateAssetMenu(menuName = "HotPatata/Foliage Set")]
    public class FoliageSet : ScriptableObject
    {
        public const float CellSize = 32f;

        [Serializable]
        public class Batch
        {
            public string name;
            [Tooltip("One mesh per level of detail, nearest first.")] public Mesh[] lods = Array.Empty<Mesh>();
            [Tooltip("One material per submesh (shared by every level).")] public Material[] materials = Array.Empty<Material>();
            [Tooltip("Where each level stops being drawn (metres from the camera to the cell); the last one ends the batch.")] public float[] lodDistances = Array.Empty<float>();
            [Tooltip("Levels that cast shadows (only near trees do).")] public bool[] lodShadows = Array.Empty<bool>();
            [Tooltip("Shadows stop at this distance whatever the level.")] public float shadowDistance = 60f;
            public float minScale = 0.8f, maxScale = 1.2f;
            [Tooltip("Applied to the mesh before each instance (a scanned model's own placement: centred, standing on its base).")]
            public Matrix4x4 meshTransform = Matrix4x4.identity;
            [Tooltip("Per level: the mesh's own LOD to draw (Unity mesh LODs of a scanned model), or -1 to draw it whole.")]
            public int[] meshLods = Array.Empty<int>();

            public Bounds LocalBounds(int lod)
            {
                var m = lods[Mathf.Min(lod, lods.Length - 1)];
                var b = m.bounds;
                var result = new Bounds(meshTransform.MultiplyPoint3x4(b.center), Vector3.zero);
                for (int c = 0; c < 8; c++)
                    result.Encapsulate(meshTransform.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
                return result;
            }
            public List<Cell> cells = new List<Cell>();
        }

        [Serializable]
        public class Cell
        {
            public Vector3 origin;      // the cell's lowest corner (world)
            public float height;        // the vertical span the packed y covers
            public byte[] packed = Array.Empty<byte>();
            public int Count => packed.Length / 8;
        }

        public List<Batch> batches = new List<Batch>();

        /// <summary>Packs instances (world position, yaw in degrees, scale) into <paramref name="cell"/>, replacing what it held.</summary>
        public static void Pack(Batch batch, Cell cell, IReadOnlyList<(Vector3 position, float yaw, float scale)> instances)
        {
            ushort Q(float v, float span) => (ushort)Mathf.Clamp(Mathf.RoundToInt(v / Mathf.Max(0.001f, span) * 65535f), 0, 65535);
            var bytes = new byte[instances.Count * 8];
            for (int i = 0; i < instances.Count; i++)
            {
                var (position, yaw, scale) = instances[i];
                var local = position - cell.origin;
                ushort x = Q(local.x, CellSize), y = Q(local.y, cell.height), z = Q(local.z, CellSize);
                int o = i * 8;
                bytes[o] = (byte)x; bytes[o + 1] = (byte)(x >> 8);
                bytes[o + 2] = (byte)y; bytes[o + 3] = (byte)(y >> 8);
                bytes[o + 4] = (byte)z; bytes[o + 5] = (byte)(z >> 8);
                bytes[o + 6] = (byte)Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 360f * 255f);
                bytes[o + 7] = (byte)Mathf.RoundToInt(Mathf.InverseLerp(batch.minScale, batch.maxScale, scale) * 255f);
            }
            cell.packed = bytes;
        }

        /// <summary>The instance <paramref name="index"/> of <paramref name="cell"/>: world position, yaw (degrees) and scale.</summary>
        public static (Vector3 position, float yaw, float scale) Unpack(Batch batch, Cell cell, int index)
        {
            int o = index * 8;
            var p = cell.packed;
            float x = (p[o] | p[o + 1] << 8) / 65535f * CellSize;
            float y = (p[o + 2] | p[o + 3] << 8) / 65535f * cell.height;
            float z = (p[o + 4] | p[o + 5] << 8) / 65535f * CellSize;
            return (cell.origin + new Vector3(x, y, z), p[o + 6] / 255f * 360f, Mathf.Lerp(batch.minScale, batch.maxScale, p[o + 7] / 255f));
        }

        public int InstanceCount()
        {
            int n = 0;
            foreach (var b in batches) foreach (var c in b.cells) n += c.Count;
            return n;
        }
    }
}
