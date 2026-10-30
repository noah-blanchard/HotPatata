using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The kit in an authored level's Scene view (ARCHITECTURE §27, docs/OBSTACLES.md):
    /// <list type="bullet">
    /// <item>size handles on the selected kit object (door, bridge, lift, zone, curtain, body screen, sun beam, plate): every part follows,
    /// through the kit's own resize functions (<see cref="BombObstacleKitBuilder"/>);</item>
    /// <item>an actuator's travel: its open position drawn as a ghost, moved with a handle;</item>
    /// <item>the wiring: a line from each source to what it drives (a switch to its targets), with how long it holds;</item>
    /// <item>the Link tool: an actuator's source (or a switch's target) picked with a click in the Scene view.</item>
    /// </list>
    /// One source drives one actuator (AGENTS: no AND/OR wiring); the tool never wires two sources to one actuator.
    /// </summary>
    [InitializeOnLoad]
    public static class KitSceneTools
    {
        const string WiringPref = "HotPatata.Level.ShowWiring";
        const string WiringMenu = "HotPatata/Level/Show Wiring";

        static readonly BoxBoundsHandle box = new BoxBoundsHandle();
        static SignalActuator linking;
        static bool linkingTarget;

        static KitSceneTools() => SceneView.duringSceneGui += OnScene;

        public static bool ShowWiring
        {
            get => EditorPrefs.GetBool(WiringPref, true);
            set => EditorPrefs.SetBool(WiringPref, value);
        }

        [MenuItem(WiringMenu, priority = 60)]
        static void ToggleWiring()
        {
            ShowWiring = !ShowWiring;
            SceneView.RepaintAll();
        }

        [MenuItem(WiringMenu, true)]
        static bool ToggleWiringCheck()
        {
            Menu.SetChecked(WiringMenu, ShowWiring);
            return true;
        }

        /// <summary>Starts the Link tool: the next click picks <paramref name="actuator"/>'s source (or, for a switch, adds a target).</summary>
        public static void BeginLink(SignalActuator actuator, bool target)
        {
            linking = actuator;
            linkingTarget = target;
            SceneView.lastActiveSceneView?.Focus();
            SceneView.RepaintAll();
        }

        public static bool IsLinking(SignalActuator actuator) => linking == actuator && actuator != null;

        static void OnScene(SceneView view)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (LevelScenes.SettingsOf(scene) == null) return;
            if (ShowWiring && Event.current.type == EventType.Repaint) DrawWiring(scene);
            if (linking != null) { LinkMode(); return; }
            var selected = Selection.activeGameObject;
            if (selected != null && !EditorApplication.isPlaying) Sizing(selected);
        }

        // ------------------------------------------------------------------ wiring

        /// <summary>What a source is and how long it holds ("hourglass plate, holds 6 s").</summary>
        public static string Describe(Object source)
        {
            switch (source)
            {
                case BombGate gate: return gate.HoldSeconds <= 0f ? "hoop, latched" : $"hoop, holds {gate.HoldSeconds:0.#} s";
                case PressurePlate plate:
                    string kind = plate.MemorySeconds > 0f ? "hourglass plate" : plate.RequiredBodies > 1 ? $"heavy plate ({plate.RequiredBodies} bodies)"
                        : plate.CountsCarrier ? "plate" : "hands-free plate";
                    return plate.MemorySeconds > 0f ? $"{kind}, holds {plate.MemorySeconds:0.#} s" : $"{kind}, while held";
                case SunBeam _: return "sun beam";
                default: return source != null ? source.name : "no source";
            }
        }

        static void DrawWiring(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var actuator in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SignalActuator>(true)))
            {
                var to = actuator.Platform != null ? actuator.Platform.position : actuator.transform.position;
                var source = actuator.Source as MonoBehaviour;
                if (source != null)
                {
                    var from = source.transform.position + Vector3.up * 0.4f;
                    Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                    Handles.DrawDottedLine(from, to, 5f);
                    Handles.ConeHandleCap(0, Vector3.Lerp(from, to, 0.85f), Quaternion.LookRotation((to - from).sqrMagnitude > 1e-4f ? to - from : Vector3.forward),
                                          HandleUtility.GetHandleSize(to) * 0.12f, EventType.Repaint);
                    PieceHandles.Label(Vector3.Lerp(from, to, 0.5f), Describe(source));
                }
                else PieceHandles.Label(to + Vector3.up, actuator.name + ": no source");
                if (actuator is SignalSwitch sw)
                {
                    var targets = new SerializedObject(sw).FindProperty("targets");
                    Handles.color = new Color(0.7f, 0.9f, 1f, 0.8f);
                    for (int i = 0; i < targets.arraySize; i++)
                        if (targets.GetArrayElementAtIndex(i).objectReferenceValue is GameObject t) Handles.DrawDottedLine(actuator.transform.position, t.transform.position, 3f);
                }
            }
        }

        // ------------------------------------------------------------------ link tool

        static void LinkMode()
        {
            var e = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Handles.BeginGUI();
            GUI.Label(new Rect(10, 10, 520, 22), linkingTarget ? $"Link: click what {linking.name} turns on and off (Esc to stop)."
                                                               : $"Link: click the plate, hoop or sun beam that drives {linking.name} (Esc to stop).", EditorStyles.helpBox);
            Handles.EndGUI();
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { linking = null; e.Use(); return; }
            if (e.type != EventType.MouseDown || e.button != 0 || e.alt) return;
            var picked = HandleUtility.PickGameObject(e.mousePosition, false);
            e.Use();
            if (picked == null) return;
            var so = new SerializedObject(linking);
            if (linkingTarget)
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(picked) ?? picked;
                var screen = root.GetComponentInChildren<BodyScreen>(true);
                var target = screen != null ? screen.gameObject : root;
                if (target == linking.gameObject || linking.transform.IsChildOf(target.transform)) return;
                var list = so.FindProperty("targets");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = target;
                Debug.Log($"[Level] {linking.name} now turns {target.name} on and off.");
            }
            else
            {
                var source = picked.GetComponentsInParent<MonoBehaviour>(true).FirstOrDefault(m => m is ISignalSource);
                if (source == null) { Debug.LogWarning($"[Level] {picked.name} is not a plate, a hoop or a sun beam."); return; }
                so.FindProperty("source").objectReferenceValue = source;
                Debug.Log($"[Level] {linking.name} is now driven by {source.name} ({Describe(source)}).");
            }
            so.ApplyModifiedProperties();
            linking = null;
        }

        // ------------------------------------------------------------------ sizing

        enum Kind { None, Actuator, Zone, Curtain, Screen, Beam, Plate }

        static Kind KindOf(GameObject root, out string note)
        {
            note = null;
            var actuator = root.GetComponent<SignalActuator>();
            if (actuator != null && !(actuator is SignalSwitch))
            {
                if (actuator.Platform == null) return Kind.None;
                var so = new SerializedObject(actuator);
                if (so.FindProperty("rotateWithWaypoints").boolValue) { note = "it turns on a hinge: size it in its inspector"; return Kind.None; }
                return Kind.Actuator;
            }
            if (root.GetComponent<PressurePlate>() != null) return Kind.Plate;
            if (root.GetComponent<SunBeam>() != null) return Kind.Beam;
            if (root.GetComponentInChildren<BodyScreen>(true) != null) return Kind.Screen;
            string path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(root));
            if (path.EndsWith("LaserCurtain.prefab")) return Kind.Curtain;
            if (root.GetComponent<Zone>() != null && root.GetComponent<BoxCollider>() != null) return Kind.Zone;
            return Kind.None;
        }

        static void Sizing(GameObject selected)
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(selected);
            if (root == null) return;
            var kind = KindOf(root, out string note);
            var t = root.transform;
            if (kind == Kind.None)
            {
                if (note != null) PieceHandles.Label(t.position + Vector3.up * 2f, note);
                return;
            }
            Handles.color = PieceHandles.Line;
            switch (kind)
            {
                case Kind.Actuator: SizeActuator(root); break;
                case Kind.Zone:
                {
                    var collider = root.GetComponent<BoxCollider>();
                    if (BoxHandle(t, collider.size, new Vector3(0f, collider.size.y / 2f, 0f), out var size, out var shift))
                        Resize(root, "Size zone", () => { BombObstacleKitBuilder.ResizeZone(root, size); t.position += t.rotation * new Vector3(shift.x, 0f, shift.z); });
                    break;
                }
                case Kind.Curtain:
                {
                    var collider = root.GetComponent<BoxCollider>();
                    if (BoxHandle(t, collider.size, new Vector3(0f, collider.size.y / 2f, 0f), out var size, out var shift))
                        Resize(root, "Size curtain", () => { BombObstacleKitBuilder.ResizeCurtain(root, size.x, size.y); t.position += t.rotation * new Vector3(shift.x, 0f, 0f); });
                    break;
                }
                case Kind.Screen:
                {
                    var collision = root.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault(c => c.gameObject.layer == CourseKit.Layer(BodyScreen.LayerName));
                    if (collision == null) break;
                    if (BoxHandle(t, collision.size, new Vector3(0f, collision.size.y / 2f, 0f), out var size, out var shift))
                        Resize(root, "Size screen", () => { BombObstacleKitBuilder.ResizeScreen(root, size.x, size.y); t.position += t.rotation * new Vector3(shift.x, 0f, 0f); });
                    break;
                }
                case Kind.Beam:
                {
                    float length = root.GetComponent<BoxCollider>().size.z;
                    var end = t.position + t.forward * length;
                    EditorGUI.BeginChangeCheck();
                    var moved = Handles.Slider(end, t.forward, HandleUtility.GetHandleSize(end) * 0.1f, Handles.CubeHandleCap, 0.1f);
                    if (EditorGUI.EndChangeCheck())
                        Resize(root, "Size sun beam", () => BombObstacleKitBuilder.ResizeSunBeam(root, Mathf.Max(1f, Vector3.Dot(moved - t.position, t.forward))));
                    break;
                }
                case Kind.Plate:
                {
                    float half = root.GetComponent<BoxCollider>().size.x / 2f;
                    var edge = t.position + t.right * half;
                    EditorGUI.BeginChangeCheck();
                    var moved = Handles.Slider(edge, t.right, HandleUtility.GetHandleSize(edge) * 0.08f, Handles.CubeHandleCap, 0.05f);
                    if (EditorGUI.EndChangeCheck())
                        Resize(root, "Size plate", () => BombObstacleKitBuilder.ResizePlate(root, Mathf.Clamp(2f * Vector3.Dot(moved - t.position, t.right), 0.8f, 8f)));
                    break;
                }
            }
        }

        /// <summary>A box handle in <paramref name="t"/>'s frame: true when dragged, with the new size and how far its centre moved.</summary>
        static bool BoxHandle(Transform t, Vector3 size, Vector3 center, out Vector3 newSize, out Vector3 shift)
        {
            using (new Handles.DrawingScope(PieceHandles.Line, Matrix4x4.TRS(t.position, t.rotation, Vector3.one)))
            {
                box.center = center;
                box.size = size;
                EditorGUI.BeginChangeCheck();
                box.DrawHandle();
                bool changed = EditorGUI.EndChangeCheck();
                newSize = Vector3.Max(box.size, new Vector3(0.3f, 0.3f, 0.3f));
                shift = box.center - center;
                return changed;
            }
        }

        static void Resize(GameObject root, string what, System.Action resize)
        {
            Undo.RegisterFullObjectHierarchyUndo(root, what);
            var kind = KindOf(root, out _);
            var before = Measure(root, kind);
            resize();
            // a hand-made visual (a forest look, docs/OBSTACLES.md §4.1) follows the new size: the kit's resize only knows its own parts
            var after = Measure(root, kind);
            if (before.HasValue && after.HasValue) Refit(root, kind, before.Value, after.Value);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        /// <summary>The size the kit's resize writes (what a hand-made visual is scaled by), in the frame its visual is drawn in.</summary>
        static Vector3? Measure(GameObject root, Kind kind)
        {
            switch (kind)
            {
                case Kind.Actuator:
                    var platform = root.GetComponent<SignalActuator>().Platform;
                    var box = platform != null ? platform.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault(c => !c.isTrigger) : null;
                    return box != null ? new Vector3(box.size.x, 1f, box.size.z) : (Vector3?)null;   // a deck's thickness never stretches its posts
                case Kind.Zone:
                case Kind.Curtain:
                    return root.GetComponent<BoxCollider>().size;
                case Kind.Screen:
                    var screen = root.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault(c => c.gameObject.layer == CourseKit.Layer(BodyScreen.LayerName));
                    return screen != null ? new Vector3(screen.size.x, screen.size.y, 1f) : (Vector3?)null;
                case Kind.Plate:
                    var plate = root.GetComponent<BoxCollider>().size;
                    return new Vector3(plate.x, 1f, plate.z);
                default:
                    return null;
            }
        }

        /// <summary>Scales every outermost <see cref="CustomVisual"/> of <paramref name="root"/> by the change of size.</summary>
        static void Refit(GameObject root, Kind kind, Vector3 before, Vector3 after)
        {
            Vector3 ratio = new Vector3(Ratio(after.x, before.x), Ratio(after.y, before.y), Ratio(after.z, before.z));
            if ((ratio - Vector3.one).sqrMagnitude < 1e-8f) return;
            foreach (var visual in root.GetComponentsInChildren<CustomVisual>(true))
            {
                if (visual.transform.parent != null && visual.transform.parent.GetComponentInParent<CustomVisual>(true) != null) continue;
                // a body screen's frame keeps its depth; its net and the curtain's sparks scale in the wall's plane
                visual.transform.localScale = Vector3.Scale(visual.transform.localScale, ratio);
            }
        }

        static float Ratio(float after, float before) => before > 1e-4f ? after / before : 1f;

        /// <summary>A door, bridge or lift: its platform's box (closed, where it stands) and its open position as a ghost with a handle.</summary>
        static void SizeActuator(GameObject root)
        {
            var actuator = root.GetComponent<SignalActuator>();
            var t = root.transform;
            var collision = actuator.Platform.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault(c => !c.isTrigger);
            if (collision == null) return;
            var size = collision.size;
            var travel = actuator.WaypointOpen.localPosition - actuator.WaypointClosed.localPosition;
            if (BoxHandle(t, size, Vector3.zero, out var newSize, out var shift))
                Resize(root, "Size actuator", () =>
                {
                    BombObstacleKitBuilder.ResizeActuator(root, newSize, travel);
                    t.position += t.rotation * shift;
                });
            // the open position: a ghost box and a handle at its centre
            var open = t.TransformPoint(travel);
            using (new Handles.DrawingScope(new Color(0.4f, 0.9f, 1f, 0.8f), Matrix4x4.TRS(open, t.rotation, Vector3.one)))
                Handles.DrawWireCube(Vector3.zero, size);
            Handles.color = new Color(0.4f, 0.9f, 1f);
            Handles.DrawDottedLine(t.position, open, 4f);
            PieceHandles.Label(open + Vector3.up * (size.y * 0.5f + 0.4f), $"open ({actuator.TravelSeconds:0.#} s)");
            EditorGUI.BeginChangeCheck();
            var moved = Handles.FreeMoveHandle(open, HandleUtility.GetHandleSize(open) * 0.1f, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
                Resize(root, "Move open position", () => BombObstacleKitBuilder.ResizeActuator(root, size, t.InverseTransformPoint(moved)));
        }
    }

    /// <summary>An actuator's inspector in an authored level: its source picked in the Scene view (the Link tool), a switch's targets added.</summary>
    [CustomEditor(typeof(SignalActuator), true), CanEditMultipleObjects]
    public sealed class SignalActuatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var actuator = (SignalActuator)target;
            if (LevelScenes.SettingsOf(actuator.gameObject.scene) == null) return;
            EditorGUILayout.Space();
            var source = actuator.Source as MonoBehaviour;
            EditorGUILayout.LabelField("Driven by", source != null ? $"{source.name} ({KitSceneTools.Describe(source)})" : "nothing yet", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool linking = KitSceneTools.IsLinking(actuator);
                if (GUILayout.Button(linking ? "Click in the Scene view..." : "Link: pick its source")) KitSceneTools.BeginLink(actuator, false);
                if (actuator is SignalSwitch && GUILayout.Button("Link: add a target")) KitSceneTools.BeginLink(actuator, true);
            }
            EditorGUILayout.HelpBox("One source drives one actuator (no AND, no OR). Scene view: drag the box to size it, the blue ball to move where it opens to.",
                                    MessageType.None);
        }
    }
}
