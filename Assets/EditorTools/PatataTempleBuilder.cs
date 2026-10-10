using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using static HotPatata.Editor.BombObstacleKitBuilder;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// PatataTemple (PROJECT_SPEC §15e, ARCHITECTURE §4 and §25.4, M15): a temple in ruins over a jungle gorge, built for three
    /// players, in PatataWilds' nature look (stone instead of planks). Fifteen sections in three acts, one checkpoint each. Every
    /// section keeps a contract (<see cref="SectionContract"/>, §13.20) that also says why two players fail (§15e): its puzzle needs
    /// two tasks at once, a pair keeping the bomb alive (nobody catches their own throw) and a third holding the way, or two
    /// bodies on a heavy slab and the carrier, or a cycle of three wings each opened from another. It uses the kit plus the four
    /// systems built for three (§13.21-§13.24): the heavy plate, the hourglass plate, the sun beam and the pivot.
    /// The route is the temple's own: a causeway that bends up the valley over the jungle's canopy (act 1), a ring round the great
    /// pyramid on the temple's platform, its corners at 60° (act 2), then the well up to the sanctuary's skyway 30 m up, which circles
    /// the temple and ends on the altar over the pyramid's summit (act 3). Sections are built locally along +Z (floor of their start
    /// at y = 0, x across) and turned as units by their own angle; each ends on an octagonal court open to the sky, the next one
    /// starts from. Puzzle sections stand under a stone roof every cross wall reaches. Never edit the scene by hand: change this
    /// builder and rebuild (menu HotPatata/Course/Build PatataTemple).
    /// </summary>
    public static partial class PatataTempleBuilder
    {
        public const string ScenePath = "Assets/Scenes/PatataTemple.unity";
        public const string SceneName = "PatataTemple";
        const string GroupName = "PatataTemple";
        public const int CheckpointCount = 15;
        public const int Players = 3;   // the menu needs three online (§15e)
        const float Hub = 10f;          // a court's reach along the route (its octagon is wider: CourtHalf)
        const float CourtHalf = 12f;    // a court is two 24 m squares, one turned 45°: an octagon any turn meets
        const float Roof = 7f;          // a covered section's stone roof above its floor (every cross wall reaches it)
        const float CoverHalf = 16f;    // half the width of a roof: over the three wings and the void beside them
        const float KillDrop = 10f;     // a fall dies this far under a section's lowest floor
        const float Gap = 14f;          // a gap only a bridge, a pivot or a mover crosses
        const float WingHalf = 3.5f;    // half a wing's width (act 2: the west wing, the nave, the east wing)
        const float Divide = 4f;        // the divides between the wings stand at x = ±4

        /// <summary>The shorter hold fuse (spec §20): 5 s from checkpoint 8, 4.5 s from checkpoint 13.</summary>
        public static float FuseFor(int id) => id >= 13 ? 4.5f : id >= 8 ? 5f : 0f;

        struct SectionSpec
        {
            public string name;
            public int act;
            public float length, dy;
            public float turn;               // after this section, degrees (+ right)
            public float split;              // a section that climbs: where its upper floor starts (for the overlap check)
            public bool covered, outdoors;
            public Action<Transform> contents;
        }

        static SectionSpec S(string name, int act, float length, float dy, float turn, Action<Transform> contents, bool covered = false, bool roofed = false,
                             float split = 0f) =>
            new SectionSpec { name = name, act = act, length = length, dy = dy, turn = turn, covered = covered, outdoors = !covered && !roofed, contents = contents,
                              split = split };

        static SurfaceTheme T(string floor, string wall, string ceiling) => new SurfaceTheme(NatureDir + floor, NatureDir + wall, NatureDir + ceiling);

        /// <summary>The temple's stone: paved floors, masonry walls, mossy roofs (ARCHITECTURE §25.4).</summary>
        static SurfaceTheme Stone => T(TempleMaterials.Floor, TempleMaterials.Wall, TempleMaterials.Roof);

        /// <summary>The temple's masonry materials (Poly Haven sets in the nature library, NatureMaterialBuilder).</summary>
        static class TempleMaterials
        {
            public const string Floor = "Nature_TempleFloor", Wall = "Nature_TempleWall", Roof = "Nature_TempleRoof", Slab = "Nature_TempleSlab";
        }

        // The route: the causeway bends up the valley; the ring turns 60° at each corner round the pyramid; the well climbs 30 m to
        // the skyway, which circles the temple and turns in to the altar over the pyramid's summit. CheckFootprints keeps the
        // sections apart (the skyway passes over the ring 22 m above its roofs).
        static SectionSpec[] Sections() => new[]
        {
            // Act 1 - La Chaussée (misty dawn): the causeway, one system at a time
            S("01 La Dalle lourde", 1, 64, 0, 25, DalleLourde),
            S("02 La Relève", 1, 84, 0, -35, LaReleve, covered: true),
            S("03 Le Pivot", 1, 90, 0, 30, LePivot, covered: true),
            S("04 L'Œil du soleil", 1, 94, 0, -20, OeilDuSoleil, covered: true),
            S("05 Le Seuil", 1, 72, 0, 0, LeSeuil),
            // Act 2 - Les Trois Ailes (white noon): the ring round the pyramid, the team splits
            S("06 Le Partage", 2, 96, 0, -60, LePartage, covered: true),
            S("07 Les Têtes de pierre", 2, 96, 0, -60, TetesDePierre, covered: true),
            S("08 Les Rayons croisés", 2, 96, 0, -60, RayonsCroises, covered: true),
            S("09 La Rose", 2, 110, 0, -60, LaRose, covered: true),
            S("10 La Réunion", 2, 90, 0, -60, LaReunion, covered: true),
            // Act 3 - Le Sanctuaire (tropical storm): up the well, the skyway round the temple, the altar
            S("11 Le Puits", 3, 60, 30, 0, LePuits, roofed: true, split: 36),
            S("12 Le Couloir des pièges", 3, 110, 0, -60, CouloirDesPieges, covered: true),
            S("13 Le Gué des trois pierres", 3, 100, 0, -90, GueDesTroisPierres, covered: true),
            S("14 La Croisée", 3, 96, 0, -60, LaCroisee, covered: true),
            S("15 L'Autel", 3, 130, 0, 0, LAutel, covered: true),
        };

        struct Footprint
        {
            public Vector3 origin;
            public float yaw, length, floorY, dy, split;
        }

        static readonly List<Footprint> Footprints = new List<Footprint>();
        static readonly List<Vector3> Route = new List<Vector3>();
        static readonly List<bool> Outdoors = new List<bool>();
        static float killTop;   // the current section's kill plane (local): falls die here, retracted bridges hide under it

        [MenuItem("HotPatata/Course/Build PatataTemple")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            NatureMaterialBuilder.Ensure(false);
            NatureAudioFactory.EnsureClips();
            NatureKit.BuildCampfirePrefab();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlateHeavy + ".prefab") == null) BuildTrioKitOnly();
            var scene = PrepareCourseScene(ScenePath, out var section);
            foreach (var root in scene.GetRootGameObjects().Where(g => g.name == "PlantExposureVolume").ToArray()) Object.DestroyImmediate(root);
            var sections = Sections();
            Route.Clear();
            Outdoors.Clear();
            Footprints.Clear();
            Ruins.Clear();
            int restyled = 0;
            RebuildGroup(section, GroupName, group =>
            {
                using (UseLookSet(LookSet.Nature))
                {
                    var origin = Vector3.zero;
                    float yaw = 0;
                    for (int i = 0; i < sections.Length; i++)
                    {
                        var spec = sections[i];
                        var room = new GameObject(spec.name).transform;
                        room.SetParent(group, false);
                        float lowest = Mathf.Min(0f, spec.dy);
                        killTop = lowest - KillDrop;
                        using (UseTheme(Stone))
                        {
                            if (i == 0) Court(room, "Start court", Vector3.zero);
                            spec.contents(room);
                            if (i < sections.Length - 1) Court(room, "Court", new Vector3(0, spec.dy, spec.length));
                            // the roof covers the section between its courts; the courts stay open to the sky
                            if (spec.covered) StoneRoof(room, "Roof", -CoverHalf, CoverHalf, Hub, spec.length - Hub, Mathf.Max(0f, spec.dy) + Roof);
                            Kill(room, new Vector3(0, killTop - 1f, spec.length / 2f), new Vector3(2 * CoverHalf + 60f, 2f, spec.length + 2 * Hub + 24f));
                            Jungle(room, spec);
                        }
                        restyled += NatureRestyle.Apply(room, Stone);
                        StoneMovers(room);
                        var rotation = Quaternion.Euler(0, yaw, 0);
                        room.SetPositionAndRotation(origin, rotation);
                        Route.Add(origin + rotation * new Vector3(0, 1f, spec.length * 0.25f));
                        Route.Add(origin + rotation * new Vector3(0, spec.dy + 1f, spec.length * 0.85f));
                        Outdoors.Add(spec.outdoors);
                        Outdoors.Add(spec.outdoors);
                        Footprints.Add(new Footprint { origin = origin, yaw = yaw, length = spec.length, floorY = origin.y + lowest, dy = spec.dy, split = spec.split });
                        origin += rotation * Vector3.forward * spec.length + Vector3.up * spec.dy;
                        yaw += spec.turn;
                    }
                    CheckFootprints();
                    Physics.SyncTransforms();
                    string dressing = Dressing(group);
                    SafetyNet(group);
                    Debug.Log("[PatataTempleBuilder] " + dressing);
                }
            });
            section.gameObject.AddComponent<CourseRoute>().Configure(Route.ToArray(), Outdoors.ToArray());
            LookBuilder.ApplyToScene(scene);
            Physics.SyncTransforms();
            ValidatePasses();
            var findings = CourseContractCheck.Problems(scene, CourseContractCheck.Tuning);
            foreach (var f in findings) Debug.LogWarning("[PatataTempleBuilder] contract: " + f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RegisterInMenu(SceneName, ScenePath, CheckpointCount, MenuSlot.Third, Players);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PatataTempleBuilder] PatataTemple built: three acts, fifteen sections for three, {restyled} kit renderers in the nature look, " +
                      $"{findings.Count} contract finding(s).");
        }

        /// <summary>
        /// No two sections more than one apart overlap: their footprints (decks, roofs and the void beside them, turned with them) never
        /// meet in plan unless one passes high above the other. A section that climbs counts as its lower and its upper part.
        /// </summary>
        static void CheckFootprints()
        {
            const float Side = CoverHalf + 12f, Clearance = 3f, Height = Roof + 3f;
            var parts = new List<(int index, Vector2[] corners, float y0, float y1)>();
            for (int i = 0; i < Footprints.Count; i++)
            {
                var f = Footprints[i];
                var spans = f.dy != 0f ? new[] { (-Hub, f.split, f.origin.y), (f.split, f.length + Hub, f.origin.y + f.dy) }
                                       : new[] { (-Hub, f.length + Hub, f.origin.y) };
                foreach (var (z0, z1, y) in spans)
                    parts.Add((i, Corners(f, Side, z0, z1), y - Clearance, y + Height));
            }
            foreach (var a in parts)
                foreach (var b in parts)
                    if (b.index >= a.index + 2 && a.y0 < b.y1 && b.y0 < a.y1 && Overlap(a.corners, b.corners))
                        throw new InvalidOperationException($"PatataTemple sections {a.index + 1} and {b.index + 1} overlap");
        }

        /// <summary>A section's footprint in world XZ (half width <paramref name="side"/>, from z0 to z1 along it), its four corners.</summary>
        static Vector2[] Corners(Footprint f, float side, float z0, float z1)
        {
            var rot = Quaternion.Euler(0, f.yaw, 0);
            Vector2 C(float x, float z) { var w = f.origin + rot * new Vector3(x, 0, z); return new Vector2(w.x, w.z); }
            return new[] { C(-side, z0), C(side, z0), C(side, z1), C(-side, z1) };
        }

        /// <summary>Do two convex quadrilaterals overlap (separating axes)?</summary>
        static bool Overlap(Vector2[] a, Vector2[] b)
        {
            foreach (var poly in new[] { a, b })
                for (int i = 0; i < 4; i++)
                {
                    var edge = poly[(i + 1) % 4] - poly[i];
                    var axis = new Vector2(edge.y, -edge.x);
                    float aMin = a.Min(c => Vector2.Dot(c, axis)), aMax = a.Max(c => Vector2.Dot(c, axis));
                    float bMin = b.Min(c => Vector2.Dot(c, axis)), bMax = b.Max(c => Vector2.Dot(c, axis));
                    if (aMax <= bMin || bMax <= aMin) return false;
                }
            return true;
        }

        /// <summary>A last kill zone under everything: nothing falls out of the world.</summary>
        static void SafetyNet(Transform group)
        {
            float y = Footprints.Min(f => f.floorY) - JungleDepth - 8f;
            var go = Place(group, GameplayDir + "KillZone", "Safety net", new Vector3(0, y, 0), Quaternion.identity);
            var bounds = new Bounds(Footprints[0].origin, Vector3.zero);
            foreach (var f in Footprints) bounds.Encapsulate(f.origin);
            go.transform.position = new Vector3(bounds.center.x, y, bounds.center.z);
            go.transform.localScale = new Vector3(bounds.size.x + 400f, 4f, bounds.size.z + 400f);
        }

        // ------------------------------------------------------------------ pieces (section space: +Z forward, floor of the start at y = 0)

        /// <summary>A paved stone deck (top at <paramref name="y"/>), held up by ruined pillars down to the jungle (decoration).</summary>
        static GameObject Deck(Transform p, string name, float x0, float x1, float z0, float z1, float y, bool pillars = true)
        {
            var go = Block(p, name, new Vector3((x0 + x1) / 2, y - 0.5f, (z0 + z1) / 2), new Vector3(x1 - x0, 1, z1 - z0), KitRole.Floor);
            Carved(go, TempleMaterials.Floor);
            if (pillars && (x1 - x0) * (z1 - z0) >= 30f)
            {
                bool alongZ = z1 - z0 >= x1 - x0;
                float span = alongZ ? z1 - z0 : x1 - x0;
                int count = Mathf.Max(1, Mathf.RoundToInt(span / 20f));
                float thickness = Mathf.Clamp(Mathf.Min(x1 - x0, z1 - z0) * 0.35f, 1.2f, 3.2f);
                for (int i = 0; i < count; i++)
                {
                    float t = (i + 0.5f) / count;
                    var top = alongZ ? new Vector3((x0 + x1) / 2, y - 1f, Mathf.Lerp(z0, z1, t)) : new Vector3(Mathf.Lerp(x0, x1, t), y - 1f, (z0 + z1) / 2);
                    Ruins.Add(new RuinRequest { parent = p, localTop = top, thickness = thickness });
                }
            }
            return go;
        }

        /// <summary>
        /// A court (the octagon the route turns on): two 24 m paved squares, one turned 45°, open to the sky, its floor at
        /// <paramref name="center"/>.y. Whatever the next section's angle, its first deck meets it.
        /// </summary>
        static void Court(Transform p, string name, Vector3 center)
        {
            Deck(p, name, center.x - CourtHalf, center.x + CourtHalf, center.z - CourtHalf, center.z + CourtHalf, center.y);
            var turned = Block(p, name + " (turned)", center + new Vector3(0, -0.52f, 0), new Vector3(2 * CourtHalf, 1, 2 * CourtHalf), KitRole.Floor,
                               Quaternion.Euler(0, 45, 0));
            Carved(turned, TempleMaterials.Floor);
        }

        /// <summary>A solid block of masonry from <paramref name="bottom"/> up to <paramref name="top"/> (a tier, a face that cannot be climbed when high).</summary>
        static GameObject Solid(Transform p, string name, float x0, float x1, float z0, float z1, float top, float bottom = -1f)
        {
            var go = Block(p, name, new Vector3((x0 + x1) / 2, (top + bottom) / 2, (z0 + z1) / 2), new Vector3(x1 - x0, top - bottom, z1 - z0), KitRole.Wall);
            Carved(go, TempleMaterials.Wall);
            return go;
        }

        /// <summary>A stone roof whose underside is at <paramref name="y"/> (a ceiling for the passes): mossy slabs, vines hanging under it.</summary>
        static GameObject StoneRoof(Transform p, string name, float x0, float x1, float z0, float z1, float y)
        {
            var go = Block(p, name, new Vector3((x0 + x1) / 2, y + 0.5f, (z0 + z1) / 2), new Vector3(x1 - x0, 1, z1 - z0), KitRole.Ceiling);
            Carved(go, TempleMaterials.Roof);
            go.AddComponent<CourseCeiling>();
            return go;
        }

        /// <summary>Draws a block as carved stone (a bevelled box, not rough rock): the temple's masonry.</summary>
        static void Carved(GameObject block, string material)
        {
            var visual = block.transform.Find("Visual");
            if (visual != null) CourseKit.Skin(visual.gameObject, KitShape.BevelBox, KitColor.Neutral, Mat(NatureDir + material));
        }

        /// <summary>A masonry wall across the section at <paramref name="z"/> (x0..x1), <paramref name="height"/> tall, with holes.</summary>
        static void WallAcross(Transform p, string name, float z, float y, float height, float x0, float x1, params Hole[] holes)
        {
            Wall(p, name, z, y, height, x0, x1, holes);
            foreach (Transform child in p)
                if (child.name.StartsWith(name + "_")) Carved(child.gameObject, TempleMaterials.Wall);
        }

        /// <summary>A masonry wall along the section at <paramref name="x"/> (z0..z1), with holes (their X0/X1 are along z here).</summary>
        static void WallAlong(Transform p, string name, float x, float y, float height, float z0, float z1, params Hole[] holes)
        {
            var zs = new List<float> { z0, z1 };
            foreach (var h in holes) { zs.Add(h.X0); zs.Add(h.X1); }
            zs.Sort();
            int piece = 0;
            for (int i = 0; i + 1 < zs.Count; i++)
            {
                float a = zs[i], b = zs[i + 1];
                if (b - a < 0.01f) continue;
                float mid = (a + b) / 2f;
                Hole? hole = null;
                foreach (var h in holes) if (mid > h.X0 && mid < h.X1) hole = h;
                void Piece(float y0, float y1)
                {
                    if (y1 - y0 < 0.01f) return;
                    Carved(Block(p, $"{name}_{++piece}", new Vector3(x, y + (y0 + y1) / 2f, mid), new Vector3(1f, y1 - y0, b - a), KitRole.Wall), TempleMaterials.Wall);
                }
                if (hole == null) Piece(0f, height);
                else { Piece(0f, hole.Value.Y0); Piece(hole.Value.Y1, height); }
            }
        }

        /// <summary>
        /// A shrine around a plate: three walls and a roof, its door facing <paramref name="doorDir"/> (+1: +Z, -1: -Z, ±2: ±X), so no
        /// throw from the other sides reaches whoever holds the plate (they are never the carrier's partner while they hold it).
        /// </summary>
        static void Shrine(Transform p, string name, Vector3 center, float size, int doorDir, float height = 3.6f)
        {
            float h = size / 2f, y = center.y;
            var walls = new List<(Vector3 c, Vector3 s)>
            {
                (new Vector3(center.x, y + height / 2f, center.z + h + 0.5f), new Vector3(size + 2f, height, 1f)),   // +Z
                (new Vector3(center.x, y + height / 2f, center.z - h - 0.5f), new Vector3(size + 2f, height, 1f)),   // -Z
                (new Vector3(center.x + h + 0.5f, y + height / 2f, center.z), new Vector3(1f, height, size)),        // +X
                (new Vector3(center.x - h - 0.5f, y + height / 2f, center.z), new Vector3(1f, height, size)),        // -X
            };
            int skip = doorDir switch { 1 => 0, -1 => 1, 2 => 2, _ => 3 };
            for (int i = 0; i < walls.Count; i++)
                if (i != skip) Carved(Block(p, $"{name} wall {i}", walls[i].c, walls[i].s, KitRole.Wall), TempleMaterials.Wall);
            StoneRoof(p, name + " roof", center.x - h - 1f, center.x + h + 1f, center.z - h - 1f, center.z + h + 1f, y + height);
        }

        static PressurePlate HeavyPlate(Transform p, string name, Vector3 at) =>
            Place(p, PlateHeavy, name, at, Quaternion.identity).GetComponent<PressurePlate>();

        static PressurePlate HourglassPlate(Transform p, string name, Vector3 at, float seconds)
        {
            var plate = Place(p, PlateHourglass, name, at, Quaternion.identity).GetComponent<PressurePlate>();
            SetField(plate, "memorySeconds", v => v.floatValue = seconds);
            return plate;
        }

        static PressurePlate HandsFreePlate(Transform p, string name, Vector3 at) =>
            Place(p, PlateHandsFree, name, at, Quaternion.identity).GetComponent<PressurePlate>();

        /// <summary>A herse (portcullis of logs) filling a door hole across the section (x0..x1 at z), raised while its source is active.</summary>
        static GameObject Herse(Transform p, string name, float x0, float x1, float z, float y, MonoBehaviour source, float height = PassageHeight) =>
            Actuator(p, Door, name, new Vector3((x0 + x1) / 2, y + height / 2, z), new Vector3(x1 - x0, height, 0.8f), new Vector3(0, height + 0.3f, 0), source);

        /// <summary>A herse in a door hole along the section (z0..z1 at x).</summary>
        static GameObject HerseAlong(Transform p, string name, float z0, float z1, float x, float y, MonoBehaviour source, float height = PassageHeight)
        {
            var go = Actuator(p, Door, name, new Vector3(x, y + height / 2, (z0 + z1) / 2), new Vector3(z1 - z0, height, 0.8f), new Vector3(0, height + 0.3f, 0), source);
            go.transform.rotation = Quaternion.Euler(0, 90, 0);
            return go;
        }

        /// <summary>A stone bridge that rises from under the kill plane to span a gap along z while its source is active.</summary>
        static GameObject RisingBridge(Transform p, string name, float x, float z0, float z1, float y, float width, MonoBehaviour source, float seconds = 1.5f)
        {
            float closed = killTop - 2f;
            var go = Actuator(p, Bridge, name, new Vector3(x, closed, (z0 + z1) / 2), new Vector3(width, 0.5f, z1 - z0), new Vector3(0, y - 0.25f - closed, 0), source);
            SetField(go.GetComponent<SignalActuator>(), "travelSeconds", v => v.floatValue = seconds);
            return go;
        }

        /// <summary>A stone bridge rising to span from <paramref name="a"/> to <paramref name="b"/> (floor points, any heading).</summary>
        static GameObject RisingBridgeBetween(Transform p, string name, Vector3 a, Vector3 b, float width, MonoBehaviour source, float seconds = 1.5f)
        {
            float closed = killTop - 2f;
            var flat = b - a;
            flat.y = 0;
            var mid = (a + b) / 2;
            var go = Actuator(p, Bridge, name, new Vector3(mid.x, closed, mid.z), new Vector3(width, 0.5f, flat.magnitude), new Vector3(0, mid.y - 0.25f - closed, 0), source);
            go.transform.rotation = Quaternion.LookRotation(flat.normalized);
            SetField(go.GetComponent<SignalActuator>(), "travelSeconds", v => v.floatValue = seconds);
            return go;
        }

        /// <summary>
        /// A pivot (§13.24): a stone slab of <paramref name="size"/> turning about its centre from the closed heading
        /// (<paramref name="closedYaw"/>, degrees) by <paramref name="turn"/> while its source is active, carrying its riders.
        /// </summary>
        static GameObject Pivot(Transform p, string name, Vector3 center, Vector3 size, float closedYaw, float turn, float seconds, MonoBehaviour source)
        {
            var go = Place(p, BombObstacleKitBuilder.Pivot, name, center - Vector3.up * size.y / 2f, Quaternion.Euler(0, closedYaw, 0));
            ConfigurePivot(go, size, turn, seconds);
            Wire(go, source);
            return go;
        }

        /// <summary>
        /// A sun beam (§13.23) from a slit at <paramref name="slit"/> (its height is the beam's) along <paramref name="dir"/> for
        /// <paramref name="length"/> metres to a stone eye.
        /// </summary>
        static SunBeam Beam(Transform p, string name, Vector3 slit, Vector3 dir, float length, bool activeWhileWhole = false)
        {
            var go = Place(p, BombObstacleKitBuilder.Beam, name, slit, Quaternion.LookRotation(dir));
            ResizeSunBeam(go, length);
            if (activeWhileWhole) BeamActiveWhileWhole(go, true);
            return go.GetComponent<SunBeam>();
        }

        /// <summary>A vine ring facing along x (a throw across the section) or along z, on its stone pillar.</summary>
        static BombGate Ring(Transform p, string name, Vector3 floor, float height, float hold, bool alongX)
        {
            var gate = Gate(p, name, floor, height, hold);
            if (alongX) gate.transform.rotation = Quaternion.Euler(0, 90, 0);
            return gate;
        }

        /// <summary>A vine ring hanging where <paramref name="pass"/>'s arc is half way, facing the pass, on a vine from the roof.</summary>
        static BombGate RingOnPass(Transform p, string name, PassCorridor pass, float hold)
        {
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/ScriptableObjects/Tuning/GameTuning.asset");
            var samples = new List<Vector3>();
            if (!pass.TrySample(tuning, samples)) throw new InvalidOperationException("unreachable " + pass.name);
            var center = samples[samples.Count / 2];
            var flat = pass.To - pass.From;
            flat.y = 0;
            var gate = Place(p, GateRing, name, center, Quaternion.LookRotation(flat.normalized)).GetComponent<BombGate>();
            SetField(gate, "holdSeconds", v => v.floatValue = hold);
            NatureKit.Detail(p, name + " vine", center + Vector3.up * 3.5f, new Vector3(0.12f, 4f, 0.12f), KitRole.Frame);
            return gate;
        }

        /// <summary>A checkpoint at the junction deck and its fire beside it (never on a spawn, never on the way out).</summary>
        static void CP(Transform p, int id, Vector3 position, float fireSide = 1f)
        {
            var go = AddCheckpoint(p, $"CP_{id:00}", id, position, FuseFor(id));
            NatureKit.CampfireCheckpoint(p, go.GetComponent<Checkpoint>(), position + new Vector3(3.8f * fireSide, 0, 0));
        }

        /// <summary>A checkpoint whose arch the bomb must fly through first (§13.17), and its fire.</summary>
        static void ArchCP(Transform p, int id, Vector3 pad, Vector3 archFloor, float fireSide = -1f)
        {
            string name = $"CP_{id:00}";
            ArchCheckpoint(p, name, id, pad, FuseFor(id), archFloor);
            NatureKit.CampfireCheckpoint(p, p.Find(name).GetComponent<Checkpoint>(), pad + new Vector3(3.8f * fireSide, 0, 0));
        }

        /// <summary>The contract of a section for three (§13.20, §15e).</summary>
        static void Trio(Transform p, string force, string twoFail, params SectionContract.Shortcut[] locks) =>
            Contract(p, force, Players, "With two: " + twoFail, locks);

        static void Kill(Transform p, Vector3 center, Vector3 size)
        {
            var go = Place(p, GameplayDir + "KillZone", "Fall", center, Quaternion.identity);
            go.transform.localScale = size;
        }

        /// <summary>Torches along a covered deck's edge (warm light under the stone).</summary>
        static void Torches(Transform p, float x, float z0, float z1, float y = 0f, float step = 18f)
        {
            for (float z = z0; z <= z1 + 0.01f; z += step) NatureKit.Torch(p, new Vector3(x, y, z), 7f, 13f);
        }

        /// <summary>
        /// The restyle draws movers (bridges, pivots, lifts) as log rafts; in the temple they are carved stone slabs. Doors keep their
        /// palisade of logs (a lethal part keeps its bands).
        /// </summary>
        static void StoneMovers(Transform room)
        {
            foreach (var a in room.GetComponentsInChildren<SignalActuator>(true))
            {
                if (a is SignalSwitch || a.Platform == null || a.LethalEdge != null) continue;
                var visual = a.Platform.Find("Visual");
                if (visual != null) CourseKit.Skin(visual.gameObject, KitShape.BevelBox, KitColor.Neutral, Mat(NatureDir + TempleMaterials.Slab));
            }
        }
    }
}
