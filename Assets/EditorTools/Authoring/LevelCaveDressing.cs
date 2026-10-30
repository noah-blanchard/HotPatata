using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// A cave's dressing (ARCHITECTURE §27.6, docs/LEVEL_AUTHORING.md §29): stalactites from its roof (their tips never under 2.4 m over
    /// its floor), stalagmites on its floor (small ones off the walk's 1.5 m clearance, drawn only; large ones outside a 3 m corridor,
    /// solid), columns floor to roof outside the corridor (solid), puddles (wet mud decals, never water), cave mushrooms, and lanterns along the
    /// walk on alternating walls. The corridor is the tunnel's centre line, or in a chamber the lines from the tunnels meeting it to its
    /// middle. Built with the shell (<see cref="BuildShell"/>), seeded by the piece.
    /// </summary>
    public static partial class LevelRock
    {
        /// <summary>No stalactite's tip is lower than this over the floor under it (m).</summary>
        public const float StalactiteClear = 2.4f;
        /// <summary>A small stalagmite stands this far from the walk at least; a large one, a column, this far.</summary>
        public const float WalkClear = 1.5f, Corridor = 3f;
        /// <summary>A stalagmite taller than this is large (solid).</summary>
        public const float LargeStalagmite = 1.5f;
        const string MushroomA = "Assets/Prefabs/Props/Plants/cave_mushroom_01.prefab", MushroomB = "Assets/Prefabs/Props/Plants/cave_mushroom_02.prefab";

        /// <summary>A cave's floor and roof at a point (world x, z): false when the point is not in it.</summary>
        delegate bool Span(Vector2 q, out float floor, out float roof);

        /// <summary>The walk lines through a cave (its corridor): a tunnel's centre line, or the lines from the tunnels meeting a chamber to its middle.</summary>
        public static List<List<Vector3>> CorridorOf(RockShellShape shell)
        {
            var lines = new List<List<Vector3>>();
            var world = shell.WorldPoints();
            if (world.Count < 2) return lines;
            if (shell.Kind == RockShellKind.Tunnel)
            {
                lines.Add(Curves.Resample(Curves.Spline(world, Step * 0.5f), Step));
                return lines;
            }
            var flat = world.Select(p => new Vector2(p.x, p.z)).ToList();
            var c = new Vector3(flat.Average(p => p.x), shell.transform.position.y, flat.Average(p => p.y));
            var scene = shell.gameObject.scene;
            if (!scene.IsValid()) return lines;
            foreach (var tunnel in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RockShellShape>(true)).Where(t => t.Kind == RockShellKind.Tunnel))
            {
                var t = tunnel.WorldPoints();
                if (t.Count < 2) continue;
                foreach (var end in new[] { t[0], t[t.Count - 1] })
                    if (Poly2.Contains(flat, new Vector2(end.x, end.z)) || Poly2.DistanceToEdge(flat, new Vector2(end.x, end.z)) < 2f)
                        lines.Add(new List<Vector3> { end, c });
            }
            return lines;
        }

        static float DistanceToLines(List<List<Vector3>> lines, Vector2 q)
        {
            float best = float.MaxValue;
            foreach (var line in lines)
                if (line.Count > 1) best = Mathf.Min(best, GroundShapes.DistanceToLine(line.Select(p => new Vector2(p.x, p.z)).ToList(), q, out _, out _));
            return best;
        }

        /// <summary>The cave's floor and roof over a point, read from its clear space (marching up from its floor).</summary>
        static Span SpanOf(RockShellShape shell, ClearSpace clear)
        {
            if (shell.Kind == RockShellKind.Tunnel)
            {
                var world = shell.WorldPoints();
                var path = Curves.Resample(Curves.Spline(world, Step * 0.5f), Step);
                var rings = TunnelV2(shell) ? Rings(shell, world, path) : null;
                return (Vector2 q, out float floor, out float roof) =>
                {
                    floor = roof = 0f;
                    int best = 0;
                    float bd = float.MaxValue;
                    for (int i = 0; i < path.Count; i++)
                    {
                        float d = (new Vector2(path[i].x, path[i].z) - q).sqrMagnitude;
                        if (d < bd) { bd = d; best = i; }
                    }
                    floor = path[best].y + (rings != null ? rings[best].floorOffset : 0f);
                    return March(clear, q, floor, out roof);
                };
            }
            float y = shell.transform.position.y;
            return (Vector2 q, out float floor, out float roof) =>
            {
                floor = y;
                return March(clear, q, floor, out roof);
            };
        }

        static bool March(ClearSpace clear, Vector2 q, float floor, out float roof)
        {
            roof = floor;
            var p = new Vector3(q.x, floor + 0.1f, q.y);
            if (!clear.Contains(p)) return false;
            float step = 0.25f;
            while (clear.Contains(p + Vector3.up * step) && p.y < floor + 60f) p += Vector3.up * step;
            roof = p.y + step * 0.5f;
            return roof - floor > 1f;
        }

        /// <summary>Dresses a cave (nothing when its dressing is all 0). Returns how many lanterns it hung.</summary>
        static int Dress(RockShellShape shell, Transform root, string name)
        {
            var d = shell.Dressing;
            if (!d.Any) return 0;
            var clear = Clear(shell);
            var b = clear.bounds;
            if (b.size.x < 1f || b.size.z < 1f) return 0;
            var span = SpanOf(shell, clear);
            var corridor = CorridorOf(shell);
            var rng = new Random(shell.Seed + TimberKit.Seed(shell.PieceId ?? name, 17));
            float area = b.size.x * b.size.z;
            var drawn = new Rock();
            var solid = new Rock();
            var skylights = shell.Skylights.Select(s => (at: shell.transform.TransformPoint(new Vector3(s.at.x, 0f, s.at.y)), r: s.radius + 1f)).ToList();
            bool UnderSky(Vector2 q) => skylights.Any(s => new Vector2(q.x - s.at.x, q.y - s.at.z).magnitude < s.r) || shell.Kind == RockShellKind.Shaft && shell.OpenTop;
            Vector2 Sample() => new Vector2(Mathf.Lerp(b.min.x, b.max.x, (float)rng.NextDouble()), Mathf.Lerp(b.min.z, b.max.z, (float)rng.NextDouble()));
            float R(float lo, float hi) => Mathf.Lerp(lo, hi, (float)rng.NextDouble());

            // stalactites: from the roof, their tips never under StalactiteClear over the floor
            for (int i = 0, n = Mathf.RoundToInt(d.stalactites * area / 6f); i < n; i++)
            {
                var q = Sample();
                if (UnderSky(q) || !span(q, out float floor, out float roof)) continue;
                float length = Mathf.Min(R(0.2f, 2.5f), roof - floor - StalactiteClear);
                if (length < 0.2f) continue;
                Cone(drawn, new Vector3(q.x, roof + 0.4f, q.y), Vector3.down, R(0.12f, 0.35f) + length * 0.08f, length + 0.4f, rng.Next());
            }
            // stalagmites: small ones off the walk's clearance (drawn), large ones outside the corridor (solid)
            for (int i = 0, n = Mathf.RoundToInt(d.stalagmites * area / 8f); i < n; i++)
            {
                var q = Sample();
                if (!span(q, out float floor, out float roof)) continue;
                float off = DistanceToLines(corridor, q);
                bool large = rng.NextDouble() < 0.3 && off > Corridor;
                if (!large && off < WalkClear) continue;
                float height = Mathf.Min(large ? R(LargeStalagmite, 4f) : R(0.3f, LargeStalagmite - 0.05f), roof - floor - 0.6f);
                if (height < 0.3f) continue;
                Cone(large ? solid : drawn, new Vector3(q.x, floor - 0.3f, q.y), Vector3.up, R(0.25f, 0.45f) + height * 0.12f, height + 0.3f, rng.Next());
            }
            // columns: floor to roof, outside the corridor (solid)
            for (int i = 0, n = Mathf.RoundToInt(d.columns * area / 120f); i < n; i++)
            {
                var q = Sample();
                if (UnderSky(q) || !span(q, out float floor, out float roof) || DistanceToLines(corridor, q) < Corridor || roof - floor > 14f) continue;
                Column(solid, new Vector3(q.x, floor - 0.3f, q.y), roof - floor + 0.8f, R(0.35f, 0.7f), rng.Next());
            }
            if (drawn.Triangles > 0) Draw(root, name + " dripstone", drawn, CaveMaterial);
            if (solid.Triangles > 0) Place(root, name + " dripstone (solid)", solid, false, CaveMaterial);
            // puddles: wet mud on the floor (a cave has no sky for a wet patch to shine)
            for (int i = 0, n = Mathf.RoundToInt(d.puddles * area / 40f); i < n; i++)
            {
                var q = Sample();
                if (!span(q, out float floor, out _)) continue;
                DecalScatter.Lay(root, name + " puddle", new Vector3(q.x, floor, q.y), Vector3.up, R(1.2f, 3f), DecalKind.Mud, rng.Next(4), R(0f, 360f), shell.gameObject.scene);
            }
            // cave mushrooms off the walk
            var mushrooms = new[] { MushroomA, MushroomB }.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(m => m != null).ToArray();
            for (int i = 0, n = mushrooms.Length == 0 ? 0 : Mathf.RoundToInt(d.props * area / 30f); i < n; i++)
            {
                var q = Sample();
                if (!span(q, out float floor, out _) || DistanceToLines(corridor, q) < WalkClear) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(mushrooms[rng.Next(mushrooms.Length)], shell.gameObject.scene);
                go.name = name + " mushroom";
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(new Vector3(q.x, floor, q.y), Quaternion.Euler(0f, R(0f, 360f), 0f));
                go.transform.localScale = Vector3.one * R(0.7f, 1.4f);
                foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            }
            return d.lanternSpacing > 0f ? Lanterns(shell, root, name, corridor, clear, span, d.lanternSpacing) : 0;
        }

        /// <summary>Lanterns along the walk every <paramref name="spacing"/> metres, on alternating walls, hung under the roof's side.</summary>
        static int Lanterns(RockShellShape shell, Transform root, string name, List<List<Vector3>> corridor, ClearSpace clear, Span span, float spacing)
        {
            int hung = 0, side = 1;
            foreach (var line in corridor)
            {
                float length = Curves.Length(line);
                for (float s = spacing * 0.5f; s < length; s += spacing)
                {
                    var (p, tangent) = Curves.At(line, s);
                    var flat = new Vector3(tangent.x, 0f, tangent.z);
                    if (flat.sqrMagnitude < 1e-6f) continue;
                    var right = Vector3.Cross(Vector3.up, flat.normalized) * side;
                    side = -side;
                    var q = new Vector2(p.x, p.z);
                    if (!span(q, out float floor, out _)) continue;
                    // out toward the wall while there is room for the lantern under the rock
                    var hook = new Vector3(p.x, floor + 2.6f, p.z);
                    for (int k = 0; k < 40 && clear.Contains(hook + right * 0.9f + Vector3.up * 0.3f); k++) hook += right * 0.25f;
                    if (TimberMine.Lantern(root, name + " lantern", hook) != null) hung++;
                }
            }
            return hung;
        }

        /// <summary>A dripstone cone: rings of noise from its base (radius) along <paramref name="dir"/> to its tip.</summary>
        static void Cone(Rock rock, Vector3 at, Vector3 dir, float radius, float length, int seed)
        {
            const int Sides = 7, Rings = 6;
            var side = Vector3.Cross(dir, Vector3.forward).sqrMagnitude < 1e-4f ? Vector3.right : Vector3.Cross(dir, Vector3.forward).normalized;
            var other = Vector3.Cross(dir, side);
            var grid = new int[Rings, Sides];
            for (int r = 0; r < Rings; r++)
            {
                float t = r / (float)Rings, rad = radius * Mathf.Pow(1f - t, 1.4f);
                var c = at + dir * length * t;
                for (int a = 0; a < Sides; a++)
                {
                    float ang = a * Mathf.PI * 2f / Sides;
                    var o = side * Mathf.Cos(ang) + other * Mathf.Sin(ang);
                    grid[r, a] = rock.Add(c + o * rad * (1f + 0.25f * Noise(c * 3f + o, seed)));
                }
            }
            int tip = rock.Add(at + dir * length);
            for (int r = 0; r < Rings - 1; r++)
                for (int a = 0; a < Sides; a++)
                {
                    int n = (a + 1) % Sides;
                    var mid = (rock.V[grid[r, a]] + rock.V[grid[r + 1, n]]) * 0.5f;
                    var axis = at + dir * Vector3.Dot(mid - at, dir);
                    rock.Quad(grid[r, a], grid[r, n], grid[r + 1, n], grid[r + 1, a], mid - axis);
                }
            for (int a = 0; a < Sides; a++)
            {
                int n = (a + 1) % Sides;
                var mid = (rock.V[grid[Rings - 1, a]] + rock.V[grid[Rings - 1, n]]) * 0.5f;
                rock.Tri(grid[Rings - 1, a], grid[Rings - 1, n], tip, mid - (at + dir * length * 0.9f));
            }
        }

        /// <summary>A column: two cones met at a waist.</summary>
        static void Column(Rock rock, Vector3 foot, float height, float radius, int seed)
        {
            const int Sides = 9, Rows = 8;
            var grid = new int[Rows + 1, Sides];
            for (int r = 0; r <= Rows; r++)
            {
                float t = r / (float)Rows, rad = radius * (0.45f + 0.55f * Mathf.Abs(t * 2f - 1f));
                var c = foot + Vector3.up * height * t;
                for (int a = 0; a < Sides; a++)
                {
                    float ang = a * Mathf.PI * 2f / Sides;
                    var o = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    grid[r, a] = rock.Add(c + o * rad * (1f + 0.2f * Noise(c * 2f + o, seed)));
                }
            }
            for (int r = 0; r < Rows; r++)
                for (int a = 0; a < Sides; a++)
                {
                    int n = (a + 1) % Sides;
                    var mid = (rock.V[grid[r, a]] + rock.V[grid[r + 1, n]]) * 0.5f;
                    rock.Quad(grid[r, a], grid[r, n], grid[r + 1, n], grid[r + 1, a], mid - new Vector3(foot.x, mid.y, foot.z));
                }
        }

        /// <summary>Puts a built rock mesh under <paramref name="root"/>, drawn only (no collider).</summary>
        static GameObject Draw(Transform root, string name, Rock rock, string material)
        {
            if (rock.Triangles == 0) return null;
            var mesh = OrganicAssets.Save(rock.ToMesh(name + " rock"));
            var go = new GameObject(name) { layer = CourseKit.Layer("Environment") };
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = NatureMaterialBuilder.Load(material);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }
    }
}
