using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Caves v2 (ARCHITECTURE §27.6, docs/LEVEL_AUTHORING.md §29): a tunnel whose section changes along it (width, height, profile, lean,
    /// a step in its floor, its frame following its pitch up to 35°, its ends flared where they open into a chamber), a cavern with an
    /// offset crown over walls, alcoves, pillars and shelves (a mezzanine), and the shaft (straight walls up from an outline, open at the
    /// top or roofed, ledges round its wall). A cave without any of it is built by the M20 code exactly. Every push is outward from the
    /// clear space (<see cref="Clear"/>), which knows the sections, the crown, the alcoves, the pillars and the shelves.
    /// </summary>
    public static partial class LevelRock
    {
        /// <summary>A tunnel's frame never pitches more than this (degrees): steeper, its walls stay upright.</summary>
        public const float MaxPitch = 35f;
        /// <summary>A flared end is this much wider at the junction.</summary>
        public const float FlareWidth = 1.4f;

        /// <summary>True when a tunnel is built by the v2 code (sections, a flare, an attached end).</summary>
        static bool TunnelV2(RockShellShape shell) =>
            shell.Sections.Count > 0 || shell.Flare > 0f || shell.StartAttach.Attached || shell.EndAttach.Attached;

        // ------------------------------------------------------------------ sections

        /// <summary>One ring's section: half width, height, profile, lean (tan), floor offset.</summary>
        public struct Ring
        {
            public float half, height, lean, floorOffset;
            public CaveProfile profile;
        }

        /// <summary>A point of a profile: u runs 0..1 from the right floor over the crown to the left floor (x right, y up).</summary>
        public static Vector2 Profile(CaveProfile profile, float half, float height, float lean, float u)
        {
            Vector2 p;
            if (profile == CaveProfile.Arch)
            {
                // straight walls to 45 %, then a half ellipse
                float wall = height * 0.45f, ry = height - wall;
                if (u < 0.2f) p = new Vector2(half, wall * (u / 0.2f));
                else if (u > 0.8f) p = new Vector2(-half, wall * (1f - (u - 0.8f) / 0.2f));
                else
                {
                    float th = (u - 0.2f) / 0.6f * Mathf.PI;
                    p = new Vector2(half * Mathf.Cos(th), wall + ry * Mathf.Sin(th));
                }
            }
            else
            {
                float a = u * Mathf.PI, c = Mathf.Cos(a), s = Mathf.Sin(a);
                float q = profile switch
                {
                    CaveProfile.Rift => 2f,
                    CaveProfile.Gallery => 4.5f,
                    CaveProfile.Fissure => c > 0f ? 1.6f : 3.5f,
                    _ => Squareness,
                };
                p = new Vector2(half * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / q), height * Mathf.Pow(Mathf.Abs(s), 2f / q));
            }
            p.x += lean * p.y;
            return p;
        }

        /// <summary>True when (lateral, up) lies inside the profile (its clear space).</summary>
        public static bool InsideProfile(Ring r, float lateral, float up)
        {
            if (up <= 0f || up >= r.height) return false;
            float x = lateral - r.lean * up;
            if (r.profile == CaveProfile.Arch)
            {
                float wall = r.height * 0.45f, ry = r.height - wall;
                if (Mathf.Abs(x) >= r.half) return false;
                if (up <= wall) return true;
                float ex = x / r.half, ey = (up - wall) / ry;
                return ex * ex + ey * ey < 1f;
            }
            float q = r.profile switch
            {
                CaveProfile.Rift => 2f,
                CaveProfile.Gallery => 4.5f,
                CaveProfile.Fissure => x > 0f ? 1.6f : 3.5f,
                _ => Squareness,
            };
            return Mathf.Pow(Mathf.Abs(x) / r.half, q) + Mathf.Pow(up / r.height, q) < 1f;
        }

        /// <summary>
        /// The rings along a tunnel's path: each sample's section blended (smoothstep) between the sections of the control points it lies
        /// between; flared near the ends. Without sections, every ring is Width and Height, Round.
        /// </summary>
        public static Ring[] Rings(RockShellShape shell, List<Vector3> control, List<Vector3> path)
        {
            int n = path.Count;
            var rings = new Ring[n];
            var sections = shell.Sections;
            // the distance along the path, for the flare
            var along = new float[n];
            for (int i = 1; i < n; i++) along[i] = along[i - 1] + Vector3.Distance(path[i], path[i - 1]);
            float total = along[n - 1];
            for (int i = 0; i < n; i++)
            {
                CaveSection sec;
                if (sections.Count == 0) sec = new CaveSection(shell.Width, shell.Height, CaveProfile.Round);
                else
                {
                    // the control segment nearest this sample, and how far along it
                    int best = 0;
                    float bt = 0f, bd = float.MaxValue;
                    for (int k = 0; k + 1 < control.Count; k++)
                    {
                        var ab = control[k + 1] - control[k];
                        float t = Mathf.Clamp01(Vector3.Dot(path[i] - control[k], ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                        float d = Vector3.Distance(path[i], control[k] + ab * t);
                        if (d < bd) { bd = d; best = k; bt = t; }
                    }
                    var a = sections[Mathf.Min(best, sections.Count - 1)];
                    var b = sections[Mathf.Min(best + 1, sections.Count - 1)];
                    float w = Mathf.SmoothStep(0f, 1f, bt);
                    sec = new CaveSection(Mathf.Lerp(a.width, b.width, w), Mathf.Lerp(a.height, b.height, w), w < 0.5f ? a.profile : b.profile,
                                          Mathf.Lerp(a.lean, b.lean, w), Mathf.Lerp(a.floorOffset, b.floorOffset, w));
                }
                float flare = 1f;
                if (shell.Flare > 0f)
                {
                    float end = Mathf.Min(along[i], total - along[i]);
                    flare = Mathf.Lerp(FlareWidth, 1f, Mathf.SmoothStep(0f, 1f, end / shell.Flare));
                }
                rings[i] = new Ring
                {
                    half = sec.width * 0.5f * flare, height = sec.height * Mathf.Lerp(1f, 1.15f, flare - 1f), profile = sec.profile,
                    lean = Mathf.Tan(sec.lean * Mathf.Deg2Rad), floorOffset = sec.floorOffset,
                };
            }
            return rings;
        }

        /// <summary>Each sample's frame: right (level) and up (following the path's pitch, at most <see cref="MaxPitch"/>).</summary>
        static void Frames(List<Vector3> path, out Vector3[] rights, out Vector3[] ups)
        {
            int n = path.Count;
            rights = new Vector3[n];
            ups = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var t = path[Mathf.Min(n - 1, i + 1)] - path[Mathf.Max(0, i - 1)];
                var flat = new Vector3(t.x, 0f, t.z);
                if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
                flat.Normalize();
                var right = Vector3.Cross(Vector3.up, flat);
                float pitch = Mathf.Clamp(Mathf.Atan2(t.y, new Vector2(t.x, t.z).magnitude) * Mathf.Rad2Deg, -MaxPitch, MaxPitch);
                var forward = Quaternion.AngleAxis(-pitch, right) * flat;
                rights[i] = right;
                ups[i] = Vector3.Cross(forward, right).normalized;
            }
        }

        /// <summary>How many rings (or columns) round a junction the rock's thickness is drawn from inside as well.</summary>
        const int JunctionRings = 3;

        /// <summary>
        /// A rim through the rock's thickness; near a junction drawn both ways, so where one cave's hole is wider than the other's section
        /// the rock between them shows from either side (never the sky through the back of a face).
        /// </summary>
        static void Rim2(Rock rock, bool both, int a, int b, int c, int d, Vector3 facing)
        {
            rock.Quad(a, b, c, d, facing);
            if (both) rock.Quad(a, b, c, d, -facing);
        }

        // ------------------------------------------------------------------ the v2 tunnel

        static void BuildTunnelV2(RockShellShape shell, Transform root, string name)
        {
            var control = shell.WorldPoints();
            if (control.Count < 2 || Curves.Length(control) < 1f) return;
            var path = Curves.Resample(Curves.Spline(control, Step * 0.5f), Step);
            int n = path.Count;
            if (n < 2) return;
            var rings = Rings(shell, control, path);
            for (int i = 0; i < n; i++) path[i] += Vector3.up * rings[i].floorOffset;
            Frames(path, out var rights, out var ups);
            float rough = shell.Roughness, thick = shell.Thickness;
            int seed = shell.Seed + TimberKit.Seed(shell.PieceId ?? name, 3);
            int m = Around + 3;

            var others = OtherCaves(shell);
            var rock = new Rock();
            var inner = new int[n, m];
            var outer = new int[n, m];
            var normals = new Vector3[n, m];
            for (int i = 0; i < n; i++)
            {
                var r = rings[i];
                var section = new Vector2[m];
                section[0] = new Vector2(r.half, -CaveSink);
                for (int j = 0; j <= Around; j++) section[j + 1] = Profile(r.profile, r.half, r.height, r.lean, j / (float)Around);
                section[m - 1] = new Vector2(-r.half, -CaveSink);
                for (int j = 0; j < m; j++)
                {
                    var t = section[Mathf.Min(m - 1, j + 1)] - section[Mathf.Max(0, j - 1)];
                    var o = new Vector2(t.y, -t.x).normalized;
                    var p0 = path[i] + rights[i] * section[j].x + ups[i] * section[j].y;
                    var nrm = (rights[i] * o.x + ups[i] * o.y).normalized;
                    normals[i, j] = nrm;
                    var p = p0 + nrm * rough * Noise(p0, seed);
                    inner[i, j] = rock.Add(p);
                    outer[i, j] = rock.Add(p + nrm * thick * (0.75f + 0.5f * Noise(p0 * 1.7f, seed + 11)));
                }
            }
            var holed = new bool[n - 1, m - 1];
            var cut = new bool[n - 1, m - 1];
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < m - 1; j++)
                {
                    var mid = (rock.V[inner[i, j]] + rock.V[inner[i + 1, j + 1]]) * 0.5f;
                    float above = Vector3.Dot(mid - path[i], ups[i]);
                    cut[i, j] = InCaves(others, rock.V[inner[i, j]], rock.V[inner[i + 1, j]], rock.V[inner[i, j + 1]], rock.V[inner[i + 1, j + 1]]);
                    holed[i, j] = cut[i, j] || Holed(shell, mid, above, rings[i].height);
                }
            var nearCut = new bool[n - 1];
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < m - 1; j++)
                    if (cut[i, j])
                        for (int d = -JunctionRings; d <= JunctionRings; d++)
                            if (i + d >= 0 && i + d < n - 1) nearCut[i + d] = true;
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < m - 1; j++)
                {
                    var facing = normals[i, j] + normals[i + 1, j + 1];
                    if (holed[i, j])
                    {
                        bool open = cut[i, j];
                        if (i == 0 ? !open : !holed[i - 1, j]) Rim2(rock, nearCut[i], inner[i, j], inner[i, j + 1], outer[i, j + 1], outer[i, j], path[i] - path[i + 1]);
                        if (i == n - 2 ? !open : !holed[i + 1, j]) Rim2(rock, nearCut[i], inner[i + 1, j], inner[i + 1, j + 1], outer[i + 1, j + 1], outer[i + 1, j], path[i + 1] - path[i]);
                        if (j == 0 ? !open : !holed[i, j - 1]) Rim2(rock, nearCut[i], inner[i, j], inner[i + 1, j], outer[i + 1, j], outer[i, j], rock.V[inner[i, j]] - rock.V[inner[i, j + 1]]);
                        if (j == m - 2 ? !open : !holed[i, j + 1]) Rim2(rock, nearCut[i], inner[i, j + 1], inner[i + 1, j + 1], outer[i + 1, j + 1], outer[i, j + 1], rock.V[inner[i, j + 1]] - rock.V[inner[i, j]]);
                        continue;
                    }
                    rock.Quad(inner[i, j], inner[i + 1, j], inner[i + 1, j + 1], inner[i, j + 1], -facing);
                    if (nearCut[i]) rock.Quad(outer[i, j], outer[i + 1, j], outer[i + 1, j + 1], outer[i, j + 1], -facing);
                    rock.Slot = 1;
                    rock.Quad(outer[i, j], outer[i + 1, j], outer[i + 1, j + 1], outer[i, j + 1], facing);
                    rock.Slot = 0;
                }
            foreach (bool start in new[] { true, false })
            {
                int i = start ? 0 : n - 1;
                var dir = (start ? path[0] - path[1] : path[n - 1] - path[n - 2]).normalized;
                for (int j = 0; j < m - 1; j++)
                    if (!cut[start ? 0 : n - 2, j]) Rim(rock, inner[i, j], inner[i, j + 1], outer[i, j + 1], outer[i, j], dir);
                if (start ? shell.CloseStart : shell.CloseEnd) Cap(rock, path[i], inner, outer, i, m, dir, thick, others);
            }
            Place(root, name + " shell", rock, true, CaveMaterial, CaveOutsideMaterial);
            if (shell.Floor) TunnelFloor(root, name, path, rights, rings.Select(r => r.half + rough * 0.5f).ToArray());
        }

        /// <summary>A v2 tunnel's floor: its half width ring by ring.</summary>
        static void TunnelFloor(Transform root, string name, List<Vector3> path, Vector3[] rights, float[] halves)
        {
            var rock = new Rock();
            int n = path.Count;
            var top = new int[n, 2];
            var bottom = new int[n, 2];
            for (int i = 0; i < n; i++)
                for (int s = 0; s < 2; s++)
                {
                    var edge = path[i] + rights[i] * (s == 0 ? halves[i] : -halves[i]);
                    top[i, s] = rock.Add(edge);
                    bottom[i, s] = rock.Add(edge + Vector3.down * CaveSink);
                }
            for (int i = 0; i < n - 1; i++)
            {
                rock.Quad(top[i, 0], top[i + 1, 0], top[i + 1, 1], top[i, 1], Vector3.up);
                rock.Quad(bottom[i, 0], bottom[i + 1, 0], bottom[i + 1, 1], bottom[i, 1], Vector3.down);
                for (int s = 0; s < 2; s++) rock.Quad(top[i, s], top[i + 1, s], bottom[i + 1, s], bottom[i, s], rights[i] * (s == 0 ? 1f : -1f));
            }
            foreach (int i in new[] { 0, n - 1 })
            {
                var dir = i == 0 ? path[0] - path[1] : path[n - 1] - path[n - 2];
                rock.Quad(top[i, 0], top[i, 1], bottom[i, 1], bottom[i, 0], dir);
            }
            Place(root, name + " floor", rock, false, CaveFloorMaterial);
        }

        // ------------------------------------------------------------------ the shaped cavern

        /// <summary>A cavern's outline (world x, z) with its alcoves bulging out.</summary>
        public static List<Vector2> CavernOutline(RockShellShape shell, float step)
        {
            var flat = Poly2.CounterClockwise(shell.WorldPoints().Select(p => new Vector2(p.x, p.z)).ToList());
            var ring = ResampleClosed(flat, step);
            if (shell.Alcoves.Count == 0 || ring.Count < 3) return ring;
            int k = ring.Count;
            var along = new float[k + 1];
            for (int i = 1; i <= k; i++) along[i] = along[i - 1] + Vector2.Distance(ring[i - 1], ring[i % k]);
            float perimeter = along[k];
            var outward = new Vector2[k];
            for (int i = 0; i < k; i++) outward[i] = Poly2.Outward(ring[(i + 1) % k] - ring[(i + k - 1) % k]).normalized;
            var result = new List<Vector2>(ring);
            foreach (var alcove in shell.Alcoves)
            {
                float at = alcove.along * perimeter, halfW = alcove.width * 0.5f;
                for (int i = 0; i < k; i++)
                {
                    float d = Mathf.Abs(along[i] - at);
                    d = Mathf.Min(d, perimeter - d);
                    if (d >= halfW) continue;
                    float bump = 0.5f + 0.5f * Mathf.Cos(d / halfW * Mathf.PI);
                    result[i] += outward[i] * alcove.depth * bump;
                }
            }
            return result;
        }

        /// <summary>A shaped cavern's crown point (world x, z), its walls' and crown's heights over its floor.</summary>
        static void Crown(RockShellShape shell, List<Vector2> ring, out Vector2 apex, out float wall, out float crown)
        {
            var c = new Vector2(ring.Average(p => p.x), ring.Average(p => p.y));
            var offset = shell.transform.TransformVector(new Vector3(shell.Apex.x, 0f, shell.Apex.y));
            apex = c + new Vector2(offset.x, offset.z);
            crown = shell.ApexHeight > 0f ? shell.ApexHeight : shell.Height;
            wall = shell.ApexHeight > 0f ? Mathf.Min(shell.Height, crown - 0.5f) : 0f;
        }

        static void BuildCavernV2(RockShellShape shell, Transform root, string name)
        {
            var ring = CavernOutline(shell, Step * 1.5f);
            int k = ring.Count;
            if (k < 3) return;
            float y = shell.transform.position.y, rough = shell.Roughness, thick = shell.Thickness;
            Crown(shell, ring, out var apex, out float wall, out float crown);
            int seed = shell.Seed + TimberKit.Seed(shell.PieceId ?? name, 5);
            float radius = ring.Max(p => Vector2.Distance(p, apex));
            int rows = Mathf.Clamp(Mathf.CeilToInt(radius / (Step * 1.5f)), 6, 40);
            int wallRows = wall > 0f ? Mathf.Max(1, Mathf.CeilToInt(wall / (Step * 1.5f))) : 0;
            // rows: the foot (under the floor), up the walls, then up the dome to near its crown, then a fan to the crown
            var shares = new List<float> { 1f };
            var ups = new List<float> { -CaveSink };
            for (int r = 1; r <= wallRows; r++) { shares.Add(1f); ups.Add(wall * r / wallRows); }
            for (int r = 1; r <= rows; r++)
            {
                float s = Mathf.Lerp(1f, 0.06f, r / (float)rows);
                shares.Add(s);
                ups.Add(wall + (crown - wall) * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(s, Squareness)), 1f / Squareness));
            }
            var heart = new Vector3(apex.x, y + crown * 0.3f, apex.y);
            var others = OtherCaves(shell);
            var rock = new Rock();
            int rc = shares.Count;
            var inner = new int[k, rc];
            var outer = new int[k, rc];
            var normals = new Vector3[k, rc];
            for (int col = 0; col < k; col++)
                for (int row = 0; row < rc; row++)
                {
                    var xz = apex + (ring[col] - apex) * shares[row];
                    var p0 = new Vector3(xz.x, y + ups[row], xz.y);
                    var nrm = p0 - heart;
                    if (row <= wallRows) nrm.y = 0f;
                    nrm = nrm.sqrMagnitude < 1e-6f ? Vector3.up : nrm.normalized;
                    normals[col, row] = nrm;
                    var p = p0 + nrm * rough * Noise(p0, seed);
                    inner[col, row] = rock.Add(p);
                    outer[col, row] = rock.Add(p + nrm * thick * (0.75f + 0.5f * Noise(p0 * 1.7f, seed + 11)));
                }
            var holed = new bool[k, rc - 1];
            for (int col = 0; col < k; col++)
                for (int row = 0; row < rc - 1; row++)
                {
                    int next = (col + 1) % k;
                    var mid = (rock.V[inner[col, row]] + rock.V[inner[next, row + 1]]) * 0.5f;
                    holed[col, row] = Holed(shell, mid, mid.y - y, crown) ||
                                      InCaves(others, rock.V[inner[col, row]], rock.V[inner[next, row]], rock.V[inner[col, row + 1]], rock.V[inner[next, row + 1]]);
                }
            var nearHole = new bool[k, rc - 1];
            for (int col = 0; col < k; col++)
                for (int row = 0; row < rc - 1; row++)
                    if (holed[col, row])
                        for (int dc = -JunctionRings; dc <= JunctionRings; dc++)
                            for (int dr = -JunctionRings; dr <= JunctionRings; dr++)
                                if (row + dr >= 0 && row + dr < rc - 1) nearHole[(col + dc + k) % k, row + dr] = true;
            for (int col = 0; col < k; col++)
            {
                int next = (col + 1) % k, prev = (col + k - 1) % k;
                for (int row = 0; row < rc - 1; row++)
                {
                    var facing = normals[col, row] + normals[next, row + 1];
                    if (holed[col, row])
                    {
                        if (!holed[prev, row]) Rim2(rock, true, inner[col, row], inner[col, row + 1], outer[col, row + 1], outer[col, row], rock.V[inner[col, row]] - rock.V[inner[next, row]]);
                        if (!holed[next, row]) Rim2(rock, true, inner[next, row], inner[next, row + 1], outer[next, row + 1], outer[next, row], rock.V[inner[next, row]] - rock.V[inner[col, row]]);
                        if (row == 0 || !holed[col, row - 1]) Rim2(rock, true, inner[col, row], inner[next, row], outer[next, row], outer[col, row], rock.V[inner[col, row]] - rock.V[inner[col, row + 1]]);
                        if (row == rc - 2 || !holed[col, row + 1]) Rim2(rock, true, inner[col, row + 1], inner[next, row + 1], outer[next, row + 1], outer[col, row + 1], rock.V[inner[col, row + 1]] - rock.V[inner[col, row]]);
                        continue;
                    }
                    rock.Quad(inner[col, row], inner[next, row], inner[next, row + 1], inner[col, row + 1], -facing);
                    if (nearHole[col, row]) rock.Quad(outer[col, row], outer[next, row], outer[next, row + 1], outer[col, row + 1], -facing);
                    rock.Slot = 1;
                    rock.Quad(outer[col, row], outer[next, row], outer[next, row + 1], outer[col, row + 1], facing);
                    rock.Slot = 0;
                }
            }
            var top = new Vector3(apex.x, y + crown, apex.y);
            int ci = rock.Add(top + Vector3.up * rough * Noise(top, seed)), co = rock.Add(top + Vector3.up * (rough + thick));
            for (int col = 0; col < k; col++)
            {
                int next = (col + 1) % k;
                if (InCaves(others, rock.V[ci], rock.V[inner[col, rc - 1]], rock.V[inner[next, rc - 1]])) continue;
                rock.Tri(ci, inner[col, rc - 1], inner[next, rc - 1], Vector3.down);
                rock.Slot = 1;
                rock.Tri(co, outer[col, rc - 1], outer[next, rc - 1], Vector3.up);
                rock.Slot = 0;
            }
            Place(root, name + " shell", rock, true, CaveMaterial, CaveOutsideMaterial);
            if (shell.Floor) CavernFloor(root, name, ring, y);
        }

        /// <summary>The roof's height over a shaped cavern's floor at <paramref name="q"/> (world x, z).</summary>
        static float DomeHeight(List<Vector2> ring, Vector2 apex, float wall, float crown, Vector2 q)
        {
            float reach = RayExit(ring, apex, q - apex);
            float share = reach <= 1e-4f ? 0f : Vector2.Distance(q, apex) / reach;
            return wall + (crown - wall) * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Min(1f, share), Squareness)), 1f / Squareness);
        }

        // ------------------------------------------------------------------ pillars and shelves

        /// <summary>A cavern's pillars (solid, floor to roof) and shelves (walked on).</summary>
        static void CavernParts(RockShellShape shell, Transform root, string name)
        {
            float y = shell.transform.position.y;
            int seed = shell.Seed + TimberKit.Seed(shell.PieceId ?? name, 9);
            for (int i = 0; i < shell.Pillars.Count; i++)
            {
                var pillar = shell.Pillars[i];
                var at = shell.transform.TransformPoint(new Vector3(pillar.at.x, 0f, pillar.at.y));
                float roof = RoofOver(shell, new Vector2(at.x, at.z));
                var rock = new Rock();
                const int Around8 = 12;
                int rows = Mathf.Max(2, Mathf.CeilToInt((roof + 1f + CaveSink) / 0.6f));
                var grid = new int[rows + 1, Around8];
                for (int r = 0; r <= rows; r++)
                {
                    float t = r / (float)rows, up = Mathf.Lerp(-CaveSink, roof + 1f, t);
                    float rad = pillar.radius * (1f - (1f - pillar.waist) * Mathf.Sin(Mathf.Clamp01(up / Mathf.Max(0.5f, roof)) * Mathf.PI));
                    for (int a = 0; a < Around8; a++)
                    {
                        float ang = a * Mathf.PI * 2f / Around8;
                        var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                        var p0 = new Vector3(at.x, y + up, at.z) + dir * rad;
                        grid[r, a] = rock.Add(p0 + dir * shell.Roughness * 0.4f * Noise(p0, seed + i));
                    }
                }
                for (int r = 0; r < rows; r++)
                    for (int a = 0; a < Around8; a++)
                    {
                        int b = (a + 1) % Around8;
                        var mid = (rock.V[grid[r, a]] + rock.V[grid[r + 1, b]]) * 0.5f;
                        rock.Quad(grid[r, a], grid[r, b], grid[r + 1, b], grid[r + 1, a], mid - new Vector3(at.x, mid.y, at.z));
                    }
                Place(root, $"{name} pillar {i}", rock, false, CaveMaterial);
            }
            for (int i = 0; i < shell.Floors.Count; i++)
            {
                var shelf = shell.Floors[i];
                if (shelf.outline == null || shelf.outline.Count < 3) continue;
                var ring = Poly2.CounterClockwise(shelf.outline.Select(p => shell.transform.TransformPoint(new Vector3(p.x, 0f, p.y))).Select(p => new Vector2(p.x, p.z)).ToList());
                Slab(root, $"{name} shelf {i}", ring, y + shelf.height, shelf.thickness, seed + i);
            }
        }

        /// <summary>A walked slab of rock: its top at <paramref name="top"/> over the outline, <paramref name="thickness"/> deep.</summary>
        static void Slab(Transform root, string name, List<Vector2> ring, float top, float thickness, int seed)
        {
            var rock = new Rock();
            int k = ring.Count;
            var up = ring.Select(p => rock.Add(new Vector3(p.x, top, p.y))).ToArray();
            var down = ring.Select(p => rock.Add(new Vector3(p.x, top - thickness - 0.3f * Noise(new Vector3(p.x, top, p.y), seed), p.y))).ToArray();
            var tris = Poly2.Triangulate(ring);
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                rock.Tri(up[tris[t]], up[tris[t + 1]], up[tris[t + 2]], Vector3.up);
                rock.Tri(down[tris[t]], down[tris[t + 1]], down[tris[t + 2]], Vector3.down);
            }
            for (int i = 0; i < k; i++)
            {
                int next = (i + 1) % k;
                var e = ring[next] - ring[i];
                rock.Quad(up[i], up[next], down[next], down[i], new Vector3(e.y, 0f, -e.x));
            }
            Place(root, name, rock, false, CaveMaterial);
        }

        /// <summary>The roof's height over a cavern's floor at <paramref name="q"/> (world x, z).</summary>
        static float RoofOver(RockShellShape shell, Vector2 q)
        {
            if (shell.ShapedCavern)
            {
                var ring = CavernOutline(shell, Step * 1.5f);
                Crown(shell, ring, out var apex, out float wall, out float crown);
                return DomeHeight(ring, apex, wall, crown, q);
            }
            var flat = Poly2.CounterClockwise(shell.WorldPoints().Select(p => new Vector2(p.x, p.z)).ToList());
            var c = new Vector2(flat.Average(p => p.x), flat.Average(p => p.y));
            return DomeHeight(flat, c, 0f, shell.Height, q);
        }

        // ------------------------------------------------------------------ shafts

        static void BuildShaft(RockShellShape shell, Transform root, string name)
        {
            var flat = Poly2.CounterClockwise(shell.WorldPoints().Select(p => new Vector2(p.x, p.z)).ToList());
            if (flat.Count < 3) return;
            var ring = ResampleClosed(flat, Step * 1.5f);
            int k = ring.Count;
            float y = shell.transform.position.y, h = shell.Height, rough = shell.Roughness, thick = shell.Thickness;
            int seed = shell.Seed + TimberKit.Seed(shell.PieceId ?? name, 13);
            var c = new Vector2(ring.Average(p => p.x), ring.Average(p => p.y));
            int rows = Mathf.Max(2, Mathf.CeilToInt((h + CaveSink) / (Step * 1.5f)));
            var others = OtherCaves(shell);
            var rock = new Rock();
            var inner = new int[k, rows + 1];
            var outer = new int[k, rows + 1];
            var normals = new Vector3[k];
            for (int col = 0; col < k; col++)
            {
                var o = ring[col] - c;
                normals[col] = new Vector3(o.x, 0f, o.y).normalized;
                for (int r = 0; r <= rows; r++)
                {
                    float up = Mathf.Lerp(-CaveSink, h, r / (float)rows);
                    var p0 = new Vector3(ring[col].x, y + up, ring[col].y);
                    var p = p0 + normals[col] * rough * Noise(p0, seed);
                    inner[col, r] = rock.Add(p);
                    outer[col, r] = rock.Add(p + normals[col] * thick * (0.75f + 0.5f * Noise(p0 * 1.7f, seed + 11)));
                }
            }
            for (int col = 0; col < k; col++)
            {
                int next = (col + 1) % k;
                var facing = normals[col] + normals[next];
                for (int r = 0; r < rows; r++)
                {
                    if (InCaves(others, rock.V[inner[col, r]], rock.V[inner[next, r]], rock.V[inner[next, r + 1]], rock.V[inner[col, r + 1]])) continue;
                    rock.Quad(inner[col, r], inner[next, r], inner[next, r + 1], inner[col, r + 1], -facing);
                    rock.Slot = 1;
                    rock.Quad(outer[col, r], outer[next, r], outer[next, r + 1], outer[col, r + 1], facing);
                    rock.Slot = 0;
                }
                // the rim at the top
                rock.Quad(inner[col, rows], inner[next, rows], outer[next, rows], outer[col, rows], Vector3.up);
            }
            if (!shell.OpenTop)
            {
                // a roof: the top rim closed over (a ceiling)
                var topRing = Enumerable.Range(0, k).Select(col => new Vector2(rock.V[outer[col, rows]].x, rock.V[outer[col, rows]].z)).ToList();
                var tris = Poly2.Triangulate(topRing);
                var lid = Enumerable.Range(0, k).Select(col => rock.Add(rock.V[outer[col, rows]] + Vector3.up * 0.01f)).ToArray();
                for (int t = 0; t + 2 < tris.Count; t += 3)
                {
                    rock.Tri(lid[tris[t]], lid[tris[t + 1]], lid[tris[t + 2]], Vector3.down);
                    rock.Tri(lid[tris[t]], lid[tris[t + 1]], lid[tris[t + 2]], Vector3.up);
                }
            }
            Place(root, name + " shaft", rock, !shell.OpenTop, CaveMaterial, CaveOutsideMaterial);
            if (shell.Floor && !shell.OpenBottom) CavernFloor(root, name, ring, y);
            // ledges round part of the wall
            float perimeter = 0f;
            for (int i = 0; i < k; i++) perimeter += Vector2.Distance(ring[i], ring[(i + 1) % k]);
            for (int l = 0; l < shell.Ledges.Count; l++)
            {
                var ledge = shell.Ledges[l];
                var outerEdge = new List<Vector2>();
                var innerEdge = new List<Vector2>();
                float run = 0f;
                for (int i = 0; i < k; i++)
                {
                    float f = run / perimeter;
                    run += Vector2.Distance(ring[i], ring[(i + 1) % k]);
                    bool inside = ledge.from <= ledge.to ? f >= ledge.from && f <= ledge.to : f >= ledge.from || f <= ledge.to;
                    if (!inside) continue;
                    var toC = (c - ring[i]).normalized;
                    outerEdge.Add(ring[i] - toC * 0.3f);
                    innerEdge.Add(ring[i] + toC * ledge.inset);
                }
                if (outerEdge.Count < 2) continue;
                innerEdge.Reverse();
                Slab(root, $"{name} ledge {l}", outerEdge.Concat(innerEdge).ToList(), y + ledge.height, 0.5f, seed + l);
            }
        }

        // ------------------------------------------------------------------ the clear space

        /// <summary>The v2 clear space of a cave (its sections, crown, alcoves, shaft), or null when the M20 one holds.</summary>
        static ClearSpace ClearV2(RockShellShape shell)
        {
            if (shell.Kind == RockShellKind.Tunnel && TunnelV2(shell))
            {
                var control = shell.WorldPoints();
                if (control.Count < 2) return new ClearSpace { inside = _ => false };
                var path = Curves.Resample(Curves.Spline(control, Step * 0.5f), Step);
                var rings = Rings(shell, control, path);
                for (int i = 0; i < path.Count; i++) path[i] += Vector3.up * rings[i].floorOffset;
                Frames(path, out var rights, out var ups);
                var bounds = new Bounds(path[0], Vector3.zero);
                for (int i = 0; i < path.Count; i++)
                {
                    float r = rings[i].half + Mathf.Abs(rings[i].lean) * rings[i].height;
                    bounds.Encapsulate(path[i] + new Vector3(-r, 0f, -r));
                    bounds.Encapsulate(path[i] + new Vector3(r, rings[i].height + 1f, r));
                }
                return new ClearSpace
                {
                    bounds = bounds,
                    inside = p =>
                    {
                        int best = -1;
                        float bd = float.MaxValue;
                        for (int i = 0; i < path.Count; i++)
                        {
                            float d = (new Vector2(p.x - path[i].x, p.z - path[i].z)).sqrMagnitude;
                            if (d < bd) { bd = d; best = i; }
                        }
                        if (best <= 0 && Vector3.Dot(p - path[0], path[0] - path[Mathf.Min(1, path.Count - 1)]) > 0f) return false;
                        if (best == path.Count - 1 && Vector3.Dot(p - path[best], path[best] - path[best - 1]) > 0f) return false;
                        var rel = p - path[best];
                        return InsideProfile(rings[best], Vector3.Dot(rel, rights[best]), Vector3.Dot(rel, ups[best]));
                    },
                };
            }
            if (shell.Kind == RockShellKind.Cavern && shell.ShapedCavern)
            {
                var ring = CavernOutline(shell, Step * 1.5f);
                if (ring.Count < 3) return new ClearSpace { inside = _ => false };
                Crown(shell, ring, out var apex, out float wall, out float crown);
                float y = shell.transform.position.y;
                var box = new Bounds(new Vector3(apex.x, y + crown * 0.5f, apex.y), Vector3.zero);
                foreach (var p in ring) box.Encapsulate(new Vector3(p.x, y, p.y));
                box.Encapsulate(new Vector3(apex.x, y + crown, apex.y));
                return new ClearSpace
                {
                    bounds = box,
                    inside = p =>
                    {
                        var q = new Vector2(p.x, p.z);
                        float up = p.y - y;
                        return up > 0f && Poly2.Contains(ring, q) && up < DomeHeight(ring, apex, wall, crown, q);
                    },
                };
            }
            if (shell.Kind == RockShellKind.Shaft)
            {
                var flat = Poly2.CounterClockwise(shell.WorldPoints().Select(p => new Vector2(p.x, p.z)).ToList());
                if (flat.Count < 3) return new ClearSpace { inside = _ => false };
                float y = shell.transform.position.y, h = shell.Height;
                var box = new Bounds(new Vector3(flat[0].x, y, flat[0].y), Vector3.zero);
                foreach (var p in flat) { box.Encapsulate(new Vector3(p.x, y, p.y)); box.Encapsulate(new Vector3(p.x, y + h, p.y)); }
                return new ClearSpace { bounds = box, inside = p => p.y > y && p.y < y + h && Poly2.Contains(flat, new Vector2(p.x, p.z)) };
            }
            return null;
        }

        /// <summary>Takes a cave's pillars and shelves out of its clear space.</summary>
        static ClearSpace WithoutParts(RockShellShape shell, ClearSpace space)
        {
            if (shell.Pillars.Count == 0 && shell.Floors.Count == 0 && shell.Ledges.Count == 0) return space;
            var pillars = shell.Pillars.Select(p => (at: shell.transform.TransformPoint(new Vector3(p.at.x, 0f, p.at.y)), r: p.radius + 0.3f)).ToList();
            float y = shell.transform.position.y;
            var shelves = shell.Floors.Where(f => f.outline != null && f.outline.Count > 2)
                .Select(f => (ring: f.outline.Select(q => shell.transform.TransformPoint(new Vector3(q.x, 0f, q.y))).Select(w => new Vector2(w.x, w.z)).ToList(), top: y + f.height, bottom: y + f.height - f.thickness - 0.3f))
                .ToList();
            var inside = space.inside;
            return new ClearSpace
            {
                bounds = space.bounds,
                inside = p => inside(p)
                              && !pillars.Any(x => new Vector2(p.x - x.at.x, p.z - x.at.z).magnitude < x.r)
                              && !shelves.Any(sh => p.y > sh.bottom && p.y < sh.top && Poly2.Contains(sh.ring, new Vector2(p.x, p.z))),
            };
        }
    }
}
