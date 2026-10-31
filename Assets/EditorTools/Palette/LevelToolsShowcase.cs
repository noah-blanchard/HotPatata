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
            ("L6 Course", new Vector2(-20f, -60f), new Vector2(40f, 50f)),
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

        [MenuItem(Menu + "Build Caves (L4)", priority = 14)]
        public static void BuildCaves() => Build("L4 Caves", CavesArea);

        [MenuItem(Menu + "Build Masonry (L5)", priority = 15)]
        public static void BuildMasonry() => Build("L5 Masonry", MasonryArea);

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
            // a switchback up the hill's flank, long enough to hold its grade
            foreach (var (u, v) in new[] { (0.03f, 0.12f), (0.1f, 0.25f), (0.16f, 0.3f), (0.08f, 0.4f), (0.13f, 0.47f) })
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

        // ------------------------------------------------------------------ L4: caves

        /// <summary>
        /// A tunnel that changes along its way (round, rift, gallery, arch; dressed), a shaped chamber (an offset crown, alcoves, a pillar, a
        /// shelf), a shaft open to the sky with a Mine Frames stair spiralling up it, a network of three chambers and three tunnels, and a
        /// cave mouth in a hillside.
        /// </summary>
        static void CavesArea(Area a)
        {
            var terrain = LevelTerrain.Terrains(a.Scene).First();
            var tunnel = a.Place(PaletteCatalog.Find("rock.tunnel"), "varied", a.At(0.06f, 0.2f), "Tunnel: round, rift, gallery, arch").GetComponent<RockShellShape>();
            tunnel.Points.Clear();
            tunnel.Points.AddRange(new[] { Vector3.zero, new Vector3(3f, 0f, 22f), new Vector3(-2f, 0.5f, 44f), new Vector3(2f, 0.5f, 64f) });
            CaveEntries.Varied(tunnel);

            a.Place(PaletteCatalog.Find("rock.cavern"), "shaped", a.At(0.26f, 0.45f), "Chamber: crown, alcoves, pillar, shelf");

            var shaft = a.Place(PaletteCatalog.Find("rock.shaft"), null, a.At(0.45f, 0.45f), "Shaft (open top) and its Mine Stair").GetComponent<RockShellShape>();
            shaft.Points.Clear();
            shaft.Points.AddRange(DeckShapes.Oval(16f, 16f, 16));
            var stair = a.Place(PaletteCatalog.Find("timber.mine-stair"), null, shaft.transform.position, null).GetComponent<MineFrameShape>();
            stair.Points.Clear();
            // two turns round the shaft, rising its height
            for (int k = 0; k <= 16; k++)
            {
                float ang = k * Mathf.PI / 4f;
                stair.Points.Add(new Vector3(Mathf.Cos(ang) * 4.6f, shaft.Height * 0.9f * k / 16f, Mathf.Sin(ang) * 4.6f));
            }
            stair.LanternEvery = 4;

            var network = a.Place(PaletteCatalog.Find("rock.cave-network"), null, a.At(0.62f, 0.25f), "Cave network: three chambers").GetComponent<CaveNetwork>();
            var nodes = network.Nodes.ToList();
            var third = CaveNetworks.AddNode(network, a.At(0.72f, 0.62f));
            CaveNetworks.Link(network, nodes[1], third);
            CaveNetworks.Link(network, third, nodes[0]);
            foreach (var node in network.Nodes) node.Dressing = new CaveDressing { stalactites = 0.5f, stalagmites = 0.4f, columns = 0.4f, puddles = 0.3f, props = 0.3f, lanternSpacing = 12f };
            foreach (var link in network.Links) link.Dressing = new CaveDressing { stalactites = 0.4f, puddles = 0.3f, lanternSpacing = 12f };

            // a hill, and the mouth on its steepest flank toward the south
            var hill = a.Place(PaletteCatalog.Find("ground.round-hill"), null, a.At(0.88f, 0.55f), "Hill").GetComponent<LandShape>();
            LevelPieceBuilder.Rebuild(hill);
            var top = Flat(hill.transform.position);
            Vector3 best = hill.transform.position, normal = Vector3.up;
            float steepest = 0f;
            for (float d = 4f; d < 60f; d += 1f)
            {
                var q = top + new Vector2(0f, -d);
                var n = terrain.terrainData.GetInterpolatedNormal((q.x - terrain.transform.position.x) / terrain.terrainData.size.x, (q.y - terrain.transform.position.z) / terrain.terrainData.size.z);
                float slope = Vector3.Angle(n, Vector3.up);
                if (slope <= steepest || slope > 35f) continue;
                steepest = slope;
                best = new Vector3(q.x, terrain.SampleHeight(new Vector3(q.x, 0f, q.y)) + terrain.transform.position.y, q.y);
                normal = n;
            }
            var mouth = CaveMouths.Make(a.Group(LevelGroup.Props), best, -normal);
            Area.Label(mouth.gameObject, $"Cave mouth on a {steepest:F0}° slope");

            LevelPieceBuilder.Rebuild(TimberLinks.Pieces(a.Scene).Where(piece => piece.transform.IsChildOf(a.Group(LevelGroup.Props)) || piece.transform.IsChildOf(a.Group(LevelGroup.Timber))).ToList());
            LevelTerrain.CutCaves(a.Scene);
            GroundBaker.Bake(terrain);
        }

        // ------------------------------------------------------------------ L5: masonry

        /// <summary>
        /// Every wall style in two rows of walls, each with a free arch and a passage (a body screen, a curtain or a door, alternately);
        /// a gate wall, a ruin fragment, and the stone door looks standing on their own.
        /// </summary>
        static void MasonryArea(Area a)
        {
            StoneKitBuilder.EnsureBuilt();
            // the kit the walls placed lives in the Kit group: what an earlier build left there (outside this area's group) goes
            var kitRoot = a.Settings.Group(LevelGroup.Kit);
            foreach (Transform old in kitRoot.Cast<Transform>().Where(t => t.name.Contains(" BombOnly") || t.name.Contains(" RunnersOnly") || t.name.Contains(" KitDoor")).ToList())
                Object.DestroyImmediate(old.gameObject);
            var styles = new (string entry, string look, string label)[]
            {
                ("masonry.stone-wall", null, "Dry stone"), ("masonry.stone-wall", "mortared", "Mortared"), ("masonry.stone-wall", "ruined", "Ruined"),
                ("masonry.stone-wall", "retaining", "Retaining"), ("masonry.timber-wall", null, "Palisade"), ("masonry.timber-wall", "plank", "Plank"),
                ("masonry.timber-wall", "logs", "Stacked logs"), ("masonry.timber-wall", "wattle", "Wattle"), ("masonry.fence", null, "Rail fence"),
                ("masonry.fence", "pickets", "Pickets"), ("masonry.fence", "rope", "Rope on posts"), ("masonry.fence", "hedge", "Hedge"),
                ("masonry.mixed-wall", null, "Stone and planks"), ("masonry.mixed-wall", "half-timbered", "Half-timbered"), ("masonry.mixed-wall", "timber-cap", "Timber cap"),
            };
            var passages = new[] { OpeningPassage.BombOnly, OpeningPassage.RunnersOnly, OpeningPassage.KitDoor };
            var made = new List<LevelPiece>();
            for (int i = 0; i < styles.Length; i++)
            {
                var (id, look, label) = styles[i];
                float u = i % 2 == 0 ? 0.3f : 0.72f, v = 0.04f + (i / 2) * 0.06f;
                var wall = a.Place(PaletteCatalog.Find(id), look, a.At(u, v), label + " wall").GetComponent<WallShape>();
                wall.Points.Clear();
                wall.Points.AddRange(new[] { new Vector3(-9f, 0f, 0f), new Vector3(0f, 0f, 2f), new Vector3(9f, 0f, 0f) });
                var passage = wall.Family == WallFamily.Fence ? OpeningPassage.KitDoor : passages[i % passages.Length];
                // a screen or a curtain needs the wall over it (else the bomb flies over the wall, a player jumps onto the screen)
                if (passage != OpeningPassage.KitDoor) wall.Height = Mathf.Max(wall.Height, 3.4f);
                float h = passage == OpeningPassage.KitDoor ? Mathf.Max(0.8f, wall.Height - 0.4f) : wall.Height - 1f;
                wall.Openings.Clear();
                if (wall.Family != WallFamily.Fence)
                    wall.Openings.Add(new WallOpening(4.5f, 1.4f, Mathf.Min(2.4f, h), OpeningPassage.Free, OpeningShape.RoundArch)
                                      { frame = wall.Family == WallFamily.Stone ? OpeningFrame.Stone : OpeningFrame.Timber });
                wall.Openings.Add(new WallOpening(12.5f, 2.6f, Mathf.Min(3f, h), passage)
                {
                    frame = wall.Family == WallFamily.Stone || wall.Family == WallFamily.Mixed ? OpeningFrame.Stone : OpeningFrame.Timber,
                    look = passage == OpeningPassage.KitDoor ? (wall.Family == WallFamily.Stone ? StoneKitBuilder.Portcullis : wall.Family == WallFamily.Mixed ? StoneKitBuilder.PlankDoor : ForestKitBuilder.Gate)
                         : wall.Family == WallFamily.Stone ? ""
                         : passage == OpeningPassage.BombOnly ? ForestKitBuilder.Net : ForestKitBuilder.Sparks,
                });
                made.Add(wall);
            }
            made.Add(a.Place(PaletteCatalog.Find("masonry.gate-wall"), null, a.At(0.3f, 0.53f), "Gate wall (a kit door in it)").GetComponent<WallShape>());
            made.Add(a.Place(PaletteCatalog.Find("masonry.ruin"), null, a.At(0.72f, 0.52f), "Ruin fragment").GetComponent<WallShape>());
            LevelPieceBuilder.Rebuild(made);
            // the kit in this area's group (a rebuild of the area clears it), each door wired to a plate before it
            int plates = 0;
            foreach (var wall in made.OfType<WallShape>())
                foreach (var o in wall.Openings.Where(o => o.kit != null))
                {
                    o.kit.transform.SetParent(a.Group(LevelGroup.Kit), true);
                    if (o.passage == OpeningPassage.KitDoor) WireToPlate(a, o.kit, -o.kit.transform.forward * 3f, ++plates);
                }

            // L5b: the stone ways, arches, ruins and towers
            GameObject P(string id, string look, float u, float v, string label) => a.Place(PaletteCatalog.Find(id), look, a.At(u, v), label);
            P("masonry.stone-stair", "built", 0.12f, 0.6f, "Stone stair (built)");
            P("masonry.stone-stair", "rock", 0.24f, 0.6f, "Stone stair (cut in rock)");
            P("masonry.paving", "terrace", 0.42f, 0.6f, "Raised terrace (slabs)");
            P("masonry.paving", "cobbles", 0.58f, 0.6f, "Cobbles");
            P("masonry.stepping-stones", null, 0.7f, 0.6f, "Stepping stones");
            var bridge = P("masonry.stone-bridge", null, 0.86f, 0.6f, "Stone bridge").GetComponent<StoneBridgeShape>();
            bridge.Start = new Vector3(0f, 2.5f, -10f);
            bridge.End = new Vector3(0f, 2.5f, 10f);
            bridge.Arches = 3;
            LevelPieceBuilder.Rebuild(bridge);
            P("masonry.arch", null, 0.1f, 0.7f, "Round arch");
            P("masonry.arch", "pointed", 0.24f, 0.7f, "Pointed arch");
            P("masonry.arch", "lintel", 0.38f, 0.7f, "Stone lintel");
            P("masonry.portal", null, 0.52f, 0.7f, "Log portal");
            P("masonry.column", null, 0.64f, 0.7f, "Column");
            P("masonry.column", "broken", 0.72f, 0.7f, "Broken column");
            P("masonry.plinth", null, 0.86f, 0.7f, "Plinth");
            P("masonry.tower", null, 0.2f, 0.8f, "Watchtower");
            P("masonry.tower", "roofed", 0.5f, 0.8f, "Roofed tower");
            P("masonry.tower", "ruined", 0.8f, 0.8f, "Ruined tower");
            // a dry-stone wall frozen into its stones, opened by hand in its middle
            var frozenWall = P("masonry.stone-wall", null, 0.3f, 0.89f, null).GetComponent<WallShape>();
            frozenWall.Points.Clear();
            frozenWall.Points.AddRange(new[] { new Vector3(-7f, 0f, 0f), new Vector3(7f, 0f, 0f) });
            LevelPieceBuilder.Rebuild(frozenWall);
            var frozen = TimberFreezer.Freeze(frozenWall);
            if (frozen != null)
            {
                Area.Label(frozen.gameObject, "Frozen dry-stone wall (opened here)");
                var c = frozen.transform.position;
                StoneFreeze.OpenHere(frozen.GetComponentsInChildren<StoneBlock>(true).Where(st => Mathf.Abs(st.transform.position.x - c.x) < 0.8f && st.transform.position.y - c.y < 1.6f).ToList());
            }
            var piers = P("timber.walkway", null, 0.7f, 0.89f, "Walkway on stone piers").GetComponent<TimberWalkShape>();
            piers.Support = TimberSupport.StonePiers;
            piers.transform.position += Vector3.up * 3f;
            LevelPieceBuilder.Rebuild(piers);
            var door = PaletteCatalog.Find("kit.door");
            var sliding = PaletteCatalog.Find("kit.sliding-door");
            foreach (var (entry, look, u, v, label) in new[]
                     {
                         (door, "portcullis", 0.15f, 0.96f, "Iron portcullis"), (door, "slab", 0.5f, 0.96f, "Stone slab"), (door, "plank", 0.85f, 0.96f, "Plank door"),
                         (sliding, "slab", 0.3f, 0.99f, "Stone slab (slides)"), (sliding, "plank", 0.7f, 0.99f, "Plank door (slides)"),
                     })
            {
                var go = a.Place(entry, look, a.At(u, v), label);
                if (go != null) WireToPlate(a, go, Vector3.back * 4f, ++plates);
            }
            var terrain = LevelTerrain.Terrains(a.Scene).FirstOrDefault();
            if (terrain != null && GroundBaker.IsLayered(terrain)) GroundBaker.Bake(terrain);
        }

        /// <summary>Sets a placed screen or curtain in the opening of a mortared wall built round it (the wall keeps it as its kit).</summary>
        static void InWall(Area a, GameObject kit, Vector3 at)
        {
            var wall = a.Place(PaletteCatalog.Find("masonry.stone-wall"), "mortared", at, null).GetComponent<WallShape>();
            wall.Height = 3.6f;
            wall.Points.Clear();
            wall.Points.AddRange(new[] { new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f) });
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(kit);
            wall.Openings.Clear();
            wall.Openings.Add(new WallOpening(5f, 2.6f, 2.6f, kit.GetComponentInChildren<BodyScreen>(true) != null ? OpeningPassage.BombOnly : OpeningPassage.RunnersOnly)
            {
                look = path.EndsWith(".prefab") ? path.Substring(0, path.Length - ".prefab".Length) : path,
                kit = kit,
                frame = OpeningFrame.Stone,
            });
            LevelPieceBuilder.Rebuild(wall);
        }

        /// <summary>A pressure plate <paramref name="offset"/> from <paramref name="door"/> on the floor, its source (one plate, one door).</summary>
        static void WireToPlate(Area a, GameObject door, Vector3 offset, int n)
        {
            var actuator = door.GetComponent<SignalActuator>();
            if (actuator == null) return;
            var at = door.transform.position + offset;
            float ground = LevelNature.GroundAt(a.Scene, new Vector2(at.x, at.z));
            if (!float.IsNaN(ground)) at.y = ground;
            var plate = CourseKit.Place(a.Group(LevelGroup.Kit), CourseKit.GameplayDir + "PressurePlate", $"Plate {n}", at, Quaternion.identity);
            var so = new SerializedObject(actuator);
            so.FindProperty("source").objectReferenceValue = plate.GetComponents<MonoBehaviour>().First(m => m is ISignalSource);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ L6: the course

        [MenuItem(Menu + "Build Course (L6)", priority = 16)]
        public static void BuildCourse() => Build("L6 Course", CourseArea);

        /// <summary>
        /// What makes the showcase a level Verify reads (L6): the start's contract (a showcase forces nothing), a checkpoint (a campfire)
        /// with its contract, and the finish, near the start.
        /// </summary>
        static void CourseArea(Area a)
        {
            const string Force = "Nothing: the level tools' showcase, walked by the route runner (no puzzle, no lock).";
            // a piece straight under a level group belongs to no area: left by an earlier build
            foreach (LevelGroup g in Enum.GetValues(typeof(LevelGroup)))
                foreach (Transform t in a.Settings.Group(g).Cast<Transform>().Where(t => t.GetComponent<LevelPiece>() != null).ToList())
                    Object.DestroyImmediate(t.gameObject);
            foreach (var old in a.Settings.GetComponentsInChildren<SectionContract>(true).Where(c => c.GetComponentInParent<Checkpoint>() == null).ToList())
                Object.DestroyImmediate(old.gameObject);
            LevelCourse.AddContract(a.Settings.transform, a.Settings.MinPlayers).Configure(Force);
            var checkpoint = LevelCourse.AddCheckpoint(a.Scene, a.Group(LevelGroup.Kit), a.At(0.5f, 0.3f));
            LevelCourse.AddContract(checkpoint.transform, a.Settings.MinPlayers).Configure(Force);
            Area.Label(checkpoint.gameObject, "Checkpoint (campfire)");
            Area.Label(LevelCourse.AddFinish(a.Group(LevelGroup.Kit), a.At(0.5f, 0.8f)).gameObject, "Finish");
            Landings(a);
        }

        /// <summary>A small timber deck where a walk ends in the air (a stair's top, a bridge's end): somewhere to step onto, as a level would have.</summary>
        static void Landings(Area a)
        {
            Physics.SyncTransforms();
            var walks = TimberLinks.Pieces(a.Scene).Select(p => (p, output: p.Output != null ? p.Output.GetComponent<LevelPieceOutput>() : null))
                .Where(x => x.output != null && x.output.WalkLine.Length > 1).Select(x => (x.p.name, (IList<Vector3>)x.output.WalkLine)).ToList();
            var deck = PaletteCatalog.Find("timber.deck");
            int n = 0;
            foreach (var problem in WalkProbe.Run(walks).Where(p => p.message.Contains("past its")))
            {
                var go = a.Place(deck, null, problem.at, $"Landing {++n}");
                if (go == null) continue;
                var d = go.GetComponent<TimberDeckShape>();
                d.transform.position = problem.at;
                d.Support = TimberSupport.Posts;
                d.Outline.Clear();
                d.Outline.AddRange(new[] { new Vector3(-1.4f, 0f, -1.4f), new Vector3(1.4f, 0f, -1.4f), new Vector3(1.4f, 0f, 1.4f), new Vector3(-1.4f, 0f, 1.4f) });
                d.Rails = false;
                LevelPieceBuilder.Rebuild(d);
            }
            Physics.SyncTransforms();
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
                // a screen or a curtain closes a passage: set in a wall's opening (alone, a player walks round it)
                if (go.GetComponentInChildren<BodyScreen>(true) != null || go.GetComponentInChildren<BombBarrier>(true) != null) InWall(a, go, at);
                // a trail or a river kept to its cell (the next cell's piece would stand across it)
                if (go.TryGetComponent<TrailShape>(out var tourTrail))
                {
                    tourTrail.Points.Clear();
                    tourTrail.Points.AddRange(new[] { new Vector3(0f, 0f, -6f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 6f) });
                    LevelPieceBuilder.Rebuild(tourTrail);
                }
                if (go.TryGetComponent<RiverShape>(out var tourRiver))
                {
                    tourRiver.Points.Clear();
                    tourRiver.Points.AddRange(new[] { new Vector3(0f, 0f, -8f), new Vector3(2f, 0f, 0f), new Vector3(0f, 0f, 8f) });
                    LevelPieceBuilder.Rebuild(tourRiver);
                }
                // a wall's kit door is wired to a plate before it
                if (go.TryGetComponent<WallShape>(out var gateWall))
                    foreach (var o in gateWall.Openings.Where(o => o.kit != null && o.passage == OpeningPassage.KitDoor))
                        WireToPlate(a, o.kit, -o.kit.transform.forward * 3f, 100 + i);
                // a loose rope lies on the ground
                if (go.TryGetComponent<TimberPath>(out var rope) && rope.Kind == TimberElementKind.Rope)
                {
                    rope.transform.position = at + Vector3.up * 0.03f;
                    rope.Sag = 0f;
                    TimberElementBuilder.Refresh(rope, true);
                }
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
