using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The organic kit's buildings (ARCHITECTURE §25.5, §27): plank walls that collide (their doors left open), pitched board roofs,
    /// a log hut and a braced trestle tower. Shared by PatataRanger and the authored levels' pieces. Drawn timber is decoration
    /// (renderers, <see cref="CourseDecoration"/>); what a body must not pass collides as boxes on Environment.
    /// </summary>
    public static class TimberBuildings
    {
        const int Plank = TimberKit.Plank, Aged = TimberKit.PlankAged, Wood = TimberKit.Timber;

        static Vector3 V3(Vector2 v, float y) => new Vector3(v.x, y, v.y);

        /// <summary>A timber frame or any hand-built piece (renderers only): <paramref name="build"/> fills its mesh.</summary>
        public static GameObject Frame(Transform p, string name, Action<MeshBuilder> build)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            var mb = new MeshBuilder(TimberKit.Slots);
            build(mb);
            TimberKit.AddVisual(go, name, mb);
            go.AddComponent<CourseDecoration>();
            return go;
        }

        /// <summary>A box collider on Environment with no renderer (the drawn timber is beside it).</summary>
        public static GameObject SolidInvisible(Transform p, string name, Vector3 center, Quaternion rot, Vector3 size)
        {
            var go = new GameObject(name) { layer = CourseKit.Layer("Environment") };
            go.transform.SetParent(p, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>A square post from its foot (sunk a little) up <paramref name="height"/>.</summary>
        public static void Post(MeshBuilder mb, Vector3 foot, float height, float size = 0.22f) =>
            mb.Beam(foot + Vector3.down * 0.3f, foot + Vector3.up * height, size, size, Wood);

        /// <summary>A pitched roof of boards over a rectangle (centre, half sizes, turned by yaw): two slopes meeting at the ridge.</summary>
        public static void Roof(MeshBuilder mb, Vector3 eaveCenter, float halfX, float halfZ, float rise, Quaternion rot, int slot = Aged, float overhang = 0.45f)
        {
            float hx = halfX + overhang, hz = halfZ + overhang;
            float slope = Mathf.Sqrt(hx * hx + rise * rise);
            float pitch = Mathf.Atan2(rise, hx) * Mathf.Rad2Deg;
            int boards = Mathf.CeilToInt(2f * hz / 0.26f);
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < boards; k++)
                {
                    float z = -hz + (k + 0.5f) * 2f * hz / boards;
                    var mid = new Vector3(side * hx * 0.5f, rise * 0.5f + 0.02f * (k % 2), z);
                    var r = rot * Quaternion.Euler(0f, 0f, -side * pitch) * Quaternion.Euler(0f, 90f, 0f);   // each slope falls from the ridge to its eave
                    mb.Box(eaveCenter + rot * mid, r, new Vector3(0.25f, 0.05f, slope + 0.05f), k % 3 == 0 ? Plank : slot,
                           new Vector2(k * 0.37f, side * 0.71f));
                }
            mb.Box(eaveCenter + rot * new Vector3(0f, rise + 0.05f, 0f), rot, new Vector3(0.18f, 0.16f, 2f * hz + 0.1f), Wood);
        }

        /// <summary>A plank wall from <paramref name="a"/> to <paramref name="b"/> (x, z) between two heights, its openings (distance along it, width, height); it collides.</summary>
        public static void PlankWall(Transform p, string name, Vector2 a, Vector2 b, float y0, float y1, params (float at, float width, float height)[] doors) =>
            PlankWall(p, name, a, b, y0, y1, doors.Select(d => (d.at, d.width, d.height, 0f)).ToArray());

        /// <summary>
        /// A plank wall with openings that may start above the floor (windows): each opening's distance along the wall, width, height and
        /// sill (its bottom above <paramref name="y0"/>).
        /// </summary>
        public static void PlankWall(Transform p, string name, Vector2 a, Vector2 b, float y0, float y1, (float at, float width, float height, float sill)[] doors,
                                     int agedEvery = 4)
        {
            var along = (b - a).normalized;
            float length = Vector2.Distance(a, b);
            var dir3 = new Vector3(along.x, 0f, along.y);
            var normal = new Vector3(along.y, 0f, -along.x);
            var rot = Quaternion.LookRotation(dir3);
            // the solid pieces: between the openings full height, under and above each opening
            var cuts = doors.OrderBy(d => d.at).ToList();
            float x = 0f;
            int piece = 0;
            void Solid(float s0, float s1, float h0, float h1)
            {
                if (s1 - s0 < 0.02f || h1 - h0 < 0.02f) return;
                var c = V3(a + along * ((s0 + s1) / 2f), (h0 + h1) / 2f);
                SolidInvisible(p, $"{name} {++piece}", c, rot, new Vector3(0.24f, h1 - h0, s1 - s0));
            }
            foreach (var d in cuts)
            {
                Solid(x, d.at - d.width / 2f, y0, y1);
                Solid(d.at - d.width / 2f, d.at + d.width / 2f, y0, y0 + d.sill);
                Solid(d.at - d.width / 2f, d.at + d.width / 2f, y0 + d.sill + d.height, y1);
                x = d.at + d.width / 2f;
            }
            Solid(x, length, y0, y1);
            Frame(p, name + " boards", mb =>
            {
                int boards = Mathf.CeilToInt(length / 0.24f);
                var boardRot = Quaternion.LookRotation(Vector3.up, normal);
                for (int k = 0; k < boards; k++)
                {
                    float s = (k + 0.5f) * length / boards;
                    var door = cuts.FirstOrDefault(d => Mathf.Abs(s - d.at) < d.width / 2f);
                    int slot = k % agedEvery == 1 ? Aged : Plank;
                    var shift = new Vector2(k * 0.31f, k * 0.17f);
                    if (door.width > 0f)
                    {
                        // a board under a window, a board over every opening
                        if (door.sill > 0.02f)
                            mb.Box(V3(a + along * s, y0 + door.sill / 2f), boardRot, new Vector3(length / boards - 0.01f, 0.05f, door.sill), slot, shift);
                        float h0 = y0 + door.sill + door.height;
                        mb.Box(V3(a + along * s, (h0 + y1) / 2f), boardRot, new Vector3(length / boards - 0.01f, 0.05f, y1 - h0), slot, shift);
                    }
                    else mb.Box(V3(a + along * s, (y0 + y1) / 2f), boardRot, new Vector3(length / boards - 0.01f, 0.05f, y1 - y0), slot, shift);
                }
                // posts at the ends and round the openings, a sill and a plate beam
                mb.Beam(V3(a, y0 - 0.2f), V3(a, y1 + 0.1f), 0.24f, 0.24f, Wood);
                mb.Beam(V3(b, y0 - 0.2f), V3(b, y1 + 0.1f), 0.24f, 0.24f, Wood);
                foreach (var d in cuts)
                {
                    float bottom = y0 + d.sill;
                    foreach (float e in new[] { -1f, 1f })
                        mb.Beam(V3(a + along * (d.at + e * (d.width / 2f + 0.09f)), d.sill > 0.02f ? bottom - 0.1f : y0),
                                V3(a + along * (d.at + e * (d.width / 2f + 0.09f)), bottom + d.height + 0.2f), 0.18f, 0.18f, Wood);
                    mb.Beam(V3(a + along * (d.at - d.width / 2f - 0.3f), bottom + d.height + 0.1f), V3(a + along * (d.at + d.width / 2f + 0.3f), bottom + d.height + 0.1f), 0.2f, 0.2f, Wood);
                    if (d.sill > 0.02f)
                        mb.Beam(V3(a + along * (d.at - d.width / 2f - 0.2f), bottom - 0.05f), V3(a + along * (d.at + d.width / 2f + 0.2f), bottom - 0.05f), 0.16f, 0.14f, Wood);
                }
                mb.Beam(V3(a, y1 - 0.1f) + normal * 0.08f, V3(b, y1 - 0.1f) + normal * 0.08f, 0.16f, 0.22f, Wood);
            });
        }

        /// <summary>A small log hut: log walls (boxes that collide), a door gap in its +X wall, a pitched roof.</summary>
        public static void Hut(Transform p, string name, Vector3 foot, Quaternion rot, float halfX, float halfZ)
        {
            const float WallH = 2.6f;
            var root = new GameObject(name).transform;
            root.SetParent(p, false);
            Frame(root, name + " frame", mb =>
            {
                // log walls: courses of logs with their ends crossing at the corners
                for (int course = 0; course < 9; course++)
                {
                    float y = 0.15f + course * 0.29f;
                    foreach (int s in new[] { -1, 1 })
                    {
                        bool alongX = course % 2 == 0;
                        var a = foot + rot * (alongX ? new Vector3(-halfX - 0.35f, y, s * halfZ) : new Vector3(s * halfX, y + 0.145f, -halfZ - 0.35f));
                        var b = foot + rot * (alongX ? new Vector3(halfX + 0.35f, y, s * halfZ) : new Vector3(s * halfX, y + 0.145f, halfZ + 0.35f));
                        if (!alongX && s > 0 && course < 7)
                        {
                            // the door in the +X wall
                            mb.Tube(new[] { a, Vector3.Lerp(a, b, 0.36f) }, new[] { 0.16f, 0.16f }, 8, Wood, true, course * 0.2f);
                            mb.Tube(new[] { Vector3.Lerp(a, b, 0.64f), b }, new[] { 0.16f, 0.16f }, 8, Wood, true, course * 0.3f);
                        }
                        else mb.Tube(new[] { a, b }, new[] { 0.16f, 0.16f }, 8, Wood, true, course * 0.37f + s);
                    }
                }
                Roof(mb, foot + Vector3.up * (WallH + 0.1f), halfX, halfZ, 1.5f, rot * Quaternion.Euler(0f, 90f, 0f), Aged, 0.6f);
            });
            // walls collide as boxes (the door gap stays open)
            foreach (int s in new[] { -1, 1 })
                SolidInvisible(root, name + " wall z" + s, foot + rot * new Vector3(0f, WallH / 2f, s * halfZ), rot, new Vector3(2f * halfX + 0.4f, WallH, 0.32f));
            SolidInvisible(root, name + " wall x-", foot + rot * new Vector3(-halfX, WallH / 2f, 0f), rot, new Vector3(0.32f, WallH, 2f * halfZ));
            foreach (int s in new[] { -1, 1 })
                SolidInvisible(root, name + " wall x+" + s, foot + rot * new Vector3(halfX, WallH / 2f, s * halfZ * 0.68f), rot, new Vector3(0.32f, WallH, halfZ * 0.64f));
        }

        /// <summary>
        /// A trestle tower: four splayed legs from the ground (<paramref name="ground"/>, x z to height) up to <paramref name="top"/>,
        /// horizontal girts every 3.5 m and cross braces on each face; the legs collide (a body stops at them).
        /// </summary>
        public static void Trestle(Transform p, string name, Vector3 center, float top, Vector2 half, float yaw, Func<Vector2, float> ground)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
            var heads = new Vector3[4];
            var feet = new Vector3[4];
            float lowest = top - 12f;
            for (int i = 0; i < 4; i++)
            {
                var c = corners[i];
                heads[i] = new Vector3(center.x, top, center.z) + rot * new Vector3(c.x * half.x, 0f, c.y * half.y);
                float drop = Mathf.Max(1f, top - ground(new Vector2(heads[i].x, heads[i].z)));
                if (float.IsNaN(drop)) drop = 12f;
                var footXZ = new Vector3(center.x, 0f, center.z) + rot * new Vector3(c.x * (half.x + 0.06f * drop), 0f, c.y * (half.y + 0.06f * drop));
                float g = ground(new Vector2(footXZ.x, footXZ.z));
                feet[i] = new Vector3(footXZ.x, (float.IsNaN(g) ? lowest : g) - 0.4f, footXZ.z);
            }
            float bottom = feet.Max(f => f.y);
            Frame(p, name, mb =>
            {
                for (int i = 0; i < 4; i++) mb.Beam(feet[i], heads[i] + Vector3.up * 0.2f, 0.3f, 0.3f, Wood, new Vector2(i * 0.7f, 0f));
                var levels = new List<float>();
                for (float y = bottom + 2.4f; y < top - 0.6f; y += 3.5f) levels.Add(y);
                levels.Add(top - 0.35f);
                Vector3 Leg(int i, float y) => Vector3.Lerp(feet[i], heads[i], Mathf.InverseLerp(feet[i].y, heads[i].y, y));
                for (int k = 0; k < levels.Count; k++)
                    for (int i = 0; i < 4; i++)
                    {
                        int j = (i + 1) % 4;
                        mb.Beam(Leg(i, levels[k]), Leg(j, levels[k]), 0.14f, 0.2f, Wood);
                        if (k > 0)
                        {
                            mb.Beam(Leg(i, levels[k - 1]), Leg(j, levels[k]), 0.1f, 0.16f, Wood);
                            mb.Beam(Leg(j, levels[k - 1]), Leg(i, levels[k]), 0.1f, 0.16f, Wood);
                        }
                    }
            });
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject(name + " leg " + i) { layer = CourseKit.Layer("Environment") };
                go.transform.SetParent(p, false);
                var a = feet[i];
                var b = heads[i];
                go.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation((b - a).normalized, Vector3.forward));
                go.AddComponent<BoxCollider>().size = new Vector3(0.3f, 0.3f, Vector3.Distance(a, b));
            }
        }
    }
}
