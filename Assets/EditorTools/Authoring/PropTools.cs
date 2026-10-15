using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>A prop's inspector (ARCHITECTURE §27): Solid gives it a box round what it draws (on Environment), or takes it away.</summary>
    [CustomEditor(typeof(PropSolid)), CanEditMultipleObjects]
    public sealed class PropSolidEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            bool solid = targets.Cast<PropSolid>().All(p => p.Solid);
            EditorGUI.showMixedValue = targets.Cast<PropSolid>().Select(p => p.Solid).Distinct().Count() > 1;
            EditorGUI.BeginChangeCheck();
            bool wanted = EditorGUILayout.Toggle(new GUIContent("Solid", "A box round the prop that players and the bomb collide with. Off: decoration (walk through it)."), solid);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                foreach (var prop in targets.Cast<PropSolid>()) SetSolid(prop, wanted);
            EditorGUILayout.HelpBox("A prop that looks walkable or in the way should be solid; small things (bowls, stones, plants) stay decoration.", MessageType.None);
        }

        /// <summary>Makes <paramref name="prop"/> solid (a box child on Environment fitted to its renderers) or decoration.</summary>
        public static void SetSolid(PropSolid prop, bool solid, bool undo = true)
        {
            if (undo) Undo.RecordObject(prop, solid ? "Solid prop" : "Decoration prop");
            if (prop.Box != null)
            {
                if (undo) Undo.DestroyObjectImmediate(prop.Box.gameObject);
                else Object.DestroyImmediate(prop.Box.gameObject);
            }
            BoxCollider box = null;
            if (solid)
            {
                var renderers = prop.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var t = prop.transform;
                    var min = Vector3.positiveInfinity;
                    var max = Vector3.negativeInfinity;
                    foreach (var r in renderers)
                    {
                        var b = r.bounds;
                        for (int c = 0; c < 8; c++)
                        {
                            var corner = t.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                            min = Vector3.Min(min, corner);
                            max = Vector3.Max(max, corner);
                        }
                    }
                    var go = new GameObject("Solid") { layer = CourseKit.Layer("Environment") };
                    if (undo) Undo.RegisterCreatedObjectUndo(go, "Solid prop");
                    go.transform.SetParent(t, false);
                    box = go.AddComponent<BoxCollider>();
                    box.center = (min + max) / 2f;
                    box.size = max - min;
                }
            }
            prop.Configure(solid && box != null, box);
            EditorUtility.SetDirty(prop);
            if (undo && prop.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(prop.gameObject.scene);
        }
    }

    /// <summary>
    /// HotPatata/Level/Props Brush (ARCHITECTURE §27): paints small props on the ground and the decks (pebbles, twigs, roots, moss),
    /// turned and sized at random, never on a walkway or a bridge's line. Click and drag to paint, Shift to erase what was painted.
    /// They go in Level/Props, as library prefabs (decoration); the eraser only takes the brushes' props.
    /// </summary>
    public sealed class PropBrushWindow : EditorWindow
    {
        /// <summary>What a brush paints: library props and their size range.</summary>
        static readonly (string name, string[] models, Vector2 scale)[] Brushes =
        {
            ("Pebbles", new[] { "stone_01", "rock_09", "rock_07" }, new Vector2(0.8f, 2.2f)),
            ("Twigs", new[] { "dry_branches_medium_01" }, new Vector2(0.35f, 0.7f)),
            ("Roots", new[] { "single_root", "root_cluster_02" }, new Vector2(0.4f, 0.9f)),
            ("Moss", new[] { "moss_01" }, new Vector2(3f, 7f)),
            ("Ferns", new[] { "fern_02" }, new Vector2(0.4f, 0.8f)),
        };

        int brush;
        float radius = 3f, density = 0.6f;
        bool painting;
        Vector3 lastDab;
        System.Random rng = new System.Random();

        [MenuItem("HotPatata/Level/Props Brush", priority = 61)]
        static void Open() => GetWindow<PropBrushWindow>(false, "Props Brush");

        void OnEnable() => SceneView.duringSceneGui += OnScene;

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnScene;
            painting = false;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Paint small props on the ground and the decks: click and drag in the Scene view. Shift: erase what was painted. " +
                                    "Never on a walkway or a bridge.", MessageType.None);
            brush = GUILayout.SelectionGrid(brush, Brushes.Select(b => b.name).ToArray(), 3);
            radius = EditorGUILayout.Slider("Radius (m)", radius, 0.5f, 10f);
            density = EditorGUILayout.Slider("Per square metre", density, 0.05f, 3f);
            var label = painting ? "Stop painting" : "Paint";
            if (GUILayout.Button(label))
            {
                painting = !painting;
                SceneView.RepaintAll();
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PropLibraryBuilder.PrefabPath(Brushes[brush].models[0])) == null)
                EditorGUILayout.HelpBox("Build the prop library first (HotPatata/Level/Build Prop Library).", MessageType.Warning);
        }

        void OnScene(SceneView view)
        {
            if (!painting) return;
            var e = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!Ground(ray, out var hit)) return;
            Handles.color = e.shift ? new Color(1f, 0.4f, 0.3f) : new Color(0.5f, 1f, 0.5f);
            Handles.DrawWireDisc(hit.point, hit.normal, radius, 2f);
            if (e.type == EventType.MouseMove) view.Repaint();
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { painting = false; Repaint(); e.Use(); return; }
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && !e.alt)
            {
                if (e.type == EventType.MouseDown || Vector3.Distance(lastDab, hit.point) > radius * 0.5f)
                {
                    if (e.shift) Erase(hit.point);
                    else Dab(hit.point);
                    lastDab = hit.point;
                }
                e.Use();
            }
        }

        /// <summary>The ground or a deck under the mouse (never a prop, a tree or the kit).</summary>
        static bool Ground(Ray ray, out RaycastHit best)
        {
            best = default;
            float d = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(ray, 1000f, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= d || hit.collider.GetComponentInParent<PropSolid>() != null) continue;
                var proxy = hit.collider.GetComponent<WalkableProxy>();
                bool ground = hit.collider is TerrainCollider || (proxy != null && (proxy.ProxyKind == WalkableProxy.Kind.Deck || proxy.ProxyKind == WalkableProxy.Kind.Terrain));
                if (!ground) continue;
                d = hit.distance;
                best = hit;
            }
            return d < float.MaxValue;
        }

        static Transform Group()
        {
            var settings = LevelScenes.SettingsOf(EditorSceneManager.GetActiveScene());
            return settings != null ? settings.Group(LevelGroup.Props) : null;
        }

        /// <summary>The library prefabs any brush paints (what the eraser may take).</summary>
        static HashSet<GameObject> BrushPrefabs() =>
            new HashSet<GameObject>(Brushes.SelectMany(b => b.models).Select(m => AssetDatabase.LoadAssetAtPath<GameObject>(PropLibraryBuilder.PrefabPath(m))).Where(p => p != null));

        void Dab(Vector3 center)
        {
            var (_, models, scale) = Brushes[brush];
            var prefabs = models.Select(m => AssetDatabase.LoadAssetAtPath<GameObject>(PropLibraryBuilder.PrefabPath(m))).Where(p => p != null).ToArray();
            if (prefabs.Length == 0) return;
            var scene = EditorSceneManager.GetActiveScene();
            var paths = LevelForest.Paths(scene).Where(s => (s.a - new Vector2(center.x, center.z)).magnitude < radius + 30f).ToList();
            var group = Group();
            int count = Mathf.Max(1, Mathf.RoundToInt(Mathf.PI * radius * radius * density * 0.5f));
            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                var at = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!Ground(new Ray(at + Vector3.up * 3f, Vector3.down), out var hit)) continue;
                if (paths.Count > 0 && LevelForest.Distance(paths, new Vector2(hit.point.x, hit.point.z)) < 1f) continue;   // never on a walk
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[rng.Next(prefabs.Length)], group);
                Undo.RegisterCreatedObjectUndo(go, "Paint props");
                go.transform.position = hit.point;
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * Mathf.Lerp(scale.x, scale.y, (float)rng.NextDouble());
            }
        }

        void Erase(Vector3 center)
        {
            var group = Group();
            if (group == null) return;
            var painted = BrushPrefabs();
            foreach (var t in group.Cast<Transform>().ToArray())
                if (painted.Contains(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)) &&
                    new Vector2(t.position.x - center.x, t.position.z - center.z).magnitude < radius)
                    Undo.DestroyObjectImmediate(t.gameObject);
        }
    }
}
