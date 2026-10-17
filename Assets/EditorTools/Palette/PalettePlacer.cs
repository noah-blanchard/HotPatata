using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// The palette in the Scene view (ARCHITECTURE §27.3, docs/LEVEL_AUTHORING.md §25): an armed entry follows the mouse as a ghost and a
    /// click places it on the surface under it (R or Ctrl+wheel turns it, Esc or a right click disarms it); a tile dragged from the palette
    /// is placed where it is dropped. In burst mode each click scatters copies, turned and sized at random, never on a walkway, a bridge's
    /// line, a pass arc or water; Shift+click erases what this burst session placed. Snapping (one toggle per kind, Alt held turns it off):
    /// the ground and floors, a deck's edge or a walkway's end (an attached end), and what later tools register (<see cref="Snappers"/>).
    /// </summary>
    [InitializeOnLoad]
    public static class PalettePlacer
    {
        public const string DragKey = "HotPatataPaletteEntry";
        /// <summary>How far from the mouse a deck edge or a piece's end catches a line piece (flat metres).</summary>
        public const float DeckEdgeReach = 1.5f;
        /// <summary>A burst copy keeps this far from a walk line or a deck's outline (m), and this far from a pass arc.</summary>
        public const float BurstPathClearance = 1f, BurstArcClearance = 2f;
        const string GhostName = "__PaletteGhost";

        /// <summary>Snapping kinds the author turned off (all on by default).</summary>
        public static PaletteSnap Disabled
        {
            get => (PaletteSnap)EditorPrefs.GetInt("HotPatata.Palette.SnapOff", 0);
            set => EditorPrefs.SetInt("HotPatata.Palette.SnapOff", (int)value);
        }

        /// <summary>The burst settings (the palette's strip).</summary>
        public sealed class BurstSettings
        {
            public int Count = 1;
            public float Radius = 0f;
            public Vector2 Scale = new Vector2(0.85f, 1.2f);
            public bool RandomYaw = true, AlignToNormal = true;
        }

        public static readonly BurstSettings Burst = new BurstSettings();

        /// <summary>
        /// Snap finders other tools add (a wall's end, a wall's opening): given the entry and the surface point under the mouse, the snap
        /// they find, or a snap of kind None.
        /// </summary>
        public static readonly List<(PaletteSnap kind, Func<PaletteEntry, Vector3, PlaceSnap> find)> Snappers = new List<(PaletteSnap, Func<PaletteEntry, Vector3, PlaceSnap>)>();

        public static PaletteEntry Armed { get; private set; }
        public static string ArmedLook { get; private set; }
        public static float Yaw { get; private set; }
        /// <summary>Raised when the armed entry changes (the palette redraws its strip).</summary>
        public static event Action Changed;

        static readonly List<GameObject> session = new List<GameObject>();
        static GameObject ghost;
        static string ghostKey;
        static bool painting;
        static Vector3 lastDab;
        static readonly Random rng = new Random();

        static PalettePlacer()
        {
            SceneView.duringSceneGui += OnScene;
            EditorApplication.delayCall += ClearGhosts;
            AssemblyReloadEvents.beforeAssemblyReload += Disarm;
            EditorSceneManager.sceneClosing += (_, _) => Disarm();
        }

        public static void Arm(PaletteEntry entry, string look)
        {
            Armed = entry;
            ArmedLook = look;
            session.Clear();
            DestroyGhost();
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        public static void Disarm()
        {
            if (Armed == null) return;
            Armed = null;
            painting = false;
            session.Clear();
            DestroyGhost();
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        /// <summary>The objects this burst session placed (what Shift+click may erase).</summary>
        public static IReadOnlyList<GameObject> Session => session;

        // ------------------------------------------------------------------ the Scene view

        static void OnScene(SceneView view)
        {
            var e = Event.current;
            if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
            {
                OnDrop(e);
                return;
            }
            if (Armed == null) return;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape || e.type == EventType.MouseDown && e.button == 1)
            {
                Disarm();
                e.Use();
                return;
            }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
            {
                Yaw = Mathf.Repeat(Yaw + (e.shift ? -45f : 45f), 360f);
                e.Use();
            }
            if (e.type == EventType.ScrollWheel && e.control)
            {
                Yaw = Mathf.Repeat(Yaw + Mathf.Sign(e.delta.y) * 15f, 360f);
                e.Use();
            }
            var scene = EditorSceneManager.GetActiveScene();
            if (!Surface(scene, HandleUtility.GUIPointToWorldRay(e.mousePosition), out var point, out var normal))
            {
                if (ghost != null) ghost.SetActive(false);
                return;
            }
            var snap = e.alt ? default : FindSnap(scene, Armed, point);
            var at = snap.Found ? snap.Point : point;
            DrawGhost(at, normal, snap, e.shift);
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) view.Repaint();

            bool burst = Armed.Burst;
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                if (burst)
                {
                    if (e.shift) Erase(at, Mathf.Max(1f, Burst.Radius));
                    else Dab(scene, Armed, at, normal);
                    painting = true;
                    lastDab = at;
                }
                else Place(Armed, ArmedLook, PlaceAt(at, normal, snap));
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && painting && burst && Burst.Radius > 0f)
            {
                if (Vector3.Distance(lastDab, at) > Burst.Radius * 0.5f)
                {
                    if (e.shift) Erase(at, Burst.Radius);
                    else Dab(scene, Armed, at, normal);
                    lastDab = at;
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0) painting = false;
        }

        static PlaceContext PlaceAt(Vector3 at, Vector3 normal, PlaceSnap snap)
        {
            var c = PlaceContext.At(at, normal, Yaw);
            c.Snapped = snap;
            c.Seed = rng.Next();
            return c;
        }

        static GameObject Place(PaletteEntry entry, string look, PlaceContext c)
        {
            var made = PaletteCatalog.Place(entry, look, c);
            // the ghost under the mouse is not what the next click lands on
            Physics.SyncTransforms();
            return made;
        }

        static void OnDrop(Event e)
        {
            if (!(DragAndDrop.GetGenericData(DragKey) is string key)) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var parts = key.Split('|');
                var entry = PaletteCatalog.Find(parts[0]);
                var scene = EditorSceneManager.GetActiveScene();
                if (entry != null && Surface(scene, HandleUtility.GUIPointToWorldRay(e.mousePosition), out var point, out var normal))
                {
                    var snap = e.alt ? default : FindSnap(scene, entry, point);
                    if (entry.Burst) Dab(scene, entry, snap.Found ? snap.Point : point, normal);
                    else Place(entry, parts.Length > 1 ? parts[1] : null, PlaceAt(snap.Found ? snap.Point : point, normal, snap));
                }
                DragAndDrop.SetGenericData(DragKey, null);
            }
            e.Use();
        }

        // ------------------------------------------------------------------ surface and snapping

        /// <summary>
        /// The surface under <paramref name="ray"/>: the level's floors, ground and solids (never a ghost, a trigger or the kit's zones); else
        /// the plane at the ground under the view.
        /// </summary>
        public static bool Surface(Scene scene, Ray ray, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = Vector3.up;
            float best = float.MaxValue;
            // the ground snap off: on the plane at the view's ground, whatever stands there
            var hits = (Disabled & (PaletteSnap.Ground | PaletteSnap.Floor)) == 0
                ? Physics.RaycastAll(ray, 3000f, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore)
                : new RaycastHit[0];
            foreach (var hit in hits)
            {
                if (hit.distance >= best || hit.collider.gameObject.scene != scene || hit.collider.transform.root.name == GhostName) continue;
                best = hit.distance;
                point = hit.point;
                normal = hit.normal;
            }
            if (best < float.MaxValue) return true;
            var view = SceneView.lastActiveSceneView;
            var pivot = view != null ? view.pivot : Vector3.zero;
            float ground = LevelNature.GroundAt(scene, new Vector2(pivot.x, pivot.z));
            var plane = new Plane(Vector3.up, new Vector3(0f, float.IsNaN(ground) ? pivot.y : ground, 0f));
            if (!plane.Raycast(ray, out float enter) || enter > 3000f) return false;
            point = ray.GetPoint(enter);
            return true;
        }

        /// <summary>What <paramref name="entry"/> placed at <paramref name="point"/> snaps to (the snapping kinds it takes that are on).</summary>
        public static PlaceSnap FindSnap(Scene scene, PaletteEntry entry, Vector3 point)
        {
            var wanted = entry.Snap & ~Disabled;
            foreach (var (kind, find) in Snappers)
            {
                if ((wanted & kind) == 0) continue;
                var s = find(entry, point);
                if (s.Found) return s;
            }
            if ((wanted & PaletteSnap.DeckEdge) != 0 && TimberLinks.TryNearest(scene, point, null, DeckEdgeReach, out var attach, out var edge))
                return new PlaceSnap { Kind = PaletteSnap.DeckEdge, Point = edge, Direction = Outward(attach), Attach = attach, Label = attach.Describe };
            return default;
        }

        /// <summary>The way out of an attach: off a deck's edge, or on along the end of the piece it is joined to.</summary>
        static Vector3 Outward(TimberAttach attach)
        {
            if (attach.OnDeck)
            {
                attach.deck.EdgePoint(attach.along, out var outward);
                return outward;
            }
            IList<Vector3> line = attach.joined switch
            {
                TimberWalkShape walk => walk.WorldControl(),
                RopeBridgeShape bridge => new[] { bridge.WorldEnds().a, bridge.WorldEnds().b },
                _ => null,
            };
            if (line == null || line.Count < 2) return Vector3.forward;
            var d = attach.joinedStart ? line[0] - line[1] : line[line.Count - 1] - line[line.Count - 2];
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        // ------------------------------------------------------------------ burst

        /// <summary>What a burst keeps off: the walk lines and outlines, the water, the pass arcs (gathered once per click).</summary>
        public sealed class Keepout
        {
            public List<(Vector2 a, Vector2 b)> Paths;
            public List<(Vector2 center, Vector2 half, float yaw)> Waters;
            public List<Vector3> Arcs;

            public static Keepout Of(Scene scene) => new Keepout
            {
                Paths = LevelForest.Paths(scene).Concat(PalettePaths.Extra(scene)).ToList(),
                Waters = LevelForest.Waters(scene),
                Arcs = NatureDressing.PassSamples(),
            };

            /// <summary>True where a burst may place a copy.</summary>
            public bool Allows(Vector3 p)
            {
                var q = new Vector2(p.x, p.z);
                if (Paths.Count > 0 && LevelForest.Distance(Paths, q) < BurstPathClearance) return false;
                if (LevelForest.OnWater(Waters, q) || PalettePaths.OnWater(q)) return false;
                return !Arcs.Any(a => new Vector2(a.x - p.x, a.z - p.z).magnitude < BurstArcClearance + PassCorridor.DecorationClearance && a.y > p.y - 1f && a.y < p.y + 25f);
            }
        }

        /// <summary>
        /// One burst click: <see cref="BurstSettings.Count"/> copies of the entry's prefabs within the radius of <paramref name="center"/>,
        /// on the ground or a deck, turned and sized at random, aligned to the surface if asked, never where <see cref="Keepout"/> forbids.
        /// Returns what was placed.
        /// </summary>
        public static List<GameObject> Dab(Scene scene, PaletteEntry entry, Vector3 center, Vector3 normal, BurstSettings settings = null, int? seed = null)
        {
            settings ??= Burst;
            var placed = new List<GameObject>();
            var prefabs = entry.BurstPrefabs.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p != null).ToArray();
            if (prefabs.Length == 0) return placed;
            var r = seed.HasValue ? new Random(seed.Value) : rng;
            var keep = Keepout.Of(scene);
            var settingsOf = LevelScenes.SettingsOf(scene);
            var group = settingsOf != null ? settingsOf.Group(LevelGroup.Props) : null;
            var scale = entry.BurstScale == Vector2.one ? settings.Scale : entry.BurstScale;
            int count = Mathf.Max(1, settings.Count);
            for (int i = 0; i < count; i++)
            {
                var at = center;
                var n = normal;
                if (settings.Radius > 0f || i > 0)
                {
                    float a = (float)r.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)r.NextDouble()) * Mathf.Max(settings.Radius, 0.5f);
                    var probe = center + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                    if (!BurstGround(scene, new Ray(probe + Vector3.up * 3f, Vector3.down), out var hit)) continue;
                    at = hit.point;
                    n = hit.normal;
                }
                if (!keep.Allows(at)) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[r.Next(prefabs.Length)], group != null ? group : null);
                if (group == null) SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Burst " + entry.Label);
                go.transform.position = at;
                float yaw = settings.RandomYaw ? (float)r.NextDouble() * 360f : Yaw;
                var up = settings.AlignToNormal ? Quaternion.FromToRotation(Vector3.up, n) : Quaternion.identity;
                go.transform.rotation = up * Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = Vector3.one * Mathf.Lerp(scale.x, scale.y, (float)r.NextDouble());
                placed.Add(go);
                session.Add(go);
            }
            if (placed.Count > 0) Remember(entry);
            return placed;
        }

        static void Remember(PaletteEntry entry) => PaletteCatalog.Remember(entry.Id);

        /// <summary>The ground or a deck under a burst probe (never a prop, a tree, a rail or the kit).</summary>
        static bool BurstGround(Scene scene, Ray ray, out RaycastHit best)
        {
            best = default;
            float d = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(ray, 1000f, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= d || hit.collider.gameObject.scene != scene || hit.collider.GetComponentInParent<PropSolid>() != null) continue;
                var proxy = hit.collider.GetComponent<WalkableProxy>();
                bool ground = hit.collider is TerrainCollider || (proxy != null && (proxy.ProxyKind == WalkableProxy.Kind.Deck || proxy.ProxyKind == WalkableProxy.Kind.Terrain));
                if (!ground) continue;
                d = hit.distance;
                best = hit;
            }
            return d < float.MaxValue;
        }

        /// <summary>Erases what this burst session placed within <paramref name="radius"/> of <paramref name="center"/>.</summary>
        public static int Erase(Vector3 center, float radius)
        {
            int n = 0;
            foreach (var go in session.ToArray())
            {
                if (go == null) { session.Remove(go); continue; }
                if (new Vector2(go.transform.position.x - center.x, go.transform.position.z - center.z).magnitude > radius) continue;
                session.Remove(go);
                Undo.DestroyObjectImmediate(go);
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ the ghost

        static void DrawGhost(Vector3 at, Vector3 normal, PlaceSnap snap, bool erasing)
        {
            var rotation = Quaternion.Euler(0f, Yaw, 0f);
            if (Armed.Burst)
            {
                Handles.color = erasing ? new Color(1f, 0.4f, 0.3f) : new Color(0.5f, 1f, 0.5f);
                Handles.DrawWireDisc(at, normal, Mathf.Max(0.5f, Burst.Radius), 2f);
                Handles.Label(at + Vector3.up * 0.5f, erasing ? "erase this burst's" : Armed.Label);
                return;
            }
            string prefab = Armed.PrefabOf(ArmedLook);
            if (prefab != null)
            {
                if (ghost == null || ghostKey != prefab) MakeGhost(prefab);
                if (ghost != null)
                {
                    ghost.SetActive(true);
                    ghost.transform.SetPositionAndRotation(at, rotation);
                }
            }
            Handles.color = snap.Found ? new Color(0.3f, 0.9f, 1f) : new Color(1f, 0.85f, 0.3f);
            Handles.DrawWireDisc(at, Vector3.up, 1.2f, 2f);
            Handles.ArrowHandleCap(0, at + Vector3.up * 0.05f, snap.Found && snap.Direction.sqrMagnitude > 0f ? Quaternion.LookRotation(snap.Direction) : rotation, 2f, EventType.Repaint);
            Handles.Label(at + Vector3.up * 1.5f, snap.Found ? $"{Armed.Label}: {snap.Label}" : Armed.Label);
        }

        static void MakeGhost(string prefabPath)
        {
            DestroyGhost();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;
            ghost = Object.Instantiate(prefab);
            ghost.name = GhostName;
            ghost.hideFlags = HideFlags.HideAndDontSave;
            foreach (var c in ghost.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var b in ghost.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            ghostKey = prefabPath;
        }

        static void DestroyGhost()
        {
            if (ghost != null) Object.DestroyImmediate(ghost);
            ghost = null;
            ghostKey = null;
        }

        /// <summary>Ghosts left by a domain reload.</summary>
        static void ClearGhosts()
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g != null && g.name == GhostName && g.transform.parent == null).ToArray())
                Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// Lines and water the burst keeps off besides the timber's and the lakes (ARCHITECTURE §27.3): what later tools add (a trail, a river's
    /// ribbon) registers here.
    /// </summary>
    public static class PalettePaths
    {
        public static readonly List<Func<Scene, IEnumerable<(Vector2 a, Vector2 b)>>> Lines = new List<Func<Scene, IEnumerable<(Vector2, Vector2)>>>();
        public static readonly List<Func<Vector2, bool>> Water = new List<Func<Vector2, bool>>();

        public static IEnumerable<(Vector2 a, Vector2 b)> Extra(Scene scene) => Lines.SelectMany(f => f(scene));

        public static bool OnWater(Vector2 q) => Water.Any(f => f(q));
    }
}
