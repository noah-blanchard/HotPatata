using System.Collections.Generic;
using System.Linq;
using HotPatata;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Checks that an obstacle prefab (a kit prefab or anyone's variant with another visual) keeps the contract of its systems
    /// (docs/OBSTACLES.md §4): every reference a system needs is set and inside the prefab, a moving part is never the object
    /// carrying the system, colliders are box-like on gameplay layers and never under a hand-made visual, a lethal part has a
    /// kill trigger, and replicated systems have their network companion. Menu: <b>HotPatata/Kit/Validate Selected Prefabs</b>
    /// and <b>Validate All Obstacle Prefabs</b>; <c>ObstaclePrefabTests</c> runs it on every prefab of the project.
    /// </summary>
    public static class ObstaclePrefabValidator
    {
        static readonly string[] GameplayLayers = { "Player", "PlayerCatch", "Bomb", "Environment", "Hazard", "Trigger" };

        /// <summary>The systems this validator knows: a prefab holding one of them is an obstacle prefab.</summary>
        public static bool IsObstacle(GameObject root) =>
            root.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c is IObstacleState || c is Conveyor || c is LaunchPad || c is Zone || c is KillZone);

        /// <summary>Every problem found on <paramref name="root"/> (empty: the prefab keeps the contract).</summary>
        public static List<string> Validate(GameObject root)
        {
            var problems = new List<string>();
            void Need(bool ok, Component c, string what) { if (!ok) problems.Add($"{Path(root, c.transform)} ({c.GetType().Name}): {what}"); }
            bool Inside(Transform t) => t != null && (t == root.transform || t.IsChildOf(root.transform));
            void Part(Component c, Transform part, string field)
            {
                Need(part != null, c, field + " is not set");
                if (part == null) return;
                Need(Inside(part), c, field + " is outside the prefab");
                Need(part != c.transform, c, field + " must be a child, never the object carrying the system (its waypoints would move with it)");
            }
            void Point(Component c, Transform point, string field)
            {
                Need(point != null, c, field + " is not set");
                if (point != null) Need(Inside(point), c, field + " is outside the prefab");
            }

            foreach (var mp in root.GetComponentsInChildren<MovingPlatform>(true))
            {
                Part(mp, mp.Platform, "platform");
                Point(mp, mp.WaypointA, "waypointA");
                Point(mp, mp.WaypointB, "waypointB");
                if (mp.Platform != null && mp.WaypointA != null) Need(!mp.WaypointA.IsChildOf(mp.Platform), mp, "waypoints must not ride on the moving part");
                if (mp.Platform != null) Need(IsKinematic(mp.Platform), mp, "the moving part needs a kinematic Rigidbody (riders and the bomb see it move)");
            }
            foreach (var a in root.GetComponentsInChildren<SignalActuator>(true))
            {
                Part(a, a.Platform, "platform");
                Point(a, a.WaypointClosed, "waypointClosed");
                Point(a, a.WaypointOpen, "waypointOpen");
                if (a.Platform != null && a.WaypointClosed != null) Need(!a.WaypointClosed.IsChildOf(a.Platform), a, "waypoints must not ride on the moving part");
                if (a.Platform != null) Need(IsKinematic(a.Platform), a, "the moving part needs a kinematic Rigidbody");
                if (a.LethalEdge != null)
                {
                    Need(a.LethalEdge.isTrigger && a.LethalEdge.GetComponent<KillZone>() != null, a, "the lethal edge must be a KillZone trigger");
                    Need(a.Platform != null && a.LethalEdge.transform.IsChildOf(a.Platform), a, "the lethal edge must move with the part");
                }
                Networked(a, typeof(NetworkSignalActuator));
            }
            foreach (var f in root.GetComponentsInChildren<FallingPlatform>(true))
            {
                Part(f, f.Body, "body");
                Need(f.Trigger != null && f.Trigger.isTrigger, f, "trigger must be a trigger collider");
                if (f.Trigger != null && f.Body != null) Need(!f.Trigger.transform.IsChildOf(f.Body), f, "the trigger must not fall with the body");
                Networked(f, typeof(NetworkFallingPlatform));
            }
            foreach (var r in root.GetComponentsInChildren<RotatingObstacle>(true))
            {
                Need(r.Axis.sqrMagnitude > 0.0001f, r, "axis is zero");
                Need(r.GetComponentsInChildren<Collider>(true).All(c => c.isTrigger) || IsKinematic(r.transform), r,
                     "a turning part with colliders needs a kinematic Rigidbody");
            }
            foreach (var t in root.GetComponentsInChildren<BombTransit>(true))
            {
                Need(t.ExitCount > 0, t, "no exit");
                for (int i = 0; i < t.ExitCount; i++)
                {
                    var e = t.GetExit(i);
                    Point(t, e.hold, $"exit {i} hold");
                    Point(t, e.muzzle, $"exit {i} muzzle");
                    Point(t, e.pad, $"exit {i} pad");
                }
                var so = new SerializedObject(t);
                Need(so.FindProperty("tuning").objectReferenceValue != null, t, "tuning is not set");
                Need(root.GetComponentsInChildren<TransitMouth>(true).Any(m => new SerializedObject(m).FindProperty("transit").objectReferenceValue == t), t,
                     "no TransitMouth leads into it");
                Networked(t, typeof(NetworkBombTransit));
            }
            foreach (var m in root.GetComponentsInChildren<TransitMouth>(true))
            {
                var transit = new SerializedObject(m).FindProperty("transit").objectReferenceValue as BombTransit;
                Need(transit != null, m, "transit is not set");
                if (transit != null) Need(new SerializedObject(m).FindProperty("exit").intValue < transit.ExitCount, m, "exit index has no exit");
            }
            foreach (var g in root.GetComponentsInChildren<BombGate>(true)) Networked(g, typeof(NetworkBombGate));
            foreach (var z in root.GetComponentsInChildren<Zone>(true))
            {
                Need(z.Volume != null && z.Volume.isTrigger, z, "volume must be a trigger BoxCollider");
                Need(z.gameObject.layer == LayerMask.NameToLayer("Trigger"), z, "a zone sits on the Trigger layer");
            }
            foreach (var k in root.GetComponentsInChildren<KillZone>(true))
            {
                var c = k.GetComponent<Collider>();
                Need(c != null && c.isTrigger, k, "a KillZone needs a trigger collider");
            }

            // Colliders: box-like, on gameplay layers, never under a hand-made visual; a lethal solid has a kill trigger.
            var layers = GameplayLayers.Select(LayerMask.NameToLayer).ToHashSet();
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                Need(c is not MeshCollider, c, "no MeshCollider (box colliders only: the bomb's contact must be predictable)");
                Need(layers.Contains(c.gameObject.layer), c, $"collider on layer '{LayerMask.LayerToName(c.gameObject.layer)}' (use {string.Join(", ", GameplayLayers)})");
                Need(!CustomVisual.Covers(c), c, "a collider under a CustomVisual (visuals never collide: put colliders beside the visual)");
            }
            bool lethalSolid = root.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger && c.gameObject.layer == LayerMask.NameToLayer("Hazard"));
            if (lethalSolid && root.GetComponentInChildren<SignalActuator>(true) == null)
                Need(root.GetComponentsInChildren<KillZone>(true).Length > 0, root.transform, "a Hazard part needs a KillZone trigger around it");
            return problems;

            void Networked(Component c, System.Type companion)
            {
                Need(c.GetComponent(companion) != null, c, companion.Name + " is missing (the host replicates this system)");
                Need(c.GetComponentInParent<NetworkObject>(true) != null, c, "a NetworkObject is missing");
            }
        }

        static bool IsKinematic(Transform t)
        {
            var body = t.GetComponentInParent<Rigidbody>(true);
            return body != null && body.isKinematic;
        }

        static string Path(GameObject root, Transform t)
        {
            var names = new List<string>();
            for (; t != null && t != root.transform.parent; t = t.parent) names.Insert(0, t.name);
            return string.Join("/", names);
        }

        /// <summary>Every prefab under <c>Assets/</c> that holds an obstacle system.</summary>
        public static IEnumerable<string> ObstaclePrefabPaths() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => { var go = AssetDatabase.LoadAssetAtPath<GameObject>(p); return go != null && IsObstacle(go); });

        [MenuItem("HotPatata/Kit/Validate Selected Prefabs")]
        static void ValidateSelected() => Report(Selection.gameObjects.Select(g => AssetDatabase.GetAssetPath(g)).Where(p => p.EndsWith(".prefab")));

        [MenuItem("HotPatata/Kit/Validate All Obstacle Prefabs")]
        static void ValidateAll() => Report(ObstaclePrefabPaths());

        static void Report(IEnumerable<string> paths)
        {
            int count = 0, bad = 0;
            foreach (var path in paths)
            {
                count++;
                var problems = Validate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                if (problems.Count == 0) continue;
                bad++;
                Debug.LogError($"[ObstaclePrefabValidator] {path}\n  " + string.Join("\n  ", problems), AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            Debug.Log($"[ObstaclePrefabValidator] {count} prefab(s) checked, {bad} with problems");
        }
    }
}
