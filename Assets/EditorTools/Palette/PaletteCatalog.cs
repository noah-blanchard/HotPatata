using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>What a palette entry places (ARCHITECTURE §27.3).</summary>
    public enum PaletteKind { Piece, Kit, Prop, Structure, Preset, Element, Course, Air, Tool }

    /// <summary>What a placed entry snaps to (ARCHITECTURE §27.3): one toggle per kind in the palette, all on by default, Alt held turns them off.</summary>
    [Flags]
    public enum PaletteSnap { None = 0, Ground = 1, Floor = 2, DeckEdge = 4, WallEnd = 8, WallOpening = 16, Normal = 32 }

    /// <summary>
    /// A static method that places one thing of the level (ARCHITECTURE §27.3): <c>static GameObject Name(PlaceContext c)</c>. Its
    /// section is "Tab/Section"; the entries of a section keep their order in the source file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PaletteItemAttribute : Attribute
    {
        public readonly string Id, Section, Label;
        public readonly int Line;
        public PaletteKind Kind = PaletteKind.Piece;
        public PaletteSnap Snap = PaletteSnap.Ground | PaletteSnap.Floor;
        public bool Burst;
        public string Tags = "", Tooltip = "";
        /// <summary>The look the default method places (the others are <see cref="PaletteLookAttribute"/>s).</summary>
        public string LookLabel = "KayKit";
        /// <summary>A prefab its tile shows (else the palette renders the entry, or shows an icon).</summary>
        public string Prefab = "";
        /// <summary>An editor icon (EditorGUIUtility.IconContent name) for what has no shape to render.</summary>
        public string Icon = "";
        /// <summary>The piece it makes (its presets are offered beside it).</summary>
        public Type Piece;
        /// <summary>Another tab the palette lists it in too (never a second menu item).</summary>
        public string Also = "";

        public PaletteItemAttribute(string id, string section, string label, [CallerLineNumber] int line = 0)
        {
            Id = id;
            Section = section;
            Label = label;
            Line = line;
        }
    }

    /// <summary>Another look of a palette entry (a kit system's forest or stone look): <c>static GameObject Name(PlaceContext c)</c>.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PaletteLookAttribute : Attribute
    {
        public readonly string Entry, Id, Label;
        public readonly int Line;
        public string Prefab = "", Tooltip = "";

        public PaletteLookAttribute(string entry, string id, string label, [CallerLineNumber] int line = 0)
        {
            Entry = entry;
            Id = id;
            Label = label;
            Line = line;
        }
    }

    /// <summary>One look of an entry: its label and what it places.</summary>
    public sealed class PaletteLook
    {
        public string Id, Label, Tooltip, Prefab;
        public Func<PlaceContext, GameObject> Create;
    }

    /// <summary>
    /// One placeable thing of the level (ARCHITECTURE §27.3): a piece, a kit system and its looks, a prop, a library structure, a preset,
    /// an element, a course marker or a sound. The palette and the GameObject/HotPatata Level menus are both made from these.
    /// </summary>
    public sealed class PaletteEntry
    {
        public string Id, Tab, Section, Label, Tooltip, Tags;
        public PaletteKind Kind;
        public PaletteSnap Snap;
        public bool Burst;
        /// <summary>Made from code (a menu item each); asset entries (props, the library, presets) are in the palette only.</summary>
        public bool InMenu;
        public int Order;
        public List<PaletteLook> Looks = new List<PaletteLook>();
        public string Icon;
        /// <summary>The piece it makes, if it makes one.</summary>
        public Type PieceType;
        /// <summary>Another tab the palette lists it in.</summary>
        public string AlsoIn = "";
        /// <summary>Burst entries: the prefabs a click picks from, and the size range.</summary>
        public string[] BurstPrefabs = Array.Empty<string>();
        public Vector2 BurstScale = Vector2.one;
        /// <summary>A preset entry: the preset applied to the new piece.</summary>
        public TimberPreset Preset;

        public string Path => Tab + "/" + Section;
        public PaletteLook DefaultLook => Looks[0];
        public PaletteLook Look(string id) => Looks.FirstOrDefault(l => l.Id == id) ?? Looks[0];

        /// <summary>The prefab its tile shows for <paramref name="look"/>, or null (rendered or an icon).</summary>
        public string PrefabOf(string look) => Look(look).Prefab is { Length: > 0 } p ? p : null;

        /// <summary>A key of what its thumbnail shows: rendered again when it changes.</summary>
        public string ThumbnailKey(string look) => look == null || look == DefaultLook.Id ? Id : Id + "@" + look;

        public bool Matches(string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            string hay = (Label + " " + Path + " " + Tags + " " + string.Join(" ", Looks.Select(l => l.Label))).ToLowerInvariant();
            return search.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).All(hay.Contains);
        }
    }

    /// <summary>
    /// Where and how an entry is placed (ARCHITECTURE §27.3): the point clicked or dropped in the Scene view (else where the view looks,
    /// as the menus always did), the surface's normal, a turn, the snap target found, and a seed.
    /// </summary>
    public sealed class PlaceContext
    {
        public Scene Scene;
        /// <summary>A point was clicked or dropped (else the entry goes where the Scene view looks).</summary>
        public bool Explicit;
        public Vector3 Point, Normal = Vector3.up;
        /// <summary>The turn round the vertical the ghost was given (degrees).</summary>
        public float Yaw;
        public PlaceSnap Snapped;
        public int Seed;

        public static PlaceContext FromView() => new PlaceContext { Scene = EditorSceneManager.GetActiveScene() };

        public static PlaceContext At(Vector3 point, Vector3 normal, float yaw = 0f) =>
            new PlaceContext { Scene = EditorSceneManager.GetActiveScene(), Explicit = true, Point = point, Normal = normal, Yaw = yaw };

        public LevelSettings Settings => LevelScenes.SettingsOf(Scene);

        public Transform Group(LevelGroup group)
        {
            var settings = Settings;
            if (settings == null) return null;
            // the land pieces' group is made with the level's first land piece (levels made before M21 have none)
            if (group == LevelGroup.Ground && settings.Group(group) == settings.transform)
            {
                var ground = new GameObject("Ground").transform;
                Undo.RegisterCreatedObjectUndo(ground.gameObject, "Ground group");
                Undo.SetTransformParent(ground, settings.transform, "Ground group");
                ground.localPosition = Vector3.zero;
                Undo.RecordObject(settings, "Ground group");
                settings.SetGroundGroup(ground);
                EditorUtility.SetDirty(settings);
            }
            return settings.Group(group);
        }

        /// <summary>The ground <paramref name="above"/> under the point (or under the Scene view's pivot): where pieces have always gone.</summary>
        public Vector3 Ground(float above = 0f)
        {
            if (Explicit) return Point + Vector3.up * above;
            var view = SceneView.lastActiveSceneView;
            var pivot = view != null ? view.pivot : Vector3.zero;
            // the level's ground (its terrain first): what was just placed there is not the ground
            float ground = LevelNature.GroundAt(Scene, new Vector2(pivot.x, pivot.z));
            return new Vector3(pivot.x, (float.IsNaN(ground) ? pivot.y : ground) + above, pivot.z);
        }

        /// <summary>The floor at the point (a deck, a walkway, the ground), or the floor the Scene view looks at: where the kit has always gone.</summary>
        public Vector3 Floor() => Explicit ? Point : LevelNature.ViewFloor(Scene);
    }

    /// <summary>What a placement snapped to: a deck edge or a piece's end (with the attach it makes), a wall's end or opening.</summary>
    public struct PlaceSnap
    {
        public PaletteSnap Kind;
        public Vector3 Point, Direction;
        public TimberAttach Attach;
        public Object Target;
        public string Label;
        public bool Found => Kind != PaletteSnap.None;
    }

    /// <summary>
    /// The level's catalogue (ARCHITECTURE §27.3, docs/LEVEL_AUTHORING.md §25): every <see cref="PaletteItemAttribute"/> method of the
    /// editor assembly, plus the props, the structure library, the presets and the burst presets found in their folders. One catalogue
    /// feeds the Level Palette and the generated GameObject/HotPatata Level menus (<see cref="PaletteMenuWriter"/>).
    /// </summary>
    public static class PaletteCatalog
    {
        /// <summary>The palette's tabs (and the menus' submenus), in order.</summary>
        public static readonly string[] Tabs = { "Ground", "Rock", "Nature", "Timber", "Masonry", "Structures", "Elements", "Course", "Kit", "Props", "Air" };
        public const string MenuRoot = "GameObject/HotPatata Level/";

        static List<PaletteEntry> code;

        /// <summary>The entries made from code (each has a menu item), in tab, section and source order.</summary>
        public static IReadOnlyList<PaletteEntry> CodeEntries => code ??= Collect();

        /// <summary>Every entry: code, then the assets found in their folders (props, library, presets, burst presets).</summary>
        public static List<PaletteEntry> All() => CodeEntries.Concat(PaletteAssets.Entries()).ToList();

        public static PaletteEntry Find(string id) => CodeEntries.FirstOrDefault(e => e.Id == id) ?? PaletteAssets.Entries().FirstOrDefault(e => e.Id == id);

        /// <summary>Forgets the scanned assets (a folder changed).</summary>
        public static void Refresh() => PaletteAssets.Forget();

        static List<PaletteEntry> Collect()
        {
            var methods = typeof(PaletteCatalog).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .ToList();
            var entries = new List<PaletteEntry>();
            foreach (var m in methods)
            {
                var a = m.GetCustomAttribute<PaletteItemAttribute>();
                if (a == null) continue;
                var slash = a.Section.IndexOf('/');
                var entry = new PaletteEntry
                {
                    Id = a.Id,
                    Tab = slash < 0 ? a.Section : a.Section.Substring(0, slash),
                    Section = slash < 0 ? "" : a.Section.Substring(slash + 1),
                    Label = a.Label,
                    Tooltip = a.Tooltip,
                    Tags = a.Tags,
                    Kind = a.Kind,
                    Snap = a.Snap,
                    Burst = a.Burst,
                    InMenu = true,
                    Order = a.Line,
                    Icon = a.Icon,
                    PieceType = a.Piece,
                    AlsoIn = a.Also,
                };
                entry.Looks.Add(new PaletteLook { Id = "default", Label = a.LookLabel, Tooltip = a.Tooltip, Prefab = a.Prefab, Create = Bind(m) });
                entries.Add(entry);
            }
            foreach (var m in methods)
            {
                var a = m.GetCustomAttribute<PaletteLookAttribute>();
                if (a == null) continue;
                var entry = entries.FirstOrDefault(e => e.Id == a.Entry) ?? throw new InvalidOperationException($"palette look {a.Id} of an unknown entry {a.Entry}");
                entry.Looks.Add(new PaletteLook { Id = a.Id, Label = a.Label, Tooltip = a.Tooltip, Prefab = a.Prefab, Create = Bind(m) });
            }
            foreach (var e in entries)
            {
                var defaultLook = e.Looks[0];
                var others = e.Looks.Skip(1).OrderBy(l => l.Label).ToList();
                e.Looks.Clear();
                e.Looks.Add(defaultLook);
                e.Looks.AddRange(others);
            }
            var dupes = entries.GroupBy(e => e.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0) throw new InvalidOperationException("palette entries share an id: " + string.Join(", ", dupes));
            return entries.OrderBy(e => TabIndex(e.Tab)).ThenBy(e => e.Order).ToList();
        }

        static Func<PlaceContext, GameObject> Bind(MethodInfo m) => c => (GameObject)m.Invoke(null, new object[] { c });

        public static int TabIndex(string tab)
        {
            int i = Array.IndexOf(Tabs, tab);
            return i < 0 ? Tabs.Length : i;
        }

        /// <summary>The sections of a tab, in the order their first entry comes.</summary>
        public static IEnumerable<string> Sections(IEnumerable<PaletteEntry> entries, string tab) =>
            entries.Where(e => e.Tab == tab).Select(e => e.Section).Distinct();

        // ------------------------------------------------------------------ placing

        /// <summary>Recent entries kept (the palette's Recent tab).</summary>
        public const int RecentCount = 12;
        const string RecentKey = "HotPatata.Palette.Recent", FavouritesKey = "HotPatata.Palette.Favourites";

        /// <summary>
        /// Places <paramref name="entry"/> (its <paramref name="look"/>) at <paramref name="context"/>: made, turned, snapped, remembered in
        /// Recent and selected. Returns what was placed.
        /// </summary>
        public static GameObject Place(PaletteEntry entry, string look, PlaceContext context)
        {
            if (entry == null) return null;
            var made = entry.Look(look).Create(context);
            if (made == null) return null;
            if (context.Explicit && Mathf.Abs(context.Yaw) > 0.01f && !context.Snapped.Found)
            {
                Undo.RecordObject(made.transform, "Turn " + entry.Label);
                made.transform.rotation = Quaternion.Euler(0f, context.Yaw, 0f) * made.transform.rotation;
                if (made.TryGetComponent<LevelPiece>(out var turned)) LevelPieceBuilder.Rebuild(turned);
            }
            if (entry.Preset != null && made.TryGetComponent<LevelPiece>(out var piece))
            {
                entry.Preset.ApplyTo(piece);
                LevelPieceBuilder.Rebuild(piece);
            }
            Remember(entry.Id);
            Selection.activeGameObject = made;
            return made;
        }

        /// <summary>Places entry <paramref name="id"/> where the Scene view looks (the menus).</summary>
        public static GameObject PlaceFromMenu(string id, string look = null) => Place(Find(id), look, PlaceContext.FromView());

        public static List<string> Recent() => Load(RecentKey);

        public static List<string> Favourites() => Load(FavouritesKey);

        public static bool IsFavourite(string id) => Favourites().Contains(id);

        public static void SetFavourite(string id, bool on)
        {
            var list = Favourites();
            list.Remove(id);
            if (on) list.Add(id);
            Save(FavouritesKey, list);
        }

        public static void Remember(string id)
        {
            var list = Recent();
            list.Remove(id);
            list.Insert(0, id);
            if (list.Count > RecentCount) list.RemoveRange(RecentCount, list.Count - RecentCount);
            Save(RecentKey, list);
        }

        static List<string> Load(string key) =>
            EditorPrefs.GetString(key, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        static void Save(string key, List<string> ids) => EditorPrefs.SetString(key, string.Join("\n", ids));

        // ------------------------------------------------------------------ shared by the entries

        /// <summary>A new, empty object in the level's <paramref name="group"/>, at <paramref name="at"/>, registered for undo and selected.</summary>
        public static GameObject NewObject(PlaceContext c, string name, Vector3 at, LevelGroup group)
        {
            var parent = c.Group(group);
            var go = new GameObject(ObjectNames.GetUniqueName(Siblings(parent), name));
            Undo.RegisterCreatedObjectUndo(go, "Add " + name);
            if (parent != null) Undo.SetTransformParent(go.transform, parent, "Add " + name);
            go.transform.position = at;
            Selection.activeGameObject = go;
            return go;
        }

        /// <summary>A new piece of <paramref name="type"/> at <paramref name="at"/>, in its level group, built.</summary>
        public static LevelPiece NewPiece(PlaceContext c, Type type, string name, Vector3 at, LevelGroup group = LevelGroup.Timber)
        {
            var go = NewObject(c, name, at, group);
            var piece = (LevelPiece)Undo.AddComponent(go, type);
            piece.EnsureId();
            LevelPieceBuilder.Rebuild(piece);
            return piece;
        }

        static string[] Siblings(Transform parent) =>
            parent == null ? Array.Empty<string>() : parent.Cast<Transform>().Select(t => t.name).ToArray();

        /// <summary>A prefab instance in the level's <paramref name="group"/>, standing on the ground at the context (Props, the library).</summary>
        public static GameObject NewPrefab(PlaceContext c, string path, LevelGroup group, bool alignToNormal = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[Palette] {path} is missing (build its library first).");
                return null;
            }
            var parent = c.Group(group);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = ObjectNames.GetUniqueName(Siblings(parent).Where(n => n != go.name).ToArray(), prefab.name);
            Undo.RegisterCreatedObjectUndo(go, "Add " + prefab.name);
            go.transform.position = c.Floor();
            if (alignToNormal) go.transform.rotation = Quaternion.FromToRotation(Vector3.up, c.Normal);
            return go;
        }

        public static string FileName(string path) => System.IO.Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// The entries made from assets (ARCHITECTURE §27.3): every prop of the prop library by folder (burst), the props sets, the structure
    /// library by folder, the timber presets, the scanned rocks (Rock tab, burst) and the burst presets that replaced the Props Brush.
    /// </summary>
    public static class PaletteAssets
    {
        static List<PaletteEntry> cached;

        /// <summary>The burst presets (the old Props Brush's brushes): library props and their size range.</summary>
        public static readonly (string id, string label, string[] models, Vector2 scale)[] BurstPresets =
        {
            ("burst.pebbles", "Pebbles", new[] { "stone_01", "rock_09", "rock_07" }, new Vector2(0.8f, 2.2f)),
            ("burst.twigs", "Twigs", new[] { "dry_branches_medium_01" }, new Vector2(0.35f, 0.7f)),
            ("burst.roots", "Roots", new[] { "single_root", "root_cluster_02" }, new Vector2(0.4f, 0.9f)),
            ("burst.moss", "Moss", new[] { "moss_01" }, new Vector2(3f, 7f)),
            ("burst.ferns", "Ferns", new[] { "fern_02" }, new Vector2(0.4f, 0.8f)),
        };

        /// <summary>The scanned rocks offered in the Rock tab.</summary>
        static readonly string[] Scans =
        {
            "boulder_01", "rock_07", "rock_09", "rock_face_01", "rock_face_02", "rock_moss_set_01", "rock_moss_set_02", "namaqualand_boulder_02",
            "namaqualand_boulder_04", "stone_01", "namaqualand_cliff_01", "namaqualand_cliff_02", "coastal_cliff_04", "mountainside",
        };

        public static void Forget() => cached = null;

        public static List<PaletteEntry> Entries() => cached ??= Scan();

        static List<PaletteEntry> Scan()
        {
            var list = new List<PaletteEntry>();
            int order = 0;
            PaletteEntry Prefab(string id, string tab, string section, string label, string path, PaletteKind kind, LevelGroup group, bool burst, string tags)
            {
                var e = new PaletteEntry
                {
                    Id = id, Tab = tab, Section = section, Label = label, Tooltip = path, Tags = tags, Kind = kind, Burst = burst,
                    Snap = PaletteSnap.Ground | PaletteSnap.Floor | (burst ? PaletteSnap.Normal : 0), Order = order++,
                    BurstPrefabs = burst ? new[] { path } : Array.Empty<string>(),
                    BurstScale = new Vector2(0.85f, 1.2f),
                };
                e.Looks.Add(new PaletteLook { Id = "default", Label = label, Prefab = path, Create = c => PaletteCatalog.NewPrefab(c, path, group) });
                return e;
            }

            // the burst presets first (the Props tab opens on them)
            foreach (var (id, label, models, scale) in BurstPresets)
            {
                var paths = models.Select(PropLibraryBuilder.PrefabPath).Where(p => p != null).ToArray();
                if (paths.Length == 0) continue;
                var e = new PaletteEntry
                {
                    Id = id, Tab = "Props", Section = "Burst presets", Label = label, Tooltip = string.Join(", ", models), Tags = "burst brush scatter " + label,
                    Kind = PaletteKind.Prop, Burst = true, Snap = PaletteSnap.Ground | PaletteSnap.Floor | PaletteSnap.Normal, Order = order++,
                    BurstPrefabs = paths, BurstScale = scale,
                };
                e.Looks.Add(new PaletteLook { Id = "default", Label = label, Prefab = paths[0], Create = c => PaletteCatalog.NewPrefab(c, paths[0], LevelGroup.Props, true) });
                list.Add(e);
            }

            // the scanned rocks (Rock tab)
            foreach (var model in Scans)
            {
                var path = PropLibraryBuilder.PrefabPath(model);
                if (path == null || !File.Exists(path)) continue;
                list.Add(Prefab("scan." + model, "Rock", "Scans", Nice(model), path, PaletteKind.Prop, LevelGroup.Props, true, "rock scan boulder cliff"));
            }

            // the prop library by folder, and its sets
            if (Directory.Exists(PropLibraryBuilder.Folder))
                foreach (var dir in Directory.GetDirectories(PropLibraryBuilder.Folder).OrderBy(d => d))
                {
                    string folder = System.IO.Path.GetFileName(dir);
                    bool sets = folder == "Sets";
                    foreach (var file in Directory.GetFiles(dir, "*.prefab").OrderBy(f => f))
                    {
                        string path = file.Replace('\\', '/');
                        string name = PaletteCatalog.FileName(path);
                        list.Add(Prefab("prop." + name, "Props", sets ? "Sets" : folder, Nice(name), path, PaletteKind.Prop, LevelGroup.Props, !sets, "prop " + folder));
                    }
                }

            // the structure library by folder
            if (Directory.Exists(StructureLibrary.Folder))
                foreach (var file in Directory.GetFiles(StructureLibrary.Folder, "*.prefab", SearchOption.AllDirectories).OrderBy(f => f))
                {
                    string path = file.Replace('\\', '/');
                    string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));
                    string name = PaletteCatalog.FileName(path);
                    list.Add(Prefab("library." + folder + "." + name, "Structures", "Library: " + folder, Nice(name), path, PaletteKind.Structure, LevelGroup.Timber, false,
                                    "library structure frozen " + folder));
                }

            // the timber presets, in their piece's tab
            foreach (var guid in AssetDatabase.FindAssets("t:TimberPreset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<TimberPreset>(path);
                if (preset == null || preset.PieceType == null) continue;
                var type = preset.PieceType;
                var host = PaletteCatalog.CodeEntries.FirstOrDefault(c => c.PieceType == type);
                var e = new PaletteEntry
                {
                    Id = "preset." + guid, Tab = host?.Tab ?? "Timber", Section = "Presets", Label = preset.name, Tooltip = path, Tags = "preset " + type.Name,
                    Kind = PaletteKind.Preset, Snap = host?.Snap ?? PaletteSnap.Ground, Order = order++, Preset = preset, PieceType = type,
                };
                e.Looks.Add(new PaletteLook
                {
                    Id = "default", Label = preset.name,
                    Create = host != null ? host.DefaultLook.Create : c => PaletteCatalog.NewPiece(c, type, preset.name, c.Ground()).gameObject,
                });
                list.Add(e);
            }
            return list;
        }

        static string Nice(string model) => ObjectNames.NicifyVariableName(model.Replace('_', ' '));

        sealed class Watch : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                bool Touches(string p) => p.StartsWith(PropLibraryBuilder.Folder) || p.StartsWith(StructureLibrary.Folder) || p.EndsWith(".asset");
                if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(Touches)) Forget();
            }
        }
    }
}
