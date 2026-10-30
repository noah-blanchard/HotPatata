using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The ground's tools in the Scene view and the palette's Ground tab (ARCHITECTURE §27.4, docs/LEVEL_AUTHORING.md §26): bake the rules,
    /// resample the terrain, and the Reset Painted brush (click and drag: the texels under it go back to the rules; Shift+wheel sizes it,
    /// Esc stops).
    /// </summary>
    [InitializeOnLoad]
    public static class GroundTools
    {
        static bool resetBrush;
        static float radius = 6f;

        public static bool ResetBrush
        {
            get => resetBrush;
            set
            {
                resetBrush = value;
                SceneView.RepaintAll();
            }
        }

        static GroundTools() => SceneView.duringSceneGui += OnScene;

        [PaletteItem("ground.bake", "Ground/Tools", "Bake Ground Rules", Kind = PaletteKind.Tool, Icon = "d_Refresh",
                     Tooltip = "Paints the layered ground from its rules (slope, height, hollows, water, walk lines, rock); what you painted by hand stays.")]
        static GameObject Bake(PlaceContext c)
        {
            int n = 0, kept = 0;
            foreach (var t in LevelTerrain.Terrains(c.Scene).Where(GroundBaker.IsLayered))
            {
                kept += GroundBaker.Bake(t);
                n++;
            }
            Debug.Log(n == 0 ? "[Ground] no layered terrain here (Level inspector: Ground = Layered)." : $"[Ground] rules baked; {kept} painted texel(s) kept.");
            return null;
        }

        [PaletteItem("ground.reset-painted", "Ground/Tools", "Reset Painted (brush)", Kind = PaletteKind.Tool, Icon = "d_TerrainInspector.TerrainToolSplat",
                     Tooltip = "A brush: the ground under it goes back to the rules. Shift+wheel sizes it, Esc stops.")]
        static GameObject ResetPaintedBrush(PlaceContext c)
        {
            ResetBrush = !ResetBrush;
            return null;
        }

        [PaletteItem("ground.resample", "Ground/Tools", "Resample Terrain", Kind = PaletteKind.Tool, Icon = "d_TerrainInspector.TerrainToolSettings",
                     Tooltip = "The terrain at another size (256 to 1536 m) or fineness (1 m or 0.5 m a sample), its relief kept.")]
        static GameObject Resample(PlaceContext c)
        {
            ResampleTerrainWindow.Open();
            return null;
        }

        static void OnScene(SceneView view)
        {
            if (!resetBrush) return;
            var e = Event.current;
            var scene = EditorSceneManager.GetActiveScene();
            var terrain = LevelTerrain.Terrains(scene).FirstOrDefault(GroundBaker.IsLayered);
            if (terrain == null) return;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                ResetBrush = false;
                e.Use();
                return;
            }
            if (e.type == EventType.ScrollWheel && e.shift)
            {
                radius = Mathf.Clamp(radius * (e.delta.y > 0 ? 0.9f : 1.1f), 1f, 60f);
                e.Use();
            }
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!terrain.GetComponent<TerrainCollider>().Raycast(ray, out var hit, 5000f)) return;
            Handles.color = new Color(1f, 0.45f, 0.3f);
            Handles.DrawWireDisc(hit.point, Vector3.up, radius, 2f);
            Handles.Label(hit.point + Vector3.up * 1.5f, "Reset painted");
            if (e.type == EventType.MouseMove) view.Repaint();
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && !e.alt)
            {
                GroundBaker.ResetPainted(terrain, hit.point, radius);
                e.Use();
            }
        }
    }
}
