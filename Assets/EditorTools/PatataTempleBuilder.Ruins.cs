using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// The valley PatataTemple stands in (ARCHITECTURE §4 and §25.4), decoration only: no forest of trunks round the course but the
    /// jungle seen from above, a carpet of broad crowns some ten metres under the stone, a few giants emerging far off, the river down
    /// the valley, the great stepped pyramid inside the ring with the altar over its summit, ruined towers rising out of the canopy, the
    /// columns under the causeway, and the mist in the valley (<see cref="MistField"/>), all instanced where it can be
    /// (<see cref="FoliageSet"/>). Nothing collides; nothing that looks like ground is within a jump of a floor (it stays under the
    /// kill planes); nothing comes near a pass arc. Deterministic.
    /// </summary>
    public static partial class PatataTempleBuilder
    {
        const float JungleDepth = 26f;
        const float TerrainCell = 8f, TerrainMargin = 240f;
        static string FoliagePath => NatureDressing.GeneratedDir + "PatataTemple_Foliage.asset";
        static string MistFloorPath => NatureDressing.GeneratedDir + "PatataTemple_MistFloor.asset";

        /// <summary>A deck's column, asked for while its section is built (section space) and raised once the jungle floor is known.</summary>
        struct RuinRequest
        {
            public Transform parent;
            public Vector3 localTop;
            public float thickness;
        }

        static readonly List<RuinRequest> Ruins = new List<RuinRequest>();

        sealed class Ground
        {
            public float x0, z0;
            public int nx, nz;
            public Vector3[,] points;

            public float Sample(float x, float z)
            {
                float fx = Mathf.Clamp((x - x0) / TerrainCell, 0f, nx - 1.001f), fz = Mathf.Clamp((z - z0) / TerrainCell, 0f, nz - 1.001f);
                int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz);
                float u = fx - i, v = fz - j;
                return Mathf.Lerp(Mathf.Lerp(points[i, j].y, points[i + 1, j].y, u), Mathf.Lerp(points[i, j + 1].y, points[i + 1, j + 1].y, u), v);
            }
        }

        static Ground ground;

        /// <summary>The act's jungle sound: birds at dawn and noon, crickets and wind in the storm (a fixed ambience, never a movement sound).</summary>
        static void Jungle(Transform p, SectionSpec spec)
        {
            var kind = spec.act >= 3 ? NatureAudioFactory.Ambience.Crickets : NatureAudioFactory.Ambience.Birds;
            Sound(p, new Vector3(0, Mathf.Max(0, spec.dy) + 6, spec.length * 0.5f), kind);
            if (spec.act >= 3) Sound(p, new Vector3(0, Mathf.Max(0, spec.dy) + 10, spec.length * 0.2f), NatureAudioFactory.Ambience.Wind);
        }

        static void Sound(Transform p, Vector3 local, NatureAudioFactory.Ambience kind)
        {
            var marker = new GameObject("Ambience " + kind);
            marker.transform.SetParent(p, false);
            marker.transform.localPosition = local;
            NatureKit.Ambient(marker.transform, kind.ToString(), Vector3.zero, kind, 0.7f, 45f);
        }

        static Rect FootRect(Footprint f, float grow = 0f)
        {
            var rot = Quaternion.Euler(0, f.yaw, 0);
            var a = f.origin + rot * new Vector3(-CoverHalf - grow, 0, -Hub - grow);
            var b = f.origin + rot * new Vector3(CoverHalf + grow, 0, f.length + Hub + grow);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
        }

        static float DistanceToRect(Rect r, float x, float z) =>
            new Vector2(Mathf.Max(0f, Mathf.Max(r.xMin - x, x - r.xMax)), Mathf.Max(0f, Mathf.Max(r.yMin - z, z - r.yMax))).magnitude;

        static float Noise(float x, float z, int seed) =>
            NatureShapes.Noise(new Vector3(x * 0.011f, 0.5f, z * 0.011f), seed) * 0.6f + NatureShapes.Noise(new Vector3(x * 0.04f, 1.5f, z * 0.04f), seed + 5) * 0.4f;

        /// <summary>
        /// The valley: its floor, the river, the mist, the columns under the decks, the great pyramid inside the ring, the ruined towers
        /// beyond, and the jungle's canopy, a carpet of crowns some ten metres under the stone.
        /// </summary>
        static string Dressing(Transform group)
        {
            var root = new GameObject("Jungle").transform;
            root.SetParent(group, false);
            var rects = Footprints.Select(f => FootRect(f)).ToArray();
            float minX = rects.Min(r => r.xMin) - TerrainMargin, maxX = rects.Max(r => r.xMax) + TerrainMargin;
            float minZ = rects.Min(r => r.yMin) - TerrainMargin, maxZ = rects.Max(r => r.yMax) + TerrainMargin;
            var river = RiverPath();

            // the floor: JungleDepth under the causeway and the ring (the skyway's sections float over it), rising to the valley's
            // sides, and cut by the river's bed
            float valley = Footprints.Take(10).Min(f => f.floorY) - JungleDepth;
            var g = ground = new Ground { x0 = minX, z0 = minZ, nx = Mathf.CeilToInt((maxX - minX) / TerrainCell) + 1, nz = Mathf.CeilToInt((maxZ - minZ) / TerrainCell) + 1 };
            g.points = new Vector3[g.nx, g.nz];
            for (int i = 0; i < g.nx; i++)
                for (int j = 0; j < g.nz; j++)
                {
                    float x = minX + i * TerrainCell, z = minZ + j * TerrainCell;
                    float nearest = rects.Min(r => DistanceToRect(r, x, z));
                    float h = valley + (Noise(x, z, 47) - 0.5f) * 2f * Mathf.Lerp(1.5f, 8f, Mathf.Clamp01(nearest / 90f));
                    h += 34f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((nearest - 60f) / 200f));   // the valley's sides
                    float toRiver = DistanceToPath(river, x, z);
                    h -= 4f * Mathf.Clamp01(1f - toRiver / 14f);                                      // the river's bed
                    g.points[i, j] = new Vector3(x, h, z);
                }
            var terrainRoot = new GameObject("Valley floor").transform;
            terrainRoot.SetParent(root, false);
            int chunks = NatureTerrainMesh.Build(terrainRoot, g.points, (a, b, c) => true, (i, j) => 0,
                _ => NatureMaterialBuilder.Load("Nature_LeafLitter"), NatureDressing.GeneratedDir, "PatataTemple_Terrain");
            int reaches = River(root, river, valley);

            var (mistFloor, mistRect) = NatureTerrainMesh.BakeMistFloor(g.Sample, Rect.MinMaxRect(minX, minZ, maxX, maxZ), 256, 3, MistFloorPath);
            var mist = new GameObject("Mist");
            mist.transform.SetParent(root, false);
            mist.AddComponent<MistField>().Configure(mistFloor, mistRect, 0.85f);

            int columns = Columns(root);
            int pyramid = Pyramid(root, out var pyramidBox);
            int towers = Towers(root, pyramidBox);
            string plants = Plant(root, pyramidBox);
            return $"valley floor {chunks} chunks, river {reaches} reaches, {columns} ruined columns, pyramid {pyramid} cells, {towers} towers, {plants}";
        }

        // ------------------------------------------------------------------ the river

        /// <summary>The river's line in world XZ: down the valley beside the causeway (act 1), then round the temple's platform.</summary>
        static List<Vector2> RiverPath()
        {
            var path = new List<Vector2>();
            for (int i = 0; i < 5; i++)
            {
                var f = Footprints[i];
                var rot = Quaternion.Euler(0, f.yaw, 0);
                foreach (float t in new[] { 0f, 0.5f })
                {
                    var w = f.origin + rot * new Vector3(i % 2 == 0 ? 52f : 44f, 0, f.length * t);
                    path.Add(new Vector2(w.x, w.z));
                }
            }
            var first = Footprints[0];
            var back = first.origin + Quaternion.Euler(0, first.yaw, 0) * new Vector3(56f, 0, -160f);
            path.Insert(0, new Vector2(back.x, back.z));
            var last = Footprints[4];
            var on = last.origin + Quaternion.Euler(0, last.yaw, 0) * new Vector3(70f, 0, last.length + 120f);
            path.Add(new Vector2(on.x, on.z));
            return path;
        }

        static float DistanceToPath(List<Vector2> path, float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector2 a = path[i], ab = path[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }

        /// <summary>The river: a reach of water per stretch of its line, on the bed cut in the floor (far under every kill plane).</summary>
        static int River(Transform root, List<Vector2> path, float valley)
        {
            var parent = new GameObject("River").transform;
            parent.SetParent(root, false);
            for (int i = 0; i + 1 < path.Count; i++)
            {
                var a = new Vector3(path[i].x, 0, path[i].y);
                var b = new Vector3(path[i + 1].x, 0, path[i + 1].y);
                var reach = new GameObject("Reach " + (i + 1)).transform;
                reach.SetParent(parent, false);
                reach.SetPositionAndRotation(a, Quaternion.LookRotation(b - a));
                NatureKit.Water(reach, "Water", -7f, 7f, -6f, Vector3.Distance(a, b) + 6f, valley - 2.2f, valley - 4.5f);
            }
            return path.Count - 1;
        }

        // ------------------------------------------------------------------ the pyramid and the towers

        const float PyramidCell = 6f, PyramidStep = 3.5f, PyramidTread = 5f, PyramidTopHalf = 9f;
        const float KillClearance = 12f;   // a stone face stays this far under any floor near it: a fall dies before it lands

        /// <summary>
        /// The great pyramid inside the ring, stepped, its summit under the altar (decoration, no collider). Each column of it stays
        /// <see cref="KillClearance"/> under every floor within a long jump of it, so nothing that looks like ground is ever reachable:
        /// it steps back from the ring's inner edge and sinks under the skyway.
        /// </summary>
        static int Pyramid(Transform root, out Bounds box)
        {
            // its centre: the middle of the ring (the act 2 sections' starts and the last one's end)
            var ring = Footprints.Skip(5).Take(5).Select(f => f.origin).ToList();
            ring.Add(Footprints[10].origin);
            var c = new Vector3(ring.Average(v => v.x), 0f, ring.Average(v => v.z));
            float top = Footprints[14].floorY - KillClearance;
            float bottom = ground.Sample(c.x, c.z) - 1f;
            int steps = Mathf.CeilToInt((top - bottom) / PyramidStep);
            float half = PyramidTopHalf + steps * PyramidTread;
            int n = Mathf.CeilToInt(2f * half / PyramidCell);
            var heights = new float[n, n];
            int solid = 0;
            int course = LayerMask.GetMask("Environment", "Hazard", BodyScreen.LayerName);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float x = c.x - half + (i + 0.5f) * PyramidCell, z = c.z - half + (j + 0.5f) * PyramidCell;
                    float d = Mathf.Max(Mathf.Abs(x - c.x), Mathf.Abs(z - c.z));
                    float h = d <= PyramidTopHalf ? top : top - PyramidStep * Mathf.Ceil((d - PyramidTopHalf) / PyramidTread);
                    // stay well under every floor a long jump away (and under the decks overhead)
                    float reach = 14f + PyramidCell * 0.5f;
                    foreach (var col in Physics.OverlapBox(new Vector3(x, 0f, z), new Vector3(reach, 200f, reach), Quaternion.identity, course, QueryTriggerInteraction.Ignore))
                        if (col.GetComponentInParent<KillZone>() == null) h = Mathf.Min(h, col.bounds.min.y - KillClearance);
                    heights[i, j] = h;
                    if (h > bottom + 0.5f) solid++;
                }
            var mesh = StepMesh(heights, c.x - half, c.z - half, PyramidCell, bottom);
            System.IO.Directory.CreateDirectory(NatureDressing.GeneratedDir.TrimEnd('/'));
            string path = NatureDressing.GeneratedDir + "PatataTemple_Pyramid.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { existing.Clear(); EditorUtility.CopySerialized(mesh, existing); mesh = existing; }
            var go = new GameObject("Great pyramid");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = NatureMaterialBuilder.Load(TempleMaterials.Wall);
            go.AddComponent<CourseDecoration>();
            go.isStatic = true;
            box = new Bounds(new Vector3(c.x, (top + bottom) / 2f, c.z), new Vector3(2f * half, top - bottom, 2f * half));
            return solid;
        }

        /// <summary>A stepped block mesh from a grid of column heights (tops, and the sides where a neighbour is lower), down to <paramref name="bottom"/>.</summary>
        static Mesh StepMesh(float[,] h, float x0, float z0, float cell, float bottom)
        {
            int nx = h.GetLength(0), nz = h.GetLength(1);
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c2, Vector3 d, Vector3 normal)
            {
                int k = verts.Count;
                verts.AddRange(new[] { a, b, c2, d });
                normals.AddRange(new[] { normal, normal, normal, normal });
                tris.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }
            float H(int i, int j) => i < 0 || j < 0 || i >= nx || j >= nz ? bottom : Mathf.Max(bottom, h[i, j]);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float top = H(i, j);
                    if (top <= bottom + 0.01f) continue;
                    float xa = x0 + i * cell, xb = xa + cell, za = z0 + j * cell, zb = za + cell;
                    Quad(new Vector3(xa, top, za), new Vector3(xa, top, zb), new Vector3(xb, top, zb), new Vector3(xb, top, za), Vector3.up);
                    float w = H(i - 1, j), e = H(i + 1, j), s = H(i, j - 1), n = H(i, j + 1);
                    if (w < top) Quad(new Vector3(xa, w, zb), new Vector3(xa, top, zb), new Vector3(xa, top, za), new Vector3(xa, w, za), Vector3.left);
                    if (e < top) Quad(new Vector3(xb, e, za), new Vector3(xb, top, za), new Vector3(xb, top, zb), new Vector3(xb, e, zb), Vector3.right);
                    if (s < top) Quad(new Vector3(xa, s, za), new Vector3(xa, top, za), new Vector3(xb, top, za), new Vector3(xb, s, za), Vector3.back);
                    if (n < top) Quad(new Vector3(xb, n, zb), new Vector3(xb, top, zb), new Vector3(xa, top, zb), new Vector3(xa, n, zb), Vector3.forward);
                }
            var mesh = new Mesh { name = "PatataTemple_Pyramid", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Ruined towers rising out of the canopy beyond the course (decoration): stacked, ever narrower blocks of masonry.</summary>
        static int Towers(Transform root, Bounds pyramid)
        {
            var parent = new GameObject("Towers").transform;
            parent.SetParent(root, false);
            var rng = new Random(733);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var rects = Footprints.Select(f => FootRect(f, 30f)).ToArray();
            var stone = NatureMaterialBuilder.Load(TempleMaterials.Wall);
            int count = 0;
            for (int attempt = 0; attempt < 200 && count < 9; attempt++)
            {
                var anchor = Footprints[rng.Next(Footprints.Count)];
                float angle = R(0f, 360f), distance = R(70f, 190f);
                var at = anchor.origin + Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
                if (rects.Any(r => r.Contains(new Vector2(at.x, at.z)))) continue;
                if (new Rect(pyramid.min.x - 20f, pyramid.min.z - 20f, pyramid.size.x + 40f, pyramid.size.z + 40f).Contains(new Vector2(at.x, at.z))) continue;
                float foot = ground.Sample(at.x, at.z) - 1f, height = R(30f, 52f), width = R(8f, 12f), y = foot;
                int tiers = rng.Next(3, 6);
                for (int t = 0; t < tiers && y < foot + height; t++)
                {
                    float h = height / tiers * R(0.8f, 1.2f);
                    var block = NatureKit.Detail(parent, "Tower", new Vector3(at.x, y + h / 2f, at.z), new Vector3(width, h, width), KitRole.Wall,
                                                 Quaternion.Euler(0, R(-6f, 6f) + angle, R(-1.5f, 1.5f)));
                    CourseKit.Skin(block.GetComponentInChildren<Renderer>()?.gameObject ?? block, KitShape.BevelBox, KitColor.Neutral, stone);
                    y += h;
                    width *= R(0.7f, 0.85f);
                }
                count++;
            }
            return count;
        }

        /// <summary>A ruined column of masonry under each deck, from its underside down to the jungle floor (no collider).</summary>
        static int Columns(Transform root)
        {
            var parent = new GameObject("Columns").transform;
            parent.SetParent(root, false);
            var stone = AssetDatabase.LoadAssetAtPath<Material>(CourseKit.MatDir + CourseKit.NatureDir + TempleMaterials.Wall + ".mat");
            int count = 0;
            int course = LayerMask.GetMask("Environment", "Hazard", BodyScreen.LayerName);
            foreach (var r in Ruins)
            {
                var top = r.parent.TransformPoint(r.localTop);
                float foot = ground.Sample(top.x, top.z) - 1f;
                float height = top.y - foot;
                if (height < 1f) continue;
                // a column of the skyway never goes down through the ring or the causeway below it
                var span = new Vector3(r.thickness / 2f + 0.5f, (height - 1.5f) / 2f, r.thickness / 2f + 0.5f);
                if (Physics.OverlapBox(new Vector3(top.x, foot + (height - 1.5f) / 2f, top.z), span, r.parent.rotation, course, QueryTriggerInteraction.Ignore)
                        .Any(c => c.GetComponentInParent<KillZone>() == null)) continue;
                var yaw = r.parent.rotation;
                var column = NatureKit.Detail(parent, "Column", new Vector3(top.x, (top.y + foot) / 2f, top.z), new Vector3(r.thickness, height, r.thickness), KitRole.Wall, yaw);
                var visual = column.GetComponentInChildren<Renderer>()?.gameObject ?? column;
                CourseKit.Skin(visual, KitShape.BevelBox, KitColor.Neutral, stone);
                count++;
            }
            return count;
        }

        // ------------------------------------------------------------------ the canopy

        static Mesh M(string kind, int variant, int lod) => AssetDatabase.LoadAssetAtPath<Mesh>(NatureTreeBuilder.MeshPath(kind, variant, lod));

        sealed class Bins
        {
            readonly Dictionary<FoliageSet.Batch, Dictionary<(int, int), List<(Vector3, float, float)>>> bins =
                new Dictionary<FoliageSet.Batch, Dictionary<(int, int), List<(Vector3, float, float)>>>();

            public int Count;

            public void Add(FoliageSet.Batch batch, Vector3 p, float yaw, float scale)
            {
                if (!bins.TryGetValue(batch, out var cells)) bins[batch] = cells = new Dictionary<(int, int), List<(Vector3, float, float)>>();
                var key = (Mathf.FloorToInt(p.x / FoliageSet.CellSize), Mathf.FloorToInt(p.z / FoliageSet.CellSize));
                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<(Vector3, float, float)>();
                list.Add((p, yaw, Mathf.Clamp(scale, batch.minScale, batch.maxScale)));
                Count++;
            }

            public void Pack()
            {
                foreach (var (batch, cells) in bins)
                    foreach (var ((cx, cz), list) in cells)
                    {
                        float minY = list.Min(t => t.Item1.y), maxY = list.Max(t => t.Item1.y);
                        var cell = new FoliageSet.Cell { origin = new Vector3(cx * FoliageSet.CellSize, minY, cz * FoliageSet.CellSize), height = Mathf.Max(0.01f, maxY - minY) };
                        FoliageSet.Pack(batch, cell, list);
                        batch.cells.Add(cell);
                    }
            }
        }

        static Bounds WorldBox(FoliageSet.Batch batch, Vector3 p, float yaw, float scale)
        {
            var local = batch.LocalBounds(0);
            var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
            var b = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int c = 0; c < 8; c++)
                b.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
            return b;
        }

        static bool HitsTheCourse(Bounds b)
        {
            foreach (var c in Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
                if (c.GetComponentInParent<KillZone>() == null) return true;
            return false;
        }

        /// <summary>
        /// The jungle seen from the stone: a near-continuous carpet of broad crowns whose tops stay under every floor near them, a few
        /// giants emerging far from the course, and under the carpet ferns and broad-leaved plants where gaps let them show.
        /// </summary>
        static string Plant(Transform root, Bounds pyramid)
        {
            var set = AssetDatabase.LoadAssetAtPath<FoliageSet>(FoliagePath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<FoliageSet>();
                AssetDatabase.CreateAsset(set, FoliagePath);
            }
            set.batches.Clear();
            var bins = new Bins();
            NatureDressing.IndexArcs(NatureDressing.PassSamples());
            var rng = new Random(1517);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            T Pick<T>(T[] options) => options[rng.Next(options.Length)];
            var rects = Footprints.Select(f => FootRect(f)).ToArray();
            bool InPyramid(Vector3 p) => p.x > pyramid.min.x - 2f && p.x < pyramid.max.x + 2f && p.z > pyramid.min.z - 2f && p.z < pyramid.max.z + 2f;

            // ---- batches
            var crowns = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
            {
                var s = NatureTreeBuilder.Species.Broadleaf;
                var lods = new[] { M(s.ToString(), v, 0), M(s.ToString(), v, 1), M(s.ToString(), v, 2) };
                var b = NatureDressing.Batch(set, $"Canopy {v}", lods, NatureTreeBuilder.MaterialsFor(s, lods[0]), new[] { 80f, 200f, 420f }, new[] { true, false, false }, 1.2f, 2.4f);
                b.shadowDistance = 80f;
                return b;
            }).ToArray();
            var giants = new FoliageSet.Batch[NatureTreeBuilder.Variants][];
            var (bark, leaves) = NatureTreeBuilder.MaterialsFor(NatureTreeBuilder.Giant.Oak);
            for (int v = 0; v < NatureTreeBuilder.Variants; v++)
            {
                giants[v] = new FoliageSet.Batch[3];
                for (int part = 0; part < 3; part++)
                {
                    string k = NatureTreeBuilder.GiantKind(NatureTreeBuilder.Giant.Oak, part);
                    var lods = part == 0 ? new[] { M(k, v, 0), M(k, v, 1) } : new[] { M(k, v, 0), M(k, v, 1), M(k, v, 2) };
                    var materials = part == 2 ? new[] { NatureMaterialBuilder.Load(bark), NatureMaterialBuilder.Load(leaves) } : new[] { NatureMaterialBuilder.Load(bark) };
                    giants[v][part] = NatureDressing.Batch(set, $"Emergent {k} {v}", lods, materials, part == 0 ? new[] { 60f, 160f } : new[] { 120f, 300f, 600f },
                                                           part == 0 ? new[] { false, false } : new[] { true, false, false }, 0.45f, 0.75f);
                }
            }
            var ferns = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                NatureDressing.Batch(set, "Fern " + v, new[] { M("Fern", v, 0) }, new[] { NatureMaterialBuilder.Load("Nature_Fern") }, new[] { 60f }, new[] { false }, 2.2f, 4f)).ToArray();
            var jungle = NatureDressing.ModelBatches(set, "calathea_orbifolia_01", "Nature_Calathea", 0.9f, new[] { 50f }, new[] { -1 }, new[] { false }, 1.6f, 3.2f)
                .Concat(NatureDressing.ModelBatches(set, "anthurium_botany_01", "Nature_Anthurium", 1.1f, new[] { 50f }, new[] { -1 }, new[] { false }, 1.4f, 2.8f)).ToArray();

            // ---- the carpet: crowns every ~9 m, their tops kept 7 m or more under any floor within 20 m (and off the pyramid)
            int carpet = 0;
            float minX = ground.x0, minZ = ground.z0, maxX = ground.x0 + (ground.nx - 1) * TerrainCell, maxZ = ground.z0 + (ground.nz - 1) * TerrainCell;
            int course = LayerMask.GetMask("Environment", "Hazard", BodyScreen.LayerName);
            for (float x = minX + 4.5f; x < maxX - 4.5f; x += 9f)
                for (float z = minZ + 4.5f; z < maxZ - 4.5f; z += 9f)
                {
                    var at = new Vector3(x + R(-3.5f, 3.5f), 0f, z + R(-3.5f, 3.5f));
                    if (InPyramid(at)) continue;
                    float near = rects.Min(r => DistanceToRect(r, at.x, at.z));
                    if (near > 260f) continue;
                    at.y = ground.Sample(at.x, at.z) - 0.4f;
                    float ceiling = float.MaxValue;
                    foreach (var col in Physics.OverlapBox(new Vector3(at.x, 0f, at.z), new Vector3(20f, 200f, 20f), Quaternion.identity, course, QueryTriggerInteraction.Ignore))
                        if (col.GetComponentInParent<KillZone>() == null) ceiling = Mathf.Min(ceiling, col.bounds.min.y - 7f);
                    var batch = Pick(crowns);
                    float crownHeight = batch.LocalBounds(0).max.y;
                    float scale = R(1.5f, 2.3f);
                    if (at.y + crownHeight * scale > ceiling) scale = (ceiling - at.y) / crownHeight;
                    if (scale < 1.2f) continue;
                    float yaw = R(0f, 360f);
                    if (NatureDressing.TouchesAnArc(batch, at, yaw, scale)) continue;
                    bins.Add(batch, at, yaw, scale);
                    carpet++;
                }

            // ---- emergent giants: only far from the course, out of the valley's middle
            int emergent = 0;
            var boles = new List<Vector2>();
            for (int attempt = 0; attempt < 600 && emergent < 60; attempt++)
            {
                var at = new Vector3(R(minX + 20f, maxX - 20f), 0f, R(minZ + 20f, maxZ - 20f));
                float near = rects.Min(r => DistanceToRect(r, at.x, at.z));
                if (near < 80f || near > 240f || InPyramid(at)) continue;
                if (boles.Any(b => (b - new Vector2(at.x, at.z)).magnitude < 40f)) continue;
                at.y = ground.Sample(at.x, at.z) - 0.6f;
                int v = rng.Next(NatureTreeBuilder.Variants);
                float yaw = R(0f, 360f), scale = R(0.5f, 0.72f);
                if (HitsTheCourse(WorldBox(giants[v][2], at, yaw, scale))) continue;
                for (int part = 0; part < 3; part++) bins.Add(giants[v][part], at, yaw, scale);
                boles.Add(new Vector2(at.x, at.z));
                emergent++;
            }

            // ---- under the carpet: ferns and broad leaves along the river and in the clearings, seen from the edges
            int cover = 0;
            for (float x = minX + 3f; x < maxX - 3f; x += 6f)
                for (float z = minZ + 3f; z < maxZ - 3f; z += 6f)
                {
                    var at = new Vector3(x + R(-2.5f, 2.5f), 0f, z + R(-2.5f, 2.5f));
                    if (InPyramid(at) || rects.Min(r => DistanceToRect(r, at.x, at.z)) > 120f || rng.NextDouble() > 0.5) continue;
                    at.y = ground.Sample(at.x, at.z);
                    var batch = rng.NextDouble() < 0.5 && jungle.Length > 0 ? Pick(jungle) : Pick(ferns);
                    float scale = R(batch.minScale, batch.maxScale), yaw = R(0f, 360f);
                    if (NatureDressing.TouchesAnArc(batch, at, yaw, scale)) continue;
                    bins.Add(batch, at, yaw, scale);
                    cover++;
                }
            bins.Pack();
            EditorUtility.SetDirty(set);
            var go = new GameObject("Foliage");
            go.transform.SetParent(root, false);
            go.AddComponent<FoliageInstancer>().Configure(set);
            return $"{carpet} canopy crowns, {emergent} emergent giants, {cover} ferns and jungle plants ({bins.Count} instances)";
        }
    }
}
