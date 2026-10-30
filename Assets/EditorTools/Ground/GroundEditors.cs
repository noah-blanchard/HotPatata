using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The land pieces in the palette and the Scene view (ARCHITECTURE §27.4, docs/LEVEL_AUTHORING.md §27): hills, hollows, ridges and
    /// plateaus (and their presets), trails, river beds and ravines, made in the level's Ground group (the stack applies them in its order),
    /// their dots dragged like a deck's, a land shape's height on a white handle.
    /// </summary>
    public static class GroundEntries
    {
        static GameObject Land(PlaceContext c, string name, LandKind kind, float height, float falloff, LandProfile profile, Vector3[] outline)
        {
            var go = PaletteCatalog.NewObject(c, name, c.Ground(0f), LevelGroup.Ground);
            var land = Undo.AddComponent<LandShape>(go);
            land.Kind = kind;
            land.Height = height;
            land.Falloff = falloff;
            land.Profile = profile;
            if (outline != null)
            {
                land.Points.Clear();
                land.Points.AddRange(outline);
            }
            land.EnsureId();
            LevelPieceBuilder.Rebuild(land);
            return go;
        }

        static Vector3[] Blob(float rx, float rz, int n = 10) =>
            Enumerable.Range(0, n).Select(i =>
            {
                float a = i * Mathf.PI * 2f / n, wobble = 1f + 0.12f * Mathf.Sin(a * 3f + 0.7f);
                return new Vector3(Mathf.Cos(a) * rx * wobble, 0f, Mathf.Sin(a) * rz * wobble);
            }).ToArray();

        [PaletteItem("ground.hill", "Ground/Shapes", "Hill", Piece = typeof(LandShape), Icon = "d_TerrainInspector.TerrainToolRaise", Tags = "hill mound rise land shape",
                     Tooltip = "A round hill added to the ground (non-destructive: move it, the sculpt round it stays).")]
        static GameObject Hill(PlaceContext c) => Land(c, "Hill", LandKind.Hill, 10f, 14f, LandProfile.Smooth, Blob(14f, 11f));

        [PaletteItem("ground.hollow", "Ground/Shapes", "Hollow", Piece = typeof(LandShape), Icon = "TerrainInspector.TerrainToolLowerAlt", Tags = "hollow basin dip land shape")]
        static GameObject Hollow(PlaceContext c) => Land(c, "Hollow", LandKind.Hollow, 4f, 10f, LandProfile.Smooth, Blob(12f, 9f));

        [PaletteItem("ground.ridge", "Ground/Shapes", "Ridge", Piece = typeof(LandShape), Icon = "TerrainInspector.TerrainToolSmoothHeight", Tags = "ridge spine crest land shape")]
        static GameObject Ridge(PlaceContext c) =>
            Land(c, "Ridge", LandKind.Ridge, 9f, 12f, LandProfile.Smooth, new[] { new Vector3(-20f, 0f, -4f), new Vector3(-5f, 0f, 3f), new Vector3(10f, 0f, -2f), new Vector3(24f, 0f, 4f) });

        [PaletteItem("ground.plateau", "Ground/Shapes", "Plateau", Piece = typeof(LandShape), Icon = "d_TerrainInspector.TerrainToolSetheight", Tags = "plateau mesa table flat land shape")]
        static GameObject Plateau(PlaceContext c) => Land(c, "Plateau", LandKind.Plateau, 6f, 8f, LandProfile.Smooth, Blob(12f, 10f, 8));

        [PaletteItem("ground.round-hill", "Ground/Shape presets", "Round Hill", Piece = typeof(LandShape), Icon = "d_TerrainInspector.TerrainToolRaise", Tags = "preset hill")]
        static GameObject RoundHill(PlaceContext c) => Land(c, "Round hill", LandKind.Hill, 18f, 24f, LandProfile.Smooth, Blob(20f, 20f, 12));

        [PaletteItem("ground.long-ridge", "Ground/Shape presets", "Long Ridge", Piece = typeof(LandShape), Icon = "TerrainInspector.TerrainToolSmoothHeight", Tags = "preset ridge")]
        static GameObject LongRidge(PlaceContext c) =>
            Land(c, "Long ridge", LandKind.Ridge, 14f, 18f, LandProfile.Steep, new[] { new Vector3(-50f, 0f, -6f), new Vector3(-20f, 0f, 6f), new Vector3(15f, 0f, -4f), new Vector3(50f, 0f, 8f) });

        [PaletteItem("ground.basin", "Ground/Shape presets", "Basin", Piece = typeof(LandShape), Icon = "TerrainInspector.TerrainToolLowerAlt", Tags = "preset basin hollow")]
        static GameObject Basin(PlaceContext c) => Land(c, "Basin", LandKind.Hollow, 6f, 18f, LandProfile.Terraced, Blob(26f, 20f, 12));

        [PaletteItem("ground.mesa", "Ground/Shape presets", "Mesa", Piece = typeof(LandShape), Icon = "d_TerrainInspector.TerrainToolSetheight", Tags = "preset mesa plateau")]
        static GameObject Mesa(PlaceContext c) => Land(c, "Mesa", LandKind.Plateau, 16f, 7f, LandProfile.Steep, Blob(18f, 14f, 10));

        [PaletteItem("ground.trail", "Ground/Ways", "Trail", Piece = typeof(TrailShape), Icon = "d_TerrainInspector.TerrainToolSplat", Tags = "trail path way walk",
                     Tooltip = "A way on the ground: held to its grade, flat across, banked, painted and walked.")]
        static GameObject Trail(PlaceContext c)
        {
            var go = PaletteCatalog.NewObject(c, "Trail", c.Ground(0f), LevelGroup.Ground);
            var trail = Undo.AddComponent<TrailShape>(go);
            trail.EnsureId();
            LevelPieceBuilder.Rebuild(trail);
            return go;
        }

        [PaletteItem("ground.river", "Ground/Ways", "River Bed", Piece = typeof(RiverShape), Icon = "TerrainInspector.TerrainToolLowerAlt", Tags = "river stream water bed kill",
                     Tooltip = "A river carved along a line, its water over kill zones (falling in fails the section).")]
        static GameObject River(PlaceContext c)
        {
            var go = PaletteCatalog.NewObject(c, "River", c.Ground(0f), LevelGroup.Ground);
            var river = Undo.AddComponent<RiverShape>(go);
            river.EnsureId();
            LevelPieceBuilder.Rebuild(river);
            return go;
        }

        [PaletteItem("ground.ravine", "Ground/Ways", "Ravine", Piece = typeof(RiverShape), Icon = "TerrainInspector.TerrainToolLowerAlt", Tags = "ravine gorge gully dry")]
        static GameObject Ravine(PlaceContext c)
        {
            var go = PaletteCatalog.NewObject(c, "Ravine", c.Ground(0f), LevelGroup.Ground);
            var river = Undo.AddComponent<RiverShape>(go);
            river.Kind = RiverKind.Ravine;
            river.Width = 10f;
            river.Depth = 6f;
            river.Profile = RiverProfile.V;
            river.EnsureId();
            LevelPieceBuilder.Rebuild(river);
            return go;
        }
    }

    [CustomEditor(typeof(LandShape)), CanEditMultipleObjects]
    public sealed class LandShapeEditor : LevelPieceEditor
    {
        LandShape Land => (LandShape)target;

        protected override string Help =>
            "A land shape: its dots are its outline (a ridge: its crest line). It is added to the ground, not painted in: move it, change it or delete it " +
            "and the sculpt round it stays. The white handle sets its height; the ring is where it fades out. The level's Ground group gives the order.";

        protected override void DrawTools() => PencilButton(Land.Points, Land.Closed);

        protected override void DrawScene()
        {
            var land = Land;
            PieceHandles.EditPoints(land, land.Points, land.Closed, true, land.Closed ? 3 : 2);
            var pts = land.WorldPoints();
            if (pts.Count < 2) return;
            float y = land.transform.position.y;
            Handles.color = new Color(0.5f, 1f, 0.6f, 0.5f);
            var outer = land.Closed ? Poly2.Offset(Poly2.CounterClockwise(pts), land.Falloff) : null;
            if (outer != null && outer.Count > 2) Handles.DrawAAPolyLine(2f, outer.Select(p => new Vector3(p.x, y, p.y)).Append(new Vector3(outer[0].x, y, outer[0].y)).ToArray());
            var c = land.transform.position + Vector3.up * land.Height;
            EditorGUI.BeginChangeCheck();
            var to = Handles.Slider(c, Vector3.up, HandleUtility.GetHandleSize(c) * 0.08f, Handles.CubeHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(land, "Land shape height");
                land.Height = to.y - land.transform.position.y;
                EditorUtility.SetDirty(land);
            }
            PieceHandles.Label(c + Vector3.up * 0.5f, $"{land.Kind} {land.Height:F1} m");
        }
    }

    [CustomEditor(typeof(TrailShape)), CanEditMultipleObjects]
    public sealed class TrailShapeEditor : LevelPieceEditor
    {
        TrailShape Trail => (TrailShape)target;

        protected override string Help =>
            "A trail: its dots are its line on the ground. The ground is held to its grade along it and flat across it, banked each side, painted, and " +
            "walked by the route runner. Where the slope is steeper than its grade it cuts and fills.";

        protected override void DrawTools()
        {
            if (GroundShapes.Trails.TryGetValue(Trail.PieceId ?? "", out var p))
            {
                bool deep = p.worstCut > TrailShape.DeepCut;
                EditorGUILayout.HelpBox($"Deepest cut or fill: {p.worstCut:F1} m." + (deep ? " The slope is steeper than the grade there: add a Stone Stair, or lower its grade." : ""),
                                        deep ? MessageType.Warning : MessageType.None);
            }
            PencilButton(Trail.Points, false);
        }

        protected override void DrawScene()
        {
            PieceHandles.EditPoints(Trail, Trail.Points, false, true, 2);
            Handles.color = new Color(0.9f, 0.75f, 0.45f, 0.8f);
            var line = GroundOutputs.TrailLine(Trail);
            if (line.Length > 1) Handles.DrawAAPolyLine(3f, line);
        }
    }

    [CustomEditor(typeof(RiverShape)), CanEditMultipleObjects]
    public sealed class RiverShapeEditor : LevelPieceEditor
    {
        RiverShape River => (RiverShape)target;

        protected override string Help =>
            "A river bed (or a dry ravine): its dots are its line, upstream first. The bed falls along it and never rises; a river's water lies Water Drop " +
            "under the banks, over kill zones all along (falling in fails the section). Its flow is drawn only: it moves nothing.";

        protected override void DrawTools() => PencilButton(River.Points, false);

        protected override void DrawScene()
        {
            PieceHandles.EditPoints(River, River.Points, false, true, 2);
            if (!GroundShapes.Rivers.TryGetValue(River.PieceId ?? "", out var bed) || bed.bed.Length < 2) return;
            Handles.color = new Color(0.4f, 0.75f, 1f, 0.8f);
            Handles.DrawAAPolyLine(3f, bed.bed);
        }
    }
}
