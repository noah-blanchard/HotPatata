using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// PatataCanopy's scanned props (ARCHITECTURE §25.3, Poly Haven and CC0 scans): the junction decks lived on (crates, barrels,
    /// baskets, buckets, jugs, a stool), braced underneath with the pier's poles; lanterns hanging under the leaf roofs;
    /// nests on the roofs and frogs on the crates; old walkways clinging to the giants high over the roofs; roots at the foot of the
    /// deck trunks on the forest floor. A crate or a barrel looks like it can be stood on, so it is solid (an Environment box); all
    /// the rest is decoration (<see cref="CourseDecoration"/>, no collider). Nothing stands in a way across a junction, near a
    /// spawn, a fire, a plate, a zone or a pass arc; nothing that looks walkable is within a jump of a floor. Deterministic.
    /// </summary>
    public static partial class PatataCanopyBuilder
    {
        const float LaneHalf = 3.5f;   // the way across a junction deck (in and out) stays this clear of props

        static readonly (string model, float height)[] SolidClutter =
        {
            ("wooden_crate_01", 0.82f), ("wooden_crate_02", 0.86f), ("wine_barrel_01", 1.15f), ("barrel01", 1.2f), ("barrel02", 1.15f),
        };

        static readonly (string model, float height)[] SmallClutter =
        {
            ("wooden_bucket_01", 0.5f), ("wicker_basket_01", 0.42f), ("wicker_basket_02", 0.48f), ("jug_01", 0.45f), ("wooden_bowl_01", 0.16f),
            ("wooden_stool_01", 0.62f),
        };

        static int EnvironmentLayer => LayerMask.NameToLayer("Environment");

        /// <summary>Every prop of the course, once the sections and the forest stand; returns a summary for the build log.</summary>
        static string Props(Transform group, List<Transform> rooms)
        {
            var root = new GameObject("Canopy props").transform;
            root.SetParent(group, false);
            var deco = new GameObject("Treehouse dressing").transform;
            deco.SetParent(root, false);
            deco.gameObject.AddComponent<CourseDecoration>();
            var solids = new GameObject("Crates and barrels").transform;
            solids.SetParent(root, false);
            var samples = NatureDressing.PassSamples();
            var rng = new Random(1311);

            int crates = 0, clutter = 0, braces = 0;
            for (int i = 0; i < rooms.Count; i++) JunctionLife(rooms, i, deco, solids, samples, rng, ref crates, ref clutter, ref braces);
            int lanterns = rooms.Sum(room => Lanterns(room, deco, samples, rng));
            int nests = Nests(deco, samples, rng);
            int walkways = Walkways(deco, samples, rng);
            int roots = Roots(deco, rng);
            Physics.SyncTransforms();
            return $"{crates} crates and barrels, {clutter} small props, {braces} braces under the junctions, {lanterns} lanterns, {nests} nests and frogs, " +
                   $"{walkways} old walkways, {roots} roots";
        }

        /// <summary>A course floor (a solid that is neither a roof, a kill zone nor a trigger).</summary>
        static bool IsFloor(Collider c) => !c.isTrigger && c.GetComponentInParent<KillZone>() == null && c.GetComponent<CourseCeiling>() == null;

        // ------------------------------------------------------------------ the junction decks

        /// <summary>
        /// A junction deck lived on: two or three clusters (a crate or a barrel, solid, and small things round it) in its corners, off
        /// the ways in and out, and the pier's poles bracing its corners underneath.
        /// </summary>
        static void JunctionLife(List<Transform> rooms, int i, Transform deco, Transform solids, List<Vector3> samples, Random rng,
                                 ref int crates, ref int clutter, ref int braces)
        {
            var room = rooms[i];
            foreach (var deck in room.GetComponentsInChildren<BoxCollider>().Where(c => c.name == "Junction deck" || c.name == "Start deck").ToArray())
            {
                var t = deck.transform;
                var size = Vector3.Scale(deck.size, t.lossyScale);
                var top = t.TransformPoint(deck.center) + Vector3.up * size.y / 2f;
                Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }
                var lanes = new List<Vector3> { Flat(room.forward) };
                if (deck.name == "Junction deck" && i + 1 < rooms.Count) lanes.Add(Flat(rooms[i + 1].forward));
                bool InLane(Vector3 p) => lanes.Any(l => Vector3.Cross(l, Flat(p - top) * (p - top).magnitude).magnitude < LaneHalf);

                // the clusters: candidate corners, kept off the lanes, the spawns, the fire and everything that plays
                var spots = new List<Vector3>();
                float half = Mathf.Min(size.x, size.z) / 2f - 2.2f;
                for (int c = 0; c < 4; c++)
                    foreach (float inset in new[] { 0f, 2.2f })
                    {
                        var local = new Vector3(((c & 1) == 0 ? -1 : 1) * (half - inset), 0f, ((c & 2) == 0 ? -1 : 1) * (half - inset * 0.4f));
                        spots.Add(top + t.rotation * local);
                    }
                int placed = 0;
                foreach (var spot in spots.OrderBy(_ => rng.Next()))
                {
                    if (placed >= 4) break;
                    if (InLane(spot) || !ClusterFits(room, spot, samples)) continue;
                    var (model, height) = SolidClutter[rng.Next(SolidClutter.Length)];
                    float yaw = (float)rng.NextDouble() * 360f;
                    var solid = model.StartsWith("barrel0")
                        ? ScannedProps.KitPiece(solids, "wooden_barrels_01", new[] { "wooden_barrels_01_" + model }, (_, __) => model == "barrel01" ? "Nature_Prop_Barrel1" : "Nature_Prop_Barrel2", spot, yaw, height)
                        : ScannedProps.Stand(solids, model, spot, Quaternion.Euler(0f, yaw, 0f), height);
                    Solidify(solid);
                    crates++;
                    // a crate on a crate, sometimes (1.7 m at most: nothing on a junction deck is within reach of its top)
                    if (model.StartsWith("wooden_crate") && rng.NextDouble() < 0.4)
                    {
                        var on = ScannedProps.Stand(solids, "wooden_crate_02", spot + Vector3.up * height, Quaternion.Euler(0f, yaw + 90f + (float)rng.NextDouble() * 20f, 0f), 0.78f);
                        Solidify(on);
                        crates++;
                    }
                    if (rng.NextDouble() < 0.3)   // a frog on the crate
                        ScannedProps.Stand(deco, "tree_frog", spot + Vector3.up * height, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), 0.12f, shadows: false).isStatic = true;
                    int smalls = 3 + rng.Next(3);
                    for (int k = 0, tries = 0; k < smalls && tries < 14; tries++)
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 1.0f + (float)rng.NextDouble() * 0.9f;
                        var at = spot + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                        if (InLane(at) || ScannedProps.NearGameplay(at, 1.2f) || !OnFloor(at, deck)) continue;
                        var (small, h) = SmallClutter[rng.Next(SmallClutter.Length)];
                        NatureKit.Prop(deco, small, ScannedProps.MaterialOf(small), deco.InverseTransformPoint(at), (float)rng.NextDouble() * 360f, h, shadows: false).isStatic = true;
                        k++;
                        clutter++;
                    }
                    placed++;
                }

                // the pier's poles bracing the deck's corners underneath (decoration under the floor, never on it)
                float bottom = top.y - size.y;
                for (int c = 0; c < 4; c++)
                {
                    var local = new Vector3(((c & 1) == 0 ? -1 : 1) * (size.x / 2f - 1.6f), 0f, ((c & 2) == 0 ? -1 : 1) * (size.z / 2f - 1.6f));
                    var at = t.TransformPoint(deck.center) + t.rotation * local;
                    float h = 3.8f * (1f + (float)rng.NextDouble() * 0.3f);
                    at.y = bottom - h;
                    ScannedProps.KitPiece(deco, "modular_wooden_pier", new[] { "modular_wooden_pier_poles" }, (_, __) => "Nature_Prop_PierPoles", at,
                                          t.eulerAngles.y + c * 90f, h).isStatic = true;
                    braces++;
                }
            }
        }

        /// <summary>A cluster fits here: on the deck, nothing standing or playing within reach, no spawn, fire or pass arc near.</summary>
        static bool ClusterFits(Transform room, Vector3 at, List<Vector3> samples)
        {
            foreach (var c in Physics.OverlapBox(at + Vector3.up * 1.2f, new Vector3(1.8f, 1.1f, 1.8f), Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
                if (c.GetComponentInParent<KillZone>() == null && c.GetComponentInParent<Checkpoint>() == null) return false;
            if (ScannedProps.NearGameplay(at, 2f)) return false;
            if (Object.FindObjectsByType<PlayerSpawn>(FindObjectsSortMode.None).Any(s => Vector3.Distance(s.transform.position, at) < 3f)) return false;
            if (Object.FindObjectsByType<CampfirePresentation>(FindObjectsSortMode.None).Any(f => Vector3.Distance(f.transform.position, at) < 3f)) return false;
            return !samples.Any(s => s.y < at.y + 3.5f && s.y > at.y - 1f && new Vector2(s.x - at.x, s.z - at.z).magnitude < 2.6f);
        }

        static bool OnFloor(Vector3 at, Collider deck) =>
            Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out var hit, 0.8f, 1 << EnvironmentLayer, QueryTriggerInteraction.Ignore) && hit.collider == deck;

        /// <summary>A crate or a barrel looks like it can be stood on: its box (Environment) makes it so.</summary>
        static void Solidify(GameObject prop)
        {
            prop.isStatic = true;
            var b = ScannedProps.WorldBounds(prop);
            var box = new GameObject("Prop box") { layer = EnvironmentLayer };
            box.transform.SetParent(prop.transform, false);
            box.transform.position = b.center;
            box.AddComponent<BoxCollider>().size = b.size;
            box.isStatic = true;
        }

        // ------------------------------------------------------------------ lanterns, nests, frogs

        /// <summary>Lanterns hanging on a rope from the leaf roof over the edges of a covered section's decks, clear of every pass.</summary>
        static int Lanterns(Transform room, Transform deco, List<Vector3> samples, Random rng)
        {
            int placed = 0;
            foreach (var roof in room.GetComponentsInChildren<CourseCeiling>().Select(c => c.GetComponent<BoxCollider>()).Where(c => c != null).ToArray())
            {
                var t = roof.transform;
                var size = Vector3.Scale(roof.size, t.lossyScale);
                var center = t.TransformPoint(roof.center);
                float underside = center.y - size.y / 2f;
                for (float z = -size.z / 2f + 5f; z < size.z / 2f - 5f; z += 8f + (float)rng.NextDouble() * 4f)
                    foreach (float x in new[] { -7f, -4f, 4f, 7f }.OrderBy(_ => rng.Next()).Take(2))
                    {
                        if (rng.NextDouble() < 0.15) continue;
                        var at = center + t.right * (x + (float)(rng.NextDouble() - 0.5)) + t.forward * z;
                        at.y = underside;
                        // over a floor of this section, 4 m or more under the roof
                        if (!Physics.Raycast(at + Vector3.down * 0.2f, Vector3.down, out var hit, 9f, 1 << EnvironmentLayer, QueryTriggerInteraction.Ignore) || !IsFloor(hit.collider))
                            continue;
                        if (hit.distance < 4f) continue;
                        float drop = 1.6f + (float)rng.NextDouble() * 1.0f;
                        var lantern = new Vector3(at.x, underside - drop, at.z);
                        if (samples.Any(s => s.y > lantern.y - 1.9f && new Vector2(s.x - at.x, s.z - at.z).magnitude < 1.5f)) continue;
                        if (Physics.OverlapBox(new Vector3(at.x, underside - drop / 2f - 0.6f, at.z), new Vector3(0.7f, drop / 2f + 0.6f, 0.7f), Quaternion.identity, ~0, QueryTriggerInteraction.Collide)
                            .Any(c => c != roof && c.GetComponentInParent<KillZone>() == null && c.GetComponentInParent<Checkpoint>() == null)) continue;
                        NatureKit.Detail(deco, "Lantern rope", deco.InverseTransformPoint(new Vector3(at.x, underside - drop / 2f + 0.2f, at.z)), new Vector3(0.03f, drop - 0.4f, 0.03f), KitRole.Frame);
                        ScannedProps.KitPiece(deco, "Lantern_01", new[] { "Lantern_01", "Lantern_01_glass" },
                            (mesh, _) => mesh.EndsWith("glass") ? NatureMaterialBuilder.LanternName : "Nature_Prop_HangingLantern",
                            lantern + Vector3.down * 0.95f, (float)rng.NextDouble() * 360f, 0.95f, shadows: false).isStatic = true;
                        placed++;
                    }
            }
            return placed;
        }

        /// <summary>Nests on the leaf roofs (seen from the decks above them), out of reach.</summary>
        static int Nests(Transform deco, List<Vector3> samples, Random rng)
        {
            int placed = 0;
            foreach (var roof in Roofs.Where(r => r != null).Select(r => r.GetComponent<BoxCollider>()).Where(c => c != null))
            {
                var b = roof.bounds;
                for (int k = 0; k < 2; k++)
                {
                    var at = new Vector3(Mathf.Lerp(b.min.x + 3f, b.max.x - 3f, (float)rng.NextDouble()), b.max.y, Mathf.Lerp(b.min.z + 3f, b.max.z - 3f, (float)rng.NextDouble()));
                    if (samples.Any(s => s.y > at.y - 1f && new Vector2(s.x - at.x, s.z - at.z).magnitude < 2f)) continue;
                    if (Physics.OverlapBox(at + Vector3.up * 1f, new Vector3(1f, 0.9f, 1f), Quaternion.identity, ~0, QueryTriggerInteraction.Collide).Any(c => c != roof && c.GetComponentInParent<KillZone>() == null)) continue;
                    ScannedProps.Stand(deco, "duck_nest_eggs", at, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), 0.32f, 0.08f).isStatic = true;
                    placed++;
                }
            }
            return placed;
        }

        // ------------------------------------------------------------------ old walkways on the giants

        /// <summary>
        /// Old walkways (the pier's four sections in a row, poles hanging) clinging to the giants beside the course, high over the roofs
        /// and reaching away from the course: 3.5 m or more over every floor within 12 m and 10 m or more under any above it, so none is
        /// ever within a jump; none comes near a pass arc or another giant's bole.
        /// </summary>
        static int Walkways(Transform deco, List<Vector3> samples, Random rng)
        {
            var sections = new[] { "modular_wooden_pier_section_01", "modular_wooden_pier_section_02", "modular_wooden_pier_section_03", "modular_wooden_pier_section_04" };
            var used = new HashSet<int>();
            int placed = 0;
            int course = LayerMask.GetMask("Environment", "Hazard", BodyScreen.LayerName);
            for (int k = 0; k < Footprints.Count; k++)
            {
                var f = Footprints[k];
                var rect = FootRect(f);
                if (rng.NextDouble() < 0.2) continue;
                var near = Enumerable.Range(0, Boles.Count).Where(b => !used.Contains(b) && DistanceToRect(rect, Boles[b].x, Boles[b].z) < 14f)
                    .OrderBy(_ => rng.Next()).Take(6).ToArray();
                foreach (int b in near)
                {
                    var bole = Boles[b];
                    var at = new Vector3(bole.x, 0f, bole.z);
                    var mid = f.origin + Quaternion.Euler(0, f.yaw, 0) * new Vector3(0, 0, f.length / 2f);
                    var away = at - mid;
                    away.y = 0f;
                    away.Normalize();
                    float scale = 1f + (float)rng.NextDouble() * 0.3f;
                    float y = f.topY + 2.5f + (float)rng.NextDouble() * 4f;
                    var start = at + away * (bole.y + 0.3f);
                    var foot = start + away * 6.5f * scale;
                    foot.y = y;
                    float yaw = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
                    var go = ScannedProps.KitPiece(deco, "modular_wooden_pier", sections, (_, slot) => slot == 0 ? "Nature_Prop_PierPoles" : "Nature_Prop_PierPlanks",
                                                   foot, yaw, 4.3f * scale);
                    var box = ScannedProps.WorldBounds(go);
                    bool ok = !samples.Any(s => { var e = box; e.Expand(4f); return e.Contains(s); });
                    if (ok)
                        foreach (var c in Physics.OverlapBox(box.center, box.extents + new Vector3(12f, 0f, 12f) + Vector3.up * 40f, Quaternion.identity, course, QueryTriggerInteraction.Ignore))
                        {
                            if (!IsFloor(c)) continue;
                            bool below = c.bounds.max.y + 3.5f <= box.min.y, above = c.bounds.min.y - 10f >= box.max.y;
                            if (!below && !above) { ok = false; break; }
                        }
                    if (ok && Boles.Where((o, j) => j != b).Any(o => (new Vector2(o.x, o.z) - new Vector2(box.center.x, box.center.z)).magnitude < o.y + box.extents.magnitude * 0.5f + 1f))
                        ok = false;
                    if (!ok) { Object.DestroyImmediate(go); continue; }
                    go.isStatic = true;
                    used.Add(b);
                    placed++;
                    break;
                }
            }
            return placed;
        }

        /// <summary>Root clusters at the foot of the deck trunks on the forest floor (decoration, far under every floor).</summary>
        static int Roots(Transform deco, Random rng)
        {
            int placed = 0;
            foreach (var s in Supports)
            {
                if (rng.NextDouble() < 0.5) continue;
                var top = s.parent.TransformPoint(s.localTop);
                var foot = new Vector3(top.x, ground.Sample(top.x, top.z), top.z);
                string model = rng.NextDouble() < 0.7 ? "root_cluster_02" : "single_root";
                float natural = RootHeight(model) * (1.3f + (float)rng.NextDouble() * 0.6f) * Mathf.Max(1f, s.thickness / 1.5f);
                ScannedProps.Stand(deco, model, foot, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), natural, natural * 0.2f).isStatic = true;
                placed++;
            }
            return placed;
        }

        static readonly Dictionary<string, float> RootHeights = new Dictionary<string, float>();

        static float RootHeight(string model)
        {
            if (!RootHeights.TryGetValue(model, out var h)) RootHeights[model] = h = ScannedProps.Extent(model, Quaternion.identity).y;
            return h;
        }
    }
}
