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
    /// PatataWilds (PROJECT_SPEC §15c, ARCHITECTURE §4 and §25.3, M12): the third full course, a realistic outdoor climb through a
    /// forest, along a river and up granite cliffs to a summit, in five acts and twenty-five sections (about 1.7 km, 20+ minutes,
    /// one checkpoint per section). Sections are built locally along +Z and turned as units, as in PatataWorks and the industrial
    /// plant (whose proven layouts several sections reuse, re-dressed); the zig-zag of turns was searched so no two sections
    /// overlap. Every section is closed by cliffs (players can never leave it), open to the sky except the cave; pits are water
    /// or deep gorges with a kill zone. Every obstacle of the kit keeps its logic and box colliders and is drawn in the nature
    /// look (rafts, palisades, swinging logs, hollow logs, water wheels); checkpoints are campfires that catch when reached; the
    /// day moves from dawn to dusk with the acts. Never edit the scene by hand: change this builder and rebuild
    /// (menu HotPatata/Course/Build PatataWilds).
    /// </summary>
    public static partial class PatataWildsBuilder
    {
        public const string ScenePath = "Assets/Scenes/PatataWilds.unity";
        public const string SceneName = "PatataWilds";
        const string GroupName = "PatataWilds";
        public const int CheckpointCount = 25;
        const float Half = 12f;                  // half the width of a section (24 m, as in PatataWorks and the plant)
        const float CliffAbove = 7f;             // cliffs rise at least this far above a section's highest floor (stretches go up to 4.5 m higher)
        const float WaterBed = -3f;              // the bed of a water pit, under its section's lower floor
        const float WaterSurface = -0.9f;        // a water surface, under its section's lower floor

        /// <summary>The shorter hold fuse of the summit (spec §20): 5 s from checkpoint 21, 4.5 s from checkpoint 24.</summary>
        public static float FuseFor(int id) => id >= 24 ? 4.5f : id >= 21 ? 5f : 0f;

        struct SectionSpec
        {
            public string name;
            public int act;
            public float length, dy;
            public int turn;                      // after this section: -1 left, 0 straight, +1 right
            public bool pit;                      // a deep gorge below (20 m); else a solid foundation just under the floors
            public bool water;                    // the pit is water (bed 3 m down) rather than a gorge
            public float ceiling;                 // > 0: a cave roof at this height
            public SurfaceTheme theme;
            public Action<Transform> contents;
            public bool cornerFloor;              // at a turn into this section, its floor covers the corner square (else the previous one's)
        }

        // The stretch of floor the section being built owns (PatataWorks, the plant): no two floors overlap at one height.
        static float floorStart, floorEnd, floorEndLevel;
        static readonly List<Vector3> Route = new List<Vector3>();
        static readonly List<bool> Outdoors = new List<bool>();
        static int currentAct;

        static SurfaceTheme T(string floor, string wall, string ceiling) => new SurfaceTheme(NatureDir + floor, NatureDir + wall, NatureDir + ceiling);

        static readonly SurfaceTheme Forest = T("Nature_ForestFloor", "Nature_Cliff", "Nature_RockFace");
        static readonly SurfaceTheme Leaves = T("Nature_LeafLitter", "Nature_MossyRock", "Nature_RockFace");
        static readonly SurfaceTheme Trail = T("Nature_Trail", "Nature_Cliff", "Nature_RockFace");
        static readonly SurfaceTheme River = T("Nature_RockPath", "Nature_RockFace", "Nature_RockFace");
        static readonly SurfaceTheme Gravel = T("Nature_Gravel", "Nature_RockFace", "Nature_RockFace");
        static readonly SurfaceTheme Granite = T("Nature_RocksGround", "Nature_Cliff", "Nature_RockPitted");
        static readonly SurfaceTheme Cave = T("Nature_RockPath", "Nature_RockWall", "Nature_RockPitted");
        static readonly SurfaceTheme Mud = T("Nature_Mud", "Nature_MossyRock", "Nature_RockFace");
        static readonly SurfaceTheme Meadow = T("Nature_Grass", "Nature_Cliff", "Nature_RockFace");
        static readonly SurfaceTheme Summit = T("Nature_GrassPath", "Nature_RockWall", "Nature_RockFace");

        static SectionSpec S(string name, int act, float length, float dy, int turn, SurfaceTheme theme, Action<Transform> contents,
                             bool pit = false, bool water = false, float ceiling = 0f, bool cornerFloor = true) =>
            new SectionSpec { name = name, act = act, length = length, dy = dy, turn = turn, pit = pit || water, water = water, ceiling = ceiling,
                              theme = theme, contents = contents, cornerFloor = cornerFloor };

        // The turn after each section was searched (no two sections overlap; a 24 m shaft is entered and left by turns, as in the plant).
        static SectionSpec[] Sections() => new[]
        {
            // Act 1 - Misty Hollow (dawn)
            S("01 Trailhead Glade", 1, 56, 0, -1, Forest, TrailheadGlade),
            S("02 Fern Terraces", 1, 64, 7, 0, Leaves, FernTerraces),
            S("03 Brook Crossing", 1, 72, 0, 1, River, BrookCrossing, water: true),
            S("04 Rotten Boardwalk", 1, 64, 4, 1, Leaves, RottenBoardwalk, water: true),
            S("05 Hollow Log Junction", 1, 96, 0, -1, Forest, HollowLogJunction, cornerFloor: false),   // the boardwalk's raft gap and exit stay
            // Act 2 - River Run (noon)
            S("06 Log-Drive Lanes", 2, 96, 0, -1, River, LogDriveLanes, water: true),
            S("07 Rapids Rafts", 2, 72, 0, 0, Gravel, RapidsRafts, water: true),
            S("08 Two Banks, One Bomb", 2, 96, 0, 1, River, TwoBanks, water: true),
            S("09 Mudslide", 2, 72, -18, -1, Mud, Mudslide, pit: true),
            S("10 Mill Race", 2, 64, 0, 1, Gravel, MillRace),
            // Act 3 - Granite Cliffs (late afternoon)
            S("11 Cliff Base Relay", 3, 64, 10, 0, Granite, CliffBaseRelay),
            S("12 Hold the Rope", 3, 64, 14, -1, Granite, HoldTheRope, pit: true),
            S("13 Ember Cave", 3, 108, 0, 0, Cave, EmberCave, ceiling: 11),
            S("14 Ledge Traverse", 3, 48, 0, -1, Granite, LedgeTraverse),
            S("15 Up the Cliff", 3, 24, 34, -1, Granite, UpTheCliff, pit: true),
            // Act 4 - Waterfall Gorge (sunset)
            S("16 Spray Bridges", 4, 72, 0, 0, Gravel, SprayBridges, pit: true, cornerFloor: false),        // the shaft's top floors and cannon gap stay
            S("17 Behind the Falls", 4, 56, 0, 1, River, BehindTheFalls, water: true),
            S("18 Wheel Gorge", 4, 64, 6, 1, Gravel, WheelGorge),
            S("19 Down the Rapids", 4, 72, -28, -1, Mud, DownTheRapids, pit: true),
            S("20 Gorge Lock", 4, 24, 34, -1, Granite, GorgeLock, pit: true),
            // Act 5 - The Summit (dusk)
            S("21 Alpine Meadow", 5, 64, 8, 1, Meadow, AlpineMeadow, cornerFloor: false),                // the lock's top floors and tube gap stay
            S("22 Switchbacks", 5, 72, 16, 1, Summit, Switchbacks, pit: true),
            S("23 Boulder Run", 5, 60, 0, 0, Summit, BoulderRun),
            S("24 Knife Edge", 5, 64, 12, 1, Summit, KnifeEdge, pit: true),
            S("25 Summit Arch", 5, 52, 0, 0, Meadow, SummitArch),
        };

        /// <summary>One section's footprint in world XZ (for the terrain and the overlap check): centre, half extents, yaw.</summary>
        public struct Footprint
        {
            public Vector3 origin;     // the section's local origin in the world
            public float yaw;          // degrees
            public float length;
            public float floorY, topY; // its lowest floor and the top of its cliffs (world)
            public int act;
        }

        public static readonly List<Footprint> Footprints = new List<Footprint>();

        [MenuItem("HotPatata/Course/Build PatataWilds")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            NatureMaterialBuilder.Ensure(false);
            NatureKit.EnsureWater();
            NatureAudioFactory.EnsureClips();
            NatureKit.BuildCampfirePrefab();
            if (AssetDatabase.LoadAssetAtPath<Mesh>(NatureTreeBuilder.MeshPath("Grass", 0, 0)) == null) NatureTreeBuilder.BuildAll();
            var scene = IndustrialLabBuilder.PrepareScene(ScenePath, out var section);
            var sections = Sections();
            Route.Clear();
            Outdoors.Clear();
            Footprints.Clear();
            int restyled = 0;
            RebuildGroup(section, GroupName, root =>
            {
                using (UseLookSet(LookSet.Nature))
                {
                    var origin = Vector3.zero;
                    float yaw = 0;
                    int n = sections.Length;
                    var absTop = new float[n];
                    var absLowest = new float[n];
                    float y = 0f;
                    for (int i = 0; i < n; i++) { absTop[i] = y + Mathf.Max(0f, sections[i].dy); absLowest[i] = y + Mathf.Min(0f, sections[i].dy); y += sections[i].dy; }
                    for (int i = 0; i < n; i++)
                    {
                        var spec = sections[i];
                        currentAct = spec.act;
                        bool first = i == 0, last = i == n - 1;
                        floorStart = first ? -Half : sections[i - 1].turn == 0 ? 0 : spec.cornerFloor ? -Half : Half;
                        floorEnd = last ? spec.length + Half : spec.turn == 0 ? spec.length : sections[i + 1].cornerFloor ? spec.length - Half : spec.length + Half;
                        floorEndLevel = spec.dy;
                        var room = new GameObject(spec.name).transform;
                        room.SetParent(root, false);
                        int turnIn = first ? 0 : sections[i - 1].turn;
                        float top = Mathf.Max(absTop[i], first ? absTop[i] : absTop[i - 1], last ? absTop[i] : absTop[i + 1]);
                        float wallTop = (spec.ceiling > 0f ? origin.y + spec.ceiling + 1f : top + CliffAbove) - origin.y;
                        float wallBottom = Mathf.Min(absLowest[i], first ? absLowest[i] : absLowest[i - 1], last ? absLowest[i] : absLowest[i + 1]) - 30f - origin.y;
                        using (UseTheme(spec.theme))
                        {
                            Shell(room, spec, i, n, turnIn, wallTop, wallBottom);
                            spec.contents(room);
                        }
                        restyled += NatureRestyle.Apply(room, spec.theme);
                        room.SetPositionAndRotation(origin, Quaternion.Euler(0, yaw, 0));
                        var rotation = Quaternion.Euler(0, yaw, 0);
                        Route.Add(origin + rotation * new Vector3(0, 1f, spec.length * 0.25f));
                        Route.Add(origin + rotation * new Vector3(0, spec.dy + 1f, spec.length * 0.85f));
                        Outdoors.Add(spec.ceiling <= 0f);
                        Outdoors.Add(spec.ceiling <= 0f);
                        Footprints.Add(new Footprint { origin = origin, yaw = yaw, length = spec.length, floorY = origin.y + Mathf.Min(0f, spec.dy),
                                                       topY = origin.y + wallTop, act = spec.act });
                        origin += rotation * Vector3.forward * spec.length + Vector3.up * spec.dy;
                        yaw += 90f * spec.turn;
                    }
                    SafetyNet(root);
                }
            });
            CheckFootprints();
            section.gameObject.AddComponent<CourseRoute>().Configure(Route.ToArray(), Outdoors.ToArray());
            LookBuilder.ApplyToScene(scene);
            Physics.SyncTransforms();
            PatataWorksBuilder.ValidatePasses();
            var dressing = NatureDressing.Dress(section, GroupName, Footprints);
            Debug.Log($"[PatataWildsBuilder] {restyled} kit renderers redrawn in the nature look; {dressing}");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RegisterInMenu(SceneName, ScenePath, CheckpointCount, MenuSlot.First);
            AssetDatabase.SaveAssets();
            Debug.Log("[PatataWildsBuilder] PatataWilds built: five acts, twenty-five sections and campfires, dawn to dusk.");
        }

        /// <summary>No two sections more than one apart may overlap (searched once; checked on every build).</summary>
        static void CheckFootprints()
        {
            Rect R(Footprint f)
            {
                var rot = Quaternion.Euler(0, f.yaw, 0);
                var a = f.origin + rot * new Vector3(-Half, 0, -Half);
                var b = f.origin + rot * new Vector3(Half, 0, f.length + Half);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
            }
            for (int i = 0; i < Footprints.Count; i++)
                for (int j = i + 2; j < Footprints.Count; j++)
                {
                    var a = R(Footprints[i]);
                    var b = R(Footprints[j]);
                    if (a.xMin < b.xMax - 0.01f && b.xMin < a.xMax - 0.01f && a.yMin < b.yMax - 0.01f && b.yMin < a.yMax - 0.01f)
                        throw new InvalidOperationException($"PatataWilds sections {i + 1} and {j + 1} overlap");
                }
        }

        /// <summary>A last kill zone under the whole course: nothing can fall out of the world (a bomb thrown over a cliff explodes).</summary>
        static void SafetyNet(Transform root)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Collider>())
            {
                if (r.isTrigger) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            var go = Place(root, GameplayDir + "KillZone", "Safety net", new Vector3(bounds.center.x, bounds.min.y - 4f, bounds.center.z), Quaternion.identity);
            go.transform.localScale = new Vector3(bounds.size.x + 400f, 4f, bounds.size.z + 400f);
        }

        // ------------------------------------------------------------------ the shell: cliffs on both sides, a cave roof where asked

        static void Shell(Transform room, SectionSpec spec, int index, int count, int turnIn, float wallTop, float bottom)
        {
            float L = spec.length;
            bool first = index == 0, last = index == count - 1;
            var rng = new System.Random(500 + index);
            foreach (int side in new[] { -1, 1 })
            {
                float za = first ? -Half : turnIn == 0 ? 0 : side == turnIn ? Half : -Half;
                float zb = last ? L + Half : spec.turn == 0 ? L : side == spec.turn ? L - Half : L + Half;
                // the cliff in stretches of different heights (never lower than the section's own height), so its top line breaks up
                float z = za;
                while (z < zb - 0.01f)
                {
                    float stretch = Mathf.Min(zb - z, 7f + (float)rng.NextDouble() * 9f);
                    if (zb - (z + stretch) < 4f) stretch = zb - z;
                    float stretchTop = spec.ceiling > 0f ? wallTop : wallTop + (float)rng.NextDouble() * 4.5f;
                    Block(room, "Cliff", new Vector3(side * (Half + 0.5f), (bottom + stretchTop) / 2, z + stretch / 2), new Vector3(1, stretchTop - bottom, stretch), KitRole.Wall);
                    z += stretch;
                }
            }
            if (first) Block(room, "Cliff end", new Vector3(0, (bottom + wallTop) / 2, -Half - 0.5f), new Vector3(2 * Half + 2, wallTop - bottom, 1), KitRole.Wall);
            if (last) Block(room, "Cliff end", new Vector3(0, (bottom + wallTop) / 2, L + Half + 0.5f), new Vector3(2 * Half + 2, wallTop - bottom, 1), KitRole.Wall);
            if (spec.ceiling > 0f)
            {
                float ca = first ? -Half : turnIn == 0 ? 0 : -Half;
                float cb = last ? L + Half : spec.turn == 0 ? L : L - Half;
                Block(room, "Cave roof", new Vector3(0, spec.ceiling + 1.5f, (ca + cb) / 2), new Vector3(2 * Half, 3, cb - ca), KitRole.Ceiling).AddComponent<CourseCeiling>();
            }
            // A solid foundation under the section's floors, down to the cliffs' foot: a gorge shows its bottom 20 m down, water its bed
            // 3 m down, any other section hides it just under its floors.
            float top = Mathf.Min(0, spec.dy) - (spec.water ? -WaterBed : spec.pit ? 20f : 1.05f);
            var foundation = Block(room, spec.water ? "River bed" : spec.pit ? "Gorge floor" : "Foundation", new Vector3(0, (top + bottom) / 2, (floorStart + floorEnd) / 2),
                                   new Vector3(2 * Half, top - bottom, floorEnd - floorStart), KitRole.Floor);
            if (spec.water) CourseKit.Skin(foundation.transform.Find("Visual").gameObject, KitShape.RoughBox, KitColor.Neutral, NatureMaterialBuilder.Load("Nature_Riverbed"));
        }

        // ------------------------------------------------------------------ helpers (as PatataWorks and the plant)

        static void Floor(Transform p, string name, float a, float b, float y = 0, float width = 2 * Half, float x = 0, KitRole role = KitRole.Floor)
        {
            if (Mathf.Abs(y) < 0.01f) a = Mathf.Max(a, floorStart);
            if (Mathf.Abs(y - floorEndLevel) < 0.01f) b = Mathf.Min(b, floorEnd);
            if (b - a < 0.01f) return;
            Block(p, name, new Vector3(x, y - 0.5f, (a + b) / 2), new Vector3(width, 1, b - a), role);
        }

        /// <summary>A solid step of earth and rock from the foundation up to <paramref name="top"/> (terraces, ledges).</summary>
        static void Step(Transform p, string name, float a, float b, float top, float width = 2 * Half, float x = 0)
        {
            if (b - a < 0.01f) return;
            const float foot = -1.05f;
            Block(p, name, new Vector3(x, (top + foot) / 2, (a + b) / 2), new Vector3(width, top - foot, b - a), KitRole.Floor);
        }

        static void Ramp(Transform p, string name, Vector3 a, Vector3 b, float width, bool slide = false)
        {
            var rotation = Quaternion.LookRotation(b - a, Vector3.up);
            Block(p, name, (a + b) / 2 - rotation * Vector3.up * 0.5f, new Vector3(width, 1, Vector3.Distance(a, b)), slide ? KitRole.Slide : KitRole.Stairs, rotation, slide);
        }

        static void Pass(Transform p, string name, Vector3 from, Vector3 to, PassCorridor.ArcKind kind = PassCorridor.ArcKind.Normal,
                         bool timed = false, float opening = 0, float flight = 0)
        {
            var go = new GameObject("Pass " + name);
            go.transform.SetParent(p, false);
            go.transform.position = from;
            go.AddComponent<PassCorridor>().Configure(to, kind, flight, timed, opening);
        }

        /// <summary>A checkpoint (one per section) and its campfire beside it, 3.8 m from the trigger's centre, never on a spawn.</summary>
        static void CP(Transform p, int id, Vector3 position, float yaw = 0, float fireSide = 1f, Vector3? fireOffset = null)
        {
            var go = AddCheckpoint(p, $"CP_{id:00}", id, position, FuseFor(id));
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var offset = fireOffset ?? Quaternion.Euler(0, yaw, 0) * new Vector3(3.8f * fireSide, 0, 0);
            DressCheckpoint(p, go.GetComponent<Checkpoint>(), position + offset);
        }

        /// <summary>No square on the ground (spec §19): the pad is switched off, the campfire marks the checkpoint.</summary>
        static void DressCheckpoint(Transform p, Checkpoint checkpoint, Vector3 firePosition)
        {
            var so = new SerializedObject(checkpoint);
            var pad = so.FindProperty("padRenderer").objectReferenceValue as Renderer;
            if (pad != null)
            {
                pad.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(pad.gameObject);
            }
            so.FindProperty("padRenderer").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            NatureKit.Campfire(p, firePosition, checkpoint);
        }

        static void Kill(Transform p, Vector3 position, Vector3 size)
        {
            var go = Place(p, GameplayDir + "KillZone", "Gorge recovery", position, Quaternion.identity);
            go.transform.localScale = size;
        }

        /// <summary>A palisade across the section with a doorway, its log gate opened by <paramref name="source"/> (§13.15).</summary>
        static void Palisade(Transform p, string name, float z, float y, MonoBehaviour source)
        {
            Wall(p, name + " palisade", z, y, 9, -12, 12, new[] { new Hole(-3, 3, 0, 5) });
            // a palisade is upright logs, not rock: the wall pieces are redrawn (their colliders are unchanged)
            foreach (Transform child in p)
                if (child.name.StartsWith(name + " palisade_"))
                    CourseKit.Skin(child.Find("Visual").gameObject, KitShape.Logs, KitColor.Neutral, NatureMaterialBuilder.Load("Nature_BarkDark"));
            Actuator(p, Door, name, new Vector3(0, y + 2.5f, z), new Vector3(6, 5, 0.8f), new Vector3(0, 5.5f, 0), source);
        }

        static void TransitPasses(Transform parent, GameObject go)
        {
            var transit = go.GetComponent<BombTransit>();
            for (int i = 0; i < transit.ExitCount; i++)
            {
                var e = transit.GetExit(i);
                if (!e.pad.gameObject.activeInHierarchy) continue;
                Pass(parent, go.name + " exit " + i, e.muzzle.position, transit.AimPoint(i), PassCorridor.ArcKind.Fixed, flight: e.flightTime);
            }
        }

        /// <summary>A world ambience loop, placed when the section is dressed (positions in the section's space, converted later).</summary>
        static void Sound(Transform p, Vector3 local, NatureAudioFactory.Ambience kind)
        {
            var marker = new GameObject("Ambience " + kind);
            marker.transform.SetParent(p, false);
            marker.transform.localPosition = local;
            NatureKit.Ambient(marker.transform, kind.ToString(), Vector3.zero, kind,
                kind == NatureAudioFactory.Ambience.Waterfall ? 1f : 0.7f,
                kind == NatureAudioFactory.Ambience.Waterfall ? 70f : kind == NatureAudioFactory.Ambience.River ? 32f : 45f);
        }

        /// <summary>The act's forest sound for a section: birds from dawn to sunset, crickets at dusk, wind on the summit.</summary>
        static void ActSound(Transform p, float length)
        {
            var kind = currentAct >= 5 ? NatureAudioFactory.Ambience.Crickets : NatureAudioFactory.Ambience.Birds;
            Sound(p, new Vector3(0, 6, length * 0.5f), kind);
            if (currentAct >= 5) Sound(p, new Vector3(0, 10, length * 0.2f), NatureAudioFactory.Ambience.Wind);
        }
    }
}
