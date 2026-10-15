using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The inspector and Scene view of every authored piece (ARCHITECTURE §27): its fields, Rebuild, presets, and the piece's own handles
    /// (<see cref="DrawScene"/>). The geometry follows on its own once the mouse lets go (<see cref="LevelPieceWatcher"/>).
    /// </summary>
    public abstract class LevelPieceEditor : UnityEditor.Editor
    {
        TimberPreset preset;

        protected LevelPiece Piece => (LevelPiece)target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            DrawTools();
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild")) LevelPieceBuilder.Rebuild(targets.Cast<LevelPiece>().ToList());
                if (GUILayout.Button("Save As Preset")) TimberPreset.SaveAs(Piece);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                preset = (TimberPreset)EditorGUILayout.ObjectField(preset, typeof(TimberPreset), false);
                using (new EditorGUI.DisabledScope(preset == null))
                    if (GUILayout.Button("Apply Preset", GUILayout.Width(110)) && !preset.ApplyTo(Piece))
                        Debug.LogWarning($"[Level] {preset.name} is a preset for another kind of piece");
            }
            EditorGUILayout.HelpBox(Help, MessageType.None);
        }

        /// <summary>The piece's own buttons (shapes, pencil).</summary>
        protected virtual void DrawTools() { }

        protected abstract string Help { get; }

        // protected, not private: Unity finds OnSceneGUI by reflection on the derived editor, which never sees a private base method
        protected void OnSceneGUI()
        {
            PiecePencil.OnSceneGUI(Piece);
            if (!PiecePencil.IsDrawing(Piece)) DrawScene();
        }

        protected abstract void DrawScene();

        /// <summary>The pencil button: draws <paramref name="points"/> again click by click.</summary>
        protected void PencilButton(List<Vector3> points, bool flat)
        {
            bool drawing = PiecePencil.IsDrawing(Piece);
            if (GUILayout.Button(drawing ? "Stop drawing" : "Draw points (pencil)"))
            {
                if (drawing) PiecePencil.End();
                else PiecePencil.Begin(Piece, points, flat);
            }
        }
    }

    // ------------------------------------------------------------------ deck

    [CustomEditor(typeof(TimberDeckShape)), CanEditMultipleObjects]
    public sealed class TimberDeckShapeEditor : LevelPieceEditor
    {
        static float width = 6f, depth = 4f, corner = 1f, ringWidth = 3f, arcFrom = 0f, arcTo = 180f;

        TimberDeckShape Deck => (TimberDeckShape)target;

        protected override string Help =>
            "Scene view: click a dot to move it, Ctrl+click to delete it, click a + to add a point. Move the piece to change its height. " +
            "Walkway and bridge ends dropped on its edge open its railing (green); cyan dots are its own openings, slide them along the edge.";

        protected override void DrawTools()
        {
            EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                width = EditorGUILayout.FloatField("Width × depth", width);
                depth = EditorGUILayout.FloatField(depth);
            }
            corner = EditorGUILayout.FloatField("Corner radius", corner);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rectangle")) SetOutline(DeckShapes.Rectangle(width, depth));
                if (GUILayout.Button("Rounded")) SetOutline(DeckShapes.Rounded(width, depth, corner));
                if (GUILayout.Button("Oval")) SetOutline(DeckShapes.Oval(width, depth));
            }
            var trunk = Deck.Trunk;
            using (new EditorGUI.DisabledScope(trunk == null))
            {
                ringWidth = EditorGUILayout.FloatField("Ring width", ringWidth);
                using (new EditorGUILayout.HorizontalScope())
                {
                    arcFrom = EditorGUILayout.FloatField("Arc from / to (°)", arcFrom);
                    arcTo = EditorGUILayout.FloatField(arcTo);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Ring round the trunk")) RoundTrunk(0f, 360f);
                    if (GUILayout.Button("Arc round the trunk")) RoundTrunk(arcFrom, arcTo);
                }
            }
            if (trunk == null) EditorGUILayout.HelpBox("Set Trunk (a giant tree) to wrap the deck round it.", MessageType.None);
            PencilButton(Deck.Outline, true);
            if (GUILayout.Button("Add opening"))
            {
                Undo.RecordObject(Deck, "Add opening");
                Deck.Openings.Add(new TimberOpening { along = 0f, width = 1.6f });
                EditorUtility.SetDirty(Deck);
            }
        }

        void SetOutline(List<Vector3> local)
        {
            Undo.RecordObject(Deck, "Deck shape");
            Deck.Outline.Clear();
            Deck.Outline.AddRange(local);
            EditorUtility.SetDirty(Deck);
        }

        void RoundTrunk(float from, float to)
        {
            if (!Deck.Trunk.TryTrunkAt(Deck.transform.position.y, out var center, out float radius))
            {
                Debug.LogWarning($"[Level] {Deck.name}: its trunk does not reach {Deck.transform.position.y:F1} m");
                return;
            }
            var world = DeckShapes.RoundTrunk(new Vector2(center.x, center.z), radius, ringWidth, from, to);
            SetOutline(world.Select(p => { var l = Deck.Local(new Vector3(p.x, Deck.transform.position.y, p.y)); return new Vector3(l.x, 0f, l.z); }).ToList());
        }

        protected override void DrawScene()
        {
            var deck = Deck;
            PieceHandles.EditPoints(deck, deck.Outline, true, true, 3);
            // attached ends (green) and the deck's own openings (cyan, slide along the edge)
            foreach (var link in TimberLinks.Links(deck.gameObject.scene).Where(l => l.deck == deck))
            {
                var at = deck.EdgePoint(link.along);
                Handles.color = PieceHandles.Attached;
                Handles.DrawWireDisc(at, Vector3.up, link.width * 0.5f + TimberLinks.OpeningMargin * 0.5f);
            }
            for (int i = 0; i < deck.Openings.Count; i++)
            {
                var o = deck.Openings[i];
                var at = deck.EdgePoint(o.along);
                Handles.color = PieceHandles.OpeningColor;
                Handles.DrawWireDisc(at, Vector3.up, o.width * 0.5f);
                EditorGUI.BeginChangeCheck();
                var to = Handles.FreeMoveHandle(at, HandleUtility.GetHandleSize(at) * 0.06f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(deck, "Move opening");
                    o.along = deck.NearestAlong(to, out _);
                    deck.Openings[i] = o;
                    EditorUtility.SetDirty(deck);
                }
            }
        }
    }

    /// <summary>The ready-made outlines of a deck (local x, z) and its outline round a trunk (world x, z).</summary>
    public static class DeckShapes
    {
        public static List<Vector3> Rectangle(float w, float d) => Rounded(w, d, 0f);

        public static List<Vector3> Rounded(float w, float d, float r)
        {
            float hx = Mathf.Max(0.5f, w * 0.5f), hz = Mathf.Max(0.5f, d * 0.5f);
            r = Mathf.Clamp(r, 0f, Mathf.Min(hx, hz) - 0.05f);
            var list = new List<Vector3>();
            if (r < 0.05f)
            {
                list.Add(new Vector3(-hx, 0f, -hz));
                list.Add(new Vector3(hx, 0f, -hz));
                list.Add(new Vector3(hx, 0f, hz));
                list.Add(new Vector3(-hx, 0f, hz));
                return list;
            }
            var corners = new[] { new Vector2(hx - r, -hz + r), new Vector2(hx - r, hz - r), new Vector2(-hx + r, hz - r), new Vector2(-hx + r, -hz + r) };
            for (int c = 0; c < 4; c++)
                for (int k = 0; k <= 4; k++)
                {
                    float a = (-90f + c * 90f + k * 22.5f) * Mathf.Deg2Rad;
                    list.Add(new Vector3(corners[c].x + Mathf.Cos(a) * r, 0f, corners[c].y + Mathf.Sin(a) * r));
                }
            return list;
        }

        public static List<Vector3> Oval(float w, float d, int samples = 24)
        {
            var list = new List<Vector3>();
            for (int i = 0; i < samples; i++)
            {
                float a = i / (float)samples * Mathf.PI * 2f;
                list.Add(new Vector3(Mathf.Cos(a) * w * 0.5f, 0f, Mathf.Sin(a) * d * 0.5f));
            }
            return list;
        }

        /// <summary>
        /// A deck round a trunk from <paramref name="from"/> to <paramref name="to"/> degrees (a whole ring keeps a seam: the outline doubles
        /// back on itself there, and no rail runs along it).
        /// </summary>
        public static List<Vector2> RoundTrunk(Vector2 center, float trunkRadius, float width, float from, float to)
        {
            if (to - from >= 359f)
            {
                from += 0.6f;
                to = from + 358.8f;
            }
            float inner = trunkRadius + 0.12f, outer = inner + Mathf.Max(1f, width);
            int samples = Mathf.Max(8, Mathf.CeilToInt((to - from) / 7.5f));
            return Poly2.Arc(center, inner, _ => outer, from * Mathf.Deg2Rad, to * Mathf.Deg2Rad, samples);
        }
    }

    // ------------------------------------------------------------------ walkway

    [CustomEditor(typeof(TimberWalkShape)), CanEditMultipleObjects]
    public sealed class TimberWalkShapeEditor : LevelPieceEditor
    {
        TimberWalkShape Walk => (TimberWalkShape)target;

        protected override string Help =>
            "Scene view: click a dot to move it, Ctrl+click to delete it, click a + to add a point. Steps appear where it climbs. " +
            "Drop an end on a deck's edge to attach it (green; it opens the deck's rail); hold Shift to place it freely.";

        protected override void DrawTools()
        {
            PencilButton(Walk.Points, false);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!Walk.Start.Attached))
                    if (GUILayout.Button("Detach start")) SetAttach(true, default);
                using (new EditorGUI.DisabledScope(!Walk.End.Attached))
                    if (GUILayout.Button("Detach end")) SetAttach(false, default);
            }
        }

        void SetAttach(bool start, TimberAttach attach)
        {
            Undo.RecordObject(Walk, "Attach walkway");
            if (start) Walk.Start = attach;
            else Walk.End = attach;
            EditorUtility.SetDirty(Walk);
        }

        protected override void DrawScene()
        {
            var walk = Walk;
            int last = walk.Points.Count - 1;
            Vector3? Drawn(int i)
            {
                if (i == 0 && walk.Start.Attached) return walk.Start.deck.EdgePoint(walk.Start.along);
                if (i == last && walk.End.Attached) return walk.End.deck.EdgePoint(walk.End.along);
                return null;
            }
            // the smooth line the boards follow
            var control = walk.WorldControl();
            if (control.Count >= 2)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.5f);
                Handles.DrawAAPolyLine(2f, Curves.Spline(control, 0.4f).ToArray());
            }
            int moved = PieceHandles.EditPoints(walk, walk.Points, false, false, 2, Drawn);
            if (moved == 0 || moved == last) SnapEnd(moved == 0);
            foreach (var (attach, i) in new[] { (walk.Start, 0), (walk.End, last) })
                if (attach.Attached && i >= 0) PieceHandles.Label(walk.World(walk.Points[i]) + Vector3.up * 0.4f, "on " + attach.deck.name);
        }

        void SnapEnd(bool start)
        {
            var walk = Walk;
            int i = start ? 0 : walk.Points.Count - 1;
            var world = walk.World(walk.Points[i]);
            var attach = PieceHandles.SnapEnd(walk, world, out var at);
            if (attach.Attached) walk.Points[i] = walk.Local(at);
            if (start) walk.Start = attach;
            else walk.End = attach;
            EditorUtility.SetDirty(walk);
        }
    }

    // ------------------------------------------------------------------ rope bridge

    [CustomEditor(typeof(RopeBridgeShape)), CanEditMultipleObjects]
    public sealed class RopeBridgeShapeEditor : LevelPieceEditor
    {
        RopeBridgeShape Bridge => (RopeBridgeShape)target;

        protected override string Help =>
            "Scene view: move either end; drop it on a deck's edge to attach it (green; it opens the deck's rail), hold Shift to place it freely. " +
            "Keep the ends gentle: 18° or less where it leaves the deck.";

        protected override void DrawScene()
        {
            var bridge = Bridge;
            var (a, b) = bridge.WorldEnds();
            float sag = bridge.SagFor(a, b);
            Handles.color = PieceHandles.Line;
            Handles.DrawAAPolyLine(3f, Curves.Sag(a, b, sag, 24).ToArray());
            float slope = Curves.SagEndSlope(a, b, sag);
            PieceHandles.Label((a + b) * 0.5f + Vector3.up * 0.6f, $"{Vector3.Distance(a, b):F1} m, ends {slope:F0}°" + (slope > 18f ? " (too steep)" : ""));
            var toA = PieceHandles.EndHandle(a, bridge.StartAttach.Attached);
            if (toA.HasValue) MoveEnd(true, toA.Value);
            var toB = PieceHandles.EndHandle(b, bridge.EndAttach.Attached);
            if (toB.HasValue) MoveEnd(false, toB.Value);
        }

        void MoveEnd(bool start, Vector3 world)
        {
            var bridge = Bridge;
            Undo.RecordObject(bridge, "Move bridge end");
            var attach = PieceHandles.SnapEnd(bridge, world, out var at);
            if (start)
            {
                bridge.A = bridge.Local(at);
                bridge.StartAttach = attach;
            }
            else
            {
                bridge.B = bridge.Local(at);
                bridge.EndAttach = attach;
            }
            EditorUtility.SetDirty(bridge);
        }
    }

    // ------------------------------------------------------------------ railing

    [CustomEditor(typeof(RailingShape)), CanEditMultipleObjects]
    public sealed class RailingShapeEditor : LevelPieceEditor
    {
        RailingShape Railing => (RailingShape)target;

        protected override string Help => "Scene view: the points sit on the walking surface under the rail. Click a dot to move it, Ctrl+click to delete it, click a + to add a point.";

        protected override void DrawTools() => PencilButton(Railing.Points, false);

        protected override void DrawScene() => PieceHandles.EditPoints(Railing, Railing.Points, Railing.Closed, false, 2);
    }

    // ------------------------------------------------------------------ house

    [CustomEditor(typeof(HouseShape)), CanEditMultipleObjects]
    public sealed class HouseShapeEditor : LevelPieceEditor
    {
        HouseShape House => (HouseShape)target;

        protected override string Help =>
            "Scene view: drag the yellow squares to size the house, slide the cyan doors along their walls. Its walls stop players and the bomb; " +
            "its ceiling stops lobs over them (a house is a sealed room but for its doors and windows).";

        protected override void DrawScene()
        {
            var house = House;
            var t = house.transform;
            var rot = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            Vector3 W(Vector3 local) => t.position + rot * local;
            float hx = house.Size.x * 0.5f, hz = house.Size.y * 0.5f;
            Handles.color = PieceHandles.Line;
            var corners = new[] { W(new Vector3(-hx, 0f, -hz)), W(new Vector3(hx, 0f, -hz)), W(new Vector3(hx, 0f, hz)), W(new Vector3(-hx, 0f, hz)) };
            Handles.DrawAAPolyLine(3f, corners[0], corners[1], corners[2], corners[3], corners[0]);
            var up = Vector3.up * house.WallHeight;
            Handles.DrawAAPolyLine(2f, corners[0] + up, corners[1] + up, corners[2] + up, corners[3] + up, corners[0] + up);
            // size handles on the +x and +z walls (the house grows both ways)
            foreach (var (axis, half) in new[] { (Vector3.right, hx), (Vector3.forward, hz) })
            {
                var at = W(axis * half);
                EditorGUI.BeginChangeCheck();
                var to = Handles.Slider(at, rot * axis, HandleUtility.GetHandleSize(at) * 0.08f, Handles.CubeHandleCap, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(house, "Size house");
                    float newHalf = Mathf.Max(1f, Vector3.Dot(to - t.position, rot * axis));
                    house.Size = axis == Vector3.right ? new Vector2(newHalf * 2f, house.Size.y) : new Vector2(house.Size.x, newHalf * 2f);
                    EditorUtility.SetDirty(house);
                }
            }
            // doors: drawn in their walls, slid along them
            for (int i = 0; i < house.Doors.Count; i++)
            {
                var door = house.Doors[i];
                var (la, lb) = house.WallEnds(door.wall);
                var a = W(la);
                var b = W(lb);
                var along = (b - a).normalized;
                var at = Vector3.Lerp(a, b, door.along) + Vector3.up * door.sill;
                Handles.color = PieceHandles.OpeningColor;
                var half = along * door.width * 0.5f;
                var h = Vector3.up * door.height;
                Handles.DrawAAPolyLine(3f, at - half, at - half + h, at + half + h, at + half, at - half);
                EditorGUI.BeginChangeCheck();
                var to = Handles.Slider(at + h * 0.5f, along, HandleUtility.GetHandleSize(at) * 0.07f, Handles.DotHandleCap, 0.05f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(house, "Move door");
                    door.along = Mathf.Clamp01(Vector3.Dot(to - a, along) / Mathf.Max(0.1f, Vector3.Distance(a, b)));
                    house.Doors[i] = door;
                    EditorUtility.SetDirty(house);
                }
            }
        }
    }

    // ------------------------------------------------------------------ trestle

    [CustomEditor(typeof(TrestleShape)), CanEditMultipleObjects]
    public sealed class TrestleShapeEditor : LevelPieceEditor
    {
        TrestleShape Trestle => (TrestleShape)target;

        protected override string Help => "The piece's position is the top of the tower; its legs reach down to the ground. Drag the yellow squares to size its head.";

        protected override void DrawScene()
        {
            var trestle = Trestle;
            var t = trestle.transform;
            var rot = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            var c = t.position;
            var h = trestle.Half;
            Handles.color = PieceHandles.Line;
            Handles.DrawAAPolyLine(3f, c + rot * new Vector3(-h.x, 0, -h.y), c + rot * new Vector3(h.x, 0, -h.y), c + rot * new Vector3(h.x, 0, h.y),
                                   c + rot * new Vector3(-h.x, 0, h.y), c + rot * new Vector3(-h.x, 0, -h.y));
            foreach (var axis in new[] { Vector3.right, Vector3.forward })
            {
                float half = axis == Vector3.right ? h.x : h.y;
                var at = c + rot * axis * half;
                EditorGUI.BeginChangeCheck();
                var to = Handles.Slider(at, rot * axis, HandleUtility.GetHandleSize(at) * 0.08f, Handles.CubeHandleCap, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(trestle, "Size trestle");
                    float v = Mathf.Max(0.4f, Vector3.Dot(to - c, rot * axis));
                    trestle.Half = axis == Vector3.right ? new Vector2(v, h.y) : new Vector2(h.x, v);
                    EditorUtility.SetDirty(trestle);
                }
            }
        }
    }
}
