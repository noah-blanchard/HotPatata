using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// Freezes a piece of timber into its elements (ARCHITECTURE §27.1): the piece is drawn once more while an
    /// <see cref="ElementRecorder"/> listens, and each board, beam, rope and wall it lays becomes an element of its own under a
    /// <see cref="FrozenStructure"/> (sorted in groups: boards, frame, supports, each rail...). The structure looks exactly like the
    /// piece; from then on each element is moved by hand and the piece is gone (one way: Ctrl+Z only). Colliders the elements do not
    /// stand for (a house's ceiling) are kept as they were.
    /// </summary>
    public static class TimberFreezer
    {
        /// <summary>The pieces that freeze: timber (not trees, water or forests).</summary>
        public static bool CanFreeze(LevelPiece piece) =>
            piece is TimberDeckShape || piece is TimberWalkShape || piece is RopeBridgeShape || piece is RailingShape || piece is HouseShape || piece is TrestleShape
            || piece is ShelterShape || piece is GantryShape || piece is SignShape || piece is PulleyBeamShape;

        /// <summary>Freezes every piece that can be (one undo step); returns the structures made.</summary>
        public static List<FrozenStructure> Freeze(IEnumerable<LevelPiece> pieces)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var made = pieces.Where(p => p != null && CanFreeze(p)).ToList().Select(Freeze).Where(f => f != null).ToList();
            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Freeze into elements");
            if (made.Count > 0) Selection.objects = made.Select(f => (Object)f.gameObject).ToArray();
            return made;
        }

        public static FrozenStructure Freeze(LevelPiece piece)
        {
            if (piece == null || !CanFreeze(piece)) return null;
            string tooBig = LevelPieceBuilder.TooBig(piece);
            if (tooBig != null)
            {
                Debug.LogWarning($"[Timber] {piece.name} is not frozen: {tooBig}", piece);
                return null;
            }
            var scene = piece.gameObject.scene;
            var recorder = new ElementRecorder();
            // drawn in the piece's own scene (what it draws may be moved into it), out of sight, gone before the method returns
            var temp = new GameObject("Freezing " + piece.name) { hideFlags = HideFlags.HideInHierarchy };
            if (scene.IsValid() && temp.scene != scene) SceneManager.MoveGameObjectToScene(temp, scene);
            // the piece's own geometry stays out of the way while it is drawn again, as in a rebuild
            var output = piece.Output;
            bool wasActive = output != null && output.activeSelf;
            if (output != null) output.SetActive(false);
            ElementRecorder.Active = recorder;
            OrganicAssets.BeginInMemory();
            TimberKit.ClearReport();
            try { LevelPieceBuilder.BuildGeometry(piece, temp.transform); }
            finally
            {
                ElementRecorder.Active = null;
                OrganicAssets.End();
                TimberKit.ClearReport();
                if (output != null) output.SetActive(wasActive);
            }

            var t = piece.transform;
            var root = new GameObject(piece.name);
            Undo.RegisterCreatedObjectUndo(root, "Freeze " + piece.name);
            if (t.parent != null) root.transform.SetParent(t.parent, false);
            else if (root.scene != scene) SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(t.position, Quaternion.Euler(0f, t.eulerAngles.y, 0f));
            root.transform.SetSiblingIndex(t.GetSiblingIndex());
            var frozen = root.AddComponent<FrozenStructure>();
            var proxy = temp.GetComponentInChildren<WalkableProxy>(true);
            if (proxy != null)
            {
                frozen.SurfaceKind = proxy.ProxyKind;
                frozen.Tolerance = proxy.Tolerance;
            }
            frozen.Runs.AddRange(recorder.Runs);

            var groups = new Dictionary<string, Transform>();
            var counts = new Dictionary<string, int>();
            Transform Group(string name)
            {
                name = string.IsNullOrEmpty(name) ? "Timber" : name;
                if (name.StartsWith(piece.name + " ")) name = name.Substring(piece.name.Length + 1);
                if (groups.TryGetValue(name, out var g)) return g;
                g = new GameObject(name).transform;
                g.SetParent(root.transform, false);
                return groups[name] = g;
            }
            string Name(Transform group, string what)
            {
                string key = group.GetInstanceID() + what;
                counts.TryGetValue(key, out int n);
                counts[key] = ++n;
                return what + " " + n;
            }
            foreach (var record in recorder.Records)
            {
                var group = Group(record.tags.group);
                Make(record, group, Name(group, ElementRecorder.KindOf(record).ToString()), root.transform.rotation);
            }
            foreach (var wall in recorder.Walls)
            {
                var group = Group(wall.group);
                var go = new GameObject(Name(group, "Wall"));
                go.transform.SetParent(group, false);
                go.transform.SetPositionAndRotation(Centre(wall.line), root.transform.rotation);
                var w = go.AddComponent<TimberWall>();
                w.Height = wall.height;
                w.Points.Clear();
                w.Points.AddRange(wall.line.Select(go.transform.InverseTransformPoint));
            }
            // colliders the elements do not stand for (a ceiling): kept as they are
            foreach (var collider in temp.GetComponentsInChildren<Collider>(true))
            {
                if (collider.GetComponent<WalkableProxy>() != null || recorder.IsReplaced(collider.transform)) continue;
                collider.transform.SetParent(Group("Bodies"), true);
                collider.gameObject.hideFlags = HideFlags.None;
            }
            foreach (var mesh in LevelPieceBuilder.Meshes(temp).Where(m => !EditorUtility.IsPersistent(m)).ToList()) Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(temp);

            // what the author hung under the piece goes with it; the piece goes
            foreach (Transform child in t.Cast<Transform>().ToList())
                if (child.GetComponent<LevelPieceOutput>() == null) Undo.SetTransformParent(child, root.transform, "Freeze " + piece.name);
            ReleaseAttached(piece);
            Undo.DestroyObjectImmediate(piece.gameObject);

            Refresh(frozen);
            MeshCatalog.Flush();
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Timber] {root.name} frozen: {recorder.Records.Count} element(s), {recorder.Walls.Count} wall(s)", root);
            return frozen;
        }

        /// <summary>Draws every element, wall and the walking surface of a structure.</summary>
        public static void Refresh(FrozenStructure frozen)
        {
            foreach (var element in frozen.GetComponentsInChildren<TimberElement>(true)) TimberElementBuilder.Refresh(element, true);
            foreach (var wall in frozen.GetComponentsInChildren<TimberWall>(true)) TimberElementBuilder.Refresh(wall, true);
            WalkSurfaceBuilder.Refresh(frozen, true);
            Physics.SyncTransforms();
        }

        /// <summary>Makes every element <paramref name="recorder"/> heard under <paramref name="parent"/> (named by kind and count), drawn.</summary>
        public static List<TimberElement> MakeAll(ElementRecorder recorder, Transform parent)
        {
            var counts = new Dictionary<(Transform, TimberElementKind), int>();
            var groups = new Dictionary<string, Transform>();
            bool single = recorder.Records.Select(r => r.tags.group).Distinct().Count() <= 1;
            var made = new List<TimberElement>();
            foreach (var record in recorder.Records)
            {
                // sorted in groups by what the kit said they are (one group only: straight under the parent)
                string name = string.IsNullOrEmpty(record.tags.group) ? "Timber" : record.tags.group;
                if (name.StartsWith(parent.name + " ")) name = name.Substring(parent.name.Length + 1);
                if (!groups.TryGetValue(name, out var group))
                {
                    group = parent;
                    if (!single)
                    {
                        group = new GameObject(name).transform;
                        group.SetParent(parent, false);
                    }
                    groups[name] = group;
                }
                var kind = ElementRecorder.KindOf(record);
                counts.TryGetValue((group, kind), out int n);
                counts[(group, kind)] = ++n;
                made.Add(Make(record, group, kind + " " + n, parent.rotation));
            }
            foreach (var element in made) TimberElementBuilder.Refresh(element, true);
            return made;
        }

        static TimberElement Make(ElementRecorder.Record r, Transform parent, string name, Quaternion rootRotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TimberElement element;
            switch (r.shape)
            {
                case ElementRecorder.Shape.Box:
                {
                    go.transform.SetPositionAndRotation(r.center, r.rotation);
                    var board = go.AddComponent<TimberBoard>();
                    board.Size = r.size;
                    element = board;
                    break;
                }
                case ElementRecorder.Shape.Paint:
                {
                    go.transform.SetPositionAndRotation(r.center, r.rotation);
                    var paint = go.AddComponent<TimberPaint>();
                    paint.Outline.Clear();
                    paint.Outline.AddRange(r.profile);
                    paint.Paint = r.paint;
                    element = paint;
                    break;
                }
                case ElementRecorder.Shape.Lathe:
                {
                    go.transform.SetPositionAndRotation(r.center, r.rotation);
                    var turned = go.AddComponent<TimberTurned>();
                    turned.Profile.Clear();
                    turned.Profile.AddRange(r.profile);
                    turned.Sides = r.sides;
                    element = turned;
                    break;
                }
                default:
                {
                    go.transform.SetPositionAndRotation(Centre(r.path), rootRotation);
                    var path = go.AddComponent<TimberPath>();
                    path.Points.Clear();
                    path.Points.AddRange(r.path.Select(go.transform.InverseTransformPoint));
                    path.Caps = r.caps;
                    if (r.shape == ElementRecorder.Shape.Tube)
                    {
                        path.Shape = TimberPath.Profile.Round;
                        path.Radius = r.radius;
                    }
                    else
                    {
                        path.Shape = TimberPath.Profile.Square;
                        path.Width = r.width;
                        path.Height = r.height;
                        path.Anchor = r.anchor == MeshBuilder.Anchor.Top ? TimberPath.Seat.Top : r.anchor == MeshBuilder.Anchor.Bottom ? TimberPath.Seat.Bottom : TimberPath.Seat.Center;
                    }
                    element = path;
                    break;
                }
            }
            element.Kind = ElementRecorder.KindOf(r);
            element.Wood = (TimberWood)r.slot;
            element.Grain = r.grain;
            element.Solid = r.tags.solid && r.shape != ElementRecorder.Shape.Paint;
            element.Walk = r.shape == ElementRecorder.Shape.Box ? r.tags.walk : TimberWalk.None;
            element.Run = r.tags.run;
            element.Order = r.tags.order;
            return element;
        }

        /// <summary>
        /// The walkways and bridges whose ends sit on the piece (its deck edge, or its own end) keep them where they are drawn now: the
        /// piece is going, so each such end becomes a free point at the same place.
        /// </summary>
        static void ReleaseAttached(LevelPiece piece)
        {
            bool Refers(TimberAttach attach) => attach.deck == piece || attach.joined == piece;
            foreach (var other in TimberLinks.Pieces(piece.gameObject.scene).ToList())
            {
                if (other == piece) continue;
                if (other is TimberWalkShape walk)
                {
                    bool start = Refers(walk.Start), end = Refers(walk.End);
                    if (!start && !end) continue;
                    Undo.RecordObject(walk, "Freeze " + piece.name);
                    var control = walk.WorldControl();
                    // an end that stays on its deck gets its lead point back from the deck itself
                    if (!start && walk.Start.OnDeck) control.RemoveAt(0);
                    if (!end && walk.End.OnDeck) control.RemoveAt(control.Count - 1);
                    walk.Points.Clear();
                    walk.Points.AddRange(control.Select(walk.transform.InverseTransformPoint));
                    if (start) walk.Start = default;
                    if (end) walk.End = default;
                    EditorUtility.SetDirty(walk);
                }
                else if (other is RopeBridgeShape bridge)
                {
                    bool start = Refers(bridge.StartAttach), end = Refers(bridge.EndAttach);
                    if (!start && !end) continue;
                    Undo.RecordObject(bridge, "Freeze " + piece.name);
                    var (a, b) = bridge.WorldEnds();
                    if (start)
                    {
                        bridge.A = bridge.transform.InverseTransformPoint(a);
                        bridge.StartAttach = default;
                    }
                    if (end)
                    {
                        bridge.B = bridge.transform.InverseTransformPoint(b);
                        bridge.EndAttach = default;
                    }
                    EditorUtility.SetDirty(bridge);
                }
            }
        }

        static Vector3 Centre(IList<Vector3> points) => points.Count == 0 ? Vector3.zero : points.Aggregate(Vector3.zero, (s, p) => s + p) / points.Count;
    }
}
