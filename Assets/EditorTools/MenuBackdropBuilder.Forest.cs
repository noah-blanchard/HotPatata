using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// The menu's forest clearing (ARCHITECTURE §6.2 and §25.3): a grass floor, flat round the show and rising a little far off;
    /// trees all round (broadleaf and firs near, giant oaks beyond) that dissolve into the mist, so there is no wall and no
    /// horizon, only depth; ferns, bushes and grass in the clearing; light shafts hanging behind the show towards the low sun; the
    /// mist pooled on the floor (<see cref="MistField"/>). Nothing stands between a camera spot and its board or the show, and no
    /// tree in the clearing. Decoration only, instanced (<see cref="FoliageSet"/>), deterministic.
    /// </summary>
    public static partial class MenuBackdropBuilder
    {
        const float ShowZ = 1.5f;              // the show's centre (the players' arc)
        const float ClearingRadius = 15f;      // no tree trunk within this of the show
        const float GroundCell = 4f, GroundHalf = 200f;
        static string FoliagePath => NatureDressing.GeneratedDir + "Menu_Foliage.asset";
        static string MistFloorPath => NatureDressing.GeneratedDir + "Menu_MistFloor.asset";
        static readonly Vector3 ShowCentre = new Vector3(0f, 0f, ShowZ);

        /// <summary>The floor's height: flat round the show and the stations, a gentle roll and a slow rise beyond.</summary>
        static float GroundHeight(float x, float z)
        {
            float d = new Vector2(x, z - ShowZ).magnitude;
            float roll = (NatureShapes.Noise(new Vector3(x * 0.03f, 0.5f, z * 0.03f), 71) - 0.5f) * 1.6f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 24f) / 20f)) * roll + 7f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 50f) / 140f));
        }

        static void BuildClearing(Transform p)
        {
            int n = Mathf.CeilToInt(2f * GroundHalf / GroundCell) + 1;
            var points = new Vector3[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float x = -GroundHalf + i * GroundCell, z = ShowZ - GroundHalf + j * GroundCell;
                    points[i, j] = new Vector3(x, GroundHeight(x, z), z);
                }
            var floor = Group(p, "Floor");
            NatureTerrainMesh.Build(floor, points, (a, b, c) => true, (i, j) => 0, _ => NatureMaterialBuilder.Load("Nature_Grass"),
                                    NatureDressing.GeneratedDir, "Menu_Ground");

            var area = Rect.MinMaxRect(-GroundHalf, ShowZ - GroundHalf, GroundHalf, ShowZ + GroundHalf);
            var (mistFloor, mistRect) = NatureTerrainMesh.BakeMistFloor(GroundHeight, area, 128, 3, MistFloorPath);
            var mist = new GameObject("Mist");
            mist.transform.SetParent(p, false);
            mist.AddComponent<MistField>().Configure(mistFloor, mistRect, 0.8f);

            Plant(p);
            Shafts(p);
        }

        // ------------------------------------------------------------------ what must stay clear

        /// <summary>The lines every camera spot sees along: to its board and to what it looks at.</summary>
        static IEnumerable<(Vector3 a, Vector3 b)> Sightlines()
        {
            foreach (var (_, board, _, camera, lookAt) in Stations)
            {
                yield return (camera, board);
                yield return (camera, lookAt);
                yield return (camera, ShowCentre + Vector3.up * 1f);
            }
            foreach (var spot in StageSpots) yield return (Stations.First(s => s.id == MenuStationId.Lobby).camera, spot + Vector3.up);
        }

        /// <summary>Would something in <paramref name="box"/> hide a board, the show or the stage, or stand in the clearing?</summary>
        static bool InTheWay(Bounds box, float clearing)
        {
            var flat = new Vector2(box.center.x, box.center.z - ShowZ);
            float reach = Mathf.Max(box.extents.x, box.extents.z);
            if (flat.magnitude - reach < clearing) return true;
            var grown = box;
            grown.Expand(new Vector3(3f, 2f, 3f));
            foreach (var (a, b) in Sightlines())
                for (int k = 0; k <= 24; k++)
                    if (grown.Contains(Vector3.Lerp(a, b, k / 24f))) return true;
            return false;
        }

        // ------------------------------------------------------------------ the trees and the plants

        static Mesh M(string kind, int variant, int lod) => AssetDatabase.LoadAssetAtPath<Mesh>(NatureTreeBuilder.MeshPath(kind, variant, lod));

        static Bounds WorldBox(FoliageSet.Batch batch, Vector3 p, float yaw, float scale)
        {
            var local = batch.LocalBounds(0);
            var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
            var b = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int c = 0; c < 8; c++)
                b.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
            return b;
        }

        static void Plant(Transform p)
        {
            var set = AssetDatabase.LoadAssetAtPath<FoliageSet>(FoliagePath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<FoliageSet>();
                AssetDatabase.CreateAsset(set, FoliagePath);
            }
            set.batches.Clear();
            var rng = new Random(2026);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            T Pick<T>(T[] options) => options[rng.Next(options.Length)];
            var placed = new Dictionary<FoliageSet.Batch, List<(Vector3, float, float)>>();
            int count = 0;
            void Add(FoliageSet.Batch b, Vector3 at, float yaw, float scale)
            {
                if (!placed.TryGetValue(b, out var list)) placed[b] = list = new List<(Vector3, float, float)>();
                list.Add((at, yaw, Mathf.Clamp(scale, b.minScale, b.maxScale)));
                count++;
            }

            FoliageSet.Batch[] Species(NatureTreeBuilder.Species s, float near, float min, float max) => Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
            {
                var lods = new[] { M(s.ToString(), v, 0), M(s.ToString(), v, 1), M(s.ToString(), v, 2) };
                var b = NatureDressing.Batch(set, $"{s} {v}", lods, NatureTreeBuilder.MaterialsFor(s, lods[0]), new[] { near, near * 2.5f, 400f }, new[] { true, false, false }, min, max);
                b.shadowDistance = 70f;
                return b;
            }).ToArray();
            var broadleaf = Species(NatureTreeBuilder.Species.Broadleaf, 60f, 1.4f, 3.2f);
            var firs = Species(NatureTreeBuilder.Species.Fir, 60f, 1.4f, 3f);
            var bushes = Species(NatureTreeBuilder.Species.Bush, 40f, 1f, 2.2f);
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
                    giants[v][part] = NatureDressing.Batch(set, $"{k} {v}", lods, materials, part == 0 ? new[] { 60f, 160f } : new[] { 120f, 300f, 600f },
                                                           part == 0 ? new[] { false, false } : new[] { true, false, false }, 0.6f, 1.1f);
                }
            }
            var ferns = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                NatureDressing.Batch(set, "Fern " + v, new[] { M("Fern", v, 0) }, new[] { NatureMaterialBuilder.Load("Nature_Fern") }, new[] { 70f }, new[] { false }, 1.6f, 3.2f)).ToArray();
            var grass = Enumerable.Range(0, NatureTreeBuilder.Variants).Select(v =>
                NatureDressing.Batch(set, "Grass " + v, new[] { M("Grass", v, 0) }, new[] { NatureMaterialBuilder.Load("Nature_GrassBlades") }, new[] { 45f }, new[] { false }, 0.9f, 1.5f)).ToArray();

            // ---- the giants: a ring of great trunks from 34 m out, rising into the mist
            var boles = new List<Vector2>();
            int giantCount = 0;
            for (int attempt = 0; attempt < 900 && giantCount < 70; attempt++)
            {
                float angle = R(0f, 360f), distance = R(34f, 170f);
                var at = ShowCentre + Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
                if (boles.Any(b => (b - new Vector2(at.x, at.z)).magnitude < 16f)) continue;
                at.y = GroundHeight(at.x, at.z) - 0.6f;
                int v = rng.Next(NatureTreeBuilder.Variants);
                float yaw = R(0f, 360f), scale = R(0.65f, 1f);
                // the crown is far overhead: only the roots and the bole must stay out of the way
                if (InTheWay(WorldBox(giants[v][1], at, yaw, scale), 30f) || InTheWay(WorldBox(giants[v][0], at, yaw, scale), 30f)) continue;
                for (int part = 0; part < 3; part++) Add(giants[v][part], at, yaw, scale);
                boles.Add(new Vector2(at.x, at.z));
                giantCount++;
            }

            // ---- the trees: dense from the clearing's edge outwards
            int trees = 0;
            for (float x = -GroundHalf + 3f; x < GroundHalf - 3f; x += 6f)
                for (float z = ShowZ - GroundHalf + 3f; z < ShowZ + GroundHalf - 3f; z += 6f)
                {
                    var at = new Vector3(x + R(-2.6f, 2.6f), 0f, z + R(-2.6f, 2.6f));
                    float d = new Vector2(at.x, at.z - ShowZ).magnitude;
                    if (d < ClearingRadius || d > 170f || rng.NextDouble() > Mathf.Lerp(0.35f, 0.7f, Mathf.Clamp01((d - 15f) / 40f))) continue;
                    if (boles.Any(b => (b - new Vector2(at.x, at.z)).magnitude < 5f)) continue;
                    at.y = GroundHeight(at.x, at.z) - 0.3f;
                    var batch = rng.NextDouble() < 0.7 ? Pick(broadleaf) : Pick(firs);
                    float yaw = R(0f, 360f), scale = R(batch.minScale, batch.maxScale);
                    if (InTheWay(WorldBox(batch, at, yaw, scale), ClearingRadius)) continue;
                    Add(batch, at, yaw, scale);
                    trees++;
                }

            // ---- the clearing: grass everywhere, ferns and bushes towards its edge; never on the show or the stage
            int plants = 0;
            var keepClear = PlayerSpots.Concat(StageSpots).ToArray();
            for (float x = -60f; x < 60f; x += 1.6f)
                for (float z = ShowZ - 60f; z < ShowZ + 60f; z += 1.6f)
                {
                    var at = new Vector3(x + R(-0.7f, 0.7f), 0f, z + R(-0.7f, 0.7f));
                    float d = new Vector2(at.x, at.z - ShowZ).magnitude;
                    if (d > 60f || keepClear.Any(s => (new Vector2(s.x - at.x, s.z - at.z)).magnitude < 1.6f)) continue;
                    at.y = GroundHeight(at.x, at.z);
                    double roll = rng.NextDouble();
                    FoliageSet.Batch batch;
                    if (d > 12f && roll < 0.12) batch = Pick(ferns);
                    else if (d > 13f && roll < 0.16) batch = Pick(bushes);
                    else if (roll < 0.55) batch = Pick(grass);
                    else continue;
                    float yaw = R(0f, 360f), scale = R(batch.minScale, batch.maxScale);
                    if (batch != grass[0] && batch != grass[1] && batch != grass[2] && InTheWay(WorldBox(batch, at, yaw, scale), 9f)) continue;
                    Add(batch, at, yaw, scale);
                    plants++;
                }

            foreach (var (batch, list) in placed)
                foreach (var cellGroup in list.GroupBy(t => (Mathf.FloorToInt(t.Item1.x / FoliageSet.CellSize), Mathf.FloorToInt(t.Item1.z / FoliageSet.CellSize))))
                {
                    var items = cellGroup.ToList();
                    float minY = items.Min(t => t.Item1.y), maxY = items.Max(t => t.Item1.y);
                    var cell = new FoliageSet.Cell
                    {
                        origin = new Vector3(cellGroup.Key.Item1 * FoliageSet.CellSize, minY, cellGroup.Key.Item2 * FoliageSet.CellSize),
                        height = Mathf.Max(0.01f, maxY - minY)
                    };
                    FoliageSet.Pack(batch, cell, items);
                    batch.cells.Add(cell);
                }
            EditorUtility.SetDirty(set);
            var go = new GameObject("Foliage");
            go.transform.SetParent(p, false);
            go.AddComponent<FoliageInstancer>().Configure(set);
            Debug.Log($"[MenuBackdropBuilder] clearing: {giantCount} giants, {trees} trees, {plants} plants ({count} instances)");
        }

        /// <summary>A few light shafts hanging from the crowns behind the show, towards the low sun (HotPatata/LightShaft).</summary>
        static void Shafts(Transform p)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(CourseKit.MatDir + "Nature/Nature_LightShaft.mat");
            if (material == null) return;
            var shafts = Group(p, "Light shafts");
            var spots = new[] { (-14f, 30f, 26f), (-3f, 34f, 34f), (9f, 28f, 22f), (18f, 32f, 38f), (-24f, 30f, 44f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var (x, y, z) = spots[i];
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/Art/Nature/Generated/LightShaft_{i % 3}.asset");
                if (mesh == null) continue;
                var go = new GameObject("Shaft " + (i + 1));
                go.transform.SetParent(shafts, false);
                go.transform.localPosition = new Vector3(x, y, ShowZ + z);
                go.transform.localScale = Vector3.one * 40f;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
    }
}
