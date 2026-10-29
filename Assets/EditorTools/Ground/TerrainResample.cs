using System;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>A terrain's size (m a side) and how fine its heights are (ARCHITECTURE §27.4).</summary>
    public enum TerrainFineness { Normal, Fine }

    /// <summary>
    /// A level's terrain at another size or fineness (ARCHITECTURE §27.4, docs/LEVEL_AUTHORING.md §26): the heights resampled bicubically
    /// (no terraces; exact at the old samples), the splat and the painted mask bilinearly then renormalised, the holes cut again, trees
    /// and details kept where they stand in the world, the mist baked again. The terrain keeps its centre; a larger one is extended with
    /// its edge's ground. One undo step, and a copy of the old terrain data beside it until the next save.
    /// </summary>
    public static class TerrainResample
    {
        public static readonly float[] Sizes = { 256f, 512f, 1024f, 1536f };
        /// <summary>Memory over this (MB) is warned of.</summary>
        public const float WarnMb = 128f;

        /// <summary>The heightmap of a terrain of <paramref name="size"/> m: one sample per metre (Normal) or two (Fine), rounded up to 2ⁿ+1, at most 4097.</summary>
        public static int Resolution(float size, TerrainFineness fineness)
        {
            float step = fineness == TerrainFineness.Fine ? 0.5f : 1f;
            int wanted = Mathf.CeilToInt(size / step) + 1;
            int res = 33;
            while (res < wanted && res < 4097) res = (res - 1) * 2 + 1;
            return res;
        }

        /// <summary>The terrain data's memory (MB): heights (16 bits), the splat (two RGBA maps), the holes.</summary>
        public static float MemoryMb(int heightmap)
        {
            int alpha = LevelTerrain.AlphamapResolutionFor(heightmap);
            double bytes = (double)heightmap * heightmap * 2 + 2.0 * alpha * alpha * 4 + (double)(heightmap - 1) * (heightmap - 1);
            return (float)(bytes / (1024 * 1024));
        }

        public static string BackupPath(Terrain terrain) =>
            Path.ChangeExtension(AssetDatabase.GetAssetPath(terrain.terrainData), null) + "_Backup.asset";

        /// <summary>Resamples <paramref name="terrain"/> to <paramref name="size"/> m and <paramref name="resolution"/> height samples.</summary>
        public static void Resample(Terrain terrain, float size, int resolution)
        {
            var data = terrain.terrainData;
            Undo.SetCurrentGroupName("Resample terrain");
            int group = Undo.GetCurrentGroup();
            string path = AssetDatabase.GetAssetPath(data);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(BackupPath(terrain));
                AssetDatabase.CopyAsset(path, BackupPath(terrain));
            }
            Undo.RegisterCompleteObjectUndo(new Object[] { data, terrain.transform }, "Resample terrain");

            var oldSize = data.size;
            var oldOrigin = terrain.transform.position;
            int oldRes = data.heightmapResolution;
            var oldHeights = data.GetHeights(0, 0, oldRes, oldRes);
            int oldAlpha = data.alphamapResolution, layers = data.alphamapLayers;
            var oldMaps = layers > 0 ? data.GetAlphamaps(0, 0, oldAlpha, oldAlpha) : null;
            var trees = data.treeInstances.Select(t => (world: oldOrigin + Vector3.Scale(t.position, oldSize), tree: t)).ToArray();
            int oldDetailRes = data.detailResolution;
            var oldDetails = Enumerable.Range(0, data.detailPrototypes.Length).Select(l => data.GetDetailLayer(0, 0, oldDetailRes, oldDetailRes, l)).ToArray();
            int perPatch = data.detailResolutionPerPatch;
            var painted = GroundBaker.Painted(terrain);
            var lastAuto = AssetDatabase.LoadAssetAtPath<GroundLayerMap>(GroundBaker.AutoPath(terrain));
            byte[] auto = lastAuto != null && lastAuto.Resolution == oldAlpha ? lastAuto.Read() : null;

            // the new grid keeps the centre
            var center = oldOrigin + new Vector3(oldSize.x * 0.5f, 0f, oldSize.z * 0.5f);
            var origin = new Vector3(center.x - size * 0.5f, oldOrigin.y, center.z - size * 0.5f);
            data.heightmapResolution = resolution;
            resolution = data.heightmapResolution;
            data.size = new Vector3(size, oldSize.y, size);
            terrain.transform.position = origin;

            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    float wx = origin.x + x / (float)(resolution - 1) * size, wz = origin.z + z / (float)(resolution - 1) * size;
                    float fx = (wx - oldOrigin.x) / oldSize.x * (oldRes - 1), fz = (wz - oldOrigin.z) / oldSize.z * (oldRes - 1);
                    heights[z, x] = Mathf.Clamp01(Bicubic(oldHeights, oldRes, fx, fz));
                }
            data.SetHeights(0, 0, heights);

            if (oldMaps != null)
            {
                int alpha = LevelTerrain.AlphamapResolutionFor(resolution);
                data.alphamapResolution = alpha;
                var maps = new float[alpha, alpha, layers];
                var newPainted = painted != null ? new byte[alpha * alpha] : null;
                var newAuto = auto != null ? new byte[alpha * alpha * layers] : null;
                for (int z = 0; z < alpha; z++)
                    for (int x = 0; x < alpha; x++)
                    {
                        float wx = origin.x + x / (float)(alpha - 1) * size, wz = origin.z + z / (float)(alpha - 1) * size;
                        float fx = Mathf.Clamp((wx - oldOrigin.x) / oldSize.x * (oldAlpha - 1), 0f, oldAlpha - 1.001f);
                        float fz = Mathf.Clamp((wz - oldOrigin.z) / oldSize.z * (oldAlpha - 1), 0f, oldAlpha - 1.001f);
                        int ix = (int)fx, iz = (int)fz;
                        float tx = fx - ix, tz = fz - iz, sum = 0f;
                        for (int l = 0; l < layers; l++)
                        {
                            float v = Mathf.Lerp(Mathf.Lerp(oldMaps[iz, ix, l], oldMaps[iz, ix + 1, l], tx), Mathf.Lerp(oldMaps[iz + 1, ix, l], oldMaps[iz + 1, ix + 1, l], tx), tz);
                            maps[z, x, l] = v;
                            sum += v;
                        }
                        for (int l = 0; l < layers; l++) maps[z, x, l] = sum > 1e-5f ? maps[z, x, l] / sum : (l == 0 ? 1f : 0f);
                        int nearest = Mathf.RoundToInt(fz) * oldAlpha + Mathf.RoundToInt(fx);
                        if (newPainted != null) newPainted[z * alpha + x] = painted[nearest];
                        if (newAuto != null)
                            for (int l = 0; l < layers; l++) newAuto[(z * alpha + x) * layers + l] = auto[nearest * layers + l];
                    }
                data.SetAlphamaps(0, 0, maps);
                if (newPainted != null) AssetDatabase.LoadAssetAtPath<GroundLayerMap>(GroundBaker.PaintedPath(terrain)).Write(alpha, 1, newPainted);
                if (newAuto != null) lastAuto.Write(alpha, layers, newAuto);
            }

            // trees where they stood
            data.treeInstances = trees.Where(t => Inside(t.world, origin, size)).Select(t =>
            {
                var tree = t.tree;
                tree.position = new Vector3((t.world.x - origin.x) / size, tree.position.y, (t.world.z - origin.z) / size);
                return tree;
            }).ToArray();

            // details where they stood (one cell per metre)
            int detailRes = Mathf.Max(16, Mathf.RoundToInt(size));
            data.SetDetailResolution(detailRes, perPatch);
            for (int l = 0; l < oldDetails.Length; l++)
            {
                var layer = new int[detailRes, detailRes];
                for (int z = 0; z < detailRes; z++)
                    for (int x = 0; x < detailRes; x++)
                    {
                        float wx = origin.x + (x + 0.5f) / detailRes * size, wz = origin.z + (z + 0.5f) / detailRes * size;
                        int ox = Mathf.FloorToInt((wx - oldOrigin.x) / oldSize.x * oldDetailRes), oz = Mathf.FloorToInt((wz - oldOrigin.z) / oldSize.z * oldDetailRes);
                        if (ox >= 0 && oz >= 0 && ox < oldDetailRes && oz < oldDetailRes) layer[z, x] = oldDetails[l][oz, ox];
                    }
                data.SetDetailLayer(0, 0, l, layer);
            }
            EditorUtility.SetDirty(data);

            var scene = terrain.gameObject.scene;
            LevelTerrain.CutCaves(scene);
            var splat = terrain.GetComponent<GroundSplat>();
            if (splat != null) splat.Apply();
            var settings = LevelScenes.SettingsOf(scene);
            if (settings != null) LevelTerrain.BakeMist(scene, settings);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[Ground] {terrain.name} resampled to {size:F0} m, {resolution}² heights ({size / (resolution - 1):F2} m a sample). The old data is kept as {BackupPath(terrain)} until the next save.");
        }

        static bool Inside(Vector3 world, Vector3 origin, float size) =>
            world.x >= origin.x && world.z >= origin.z && world.x <= origin.x + size && world.z <= origin.z + size;

        /// <summary>Catmull-Rom over the 4×4 samples round (x, z), clamped at the edges: the old samples themselves come back exactly.</summary>
        public static float Bicubic(float[,] h, int res, float x, float z)
        {
            x = Mathf.Clamp(x, 0f, res - 1);
            z = Mathf.Clamp(z, 0f, res - 1);
            int ix = Mathf.Min((int)x, res - 2), iz = Mathf.Min((int)z, res - 2);
            float tx = x - ix, tz = z - iz;
            float Row(int r)
            {
                int zz = Mathf.Clamp(r, 0, res - 1);
                float p0 = h[zz, Mathf.Clamp(ix - 1, 0, res - 1)], p1 = h[zz, ix], p2 = h[zz, ix + 1], p3 = h[zz, Mathf.Clamp(ix + 2, 0, res - 1)];
                return CatmullRom(p0, p1, p2, p3, tx);
            }
            return CatmullRom(Row(iz - 1), Row(iz), Row(iz + 1), Row(iz + 2), tz);
        }

        static float CatmullRom(float p0, float p1, float p2, float p3, float t) =>
            0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);

        /// <summary>Deletes the backups of the terrains of a level just saved.</summary>
        public static void ForgetBackups(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var t in LevelTerrain.Terrains(scene))
            {
                if (t.terrainData == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(t.terrainData))) continue;
                string backup = BackupPath(t);
                if (File.Exists(backup)) AssetDatabase.DeleteAsset(backup);
            }
        }
    }

    /// <summary>HotPatata/Ground/Resample Terrain: the open level's terrain at another size or fineness.</summary>
    public sealed class ResampleTerrainWindow : EditorWindow
    {
        int size = 2;
        TerrainFineness fineness;

        [MenuItem("HotPatata/Ground/Resample Terrain...", priority = 20)]
        public static void Open()
        {
            var w = GetWindow<ResampleTerrainWindow>(true, "Resample Terrain");
            w.minSize = new Vector2(380, 170);
            var t = LevelTerrain.Terrains(EditorSceneManager.GetActiveScene()).FirstOrDefault();
            if (t != null)
            {
                w.size = Mathf.Max(0, Array.FindIndex(TerrainResample.Sizes, s => s >= t.terrainData.size.x - 1f));
                w.fineness = t.terrainData.size.x / (t.terrainData.heightmapResolution - 1) < 0.8f ? TerrainFineness.Fine : TerrainFineness.Normal;
            }
        }

        void OnGUI()
        {
            var terrain = LevelTerrain.Terrains(EditorSceneManager.GetActiveScene()).FirstOrDefault();
            if (terrain == null)
            {
                EditorGUILayout.HelpBox("The open scene has no terrain.", MessageType.Info);
                return;
            }
            var d = terrain.terrainData;
            EditorGUILayout.LabelField("Now", $"{d.size.x:F0} m, {d.heightmapResolution}² ({d.size.x / (d.heightmapResolution - 1):F2} m a sample)");
            size = EditorGUILayout.Popup("Size", size, TerrainResample.Sizes.Select(s => $"{s:F0} m").ToArray());
            fineness = (TerrainFineness)EditorGUILayout.EnumPopup("Fineness", fineness);
            int res = TerrainResample.Resolution(TerrainResample.Sizes[size], fineness);
            float mb = TerrainResample.MemoryMb(res);
            EditorGUILayout.LabelField("Becomes", $"{res}² heights ({TerrainResample.Sizes[size] / (res - 1):F2} m a sample), {mb:F0} MB");
            if (mb > TerrainResample.WarnMb) EditorGUILayout.HelpBox($"Over {TerrainResample.WarnMb:F0} MB of terrain data: heavy to load and to save.", MessageType.Warning);
            EditorGUILayout.HelpBox("The relief is kept (exact at the old samples), the centre stays where it is, trees and details stay where they stand. Ctrl+Z undoes it.", MessageType.None);
            if (GUILayout.Button("Resample"))
            {
                TerrainResample.Resample(terrain, TerrainResample.Sizes[size], res);
                Close();
            }
        }
    }
}
