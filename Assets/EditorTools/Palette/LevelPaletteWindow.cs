using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata.Editor
{
    /// <summary>
    /// HotPatata/Level/Palette (ARCHITECTURE §27.3, docs/LEVEL_AUTHORING.md §25): every placeable thing of a level in one dockable window.
    /// Tabs by kind, collapsible sections, tiles with pictures (or rows), a search across every tab, Favourites and Recent. A click arms a
    /// tile (then a click in the Scene view places it, <see cref="PalettePlacer"/>), a double click places it where the view looks, a drag
    /// drops it in the Scene view. The strip at the bottom holds the armed entry's look chips, its preset, the burst settings and snapping.
    /// </summary>
    public sealed class LevelPaletteWindow : EditorWindow
    {
        const string Dir = "Assets/EditorTools/Palette/";
        const string FavouritesTab = "Favourites", RecentTab = "Recent";
        const string TabKey = "HotPatata.Palette.Tab", ListKey = "HotPatata.Palette.List";

        static readonly Dictionary<string, string> chosenLook = new Dictionary<string, string>();

        string tab;
        string search = "";
        bool list;
        VisualElement tabsBar;
        ScrollView grid;
        VisualElement strip, looks, burst;
        DropdownField preset;
        Label armedLabel;
        readonly HashSet<string> folded = new HashSet<string>();
        bool waitingForPreviews;

        [MenuItem("HotPatata/Level/Palette", priority = 2)]
        public static void Open()
        {
            var w = GetWindow<LevelPaletteWindow>(false, "Level Palette");
            w.minSize = new Vector2(300, 320);
        }

        void OnEnable()
        {
            tab = EditorPrefs.GetString(TabKey, "Timber");
            list = EditorPrefs.GetBool(ListKey, false);
            PalettePlacer.Changed += RefreshStrip;
        }

        void OnDisable() => PalettePlacer.Changed -= RefreshStrip;

        void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Dir + "LevelPalette.uxml");
            tree.CloneTree(rootVisualElement);
            var root = rootVisualElement;
            tabsBar = root.Q("tabs");
            grid = root.Q<ScrollView>("grid");
            strip = root.Q("strip");
            looks = root.Q("looks");
            burst = root.Q("burst");
            preset = root.Q<DropdownField>("preset");
            armedLabel = root.Q<Label>("armed-label");

            var searchField = root.Q<ToolbarSearchField>("search");
            searchField.RegisterValueChangedCallback(e =>
            {
                search = e.newValue ?? "";
                RefreshGrid();
            });
            var listToggle = root.Q<ToolbarToggle>("list-view");
            listToggle.SetValueWithoutNotify(list);
            listToggle.RegisterValueChangedCallback(e =>
            {
                list = e.newValue;
                EditorPrefs.SetBool(ListKey, list);
                RefreshGrid();
            });
            var menu = root.Q<ToolbarMenu>("window-menu");
            menu.menu.AppendAction("Refresh thumbnails", _ =>
            {
                PaletteThumbnails.RefreshAll();
                RefreshGrid();
            });
            menu.menu.AppendAction("Render missing thumbnails", _ => RenderMissing());
            menu.menu.AppendAction("Rescan props, library and presets", _ =>
            {
                PaletteCatalog.Refresh();
                RefreshAll();
            });
            menu.menu.AppendAction("Write menus", _ => PaletteMenuWriter.Write());
            root.Q<Button>("disarm").clicked += PalettePlacer.Disarm;

            BindBurst(root);
            BindSnaps(root);
            preset.RegisterValueChangedCallback(OnPreset);
            RefreshAll();
            EditorApplication.delayCall += RenderMissing;
        }

        void RefreshAll()
        {
            RefreshTabs();
            RefreshGrid();
            RefreshStrip();
        }

        // ------------------------------------------------------------------ tabs

        void RefreshTabs()
        {
            tabsBar.Clear();
            var entries = PaletteCatalog.All();
            foreach (var name in new[] { FavouritesTab, RecentTab }.Concat(PaletteCatalog.Tabs.Where(t => entries.Any(e => e.Tab == t || e.AlsoIn == t))))
            {
                var button = new Button(() =>
                {
                    tab = name;
                    EditorPrefs.SetString(TabKey, tab);
                    RefreshTabs();
                    RefreshGrid();
                }) { text = name == FavouritesTab ? "★" : name, tooltip = name };
                button.AddToClassList("pal-tab");
                if (name == tab) button.AddToClassList("pal-tab--on");
                tabsBar.Add(button);
            }
        }

        // ------------------------------------------------------------------ the grid

        void RefreshGrid()
        {
            if (grid == null) return;
            grid.Clear();
            var all = PaletteCatalog.All();
            IEnumerable<(string title, List<PaletteEntry> items)> groups;
            if (!string.IsNullOrWhiteSpace(search))
                groups = PaletteCatalog.Tabs.Select(t => (t, all.Where(e => e.Tab == t && e.Matches(search)).ToList())).Where(g => g.Item2.Count > 0);
            else if (tab == FavouritesTab)
                groups = new[] { ("Favourites", PaletteCatalog.Favourites().Select(id => all.FirstOrDefault(e => e.Id == id)).Where(e => e != null).ToList()) };
            else if (tab == RecentTab)
                groups = new[] { ("Recent", PaletteCatalog.Recent().Select(id => all.FirstOrDefault(e => e.Id == id)).Where(e => e != null).ToList()) };
            else
            {
                var inTab = all.Where(e => e.Tab == tab || e.AlsoIn == tab).ToList();
                groups = inTab.Select(e => e.Tab == tab ? e.Section : e.Tab).Distinct()
                              .Select(s => (s, inTab.Where(e => (e.Tab == tab ? e.Section : e.Tab) == s).ToList()));
            }
            int shown = 0;
            foreach (var (title, items) in groups)
            {
                if (items.Count == 0) continue;
                string key = tab + "/" + title;
                var fold = new Foldout { text = $"{(string.IsNullOrEmpty(title) ? tab : title)}  ({items.Count})", value = !folded.Contains(key) };
                fold.RegisterValueChangedCallback(e =>
                {
                    if (e.target != fold) return;
                    if (e.newValue) folded.Remove(key);
                    else folded.Add(key);
                });
                var box = new VisualElement();
                box.AddToClassList("pal-section-items");
                if (list) box.AddToClassList("pal-section-items--list");
                foreach (var entry in items) box.Add(Tile(entry));
                fold.Add(box);
                grid.Add(fold);
                shown += items.Count;
            }
            if (shown == 0)
            {
                var empty = new Label(tab == FavouritesTab ? "No favourites yet: click a tile's star." : tab == RecentTab ? "Nothing placed yet." : "Nothing matches.");
                empty.AddToClassList("pal-empty");
                grid.Add(empty);
            }
            WaitForPreviews();
        }

        VisualElement Tile(PaletteEntry entry)
        {
            var tile = new VisualElement { tooltip = Tooltip(entry) };
            tile.AddToClassList(list ? "pal-row-item" : "pal-tile");
            if (PalettePlacer.Armed == entry) tile.AddToClassList("pal-tile--armed");
            var image = new Image { scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList("pal-tile-image");
            image.image = PaletteThumbnails.Get(entry, LookOf(entry)) ?? PaletteThumbnails.Placeholder(entry);
            image.userData = entry;
            tile.Add(image);
            var label = new Label(entry.Label);
            label.AddToClassList("pal-tile-label");
            tile.Add(label);
            if (entry.Looks.Count > 1)
            {
                var badge = new Label($"{entry.Looks.Count} looks");
                badge.AddToClassList("pal-looks-badge");
                tile.Add(badge);
            }
            var star = new Button { text = "★", tooltip = "Favourite" };
            star.AddToClassList("pal-star");
            if (PaletteCatalog.IsFavourite(entry.Id)) star.AddToClassList("pal-star--on");
            star.clicked += () =>
            {
                PaletteCatalog.SetFavourite(entry.Id, !PaletteCatalog.IsFavourite(entry.Id));
                RefreshGrid();
            };
            tile.Add(star);

            // a press on this tile: a tool runs on its release here, a drag starts once the mouse has moved off it a little
            Vector2? pressedAt = null;
            tile.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button != 0) return;
                pressedAt = e.mousePosition;
                if (entry.Kind == PaletteKind.Tool) return;
                if (e.clickCount == 2)
                {
                    PalettePlacer.Disarm();
                    PaletteCatalog.Place(entry, LookOf(entry), PlaceContext.FromView());
                    e.StopPropagation();
                    return;
                }
                if (PalettePlacer.Armed == entry) PalettePlacer.Disarm();
                else PalettePlacer.Arm(entry, LookOf(entry));
                foreach (var other in grid.Query(className: "pal-tile--armed").ToList()) other.RemoveFromClassList("pal-tile--armed");
                if (PalettePlacer.Armed == entry) tile.AddToClassList("pal-tile--armed");
            });
            tile.RegisterCallback<MouseUpEvent>(e =>
            {
                bool full = pressedAt.HasValue && e.button == 0;
                pressedAt = null;
                if (full && entry.Kind == PaletteKind.Tool) entry.Look(null).Create(PlaceContext.FromView());
            });
            tile.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (!pressedAt.HasValue || entry.Kind == PaletteKind.Tool) return;
                pressedAt = null;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(PalettePlacer.DragKey, entry.Id + "|" + LookOf(entry));
                DragAndDrop.objectReferences = new Object[0];
                DragAndDrop.StartDrag(entry.Label);
            });
            tile.AddManipulator(new ContextualMenuManipulator(e =>
            {
                e.menu.AppendAction("Place where the view looks", _ => PaletteCatalog.Place(entry, LookOf(entry), PlaceContext.FromView()),
                                    entry.Kind == PaletteKind.Tool ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                e.menu.AppendAction(PaletteCatalog.IsFavourite(entry.Id) ? "Remove from favourites" : "Add to favourites", _ =>
                {
                    PaletteCatalog.SetFavourite(entry.Id, !PaletteCatalog.IsFavourite(entry.Id));
                    RefreshGrid();
                });
                var prefab = entry.PrefabOf(LookOf(entry));
                if (prefab != null) e.menu.AppendAction("Show the prefab", _ => EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(prefab)));
            }));
            return tile;
        }

        static string Tooltip(PaletteEntry e)
        {
            string looksText = e.Looks.Count > 1 ? "\nLooks: " + string.Join(", ", e.Looks.Select(l => l.Label)) : "";
            string burstText = e.Burst ? "\nBurst: each click scatters copies." : "";
            return $"{e.Label} ({e.Path}){(string.IsNullOrEmpty(e.Tooltip) ? "" : "\n" + e.Tooltip)}{looksText}{burstText}";
        }

        static string LookOf(PaletteEntry e) => chosenLook.TryGetValue(e.Id, out var look) ? look : e.DefaultLook.Id;

        /// <summary>Asset previews come a few frames later: redraw the pictures until they are all there.</summary>
        void WaitForPreviews()
        {
            if (waitingForPreviews) return;
            waitingForPreviews = true;
            rootVisualElement.schedule.Execute(() =>
            {
                foreach (var image in grid.Query<Image>(className: "pal-tile-image").ToList())
                    if (image.userData is PaletteEntry entry)
                    {
                        var tex = PaletteThumbnails.Get(entry, LookOf(entry));
                        if (tex != null && image.image != tex) image.image = tex;
                    }
            }).Every(250).Until(() =>
            {
                bool done = !PaletteThumbnails.Loading;
                if (done) waitingForPreviews = false;
                return done;
            });
        }

        void RenderMissing()
        {
            if (this == null) return;
            var missing = PaletteThumbnails.Missing(PaletteCatalog.All());
            if (missing.Count == 0) return;
            if (PaletteThumbnails.Render(missing) > 0) RefreshGrid();
        }

        // ------------------------------------------------------------------ the strip

        void RefreshStrip()
        {
            if (strip == null) return;
            var armed = PalettePlacer.Armed;
            armedLabel.text = armed == null
                ? "Click a tile to arm it, double-click to place it where the view looks, or drag it into the Scene view."
                : $"Armed: {armed.Label}" + (armed.Burst ? " (burst)" : "") + " — click in the Scene view to place it.";
            rootVisualElement.Q<Button>("disarm").style.display = armed != null ? DisplayStyle.Flex : DisplayStyle.None;
            looks.Clear();
            if (armed != null && armed.Looks.Count > 1)
                foreach (var look in armed.Looks)
                {
                    var chip = new Button(() =>
                    {
                        chosenLook[armed.Id] = look.Id;
                        PalettePlacer.Arm(armed, look.Id);
                        RefreshGrid();
                    }) { text = look.Label, tooltip = look.Tooltip };
                    chip.AddToClassList("pal-chip");
                    if (LookOf(armed) == look.Id) chip.AddToClassList("pal-chip--on");
                    looks.Add(chip);
                }
            looks.style.display = armed != null && armed.Looks.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;

            var presets = armed != null && armed.PieceType != null
                ? PaletteCatalog.All().Where(e => e.Kind == PaletteKind.Preset && e.PieceType == armed.PieceType).ToList()
                : new List<PaletteEntry>();
            preset.style.display = presets.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (presets.Count > 0)
            {
                preset.choices = new List<string> { "(none)" }.Concat(presets.Select(p => p.Label)).ToList();
                preset.SetValueWithoutNotify(armed.Kind == PaletteKind.Preset ? armed.Label : "(none)");
            }
            burst.style.display = armed != null && armed.Burst ? DisplayStyle.Flex : DisplayStyle.None;
            if (armed != null && armed.Burst && armed.BurstScale != Vector2.one) rootVisualElement.Q<MinMaxSlider>("burst-scale").SetValueWithoutNotify(armed.BurstScale);
        }

        void OnPreset(ChangeEvent<string> e)
        {
            var armed = PalettePlacer.Armed;
            if (armed == null || e.newValue == e.previousValue) return;
            var all = PaletteCatalog.All();
            var target = e.newValue == "(none)"
                ? all.FirstOrDefault(x => x.InMenu && x.PieceType == armed.PieceType)
                : all.FirstOrDefault(x => x.Kind == PaletteKind.Preset && x.PieceType == armed.PieceType && x.Label == e.newValue);
            if (target != null) PalettePlacer.Arm(target, null);
        }

        void BindBurst(VisualElement root)
        {
            var b = PalettePlacer.Burst;
            var count = root.Q<SliderInt>("burst-count");
            count.SetValueWithoutNotify(b.Count);
            count.RegisterValueChangedCallback(e => b.Count = e.newValue);
            var radius = root.Q<Slider>("burst-radius");
            radius.SetValueWithoutNotify(b.Radius);
            radius.RegisterValueChangedCallback(e => b.Radius = e.newValue);
            var scale = root.Q<MinMaxSlider>("burst-scale");
            scale.SetValueWithoutNotify(b.Scale);
            scale.RegisterValueChangedCallback(e => b.Scale = e.newValue);
            var yaw = root.Q<Toggle>("burst-yaw");
            yaw.SetValueWithoutNotify(b.RandomYaw);
            yaw.RegisterValueChangedCallback(e => b.RandomYaw = e.newValue);
            var normal = root.Q<Toggle>("burst-normal");
            normal.SetValueWithoutNotify(b.AlignToNormal);
            normal.RegisterValueChangedCallback(e => b.AlignToNormal = e.newValue);
        }

        static void BindSnaps(VisualElement root)
        {
            void Bind(string name, PaletteSnap kind)
            {
                var toggle = root.Q<Toggle>(name);
                toggle.SetValueWithoutNotify((PalettePlacer.Disabled & kind) == 0);
                toggle.RegisterValueChangedCallback(e => PalettePlacer.Disabled = e.newValue ? PalettePlacer.Disabled & ~kind : PalettePlacer.Disabled | kind);
            }
            Bind("snap-ground", PaletteSnap.Ground | PaletteSnap.Floor);
            Bind("snap-deck", PaletteSnap.DeckEdge);
            Bind("snap-wall", PaletteSnap.WallEnd);
            Bind("snap-opening", PaletteSnap.WallOpening);
        }
    }
}
