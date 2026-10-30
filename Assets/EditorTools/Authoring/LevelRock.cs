using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HotPatata.Editor
{
    /// <summary>
    /// The rock pieces of an authored level (ARCHITECTURE §27.2): caves (<see cref="RockShellShape"/>, a tunnel along a line or a cavern
    /// over an outline) and cliffs (<see cref="CliffShape"/>). Each is one rough mesh drawn in the nature look's rock and, the same mesh, its
    /// collider on Environment (a <see cref="WalkableProxy"/> of kind Solid: the static organic ground's exception to the box-only rule).
    /// A cave's roof is a <see cref="CourseCeiling"/>. The rock bulges outward only, so the clear space a piece is drawn with stays clear, and no
    /// rock is drawn in another cave's clear space (a tunnel's mouth opens in a cavern's wall, a gallery's end, a cliff's face).
    /// Called by <see cref="LevelPieceBuilder"/>.
    /// </summary>
    public static partial class LevelRock
    {
        /// <summary>A cave's inside, its outside (moss on what faces up), its floor.</summary>
        public const string CaveMaterial = "Nature_CaveRock", CaveOutsideMaterial = "Nature_MossyRock", CaveFloorMaterial = "Nature_RocksGround";

        /// <summary>The material of a cliff's rock.</summary>
        public static string CliffMaterial(CliffRock rock) => rock switch
        {
            CliffRock.Sandstone => "Nature_Cliff",
            CliffRock.Mossy => "Nature_MossyRock",
            CliffRock.Limestone => "Nature_Limestone",
            CliffRock.Basalt => "Nature_Basalt",
            CliffRock.Earth => "Nature_EarthBank",
            _ => "Nature_Granite",
        };

        /// <summary>A cliff's detailed faces are sampled about every this many metres where strata or fracture are strong.</summary>
        public const float FineStep = 0.6f;
        /// <summary>How far a cave's walls reach under its floor (m): into the ground, so no gap shows at their foot.</summary>
        public const float CaveSink = 1.5f;
        /// <summary>A tunnel is sampled about every this many metres along its line; a cliff's outline about every this many.</summary>
        public const float Step = 1f, CliffStep = 1.2f;
        /// <summary>The share of a cave's height above which a hole is a skylight (the roof, not the walls).</summary>
        public const float RoofShare = 0.45f;
        const int Around = 26;      // a tunnel's section samples
        const float Squareness = 2.6f;   // a tunnel's section: 2 is an ellipse, higher is boxier (flatter walls and crown)

        // ------------------------------------------------------------------ mesh assembly

        /// <summary>A mesh of shared vertices (smooth rock) in up to two materials: triangles are turned to face the way asked.</summary>
        sealed class Rock
        {
            public readonly List<Vector3> V = new List<Vector3>();
            /// <summary>Per vertex (when set): alpha is the foot band, 1 at the ground fading to 0 at its top.</summary>
            public List<Color> Colors;
            readonly List<int>[] subs = { new List<int>(), new List<int>() };
            List<int> tris;

            public Rock() => tris = subs[0];

            /// <summary>The material slot the next triangles go in (0 or 1).</summary>
            public int Slot { set => tris = subs[Mathf.Clamp(value, 0, 1)]; }

            public int Add(Vector3 p)
            {
                V.Add(p);
                return V.Count - 1;
            }

            /// <summary>A quad a-b-c-d (a ring order), turned so its face looks along <paramref name="facing"/>.</summary>
            public void Quad(int a, int b, int c, int d, Vector3 facing)
            {
                var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]) + Vector3.Cross(V[c] - V[a], V[d] - V[a]);
                if (Vector3.Dot(n, facing) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }
                else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c); }
            }

            public void Tri(int a, int b, int c, Vector3 facing)
            {
                var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                if (Vector3.Dot(n, facing) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); }
                else { tris.Add(a); tris.Add(c); tris.Add(b); }
            }

            public int Triangles => (subs[0].Count + subs[1].Count) / 3;

            public int Slots => subs[1].Count > 0 ? 2 : 1;

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(V);
                mesh.subMeshCount = Slots;
                for (int s = 0; s < Slots; s++) mesh.SetTriangles(subs[s], s);
                mesh.SetUVs(0, V.Select(v => new Vector2(v.x + v.y * 0.37f, v.z + v.y * 0.61f) * 0.25f).ToList());
                if (Colors != null && Colors.Count == V.Count) mesh.SetColors(Colors);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>
        /// The rock's roughness in <paramref name="octaves"/> octaves (fBm), each <paramref name="lacunarity"/> times finer and 0.5 as strong,
        /// 0..1. One octave is <see cref="Noise"/> itself.
        /// </summary>
        public static float Fbm(Vector3 p, int seed, int octaves, float lacunarity)
        {
            if (octaves <= 1) return Noise(p, seed);
            float sum = 0f, amp = 1f, norm = 0f, f = 1f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(p * f, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                f *= lacunarity;
            }
            return sum / norm;
        }

        static float Hash(int a, int b, int seed)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263 + seed * 2246822519);
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
            }
        }

        /// <summary>A rock's roughness at a point: 0..1, smooth over a few metres, the piece's seed shifting it.</summary>
        public static float Noise(Vector3 p, int seed)
        {
            float ox = (seed * 0.6180339f) % 97f + 13.1f, oz = (seed * 0.3819660f) % 89f + 7.7f;
            float Octave(float s)
            {
                var q = p * s;
                return (Mathf.PerlinNoise(q.x + ox, q.y + oz) + Mathf.PerlinNoise(q.y + oz, q.z + ox) + Mathf.PerlinNoise(q.z + ox * 0.5f, q.x + oz * 0.5f)) / 3f;
            }
            float n = Octave(0.12f) * 0.45f + Octave(0.33f) * 0.33f + Octave(0.9f) * 0.22f;
            return Mathf.Clamp01((n - 0.3f) / 0.4f);
        }

        /// <summary>Puts a rock mesh under <paramref name="root"/> that is only drawn (its collider is a simpler mesh beside it).</summary>
        static GameObject PlaceDrawn(Transform root, string name, Rock rock, params string[] materials)
        {
            if (rock.Triangles == 0) return null;
            var mesh = OrganicAssets.Save(rock.ToMesh(name + " rock"));
            var go = new GameObject(name) { layer = CourseKit.Layer("Environment") };
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials.Take(rock.Slots).Select(NatureMaterialBuilder.Load).ToArray();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>Puts a rock's collider under <paramref name="root"/> (no renderer): the same faces a plain build of the piece collides with.</summary>
        static GameObject PlaceCollider(Transform root, string name, Rock rock, bool ceiling)
        {
            if (rock.Triangles == 0) return null;
            var mesh = OrganicAssets.Save(rock.ToMesh(name + " collider"));
            var go = new GameObject(name) { layer = CourseKit.Layer("Environment") };
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<WalkableProxy>().Configure(WalkableProxy.Kind.Solid, 0.05f);
            if (ceiling) go.AddComponent<CourseCeiling>();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>Puts a built rock mesh under <paramref name="root"/>: drawn in its <paramref name="materials"/> (one per slot), colliding as itself.</summary>
        static GameObject Place(Transform root, string name, Rock rock, bool ceiling, params string[] materials)
        {
            if (rock.Triangles == 0) return null;
            var mesh = OrganicAssets.Save(rock.ToMesh(name + " rock"));
            var go = new GameObject(name) { layer = CourseKit.Layer("Environment") };
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials.Take(rock.Slots).Select(NatureMaterialBuilder.Load).ToArray();
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<WalkableProxy>().Configure(WalkableProxy.Kind.Solid, 0.05f);
            if (ceiling) go.AddComponent<CourseCeiling>();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ------------------------------------------------------------------ caves

        /// <summary>A point's floor inside a cave's footprint (its walls included), or false outside it: the mist's ground under the earth.</summary>
        public delegate bool FloorUnder(Vector2 point, out float floor);

        /// <summary>The footprint of <paramref name="shell"/> on the flat, and its floor there.</summary>
        public static FloorUnder Footprint(RockShellShape shell)
        {
            var world = shell.WorldPoints();
            if (world.Count < 2) return (Vector2 _, out float f) => { f = 0f; return false; };
            if (shell.Kind == RockShellKind.Cavern)
            {
                var poly = world.Select(w => new Vector2(w.x, w.z)).ToList();
                float y = shell.transform.position.y, margin = shell.Thickness + shell.Roughness;
                return (Vector2 p, out float f) => { f = y; return Poly2.Contains(poly, p) || Poly2.DistanceToEdge(poly, p) < margin; };
            }
            var line = Curves.Resample(Curves.Spline(world, Step * 0.5f), Step);
            float reach = shell.Width * 0.5f + shell.Thickness + shell.Roughness;
            return (Vector2 p, out float f) =>
            {
                f = 0f;
                float best = float.MaxValue;
                for (int i = 0; i < line.Count - 1; i++)
                {
                    Vector2 a = new Vector2(line[i].x, line[i].z), b = new Vector2(line[i + 1].x, line[i + 1].z), ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    float d = Vector2.Distance(p, a + ab * t);
                    if (d < best) { best = d; f = Mathf.Lerp(line[i].y, line[i + 1].y, t); }
                }
                return best < reach;
            };
        }

        /// <summary>A cave's clear space: where a player stands inside it (the space it is drawn with, its rock bulging outward only).</summary>
        public sealed class ClearSpace
        {
            public Bounds bounds;
            public System.Func<Vector3, bool> inside;

            public bool Contains(Vector3 p) => bounds.Contains(p) && inside(p);
        }

        /// <summary>The clear space of <paramref name="shell"/>: over its floor and under its roof (a tunnel's section, a cavern's dome).</summary>
        public static ClearSpace Clear(RockShellShape shell) => WithoutParts(shell, ClearV2(shell) ?? ClearM20(shell));

        static ClearSpace ClearM20(RockShellShape shell)
        {
            var world = shell.WorldPoints();
            if (shell.Kind == RockShellKind.Cavern)
            {
                if (world.Count < 3) return new ClearSpace { inside = _ => false };
                var flat = Poly2.CounterClockwise(world.Select(p => new Vector2(p.x, p.z)).ToList());
                var ring = ResampleClosed(flat, Step * 1.5f);
                var c = new Vector2(ring.Average(p => p.x), ring.Average(p => p.y));
                float y = shell.transform.position.y, h = shell.Height;
                var box = new Bounds(new Vector3(c.x, y + h * 0.5f, c.y), Vector3.zero);
                foreach (var p in flat) box.Encapsulate(new Vector3(p.x, y, p.y));
                box.Encapsulate(new Vector3(c.x, y + h, c.y));
                return new ClearSpace
                {
                    bounds = box,
                    inside = p =>
                    {
                        var q = new Vector2(p.x, p.z);
                        float up = p.y - y;
                        if (up <= 0f || up >= h || !Poly2.Contains(flat, q)) return false;
                        float reach = RayExit(flat, c, q - c);
                        float share = reach <= 1e-4f ? 0f : Vector2.Distance(q, c) / reach;
                        return up < h * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(share, Squareness)), 1f / Squareness);
                    },
                };
            }
            if (world.Count < 2) return new ClearSpace { inside = _ => false };
            var line = Curves.Resample(Curves.Spline(world, Step * 0.5f), Step);
            float w = shell.Width * 0.5f, height = shell.Height;
            var bounds = new Bounds(line[0], Vector3.zero);
            foreach (var p in line)
            {
                bounds.Encapsulate(p + new Vector3(-w, 0f, -w));
                bounds.Encapsulate(p + new Vector3(w, height, w));
            }
            return new ClearSpace
            {
                bounds = bounds,
                inside = p =>
                {
                    var q = new Vector2(p.x, p.z);
                    float best = float.MaxValue, floor = 0f;
                    bool beyond = false;
                    for (int i = 0; i < line.Count - 1; i++)
                    {
                        Vector2 a = new Vector2(line[i].x, line[i].z), b = new Vector2(line[i + 1].x, line[i + 1].z), ab = b - a;
                        float raw = Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude), t = Mathf.Clamp01(raw);
                        float d = Vector2.Distance(q, a + ab * t);
                        if (d >= best) continue;
                        best = d;
                        floor = Mathf.Lerp(line[i].y, line[i + 1].y, t);
                        beyond = (i == 0 && raw < 0f) || (i == line.Count - 2 && raw > 1f);
                    }
                    if (beyond || best >= w) return false;
                    float up = p.y - floor;
                    return up > 0f && up < height * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(best / w, Squareness)), 1f / Squareness);
                },
            };
        }

        /// <summary>How far from <paramref name="from"/> along <paramref name="dir"/> the ray leaves the closed outline.</summary>
        static float RayExit(IList<Vector2> poly, Vector2 from, Vector2 dir)
        {
            if (dir.sqrMagnitude < 1e-8f) return 0f;
            dir.Normalize();
            float best = float.MaxValue;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], e = poly[(i + 1) % poly.Count] - a;
                float den = dir.x * e.y - dir.y * e.x;
                if (Mathf.Abs(den) < 1e-8f) continue;
                var ap = a - from;
                float t = (ap.x * e.y - ap.y * e.x) / den, u = (ap.x * dir.y - ap.y * dir.x) / den;
                if (t > 0f && u >= 0f && u <= 1f) best = Mathf.Min(best, t);
            }
            return best == float.MaxValue ? 0f : best;
        }

        /// <summary>
        /// The clear spaces of the other caves in <paramref name="self"/>'s scene: no rock is drawn in them, so a tunnel's mouth opens in a
        /// cavern's wall, a gallery's end or a cliff's face (ARCHITECTURE §27.2).
        /// </summary>
        public static List<ClearSpace> OtherCaves(Component self)
        {
            var scene = self.gameObject.scene;
            if (!scene.IsValid()) return new List<ClearSpace>();
            return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RockShellShape>(true))
                .Where(s => s != self && s.gameObject.activeInHierarchy).Select(Clear).ToList();
        }

        /// <summary>
        /// True when the quad a-b-c-d reaches into one of the caves' clear space: its corners, the middles of its sides or its middle (a
        /// tall quad of a cavern's wall can stand across a tunnel with every corner outside it).
        /// </summary>
        static bool InCaves(List<ClearSpace> caves, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            if (caves.Count == 0) return false;
            var mid = (a + b + c + d) * 0.25f;
            foreach (var cave in caves)
                if (cave.Contains(a) || cave.Contains(b) || cave.Contains(c) || cave.Contains(d) || cave.Contains(mid) ||
                    cave.Contains((a + b) * 0.5f) || cave.Contains((b + c) * 0.5f) || cave.Contains((c + d) * 0.5f) || cave.Contains((d + a) * 0.5f)) return true;
            return false;
        }

        static bool InCaves(List<ClearSpace> caves, Vector3 a, Vector3 b, Vector3 c) => InCaves(caves, a, b, c, c);

        public static void BuildShell(RockShellShape shell, Transform root, string name)
        {
            if (shell.Kind == RockShellKind.Shaft) BuildShaft(shell, root, name);
            else if (shell.Kind == RockShellKind.Cavern)
            {
                if (shell.ShapedCavern) BuildCavernV2(shell, root, name);
                else BuildCavern(shell, root, name);
            }
            else if (TunnelV2(shell)) BuildTunnelV2(shell, root, name);
            else BuildTunnel(shell, root, name);
            if (shell.Kind != RockShellKind.Tunnel) CavernParts(shell, root, name);
            Dress(shell, root, name);
        }

        /// <summary>A hole is cut where a quad's middle (world) is in a skylight (high up) or a mouth.</summary>
        static bool Holed(RockShellShape shell, Vector3 world, float above, float height)
        {
            var local = shell.transform.InverseTransformPoint(world);
            var flat = new Vector2(local.x, local.z);
            if (above > height * RoofShare)
                foreach (var s in shell.Skylights)
                    if (Vector2.Distance(flat, s.at) < s.radius) return true;
            return false;
        }

        static void BuildTunnel(RockShellShape shell, Transform root, string name)
        {
            var control = shell.WorldPoints();
            if (control.Count < 2 || Curves.Length(control) < 1f) return;
            var path = Curves.Resample(Curves.Spline(control, Step * 0.5f), Step);
            int n = path.Count;
            if (n < 2) return;
            float w = shell.Width * 0.5f, h = shell.Height, rough = shell.Roughness, thick = shell.Thickness;
            int seed = shell.Seed + TimberKit.Seed(shell.PieceId ?? name, 3);

            // the section, right floor to left floor over the crown (x right, y up), and each point's outward normal
            var section = new List<Vector2> { new Vector2(w, -CaveSink) };
            for (int j = 0; j <= Around; j++)
            {
                float a = j / (float)Around * Mathf.PI, c = Mathf.Cos(a), s = Mathf.Sin(a);
                section.Add(new Vector2(w * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / Squareness), h * Mathf.Pow(Mathf.Abs(s), 2f / Squareness)));
            }
            section.Add(new Vector2(-w, -CaveSink));
            int m = section.Count;
            var outward = new Vector2[m];
            for (int j = 0; j < m; j++)
            {
                var t = section[Mathf.Min(m - 1, j + 1)] - section[Mathf.Max(0, j - 1)];
                outward[j] = new Vector2(t.y, -t.x).normalized;
            }

            var rock = new Rock();
            var inner = new int[n, m];
            var outer = new int[n, m];
            var rights = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var t = (path[Mathf.Min(n - 1, i + 1)] - path[Mathf.Max(0, i - 1)]);
                t.y = 0f;
                if (t.sqrMagnitude < 1e-6f) t = Vector3.forward;
                var right = Vector3.Cross(Vector3.up, t.normalized);
                rights[i] = right;
                for (int j = 0; j < m; j++)
                {
                    var p0 = path[i] + right * section[j].x + Vector3.up * section[j].y;
                    var nrm = (right * outward[j].x + Vector3.up * outward[j].y).normalized;
                    var p = p0 + nrm * rough * Noise(p0, seed);
                    inner[i, j] = rock.Add(p);
                    outer[i, j] = rock.Add(p + nrm * thick * (0.75f + 0.5f * Noise(p0 * 1.7f, seed + 11)));
                }
            }

            // the surfaces, holes cut where they are asked (a hole's rim joins the inner face to the outer)
            var holed = new bool[n - 1, m - 1];
            var cut = new bool[n - 1, m - 1];   // in another cave's clear space: open, and no rim toward a mouth
            var others = OtherCaves(shell);
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < m - 1; j++)
                {
                    var mid = (rock.V[inner[i, j]] + rock.V[inner[i + 1, j + 1]]) * 0.5f;
                    float above = (section[j].y + section[j + 1].y) * 0.5f;
                    cut[i, j] = InCaves(others, rock.V[inner[i, j]], rock.V[inner[i + 1, j]], rock.V[inner[i, j + 1]], rock.V[inner[i + 1, j + 1]]);
                    holed[i, j] = cut[i, j] || Holed(shell, mid, above, h);
                }
            Vector3 Out(int i, int j) => (rights[i] * outward[j].x + Vector3.up * outward[j].y).normalized;
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < m - 1; j++)
                {
                    var facing = Out(i, j) + Out(i + 1, j + 1);
                    if (holed[i, j])
                    {
                        // rims on the edges the hole shares with rock
                        bool open = cut[i, j];
                        if (i == 0 ? !open : !holed[i - 1, j]) Rim(rock, inner[i, j], inner[i, j + 1], outer[i, j + 1], outer[i, j], path[i] - path[i + 1]);
                        if (i == n - 2 ? !open : !holed[i + 1, j]) Rim(rock, inner[i + 1, j], inner[i + 1, j + 1], outer[i + 1, j + 1], outer[i + 1, j], path[i + 1] - path[i]);
                        if (j == 0 ? !open : !holed[i, j - 1]) Rim(rock, inner[i, j], inner[i + 1, j], outer[i + 1, j], outer[i, j], rock.V[inner[i, j]] - rock.V[inner[i, j + 1]]);
                        if (j == m - 2 ? !open : !holed[i, j + 1]) Rim(rock, inner[i, j + 1], inner[i + 1, j + 1], outer[i + 1, j + 1], outer[i, j + 1], rock.V[inner[i, j + 1]] - rock.V[inner[i, j]]);
                        continue;
                    }
                    rock.Quad(inner[i, j], inner[i + 1, j], inner[i + 1, j + 1], inner[i, j + 1], -facing);
                    rock.Slot = 1;
                    rock.Quad(outer[i, j], outer[i + 1, j], outer[i + 1, j + 1], outer[i, j + 1], facing);
                    rock.Slot = 0;
                }
            // the mouths' rims, or a dead end's wall
            foreach (bool start in new[] { true, false })
            {
                int i = start ? 0 : n - 1;
                var along = (start ? path[0] - path[1] : path[n - 1] - path[n - 2]).normalized;
                for (int j = 0; j < m - 1; j++)
                    if (!cut[start ? 0 : n - 2, j]) Rim(rock, inner[i, j], inner[i, j + 1], outer[i, j + 1], outer[i, j], along);
                if (start ? shell.CloseStart : shell.CloseEnd) Cap(rock, path[i], inner, outer, i, m, along, thick, others);
            }
            Place(root, name + " shell", rock, true, CaveMaterial, CaveOutsideMaterial);
            if (shell.Floor) TunnelFloor(root, name, path, rights, w + rough * 0.5f);
        }

        /// <summary>A strip joining a hole's (or a mouth's) inner edge to its outer one.</summary>
        static void Rim(Rock rock, int a, int b, int c, int d, Vector3 facing) => rock.Quad(a, b, c, d, facing);

        /// <summary>A dead end: the section's inner ring closed by a fan, the outer one a little beyond it.</summary>
        /// <summary>A dead end's wall: rings from the section in to its middle (inside and out), open where another cave passes through.</summary>
        static void Cap(Rock rock, Vector3 center, int[,] inner, int[,] outer, int i, int m, Vector3 along, float thick, List<ClearSpace> others)
        {
            const int Rings = 5;
            var mid = center + Vector3.up * 0.2f;
            // which of the inner wall's cells are open (another cave passes through): the outer wall opens with them
            var innerRings = new Vector3[Rings + 1, m];
            for (int j = 0; j < m; j++)
                for (int r = 0; r <= Rings; r++) innerRings[r, j] = Vector3.Lerp(rock.V[inner[i, j]], mid, r / (float)Rings);
            var open = new bool[Rings, m - 1];
            for (int r = 0; r < Rings; r++)
                for (int j = 0; j < m - 1; j++)
                    open[r, j] = InCaves(others, innerRings[r, j], innerRings[r, j + 1], innerRings[r + 1, j + 1], innerRings[r + 1, j]);
            foreach (bool inside in new[] { true, false })
            {
                var heart = inside ? mid : mid + along * thick;
                var rings = new int[Rings + 1, m];
                for (int j = 0; j < m; j++)
                {
                    rings[0, j] = inside ? inner[i, j] : outer[i, j];
                    var edge = rock.V[rings[0, j]];
                    for (int r = 1; r <= Rings; r++) rings[r, j] = rock.Add(Vector3.Lerp(edge, heart, r / (float)Rings));
                }
                rock.Slot = inside ? 0 : 1;
                for (int r = 0; r < Rings; r++)
                    for (int j = 0; j < m - 1; j++)
                        if (!open[r, j]) rock.Quad(rings[r, j], rings[r, j + 1], rings[r + 1, j + 1], rings[r + 1, j], inside ? -along : along);
                rock.Slot = 0;
            }
        }

        /// <summary>A tunnel's own floor: a rock slab along its line, its top at the line, walked on.</summary>
        static void TunnelFloor(Transform root, string name, List<Vector3> path, Vector3[] rights, float half)
        {
            var rock = new Rock();
            int n = path.Count;
            var top = new int[n, 2];
            var bottom = new int[n, 2];
            for (int i = 0; i < n; i++)
                for (int s = 0; s < 2; s++)
                {
                    var edge = path[i] + rights[i] * (s == 0 ? half : -half);
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
                var along = i == 0 ? path[0] - path[1] : path[n - 1] - path[n - 2];
                rock.Quad(top[i, 0], top[i, 1], bottom[i, 1], bottom[i, 0], along);
            }
            Place(root, name + " floor", rock, false, CaveFloorMaterial);
        }

        static void BuildCavern(RockShellShape shell, Transform root, string name)
        {
            var world = shell.WorldPoints();
            if (world.Count < 3) return;
            var flat = Poly2.CounterClockwise(world.Select(p => new Vector2(p.x, p.z)).ToList());
            var ring = ResampleClosed(flat, Step * 1.5f);
            int k = ring.Count;
            float y = shell.transform.position.y, h = shell.Height, rough = shell.Roughness, thick = shell.Thickness;
            var c = new Vector2(ring.Average(p => p.x), ring.Average(p => p.y));
            if (!Poly2.Contains(flat, c)) Debug.LogWarning($"[Level] {shell.name}: its outline's middle is outside it, the dome will fold (keep a cavern's outline round)", shell);
            int seed = shell.Seed + TimberKit.Seed(shell.PieceId ?? name, 5);
            float radius = ring.Max(p => Vector2.Distance(p, c));
            int rows = Mathf.Clamp(Mathf.CeilToInt(radius / (Step * 1.5f)), 6, 40);
            // rows from the wall's foot (under the floor) up the dome to near its crown, then a fan to the crown
            var shares = new List<float> { 1f };
            for (int r = 0; r <= rows; r++) shares.Add(Mathf.Lerp(1f, 0.06f, r / (float)rows));
            Vector3 At(int col, int row)
            {
                float s = shares[row];
                var xz = c + (ring[col] - c) * s;
                float up = row == 0 ? -CaveSink : h * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(s, Squareness)), 1f / Squareness);
                return new Vector3(xz.x, y + up, xz.y);
            }
            var heart = new Vector3(c.x, y + h * 0.3f, c.y);
            var rock = new Rock();
            int rc = shares.Count;
            var inner = new int[k, rc];
            var outer = new int[k, rc];
            var normals = new Vector3[k, rc];
            for (int col = 0; col < k; col++)
                for (int row = 0; row < rc; row++)
                {
                    var p0 = At(col, row);
                    var nrm = p0 - heart;
                    if (row == 0) nrm.y = 0f;
                    nrm = nrm.sqrMagnitude < 1e-6f ? Vector3.up : nrm.normalized;
                    normals[col, row] = nrm;
                    var p = p0 + nrm * rough * Noise(p0, seed);
                    inner[col, row] = rock.Add(p);
                    outer[col, row] = rock.Add(p + nrm * thick * (0.75f + 0.5f * Noise(p0 * 1.7f, seed + 11)));
                }
            var holed = new bool[k, rc - 1];
            var others = OtherCaves(shell);
            for (int col = 0; col < k; col++)
                for (int row = 0; row < rc - 1; row++)
                {
                    int next = (col + 1) % k;
                    var mid = (rock.V[inner[col, row]] + rock.V[inner[next, row + 1]]) * 0.5f;
                    holed[col, row] = Holed(shell, mid, mid.y - y, h) ||
                                      InCaves(others, rock.V[inner[col, row]], rock.V[inner[next, row]], rock.V[inner[col, row + 1]], rock.V[inner[next, row + 1]]);
                }
            for (int col = 0; col < k; col++)
            {
                int next = (col + 1) % k, prev = (col + k - 1) % k;
                for (int row = 0; row < rc - 1; row++)
                {
                    var facing = normals[col, row] + normals[next, row + 1];
                    if (holed[col, row])
                    {
                        if (!holed[prev, row]) Rim(rock, inner[col, row], inner[col, row + 1], outer[col, row + 1], outer[col, row], rock.V[inner[col, row]] - rock.V[inner[next, row]]);
                        if (!holed[next, row]) Rim(rock, inner[next, row], inner[next, row + 1], outer[next, row + 1], outer[next, row], rock.V[inner[next, row]] - rock.V[inner[col, row]]);
                        if (row == 0 || !holed[col, row - 1]) Rim(rock, inner[col, row], inner[next, row], outer[next, row], outer[col, row], rock.V[inner[col, row]] - rock.V[inner[col, row + 1]]);
                        if (row == rc - 2 || !holed[col, row + 1]) Rim(rock, inner[col, row + 1], inner[next, row + 1], outer[next, row + 1], outer[col, row + 1], rock.V[inner[col, row + 1]] - rock.V[inner[col, row]]);
                        continue;
                    }
                    rock.Quad(inner[col, row], inner[next, row], inner[next, row + 1], inner[col, row + 1], -facing);
                    rock.Slot = 1;
                    rock.Quad(outer[col, row], outer[next, row], outer[next, row + 1], outer[col, row + 1], facing);
                    rock.Slot = 0;
                }
            }
            // the crown: a fan over the last row (inside and out)
            var crown = new Vector3(c.x, y + h, c.y);
            int ci = rock.Add(crown + Vector3.up * rough * Noise(crown, seed)), co = rock.Add(crown + Vector3.up * (rough + thick));
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

        static void CavernFloor(Transform root, string name, List<Vector2> ring, float y)
        {
            var rock = new Rock();
            int k = ring.Count;
            var top = ring.Select(p => rock.Add(new Vector3(p.x, y, p.y))).ToArray();
            var bottom = ring.Select(p => rock.Add(new Vector3(p.x, y - CaveSink, p.y))).ToArray();
            var tris = Poly2.Triangulate(ring);
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                rock.Tri(top[tris[t]], top[tris[t + 1]], top[tris[t + 2]], Vector3.up);
                rock.Tri(bottom[tris[t]], bottom[tris[t + 1]], bottom[tris[t + 2]], Vector3.down);
            }
            for (int i = 0; i < k; i++)
            {
                int next = (i + 1) % k;
                var e = ring[next] - ring[i];
                rock.Quad(top[i], top[next], bottom[next], bottom[i], new Vector3(e.y, 0f, -e.x));
            }
            Place(root, name + " floor", rock, false, CaveFloorMaterial);
        }

        /// <summary>A closed outline's points about every <paramref name="step"/> metres, its corners kept.</summary>
        public static List<Vector2> ResampleClosed(IList<Vector2> poly, float step)
        {
            var list = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
                for (int s = 0; s < n; s++) list.Add(Vector2.Lerp(a, b, s / (float)n));
            }
            return list;
        }

        // ------------------------------------------------------------------ cliffs

        public static void BuildCliff(CliffShape cliff, Transform root, string name)
        {
            if (cliff.WorldOutline().Count < 3) return;
            string material = CliffMaterial(cliff.Rock);
            if (cliff.FootBlend > 0f) material = FootMaterial(material);
            var plain = CliffRockMesh(cliff, name, CliffStep, false);
            if (!cliff.Detailed)
            {
                if (cliff.FootBlend > 0f) plain.Colors = FootColors(cliff, plain.V);
                Place(root, name + " rock", plain, false, material);
            }
            else
            {
                // the faces drawn finer than they collide: the collider is the plain build (the locks read it), the drawing the detail
                bool fine = cliff.Strata > 0.3f || cliff.Fracture > 0.3f || cliff.Rock == CliffRock.Basalt;
                var drawn = CliffRockMesh(cliff, name, fine ? FineStep : CliffStep, true);
                if (cliff.FootBlend > 0f) drawn.Colors = FootColors(cliff, drawn.V);
                PlaceDrawn(root, name + " rock", drawn, material);
                PlaceCollider(root, name + " rock collider", plain, false);
            }
            RockDressing.DressCliff(cliff, root, name);
        }

        /// <summary>The foot band of each vertex: 1 at the ground under it, 0 from <see cref="CliffShape.FootBlend"/> up.</summary>
        static List<Color> FootColors(CliffShape cliff, List<Vector3> v)
        {
            var scene = cliff.gameObject.scene;
            float band = Mathf.Max(0.05f, cliff.FootBlend);
            return v.Select(p =>
            {
                float ground = LevelNature.GroundAt(scene, new Vector2(p.x, p.z));
                if (float.IsNaN(ground)) ground = cliff.transform.position.y;
                return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - (p.y - ground) / band));
            }).ToList();
        }

        /// <summary>
        /// The material <paramref name="baseName"/> with its foot drawn in the ground's scree (the <c>_FOOTLAYER</c> of HotPatata/Nature,
        /// masked by the mesh's vertex alpha): made once beside the nature materials.
        /// </summary>
        public static string FootMaterial(string baseName)
        {
            string name = baseName + "_Foot";
            if (AssetDatabase.LoadAssetAtPath<Material>(NatureMaterialBuilder.Dir + name + ".mat") != null) return name;
            var source = NatureMaterialBuilder.Load(baseName);
            var scree = NatureMaterialBuilder.Load("Nature_RocksGround");
            var mat = new Material(source) { name = name };
            mat.EnableKeyword("_FOOTLAYER");
            mat.SetFloat("_FootLayer", 1f);
            if (scree != null)
            {
                mat.SetTexture("_FootMap", scree.GetTexture("_BaseMap"));
                mat.SetTexture("_FootBumpMap", scree.GetTexture("_BumpMap"));
            }
            AssetDatabase.CreateAsset(mat, NatureMaterialBuilder.Dir + name + ".mat");
            return name;
        }

        /// <summary>
        /// A cliff's rock: bands from its foot (sunk) to each ledge and the top, every face pushed outward only. Plain
        /// (<paramref name="detail"/> off, every <see cref="CliffStep"/>): the M20 build exactly (its collider). Detailed: fBm, strata,
        /// fracture facets, basalt's columns and the overhang, every <paramref name="step"/>.
        /// </summary>
        static Rock CliffRockMesh(CliffShape cliff, string name, float step, bool detail)
        {
            var outline = cliff.WorldOutline();
            var ring = ResampleClosed(Poly2.CounterClockwise(outline), step);
            int k = ring.Count;
            float foot = cliff.transform.position.y, rough = cliff.Roughness;
            int seed = cliff.Seed + TimberKit.Seed(cliff.PieceId ?? name, 7);

            // each column's outward direction (the bisector of its two edges), pushed so an inset keeps the faces parallel
            var outward = new Vector2[k];
            for (int i = 0; i < k; i++)
            {
                var e0 = ring[i] - ring[(i + k - 1) % k];
                var e1 = ring[(i + 1) % k] - ring[i];
                var n0 = Poly2.Outward(e0);
                var n1 = Poly2.Outward(e1);
                var bis = (n0 + n1).normalized;
                if (bis.sqrMagnitude < 1e-6f) bis = n1;
                outward[i] = bis / Mathf.Max(0.35f, Vector2.Dot(bis, n1));
            }
            // the distance round the outline at each column (the facets and columns are laid in it)
            var along = new float[k];
            for (int i = 1; i < k; i++) along[i] = along[i - 1] + Vector2.Distance(ring[i], ring[i - 1]);

            // the bands: from the foot (sunk) to the first shelf, shelf to shelf, the last up to the top; each with its inset
            var shelves = cliff.Ledges.Where(l => l.height > 0f && l.height < cliff.Height).OrderBy(l => l.height).ToList();
            var bands = new List<(float y0, float y1, float inset)>();
            float from = -cliff.Sink, inset = 0f;
            foreach (var shelf in shelves)
            {
                bands.Add((from, shelf.height, inset));
                from = shelf.height;
                inset += Mathf.Max(0f, shelf.inset);
            }
            bands.Add((from, cliff.Height, inset));

            var rock = new Rock();
            float lean = cliff.Batter;
            var caves = OtherCaves(cliff);
            // the ragged top: each column's rise above the top, smooth along the outline (only ever higher)
            var crest = new float[k];
            for (int col = 0; col < k; col++)
                crest[col] = cliff.Crest * Noise(new Vector3(ring[col].x * 1.6f, 17.3f, ring[col].y * 1.6f), seed + 23);

            // how far a face is pushed out at a point (0..rough and more for the overhang): always outward
            float Push(Vector3 p0, int col, float up)
            {
                if (!detail) return rough * Noise(p0, seed);
                float d = Fbm(p0, seed, cliff.Octaves, cliff.Lacunarity);
                if (cliff.Strata > 0f)
                {
                    float band = up / 1.6f + Noise(p0 * 0.35f, seed + 5) * 0.8f;
                    float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((band - Mathf.Floor(band) - 0.35f) / 0.3f));
                    d *= Mathf.Lerp(1f, 0.3f + 0.9f * s, cliff.Strata);
                }
                if (cliff.Rock == CliffRock.Basalt)
                {
                    // hexagonal columns: flat vertical faces, each its own depth
                    int c = Mathf.FloorToInt(along[col] / 1.1f);
                    d = Mathf.Lerp(d, 0.2f + 0.8f * Hash(c, 0, seed), 0.85f);
                }
                if (cliff.Fracture > 0f)
                {
                    // facets: the nearest jittered site in (along, up), its own plane through it
                    const float cell = 2.2f;
                    float u = along[col] / cell, v = up / cell;
                    int cu = Mathf.FloorToInt(u), cv = Mathf.FloorToInt(v);
                    float best = float.MaxValue, facet = 0f;
                    for (int du = -1; du <= 1; du++)
                        for (int dv = -1; dv <= 1; dv++)
                        {
                            int iu = cu + du, iv = cv + dv;
                            float su = iu + Hash(iu, iv, seed + 1), sv = iv + Hash(iu, iv, seed + 2);
                            float dist = (u - su) * (u - su) + (v - sv) * (v - sv);
                            if (dist >= best) continue;
                            best = dist;
                            float tiltU = (Hash(iu, iv, seed + 3) - 0.5f) * 0.6f, tiltV = (Hash(iu, iv, seed + 4) - 0.5f) * 0.6f;
                            facet = Hash(iu, iv, seed + 9) + tiltU * (u - su) + tiltV * (v - sv);
                        }
                    d = Mathf.Lerp(d, Mathf.Clamp01(facet), cliff.Fracture);
                }
                float push = rough * Mathf.Max(0f, d);
                if (cliff.Overhang > 0f) push += cliff.Overhang * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((up / Mathf.Max(0.5f, cliff.Height) - 0.62f) / 0.38f));
                return push;
            }
            Vector3 Point(int col, float up, float ins)
            {
                var xz = ring[col] - outward[col] * (ins + lean * Mathf.Max(0f, up));
                var p0 = new Vector3(xz.x, foot + up, xz.y);
                var n = new Vector3(outward[col].x, 0f, outward[col].y).normalized;
                return p0 + n * Push(p0, col, up);
            }
            int[] lastTop = null;
            for (int b = 0; b < bands.Count; b++)
            {
                var (y0, y1, ins) = bands[b];
                bool last = b == bands.Count - 1;
                int rows = Mathf.Max(1, Mathf.CeilToInt((y1 + (last ? cliff.Crest * 0.5f : 0f) - y0) / step));
                var grid = new int[k, rows + 1];
                for (int col = 0; col < k; col++)
                    for (int r = 0; r <= rows; r++)
                        grid[col, r] = rock.Add(Point(col, Mathf.Lerp(y0, y1 + (last ? crest[col] : 0f), r / (float)rows), ins));
                for (int col = 0; col < k; col++)
                {
                    int next = (col + 1) % k;
                    var e = ring[next] - ring[col];
                    var facing = new Vector3(e.y, 0f, -e.x);
                    for (int r = 0; r < rows; r++)
                    {
                        if (InCaves(caves, rock.V[grid[col, r]], rock.V[grid[next, r]], rock.V[grid[next, r + 1]], rock.V[grid[col, r + 1]])) continue;
                        rock.Quad(grid[col, r], grid[next, r], grid[next, r + 1], grid[col, r + 1], facing);
                    }
                }
                // the shelf from the band below's top edge in to this band's foot
                if (lastTop != null)
                    for (int col = 0; col < k; col++)
                    {
                        int next = (col + 1) % k;
                        if (InCaves(caves, rock.V[lastTop[col]], rock.V[lastTop[next]], rock.V[grid[next, 0]], rock.V[grid[col, 0]])) continue;
                        rock.Quad(lastTop[col], lastTop[next], grid[next, 0], grid[col, 0], Vector3.up);
                    }
                lastTop = Enumerable.Range(0, k).Select(col => grid[col, rows]).ToArray();
            }
            // the top: the last band's edge closed over
            var topRing = Enumerable.Range(0, k).Select(col => new Vector2(rock.V[lastTop[col]].x, rock.V[lastTop[col]].z)).ToList();
            var tris = Poly2.Triangulate(topRing);
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                int a = lastTop[tris[t]], b = lastTop[tris[t + 1]], c = lastTop[tris[t + 2]];
                if (InCaves(caves, rock.V[a], rock.V[b], rock.V[c])) continue;
                rock.Tri(a, b, c, Vector3.up);
            }
            return rock;
        }
    }
}
