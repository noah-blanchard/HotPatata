using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// A level terrain's heights in two layers (ARCHITECTURE §27.4, docs/LEVEL_AUTHORING.md §27): the ground is the author's sculpt (the
    /// base, <see cref="GroundHeightBase"/>) plus the land pieces (<see cref="GroundShapes"/>) applied in order. A brush stroke (Unity's or
    /// Terrain Tools') goes to the base: what it changed is added to it, so the shapes stay movable; a shape moved, edited or deleted
    /// recomposes only its old and new footprints. Each recomposition is one undo step. The stack is made with the level's first land piece.
    /// Menu HotPatata/Ground/Rebuild Land Shapes recomposes the whole terrain.
    /// </summary>
    [InitializeOnLoad]
    public static class TerrainStack
    {
        sealed class State
        {
            public GroundHeightBase asset;
            public float[] base_, last;
            public int res;
        }

        static readonly Dictionary<TerrainData, State> states = new Dictionary<TerrainData, State>();
        static readonly HashSet<TerrainData> pendingSync = new HashSet<TerrainData>();
        static bool composing, mouseHeld;

        /// <summary>Off while tests or builders write heights of their own.</summary>
        public static bool Listening = true;

        static TerrainStack()
        {
            TerrainCallbacks.heightmapChanged += OnHeightmapChanged;
            SceneView.duringSceneGui += TrackMouse;
            EditorApplication.update += SyncWhenReleased;
            EditorSceneManager.sceneClosed += _ => states.Clear();
            Undo.undoRedoPerformed += () => states.Clear();
        }

        public static string BasePath(Terrain terrain)
        {
            string data = AssetDatabase.GetAssetPath(terrain.terrainData);
            string folder = string.IsNullOrEmpty(data) ? LevelMeshStore.LevelFolder(terrain.gameObject.scene) + "Terrain/" : Path.GetDirectoryName(data).Replace('\\', '/') + "/";
            return folder + "HeightBase.asset";
        }

        /// <summary>True when the terrain's heights are a base plus shapes.</summary>
        public static bool Has(Terrain terrain) => terrain != null && AssetDatabase.LoadAssetAtPath<GroundHeightBase>(BasePath(terrain)) != null;

        static State Get(Terrain terrain, bool create)
        {
            var data = terrain.terrainData;
            int res = data.heightmapResolution;
            if (states.TryGetValue(data, out var s) && s.res == res && s.asset != null) return s;
            var asset = AssetDatabase.LoadAssetAtPath<GroundHeightBase>(BasePath(terrain));
            var current = Flatten(data.GetHeights(0, 0, res, res), res);
            if (asset == null)
            {
                if (!create) return null;
                Directory.CreateDirectory(Path.GetDirectoryName(BasePath(terrain)));
                asset = ScriptableObject.CreateInstance<GroundHeightBase>();
                asset.Write(res, current);
                AssetDatabase.CreateAsset(asset, BasePath(terrain));
            }
            float[] b = asset.Resolution == res ? asset.Read() : current;
            s = new State { asset = asset, base_ = b, last = current, res = res };
            states[data] = s;
            return s;
        }

        static float[] Flatten(float[,] h, int res)
        {
            var a = new float[res * res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++) a[z * res + x] = h[z, x];
            return a;
        }

        // ------------------------------------------------------------------ composing

        /// <summary>
        /// The ground over the world rectangle <paramref name="area"/>: the base, then every land piece of the scene in order. Writes the
        /// terrain's heights there (one undo step) and remembers the shapes. Makes the stack when <paramref name="create"/>.
        /// </summary>
        public static void Recompose(Terrain terrain, Rect area, bool create = true)
        {
            var s = Get(terrain, create);
            if (s == null) return;
            var data = terrain.terrainData;
            int res = s.res;
            var field = new GroundField(res, LevelTerrain.Area(terrain));
            var c0 = field.CellOf(area.min);
            var c1 = field.CellOf(area.max);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(c0.x) - 1, 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt(c1.x) + 1, 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(c0.y) - 1, 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt(c1.y) + 1, 0, res - 1);
            if (x1 < x0 || z1 < z0) return;
            float y0 = terrain.transform.position.y, span = data.size.y;
            // the whole running field (shapes sample the ground along their lines beyond the area), the base in world metres
            for (int i = 0; i < field.Values.Length; i++) field.Values[i] = y0 + s.base_[i] * span;
            var cells = new RectInt(0, 0, res, res);
            var shapes = GroundShapes.Of(terrain.gameObject.scene);
            foreach (var shape in shapes) shape.Apply(field, cells);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var heights = new float[h, w];
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    float v = Mathf.Clamp01((field[x, z] - y0) / span);
                    heights[z - z0, x - x0] = v;
                    s.last[z * res + x] = v;
                }
            Undo.RegisterCompleteObjectUndo(data, "Land shapes");
            composing = true;
            try { data.SetHeights(x0, z0, heights); }
            finally { composing = false; }
            s.asset.WriteShapes(shapes.Select(c => c.Id).ToArray(), shapes.Select(c => c.Bounds).ToArray(), shapes.Select(c => c.Signature).ToArray());
            EditorUtility.SetDirty(data);
            var splat = terrain.GetComponent<GroundSplat>();
            if (splat != null) splat.Apply();
        }

        /// <summary>Recomposes every terrain of <paramref name="scene"/> that has a stack (or has land pieces), whole.</summary>
        public static int RecomposeAll(Scene scene)
        {
            int n = 0;
            bool any = GroundShapes.Of(scene).Count > 0;
            foreach (var t in LevelTerrain.Terrains(scene))
            {
                if (!Has(t) && !any) continue;
                Recompose(t, LevelTerrain.Area(t));
                n++;
            }
            return n;
        }

        [MenuItem("HotPatata/Ground/Rebuild Land Shapes", priority = 10)]
        static void RebuildMenu()
        {
            int n = RecomposeAll(SceneManager.GetActiveScene());
            Debug.Log($"[Ground] land shapes recomposed on {n} terrain(s).");
        }

        /// <summary>
        /// Brings the terrains of <paramref name="scene"/> up to date with its land pieces: a piece added, changed, moved or deleted since the
        /// last composition recomposes its old and new footprints. Returns the shapes that changed.
        /// </summary>
        public static int Sync(Scene scene)
        {
            var shapes = GroundShapes.Of(scene);
            int changed = 0;
            foreach (var t in LevelTerrain.Terrains(scene))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GroundHeightBase>(BasePath(t));
                if (asset == null && shapes.Count == 0) continue;
                var known = asset == null ? new Dictionary<string, (Rect, string)>()
                    : asset.ShapeIds.Select((id, i) => (id, i)).ToDictionary(p => p.id, p => (asset.ShapeBounds[p.i], asset.ShapeSignatures[p.i]));
                var area = new List<Rect>();
                foreach (var c in shapes)
                {
                    if (known.TryGetValue(c.Id, out var old) && old.Item2 == c.Signature) continue;
                    area.Add(c.Bounds);
                    if (known.ContainsKey(c.Id)) area.Add(old.Item1);
                }
                var ids = new HashSet<string>(shapes.Select(c => c.Id));
                foreach (var gone in known.Where(k => !ids.Contains(k.Key))) area.Add(gone.Value.Item1);
                // a reordered stack changes what lies where: everything the shapes cover
                if (asset != null && area.Count == 0 && !asset.ShapeIds.SequenceEqual(shapes.Select(c => c.Id))) area.AddRange(shapes.Select(c => c.Bounds));
                if (area.Count == 0) continue;
                var box = area[0];
                foreach (var r in area.Skip(1)) box = Rect.MinMaxRect(Mathf.Min(box.xMin, r.xMin), Mathf.Min(box.yMin, r.yMin), Mathf.Max(box.xMax, r.xMax), Mathf.Max(box.yMax, r.yMax));
                Recompose(t, box);
                changed += area.Count;
            }
            return changed;
        }

        // ------------------------------------------------------------------ the author's strokes go to the base

        static void TrackMouse(SceneView view)
        {
            var e = Event.current;
            if (e.rawType == EventType.MouseDown && e.button == 0) mouseHeld = true;
            if (e.rawType == EventType.MouseUp && e.button == 0) mouseHeld = false;
        }

        static void OnHeightmapChanged(Terrain terrain, RectInt region, bool synched)
        {
            if (composing || !Listening || terrain == null || terrain.terrainData == null) return;
            if (!synched)
            {
                // a GPU brush stroke: the heights are read back once the mouse is released
                pendingSync.Add(terrain.terrainData);
                return;
            }
            var data = terrain.terrainData;
            if (!states.TryGetValue(data, out var s))
            {
                if (!Has(terrain)) return;
                s = Get(terrain, false);
                if (s == null) return;
            }
            int res = data.heightmapResolution;
            if (s.res != res) return;
            int x0 = Mathf.Clamp(region.xMin, 0, res - 1), z0 = Mathf.Clamp(region.yMin, 0, res - 1);
            int x1 = Mathf.Clamp(region.xMax, 0, res), z1 = Mathf.Clamp(region.yMax, 0, res);
            if (x1 <= x0 || z1 <= z0) return;
            var h = data.GetHeights(x0, z0, x1 - x0, z1 - z0);
            bool any = false;
            for (int z = z0; z < z1; z++)
                for (int x = x0; x < x1; x++)
                {
                    int i = z * res + x;
                    float v = h[z - z0, x - x0], delta = v - s.last[i];
                    if (Mathf.Abs(delta) < 1e-7f) continue;
                    s.base_[i] = Mathf.Clamp01(s.base_[i] + delta);
                    s.last[i] = v;
                    any = true;
                }
            if (!any) return;
            Undo.RegisterCompleteObjectUndo(s.asset, "Sculpt");
            s.asset.Write(res, s.base_);
        }

        static void SyncWhenReleased()
        {
            if (pendingSync.Count == 0 || mouseHeld) return;
            var list = pendingSync.ToList();
            pendingSync.Clear();
            foreach (var data in list)
                if (data != null) data.SyncHeightmap();
        }

        /// <summary>Takes the terrain's current heights as the base where no shape lies (a resample, a reset): the stack starts again from them.</summary>
        public static void Rebase(Terrain terrain, float[] baseHeights)
        {
            var data = terrain.terrainData;
            int res = data.heightmapResolution;
            var asset = AssetDatabase.LoadAssetAtPath<GroundHeightBase>(BasePath(terrain));
            if (asset == null) return;
            asset.Write(res, baseHeights);
            states.Remove(data);
        }

        /// <summary>The base's heights (normalised), or null.</summary>
        public static float[] Base(Terrain terrain) => Get(terrain, false)?.base_;
    }
}
