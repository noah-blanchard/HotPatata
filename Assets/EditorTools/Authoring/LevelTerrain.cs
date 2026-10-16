using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// An authored level's land and air (ARCHITECTURE §27): its Unity terrain drawn in the nature look (<see cref="GroundMaterial"/>: leaf
    /// litter on the flat, rock on the slopes, in the height mist), valley walls raised round it, the forest brushes of its tree and
    /// detail painting, paths cleared of what was painted on them; and on every save its safety net, its mist (baked from the terrain,
    /// raised in the <see cref="MistZone"/>s) and its audio map (each section's ambience).
    /// </summary>
    public static class LevelTerrain
    {
        /// <summary>The terrain's look: PatataRanger's valley ground (HotPatata/Nature, triplanar rock with a top layer of leaf litter).</summary>
        public const string GroundMaterial = "Nature_ValleyGround";
        /// <summary>The mist floor's texture size and blur.</summary>
        public const int MistSize = 256, MistBlur = 4;
        public const string DetailsFolder = "Assets/Prefabs/Nature/Details/";

        public static IEnumerable<Terrain> Terrains(Scene scene) =>
            scene.IsValid() && scene.isLoaded ? scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>(true)) : Enumerable.Empty<Terrain>();

        public static bool Covers(Terrain terrain, Vector2 p)
        {
            var o = terrain.transform.position;
            var size = terrain.terrainData.size;
            return p.x >= o.x && p.x <= o.x + size.x && p.y >= o.z && p.y <= o.z + size.z;
        }

        /// <summary>The terrain's area in world (x, z).</summary>
        public static Rect Area(Terrain terrain)
        {
            var o = terrain.transform.position;
            var size = terrain.terrainData.size;
            return new Rect(o.x, o.z, size.x, size.z);
        }

        // ------------------------------------------------------------------ look

        /// <summary>Draws <paramref name="terrain"/> in the nature look (one shader path: no instancing, no base map).</summary>
        public static void ApplyLook(Terrain terrain)
        {
            Undo.RecordObject(terrain, "Terrain look");
            terrain.materialTemplate = NatureMaterialBuilder.Load(GroundMaterial);
            terrain.drawInstanced = false;
            terrain.basemapDistance = 20000f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            terrain.gameObject.layer = CourseKit.Layer("Environment");
            EditorUtility.SetDirty(terrain);
        }

        // ------------------------------------------------------------------ valley walls

        /// <summary>
        /// Raises the terrain's border into rock walls: within <paramref name="width"/> metres of its edge the ground climbs to
        /// <paramref name="height"/> metres over the level's floor, broken by noise (never lowered). Returns the samples raised.
        /// </summary>
        public static int RaiseBorder(Terrain terrain, float width, float height, int seed = 7)
        {
            var data = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Raise valley walls");
            int res = data.heightmapResolution;
            var h = data.GetHeights(0, 0, res, res);
            float floor = Median(h), span = data.size.y;
            int raised = 0;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float wx = x / (float)(res - 1) * data.size.x, wz = z / (float)(res - 1) * data.size.z;
                    float edge = Mathf.Min(Mathf.Min(wx, data.size.x - wx), Mathf.Min(wz, data.size.z - wz));
                    float noise = Mathf.PerlinNoise(wx * 0.045f + seed, wz * 0.045f) - 0.5f;
                    float reach = width * (1f + 0.35f * noise);
                    float t = Mathf.Clamp01(1f - edge / Mathf.Max(1f, reach));
                    if (t <= 0f) continue;
                    // steep: most of the climb in the inner third of the band (a cliff, then the slope behind it)
                    float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.6f)) * height * (1f + 0.3f * (Mathf.PerlinNoise(wx * 0.11f, wz * 0.11f + seed) - 0.5f));
                    float target = Mathf.Clamp01(floor + rise / span);
                    if (target > h[z, x]) { h[z, x] = target; raised++; }
                }
            data.SetHeights(0, 0, h);
            EditorUtility.SetDirty(data);
            return raised;
        }

        static float Median(float[,] h)
        {
            var list = new List<float>();
            for (int z = 0; z < h.GetLength(0); z += 8)
                for (int x = 0; x < h.GetLength(1); x += 8) list.Add(h[z, x]);
            list.Sort();
            return list[list.Count / 2];
        }

        // ------------------------------------------------------------------ painted forest

        /// <summary>
        /// Gives <paramref name="terrain"/> the forest brushes: the nature trees (pine, fir, broadleaf, snag, bush; three of each) for
        /// its tree painting, ferns and grass for its detail painting. Painted trees are decoration (no collider), like the courses'.
        /// </summary>
        public static void SetupBrushes(Terrain terrain)
        {
            var data = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Forest brushes");
            var trees = new List<TreePrototype>();
            foreach (var kind in new[] { "Pine", "Fir", "Broadleaf", "Snag", "Bush" })
                for (int v = 0; v < NatureTreeBuilder.Variants; v++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Nature/Tree_{kind}_{v}.prefab");
                    if (prefab != null) trees.Add(new TreePrototype { prefab = prefab, bendFactor = 0f });
                }
            data.treePrototypes = trees.ToArray();
            var details = new List<DetailPrototype>();
            foreach (var (kind, material, size) in new[] { ("Fern", "Nature_Fern", new Vector2(1.6f, 3.2f)), ("Grass", "Nature_GrassBlades", new Vector2(1.2f, 2.2f)) })
                for (int v = 0; v < NatureTreeBuilder.Variants; v++)
                {
                    var prefab = DetailPrefab(kind, v, material);
                    if (prefab == null) continue;
                    details.Add(new DetailPrototype
                    {
                        prototype = prefab,
                        usePrototypeMesh = true,
                        useInstancing = true,
                        renderMode = DetailRenderMode.VertexLit,
                        minWidth = size.x, maxWidth = size.y, minHeight = size.x, maxHeight = size.y,
                        healthyColor = Color.white, dryColor = Color.white,
                        noiseSpread = 0.3f,
                    });
                }
            data.detailPrototypes = details.ToArray();
            data.SetDetailResolution(Mathf.Max(256, data.detailResolution), 16);
            terrain.treeDistance = 600f;
            terrain.treeBillboardDistance = 600f;
            terrain.detailObjectDistance = 90f;
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(terrain);
        }

        /// <summary>A prefab of one fern or grass mesh for the terrain's detail painting (made once).</summary>
        static GameObject DetailPrefab(string kind, int variant, string material)
        {
            string path = $"{DetailsFolder}Detail_{kind}_{variant}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(NatureTreeBuilder.MeshPath(kind, variant, 0));
            if (mesh == null) return null;
            Directory.CreateDirectory(DetailsFolder.TrimEnd('/'));
            var go = new GameObject($"Detail_{kind}_{variant}");
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = NatureMaterialBuilder.Load(material);
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// Takes off the terrains of <paramref name="scene"/> what was painted on a path: trees within <paramref name="clearance"/>
        /// of a walkway, a bridge or a deck, inside the level's solids or on a pass arc, and details on a path. Returns (trees, detail cells).
        /// </summary>
        public static (int trees, int cells) ClearPaths(Scene scene, float clearance = 3.5f)
        {
            var paths = LevelForest.Paths(scene);
            var waters = LevelForest.Waters(scene);
            var arcs = NatureDressing.PassSamples();
            int trees = 0, cells = 0;
            foreach (var terrain in Terrains(scene))
            {
                var data = terrain.terrainData;
                Undo.RegisterCompleteObjectUndo(data, "Clear paths");
                var o = terrain.transform.position;
                var size = data.size;
                var kept = new List<TreeInstance>();
                foreach (var tree in data.treeInstances)
                {
                    var p = o + Vector3.Scale(tree.position, size);
                    var q = new Vector2(p.x, p.z);
                    bool onPath = (paths.Count > 0 && LevelForest.Distance(paths, q) < clearance) || LevelForest.OnWater(waters, q);
                    bool inSolid = LevelForest.HitsTheLevel(new Bounds(p + Vector3.up * 2.5f, new Vector3(1.2f, 4f, 1.2f)));
                    bool onArc = arcs.Any(a => new Vector2(a.x - p.x, a.z - p.z).magnitude < 2f + PassCorridor.DecorationClearance && a.y > p.y - 1f && a.y < p.y + 25f);
                    if (onPath || inSolid || onArc) { trees++; continue; }
                    kept.Add(tree);
                }
                data.treeInstances = kept.ToArray();
                int res = data.detailResolution;
                for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
                {
                    var map = data.GetDetailLayer(0, 0, res, res, layer);
                    for (int z = 0; z < res; z++)
                        for (int x = 0; x < res; x++)
                        {
                            if (map[z, x] == 0) continue;
                            var q = new Vector2(o.x + (x + 0.5f) / res * size.x, o.z + (z + 0.5f) / res * size.z);
                            bool onPath = paths.Count > 0 && LevelForest.Distance(paths, q) <= 1.2f;
                            if (!onPath && !LevelForest.OnWater(waters, q)) continue;
                            map[z, x] = 0;
                            cells++;
                        }
                    data.SetDetailLayer(0, 0, layer, map);
                }
                EditorUtility.SetDirty(data);
            }
            return (trees, cells);
        }

        // ------------------------------------------------------------------ on save

        /// <summary>Keeps the level's safety net under its terrains' lowest point and wider than them.</summary>
        public static bool FitSafetyNet(Scene scene)
        {
            var net = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelSafetyNet>(true)).FirstOrDefault();
            var terrains = Terrains(scene).ToList();
            if (net == null || terrains.Count == 0) return false;
            var area = terrains.Select(Area).Aggregate((a, b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax)));
            float lowest = terrains.Min(t => t.transform.position.y + t.terrainData.bounds.min.y);
            var position = new Vector3(area.center.x, lowest - LevelScenes.NetDrop, area.center.y);
            var scale = new Vector3(area.width * 3f, 4f, area.height * 3f);
            if ((net.transform.position - position).sqrMagnitude < 1e-4f && (net.transform.localScale - scale).sqrMagnitude < 1e-4f) return false;
            net.transform.position = position;
            net.transform.localScale = scale;
            EditorUtility.SetDirty(net.transform);
            return true;
        }

        /// <summary>
        /// Bakes the level's mist floor from its terrain (the ground less the level's mist depth, raised in each <see cref="MistZone"/>)
        /// into <c>Assets/Art/Generated/&lt;Level&gt;/MistFloor.asset</c> and gives it to the level's <see cref="MistField"/> (made under
        /// the level when missing; true then, the day's still mist is to be applied).
        /// </summary>
        public static bool BakeMist(Scene scene, LevelSettings settings)
        {
            var terrain = Terrains(scene).FirstOrDefault();
            if (terrain == null || string.IsNullOrEmpty(scene.path)) return false;
            var zones = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MistZone>(true)).ToList();
            float Floor(float x, float z)
            {
                float h = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y - settings.MistDepth;
                foreach (var zone in zones)
                {
                    float w = zone.Weight(x, z, out float top);
                    if (w > 0f && top > h) h = Mathf.Lerp(h, top, w);
                }
                return h;
            }
            string folder = LevelMeshStore.LevelFolder(scene);
            Directory.CreateDirectory(folder.TrimEnd('/'));
            var (texture, rect) = NatureTerrainMesh.BakeMistFloor(Floor, Area(terrain), MistSize, MistBlur, folder + "MistFloor.asset");
            var field = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MistField>(true)).FirstOrDefault();
            bool created = field == null;
            if (created)
            {
                var go = new GameObject("Mist");
                go.transform.SetParent(settings.transform, false);
                field = go.AddComponent<MistField>();
            }
            field.Configure(texture, rect, 0.93f);
            field.ConfigureScale(settings.MistDensity);
            EditorUtility.SetDirty(field);
            return created;
        }

        /// <summary>The level's audio map: each section's ambience (LevelSettings), on its section root's <see cref="CourseAudio"/>.</summary>
        public static void WriteAudio(Scene scene, LevelSettings settings)
        {
            var section = LevelRoutes.SectionRoot(scene);
            if (section == null) return;
            int checkpoints = RegisterLevelWindow.CheckpointsOf(scene);
            var envs = new string[checkpoints + 1];
            for (int i = 0; i <= checkpoints; i++) envs[i] = i < settings.SectionAmbience.Count ? settings.SectionAmbience[i] ?? "" : "";
            CourseKit.WriteCourseAudio(section, Enumerable.Repeat(1, checkpoints + 1).ToArray(), envs, settings.DefaultAmbience);
            EditorUtility.SetDirty(section.GetComponent<CourseAudio>());
        }

        /// <summary>The ambience environments the sound library has (folders of Assets/Audio/Ambience/Environments).</summary>
        public static string[] Environments() => Folders("Assets/Audio/Ambience/Environments");

        /// <summary>The ambience emitters the sound library has (folders of Assets/Audio/Ambience/Emitters).</summary>
        public static string[] Emitters() => Folders("Assets/Audio/Ambience/Emitters");

        static string[] Folders(string root) =>
            Directory.Exists(root) ? Directory.GetDirectories(root).Select(Path.GetFileName).OrderBy(n => n).ToArray() : new string[0];

        /// <summary>Everything the level's land and air need on a save; returns a line for the save's log.</summary>
        public static string OnSave(Scene scene)
        {
            var settings = LevelScenes.SettingsOf(scene);
            if (settings == null) return "";
            var parts = new List<string>();
            if (FitSafetyNet(scene)) parts.Add("safety net refitted");
            int zones = LevelCourse.FitZones(scene);
            if (zones > 0) parts.Add($"{zones} checkpoint zone(s) fitted round their campfire");
            if (Terrains(scene).Any())
            {
                if (BakeMist(scene, settings)) LookBuilder.ApplyToScene(scene);   // a new mist field takes the day's still mist
                parts.Add("mist baked");
            }
            WriteAudio(scene, settings);
            return parts.Count == 0 ? "" : ", " + string.Join(", ", parts);
        }
    }
}
