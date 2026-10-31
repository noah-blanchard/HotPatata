using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>The stone pieces' inspectors and handles (docs/LEVEL_AUTHORING.md §31).</summary>
    [CustomEditor(typeof(StoneStairShape)), CanEditMultipleObjects]
    public sealed class StoneStairShapeEditor : LevelPieceEditor
    {
        StoneStairShape Stair => (StoneStairShape)target;
        protected override string Help => "A stone stair: its dots are the walk's line from its foot to its top. It is walked as a smooth ramp (the steps are drawn), " +
                                          "its steps as many as its rise needs at the rise per step; built, it has side walls.";
        protected override void DrawTools() => PencilButton(Stair.Points, false);
        protected override void DrawScene() => PieceHandles.EditPoints(Stair, Stair.Points, false, false, 2);
    }

    [CustomEditor(typeof(PavingShape)), CanEditMultipleObjects]
    public sealed class PavingShapeEditor : LevelPieceEditor
    {
        PavingShape Paving => (PavingShape)target;
        protected override string Help => "Paving: its dots are its outline at the piece's height. Raised, it stands on a retaining edge of stone (a terrace) and is " +
                                          "walked at its top.";
        protected override void DrawTools() => PencilButton(Paving.Outline, true);
        protected override void DrawScene() => PieceHandles.EditPoints(Paving, Paving.Outline, true, true, 3);
    }

    [CustomEditor(typeof(SteppingStonesShape)), CanEditMultipleObjects]
    public sealed class SteppingStonesShapeEditor : LevelPieceEditor
    {
        SteppingStonesShape Stones => (SteppingStonesShape)target;
        protected override string Help => "Stepping stones: its dots are the line they follow, a stone every Spacing (2.5 m at most: a jump). Over water, a miss " +
                                          "falls into the water's kill zone.";
        protected override void DrawTools() => PencilButton(Stones.Points, false);
        protected override void DrawScene() => PieceHandles.EditPoints(Stones, Stones.Points, false, false, 2);
    }

    [CustomEditor(typeof(StoneBridgeShape)), CanEditMultipleObjects]
    public sealed class StoneBridgeShapeEditor : LevelPieceEditor
    {
        StoneBridgeShape Bridge => (StoneBridgeShape)target;
        protected override string Help => "A stone bridge between its two dots (its deck's ends): its deck is walked, its parapets and the masonry under it stop a " +
                                          "body, its arches stay open.";

        protected override void DrawScene()
        {
            var b = Bridge;
            var (a, e) = b.WorldEnds();
            Handles.color = Color.white;
            Handles.DrawAAPolyLine(3f, a, e);
            EditorGUI.BeginChangeCheck();
            var na = Handles.PositionHandle(a, Quaternion.identity);
            var ne = Handles.PositionHandle(e, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(b, "Move bridge end");
            b.Start = b.transform.InverseTransformPoint(na);
            b.End = b.transform.InverseTransformPoint(ne);
            EditorUtility.SetDirty(b);
        }
    }

    [CustomEditor(typeof(ArchShape)), CanEditMultipleObjects]
    public sealed class ArchShapeEditor : LevelPieceEditor
    {
        protected override string Help => "An arch or a portal: its span is along the piece's red axis, its depth along the blue. Its piers and its top collide, a " +
                                          "body walks through under it; timber stands on its top. Crest breaks it like a ruin.";
        protected override void DrawScene() { }
    }

    [CustomEditor(typeof(ColumnShape)), CanEditMultipleObjects]
    public sealed class ColumnShapeEditor : LevelPieceEditor
    {
        protected override string Help => "A column: its base, its drums, its capital. Broken, its top's drums lie at its foot (they collide). An obstacle, never a lock.";
        protected override void DrawScene() { }
    }

    [CustomEditor(typeof(PlinthShape)), CanEditMultipleObjects]
    public sealed class PlinthShapeEditor : LevelPieceEditor
    {
        protected override string Help => "A plinth: a block of dressed stone, walked on, with up to three steps round it.";
        protected override void DrawScene() { }
    }

    [CustomEditor(typeof(TowerShape)), CanEditMultipleObjects]
    public sealed class TowerShapeEditor : LevelPieceEditor
    {
        TowerShape Tower => (TowerShape)target;
        protected override string Help => "A tower: its walls are drawn as a wall's (stone or timber), its openings measured round its wall from its south point (doors " +
                                          "and windows: the same passages as a wall's, their kit placed). Each floor is walked, its hatch to the east. The climb inside is " +
                                          "yours (a stone stair, a walkway, a lift). A roof is a ceiling; crenellations and a parapet are not.";

        protected override void DrawTools()
        {
            float locked = MasonryKit.LockedHeight(Tower);
            EditorGUILayout.HelpBox($"Its walls: {locked:F1} m at their lowest ({MasonryKit.Reads(locked)}).", MessageType.Info);
            if (GUILayout.Button("Add a door (kit door, to the south)"))
            {
                Undo.RecordObject(Tower, "Add door");
                Tower.Openings.Add(new WallOpening(0.6f + 1.4f, 2.4f, 2.8f, OpeningPassage.KitDoor) { frame = OpeningFrame.Stone });
                EditorUtility.SetDirty(Tower);
            }
        }

        protected override void DrawScene() { }
    }

    /// <summary>The stone pieces in the palette (ARCHITECTURE §27.3, §27.7).</summary>
    public static class StoneEntries
    {
        static T Piece<T>(PlaceContext c, string name, float above = 0f) where T : LevelPiece =>
            (T)PaletteCatalog.NewPiece(c, typeof(T), name, c.Ground(above), LevelGroup.Timber);

        static GameObject Built(LevelPiece piece)
        {
            LevelPieceBuilder.Rebuild(piece);
            return piece.gameObject;
        }

        [PaletteItem("masonry.stone-stair", "Masonry/Stone ways", "Stone Stair", Piece = typeof(StoneStairShape), LookLabel = "Flagstone", Snap = PaletteSnap.Ground | PaletteSnap.DeckEdge,
                     Tags = "stair steps stone flight", Tooltip = "Steps from its foot to its top, walked as a smooth ramp.")]
        static GameObject Stair(PlaceContext c) => Built(Piece<StoneStairShape>(c, "Stone stair"));

        [PaletteLook("masonry.stone-stair", "rock", "Cut in Rock")]
        static GameObject StairRock(PlaceContext c)
        {
            var s = Piece<StoneStairShape>(c, "Rock stair");
            Undo.RecordObject(s, "Rock stair");
            s.Kind = StoneStairKind.CutInRock;
            return Built(s);
        }

        [PaletteLook("masonry.stone-stair", "built", "Built", Tooltip = "Between side walls")]
        static GameObject StairBuilt(PlaceContext c)
        {
            var s = Piece<StoneStairShape>(c, "Built stair");
            Undo.RecordObject(s, "Built stair");
            s.Kind = StoneStairKind.Built;
            return Built(s);
        }

        [PaletteItem("masonry.paving", "Masonry/Stone ways", "Paving", Piece = typeof(PavingShape), LookLabel = "Flagstones", Tags = "paving flagstones cobbles slabs terrace floor")]
        static GameObject Paving(PlaceContext c) => Built(Piece<PavingShape>(c, "Paving"));

        [PaletteLook("masonry.paving", "cobbles", "Cobbles")]
        static GameObject Cobbles(PlaceContext c)
        {
            var p = Piece<PavingShape>(c, "Cobbles");
            Undo.RecordObject(p, "Cobbles");
            p.Kind = PavingKind.Cobbles;
            return Built(p);
        }

        [PaletteLook("masonry.paving", "terrace", "Raised Terrace", Tooltip = "Slabs on a retaining edge 1.5 m high")]
        static GameObject Terrace(PlaceContext c)
        {
            var p = Piece<PavingShape>(c, "Terrace");
            Undo.RecordObject(p, "Terrace");
            p.Kind = PavingKind.Slabs;
            p.Raised = 1.5f;
            return Built(p);
        }

        [PaletteItem("masonry.stepping-stones", "Masonry/Stone ways", "Stepping Stones", Piece = typeof(SteppingStonesShape), Tags = "stepping stones river jump ford")]
        static GameObject Stepping(PlaceContext c) => Built(Piece<SteppingStonesShape>(c, "Stepping stones"));

        [PaletteItem("masonry.stone-bridge", "Masonry/Stone ways", "Stone Bridge", Piece = typeof(StoneBridgeShape), Tags = "bridge stone arches river parapet")]
        static GameObject Bridge(PlaceContext c) => Built(Piece<StoneBridgeShape>(c, "Stone bridge"));

        [PaletteItem("masonry.arch", "Masonry/Arches", "Arch", Piece = typeof(ArchShape), LookLabel = "Round", Tags = "arch gate portal stone")]
        static GameObject Arch(PlaceContext c) => Built(Piece<ArchShape>(c, "Arch"));

        [PaletteLook("masonry.arch", "pointed", "Pointed")]
        static GameObject Pointed(PlaceContext c) => ArchOf(c, "Pointed arch", ArchKind.StonePointed, a => a.Rise = 2.2f);

        [PaletteLook("masonry.arch", "lintel", "Stone Lintel")]
        static GameObject Lintel(PlaceContext c) => ArchOf(c, "Lintel", ArchKind.StoneLintel);

        [PaletteItem("masonry.portal", "Masonry/Openings", "Portal", Piece = typeof(ArchShape), LookLabel = "Log Portal", Tags = "portal log posts beam gate",
                     Tooltip = "Two log posts, a beam and braces: a gateway with no wall.")]
        static GameObject Portal(PlaceContext c) => ArchOf(c, "Portal", ArchKind.LogPortal);

        [PaletteLook("masonry.portal", "log-lintel", "Log Lintel")]
        static GameObject LogLintel(PlaceContext c) => ArchOf(c, "Log lintel", ArchKind.LogLintel);

        static GameObject ArchOf(PlaceContext c, string name, ArchKind kind, System.Action<ArchShape> setup = null)
        {
            var a = Piece<ArchShape>(c, name);
            Undo.RecordObject(a, name);
            a.Kind = kind;
            setup?.Invoke(a);
            return Built(a);
        }

        [PaletteItem("masonry.column", "Masonry/Ruins", "Column", Piece = typeof(ColumnShape), LookLabel = "Standing", Tags = "column pillar drum ruin")]
        static GameObject Column(PlaceContext c) => Built(Piece<ColumnShape>(c, "Column"));

        [PaletteLook("masonry.column", "broken", "Broken", Tooltip = "Its top fallen at its foot")]
        static GameObject Broken(PlaceContext c)
        {
            var col = Piece<ColumnShape>(c, "Broken column");
            Undo.RecordObject(col, "Broken column");
            col.Broken = 0.55f;
            col.Lean = 4f;
            col.Capital = false;
            return Built(col);
        }

        [PaletteItem("masonry.plinth", "Masonry/Ruins", "Plinth", Piece = typeof(PlinthShape), Tags = "plinth base steps podium")]
        static GameObject Plinth(PlaceContext c) => Built(Piece<PlinthShape>(c, "Plinth"));

        [PaletteItem("masonry.tower", "Masonry/Towers", "Tower", Piece = typeof(TowerShape), LookLabel = "Watchtower", Tags = "tower watchtower keep floors crenellations roof")]
        static GameObject Tower(PlaceContext c)
        {
            var t = Piece<TowerShape>(c, "Tower");
            Undo.RecordObject(t, "Tower");
            t.Openings.Add(new WallOpening(2f, 2.2f, 2.8f, OpeningPassage.Free) { shape = OpeningShape.RoundArch, frame = OpeningFrame.Stone });
            return Built(t);
        }

        [PaletteLook("masonry.tower", "roofed", "Roofed", Tooltip = "A cone roof (a ceiling)")]
        static GameObject Roofed(PlaceContext c)
        {
            var t = Piece<TowerShape>(c, "Roofed tower");
            Undo.RecordObject(t, "Roofed tower");
            t.Top = TowerTop.ConeRoof;
            t.Openings.Add(new WallOpening(2f, 2.2f, 2.8f, OpeningPassage.Free) { shape = OpeningShape.RoundArch, frame = OpeningFrame.Stone });
            return Built(t);
        }

        [PaletteLook("masonry.tower", "ruined", "Ruined")]
        static GameObject RuinedTower(PlaceContext c)
        {
            var t = Piece<TowerShape>(c, "Ruined tower");
            Undo.RecordObject(t, "Ruined tower");
            t.Top = TowerTop.Ruined;
            t.WallStyle = WallStyle.DryStone;
            t.Floors.Clear();
            t.Openings.Add(new WallOpening(2f, 2.2f, 2.8f, OpeningPassage.Free) { shape = OpeningShape.Breach });
            return Built(t);
        }
    }
}
