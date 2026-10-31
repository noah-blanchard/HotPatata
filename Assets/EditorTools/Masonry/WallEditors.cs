using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The wall's inspector and handles (docs/LEVEL_AUTHORING.md §30): its dots, its openings (a dot on the line to slide each one, its
    /// passage and its kit), what a player does at it (mantle, climb, or a lock) and the kit's state.
    /// </summary>
    [CustomEditor(typeof(WallShape)), CanEditMultipleObjects]
    public sealed class WallShapeEditor : LevelPieceEditor
    {
        WallShape Wall => (WallShape)target;

        protected override string Help =>
            "A wall: its dots are its line at its foot (click a dot to move it, Ctrl+click to delete it, a + to add one). It collides as boxes along " +
            "its line up to its top, cut round its openings. An opening's passage places its kit in the level's Kit group (a body screen, a curtain or a " +
            "door), sized to it; a rebuild moves and resizes that kit and keeps its wiring. Drag an opening's dot along the line to move it.";

        protected override void DrawTools()
        {
            var wall = Wall;
            PencilButton(wall.Points, false);
            if (GUILayout.Button(new GUIContent("Freeze per segment", $"A mesh per straight run instead of an element per stone (a wall of more than {StoneFreeze.StoneFreezeLimit} stones freezes only so). One way (Ctrl+Z only).")))
            {
                TimberFreezer.Freeze(wall, true);
                GUIUtility.ExitGUI();
            }
            float locked = MasonryKit.LockedHeight(wall);
            EditorGUILayout.HelpBox($"{wall.Family} wall. Locked height {locked:F2} m (its lowest top): {MasonryKit.Reads(locked)}.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Add opening", GUILayout.Width(90));
                foreach (OpeningPassage passage in System.Enum.GetValues(typeof(OpeningPassage)))
                    if (GUILayout.Button(passage.ToString())) AddOpening(wall, passage);
            }
            for (int i = 0; i < wall.Openings.Count; i++)
            {
                var o = wall.Openings[i];
                string note = o.passage switch
                {
                    OpeningPassage.Free => o.PlayerFits ? "free: both pass (a player fits through it)" : "free: the bomb only by size (a player does not fit)",
                    OpeningPassage.BombOnly => o.kit != null ? "bomb only: " + o.kit.name : "bomb only: its screen is placed on the next rebuild",
                    OpeningPassage.RunnersOnly => o.kit != null ? "runners only: " + o.kit.name : "runners only: its curtain is placed on the next rebuild",
                    _ => o.kit == null ? "kit door: placed on the next rebuild"
                        : o.kit.GetComponent<SignalActuator>()?.Source == null ? "kit door: " + o.kit.name + " (unwired: link it to a source)" : "kit door: " + o.kit.name,
                };
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{i + 1}. {o.width:F1} × {o.height:F1} m at {o.along:F1} m, {note}", EditorStyles.wordWrappedMiniLabel);
                    if (o.kit != null && GUILayout.Button("Select", GUILayout.Width(52))) Selection.activeGameObject = o.kit;
                    if (o.kit != null && Mismatch(o) && GUILayout.Button("Replace kit", GUILayout.Width(80)) &&
                        EditorUtility.DisplayDialog("Replace kit", $"Delete {o.kit.name} (and its wiring) and place the opening's look instead?", "Replace", "Keep"))
                    {
                        Undo.RecordObject(wall, "Replace kit");
                        Undo.DestroyObjectImmediate(o.kit);
                        o.kit = null;
                        wall.Openings[i] = o;
                        EditorUtility.SetDirty(wall);
                        LevelPieceBuilder.Rebuild(wall);
                        GUIUtility.ExitGUI();
                    }
                    if (GUILayout.Button("×", GUILayout.Width(22)))
                    {
                        bool dropKit = o.kit != null && EditorUtility.DisplayDialog("Remove opening", $"Delete its kit {o.kit.name} too?", "Delete it", "Keep it");
                        Undo.RecordObject(wall, "Remove opening");
                        if (dropKit) Undo.DestroyObjectImmediate(o.kit);
                        wall.Openings.RemoveAt(i);
                        EditorUtility.SetDirty(wall);
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        static bool Mismatch(WallOpening o)
        {
            if (o.passage == OpeningPassage.Free) return true;
            var source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(o.kit);
            return !string.IsNullOrEmpty(source) && source != MasonryKit.LookOf(o) + ".prefab";
        }

        static void AddOpening(WallShape wall, OpeningPassage passage)
        {
            Undo.RecordObject(wall, "Add opening");
            float along = wall.Length * 0.5f;
            var o = passage switch
            {
                OpeningPassage.KitDoor => new WallOpening(along, 3f, Mathf.Min(3f, wall.Height - 0.4f), passage),
                OpeningPassage.Free => new WallOpening(along, 1.4f, Mathf.Min(2.4f, wall.Height - 0.4f), passage, OpeningShape.RoundArch),
                _ => new WallOpening(along, 2.4f, Mathf.Min(2.6f, wall.Height - 0.4f), passage),
            };
            o.frame = wall.Family == WallFamily.Stone ? OpeningFrame.Stone : OpeningFrame.Timber;
            wall.Openings.Add(o);
            EditorUtility.SetDirty(wall);
        }

        protected override void DrawScene()
        {
            var wall = Wall;
            PieceHandles.EditPoints(wall, wall.Points, wall.Closed, false, 2);
            var runs = MasonryKit.Runs(wall);
            if (runs.Count == 0) return;
            for (int i = 0; i < wall.Openings.Count; i++)
            {
                var o = wall.Openings[i];
                var run = runs.FirstOrDefault(r => o.along >= r.start && o.along <= r.start + r.length + 1e-3f);
                if (run.length <= 0f) continue;
                var at = run.At(o.along, o.sill + o.height + 0.3f);
                Handles.color = PieceHandles.OpeningColor;
                Handles.DrawWireCube(run.At(o.along, o.sill + o.height * 0.5f), new Vector3(0.1f, o.height, 0.1f));
                Handles.Label(at + Vector3.up * 0.4f, o.passage.ToString());
                EditorGUI.BeginChangeCheck();
                var to = Handles.Slider(at, run.along, HandleUtility.GetHandleSize(at) * 0.08f, Handles.DotHandleCap, 0.1f);
                if (!EditorGUI.EndChangeCheck()) continue;
                Undo.RecordObject(wall, "Move opening");
                o.along = Mathf.Clamp(o.along + Vector3.Dot(to - at, run.along), o.width * 0.5f, wall.Length - o.width * 0.5f);
                wall.Openings[i] = o;
                EditorUtility.SetDirty(wall);
            }
        }
    }

    /// <summary>
    /// The walls in the palette (ARCHITECTURE §27.3 and §27.7): a stone wall, a timber wall, a fence and a mixed wall, each style a look;
    /// a ruin fragment; a palisade gate-wall (a kit door in its opening); and the stone set of door looks on the kit's doors.
    /// </summary>
    public static class WallEntries
    {
        static GameObject Wall(PlaceContext c, string name, WallStyle style, System.Action<WallShape> setup = null)
        {
            var wall = (WallShape)PaletteCatalog.NewPiece(c, typeof(WallShape), name, c.Ground(0f), LevelGroup.Timber);
            Undo.RecordObject(wall, name);
            Dress(wall, style);
            setup?.Invoke(wall);
            LevelPieceBuilder.Rebuild(wall);
            return wall.gameObject;
        }

        /// <summary>A style's own sizes: a stone wall 2 m high, a palisade 3.6 m (a lock), a fence 1.1 m, a mixed wall 2.6 m.</summary>
        public static void Dress(WallShape wall, WallStyle style)
        {
            wall.Style = style;
            wall.Points.Clear();
            wall.Points.AddRange(new[] { new Vector3(-6f, 0f, 0f), new Vector3(0f, 0f, 1.5f), new Vector3(6f, 0f, 0f) });
            switch (WallShape.FamilyOf(style))
            {
                case WallFamily.Stone:
                    wall.Height = style == WallStyle.Retaining ? 2.4f : 2f;
                    wall.Thickness = style == WallStyle.Mortared ? 0.6f : 0.75f;
                    wall.Cap = style == WallStyle.Mortared ? WallCap.Coping : WallCap.None;
                    if (style == WallStyle.Ruined)
                    {
                        wall.Height = 3.2f;
                        wall.Crest = 1.6f;
                        wall.Breaches.Clear();
                        wall.Breaches.Add(new WallBreach { along = 4f, width = 2.5f, depth = 2.2f });
                    }
                    break;
                case WallFamily.Timber:
                    wall.Height = style == WallStyle.Palisade ? 3.6f : style == WallStyle.Wattle ? 1.5f : 2.4f;
                    wall.Thickness = style == WallStyle.Wattle ? 0.15f : 0.3f;
                    break;
                case WallFamily.Fence:
                    wall.Height = style == WallStyle.Hedge ? 1.6f : WallShape.FenceHeight;
                    wall.Thickness = style == WallStyle.Hedge ? 0.9f : 0.12f;
                    break;
                default:
                    wall.Height = style == WallStyle.HalfTimbered ? 3f : 2.6f;
                    wall.Thickness = 0.55f;
                    break;
            }
        }

        [PaletteItem("masonry.stone-wall", "Masonry/Walls", "Stone Wall", Piece = typeof(WallShape), LookLabel = "Dry Stone", Snap = PaletteSnap.Ground | PaletteSnap.WallEnd,
                     Tags = "wall stone dry mortared ruin retaining terrace", Tooltip = "Stones laid in courses along a line; its openings place the kit.")]
        static GameObject StoneWall(PlaceContext c) => Wall(c, "Stone wall", WallStyle.DryStone);

        [PaletteLook("masonry.stone-wall", "mortared", "Mortared", Tooltip = "Dressed stones in mortar, a coping on top")]
        static GameObject Mortared(PlaceContext c) => Wall(c, "Stone wall", WallStyle.Mortared);

        [PaletteLook("masonry.stone-wall", "ruined", "Ruined", Tooltip = "A dry-stone wall broken down: a ragged top and a breach")]
        static GameObject Ruined(PlaceContext c) => Wall(c, "Ruined wall", WallStyle.Ruined);

        [PaletteLook("masonry.stone-wall", "retaining", "Retaining", Tooltip = "Holds a terrace: the ground behind it raised to its top")]
        static GameObject Retaining(PlaceContext c) => Wall(c, "Retaining wall", WallStyle.Retaining);

        [PaletteItem("masonry.timber-wall", "Masonry/Walls", "Timber Wall", Piece = typeof(WallShape), LookLabel = "Palisade", Snap = PaletteSnap.Ground | PaletteSnap.WallEnd,
                     Tags = "wall timber palisade plank logs wattle", Tooltip = "Timber along a line (it freezes into elements like the rest).")]
        static GameObject TimberWall(PlaceContext c) => Wall(c, "Palisade", WallStyle.Palisade);

        [PaletteLook("masonry.timber-wall", "plank", "Plank")]
        static GameObject PlankWall(PlaceContext c) => Wall(c, "Plank wall", WallStyle.Plank);

        [PaletteLook("masonry.timber-wall", "logs", "Stacked Logs")]
        static GameObject Logs(PlaceContext c) => Wall(c, "Log wall", WallStyle.StackedLogs);

        [PaletteLook("masonry.timber-wall", "wattle", "Wattle")]
        static GameObject Wattle(PlaceContext c) => Wall(c, "Wattle", WallStyle.Wattle);

        [PaletteItem("masonry.fence", "Masonry/Fences", "Fence", Piece = typeof(WallShape), LookLabel = "Rail Fence", Snap = PaletteSnap.Ground | PaletteSnap.WallEnd,
                     Tags = "fence rail pickets rope hedge", Tooltip = "Hip high: a player mantles it (never a barrier).")]
        static GameObject Fence(PlaceContext c) => Wall(c, "Fence", WallStyle.RailFence);

        [PaletteLook("masonry.fence", "pickets", "Pickets")]
        static GameObject Pickets(PlaceContext c) => Wall(c, "Picket fence", WallStyle.Pickets);

        [PaletteLook("masonry.fence", "rope", "Rope on Posts")]
        static GameObject RopeFence(PlaceContext c) => Wall(c, "Rope fence", WallStyle.RopeOnPosts);

        [PaletteLook("masonry.fence", "hedge", "Hedge")]
        static GameObject Hedge(PlaceContext c) => Wall(c, "Hedge", WallStyle.Hedge);

        [PaletteItem("masonry.mixed-wall", "Masonry/Walls", "Mixed Wall", Piece = typeof(WallShape), LookLabel = "Stone and Planks", Snap = PaletteSnap.Ground | PaletteSnap.WallEnd,
                     Tags = "wall mixed half-timbered plaster stone timber")]
        static GameObject MixedWall(PlaceContext c) => Wall(c, "Mixed wall", WallStyle.StoneBaseTimberTop);

        [PaletteLook("masonry.mixed-wall", "half-timbered", "Half-timbered", Tooltip = "A timber frame with plaster panels on a stone band")]
        static GameObject HalfTimbered(PlaceContext c) => Wall(c, "Half-timbered wall", WallStyle.HalfTimbered);

        [PaletteLook("masonry.mixed-wall", "timber-cap", "Timber Cap")]
        static GameObject TimberCap(PlaceContext c) => Wall(c, "Capped wall", WallStyle.StoneTimberCap);

        [PaletteItem("masonry.ruin", "Masonry/Ruins", "Ruin Fragment", Piece = typeof(WallShape), Tags = "ruin fragment broken wall corner",
                     Tooltip = "A short corner of ruined wall, its top broken high and low.")]
        static GameObject Ruin(PlaceContext c) => Wall(c, "Ruin", WallStyle.Ruined, w =>
        {
            w.Points.Clear();
            w.Points.AddRange(new[] { new Vector3(-3f, 0f, 0f), Vector3.zero, new Vector3(0f, 0f, 2.5f) });
            w.Height = 3.6f;
            w.Crest = 2.6f;
            w.Breaches.Clear();
        });

        [PaletteItem("masonry.gate-wall", "Masonry/Walls", "Gate Wall", Piece = typeof(WallShape), Tags = "gate wall palisade door kit",
                     Tooltip = "A palisade with a kit door in its opening (placed in the Kit group, sized; wire it to a source).")]
        static GameObject GateWall(PlaceContext c) => Wall(c, "Gate wall", WallStyle.Palisade, w =>
        {
            w.Points.Clear();
            w.Points.AddRange(new[] { new Vector3(-8f, 0f, 0f), new Vector3(8f, 0f, 0f) });
            w.Openings.Clear();
            w.Openings.Add(new WallOpening(8f, 3.4f, 3f, OpeningPassage.KitDoor) { frame = OpeningFrame.Timber, look = ForestKitBuilder.Gate });
        });

        // the stone door looks on the kit's doors
        [PaletteLook("kit.door", "portcullis", "Iron Portcullis", Prefab = StoneKitBuilder.Portcullis, Tooltip = "Stone look")]
        static GameObject Portcullis(PlaceContext c) => Door(c, StoneKitBuilder.Portcullis, "Portcullis");

        [PaletteLook("kit.door", "slab", "Stone Slab", Prefab = StoneKitBuilder.Slab, Tooltip = "Stone look")]
        static GameObject Slab(PlaceContext c) => Door(c, StoneKitBuilder.Slab, "Stone slab");

        [PaletteLook("kit.door", "plank", "Plank Door", Prefab = StoneKitBuilder.PlankDoor, Tooltip = "Stone look")]
        static GameObject PlankDoor(PlaceContext c) => Door(c, StoneKitBuilder.PlankDoor, "Plank door");

        [PaletteLook("kit.sliding-door", "slab", "Stone Slab", Prefab = StoneKitBuilder.SlabSliding, Tooltip = "Stone look")]
        static GameObject SlabSliding(PlaceContext c) => Door(c, StoneKitBuilder.SlabSliding, "Sliding slab");

        [PaletteLook("kit.sliding-door", "plank", "Plank Door", Prefab = StoneKitBuilder.PlankDoorSliding, Tooltip = "Stone look")]
        static GameObject PlankSliding(PlaceContext c) => Door(c, StoneKitBuilder.PlankDoorSliding, "Sliding plank door");

        static GameObject Door(PlaceContext c, string path, string name)
        {
            StoneKitBuilder.EnsureBuilt();
            return PaletteKitEntries.Place(c, path, name);
        }
    }
}
