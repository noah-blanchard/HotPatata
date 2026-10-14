using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// PatataTemple's scanned props (ARCHITECTURE §25.4, CC0 museum scans and Poly Haven, §25.3): carved reliefs inlaid in the
    /// masonry, the movers drawn as carved slabs, statues on pedestals round the courts, offerings round the fires and at the foot
    /// of the stelae, and colossi rising out of the jungle. A relief or a carved slab is drawn inside its block's collider (the wall's
    /// own drawing steps back 5 cm, a slab's top is flush with its collider): nothing is drawn where nothing collides. A statue
    /// looks like it can be stood on, so it gets a box and a pedestal of masonry (both colliders); everything else is decoration
    /// (<see cref="CourseDecoration"/>, no collider). Nothing comes near a pass arc, a plate, a spawn or a fire's clearing, and no
    /// prop stands in a way in or out of a court. Deterministic.
    /// </summary>
    public static partial class PatataTempleBuilder
    {
        const float ReliefDepth = 0.05f;   // how far a wall's drawing steps back for its reliefs (inside its collider)
        const float StatueRim = 9.5f;      // a court statue's distance from the court's centre (the octagon reaches 12 m)

        /// <summary>A relief inlaid in a wall face: its model, height, height of its foot above the floor.</summary>
        struct ReliefKind
        {
            public string model;
            public float height, foot;
            public bool standing;   // stands on the floor (an offering may sit at its foot)
        }

        static readonly ReliefKind[] ReliefKinds =
        {
            new ReliefKind { model = "maya_relief_pillar", height = 4.6f, foot = 0f, standing = true },
            new ReliefKind { model = "palenque_relief_wall", height = 2.6f, foot = 0.7f },
            new ReliefKind { model = "ballgame_palma", height = 3.6f, foot = 0f, standing = true },
            new ReliefKind { model = "maya_stela_fragment", height = 2.4f, foot = 0.8f },
            new ReliefKind { model = "maya_head_helmet", height = 2.6f, foot = 1.0f },
            new ReliefKind { model = "palenque_relief_wall", height = 1.6f, foot = 0.4f },   // a low frieze for the shrines
        };

        static readonly (string model, float height)[] Offerings =
        {
            ("precolumbian_bowl", 0.28f), ("face_urn", 0.62f), ("antique_ceramic_vase_01", 0.5f), ("ceramic_vase_01", 0.45f),
            ("ceramic_vase_03", 0.55f), ("planter_pot_clay", 0.36f), ("brass_pot_01", 0.3f), ("brass_diya_lantern", 0.32f),
            ("carved_wooden_plate", 0.04f),
        };

        static readonly (string model, float height, float sink, bool pedestal)[] CourtStatueKinds =
        {
            ("aztec_chalchiuhtlicue", 3.4f, 0f, true), ("olmec_standing_figure", 3.4f, 0f, true), ("ballgame_palma", 3.0f, 0f, true),
            ("olmec_face_fragment", 2.6f, 0.45f, true), ("maya_stela_fragment", 2.4f, 0f, false),
        };

        static int Environment => LayerMask.NameToLayer("Environment");

        static readonly Dictionary<string, Vector3> Extents = new Dictionary<string, Vector3>();

        /// <summary>A model's extent as imported (its proportions), measured once per build.</summary>
        static Vector3 Extent(string model)
        {
            if (!Extents.TryGetValue(model, out var e)) Extents[model] = e = ScannedProps.Extent(model, Quaternion.identity);
            return e;
        }

        /// <summary>Every prop of the course's sections (world space, once they are placed); returns a summary for the build log.</summary>
        static string Props(Transform group, List<Transform> rooms)
        {
            var root = new GameObject("Temple props").transform;
            root.SetParent(group, false);
            var deco = new GameObject("Reliefs and offerings").transform;
            deco.SetParent(root, false);
            deco.gameObject.AddComponent<CourseDecoration>();
            var statues = new GameObject("Court statues").transform;
            statues.SetParent(root, false);
            var samples = NatureDressing.PassSamples();
            var rng = new Random(1519);

            int reliefs = 0, footOfferings = 0;
            foreach (var room in rooms) reliefs += Reliefs(room, deco, samples, rng, ref footOfferings);
            int slabs = rooms.Sum(CarvedMovers);
            int courtStatues = 0;
            for (int i = 0; i < rooms.Count; i++) courtStatues += CourtStatues(rooms, i, statues, samples, rng);
            int fireOfferings = rooms.Sum(room => FireOfferings(room, deco, samples, rng));
            int combs = 0, growth = 0;
            foreach (var room in rooms) RoofCombs(room, deco, samples, rng, ref combs, ref growth);
            Physics.SyncTransforms();
            return $"{reliefs} reliefs, {slabs} carved slabs on movers, {courtStatues} court statues, {fireOfferings + footOfferings} offerings, " +
                   $"{combs} stelae on the roof combs, {growth} roots and trees on the roofs";
        }

        // ------------------------------------------------------------------ the roof combs

        static readonly (string model, float height)[] CombKinds =
        {
            ("maya_relief_pillar", 5f), ("ballgame_palma", 4.4f), ("aztec_chalchiuhtlicue", 4.8f), ("olmec_standing_figure", 4.8f), ("maya_stela_fragment", 3.2f),
        };

        /// <summary>
        /// The roof comb of every large stone roof (a Maya temple's crest): stelae and statues standing along its ridge, facing either side
        /// in turn, and the jungle taking the stone back (root clusters, a pachira). Decoration far out of reach: over a roof no throw goes
        /// over, and more than ten metres under any floor above it.
        /// </summary>
        static void RoofCombs(Transform room, Transform deco, List<Vector3> samples, Random rng, ref int combs, ref int growth)
        {
            int course = LayerMask.GetMask("Environment", "Hazard", BodyScreen.LayerName);
            foreach (var roof in room.GetComponentsInChildren<CourseCeiling>().Select(c => c.GetComponent<BoxCollider>()).Where(c => c != null).ToArray())
            {
                var t = roof.transform;
                var size = Vector3.Scale(roof.size, t.lossyScale);
                if (size.x * size.z < 200f) continue;   // a shrine's roof
                bool alongZ = size.z >= size.x;
                var axis = alongZ ? t.forward : t.right;
                var side = alongZ ? t.right : t.forward;
                float span = alongZ ? size.z : size.x, width = alongZ ? size.x : size.z;
                var top = t.TransformPoint(roof.center) + Vector3.up * size.y / 2f;
                bool Clear(Vector3 foot, float height, float radius)
                {
                    if (samples.Any(s => s.y > foot.y - 1f && new Vector2(s.x - foot.x, s.z - foot.z).magnitude < radius + 1.5f)) return false;
                    // nothing over it within reach: a floor above stays more than ten metres higher than its top
                    foreach (var c in Physics.OverlapBox(foot + Vector3.up * (height + 6f), new Vector3(radius + 1f, height / 2f + 6f, radius + 1f), Quaternion.identity, course, QueryTriggerInteraction.Ignore))
                        if (c != roof && c.GetComponentInParent<KillZone>() == null) return false;
                    return true;
                }
                // the crest: every 9 to 14 m along the ridge
                bool flip = rng.NextDouble() < 0.5;
                for (float d = -span / 2f + 4f + (float)rng.NextDouble() * 3f; d < span / 2f - 4f; d += 9f + (float)rng.NextDouble() * 5f)
                {
                    var (model, height) = CombKinds[rng.Next(CombKinds.Length)];
                    var foot = top + axis * d;
                    if (!Clear(foot, height, 1.5f)) continue;
                    var facing = flip ? side : -side;
                    flip = !flip;
                    ScannedProps.Stand(deco, model, foot, Quaternion.LookRotation(-facing), height, 0.1f).isStatic = true;
                    combs++;
                }
                // the jungle on the stone: roots draped over it, a pachira in a crack
                int clumps = 1 + rng.Next(3);
                for (int k = 0, tries = 0; k < clumps && tries < 12; tries++)
                {
                    var foot = top + axis * ((float)(rng.NextDouble() - 0.5) * (span - 6f)) + side * ((float)(rng.NextDouble() - 0.5) * (width - 6f));
                    float yaw = (float)rng.NextDouble() * 360f;
                    if (rng.NextDouble() < 0.35)
                    {
                        if (!Clear(foot, 3.2f, 2f)) continue;
                        string v = "abcd"[rng.Next(4)].ToString();
                        ScannedProps.KitPiece(deco, "pachira_aquatica_01", new[] { "pachira_aquatica_01_bark_" + v, "pachira_aquatica_01_leaves_" + v },
                            (mesh, _) => mesh.Contains("leaves") ? "Nature_PachiraLeaves" : "Nature_Prop_PachiraBark", foot, yaw, 2.4f + (float)rng.NextDouble() * 1.2f).isStatic = true;
                    }
                    else
                    {
                        string model = rng.NextDouble() < 0.6 ? "root_cluster_02" : "single_root";
                        float natural = Extent(model).y * (0.9f + (float)rng.NextDouble() * 0.5f);
                        if (!Clear(foot, natural, 3f)) continue;
                        ScannedProps.Stand(deco, model, foot, Quaternion.Euler(0f, yaw, 0f), natural, natural * 0.15f).isStatic = true;
                    }
                    k++;
                    growth++;
                }
            }
        }

        // ------------------------------------------------------------------ reliefs in the masonry

        /// <summary>A temple masonry block (a wall piece or a pillar, never a mover, a roof or a deck): its collider.</summary>
        static bool IsMasonry(BoxCollider c)
        {
            if (c.isTrigger || c.gameObject.layer != Environment || c.GetComponent<CourseCeiling>() != null) return false;
            if (c.GetComponentInParent<SignalActuator>() != null || c.GetComponentInParent<MovingPlatform>() != null) return false;
            var visual = c.transform.Find("Visual");
            var r = visual != null ? visual.GetComponent<Renderer>() : null;
            return r != null && r.sharedMaterial != null && r.sharedMaterial.name == TempleMaterials.Wall;
        }

        static int Reliefs(Transform room, Transform deco, List<Vector3> samples, Random rng, ref int offerings)
        {
            int placed = 0;
            foreach (var c in room.GetComponentsInChildren<BoxCollider>().Where(IsMasonry).ToArray())
            {
                var t = c.transform;
                var size = Vector3.Scale(c.size, t.lossyScale);
                var center = t.TransformPoint(c.center);
                float bottom = center.y - size.y / 2f;
                if (Mathf.Abs(room.InverseTransformPoint(new Vector3(center.x, bottom, center.z)).y) > 0.15f) continue;   // a lintel, not a wall from the floor
                bool wallAlongZ = size.x <= 1.2f && size.z >= 2f, wallAlongX = size.z <= 1.2f && size.x >= 2f;
                bool pillar = size.x >= 2.5f && size.x <= 4f && size.z >= 2.5f && size.z <= 4f && size.y >= 5f;
                if (size.y < 2.6f || !(wallAlongZ || wallAlongX || pillar)) continue;
                var faces = new List<(Vector3 normal, Vector3 along, float length, float half)>();
                if (wallAlongZ || pillar) { faces.Add((t.right, t.forward, size.z, size.x / 2f)); faces.Add((-t.right, -t.forward, size.z, size.x / 2f)); }
                if (wallAlongX || pillar) { faces.Add((t.forward, -t.right, size.x, size.z / 2f)); faces.Add((-t.forward, t.right, size.x, size.z / 2f)); }
                bool recessX = false, recessZ = false;
                foreach (var (normal, along, length, half) in faces)
                {
                    var foot = new Vector3(center.x, bottom, center.z) + normal * half;
                    if (!WalkableBefore(foot, normal, c)) continue;
                    int onFace = InlayFace(deco, c, foot, normal, along, length, Mathf.Min(size.y, Roof), pillar, samples, rng, ref offerings);
                    if (onFace == 0) continue;
                    placed += onFace;
                    if (Mathf.Abs(Vector3.Dot(normal, t.right)) > 0.5f) recessX = true; else recessZ = true;
                }
                // the wall's own drawing steps back behind its reliefs, so they sit flush with its collider
                var visual = t.Find("Visual");
                var s = visual.localScale;
                if (recessX) s.x = 1f - 2f * ReliefDepth / size.x;
                if (recessZ) s.z = 1f - 2f * ReliefDepth / size.z;
                visual.localScale = s;
            }
            return placed;
        }

        /// <summary>Is there a floor in front of this face, where a player walks (not the void, not another block)?</summary>
        static bool WalkableBefore(Vector3 foot, Vector3 normal, Collider self)
        {
            var from = foot + normal * 0.9f + Vector3.up * 1f;
            foreach (var h in Physics.RaycastAll(from, Vector3.down, 1.6f, 1 << Environment, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (h.collider == self) continue;
                return Mathf.Abs(h.point.y - foot.y) < 0.3f && h.collider.GetComponent<CourseCeiling>() == null;
            }
            return false;
        }

        static int InlayFace(Transform deco, BoxCollider self, Vector3 foot, Vector3 normal, Vector3 along, float length, float height, bool pillar,
                             List<Vector3> samples, Random rng, ref int offerings)
        {
            float margin = pillar ? 0.25f : 0.7f, end = length / 2f - margin, cursor = -length / 2f + margin + (pillar ? 0f : (float)rng.NextDouble() * 1.5f);
            var frame = Quaternion.LookRotation(-normal, Vector3.up);   // +Z into the wall: a model faces -Z, so it looks out of it
            int placed = 0, tries = 0;
            while (cursor < end && tries++ < 40)
            {
                var kinds = ReliefKinds.Where(k => k.foot + k.height <= height - 0.3f).ToArray();
                if (pillar) kinds = kinds.Where(k => k.standing).ToArray();
                if (kinds.Length == 0) break;
                var kind = kinds[rng.Next(kinds.Length)];
                var ext = Extent(kind.model);
                float w = kind.height * ext.x / ext.y;
                if (pillar) { w = Mathf.Min(w, length - 2f * margin); cursor = -w / 2f; }   // one relief, centred on each face
                if (cursor + w > end) { cursor += 0.5f; continue; }
                var center = foot + along * (cursor + w / 2f) + Vector3.up * (kind.foot + kind.height / 2f) - normal * (ReliefDepth / 2f);
                if (ClearForRelief(self, center, normal, w, kind.height, samples))
                {
                    ScannedProps.Fit(deco, kind.model, center, frame, Quaternion.identity, new Vector3(w, kind.height, ReliefDepth)).isStatic = true;
                    placed++;
                    if (kind.standing && !pillar && rng.NextDouble() < 0.45)
                        offerings += Offering(deco, foot + along * (cursor + w / 2f + (float)(rng.NextDouble() - 0.5) * w * 0.6f) + normal * 0.35f, samples, rng) ? 1 : 0;
                }
                cursor += w + (pillar ? 10f : 0.6f + (float)rng.NextDouble() * 1.6f);
            }
            return placed;
        }

        /// <summary>Nothing stands in front of a relief (a block, a herse, a plate, an eye of the sun) and no pass arc comes near it.</summary>
        static bool ClearForRelief(Collider self, Vector3 center, Vector3 normal, float width, float height, List<Vector3> samples)
        {
            var rot = Quaternion.LookRotation(-normal, Vector3.up);
            var probe = center + normal * 0.45f;
            foreach (var c in Physics.OverlapBox(probe, new Vector3(width / 2f + 0.2f, height / 2f - 0.05f, 0.4f), rot, ~0, QueryTriggerInteraction.Collide))
                if (c != self && c.GetComponentInParent<KillZone>() == null) return false;
            float reach = PassCorridor.DecorationClearance + 0.4f;
            var box = new Bounds(center, Vector3.zero);
            foreach (var corner in new[] { -1f, 1f })
                foreach (var up in new[] { -1f, 1f })
                    box.Encapsulate(center + rot * new Vector3(corner * width / 2f, up * height / 2f, 0f));
            box.Expand(2f * reach);
            return !samples.Any(box.Contains);
        }

        // ------------------------------------------------------------------ offerings

        /// <summary>A small offering (no collider: it is clutter at a wall's foot or round a fire) where it bothers nothing.</summary>
        static bool Offering(Transform deco, Vector3 at, List<Vector3> samples, Random rng)
        {
            var hit = Physics.RaycastAll(at + Vector3.up * 0.8f, Vector3.down, 1.4f, 1 << Environment, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.GetComponent<CourseCeiling>() == null).OrderBy(h => h.distance).FirstOrDefault();
            if (hit.collider == null || Mathf.Abs(hit.point.y - at.y) > 0.3f) return false;
            at.y = hit.point.y;
            if (ScannedProps.NearGameplay(at, 1.2f)) return false;
            if (samples.Any(s => s.y < at.y + 1.6f && (new Vector2(s.x - at.x, s.z - at.z)).magnitude < 1.1f)) return false;
            var (model, height) = Offerings[rng.Next(Offerings.Length)];
            NatureKit.Prop(deco, model, ScannedProps.MaterialOf(model), deco.InverseTransformPoint(at), (float)rng.NextDouble() * 360f, height, shadows: false).isStatic = true;
            return true;
        }

        /// <summary>Round each fire (a checkpoint's, the summit beacon): a few offerings on the clearing's edge, away from the spawns.</summary>
        static int FireOfferings(Transform room, Transform deco, List<Vector3> samples, Random rng)
        {
            int placed = 0;
            foreach (var fire in room.GetComponentsInChildren<CampfirePresentation>())
            {
                var f = fire.transform.position;
                for (int k = 0, tries = 0; k < 5 && tries < 24; tries++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 1.5f + (float)rng.NextDouble() * 0.8f;
                    var at = f + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (room.GetComponentsInChildren<PlayerSpawn>().Any(s => Vector3.Distance(s.transform.position, at) < 1.3f)) continue;
                    if (Offering(deco, at, samples, rng)) { k++; placed++; }
                }
            }
            return placed;
        }

        // ------------------------------------------------------------------ the movers as carved slabs

        /// <summary>
        /// Every stone a player rides (a shuttle, a ford stone, the well's lift, a rising bridge, a pivot) drawn as carved slabs, face up,
        /// filling its collider (top flush): a glyph block on a square stone, Palenque reliefs on a bridge, carved stelae on a pivot. The
        /// kit's own drawing is hidden; the slabs sit under a <see cref="CustomVisual"/> on the platform, so they ride with it.
        /// </summary>
        static int CarvedMovers(Transform room)
        {
            var platforms = room.GetComponentsInChildren<SignalActuator>()
                .Where(a => !(a is SignalSwitch) && a.Platform != null && a.LethalEdge == null).Select(a => a.Platform)
                .Concat(room.GetComponentsInChildren<MovingPlatform>().Select(m => m.Platform)).Where(p => p != null).Distinct().ToArray();
            int placed = 0;
            foreach (var p in platforms)
            {
                var box = p.GetComponentsInChildren<BoxCollider>().Where(c => !c.isTrigger && !CustomVisual.Covers(c))
                    .OrderByDescending(c => c.size.x * c.size.z * c.transform.lossyScale.x * c.transform.lossyScale.z).FirstOrDefault();
                if (box == null) continue;
                var m = p.worldToLocalMatrix * box.transform.localToWorldMatrix;
                var center = m.MultiplyPoint3x4(box.center);
                var size = Vector3.Scale(box.size, m.lossyScale);
                if (size.y > 1.5f || size.x < 2.5f || size.z < 2.5f) continue;   // a slab, not a door or a wall
                var holder = new GameObject("Carved stone", typeof(CustomVisual)).transform;
                holder.SetParent(p, false);
                var faceUp = Quaternion.Euler(90f, 0f, 0f);
                if (Mathf.Abs(size.x - size.z) < 1.5f)
                    placed += Tile(holder, p, "maya_stela_fragment", faceUp, center, size, 1, alongZ: true);
                else
                {
                    bool alongZ = size.z > size.x;
                    float width = alongZ ? size.x : size.z, span = alongZ ? size.z : size.x;
                    string model = width >= 3.8f ? "palenque_relief_wall" : "maya_relief_pillar";
                    // lay the relief face up with its long side along the span
                    var natural = ScannedProps.Extent(model, faceUp);
                    bool longAlongZ = natural.z >= natural.x;
                    var orient = (longAlongZ == alongZ ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f)) * faceUp;
                    float aspect = Mathf.Max(natural.x, natural.z) / Mathf.Min(natural.x, natural.z);
                    int n = Mathf.Max(1, Mathf.RoundToInt(span / (width * aspect)));
                    placed += Tile(holder, p, model, orient, center, size, n, alongZ);
                }
                foreach (var r in p.GetComponentsInChildren<MeshRenderer>().Where(r => !CustomVisual.Covers(r) && r.GetComponent<KitSkin>() != null))
                    r.enabled = false;
            }
            return placed;
        }

        static int Tile(Transform holder, Transform platform, string model, Quaternion orient, Vector3 center, Vector3 size, int n, bool alongZ)
        {
            var axis = alongZ ? Vector3.forward : Vector3.right;
            float span = alongZ ? size.z : size.x;
            var tile = alongZ ? new Vector3(size.x, size.y, span / n) : new Vector3(span / n, size.y, size.z);
            for (int i = 0; i < n; i++)
            {
                var local = center + axis * (-span / 2f + span / n * (i + 0.5f));
                ScannedProps.Fit(holder, model, platform.TransformPoint(local), platform.rotation, orient, tile);
            }
            return n;
        }

        // ------------------------------------------------------------------ statues round the courts

        /// <summary>
        /// Up to two statues on the rim of a court, on pedestals, facing its centre, where the route neither comes in nor goes out
        /// (70° or more from both), clear of the spawns, the fire and every pass. They look like they can be climbed, so the pedestal and
        /// the statue are solid (Environment boxes).
        /// </summary>
        static int CourtStatues(List<Transform> rooms, int i, Transform parent, List<Vector3> samples, Random rng)
        {
            var room = rooms[i];
            int placed = 0;
            foreach (var court in room.GetComponentsInChildren<BoxCollider>().Where(c => c.name == "Court" || c.name == "Start court").ToArray())
            {
                var center = court.bounds.center + Vector3.up * court.bounds.extents.y;
                bool start = court.name == "Start court";
                Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }
                // the way in (back along this section) and the way out (along the next one, or this one from the start court)
                var outDir = start ? Flat(room.forward) : i + 1 < rooms.Count ? Flat(rooms[i + 1].forward) : Flat(room.forward);
                var inDir = start ? -outDir : -Flat(room.forward);
                var candidates = new List<(float score, Vector3 dir)>();
                for (int a = 0; a < 360; a += 15)
                {
                    var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    float score = Mathf.Min(Vector3.Angle(dir, inDir), Vector3.Angle(dir, outDir));
                    if (score >= 70f) candidates.Add((score, dir));
                }
                var chosen = new List<Vector3>();
                foreach (var (_, dir) in candidates.OrderByDescending(c => c.score))
                {
                    if (chosen.Count >= 2) break;
                    if (chosen.Any(d => Vector3.Angle(d, dir) < 90f)) continue;
                    var at = center + dir * StatueRim;
                    if (!StatueFits(room, court, at, samples)) continue;
                    chosen.Add(dir);
                }
                foreach (var dir in chosen)
                {
                    var kind = CourtStatueKinds[rng.Next(CourtStatueKinds.Length)];
                    Statue(parent, kind.model, center + dir * StatueRim, -dir, kind.height, kind.sink, kind.pedestal);
                    placed++;
                }
            }
            return placed;
        }

        static bool StatueFits(Transform room, Collider court, Vector3 at, List<Vector3> samples)
        {
            // on the court's paving (both of its squares), nothing else within the statue's reach (a checkpoint's clearing is fine)
            if (!Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out var hit, 2.4f, 1 << Environment, QueryTriggerInteraction.Ignore) || !hit.collider.name.StartsWith(court.name))
                return false;
            foreach (var c in Physics.OverlapBox(at + Vector3.up * 2.2f, new Vector3(1.6f, 2.1f, 1.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
                if (c.GetComponentInParent<KillZone>() == null && c.GetComponentInParent<Checkpoint>() == null) return false;
            if (room.GetComponentsInChildren<PlayerSpawn>().Any(s => Vector3.Distance(s.transform.position, at) < 3.5f)) return false;
            if (room.GetComponentsInChildren<CampfirePresentation>().Any(f => Vector3.Distance(f.transform.position, at) < 3.5f)) return false;
            return !samples.Any(s => s.y < at.y + 5.5f && s.y > at.y - 1f && new Vector2(s.x - at.x, s.z - at.z).magnitude < 2.6f);
        }

        /// <summary>A statue facing <paramref name="facing"/>, on a pedestal of masonry when asked; its box makes it solid.</summary>
        static void Statue(Transform parent, string model, Vector3 at, Vector3 facing, float height, float sink, bool pedestal)
        {
            const float Plinth = 0.8f;
            var foot = at;
            if (pedestal)
            {
                var block = Block(parent, model + " pedestal", at + Vector3.up * Plinth / 2f, new Vector3(2f, Plinth, 2f), KitRole.Wall,
                                  Quaternion.LookRotation(facing));
                Carved(block, TempleMaterials.Slab);
                foot += Vector3.up * Plinth;
            }
            var statue = ScannedProps.Stand(parent, model, foot, Quaternion.LookRotation(-facing), height, sink);
            statue.isStatic = true;
            // the statue's box: from its foot (it never reaches under the pedestal) to its top
            var b = ScannedProps.WorldBounds(statue);
            b.SetMinMax(new Vector3(b.min.x, Mathf.Max(b.min.y, foot.y), b.min.z), b.max);
            var solid = new GameObject("Statue box") { layer = Environment };
            solid.transform.SetParent(statue.transform, false);
            solid.transform.position = b.center;
            solid.AddComponent<BoxCollider>().size = b.size;
            solid.isStatic = true;
        }

        // ------------------------------------------------------------------ colossi in the jungle

        /// <summary>
        /// Colossal statues and fallen stelae rising out of the jungle round the temple (decoration): an Olmec face, the water goddess,
        /// the green-stone man, a helmeted head fallen on its back, glyph blocks tilted in the canopy. Each stays 7 m or more under every
        /// floor within 12 m of its footprint (a jump carries less), so nothing that looks like ground is ever within a jump; farther
        /// out it may rise over the stone, and no pass arc comes near it.
        /// </summary>
        static int Colossi(Transform root, Bounds pyramid, List<Vector3> samples)
        {
            var parent = new GameObject("Colossi").transform;
            parent.SetParent(root, false);
            parent.gameObject.AddComponent<CourseDecoration>();
            var rng = new Random(2027);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var kinds = new (string model, float min, float max, float sink, float tilt)[]
            {
                ("olmec_face_fragment", 26f, 32f, 0.3f, 6f), ("aztec_chalchiuhtlicue", 34f, 42f, 0.12f, 4f),
                ("olmec_standing_figure", 30f, 38f, 0.15f, 5f), ("maya_head_helmet", 22f, 28f, 0.25f, 55f),
                ("maya_stela_fragment", 14f, 18f, 0.2f, 22f), ("ballgame_palma", 24f, 30f, 0.2f, 14f),
            };
            var rects = Footprints.Select(f => FootRect(f)).ToArray();
            int course = LayerMask.GetMask("Environment", "Hazard", BodyScreen.LayerName);
            var placed = new List<Vector2>();
            int count = 0;
            for (int attempt = 0; attempt < 600 && count < 14; attempt++)
            {
                var anchor = Footprints[rng.Next(Footprints.Count)];
                var at = anchor.origin + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * R(30f, 100f);
                float near = rects.Min(r => DistanceToRect(r, at.x, at.z));
                if (near < 16f) continue;
                if (samples.Any(s => new Vector2(s.x - at.x, s.z - at.z).magnitude < 14f)) continue;
                var expanded = pyramid;
                expanded.Expand(new Vector3(16f, 0f, 16f));
                if (expanded.Contains(new Vector3(at.x, expanded.center.y, at.z))) continue;
                if (placed.Any(p => (p - new Vector2(at.x, at.z)).magnitude < 40f)) continue;
                var kind = kinds[count % kinds.Length];
                float height = R(kind.min, kind.max);
                at.y = ground.Sample(at.x, at.z);
                float ceiling = float.MaxValue;
                foreach (var c in Physics.OverlapBox(new Vector3(at.x, 0f, at.z), new Vector3(12f + height * 0.25f, 300f, 12f + height * 0.25f), Quaternion.identity, course, QueryTriggerInteraction.Ignore))
                    if (c.GetComponentInParent<KillZone>() == null) ceiling = Mathf.Min(ceiling, c.bounds.min.y - 7f);
                float sink = height * kind.sink;
                if (at.y - sink + height > ceiling) height = (ceiling - at.y) / (1f - kind.sink);
                if (height < kind.min * 0.6f) continue;
                var rotation = Quaternion.Euler(0f, R(0f, 360f), 0f) * Quaternion.Euler(-kind.tilt * R(0.6f, 1f), 0f, R(-3f, 3f));
                ScannedProps.Stand(parent, kind.model, at, rotation, height, height * kind.sink).isStatic = true;
                placed.Add(new Vector2(at.x, at.z));
                count++;
            }
            return count;
        }
    }
}
