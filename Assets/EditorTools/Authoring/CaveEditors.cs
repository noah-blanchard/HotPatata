using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Cave networks and cave mouths (M21, ARCHITECTURE §27.6, docs/LEVEL_AUTHORING.md §29): adding a chamber or a shaft to a network,
    /// linking two chambers with a tunnel attached at both ends, detaching a child, and making a mouth on a hillside with its parts.
    /// </summary>
    public static class CaveNetworks
    {
        /// <summary>A new chamber (a cavern, or a shaft) of <paramref name="network"/> standing at <paramref name="at"/>, built.</summary>
        public static RockShellShape AddNode(CaveNetwork network, Vector3 at, RockShellKind kind = RockShellKind.Cavern)
        {
            var name = kind == RockShellKind.Shaft ? "Shaft" : "Chamber";
            var go = new GameObject(ObjectNames.GetUniqueName(network.transform.Cast<Transform>().Select(t => t.name).ToArray(), name));
            Undo.RegisterCreatedObjectUndo(go, "Add " + name);
            Undo.SetTransformParent(go.transform, network.transform, "Add " + name);
            go.transform.position = at;
            var node = Undo.AddComponent<RockShellShape>(go);
            node.EnsureId();
            node.Kind = kind;
            node.Points.Clear();
            if (kind == RockShellKind.Shaft)
            {
                node.Height = 20f;
                node.Points.AddRange(DeckShapes.Oval(10f, 10f, 12));
            }
            else
            {
                node.Height = 6f;
                node.ApexHeight = 9f;
                node.Points.AddRange(DeckShapes.Oval(18f, 15f, 14));
            }
            LevelPieceBuilder.Rebuild(node);
            return node;
        }

        /// <summary>A tunnel from chamber <paramref name="a"/> to chamber <paramref name="b"/>, each end attached to its wall (facing the other), built.</summary>
        public static RockShellShape Link(CaveNetwork network, RockShellShape a, RockShellShape b)
        {
            var go = new GameObject(ObjectNames.GetUniqueName(network.transform.Cast<Transform>().Select(t => t.name).ToArray(), $"Tunnel {a.name} to {b.name}"));
            Undo.RegisterCreatedObjectUndo(go, "Link chambers");
            Undo.SetTransformParent(go.transform, network.transform, "Link chambers");
            go.transform.position = a.transform.position;
            var tunnel = Undo.AddComponent<RockShellShape>(go);
            tunnel.EnsureId();
            tunnel.Kind = RockShellKind.Tunnel;
            tunnel.Width = network.LinkWidth;
            tunnel.Height = network.LinkHeight;
            tunnel.Flare = network.Flare;
            tunnel.StartAttach = new CaveAttach { node = a, angle = Toward(a, b) };
            tunnel.EndAttach = new CaveAttach { node = b, angle = Toward(b, a) };
            var ends = tunnel.WorldPoints();
            tunnel.Points.Clear();
            tunnel.Points.Add(Vector3.zero);
            tunnel.Points.Add(tunnel.transform.InverseTransformPoint((ends[0] + ends[ends.Count - 1]) * 0.5f));
            tunnel.Points.Add(tunnel.transform.InverseTransformPoint(ends[ends.Count - 1]));
            LevelPieceBuilder.Rebuild(tunnel);
            // the chambers open where it meets them, and the tunnels already meeting them leave its flared end open
            LevelPieceBuilder.Rebuild(new List<LevelPiece> { a, b }.Concat(network.Links.Where(l => l != tunnel &&
                (l.StartAttach.node == a || l.StartAttach.node == b || l.EndAttach.node == a || l.EndAttach.node == b))).ToList());
            return tunnel;
        }

        /// <summary>The direction (degrees round, 0 = +Z, in <paramref name="from"/>'s space) from one chamber's middle to another's.</summary>
        public static float Toward(RockShellShape from, RockShellShape to)
        {
            var d = from.transform.InverseTransformPoint(Middle(to)) - from.transform.InverseTransformPoint(Middle(from));
            return Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f);
        }

        /// <summary>A chamber's middle on its floor (world).</summary>
        public static Vector3 Middle(RockShellShape node)
        {
            var w = node.WorldPoints();
            return w.Count == 0 ? node.transform.position : new Vector3(w.Average(p => p.x), node.transform.position.y, w.Average(p => p.z));
        }

        /// <summary>A child of a network made a free piece: a tunnel keeps its ends where they are, a chamber's tunnels let go of it.</summary>
        public static void Detach(RockShellShape piece)
        {
            var network = piece.GetComponentInParent<CaveNetwork>();
            if (piece.Kind == RockShellKind.Tunnel) Free(piece, true, true);
            else if (network != null)
                foreach (var link in network.Links.ToList())
                    Free(link, link.StartAttach.node == piece, link.EndAttach.node == piece);
            var group = piece.gameObject.scene.GetRootGameObjects().Select(g => g.GetComponent<LevelSettings>()).FirstOrDefault(s => s != null)?.Group(LevelGroup.Props);
            Undo.SetTransformParent(piece.transform, group, "Detach");
            LevelPieceBuilder.Rebuild(piece);
        }

        /// <summary>Lets go of a tunnel's attached ends: they stay where they are, as free points.</summary>
        public static void Free(RockShellShape tunnel, bool start, bool end)
        {
            if (!start && !end) return;
            Undo.RecordObject(tunnel, "Detach");
            var world = tunnel.WorldPoints();
            if (start && tunnel.StartAttach.Attached) tunnel.Points[0] = tunnel.transform.InverseTransformPoint(world[0]);
            if (end && tunnel.EndAttach.Attached) tunnel.Points[tunnel.Points.Count - 1] = tunnel.transform.InverseTransformPoint(world[world.Count - 1]);
            if (start) tunnel.StartAttach = default;
            if (end) tunnel.EndAttach = default;
            EditorUtility.SetDirty(tunnel);
        }

        /// <summary>Removes a chamber: its tunnels' ends stay where they were.</summary>
        public static void Remove(RockShellShape node)
        {
            var network = node.GetComponentInParent<CaveNetwork>();
            if (network != null)
                foreach (var link in network.Links.ToList())
                    Free(link, link.StartAttach.node == node, link.EndAttach.node == node);
            Undo.DestroyObjectImmediate(node.gameObject);
        }
    }

    /// <summary>A cave's mouth on a hillside (ARCHITECTURE §27.6): its tunnel, lintel, scree and talus, ambience and lanterns, made together.</summary>
    public static class CaveMouths
    {
        /// <summary>How far the tunnel runs into the hill (m), and how far the cave's ambience reaches in.</summary>
        public const float Depth = 18f, AmbienceLength = 12f;

        /// <summary>
        /// A mouth at <paramref name="at"/> (the floor, on the hillside), its tunnel running <paramref name="into"/> the hill: the tunnel
        /// (floored, so the ground opens over it on save), the lintel (a fractured cliff over the mouth, its scree and talus), the Cave
        /// ambience over its first metres and two lanterns inside.
        /// </summary>
        public static CaveMouth Make(Transform parent, Vector3 at, Vector3 into, float width = 5f, float height = 4.5f)
        {
            into.y = 0f;
            into = into.sqrMagnitude < 1e-6f ? Vector3.forward : into.normalized;
            var go = new GameObject("Cave mouth");
            Undo.RegisterCreatedObjectUndo(go, "Cave mouth");
            if (parent != null) Undo.SetTransformParent(go.transform, parent, "Cave mouth");
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(into, Vector3.up));
            var mouth = Undo.AddComponent<CaveMouth>(go);

            var tunnelGo = new GameObject("Tunnel");
            Undo.RegisterCreatedObjectUndo(tunnelGo, "Cave mouth");
            tunnelGo.transform.SetParent(go.transform, false);
            var tunnel = tunnelGo.AddComponent<RockShellShape>();
            tunnel.EnsureId();
            tunnel.Kind = RockShellKind.Tunnel;
            tunnel.Width = width;
            tunnel.Height = height;
            tunnel.Floor = true;
            tunnel.CloseEnd = true;
            tunnel.Flare = 3f;
            tunnel.Points.Clear();
            tunnel.Points.Add(new Vector3(0f, 0f, -2.5f));
            tunnel.Points.Add(new Vector3(0f, 0f, Depth * 0.5f));
            tunnel.Points.Add(new Vector3(0.8f, 0f, Depth));
            tunnel.Sections.Add(new CaveSection(width + 1f, height + 0.6f, CaveProfile.Arch));
            tunnel.Sections.Add(new CaveSection(width, height, CaveProfile.Round));
            tunnel.Sections.Add(new CaveSection(width - 0.6f, height - 0.3f, CaveProfile.Round, 4f));
            tunnel.Dressing = new CaveDressing { stalactites = 0.5f, stalagmites = 0.3f, puddles = 0.4f, props = 0.4f };

            var lintelGo = new GameObject("Lintel");
            Undo.RegisterCreatedObjectUndo(lintelGo, "Cave mouth");
            lintelGo.transform.SetParent(go.transform, false);
            lintelGo.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            var lintel = lintelGo.AddComponent<CliffShape>();
            lintel.EnsureId();
            RockEntries.Dress(lintel, CliffRock.Granite);
            float half = width * 0.5f + 2.5f;
            lintel.Outline.Clear();
            lintel.Outline.AddRange(new[]
            {
                new Vector3(-half, 0f, -1.9f), new Vector3(-half - 0.8f, 0f, 2.5f), new Vector3(-half + 0.4f, 0f, 6f),
                new Vector3(half - 0.4f, 0f, 6f), new Vector3(half + 0.8f, 0f, 2.5f), new Vector3(half, 0f, -1.9f), new Vector3(0f, 0f, -2.2f),
            });
            lintel.Height = height + 4f;
            lintel.Fracture = 0.8f;
            lintel.Crest = 0.5f;
            lintel.Scree = 0.7f;
            lintel.ScreeWidth = 5f;
            lintel.Fuse = 2.5f;

            var ambienceGo = new GameObject("Cave ambience");
            Undo.RegisterCreatedObjectUndo(ambienceGo, "Cave mouth");
            ambienceGo.transform.SetParent(go.transform, false);
            ambienceGo.transform.localPosition = new Vector3(0f, height * 0.5f, AmbienceLength * 0.5f);
            var ambience = ambienceGo.AddComponent<AmbienceZone>();
            ambience.Configure("Cave", new Vector3(width + 2f, height + 2f, AmbienceLength), 1);
            mouth.Set(tunnel, lintel, ambience);

            // the tunnel first: the lintel's rock leaves its clear space open
            LevelPieceBuilder.Rebuild(tunnel);
            LevelPieceBuilder.Rebuild(lintel);
            float side = 1f;
            foreach (float along in new[] { 4.5f, 10f })
            {
                var hook = go.transform.TransformPoint(new Vector3(side * (width * 0.5f - 0.7f), height * 0.62f, along));
                var lantern = TimberMine.Lantern(go.transform, "Lantern", hook);
                if (lantern != null)
                {
                    Undo.RegisterCreatedObjectUndo(lantern, "Cave mouth");
                    mouth.Lanterns.Add(lantern);
                }
                side = -side;
            }
            EditorUtility.SetDirty(mouth);
            return mouth;
        }
    }

    [CustomEditor(typeof(CaveNetwork))]
    public sealed class CaveNetworkEditor : LevelPieceEditor
    {
        CaveNetwork Network => (CaveNetwork)target;
        int from, to = 1;

        protected override string Help =>
            "A cave network: its chambers (caverns and shafts) and the tunnels linking them are its children. Shift+click the ground to add a chamber, " +
            "drag a chamber's dot to move it (its tunnels follow), link two chambers below. Each child is an ordinary rock piece: select it to shape it, " +
            "or detach it to make it stand on its own.";

        protected override void DrawTools()
        {
            var network = Network;
            var nodes = network.Nodes.ToList();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add chamber")) CaveNetworks.AddNode(network, network.transform.position + Vector3.right * 24f * nodes.Count);
                if (GUILayout.Button("Add shaft")) CaveNetworks.AddNode(network, network.transform.position + Vector3.right * 24f * nodes.Count, RockShellKind.Shaft);
            }
            if (nodes.Count >= 2)
            {
                var names = nodes.Select(n => n.name).ToArray();
                using (new EditorGUILayout.HorizontalScope())
                {
                    from = EditorGUILayout.Popup(Mathf.Min(from, nodes.Count - 1), names);
                    to = EditorGUILayout.Popup(Mathf.Min(to, nodes.Count - 1), names);
                    using (new EditorGUI.DisabledScope(from == to))
                        if (GUILayout.Button("Link", GUILayout.Width(60))) CaveNetworks.Link(network, nodes[from], nodes[to]);
                }
            }
            EditorGUILayout.LabelField("Chambers", EditorStyles.boldLabel);
            foreach (var node in nodes)
                Row(node, $"{node.name}: {node.Kind}, {Size(node)}, {node.Height:F0} m");
            EditorGUILayout.LabelField("Tunnels", EditorStyles.boldLabel);
            foreach (var link in network.Links.ToList())
                Row(link, $"{link.name}: {(link.StartAttach.Attached ? link.StartAttach.node.name : "free")} to {(link.EndAttach.Attached ? link.EndAttach.node.name : "free")}, {Curves.Length(link.WorldPoints()):F0} m");
        }

        static string Size(RockShellShape node)
        {
            var w = node.WorldPoints();
            if (w.Count == 0) return "empty";
            return $"{w.Max(p => p.x) - w.Min(p => p.x):F0} × {w.Max(p => p.z) - w.Min(p => p.z):F0} m";
        }

        static void Row(RockShellShape piece, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(label, EditorStyles.label)) Selection.activeGameObject = piece.gameObject;
                if (GUILayout.Button("Detach", GUILayout.Width(60))) { CaveNetworks.Detach(piece); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("×", GUILayout.Width(22)))
                {
                    if (piece.Kind == RockShellKind.Tunnel) Undo.DestroyObjectImmediate(piece.gameObject);
                    else CaveNetworks.Remove(piece);
                    GUIUtility.ExitGUI();
                }
            }
        }

        protected override void DrawScene()
        {
            var network = Network;
            Handles.color = new Color(1f, 0.82f, 0.25f, 0.8f);
            foreach (var link in network.Links)
            {
                var w = link.WorldPoints();
                if (w.Count >= 2) Handles.DrawAAPolyLine(3f, Curves.Spline(w, 0.5f).ToArray());
            }
            foreach (var node in network.Nodes)
            {
                var at = CaveNetworks.Middle(node);
                Handles.color = Color.white;
                Handles.Label(at + Vector3.up * 2f, node.name);
                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(at, HandleUtility.GetHandleSize(at) * 0.12f, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(node.transform, "Move chamber");
                    var delta = moved - at;
                    delta.y = 0f;
                    node.transform.position += delta;
                }
            }
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && e.shift &&
                Physics.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out var hit, 5000f, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore))
            {
                CaveNetworks.AddNode(network, hit.point);
                Selection.activeGameObject = network.gameObject;
                e.Use();
            }
        }
    }

    [CustomEditor(typeof(CaveMouth))]
    public sealed class CaveMouthEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("A cave's mouth: its tunnel, the lintel of rock over it (with its scree and talus), the cave's ambience and two lanterns. " +
                                    "Each is a child to edit as usual; deleting the mouth deletes them. Save the scene to open the ground over the tunnel.",
                MessageType.None);
        }
    }
}
