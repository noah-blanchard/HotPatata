using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>The inspectors and handles of the rock and mine pieces (ARCHITECTURE §27.2, docs/LEVEL_AUTHORING.md §20 to §23).</summary>
    [CustomEditor(typeof(RockShellShape)), CanEditMultipleObjects]
    public sealed class RockShellShapeEditor : LevelPieceEditor
    {
        RockShellShape Shell => (RockShellShape)target;

        protected override string Help => Shell.Kind == RockShellKind.Tunnel
            ? "A tunnel: its dots are the floor's centre line (click a dot to move it, Ctrl+click to delete it, a + to add one). Its walls and roof stop " +
              "players and the bomb, and the roof is a ceiling (nothing is lobbed over it). Cyan discs are skylights: drag their dot."
            : "A cavern: its dots are its outline on the floor (keep it round: the dome rises from it to its middle). Leave a tunnel's mouth inside " +
              "its wall to open a way in. Cyan discs are skylights: drag their dot.";

        protected override void DrawTools()
        {
            PencilButton(Shell.Points, Shell.Kind == RockShellKind.Cavern);
            if (GUILayout.Button("Add skylight (over the middle)"))
            {
                Undo.RecordObject(Shell, "Add skylight");
                var mid = Shell.Points.Count > 0 ? Shell.Points.Aggregate(Vector3.zero, (a, p) => a + p) / Shell.Points.Count : Vector3.zero;
                Shell.Skylights.Add(new RockSkylight { at = new Vector2(mid.x, mid.z), radius = 1.5f });
                EditorUtility.SetDirty(Shell);
            }
        }

        protected override void DrawScene()
        {
            var shell = Shell;
            bool cavern = shell.Kind == RockShellKind.Cavern;
            if (!cavern && shell.Points.Count >= 2)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.5f);
                var line = Curves.Spline(shell.WorldPoints(), 0.5f);
                Handles.DrawAAPolyLine(2f, line.ToArray());
                // the clear space: its crown line
                Handles.color = new Color(1f, 0.82f, 0.25f, 0.35f);
                Handles.DrawAAPolyLine(1.5f, line.Select(p => p + Vector3.up * shell.Height).ToArray());
            }
            PieceHandles.EditPoints(shell, shell.Points, cavern, cavern, cavern ? 3 : 2);
            float top = shell.transform.position.y + shell.Height;
            for (int i = 0; i < shell.Skylights.Count; i++)
            {
                var sky = shell.Skylights[i];
                var at = shell.transform.TransformPoint(new Vector3(sky.at.x, 0f, sky.at.y));
                at.y = top;
                Handles.color = PieceHandles.OpeningColor;
                Handles.DrawWireDisc(at, Vector3.up, sky.radius);
                EditorGUI.BeginChangeCheck();
                var to = Handles.FreeMoveHandle(at, HandleUtility.GetHandleSize(at) * 0.07f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(shell, "Move skylight");
                    var local = shell.transform.InverseTransformPoint(to);
                    sky.at = new Vector2(local.x, local.z);
                    shell.Skylights[i] = sky;
                    EditorUtility.SetDirty(shell);
                }
            }
        }
    }

    [CustomEditor(typeof(CliffShape)), CanEditMultipleObjects]
    public sealed class CliffShapeEditor : LevelPieceEditor
    {
        CliffShape Cliff => (CliffShape)target;

        protected override string Help =>
            "A cliff: its dots are its outline at the foot (the piece's height); it rises Height, stepping back at each ledge. Its top and shelves are walked " +
            "on. Faces under 3.4 m are climbed by a jump and a mantle.";

        protected override void DrawTools()
        {
            var faces = Cliff.Faces();
            string list = string.Join(", ", faces.Select(f => f.ToString("F1") + " m"));
            bool climbable = faces.Any(f => f < CliffShape.LockedFace);
            EditorGUILayout.HelpBox($"Faces from the foot: {list}." + (climbable ? $" A face under {CliffShape.LockedFace} m is climbed (a jump and a mantle)." : " Every face locks a climb (declare it in the section's contract)."),
                                    climbable ? MessageType.Info : MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rectangle 12 × 4")) SetOutline(DeckShapes.Rectangle(12f, 4f));
                if (GUILayout.Button("Oval 10 × 8")) SetOutline(DeckShapes.Oval(10f, 8f, 18));
            }
            PencilButton(Cliff.Outline, true);
        }

        void SetOutline(System.Collections.Generic.List<Vector3> local)
        {
            Undo.RecordObject(Cliff, "Cliff shape");
            Cliff.Outline.Clear();
            Cliff.Outline.AddRange(local);
            EditorUtility.SetDirty(Cliff);
        }

        protected override void DrawScene()
        {
            var cliff = Cliff;
            PieceHandles.EditPoints(cliff, cliff.Outline, true, true, 3);
            var outline = cliff.WorldOutline();
            if (outline.Count < 3) return;
            float y = cliff.transform.position.y + cliff.Height;
            Handles.color = new Color(1f, 0.82f, 0.25f, 0.5f);
            Handles.DrawAAPolyLine(2f, outline.Select(p => new Vector3(p.x, y, p.y)).Append(new Vector3(outline[0].x, y, outline[0].y)).ToArray());
            // the top's height handle
            var c = cliff.transform.position + Vector3.up * cliff.Height;
            EditorGUI.BeginChangeCheck();
            var to = Handles.Slider(c, Vector3.up, HandleUtility.GetHandleSize(c) * 0.08f, Handles.CubeHandleCap, 0.1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(cliff, "Cliff height");
                cliff.Height = to.y - cliff.transform.position.y;
                EditorUtility.SetDirty(cliff);
            }
            PieceHandles.Label(c + Vector3.up * 0.5f, $"{cliff.Height:F1} m");
        }
    }

    [CustomEditor(typeof(MineFrameShape)), CanEditMultipleObjects]
    public sealed class MineFrameShapeEditor : LevelPieceEditor
    {
        MineFrameShape Frames => (MineFrameShape)target;

        protected override string Help => "Mine frames: their dots are the floor's line through the gallery. Each frame stands on the ground under it; its posts stop players and the bomb.";

        protected override void DrawTools() => PencilButton(Frames.Points, false);

        protected override void DrawScene() => PieceHandles.EditPoints(Frames, Frames.Points, false, false, 2);
    }

    [CustomEditor(typeof(MineRailShape)), CanEditMultipleObjects]
    public sealed class MineRailShapeEditor : LevelPieceEditor
    {
        MineRailShape Rails => (MineRailShape)target;

        protected override string Help => "A mine track: its dots are the track's line; it lies on the ground under it. Decoration (walked over).";

        protected override void DrawTools() => PencilButton(Rails.Points, false);

        protected override void DrawScene() => PieceHandles.EditPoints(Rails, Rails.Points, false, false, 2);
    }

    [CustomEditor(typeof(LaneMarkerShape)), CanEditMultipleObjects]
    public sealed class LaneMarkerShapeEditor : StructureShapeEditor
    {
        protected override string Help => "A lane marker: the Sun (gold) or the Moon (blue) lane's glyph, ribbons and lantern, facing the piece's forward (the arrow). " +
                                           "A post beside the path, or an arch over it. Put one where each lane starts.";
    }
}
