using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// The level tools' dev showcase (M21, docs/LEVEL_AUTHORING.md §25): <see cref="ScenePath"/>, a level made with New Level (1024 m,
    /// never in the menu) where each phase of the tools shows its pieces in its own area, each item labelled. Opened in the Editor, played
    /// from its start and walked by the route runner; the gallery screenshots are taken there. Menu HotPatata/Level/Showcase.
    /// </summary>
    public static class LevelToolsShowcase
    {
        public const string Folder = "Assets/Scenes/Dev";
        public const string Name = "LevelToolsShowcase";
        public const string ScenePath = Folder + "/" + Name + ".unity";
        public const float Size = 1024f;
        public const int Resolution = 1025;
        const string Menu = "HotPatata/Level/Showcase/";

        /// <summary>The areas, by phase: where each lies from the start (its south-west corner and size, m).</summary>
        public static readonly (string name, Vector2 corner, Vector2 size)[] Areas =
        {
            ("L0 Palette tour", new Vector2(-250f, 30f), new Vector2(220f, 240f)),
            ("L1 Ground layers", new Vector2(30f, 30f), new Vector2(220f, 220f)),
            ("L2 Ground shaping", new Vector2(-250f, 280f), new Vector2(500f, 200f)),
            ("L3 Rock", new Vector2(280f, -200f), new Vector2(200f, 450f)),
            ("L4 Caves", new Vector2(-250f, -480f), new Vector2(500f, 230f)),
            ("L5 Masonry", new Vector2(-480f, -200f), new Vector2(200f, 450f)),
        };

        [MenuItem(Menu + "Create Showcase", priority = 0)]
        public static void Create()
        {
            if (System.IO.File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            LevelScenes.Create(Name, "Level Tools Showcase", 1, LevelDay.Ranger, 4, Folder, Size, Resolution);
        }

        [MenuItem(Menu + "Build Palette Tour (L0)", priority = 10)]
        public static void BuildPaletteTour() => Build("L0 Palette tour", PaletteTour);

        [MenuItem(Menu + "Build Ground Layers (L1)", priority = 11)]
        public static void BuildGroundLayers() => Build("L1 Ground layers", GroundArea);

        [MenuItem(Menu + "Build Ground Shaping (L2)", priority = 12)]
        public static void BuildGroundShaping() => Build("L2 Ground shaping", ShapingArea);

        [MenuItem(Menu + "Build Rock (L3)", priority = 13)]
        public static void BuildRock() => Build("L3 Rock", RockArea);

        /// <summary>Opens the showcase (made if missing), clears area <paramref name="area"/>'s groups and fills it again.</summary>
        public static void Build(string area, Action<Area> fill)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (!System.IO.File.Exists(ScenePath)) LevelScenes.Create(Name, "Level Tools Showcase", 1, LevelDay.Ranger, 4, Folder, Size, Resolution);
                scene = EditorSceneManager.OpenScene(ScenePath);
            }
            var settings = LevelScenes.SettingsOf(scene);
            bool watcher = LevelPieceWatcher.Enabled;
            LevelPieceWatcher.Enabled = false;
            try
            {
                var a = new Area(scene, settings, area);
                a.Clear();
                fill(a);
                LevelPieceBuilder.Refresh(TimberLinks.Pieces(scene));
            }
            finally { LevelPieceWatcher.Enabled = watcher; }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Showcase] {area} built.");
        }

        /// <summary>One area of the showcase: its groups (one per level group), its rectangle, and placing labelled items in it.</summary>
        public sealed class Area
        {
            public readonly Scene Scene;
            public readonly LevelSettings Settings;
            public readonly string Name;
            public readonly Vector3 Start;
            public readonly Rect Rect;

            public Area(Scene scene, LevelSettings settings, string name)
            {
                Scene = scene;
                Settings = settings;
                Name = name;
                Start = StartOf(scene);
                var (_, corner, size) = Areas.First(a => a.name == name);
                Rect = new Rect(new Vector2(Start.x, Start.z) + corner, size);
            }

            /// <summary>The area's own group inside the level group <paramref name="group"/>.</summary>
            public Transform Group(LevelGroup group)
            {
                var parent = Settings.Group(group);
                var t = parent.Find(Name);
                if (t != null) return t;
                t = new GameObject(Name).transform;
                t.SetParent(parent, false);
                return t;
            }

            public void Clear()
            {
                foreach (LevelGroup g in Enum.GetValues(typeof(LevelGroup)))
                {
                    var t = Settings.Group(g).Find(Name);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }
            }

            /// <summary>A point of the area: (u, v) in 0..1 across it, on the ground.</summary>
            public Vector3 At(float u, float v)
            {
                var p = new Vector2(Mathf.Lerp(Rect.xMin, Rect.xMax, u), Mathf.Lerp(Rect.yMin, Rect.yMax, v));
                float y = LevelNature.GroundAt(Scene, p);
                return new Vector3(p.x, float.IsNaN(y) ? Start.y : y, p.y);
            }

            /// <summary>Places <paramref name="entry"/>'s <paramref name="look"/> at <paramref name="at"/>, moves it into the area and labels it.</summary>
            public GameObject Place(PaletteEntry entry, string look, Vector3 at, string label = null)
            {
                var c = PlaceContext.At(at, Vector3.up);
                c.Scene = Scene;
                var go = PaletteCatalog.Place(entry, look, c);
                if (go == null) return null;
                Adopt(go, label ?? entry.Label + (look != null && look != entry.DefaultLook.Id ? " — " + entry.Look(look).Label : ""));
                return go;
            }

            /// <summary>Moves <paramref name="go"/> from its level group into this area's group there, and labels it.</summary>
            public void Adopt(GameObject go, string label)
            {
                foreach (LevelGroup g in Enum.GetValues(typeof(LevelGroup)))
                    if (go.transform.parent == Settings.Group(g))
                    {
                        go.transform.SetParent(Group(g), true);
                        break;
                    }
                if (label != null) Label(go, label);
            }

            public static void Label(GameObject go, string text)
            {
                var label = go.GetComponent<ShowcaseLabel>() ?? go.AddComponent<ShowcaseLabel>();
                label.Text = text;
            }
        }

        static Vector3 StartOf(Scene scene)
        {
            var starts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerSpawn>(true))
                              .Where(s => s.GetComponentInParent<Checkpoint>() == null).Select(s => s.transform.position).ToList();
            return starts.Count > 0 ? starts.Aggregate(Vector3.zero, (a, b) => a + b) / starts.Count : Vector3.zero;
        }

        // ------------------------------------------------------------------ L1: the ground's layers

        /// <summary>
        /// A meadow rolling gently, a hill with a steep face (rock and scree), a pond with its shore (mud, riverbed), a walkway on the
        /// ground (trail), and a path painted by hand that the rules' bake keeps.
        /// </summary>
        static void GroundArea(Area a)
        {
            var terrain = LevelTerrain.Terrains(a.Scene).First();
            var data = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Showcase ground");
            int res = data.heightmapResolution;
            var o = terrain.transform.position;
            float span = data.size.y, cell = data.size.x / (res - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt((a.Rect.xMin - o.x) / cell), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((a.Rect.xMax - o.x) / cell), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((a.Rect.yMin - o.z) / cell), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((a.Rect.yMax - o.z) / cell), 0, res - 1);
            var h = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            float floor = (a.Start.y - o.y) / span;
            Vector2 hill = Flat(a.At(0.72f, 0.7f)), pond = Flat(a.At(0.3f, 0.32f));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(o.x + x * cell, o.z + z * cell);
                    // fade the shapes out at the area's edge
                    float edge = Mathf.Min(Mathf.Min(p.x - a.Rect.xMin, a.Rect.xMax - p.x), Mathf.Min(p.y - a.Rect.yMin, a.Rect.yMax - p.y));
                    float keep = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 15f));
                    float m = (Mathf.PerlinNoise(p.x * 0.03f, p.y * 0.03f) - 0.5f) * 2.2f;
                    // the hill: a gentle back, a steep face to the south-west (rock over 45°, scree below)
                    var dh = p - hill;
                    float face = Vector2.Dot(dh.normalized, new Vector2(-0.7f, -0.7f));
                    float radius = Mathf.Lerp(48f, 26f, Mathf.Clamp01(face));
                    float t = Mathf.Clamp01(1f - dh.magnitude / radius);
                    float hillH = 24f * (face > 0.3f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2.4f)) : Mathf.SmoothStep(0f, 1f, t));
                    // the pond: a basin 3 m deep
                    float pd = (p - pond).magnitude;
                    float pondH = -3.2f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - pd / 24f) * 1.6f);
                    h[z - z0, x - x0] = floor + (m + hillH + pondH) * keep / span;
                }
            data.SetHeights(x0, z0, h);
            Physics.SyncTransforms();

            var water = PaletteCatalog.Find("ground.water");
            var w = a.Place(water, null, new Vector3(pond.x, a.Start.y - 1.4f, pond.y), "Pond: riverbed under it, mud on its shore");
            if (w != null)
            {
                var shape = w.GetComponent<WaterShape>();
                shape.Size = new Vector2(30f, 30f);
                shape.Depth = 3f;
                w.transform.position = new Vector3(pond.x, a.Start.y - 1.4f - 0.5f, pond.y);
                LevelPieceBuilder.Rebuild(shape);
            }
            var walkEntry = PaletteCatalog.Find("timber.walkway");
            var start = a.At(0.08f, 0.85f);
            var walkGo = a.Place(walkEntry, null, start, "Walkway on the ground: trail along it");
            if (walkGo != null)
            {
                var walk = walkGo.GetComponent<TimberWalkShape>();
                walk.OnGround = true;
                walk.RailLeft = walk.RailRight = false;
                walk.Points.Clear();
                for (int i = 0; i <= 6; i++)
                {
                    var q = a.At(0.08f + i * 0.07f, 0.85f + Mathf.Sin(i * 0.9f) * 0.04f) - start;
                    walk.Points.Add(new Vector3(q.x, LevelNature.GroundAt(a.Scene, Flat(start + q)) - start.y + 0.05f, q.z));
                }
                LevelPieceBuilder.Rebuild(walk);
            }
            LevelPieceBuilder.Refresh(TimberLinks.Pieces(a.Scene));

            // the rules, then a path painted by hand (trail and gravel), then the rules again: the path stays
            GroundBaker.Bake(terrain);
            int ar = data.alphamapResolution;
            var field = new GroundField(ar, LevelTerrain.Area(terrain));
            var path = new List<Vector2>();
            for (int i = 0; i <= 20; i++) path.Add(Flat(a.At(0.12f + i * 0.035f, 0.12f + Mathf.Sin(i * 0.45f) * 0.06f + i * 0.004f)));
            var maps = data.GetAlphamaps(0, 0, ar, ar);
            for (int i = 0; i + 1 < path.Count; i++)
                field.Segment(path[i], path[i + 1], 1.6f, (x, z, _) =>
                {
                    float gravel = Mathf.PerlinNoise(x * 0.4f, z * 0.4f) > 0.6f ? 0.35f : 0f;
                    for (int l = 0; l < GroundBaker.Layers; l++) maps[z, x, l] = l == GroundLayerBuilder.Trail ? 1f - gravel : l == GroundLayerBuilder.Gravel ? gravel : 0f;
                });
            data.SetAlphamaps(0, 0, maps);
            GroundBaker.Bake(terrain);
            var marker = a.Place(PaletteCatalog.Find("structure.sign"), null, new Vector3(path[0].x, LevelNature.GroundAt(a.Scene, path[0]), path[0].y), "Path painted by hand (kept by the bake)");
            if (marker != null) marker.transform.position += new Vector3(-3f, 0f, 0f);
        }

        static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);

        // ------------------------------------------------------------------ L2: shaping the ground

        /// <summary>A round hill with a trail climbing it, a long ridge, a river winding across and a dry ravine, then the rules baked.</summary>
        static void ShapingArea(Area a)
        {
            var terrain = LevelTerrain.Terrains(a.Scene).First();
            Vector3 Local(Vector3 world, Vector3 origin) => world - origin;
            T Shape<T>(string id, float u, float v, string label) where T : LevelPiece => a.Place(PaletteCatalog.Find(id), null, a.At(u, v), label).GetComponent<T>();

            var hill = Shape<LandShape>("ground.round-hill", 0.2f, 0.55f, "Round hill (a land shape)");
            hill.SurfaceNoise = 0.8f;

            var ridge = Shape<LandShape>("ground.long-ridge", 0.6f, 0.62f, "Long ridge");
            ridge.Height = 16f;

            var trail = Shape<TrailShape>("ground.trail", 0.03f, 0.12f, "Trail climbing the hill (held to 30 %)");
            var origin = trail.transform.position;
            trail.Points.Clear();
            foreach (var (u, v) in new[] { (0.03f, 0.12f), (0.1f, 0.25f), (0.13f, 0.4f), (0.17f, 0.5f), (0.2f, 0.55f) })
                trail.Points.Add(Local(a.At(u, v), origin));

            var river = Shape<RiverShape>("ground.river", 0.32f, 0.02f, "River (water over kill zones)");
            origin = river.transform.position;
            river.Points.Clear();
            foreach (var (u, v) in new[] { (0.32f, 0.02f), (0.45f, 0.12f), (0.6f, 0.08f), (0.75f, 0.2f), (0.98f, 0.18f) })
                river.Points.Add(Local(a.At(u, v), origin));
            river.Slope = 1.5f;

            var ravine = Shape<RiverShape>("ground.ravine", 0.82f, 0.95f, "Ravine (dry, scree bed)");
            origin = ravine.transform.position;
            ravine.Points.Clear();
            foreach (var (u, v) in new[] { (0.82f, 0.95f), (0.88f, 0.75f), (0.86f, 0.55f), (0.92f, 0.4f) })
                ravine.Points.Add(Local(a.At(u, v), origin));

            foreach (var piece in new LevelPiece[] { hill, ridge, trail, river, ravine }) EditorUtility.SetDirty(piece);
            LevelPieceBuilder.Rebuild(new LevelPiece[] { hill, ridge, river, ravine, trail });
            GroundBaker.Bake(terrain);
        }

        // ------------------------------------------------------------------ L3: rock v2

        /// <summary>A cliff of each of the six rocks, each with its detail, scree, talus, foot band and decals, and an outcrop; then the rules baked.</summary>
        static void RockArea(Area a)
        {
            var terrain = LevelTerrain.Terrains(a.Scene).First();
            var entry = PaletteCatalog.Find("rock.cliff");
            var looks = new[] { ("default", "Granite"), ("sandstone", "Sandstone"), ("mossy", "Mossy"), ("limestone", "Limestone"), ("basalt", "Basalt"), ("earth", "Earth bank") };
            var made = new List<LevelPiece>();
            for (int i = 0; i < looks.Length; i++)
            {
                var (look, label) = looks[i];
                float u = i % 2 == 0 ? 0.3f : 0.72f, v = 0.1f + (i / 2) * 0.3f;
                var go = a.Place(entry, look == "default" ? null : look, a.At(u, v), label + " cliff");
                var cliff = go.GetComponent<CliffShape>();
                cliff.Outline.Clear();
                float w = cliff.Rock == CliffRock.Earth ? 14f : 18f;
                cliff.Outline.AddRange(Enumerable.Range(0, 12).Select(k =>
                {
                    float ang = k * Mathf.PI * 2f / 12f, r = 1f + 0.18f * Mathf.Sin(ang * 3f + i);
                    return new Vector3(Mathf.Cos(ang) * w * 0.5f * r, 0f, Mathf.Sin(ang) * w * 0.32f * r);
                }));
                if (cliff.Rock != CliffRock.Earth)
                {
                    cliff.Height = 12f;
                    cliff.Ledges.Clear();
                    cliff.Ledges.Add(new CliffLedge { height = 5f, inset = 1.2f });
                    cliff.Crest = 1.5f;
                }
                made.Add(cliff);
            }
            made.Add(a.Place(PaletteCatalog.Find("rock.outcrop"), null, a.At(0.5f, 0.95f), "Rock outcrop").GetComponent<CliffShape>());
            LevelPieceBuilder.Rebuild(made);
            GroundBaker.Bake(terrain);
        }

        // ------------------------------------------------------------------ L0: every entry once

        /// <summary>Every code entry and look of the palette once, in a grid by tab, each with a sign beside it (tools left out).</summary>
        static void PaletteTour(Area a)
        {
            var items = new List<(PaletteEntry entry, string look)>();
            foreach (var e in PaletteCatalog.CodeEntries.Where(e => e.Kind != PaletteKind.Tool && e.Id != "course.finish" && e.Id != "course.checkpoint"))
                foreach (var look in e.Looks) items.Add((e, look.Id == e.DefaultLook.Id ? null : look.Id));
            const int columns = 9;
            int rows = (items.Count + columns - 1) / columns;
            var sign = PaletteCatalog.Find("structure.sign");
            for (int i = 0; i < items.Count; i++)
            {
                var (entry, look) = items[i];
                float u = (i % columns + 0.5f) / columns, v = (i / columns + 0.5f) / rows;
                var at = a.At(u, v);
                var go = a.Place(entry, look, at);
                if (go == null) continue;
                if (go.TryGetComponent<ForestArea>(out var forest))
                {
                    forest.Outline.Clear();
                    forest.Outline.AddRange(new[] { new Vector3(-9f, 0f, -9f), new Vector3(9f, 0f, -9f), new Vector3(9f, 0f, 9f), new Vector3(-9f, 0f, 9f) });
                    LevelPieceBuilder.Rebuild(forest);
                }
                var board = a.Place(sign, null, at + new Vector3(-9f, 0f, -9f), null);
                if (board != null)
                {
                    var s = board.GetComponent<SignShape>();
                    s.Posts = 1;
                    s.Roof = false;
                    s.Glyph = SignGlyph.None;
                    s.Board = new Vector2(1f, 0.6f);
                    s.BoardHeight = 1.4f;
                    LevelPieceBuilder.Rebuild(s);
                    Area.Label(board, go.GetComponent<ShowcaseLabel>().Text);
                }
            }
        }
    }
}
