using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// What grows a cliff out of the ground (ARCHITECTURE §27.5, docs/LEVEL_AUTHORING.md §28), all decoration (no collider): CC0 scanned
    /// rocks set into its faces (at least 70 % of their depth inside, never more than <see cref="MaxProtrusion"/> out, never in a cave's
    /// clear space), roots on an earth bank, scree at its foot (batched like the forest's plants, never on a walk line, a pass arc, water or
    /// a checkpoint's clearing), decals of moss, mud, leaves and wet ground round its foot; and its talus: the ground raised to its foot
    /// over <see cref="CliffShape.Fuse"/> metres through the height stack (<see cref="GroundShapes.Extra"/>), so no cut shows.
    /// </summary>
    [InitializeOnLoad]
    public static class RockDressing
    {
        /// <summary>A scan sticks out of the face this much at most (m): walking through it is not noticed, the face still stops a body.</summary>
        public const float MaxProtrusion = 0.4f;
        /// <summary>The scanned rocks set into faces, and the roots of an earth bank.</summary>
        public static readonly string[] FaceScans = { "boulder_01", "rock_07", "rock_09", "rock_face_01", "rock_face_02", "rock_moss_set_01", "rock_moss_set_02",
                                                      "namaqualand_boulder_02", "namaqualand_boulder_04", "stone_01" };
        public static readonly string[] Roots = { "pine_roots", "root_cluster_01", "root_cluster_02", "single_root" };
        /// <summary>A scattered scan is marked so tests and Verify find it: the face point it was set at and its outward normal.</summary>
        public const string ScanMarker = "Scan in face";

        static RockDressing()
        {
            GroundShapes.Extra.Add(Talus);
        }

        public static void DressCliff(CliffShape cliff, Transform root, string name)
        {
            if (cliff.RockScatter <= 0f && cliff.Scree <= 0f && cliff.Decals <= 0f && cliff.Rock != CliffRock.Earth) return;
            Physics.SyncTransforms();
            var colliders = root.GetComponentsInChildren<MeshCollider>(true).ToList();
            var rng = new Random(TimberKit.Seed(cliff.PieceId ?? name, cliff.Seed + 101));
            var outline = Poly2.CounterClockwise(cliff.WorldOutline());
            var ring = LevelRock.ResampleClosed(outline, 1f);
            if (ring.Count < 3 || colliders.Count == 0) return;
            float perimeter = 0f;
            for (int i = 0; i < ring.Count; i++) perimeter += Vector2.Distance(ring[i], ring[(i + 1) % ring.Count]);
            var caves = LevelRock.OtherCaves(cliff);

            if (cliff.RockScatter > 0f)
            {
                int count = Mathf.RoundToInt(cliff.RockScatter * perimeter * cliff.Height / 14f);
                SetIntoFace(cliff, root, colliders, ring, caves, rng, FaceScans, count, 0.8f, 2.2f, MaxProtrusion);
            }
            if (cliff.Rock == CliffRock.Earth)
            {
                int count = Mathf.RoundToInt(perimeter * cliff.Height / 10f);
                SetIntoFace(cliff, root, colliders, ring, caves, rng, Roots, count, 0.6f, 1.4f, 0.3f);
            }
            if (cliff.Scree > 0f) Scree(cliff, root, outline, rng);
            if (cliff.Decals > 0f) DecalScatter.AroundFoot(cliff.gameObject.scene, root, name, ring, cliff.Decals, rng);
        }

        // ------------------------------------------------------------------ scans set into the faces

        static void SetIntoFace(CliffShape cliff, Transform root, List<MeshCollider> colliders, List<Vector2> ring, List<LevelRock.ClearSpace> caves, Random rng,
                                string[] models, int count, float minSize, float maxSize, float protrusion)
        {
            var prefabs = models.Select(PropLibraryBuilder.PrefabPath).Where(p => p != null).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p != null).ToArray();
            if (prefabs.Length == 0) return;
            var group = new GameObject(ScanMarker + "s");
            group.transform.SetParent(root, false);
            float foot = cliff.transform.position.y;
            for (int n = 0, tries = 0; n < count && tries < count * 6; tries++)
            {
                int i = rng.Next(ring.Count);
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                var mid = Vector2.Lerp(a, b, (float)rng.NextDouble());
                var outward = Poly2.Outward(b - a).normalized;
                float up = (float)rng.NextDouble() * cliff.Height * 0.95f + 0.2f;
                var from = new Vector3(mid.x + outward.x * 6f, foot + up, mid.y + outward.y * 6f);
                var ray = new Ray(from, new Vector3(-outward.x, 0f, -outward.y));
                if (!Hit(colliders, ray, 12f, out var hit)) continue;
                var prefab = prefabs[rng.Next(prefabs.Length)];
                var local = Bounds(prefab);
                float size = Mathf.Lerp(minSize, maxSize, (float)rng.NextDouble());
                float scale = size / Mathf.Max(0.05f, local.size.magnitude);
                // the scan's depth along the face's normal (its own up): at least 70 % of it inside the rock
                float depth = local.size.y * scale;
                float stickOut = Mathf.Min(protrusion, depth * 0.3f);
                var rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                var top = hit.point + hit.normal * stickOut;
                // the pivot so that the scan's top (its local bounds' max y) lies at the stick-out point
                var pivot = top - rot * (new Vector3(local.center.x, local.max.y, local.center.z) * scale);
                var center = pivot + rot * (local.center * scale);
                // nothing of it in a cave's clear space: its pivot, centre, top and the corners of its box
                var corners = Enumerable.Range(0, 8).Select(i => pivot + rot * (Vector3.Scale(local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)), Vector3.one * scale))).ToList();
                if (caves.Any(c => c.Contains(center) || c.Contains(top) || c.Contains(pivot) || corners.Any(c.Contains))) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
                go.transform.SetPositionAndRotation(pivot, rot);
                go.transform.localScale = Vector3.one * scale;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var mark = go.AddComponent<ScanPlacement>();
                mark.Configure(hit.point, hit.normal);
                n++;
            }
        }

        static bool Hit(List<MeshCollider> colliders, Ray ray, float reach, out RaycastHit best)
        {
            best = default;
            float d = float.MaxValue;
            foreach (var c in colliders)
                if (c.Raycast(ray, out var h, reach) && h.distance < d)
                {
                    d = h.distance;
                    best = h;
                }
            return d < float.MaxValue;
        }

        /// <summary>A prefab's drawn bounds in its own space (scale 1).</summary>
        public static Bounds Bounds(GameObject prefab)
        {
            var renderers = prefab.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToList();
            if (renderers.Count == 0) return new Bounds(Vector3.up * 0.5f, Vector3.one);
            Bounds? b = null;
            foreach (var f in renderers)
            {
                var m = prefab.transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
                var mb = f.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    if (b == null) b = new Bounds(corner, Vector3.zero);
                    else
                    {
                        var bb = b.Value;
                        bb.Encapsulate(corner);
                        b = bb;
                    }
                }
            }
            return b.Value;
        }

        // ------------------------------------------------------------------ scree

        static void Scree(CliffShape cliff, Transform root, List<Vector2> outline, Random rng)
        {
            var scene = cliff.gameObject.scene;
            var set = ScriptableObject.CreateInstance<FoliageSet>();
            set.name = cliff.name + " scree";
            // the scanned stones and rocks, sized to 0.5 m at a scale of 1 (pebbles to blocks of a metre and a half)
            float[] distances = { 45f, 110f };
            int[] meshLods = { 2, 4 };
            bool[] noShadow = { false, false };
            var batches = NatureDressing.ModelBatches(set, "stone_01", "Nature_Prop_Stone01", 0.5f, distances, meshLods, noShadow, 0.2f, 3f)
                .Concat(NatureDressing.ModelBatches(set, "rock_09", "Nature_Prop_Rock09", 0.5f, distances, meshLods, noShadow, 0.2f, 3f))
                .Concat(NatureDressing.ModelBatches(set, "rock_07", "Nature_Prop_Rock07", 0.5f, distances, meshLods, noShadow, 0.2f, 3f)).ToArray();
            if (batches.Length == 0) return;
            var keep = PalettePlacer.Keepout.Of(scene);
            var clearings = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Checkpoint>(true)).Select(c => c.transform.position).ToList();
            NatureDressing.IndexArcs(keep.Arcs);
            var bins = new LevelForest.Bins();
            var ring = LevelRock.ResampleClosed(outline, 0.6f);
            float width = cliff.ScreeWidth;
            for (int i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                var outward = Poly2.Outward(b - a).normalized;
                int here = Mathf.RoundToInt(cliff.Scree * 5f);
                for (int s = 0; s < here; s++)
                {
                    // denser near the foot, never in a row: each stone its own distance out
                    float t = (float)rng.NextDouble();
                    float d = 0.2f + Mathf.Pow(t, 1.6f) * width;
                    var q = Vector2.Lerp(a, b, (float)rng.NextDouble()) + outward * d;
                    float ground = LevelNature.GroundAt(scene, q);
                    if (float.IsNaN(ground)) continue;
                    var p = new Vector3(q.x, ground - 0.1f, q.y);
                    if (!keep.Allows(p) || clearings.Any(c => new Vector2(c.x - p.x, c.z - p.z).magnitude < 4f)) continue;
                    var batch = batches[rng.Next(batches.Length)];
                    // mostly gravel and stones, a few blocks near the face
                    float size = (float)rng.NextDouble();
                    float scale = Mathf.Lerp(0.25f, 1f, size) * (size > 0.92f ? Mathf.Lerp(1.5f, 3f, 1f - t) : 1f);
                    float yaw = (float)rng.NextDouble() * 360f;
                    if (NatureDressing.TouchesAnArc(batch, p, yaw, scale)) continue;
                    bins.Add(batch, p, yaw, scale);
                }
            }
            bins.Pack();
            set.batches.RemoveAll(x => x.cells.Count == 0);
            if (set.batches.Count == 0) return;
            var go = new GameObject("Scree");
            go.transform.SetParent(root, false);
            go.AddComponent<FoliageInstancer>().Configure(set);
        }

        static Mesh Mesh(string kind, int variant, int lod) => AssetDatabase.LoadAssetAtPath<Mesh>(NatureTreeBuilder.MeshPath(kind, variant, lod));

        // ------------------------------------------------------------------ talus

        /// <summary>Each cliff with a fuse raises the ground to its foot over that many metres (only ever up, at most its sink).</summary>
        static IEnumerable<GroundShapes.Contribution> Talus(Scene scene)
        {
            foreach (var cliff in TimberLinks.Pieces(scene).OfType<CliffShape>())
            {
                if (cliff.Fuse <= 0f || !cliff.isActiveAndEnabled) continue;
                var outline = cliff.WorldOutline();
                if (outline.Count < 3) continue;
                float reach = cliff.Fuse + 1f;
                var box = Rect.MinMaxRect(outline.Min(p => p.x) - reach, outline.Min(p => p.y) - reach, outline.Max(p => p.x) + reach, outline.Max(p => p.y) + reach);
                float target = cliff.transform.position.y + 0.3f, most = Mathf.Max(0.5f, cliff.Sink);
                var c = new GroundShapes.Contribution { Id = cliff.PieceId + "#talus", Signature = LevelPieceBuilder.Signature(cliff), Bounds = box };
                c.Apply = (running, cells) =>
                {
                    foreach (var (x, z) in GroundShapes.Cells(running, cells, box))
                    {
                        var p = running.World(x, z);
                        float d = Poly2.Contains(outline, p) ? 0f : Poly2.DistanceToEdge(outline, p);
                        if (d > cliff.Fuse) continue;
                        float w = Mathf.SmoothStep(0f, 1f, 1f - d / cliff.Fuse);
                        running[x, z] += Mathf.Clamp(target - running[x, z], 0f, most) * w;
                    }
                };
                yield return c;
            }
        }
    }
}
