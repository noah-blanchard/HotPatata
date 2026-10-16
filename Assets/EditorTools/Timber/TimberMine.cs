using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// The old mine's timber and the lanes' markers (ARCHITECTURE §27.2): mine frames along a line (<see cref="MineFrameShape"/>), a mine
    /// track (<see cref="MineRailShape"/>) and lane markers (<see cref="LaneMarkerShape"/>), drawn with the timber kit's boards, beams,
    /// iron and rope, so they freeze into elements like the rest. Posts collide as boxes on Environment; the rest is drawn only. Lanterns
    /// are the prop library's (prefab instances, carried over when a piece is frozen).
    /// </summary>
    public static class TimberMine
    {
        const int Plank = TimberKit.Plank, Aged = TimberKit.PlankAged, Wood = TimberKit.Timber, Iron = TimberKit.Iron, Rope = TimberKit.Rope;
        public const string LanternPrefab = "Assets/Prefabs/Props/Lanterns/Lantern_01.prefab";
        /// <summary>How far a lantern hangs under what holds it (m).</summary>
        public const float LanternDrop = 0.45f;

        // ------------------------------------------------------------------ mine frames

        /// <summary>
        /// Frames every <paramref name="spacing"/> metres along <paramref name="path"/> (the floor's line): two posts on the ground under
        /// them (<paramref name="ground"/>, NaN: the line's height), a cap beam <paramref name="height"/> up, lagging boards from cap to cap.
        /// </summary>
        public static void Frames(Transform parent, string name, IList<Vector3> path, float width, float height, float spacing, bool lagging,
                                  int lanternEvery, bool aged, Func<Vector2, float> ground)
        {
            float length = Curves.Length(path);
            if (length < 0.5f) return;
            int count = Mathf.Max(1, Mathf.FloorToInt(length / spacing) + 1);
            var rng = new Random(TimberKit.Seed(name, 41));
            var caps = new List<(Vector3 left, Vector3 right, float top)>();
            var posts = new List<(Vector3 foot, float height)>();
            var lanterns = new List<Vector3>();
            TimberBuildings.Frame(parent, name, mb =>
            {
                for (int f = 0; f < count; f++)
                {
                    float d = count == 1 ? length * 0.5f : Mathf.Lerp(0f, length, f / (float)(count - 1));
                    var (p, tangent) = Curves.At(path, d);
                    var flat = new Vector3(tangent.x, 0f, tangent.z);
                    if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
                    var right = Vector3.Cross(Vector3.up, flat.normalized);
                    float capY = p.y + height;
                    var jitter = (float)(rng.NextDouble() - 0.5) * 0.08f;
                    Vector3 Foot(float side)
                    {
                        var at = p + right * side * (width * 0.5f + 0.14f);
                        float g = ground(new Vector2(at.x, at.z));
                        return new Vector3(at.x, float.IsNaN(g) ? p.y : Mathf.Min(g, capY - 1.2f), at.z);
                    }
                    var l = Foot(-1f);
                    var r = Foot(1f);
                    using (ElementRecorder.Tag("Posts", TimberElementKind.Post, TimberWalk.None, solid: true))
                    {
                        // posts lean in a touch at the top, as mine sets do
                        mb.Beam(l + Vector3.down * 0.3f, new Vector3(l.x, capY + 0.02f, l.z) + right * 0.08f, 0.24f + jitter, 0.24f, Wood);
                        mb.Beam(r + Vector3.down * 0.3f, new Vector3(r.x, capY + 0.02f, r.z) - right * 0.08f, 0.24f - jitter, 0.24f, Wood);
                    }
                    posts.Add((l, capY - l.y));
                    posts.Add((r, capY - r.y));
                    var capL = p + right * -(width * 0.5f + 0.45f) + Vector3.up * (height + 0.15f);
                    var capR = p + right * (width * 0.5f + 0.45f) + Vector3.up * (height + 0.15f);
                    using (ElementRecorder.Tag("Caps", TimberElementKind.Beam, TimberWalk.None, solid: false))
                        mb.Beam(capL, capR, 0.28f, 0.3f, Wood);
                    caps.Add((capL, capR, capY + 0.3f));
                    if (lanternEvery > 0 && f % lanternEvery == lanternEvery / 2)
                    {
                        var hook = p + Vector3.up * height;
                        using (ElementRecorder.Tag("Lanterns", TimberElementKind.Rope, TimberWalk.None, solid: false))
                            mb.Tube(new[] { hook, hook + Vector3.down * LanternDrop }, new[] { 0.02f, 0.02f }, 5, Rope);
                        lanterns.Add(hook + Vector3.down * LanternDrop);
                    }
                }
                if (!lagging || caps.Count < 2) return;
                using (ElementRecorder.Tag("Lagging", TimberElementKind.Plank, TimberWalk.None, solid: false))
                    for (int c = 0; c < caps.Count - 1; c++)
                    {
                        var a = caps[c];
                        var b = caps[c + 1];
                        float span = Vector3.Distance(a.left, a.right);
                        int boards = Mathf.Max(2, Mathf.FloorToInt(span / 0.34f));
                        for (int k = 0; k < boards; k++)
                        {
                            if (rng.NextDouble() < 0.12) continue;   // a board gone: the old roof shows its gaps
                            float t = (k + 0.5f) / boards;
                            var from = Vector3.Lerp(a.left, a.right, t) + Vector3.up * 0.17f;
                            var to = Vector3.Lerp(b.left, b.right, t) + Vector3.up * 0.17f;
                            var mid = (from + to) * 0.5f;
                            var rot = Quaternion.LookRotation(to - from) * Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() - 0.5) * 6f);
                            mb.Box(mid, rot, new Vector3(0.3f, 0.05f, Vector3.Distance(from, to) + 0.35f), k % 3 == 0 ? Plank : aged ? Aged : Plank,
                                   new Vector2(k * 0.37f, c * 0.53f));
                        }
                    }
            });
            for (int i = 0; i < posts.Count; i++)
            {
                var (foot, h) = posts[i];
                ElementRecorder.Replaced(TimberBuildings.SolidInvisible(parent, $"{name} post {i + 1}", foot + Vector3.up * h * 0.5f, Quaternion.identity,
                                                                        new Vector3(0.26f, h, 0.26f)));
            }
            foreach (var at in lanterns) Lantern(parent, name + " lantern", at);
        }

        /// <summary>A lantern of the prop library hung with its top at <paramref name="hook"/> (a prefab instance; nothing when it is missing).</summary>
        public static GameObject Lantern(Transform parent, string name, Vector3 hook)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LanternPrefab);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.identity;
            var bounds = Bounds(go);
            go.transform.position = hook + (go.transform.position - new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            return go;
        }

        static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        // ------------------------------------------------------------------ mine track

        /// <summary>Sleepers on the ground along <paramref name="path"/> and two iron rails on them, <paramref name="gauge"/> apart.</summary>
        public static void Track(Transform parent, string name, IList<Vector3> path, float gauge, float decay, Func<Vector2, float> ground)
        {
            float length = Curves.Length(path);
            if (length < 0.5f) return;
            var rng = new Random(TimberKit.Seed(name, 43));
            int sleepers = Mathf.Max(2, Mathf.FloorToInt(length / 0.62f) + 1);
            var left = new List<Vector3>();
            var rightRail = new List<Vector3>();
            TimberBuildings.Frame(parent, name, mb =>
            {
                for (int s = 0; s < sleepers; s++)
                {
                    var (p, tangent) = Curves.At(path, Mathf.Lerp(0f, length, s / (float)(sleepers - 1)));
                    var flat = new Vector3(tangent.x, 0f, tangent.z);
                    if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
                    var right = Vector3.Cross(Vector3.up, flat.normalized);
                    float g = ground(new Vector2(p.x, p.z));
                    var at = new Vector3(p.x, float.IsNaN(g) ? p.y : g, p.z);
                    left.Add(at - right * gauge * 0.5f + Vector3.up * 0.17f);
                    rightRail.Add(at + right * gauge * 0.5f + Vector3.up * 0.17f);
                    if (rng.NextDouble() < decay * 0.5) continue;   // a sleeper rotted away
                    float turn = rng.NextDouble() < decay ? (float)(rng.NextDouble() - 0.5) * 24f : (float)(rng.NextDouble() - 0.5) * 4f;
                    using (ElementRecorder.Tag("Sleepers", TimberElementKind.Plank, TimberWalk.None, solid: false))
                        mb.Box(at + Vector3.up * 0.06f, Quaternion.LookRotation(right) * Quaternion.Euler(0f, turn, 0f), new Vector3(0.22f, 0.12f, gauge + 0.6f),
                               s % 3 == 0 ? Wood : Aged, new Vector2(s * 0.29f, 0f));
                }
                using (ElementRecorder.Tag("Rails", TimberElementKind.Iron, TimberWalk.None, solid: false))
                    foreach (var rail in new[] { left, rightRail })
                        for (int i = 0; i < rail.Count - 1; i++) mb.Beam(rail[i], rail[i + 1], 0.06f, 0.09f, Iron);
            });
        }

        // ------------------------------------------------------------------ lane markers

        /// <summary>
        /// A lane marker at <paramref name="foot"/> facing the turn's forward: a post with an arm (or two posts and a lintel over the path),
        /// a round board with the lane's glyph on it, ribbons of its colour, a lantern under the arm or the lintel.
        /// </summary>
        public static void LaneMarker(Transform parent, string name, Vector3 foot, Quaternion rot, Lane lane, LaneMarkerForm form, float span, float height, bool lantern)
        {
            var facing = rot * Vector3.forward;
            var side = rot * Vector3.right;
            var faceRot = Quaternion.LookRotation(Vector3.up, facing);
            var glyph = lane == Lane.Sun ? SignGlyph.Sun : SignGlyph.Moon;
            var paint = lane == Lane.Sun ? TimberPaintColor.Amber : TimberPaintColor.Blue;
            const float board = 0.42f;
            Vector3 boardAt, hook;
            var ribbons = new List<Vector3>();
            if (form == LaneMarkerForm.Arch)
            {
                float half = span * 0.5f + 0.15f;
                boardAt = foot + Vector3.up * (height + 0.55f);
                hook = foot + Vector3.up * (height - 0.05f) + side * span * 0.3f;
                TimberBuildings.Frame(parent, name, mb =>
                {
                    using (ElementRecorder.Tag("Posts", TimberElementKind.Post, TimberWalk.None, solid: true))
                        foreach (float s in new[] { -1f, 1f }) TimberBuildings.Post(mb, foot + side * s * half, height + 0.35f, 0.24f);
                    using (ElementRecorder.Tag("Lintel", TimberElementKind.Beam, TimberWalk.None, solid: false))
                    {
                        mb.Beam(foot + side * -(half + 0.4f) + Vector3.up * (height + 0.2f), foot + side * (half + 0.4f) + Vector3.up * (height + 0.2f), 0.26f, 0.3f, Wood);
                        foreach (float s in new[] { -1f, 1f })
                            mb.Beam(foot + side * s * half + Vector3.up * (height - 0.9f), foot + side * s * (half - 0.8f) + Vector3.up * (height + 0.05f), 0.14f, 0.16f, Wood);
                    }
                    Board(mb, boardAt, facing, board);
                    if (lantern) Hanger(mb, hook);
                });
                foreach (float s in new[] { -1f, 1f }) ribbons.Add(foot + side * s * half + facing * 0.14f + Vector3.up * (height - 0.2f));
                foreach (float s in new[] { -1f, 1f })
                    ElementRecorder.Replaced(TimberBuildings.SolidInvisible(parent, $"{name} post {s}", foot + side * s * half + Vector3.up * (height + 0.35f) * 0.5f, rot,
                                                                            new Vector3(0.26f, height + 0.35f, 0.26f)));
            }
            else
            {
                boardAt = foot + Vector3.up * (height - 0.2f) + facing * 0.16f;
                hook = foot + Vector3.up * (height - 0.55f) + side * 0.75f;
                TimberBuildings.Frame(parent, name, mb =>
                {
                    using (ElementRecorder.Tag("Post", TimberElementKind.Post, TimberWalk.None, solid: true))
                        TimberBuildings.Post(mb, foot, height + 0.25f, 0.2f);
                    if (lantern)
                        using (ElementRecorder.Tag("Arm", TimberElementKind.Beam, TimberWalk.None, solid: false))
                        {
                            mb.Beam(foot + Vector3.up * (height - 0.45f) - side * 0.1f, foot + Vector3.up * (height - 0.45f) + side * 0.95f, 0.12f, 0.14f, Wood);
                            mb.Beam(foot + Vector3.up * (height - 1.1f), foot + Vector3.up * (height - 0.5f) + side * 0.5f, 0.09f, 0.1f, Wood);
                        }
                    Board(mb, boardAt, facing, board);
                    if (lantern) Hanger(mb, hook);
                });
                ribbons.Add(foot + facing * 0.12f + Vector3.up * (height - 0.75f));
                ElementRecorder.Replaced(TimberBuildings.SolidInvisible(parent, name + " post", foot + Vector3.up * (height + 0.25f) * 0.5f, rot, new Vector3(0.22f, height + 0.25f, 0.22f)));
            }
            TimberStructures.Glyph(parent, name, boardAt + facing * 0.035f, faceRot, glyph, board * 0.8f);
            int r = 0;
            foreach (var at in ribbons)
                foreach (float dx in new[] { -0.07f, 0.07f })
                {
                    // two strips tied at the top, hanging and splayed a little
                    float length = 0.85f + 0.18f * (r++ % 2);
                    var ribbon = new List<Vector2> { new Vector2(-0.05f, -length), new Vector2(0.05f, -length + 0.06f), new Vector2(0.06f, 0f), new Vector2(-0.06f, 0f) };
                    TimberStructures.Paint(parent, name + " ribbon", at + side * dx, faceRot * Quaternion.Euler(0f, dx * 90f, 0f), ribbon, paint);
                }
            if (lantern) Lantern(parent, name + " lantern", hook + Vector3.down * LanternDrop);
        }

        /// <summary>A round board facing <paramref name="facing"/> at <paramref name="center"/> (a disc of planking with an iron rim).</summary>
        static void Board(MeshBuilder mb, Vector3 center, Vector3 facing, float radius)
        {
            using (ElementRecorder.Tag("Board", TimberElementKind.Plank, TimberWalk.None, solid: false))
                mb.Tube(new[] { center - facing * 0.03f, center + facing * 0.03f }, new[] { radius, radius }, 18, Plank, true);
            using (ElementRecorder.Tag(kind: TimberElementKind.Iron))
            {
                var up = Vector3.up;
                var across = Vector3.Cross(up, facing).normalized;
                var rim = TimberStructures.Circle(center + facing * 0.035f, radius + 0.01f, 18, up, across);
                mb.Tube(rim, rim.Select(_ => 0.02f).ToList(), 5, Iron);
            }
        }

        /// <summary>The short rope a lantern hangs from.</summary>
        static void Hanger(MeshBuilder mb, Vector3 hook)
        {
            using (ElementRecorder.Tag("Lantern", TimberElementKind.Rope, TimberWalk.None, solid: false))
                mb.Tube(new[] { hook, hook + Vector3.down * LanternDrop }, new[] { 0.02f, 0.02f }, 5, Rope);
        }
    }
}
