using System.Collections.Generic;
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

        protected override string Help => Shell.Kind switch
        {
            RockShellKind.Tunnel => "A tunnel: its dots are the floor's centre line (click a dot to move it, Ctrl+click to delete it, a + to add one). Its walls and roof stop " +
                                    "players and the bomb, and the roof is a ceiling (nothing is lobbed over it). Cyan discs are skylights: drag their dot. " +
                                    "With sections, each dot has its own width (cyan arrows) and height (white arrow), blended between dots.",
            RockShellKind.Shaft => "A shaft: its dots are its outline on the floor; its walls rise Height. The way up is the author's: a Mine Stair, a walkway " +
                                   "spiralling on posts, or a lift (never a ladder). A throw up a shaft is a pass like any other: with its top closed, Verify's " +
                                   "ceiling check applies to its roof.",
            _ => "A cavern: its dots are its outline on the floor (keep it round: the dome rises from it to its crown). With an Apex Height, Height is its walls' " +
                 "and the white dot moves its crown. Leave a tunnel's mouth inside its wall to open a way in. Cyan discs are skylights, orange ones pillars: drag their dot.",
        };

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
            var shell = Shell;
            if (shell.Kind == RockShellKind.Tunnel)
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(shell.Sections.Count == 0 ? "Sections at every dot" : "Match sections to dots"))
                    {
                        Undo.RecordObject(shell, "Sections");
                        while (shell.Sections.Count < shell.Points.Count)
                            shell.Sections.Add(shell.Sections.Count > 0 ? shell.Sections[shell.Sections.Count - 1] : new CaveSection(shell.Width, shell.Height, CaveProfile.Round));
                        while (shell.Sections.Count > shell.Points.Count) shell.Sections.RemoveAt(shell.Sections.Count - 1);
                        EditorUtility.SetDirty(shell);
                    }
                    if (shell.Sections.Count > 0 && GUILayout.Button("Clear sections"))
                    {
                        Undo.RecordObject(shell, "Sections");
                        shell.Sections.Clear();
                        EditorUtility.SetDirty(shell);
                    }
                }
            if (shell.Kind == RockShellKind.Cavern)
                using (new EditorGUILayout.HorizontalScope())
                {
                    var middle = shell.Points.Count > 0 ? shell.Points.Aggregate(Vector3.zero, (a, p) => a + p) / shell.Points.Count : Vector3.zero;
                    if (GUILayout.Button("Add pillar"))
                    {
                        Undo.RecordObject(shell, "Add pillar");
                        shell.Pillars.Add(new CavePillar { at = new Vector2(middle.x + 3f, middle.z), radius = 1.2f, waist = 0.6f });
                        EditorUtility.SetDirty(shell);
                    }
                    if (GUILayout.Button("Add alcove"))
                    {
                        Undo.RecordObject(shell, "Add alcove");
                        shell.Alcoves.Add(new CaveAlcove { along = 0.25f * shell.Alcoves.Count, width = 6f, depth = 3f });
                        EditorUtility.SetDirty(shell);
                    }
                    if (GUILayout.Button("Add shelf"))
                    {
                        Undo.RecordObject(shell, "Add shelf");
                        shell.Floors.Add(new CaveFloor
                        {
                            outline = new List<Vector2> { new Vector2(middle.x, middle.z), new Vector2(middle.x + 6f, middle.z), new Vector2(middle.x + 6f, middle.z + 5f), new Vector2(middle.x, middle.z + 5f) },
                            height = 3f, thickness = 0.8f,
                        });
                        EditorUtility.SetDirty(shell);
                    }
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
            bool closed = shell.Kind != RockShellKind.Tunnel;
            PieceHandles.EditPoints(shell, shell.Points, closed, closed, closed ? 3 : 2);
            if (shell.Kind == RockShellKind.Tunnel && shell.Sections.Count > 0) SectionHandles(shell);
            if (cavern) CavernHandles(shell);
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

        /// <summary>Each dot's section: its width (cyan arrows, each side) and height (white arrow on top).</summary>
        static void SectionHandles(RockShellShape shell)
        {
            var world = shell.WorldPoints();
            for (int i = 0; i < world.Count && i < shell.Sections.Count; i++)
            {
                var sec = shell.Sections[i];
                var t = world[Mathf.Min(world.Count - 1, i + 1)] - world[Mathf.Max(0, i - 1)];
                t.y = 0f;
                if (t.sqrMagnitude < 1e-6f) continue;
                var right = Vector3.Cross(Vector3.up, t.normalized);
                float size = HandleUtility.GetHandleSize(world[i]) * 0.08f;
                EditorGUI.BeginChangeCheck();
                Handles.color = PieceHandles.OpeningColor;
                var r = Handles.Slider(world[i] + right * sec.width * 0.5f, right, size, Handles.ConeHandleCap, 0.1f);
                var l = Handles.Slider(world[i] - right * sec.width * 0.5f, -right, size, Handles.ConeHandleCap, 0.1f);
                Handles.color = Color.white;
                var top = Handles.Slider(world[i] + Vector3.up * sec.height, Vector3.up, size, Handles.ConeHandleCap, 0.1f);
                Handles.Label(world[i] + Vector3.up * (sec.height + 0.6f), sec.profile.ToString());
                if (!EditorGUI.EndChangeCheck()) continue;
                Undo.RecordObject(shell, "Tunnel section");
                float rw = Vector3.Dot(r - world[i], right) * 2f, lw = Vector3.Dot(l - world[i], -right) * 2f;
                sec.width = Mathf.Max(1.5f, Mathf.Abs(rw - sec.width) > Mathf.Abs(lw - sec.width) ? rw : lw);
                sec.height = Mathf.Max(2f, top.y - world[i].y);
                shell.Sections[i] = sec;
                EditorUtility.SetDirty(shell);
            }
        }

        /// <summary>A cavern's crown (white dot) and its pillars (orange discs).</summary>
        static void CavernHandles(RockShellShape shell)
        {
            var w = shell.WorldPoints();
            if (w.Count < 3) return;
            var middle = new Vector3(w.Average(p => p.x), shell.transform.position.y, w.Average(p => p.z));
            if (shell.ApexHeight > 0f)
            {
                var apex = middle + shell.transform.TransformVector(new Vector3(shell.Apex.x, 0f, shell.Apex.y)) + Vector3.up * shell.ApexHeight;
                Handles.color = Color.white;
                EditorGUI.BeginChangeCheck();
                var to = Handles.FreeMoveHandle(apex, HandleUtility.GetHandleSize(apex) * 0.09f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(shell, "Move crown");
                    var local = shell.transform.InverseTransformVector(to - apex);
                    shell.Apex += new Vector2(local.x, local.z);
                    EditorUtility.SetDirty(shell);
                }
            }
            for (int i = 0; i < shell.Pillars.Count; i++)
            {
                var pillar = shell.Pillars[i];
                var at = shell.transform.TransformPoint(new Vector3(pillar.at.x, 0f, pillar.at.y));
                Handles.color = new Color(1f, 0.55f, 0.2f, 0.9f);
                Handles.DrawWireDisc(at, Vector3.up, pillar.radius);
                EditorGUI.BeginChangeCheck();
                var to = Handles.FreeMoveHandle(at, HandleUtility.GetHandleSize(at) * 0.07f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(shell, "Move pillar");
                    var local = shell.transform.InverseTransformPoint(to);
                    pillar.at = new Vector2(local.x, local.z);
                    shell.Pillars[i] = pillar;
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
