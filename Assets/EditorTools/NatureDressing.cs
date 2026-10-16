using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Shared helpers for planting nature (ARCHITECTURE §25.3): the batches of a <see cref="FoliageSet"/> (scanned Poly Haven models
    /// as variants), and the intended pass arcs that every plant and rock keeps clear of. Used by the level forest
    /// (<see cref="LevelForest"/>), the level terrain and the menu backdrop. Visual only: nothing here collides.
    /// </summary>
    public static class NatureDressing
    {
        public const string GeneratedDir = "Assets/Art/Nature/Generated/";

        /// <summary>
        /// Each mesh of a scanned Poly Haven model as its own variant: the mesh and the matrix that stands it on its base,
        /// centred, in metres (the model's own scale and turn applied); its height in metres.
        /// </summary>
        static List<(Mesh mesh, Matrix4x4 bake, float height)> Variants(string model)
        {
            var result = new List<(Mesh, Matrix4x4, float)>();
            var asset = NatureAssetImporter.ModelPath(model) is string path ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            if (asset == null) return result;
            foreach (var filter in asset.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var m = asset.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = mesh.bounds;
                var world = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
                for (int c = 0; c < 8; c++)
                    world.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
                var bake = Matrix4x4.Translate(new Vector3(-world.center.x, -world.min.y, -world.center.z)) * m;
                result.Add((mesh, bake, world.size.y));
            }
            return result;
        }

        /// <summary>Batches for every variant of a scanned model, sized so a scale of 1 is <paramref name="height"/> metres tall.</summary>
        internal static FoliageSet.Batch[] ModelBatches(FoliageSet set, string model, string material, float height, float[] distances, int[] meshLods, bool[] shadows,
                                              float minScale, float maxScale)
        {
            var mat = NatureMaterialBuilder.Load(material);
            var batches = new List<FoliageSet.Batch>();
            foreach (var (mesh, bake, h) in Variants(model))
            {
                float k = height / Mathf.Max(0.01f, h);
                var b = Batch(set, $"{model} {batches.Count}", Enumerable.Repeat(mesh, distances.Length).ToArray(), new[] { mat }, distances, shadows, minScale, maxScale);
                b.meshTransform = Matrix4x4.Scale(Vector3.one * k) * bake;
                b.meshLods = meshLods;
                b.shadowDistance = 45f;
                batches.Add(b);
            }
            return batches.ToArray();
        }

        internal static FoliageSet.Batch Batch(FoliageSet set, string name, Mesh[] lods, Material[] materials, float[] distances, bool[] shadows, float minScale, float maxScale)
        {
            var b = new FoliageSet.Batch { name = name, lods = lods, materials = materials, lodDistances = distances, lodShadows = shadows, minScale = minScale, maxScale = maxScale };
            set.batches.Add(b);
            return b;
        }


        // every intended pass arc, by 2 m cell: no plant or rock may come within the decoration clearance of one
        static Dictionary<(int, int, int), List<Vector3>> arcCells;

        internal static void IndexArcs(List<Vector3> samples)
        {
            arcCells = new Dictionary<(int, int, int), List<Vector3>>();
            foreach (var p in samples)
            {
                var key = (Mathf.FloorToInt(p.x / 2f), Mathf.FloorToInt(p.y / 2f), Mathf.FloorToInt(p.z / 2f));
                if (!arcCells.TryGetValue(key, out var list)) arcCells[key] = list = new List<Vector3>();
                list.Add(p);
            }
        }

        internal static bool TouchesAnArc(FoliageSet.Batch batch, Vector3 p, float yaw, float scale)
        {
            if (arcCells == null) return false;
            var local = batch.LocalBounds(0);
            var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
            var world = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int c = 0; c < 8; c++)
                world.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
            world.Expand(2f * (PassCorridor.DecorationClearance + 0.1f));
            for (int x = Mathf.FloorToInt(world.min.x / 2f); x <= Mathf.FloorToInt(world.max.x / 2f); x++)
                for (int y = Mathf.FloorToInt(world.min.y / 2f); y <= Mathf.FloorToInt(world.max.y / 2f); y++)
                    for (int z = Mathf.FloorToInt(world.min.z / 2f); z <= Mathf.FloorToInt(world.max.z / 2f); z++)
                        if (arcCells.TryGetValue((x, y, z), out var list) && list.Any(a => world.Contains(a))) return true;
            return false;
        }


        /// <summary>Every intended pass arc, sampled (plants keep clear of them, ARCHITECTURE §25.3).</summary>
        public static List<Vector3> PassSamples()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/ScriptableObjects/Tuning/GameTuning.asset");
            var all = new List<Vector3>();
            var samples = new List<Vector3>();
            foreach (var corridor in Object.FindObjectsByType<PassCorridor>(FindObjectsSortMode.None))
                if (corridor.TrySample(tuning, samples, 0.05f)) all.AddRange(samples);
            return all;
        }

    }
}
