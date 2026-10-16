using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The timber structures of the library (ARCHITECTURE §27.1): a shelter, a gantry, a sign, a pulley beam and the winch drum, each
    /// drawn from a place, a turn and its settings (no level, no course constant), with the timber kit's boards, beams and ropes.
    /// What a body must not pass (posts, legs, low walls) collides as boxes on Environment; the rest is drawn only. The authored levels'
    /// pieces (<see cref="ShelterShape"/>, <see cref="GantryShape"/>, <see cref="SignShape"/>, <see cref="PulleyBeamShape"/>) and any
    /// builder draw them here; frozen, they are elements like the rest of the timber.
    /// </summary>
    public static class TimberStructures
    {
        const int Plank = TimberKit.Plank, Aged = TimberKit.PlankAged, Wood = TimberKit.Timber, Iron = TimberKit.Iron, Rope = TimberKit.Rope;

        // ------------------------------------------------------------------ shelter

        /// <summary>A shelter at <paramref name="foot"/> turned by <paramref name="rot"/>: four posts, plates, its roof, low walls, a drum.</summary>
        public static void Shelter(Transform parent, string name, Vector3 foot, Quaternion rot, Vector2 size, float height, ShelterRoof roof, float rise,
                                   float lowWalls, bool drum, bool aged)
        {
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            Vector3 P(float x, float y, float z) => foot + rot * new Vector3(x, y, z);
            float PostHeight(float sz) => roof == ShelterRoof.Lean && sz > 0f ? height + rise : height;
            TimberBuildings.Frame(parent, name, mb =>
            {
                using (ElementRecorder.Tag("Posts", TimberElementKind.Post, TimberWalk.None, solid: true))
                    foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                        TimberBuildings.Post(mb, P(sx * hx, 0f, sz * hz), PostHeight(sz), 0.2f);
                using (ElementRecorder.Tag("Frame", TimberElementKind.Beam, TimberWalk.None, solid: false))
                {
                    foreach (float sz in new[] { -1f, 1f })
                        mb.Beam(P(-hx - 0.2f, PostHeight(sz), sz * hz), P(hx + 0.2f, PostHeight(sz), sz * hz), 0.16f, 0.2f, Wood);
                    foreach (float sx in new[] { -1f, 1f })
                        mb.Beam(P(sx * hx, PostHeight(-1f), -hz - 0.2f), P(sx * hx, PostHeight(1f), hz + 0.2f), 0.16f, 0.2f, Wood);
                }
                using (ElementRecorder.Tag("Roof", TimberElementKind.Plank, TimberWalk.None, solid: false))
                {
                    if (roof == ShelterRoof.Gable) TimberBuildings.Roof(mb, P(0f, height + 0.15f, 0f), hx, hz, rise, rot, aged ? Aged : Plank);
                    else if (roof == ShelterRoof.Lean) LeanRoof(mb, foot, rot, hx, hz, height, rise, aged);
                }
                if (drum)
                    using (ElementRecorder.Tag("Winch", TimberElementKind.Log, TimberWalk.None, solid: false))
                        Drum(mb, P(0f, 1.15f, hz - 0.35f), rot);
            });
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                ElementRecorder.Replaced(TimberBuildings.SolidInvisible(parent, $"{name} post {sx}{sz}", P(sx * hx, PostHeight(sz) * 0.5f, sz * hz), rot,
                                                                        new Vector3(0.22f, PostHeight(sz), 0.22f)));
            if (lowWalls > 0.2f)
            {
                Vector2 F(float x, float z) { var w = P(x, 0f, z); return new Vector2(w.x, w.z); }
                TimberBuildings.PlankWall(parent, name + " back wall", F(-hx, hz), F(hx, hz), foot.y, foot.y + lowWalls,
                                          new (float, float, float, float)[0], aged ? 2 : 4);
                TimberBuildings.PlankWall(parent, name + " left wall", F(-hx, -hz), F(-hx, hz), foot.y, foot.y + lowWalls,
                                          new (float, float, float, float)[0], aged ? 2 : 4);
                TimberBuildings.PlankWall(parent, name + " right wall", F(hx, hz), F(hx, -hz), foot.y, foot.y + lowWalls,
                                          new (float, float, float, float)[0], aged ? 2 : 4);
            }
        }

        /// <summary>A lean-to roof: boards falling from the back (high) to the front, a little past both.</summary>
        static void LeanRoof(MeshBuilder mb, Vector3 foot, Quaternion rot, float hx, float hz, float height, float rise, bool aged)
        {
            float run = 2f * hz + 0.5f;
            float pitch = Mathf.Atan2(rise, 2f * hz) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(run * run + rise * rise);
            int boards = Mathf.CeilToInt((2f * hx + 0.5f) / 0.3f);
            for (int k = 0; k < boards; k++)
            {
                float x = -hx - 0.25f + (k + 0.5f) * (2f * hx + 0.5f) / boards;
                var center = foot + rot * new Vector3(x, height + rise * 0.5f + 0.12f, 0f);
                mb.Box(center, rot * Quaternion.Euler(-pitch, 0f, 0f), new Vector3(0.29f, 0.05f, length), k % 3 == 0 ? Plank : aged ? Aged : Plank,
                       new Vector2(k * 0.37f, 0.4f));
            }
        }

        /// <summary>A winch drum with its crank: a log drum on two cheeks on legs, an iron crank handle, rope wound on it.</summary>
        public static void Drum(MeshBuilder mb, Vector3 center, Quaternion rot, float length = 1.3f, float radius = 0.28f)
        {
            var axis = rot * Vector3.right;
            mb.Tube(new[] { center - axis * length * 0.5f, center + axis * length * 0.5f }, new[] { radius, radius }, 14, Wood, true);
            foreach (float s in new[] { -1f, 1f })
            {
                var cheek = center + axis * s * (length * 0.5f + 0.08f);
                mb.Tube(new[] { cheek - axis * 0.05f, cheek + axis * 0.05f }, new[] { radius + 0.12f, radius + 0.12f }, 14, Wood, true);
                using (ElementRecorder.Tag(kind: TimberElementKind.Post))
                    mb.Beam(cheek + Vector3.down * 1.1f, cheek + Vector3.up * 0.1f, 0.14f, 0.14f, Wood);
            }
            var hub = center + axis * (length * 0.5f + 0.25f);
            using (ElementRecorder.Tag(kind: TimberElementKind.Iron))
            {
                mb.Tube(new[] { center, hub }, new[] { 0.05f, 0.05f }, 8, Iron, true);
                mb.Beam(hub, hub + rot * Vector3.up * 0.45f, 0.06f, 0.06f, Iron);
            }
            var arm = rot * Vector3.up * 0.45f;
            mb.Tube(new[] { hub + arm, hub + arm + axis * 0.3f }, new[] { 0.04f, 0.04f }, 8, Wood, true);
            using (ElementRecorder.Tag(kind: TimberElementKind.Rope))
                for (int k = 0; k < 7; k++)
                {
                    float x = -length * 0.35f + k * length * 0.1f;
                    var ring = Circle(center + axis * x, radius + 0.03f, 12, rot * Vector3.up, rot * Vector3.forward);
                    mb.Tube(ring, ring.Select(_ => 0.03f).ToList(), 5, Rope);
                }
        }

        // ------------------------------------------------------------------ small shapes (shared with the kit's forest looks)

        /// <summary>A circle of <paramref name="samples"/> segments (closed: its last point is its first) in the plane of two axes.</summary>
        public static List<Vector3> Circle(Vector3 center, float radius, int samples, Vector3 axisA, Vector3 axisB)
        {
            var list = new List<Vector3>();
            for (int i = 0; i <= samples; i++)
            {
                float a = i / (float)samples * Mathf.PI * 2f;
                list.Add(center + (axisA * Mathf.Cos(a) + axisB * Mathf.Sin(a)) * radius);
            }
            return list;
        }

        /// <summary>A log from <paramref name="a"/> to <paramref name="b"/>, a little thinner at its far end, its ends closed.</summary>
        public static void Log(MeshBuilder mb, Vector3 a, Vector3 b, float radius, float shift = 0f) =>
            mb.Tube(new[] { a, b }, new[] { radius, radius * 0.92f }, 9, Wood, true, shift);

        /// <summary>A rim of short planks lying round <paramref name="center"/> at <paramref name="radius"/> (a plate's pad, a fire ring).</summary>
        public static void PlankRim(MeshBuilder mb, Vector3 center, float radius)
        {
            int boards = Mathf.CeilToInt(2f * Mathf.PI * radius / 0.32f);
            for (int k = 0; k < boards; k++)
            {
                var rot = Quaternion.Euler(0f, k / (float)boards * 360f, 0f);
                mb.Box(center + rot * new Vector3(0f, 0.03f, radius), rot, new Vector3(0.3f, 0.06f, 0.36f), k % 4 == 1 ? Aged : Plank, new Vector2(k * 0.37f, 0f));
            }
        }

        /// <summary>
        /// Planks across a box of <paramref name="size"/> (centred, its top at +y/2, the planks along x), two stringers under them and,
        /// if asked, iron straps over them: a platform's deck.
        /// </summary>
        public static void PlankDeck(MeshBuilder mb, Vector3 size, bool straps)
        {
            float top = size.y / 2f;
            int boards = Mathf.CeilToInt(size.z / 0.26f);
            for (int k = 0; k < boards; k++)
            {
                float z = -size.z / 2f + (k + 0.5f) * size.z / boards;
                mb.Box(new Vector3(0f, top - 0.03f, z), Quaternion.identity, new Vector3(size.x - 0.02f, 0.06f, size.z / boards - 0.02f), k % 4 == 1 ? Aged : Plank,
                       new Vector2(k * 0.31f, k * 0.17f));
            }
            foreach (float x in new[] { -size.x / 2f + 0.18f, size.x / 2f - 0.18f })
                mb.Box(new Vector3(x, top - 0.06f - (size.y - 0.06f) / 2f, 0f), Quaternion.identity, new Vector3(0.22f, size.y - 0.06f, size.z - 0.04f), Wood);
            if (!straps) return;
            foreach (float z in new[] { -size.z * 0.3f, 0f, size.z * 0.3f })
                mb.Box(new Vector3(0f, top + 0.005f, z), Quaternion.identity, new Vector3(size.x - 0.1f, 0.02f, 0.08f), Iron);
        }

        // ------------------------------------------------------------------ gantry

        /// <summary>
        /// A gantry at <paramref name="foot"/>: two pairs of legs <paramref name="span"/> apart (local x), braced, a head frame at
        /// <paramref name="height"/>, knee braces, a pulley wheel over the middle with its rope hanging <paramref name="ropeDrop"/>, a roof.
        /// </summary>
        public static void Gantry(Transform parent, string name, Vector3 foot, Quaternion rot, float span, float depth, float height, bool pulley, float ropeDrop, bool roof)
        {
            float half = span * 0.5f, dz = depth * 0.5f, top = height;
            Vector3 P(float x, float y, float z) => foot + rot * new Vector3(x, y, z);
            TimberBuildings.Frame(parent, name, mb =>
            {
                using (ElementRecorder.Tag("Legs", TimberElementKind.Post, TimberWalk.None, solid: true))
                    foreach (float sx in new[] { -1f, 1f })
                        foreach (float sz in new[] { -dz, dz })
                            TimberBuildings.Post(mb, P(sx * half, 0f, sz), top + 0.4f, 0.32f);
                using (ElementRecorder.Tag("Braces", TimberElementKind.Brace, TimberWalk.None, solid: false))
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        mb.Beam(P(sx * half, 1.2f, -dz), P(sx * half, top - 1f, dz), 0.14f, 0.18f, Wood);
                        mb.Beam(P(sx * half, 1.2f, dz), P(sx * half, top - 1f, -dz), 0.14f, 0.18f, Wood);
                    }
                using (ElementRecorder.Tag("Head", TimberElementKind.Beam, TimberWalk.None, solid: false))
                    foreach (float sz in new[] { -dz, dz })
                    {
                        mb.Beam(P(-half - 0.4f, top, sz), P(half + 0.4f, top, sz), 0.26f, 0.32f, Wood);
                        float knee = Mathf.Min(1.2f, half * 0.5f);
                        mb.Beam(P(-half, top - 2.2f, sz), P(-half + knee, top - 0.15f, sz), 0.16f, 0.18f, Wood);
                        mb.Beam(P(half, top - 2.2f, sz), P(half - knee, top - 0.15f, sz), 0.16f, 0.18f, Wood);
                    }
                if (roof)
                    using (ElementRecorder.Tag("Roof", TimberElementKind.Plank, TimberWalk.None, solid: false))
                        TimberBuildings.Roof(mb, P(0f, top + 0.2f, 0f), dz + 0.15f, half + 0.3f, 0.8f, rot * Quaternion.Euler(0f, 90f, 0f), Aged, 0.3f);
                if (pulley)
                    using (ElementRecorder.Tag("Pulley", TimberElementKind.Trim, TimberWalk.None, solid: false))
                    {
                        var wheel = P(0f, top - 0.75f, 0f);
                        mb.Lathe(wheel, rot * Quaternion.Euler(90f, 0f, 0f), WheelProfile(0.42f), 14, Wood);
                        using (ElementRecorder.Tag(kind: TimberElementKind.Iron))
                            foreach (float sz in new[] { -0.12f, 0.12f })
                                mb.Beam(P(0f, top - 0.1f, sz), P(0f, top - 0.75f, sz), 0.05f, 0.05f, Iron);
                        if (ropeDrop > 0.05f)
                            using (ElementRecorder.Tag(kind: TimberElementKind.Rope))
                                mb.Tube(new[] { P(0.4f, top - 0.75f, 0f), P(0.4f, top - 0.75f - ropeDrop, 0f) }, new[] { 0.03f, 0.03f }, 6, Rope);
                    }
            });
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -dz, dz })
                    ElementRecorder.Replaced(TimberBuildings.SolidInvisible(parent, $"{name} leg {sx}{sz}", P(sx * half, (top + 0.1f) * 0.5f, sz), rot,
                                                                            new Vector3(0.34f, top + 0.1f, 0.34f)));
        }

        /// <summary>A pulley wheel's half profile (radius, height): a grooved rim on a hub.</summary>
        public static Vector2[] WheelProfile(float radius) => new[]
        {
            new Vector2(0.05f, -0.08f), new Vector2(radius, -0.08f), new Vector2(radius - 0.06f, 0f), new Vector2(radius, 0.08f), new Vector2(0.05f, 0.08f),
        };

        // ------------------------------------------------------------------ pulley beam

        /// <summary>
        /// A pulley beam at <paramref name="foot"/> reaching <paramref name="reach"/> along the turn's forward at <paramref name="height"/>:
        /// on its own post (braced to it) or bolted to what stands at the foot (braced below), its wheel at the tip, a rope hanging from it.
        /// </summary>
        public static void PulleyBeam(Transform parent, string name, Vector3 foot, Quaternion rot, float reach, float height, bool post, float ropeDrop)
        {
            Vector3 P(float x, float y, float z) => foot + rot * new Vector3(x, y, z);
            TimberBuildings.Frame(parent, name, mb =>
            {
                if (post)
                    using (ElementRecorder.Tag("Post", TimberElementKind.Post, TimberWalk.None, solid: true))
                        TimberBuildings.Post(mb, foot, height + 0.3f, 0.26f);
                using (ElementRecorder.Tag("Beam", TimberElementKind.Beam, TimberWalk.None, solid: false))
                {
                    mb.Beam(P(0f, height, post ? -0.25f : 0f), P(0f, height, reach + 0.6f), 0.24f, 0.3f, Wood);
                    using (ElementRecorder.Tag(kind: TimberElementKind.Brace))
                        mb.Beam(P(0f, height - (post ? 1.6f : 2.8f), post ? 0.1f : 0.4f), P(0f, height - 0.12f, Mathf.Min(reach * 0.62f, 2.2f)), 0.16f, 0.18f, Wood);
                }
                using (ElementRecorder.Tag("Pulley", TimberElementKind.Trim, TimberWalk.None, solid: false))
                {
                    var wheel = P(0f, height - 0.45f, reach);
                    mb.Lathe(wheel, rot * Quaternion.Euler(0f, 0f, 90f), WheelProfile(0.38f), 14, Wood);
                    using (ElementRecorder.Tag(kind: TimberElementKind.Iron))
                        foreach (float sx in new[] { -0.12f, 0.12f })
                            mb.Beam(P(sx, height - 0.1f, reach), P(sx, height - 0.45f, reach), 0.05f, 0.05f, Iron);
                    if (ropeDrop > 0.05f)
                        using (ElementRecorder.Tag(kind: TimberElementKind.Rope))
                            mb.Tube(new[] { P(0f, height - 0.45f, reach + 0.36f), P(0f, height - 0.45f - ropeDrop, reach + 0.36f) }, new[] { 0.03f, 0.03f }, 6, Rope);
                }
            });
            if (post)
                ElementRecorder.Replaced(TimberBuildings.SolidInvisible(parent, name + " post", P(0f, (height + 0.3f) * 0.5f, 0f), rot, new Vector3(0.28f, height + 0.3f, 0.28f)));
        }

        // ------------------------------------------------------------------ sign

        /// <summary>
        /// A sign at <paramref name="foot"/>, its face toward the turn's forward: a board (<paramref name="board"/> wide and high, its middle
        /// <paramref name="boardHeight"/> up) on one or two posts, its glyph painted on it, a little roof over it.
        /// </summary>
        public static void Sign(Transform parent, string name, Vector3 foot, Quaternion rot, int posts, Vector2 board, float boardHeight, SignGlyph glyph, bool roof)
        {
            var facing = rot * Vector3.forward;
            var side = rot * Vector3.right;
            float top = boardHeight + board.y * 0.5f;
            var faceRot = Quaternion.LookRotation(Vector3.up, facing);   // x right, z up the board, y out of its face
            TimberBuildings.Frame(parent, name, mb =>
            {
                using (ElementRecorder.Tag("Posts", TimberElementKind.Post, TimberWalk.None, solid: true))
                {
                    if (posts >= 2)
                        foreach (float s in new[] { -1f, 1f }) TimberBuildings.Post(mb, foot + side * s * (board.x * 0.5f - 0.1f) - facing * 0.12f, top + (roof ? 0.35f : 0.1f), 0.14f);
                    else TimberBuildings.Post(mb, foot - facing * 0.12f, top + (roof ? 0.35f : 0.1f), 0.16f);
                }
                using (ElementRecorder.Tag("Board", TimberElementKind.Plank, TimberWalk.None, solid: false))
                    mb.Box(foot + Vector3.up * boardHeight - facing * 0.03f, faceRot, new Vector3(board.x, 0.06f, board.y), Plank);
                if (roof)
                    using (ElementRecorder.Tag("Roof", TimberElementKind.Plank, TimberWalk.None, solid: false))
                        TimberBuildings.Roof(mb, foot + Vector3.up * (top + 0.25f) - facing * 0.05f, 0.3f, board.x * 0.5f + 0.1f, 0.25f, Quaternion.LookRotation(side), Aged, 0.2f);
            });
            Glyph(parent, name, foot + Vector3.up * boardHeight + facing * 0.005f, faceRot, glyph, Mathf.Min(board.x, board.y) * 0.4f);
            float postHeight = top + (roof ? 0.35f : 0.1f);
            if (posts >= 2)
                foreach (float s in new[] { -1f, 1f })
                    ElementRecorder.Replaced(TimberBuildings.SolidInvisible(parent, $"{name} post {s}", foot + side * s * (board.x * 0.5f - 0.1f) - facing * 0.12f + Vector3.up * postHeight * 0.5f,
                                                                            rot, new Vector3(0.16f, postHeight, 0.16f)));
            else
                ElementRecorder.Replaced(TimberBuildings.SolidInvisible(parent, name + " post", foot - facing * 0.12f + Vector3.up * postHeight * 0.5f, rot, new Vector3(0.18f, postHeight, 0.18f)));
        }

        /// <summary>A glyph painted on a board: its parts at <paramref name="center"/> in the plane of <paramref name="faceRot"/>'s x and z.</summary>
        public static void Glyph(Transform parent, string name, Vector3 center, Quaternion faceRot, SignGlyph glyph, float s)
        {
            switch (glyph)
            {
                case SignGlyph.Owl:
                    Paint(parent, name + " owl", center, faceRot, Poly2.Radial(Vector2.zero, a => s * (1f + 0.08f * Mathf.Cos(2f * a)), 28), TimberPaintColor.Amber);
                    var up = faceRot * Vector3.up;
                    foreach (float e in new[] { -0.36f, 0.36f })
                        Paint(parent, name + " owl eye", center + up * 0.01f, faceRot, Poly2.Radial(new Vector2(e * s, 0.18f * s), _ => 0.2f * s, 14), TimberPaintColor.Blue);
                    break;
                case SignGlyph.Leaf:
                    var leaf = new List<Vector2>();
                    for (int i = 0; i <= 12; i++) { float t = i / 12f; leaf.Add(new Vector2(Mathf.Sin(t * Mathf.PI) * 0.45f * s, Mathf.Lerp(-s, s, t))); }
                    for (int i = 11; i > 0; i--) { float t = i / 12f; leaf.Add(new Vector2(-Mathf.Sin(t * Mathf.PI) * 0.45f * s, Mathf.Lerp(-s, s, t))); }
                    Paint(parent, name + " leaf", center, faceRot, leaf, TimberPaintColor.Green);
                    break;
                case SignGlyph.Axe:
                    var head = new List<Vector2> { new Vector2(-0.15f * s, 0.15f * s), new Vector2(0.55f * s, 0.7f * s), new Vector2(0.75f * s, 0.1f * s), new Vector2(0.55f * s, -0.5f * s), new Vector2(-0.15f * s, -0.05f * s) };
                    Paint(parent, name + " axe", center, faceRot, head, TimberPaintColor.Blue);
                    Paint(parent, name + " axe handle", center, faceRot,
                          new List<Vector2> { new Vector2(-0.12f * s, -s), new Vector2(0.02f * s, -s), new Vector2(0.02f * s, 0.95f * s), new Vector2(-0.12f * s, 0.95f * s) }, TimberPaintColor.Amber);
                    break;
                case SignGlyph.Sun:
                    // a gold disc and eight rays round it
                    Paint(parent, name + " sun", center, faceRot, Poly2.Radial(Vector2.zero, _ => 0.48f * s, 24), TimberPaintColor.Amber);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        var across = new Vector2(-dir.y, dir.x);
                        Paint(parent, name + " sun ray", center, faceRot,
                              new List<Vector2> { dir * 0.6f * s - across * 0.11f * s, dir * s, dir * 0.6f * s + across * 0.11f * s }, TimberPaintColor.Amber);
                    }
                    break;
                case SignGlyph.Moon:
                    Paint(parent, name + " moon", center, faceRot, Crescent(s), TimberPaintColor.Blue);
                    break;
            }
        }

        /// <summary>A crescent moon <paramref name="s"/> in radius, horns up and to the right: the outer arc, then the inner one back.</summary>
        public static List<Vector2> Crescent(float s)
        {
            var list = new List<Vector2>();
            const int samples = 14;
            // the outer circle's left side (from its top horn round to its bottom horn), then a smaller circle's left side, shifted right, back up
            for (int i = 0; i <= samples; i++)
            {
                float a = Mathf.Lerp(60f, 300f, i / (float)samples) * Mathf.Deg2Rad;
                list.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s);
            }
            for (int i = samples - 1; i >= 1; i--)
            {
                float a = Mathf.Lerp(78f, 282f, i / (float)samples) * Mathf.Deg2Rad;
                list.Add(new Vector2(0.42f * s, 0f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.82f * s);
            }
            return list;
        }

        /// <summary>A flat painted shape (its outline in the plane of <paramref name="rot"/>'s x and z, proud along its y), drawn only.</summary>
        public static void Paint(Transform parent, string name, Vector3 center, Quaternion rot, IList<Vector2> outline, TimberPaintColor paint)
        {
            ElementRecorder.Paint(center, rot, outline, paint);
            if (ElementRecorder.FreezeInPlace && ElementRecorder.Active == null)
            {
                var element = new GameObject(name).AddComponent<TimberPaint>();
                element.transform.SetParent(parent, false);
                element.transform.SetPositionAndRotation(center, rot);
                element.Kind = TimberElementKind.Trim;
                element.Outline.Clear();
                element.Outline.AddRange(outline);
                element.Paint = paint;
                TimberElementBuilder.Refresh(element, true);
                return;
            }
            var mb = new MeshBuilder();
            mb.Prism(outline, 0f, TimberPaint.Thickness, 0, 0);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.AddComponent<MeshFilter>().sharedMesh = OrganicAssets.Save(mb.ToMesh(name, out _));
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = NatureMaterialBuilder.Load(TimberElementBuilder.PaintMaterial(paint));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<CourseDecoration>();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
    }
}
