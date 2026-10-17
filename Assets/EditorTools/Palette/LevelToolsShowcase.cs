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
