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
    /// PatataCanopy (PROJECT_SPEC Â§15d, ARCHITECTURE Â§4 and Â§25.3, M13): a course in the tree tops, drawn in PatataWilds' nature
    /// look, in five acts and twenty-five sections (about 2 km, one checkpoint per section). Unlike PatataWilds it is built on the
    /// void: decks, bridges and branches over a forest floor far below, so every walkable metre is deliberate. Every section keeps
    /// a contract (<see cref="SectionContract"/>, Â§13.20): what it forces and the shortcuts it locks, measured by
    /// <see cref="CourseContractCheck"/>. Puzzle sections stand under a leaf roof that every cross wall reaches, so the bomb can
    /// never be lobbed over a wall; bramble screens let only the bomb through (Â§13.18), laser curtains only the runners,
    /// hands-free plates need an empty-handed holder and switches turn curtains, hedges and spores on and off (Â§13.19).
    /// Sections are built locally along +Z (the floor of their start at y = 0, x across) and turned as units; the turns alternate
    /// so no two sections overlap, and each section ends on a 20 m junction deck the next one starts from. Never edit the scene by
    /// hand: change this builder and rebuild (menu HotPatata/Course/Build PatataCanopy).
    /// </summary>
    public static partial class PatataCanopyBuilder
    {
        public const string ScenePath = "Assets/Scenes/PatataCanopy.unity";
        public const string SceneName = "PatataCanopy";
        const string GroupName = "PatataCanopy";
        public const int CheckpointCount = 25;
        const float Hub = 10f;          // half a junction deck: sections turn on 20 m squares
        const float Roof = 7f;          // a covered section's leaf roof above its floor (every cross wall reaches it)
        const float CoverHalf = 14f;    // half the width of a leaf roof: over the decks and the void beside them
        const float KillDrop = 10f;     // a fall dies this far under a section's lowest floor
        const float Gap = 14f;          // a gap only a bridge or a mover crosses (a slide-jump with a mantle reaches 12.75 m)

        /// <summary>The shorter hold fuse (spec Â§20): 5 s from checkpoint 15 (act 4), 4.5 s from checkpoint 20 (act 5).</summary>
        public static float FuseFor(int id) => id >= 20 ? 4.5f : id >= 15 ? 5f : 0f;

        struct SectionSpec
        {
            public string name;
            public int act;
            public float length, dy;
            public int turn;                 // after this section: -1 left, 0 straight, +1 right
            public bool covered;             // a leaf roof Roof m above the floor over the whole section
            public bool outdoors;            // open to the sky (the route's outdoors flag)
            public Action<Transform> contents;
        }

        static SectionSpec S(string name, int act, float length, float dy, int turn, Action<Transform> contents, bool covered = false, bool roofed = false) =>
            new SectionSpec { name = name, act = act, length = length, dy = dy, turn = turn, covered = covered, outdoors = !covered && !roofed, contents = contents };

        static SurfaceTheme T(string floor, string wall, string ceiling) => new SurfaceTheme(NatureDir + floor, NatureDir + wall, NatureDir + ceiling);

        static readonly SurfaceTheme Crown = T("Nature_Planks", "Nature_BarkDark", "Nature_MossWood");
        static readonly SurfaceTheme Oak = T("Nature_MossWood", "Nature_BarkBrown", "Nature_BarkDark");

        // The turns alternate (left, then right), so the course is a staircase and no two sections meet but at their junction.
        static SectionSpec[] Sections() => new[]
        {
            // Act 1 - L'OrÃ©e (dawn): one system at a time
            S("01 Premier pont", 1, 60, 0, -1, PremierPont),
            S("02 Le Filet", 1, 84, 0, 0, LeFilet, covered: true),
            S("03 L'Anneau de lianes", 1, 64, 0, 1, AnneauDeLianes),
            S("04 La Plaque", 1, 72, 0, 0, LaPlaque),
            S("05 Les Lucioles", 1, 80, 0, -1, LesLucioles, covered: true),
            // Act 2 - Les Ponts suspendus (noon): combined
            S("06 Les Deux Branches", 2, 96, 0, 0, DeuxBranches, covered: true),
            S("07 Pont-levis croisÃ©", 2, 96, 0, 1, PontLevisCroise, covered: true),
            S("08 Branches balanÃ§oires", 2, 84, 0, 0, BranchesBalancoires),
            S("09 Branches pourries", 2, 72, 0, -1, BranchesPourries),
            S("10 L'Ã‰cluse", 2, 100, 0, 0, Ecluse, covered: true),
            // Act 3 - Le Grand ChÃªne (late afternoon): inside the hollow oak, upward
            S("11 La Poulie", 3, 50, 14, 1, LaPoulie, roofed: true),
            S("12 Le Tronc creux", 3, 64, 12, 0, TroncCreux, roofed: true),
            S("13 La Spirale", 3, 56, 7.5f, -1, LaSpirale, roofed: true),
            S("14 Les Galeries", 3, 96, 0, 0, LesGaleries, covered: true),
            S("15 Le CÅ“ur du chÃªne", 3, 64, 0, 1, CoeurDuChene, covered: true),
            // Act 4 - La Cime dans le vent (sunset, 5 s fuse): speed
            S("16 Feuilles-trampolines", 4, 64, 10, 0, FeuillesTrampolines),
            S("17 Branches mouvantes", 4, 90, 0, -1, BranchesMouvantes),
            S("18 Couloir de ronces", 4, 84, 0, 0, CouloirDeRonces, covered: true),
            S("19 Canon Ã  graines", 4, 110, 0, 1, CanonAGraines, covered: true),
            S("20 Course contre l'anneau", 4, 84, 4, 0, CourseContreLAnneau),
            // Act 5 - Le Sommet (dusk, 4.5 s fuse): twisted
            S("21 La Haie qui s'ouvre", 5, 84, 0, -1, HaieQuiSouvre, covered: true),
            S("22 Le Pont des spores", 5, 96, 0, 0, PontDesSpores, covered: true),
            S("23 Glissade de la grande branche", 5, 96, -24, 1, Glissade),
            S("24 L'Ã‰cluse finale", 5, 104, 10, 0, EcluseFinale, roofed: true),
            S("25 Arche du sommet", 5, 64, 0, 0, ArcheDuSommet),
        };

        /// <summary>One section's footprint in world XZ: origin, yaw, length, lowest floor.</summary>
        struct Footprint
        {
            public Vector3 origin;
            public float yaw, length, floorY;
            public float topY;               // its highest floor or roof (measured once built)
        }

        static readonly List<Footprint> Footprints = new List<Footprint>();
        static readonly List<Vector3> Route = new List<Vector3>();
        static readonly List<bool> Outdoors = new List<bool>();
        static readonly List<int> SectionActs = new List<int>();
        static int currentAct;
        static float killTop;            // the current section's kill plane (local): falls die here, retracted bridges hide under it

        [MenuItem("HotPatata/Course/Build PatataCanopy")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            NatureMaterialBuilder.Ensure(false);
            NatureAudioFactory.EnsureClips();
            NatureKit.BuildCampfirePrefab();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NatureTreeBuilder.PrefabPath(NatureTreeBuilder.Species.Broadleaf, 0)) == null) NatureTreeBuilder.BuildAll();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BombObstacleKitBuilder.Screen + ".prefab") == null) BombObstacleKitBuilder.BuildAll();
            var scene = PrepareCourseScene(ScenePath, out var section);
            // the course template is the lamp-lit plant: its extra exposure volume does not belong outdoors
            foreach (var root in scene.GetRootGameObjects().Where(g => g.name == "PlantExposureVolume").ToArray()) Object.DestroyImmediate(root);
            var sections = Sections();
            Route.Clear();
            Outdoors.Clear();
            Footprints.Clear();
            SectionActs.Clear();
            Supports.Clear();
            Roofs.Clear();
            var rooms = new List<Transform>();
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
                        currentAct = spec.act;
                        var theme = spec.act == 3 ? Oak : Crown;
                        var room = new GameObject(spec.name).transform;
                        room.SetParent(group, false);
                        rooms.Add(room);
                        float lowest = Mathf.Min(0f, spec.dy);
                        killTop = lowest - KillDrop;
                        using (UseTheme(theme))
                        {
                            if (i == 0) Deck(room, "Start deck", -Hub, Hub, -Hub, Hub, 0);
                            spec.contents(room);
                            if (i < sections.Length - 1) Deck(room, "Junction deck", -Hub, Hub, spec.length - Hub, spec.length + Hub, spec.dy);
                            if (spec.covered) LeafRoof(room, "Leaf roof", -CoverHalf, CoverHalf, -Hub, spec.length + Hub, Mathf.Max(0f, spec.dy) + Roof);
                            Kill(room, new Vector3(0, killTop - 1f, spec.length / 2f), new Vector3(2 * CoverHalf + 12f, 2f, spec.length + 2 * Hub + 8f));
                            Birds(room, spec);
                        }
                        restyled += NatureRestyle.Apply(room, theme);
                        var rotation = Quaternion.Euler(0, yaw, 0);
                        room.SetPositionAndRotation(origin, rotation);
                        Route.Add(origin + rotation * new Vector3(0, 1f, spec.length * 0.25f));
                        Route.Add(origin + rotation * new Vector3(0, spec.dy + 1f, spec.length * 0.85f));
                        Outdoors.Add(spec.outdoors);
                        Outdoors.Add(spec.outdoors);
                        SectionActs.Add(spec.act);
                        Footprints.Add(new Footprint { origin = origin, yaw = yaw, length = spec.length, floorY = origin.y + lowest });
                        origin += rotation * Vector3.forward * spec.length + Vector3.up * spec.dy;
                        yaw += 90f * spec.turn;
                    }
                    CheckFootprints();
                    Physics.SyncTransforms();
                    MeasureTops(rooms);
                    string dressing; using (UseTheme(Crown)) dressing = Forest(group);
                    dressing += ", " + Atmosphere(group);
                    SafetyNet(group);
                    Debug.Log("[PatataCanopyBuilder] " + dressing);
                }
            });
            section.gameObject.AddComponent<CourseRoute>().Configure(Route.ToArray(), Outdoors.ToArray());
            LookBuilder.ApplyToScene(scene);
            Physics.SyncTransforms();
            ValidatePasses();
            var findings = CourseContractCheck.Problems(scene, CourseContractCheck.Tuning);
            foreach (var f in findings) Debug.LogWarning("[PatataCanopyBuilder] contract: " + f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RegisterInMenu(SceneName, ScenePath, CheckpointCount, MenuSlot.Keep);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PatataCanopyBuilder] PatataCanopy built: five acts, twenty-five sections, {restyled} kit renderers in the nature look, " +
                      $"{findings.Count} contract finding(s).");
        }

        /// <summary>No two sections more than one apart overlap (their decks, roofs and the void beside them).</summary>
        static void CheckFootprints()
        {
            Rect R(Footprint f)
            {
                var rot = Quaternion.Euler(0, f.yaw, 0);
                var a = f.origin + rot * new Vector3(-CoverHalf, 0, -Hub);
                var b = f.origin + rot * new Vector3(CoverHalf, 0, f.length + Hub);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
            }
            for (int i = 0; i < Footprints.Count; i++)
                for (int j = i + 2; j < Footprints.Count; j++)
                    if (R(Footprints[i]).Overlaps(R(Footprints[j])))
                        throw new InvalidOperationException($"PatataCanopy sections {i + 1} and {j + 1} overlap");
        }

        /// <summary>A last kill zone under everything: nothing falls out of the world.</summary>
        static void SafetyNet(Transform group)
        {
            float y = ground.Lowest - 8f;   // under the forest floor's lowest point
            var go = Place(group, GameplayDir + "KillZone", "Safety net", new Vector3(0, y, 0), Quaternion.identity);
            var bounds = new Bounds(Footprints[0].origin, Vector3.zero);
            foreach (var f in Footprints) bounds.Encapsulate(f.origin);
            go.transform.position = new Vector3(bounds.center.x, y, bounds.center.z);
            go.transform.localScale = new Vector3(bounds.size.x + 400f, 4f, bounds.size.z + 400f);
        }

        // ------------------------------------------------------------------ pieces (section space: +Z forward, floor of the start at y = 0)

        /// <summary>A plank deck (top at <paramref name="y"/>), held up by trunks that run down to the forest floor (decoration): one, or one
        /// every ~22 m along a long deck.</summary>
        static GameObject Deck(Transform p, string name, float x0, float x1, float z0, float z1, float y, bool trunk = true)
        {
            var go = Block(p, name, new Vector3((x0 + x1) / 2, y - 0.5f, (z0 + z1) / 2), new Vector3(x1 - x0, 1, z1 - z0), KitRole.Grating);
            if (trunk && (x1 - x0) * (z1 - z0) >= 30f)
            {
                float thickness = Mathf.Clamp(Mathf.Min(x1 - x0, z1 - z0) * 0.3f, 1f, 3f);
                bool alongZ = z1 - z0 >= x1 - x0;
                float span = alongZ ? z1 - z0 : x1 - x0;
                int count = Mathf.Max(1, Mathf.RoundToInt(span / 22f));
                for (int i = 0; i < count; i++)
                {
                    float t = (i + 0.5f) / count;
                    var top = alongZ ? new Vector3((x0 + x1) / 2, y - 1f, Mathf.Lerp(z0, z1, t)) : new Vector3(Mathf.Lerp(x0, x1, t), y - 1f, (z0 + z1) / 2);
                    Support(p, top, thickness);
                }
            }
            return go;
        }

        /// <summary>A solid block of the tree from the deck below up to <paramref name="top"/> (a tier, a step): its face cannot be climbed when high.</summary>
        static GameObject Solid(Transform p, string name, float x0, float x1, float z0, float z1, float top, float bottom = -1f) =>
            Block(p, name, new Vector3((x0 + x1) / 2, (top + bottom) / 2, (z0 + z1) / 2), new Vector3(x1 - x0, top - bottom, z1 - z0), KitRole.Wall);

        /// <summary>A trunk under a deck, down to the forest floor (collider-free, instanced): grown once the floor is known (<see cref="Forest"/>).</summary>
        static void Support(Transform p, Vector3 top, float thickness) =>
            Supports.Add(new SupportRequest { parent = p, localTop = top, thickness = thickness });

        /// <summary>
        /// A leaf roof whose underside is at <paramref name="y"/> (a ceiling for the passes, CourseCeiling): a dense mat of leaves, solid to the
        /// eye from below, with tufts of leaves on top (<see cref="Forest"/>, never under its underside). It casts no shadow: the sun's
        /// leaf cookie dapples the decks.
        /// </summary>
        static GameObject LeafRoof(Transform p, string name, float x0, float x1, float z0, float z1, float y)
        {
            var go = Block(p, name, new Vector3((x0 + x1) / 2, y + 0.5f, (z0 + z1) / 2), new Vector3(x1 - x0, 1, z1 - z0), KitRole.Ceiling);
            // the opaque mat is the top 40 % of the box; the lower 60 cm hold the leaf cards seen from below (Forest)
            var visual = go.transform.Find("Visual");
            visual.localPosition = new Vector3(0f, 0.3f, 0f);
            visual.localScale = new Vector3(1f, 0.4f, 1f);
            CourseKit.Skin(visual.gameObject, KitShape.BevelBox, KitColor.Neutral, NatureMaterialBuilder.Load(NatureMaterialBuilder.LeafCanopyName));
            Roofs.Add(go.transform);
            go.AddComponent<CourseCeiling>();
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static void Ramp(Transform p, string name, Vector3 a, Vector3 b, float width, bool slide = false)
        {
            var rotation = Quaternion.LookRotation(b - a, Vector3.up);
            Block(p, name, (a + b) / 2 - rotation * Vector3.up * 0.5f, new Vector3(width, 1, Vector3.Distance(a, b)), slide ? KitRole.Slide : KitRole.Stairs, rotation, slide);
        }

        /// <summary>A bark wall across the section at <paramref name="z"/> (x0..x1), <paramref name="height"/> tall, with holes.</summary>
        static void WallAcross(Transform p, string name, float z, float y, float height, float x0, float x1, params Hole[] holes)
        {
            Wall(p, name, z, y, height, x0, x1, holes);
            foreach (Transform child in p)
                if (child.name.StartsWith(name + "_")) Logs(child.gameObject);
        }

        /// <summary>A wall piece drawn as upright logs (a treehouse wall, like PatataWilds' palisades); its collider is unchanged.</summary>
        static void Logs(GameObject piece) =>
            CourseKit.Skin(piece.transform.Find("Visual").gameObject, KitShape.Logs, KitColor.Neutral, NatureMaterialBuilder.Load("Nature_LogWall"));

        /// <summary>A bark wall along the section at <paramref name="x"/> (z0..z1), with holes (their X0/X1 are along z here).</summary>
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
                    Logs(Block(p, $"{name}_{++piece}", new Vector3(x, y + (y0 + y1) / 2f, mid), new Vector3(1f, y1 - y0, b - a), KitRole.Wall));
                }
                if (hole == null) Piece(0f, height);
                else { Piece(0f, hole.Value.Y0); Piece(hole.Value.Y1, height); }
            }
        }

        /// <summary>A bramble screen filling a hole across the section (x0..x1 at z), from <paramref name="y"/> up <paramref name="height"/>.</summary>
        static GameObject ScreenAcross(Transform p, string name, float x0, float x1, float z, float y, float height)
        {
            var go = Place(p, BombObstacleKitBuilder.Screen, name, new Vector3((x0 + x1) / 2, y, z), Quaternion.identity);
            ResizeScreen(go, x1 - x0 - 0.7f, height - 0.35f);
            return go;
        }

        /// <summary>A bramble screen filling a hole along the section (z0..z1 at x).</summary>
        static GameObject ScreenAlong(Transform p, string name, float z0, float z1, float x, float y, float height)
        {
            var go = Place(p, BombObstacleKitBuilder.Screen, name, new Vector3(x, y, (z0 + z1) / 2), Quaternion.Euler(0, 90, 0));
            ResizeScreen(go, z1 - z0 - 0.7f, height - 0.35f);
            return go;
        }

        /// <summary>A laser curtain filling a passage across the section (x0..x1 at z).</summary>
        static GameObject CurtainAcross(Transform p, string name, float x0, float x1, float z, float y, float height)
        {
            var go = Place(p, LaserCurtain, name, new Vector3((x0 + x1) / 2, y, z), Quaternion.identity);
            ResizeCurtain(go, x1 - x0 - 0.6f, height - 0.3f);
            return go;
        }

        static GameObject CurtainAlong(Transform p, string name, float z0, float z1, float x, float y, float height)
        {
            var go = Place(p, LaserCurtain, name, new Vector3(x, y, (z0 + z1) / 2), Quaternion.Euler(0, 90, 0));
            ResizeCurtain(go, z1 - z0 - 0.6f, height - 0.3f);
            return go;
        }

        /// <summary>A vine ring on its pillar on a deck, facing along <paramref name="alongX"/> (a throw across the section) or along z.</summary>
        static BombGate Ring(Transform p, string name, Vector3 floor, float height, float hold, bool alongX)
        {
            var gate = Gate(p, name, floor, height, hold);
            if (alongX) gate.transform.rotation = Quaternion.Euler(0, 90, 0);
            return gate;
        }

        /// <summary>A vine ring hanging where <paramref name="pass"/>'s intended arc is half way (over a gap), facing the pass, on a vine.</summary>
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
            NatureKit.Detail(p, name + " vine", center + Vector3.up * 4.5f, new Vector3(0.12f, 6f, 0.12f), KitRole.Frame);
            return gate;
        }

        /// <summary>A plank bridge that rises from under the kill plane to span a gap while its source is active.</summary>
        static GameObject RisingBridge(Transform p, string name, float x, float z0, float z1, float y, float width, MonoBehaviour source, float seconds = 1.5f)
        {
            float closed = killTop - 2f;
            float top = y - 0.25f;
            var go = Actuator(p, Bridge, name, new Vector3(x, closed, (z0 + z1) / 2), new Vector3(width, 0.5f, z1 - z0), new Vector3(0, top - closed, 0), source);
            SetField(go.GetComponent<SignalActuator>(), "travelSeconds", v => v.floatValue = seconds);
            return go;
        }

        static GameObject SwitchFor(Transform p, string name, Vector3 at, MonoBehaviour source, bool activeWhenOpen, params GameObject[] targets)
        {
            var go = Place(p, BombObstacleKitBuilder.Switch, name, at, Quaternion.identity);
            WireSwitch(go, source, activeWhenOpen, targets);
            return go;
        }

        static PressurePlate HandsFreePlate(Transform p, string name, Vector3 at) =>
            Place(p, PlateHandsFree, name, at, Quaternion.identity).GetComponent<PressurePlate>();

        /// <summary>A launch pad wrapped in a forbidden zone: the runners fly up, the bomb never does.</summary>
        static void SporePad(Transform p, string name, Vector3 at)
        {
            Place(p, ObstaclesDir + "LaunchPad", name, at, Quaternion.identity);
            Zone(p, ZoneForbidden, name + " spores", at, new Vector3(3.4f, 3f, 3.4f));
        }

        static PassCorridor Pass(Transform p, string name, Vector3 from, Vector3 to, PassCorridor.ArcKind kind = PassCorridor.ArcKind.Normal,
                                 bool timed = false, float opening = 0, float flight = 0)
        {
            var go = new GameObject("Pass " + name);
            go.transform.SetParent(p, false);
            go.transform.position = from;
            var pass = go.AddComponent<PassCorridor>();
            pass.Configure(to, kind, flight, timed, opening);
            return pass;
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

        /// <summary>The section's contract (Â§13.20): what it forces, the shortcuts it locks (world space at build time).</summary>
        static void Contract(Transform p, string force, params SectionContract.Shortcut[] locks)
        {
            var go = new GameObject("Contract");
            go.transform.SetParent(p, false);
            go.AddComponent<SectionContract>().Configure(force, locks);
        }

        static SectionContract.Shortcut GapLock(string name, Vector3 from, Vector3 to) => SectionContract.Lock(name, SectionContract.ShortcutKind.Gap, from, to);
        static SectionContract.Shortcut ClimbLock(string name, Vector3 from, Vector3 to) => SectionContract.Lock(name, SectionContract.ShortcutKind.Climb, from, to);
        static SectionContract.Shortcut LobLock(string name, Vector3 from, Vector3 wallTop) => SectionContract.Lock(name, SectionContract.ShortcutKind.Lob, from, wallTop);

        /// <summary>A checkpoint at the junction deck and its campfire beside it (never on a spawn, never on the way out), inside the checkpoint's zone.</summary>
        static void CP(Transform p, int id, Vector3 position, float fireSide = 1f)
        {
            var go = AddCheckpoint(p, $"CP_{id:00}", id, position, FuseFor(id));
            NatureKit.CampfireCheckpoint(p, go.GetComponent<Checkpoint>(), position + new Vector3(3.8f * fireSide, 0, 0));
        }

        static void Kill(Transform p, Vector3 center, Vector3 size)
        {
            var go = Place(p, GameplayDir + "KillZone", "Fall", center, Quaternion.identity);
            go.transform.localScale = size;
        }

        /// <summary>Torches along a covered deck's edge (warm light under the leaves).</summary>
        static void Torches(Transform p, float x, float z0, float z1, float y = 0f, float step = 18f)
        {
            for (float z = z0; z <= z1 + 0.01f; z += step) NatureKit.Torch(p, new Vector3(x, y, z), 7f, 13f);
        }

        /// <summary>The act's forest sound: birds from dawn to sunset, crickets and wind at dusk.</summary>
        static void Birds(Transform p, SectionSpec spec)
        {
            var kind = currentAct >= 5 ? NatureAudioFactory.Ambience.Crickets : NatureAudioFactory.Ambience.Birds;
            Sound(p, new Vector3(0, Mathf.Max(0, spec.dy) + 6, spec.length * 0.5f), kind);
            if (currentAct >= 4) Sound(p, new Vector3(0, Mathf.Max(0, spec.dy) + 10, spec.length * 0.2f), NatureAudioFactory.Ambience.Wind);
        }

        static void Sound(Transform p, Vector3 local, NatureAudioFactory.Ambience kind)
        {
            var marker = new GameObject("Ambience " + kind);
            marker.transform.SetParent(p, false);
            marker.transform.localPosition = local;
            NatureKit.Ambient(marker.transform, kind.ToString(), Vector3.zero, kind, 0.7f, 45f);
        }
    }
}
