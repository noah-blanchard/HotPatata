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
    /// PatataRanger (PROJECT_SPEC §15f, ARCHITECTURE §4 and §25.5, M17): an old forest ranger post and its network in the trees, climbed
    /// from the valley floor to a lookout in the crowns, for two players (a third and a fourth relay). Six acts, one checkpoint each,
    /// every act a chain of trials under one contract (<see cref="SectionContract"/>, §13.20). The land comes first
    /// (<c>.Forest.cs</c>: the valley, the ravine, four giant trees), the timber is hung on it (<see cref="TimberKit"/>: decks round
    /// the trunks, stairs, rope bridges, rails, each walked on through its smoothed proxy), and the trials use the kit drawn in the
    /// ranger's own look. Never edit the scene by hand: change this builder and rebuild (menu HotPatata/Course/Build PatataRanger).
    /// </summary>
    public static partial class PatataRangerBuilder
    {
        public const string ScenePath = "Assets/Scenes/PatataRanger.unity";
        public const string SceneName = "PatataRanger";
        const string GroupName = "PatataRanger";
        public const string GeneratedDir = "Assets/Art/Generated/PatataRanger/";
        public const int CheckpointCount = 6;
        public const int Players = 2;
        const float Rail = TimberKit.RailHeight;

        /// <summary>The shorter hold fuse (spec §20): 5 s from checkpoint 3, 4.5 s from checkpoint 5 (every checkpoint states it).</summary>
        public static float FuseFor(int id) => id >= 5 ? 4.5f : id >= 3 ? 5f : 0f;

        /// <summary>The acts in order: name, ambience, how to build them.</summary>
        static (string name, string environment, Action<Transform> build)[] Acts() => new (string, string, Action<Transform>)[]
        {
            ("A Le Camp", "Forest", ActCamp),
            ("B Le Ravin", "River", ActRavine),
            ("C Le Pied du Vieux Chene", "Forest", ActOak),
            ("D Les Passerelles", "Canopy", ActWalkways),
            ("E Le Poste de Treuil", "Canopy", ActWinchHouse),
            ("F La Canopee", "Summit", ActCanopy),
        };

        static readonly List<Vector3> Route = new List<Vector3>();
        static readonly List<bool> Outdoors = new List<bool>();

        [MenuItem("HotPatata/Course/Build PatataRanger")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            NatureMaterialBuilder.Ensure(false);
            NatureAudioFactory.EnsureClips();
            NatureKit.BuildCampfirePrefab();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlateHourglass + ".prefab") == null) BuildTrioKitOnly();
            var scene = PrepareCourseScene(ScenePath, out var section);
            foreach (var root in scene.GetRootGameObjects().Where(g => g.name == "PlantExposureVolume").ToArray()) Object.DestroyImmediate(root);
            Route.Clear();
            Outdoors.Clear();
            Walks.Clear();
            OrganicAssets.Begin(GeneratedDir + "Meshes/");   // meshes only: the cleanup never reaches the mist floor or the foliage set
            var acts = Acts();
            string forestReport = "", dressing = "";
            var walkProblems = new List<string>();
            RebuildGroup(section, GroupName, group =>
            {
                using (UseLookSet(LookSet.Nature))
                using (UseTheme(new SurfaceTheme(NatureDir + "Nature_RockFace", NatureDir + "Nature_Cliff", NatureDir + "Nature_RockFace")))
                {
                    var forest = new GameObject("Forest").transform;
                    forest.SetParent(group, false);
                    // the land first: the trail the terrain draws, the giants, the ground, the stream
                    Trail.Clear();
                    Trail.AddRange(TrailPoints());
                    GrowGiants(forest);
                    forestReport = Terrain(forest);
                    Stream(forest);
                    Physics.SyncTransforms();
                    // then the post, act by act
                    foreach (var (name, _, build) in acts)
                    {
                        var act = new GameObject(name).transform;
                        act.SetParent(group, false);
                        build(act);
                        NatureRestyle.Apply(act, CurrentTheme);
                        Physics.SyncTransforms();
                    }
                    GroundKill(group);
                    SafetyNet(group);
                    Physics.SyncTransforms();
                    walkProblems = ProbeWalks();
                    dressing = Dressing(forest);
                }
            });
            int stale = OrganicAssets.End();
            section.gameObject.AddComponent<CourseRoute>().Configure(Route.ToArray(), Outdoors.ToArray());
            var previousWalks = section.GetComponent<RouteWalks>();
            if (previousWalks != null) Object.DestroyImmediate(previousWalks);
            section.gameObject.AddComponent<RouteWalks>().Configure(Walks.Select(w => new RouteWalks.Line { name = w.name, points = Curves.Resample(w.path, 0.5f).ToArray() }).ToList());
            WriteCourseAudio(section, Enumerable.Range(1, acts.Length).ToArray(), acts.Select(a => a.environment).ToArray(), "Forest");
            LookBuilder.ApplyToScene(scene);
            Physics.SyncTransforms();
            ValidatePasses();
            var findings = CourseContractCheck.Problems(scene, CourseContractCheck.Tuning);
            foreach (var f in findings) Debug.LogWarning("[PatataRangerBuilder] contract: " + f);
            foreach (var w in walkProblems) Debug.LogWarning("[PatataRangerBuilder] walk: " + w);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RegisterInMenu(SceneName, ScenePath, CheckpointCount, MenuSlot.First, Players);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PatataRangerBuilder] PatataRanger built: six acts for two, {forestReport}, {dressing}, {stale} stale mesh(es) removed, " +
                      $"{findings.Count} contract finding(s), {walkProblems.Count} walk problem(s).");
        }

        // ------------------------------------------------------------------ review views (the same shots before and after a change)

        /// <summary>The fixed views of the course reviews: player eye (1.7 m) on the route, then views from the side, below and the air.</summary>
        public static CourseShots.View[] ReviewViews => new[]
        {
            new CourseShots.View("A1 camp start", new Vector3(0f, 1.7f, 2.5f), new Vector3(1f, 2.5f, 30f), time: Day(0)),
            new CourseShots.View("A2 yard gate", new Vector3(2.5f, 1.7f, 27f), new Vector3(GateX, 3.5f, RidgeZ), time: Day(0)),
            new CourseShots.View("B1 south rim", new Vector3(3f, 3.7f, 60f), new Vector3(BridgeX, 1f, 80f), time: Day(1)),
            new CourseShots.View("B2 south stair", new Vector3(31.5f, 3.8f, 64.5f), new Vector3(19f, -4f, 76f), time: Day(1)),
            new CourseShots.View("B3 footbridge", new Vector3(16.6f, LedgeY + 1.7f, WallS(16f) + 1.8f), new Vector3(16f, LedgeY + 0.5f, WallN(16f)), time: Day(1)),
            new CourseShots.View("B4 cage from the winch", new Vector3(CageX, RimY + 1.7f, WallS(CageX) - 6f), new Vector3(CageX, LedgeY + 2f, WallN(CageX)), time: Day(1)),
            new CourseShots.View("B5 drawbridge", new Vector3(BridgeX + 1f, 4f, 66f), new Vector3(BridgeX, 6f, 87f), time: Day(1)),
            new CourseShots.View("B6 under the ledges", new Vector3(8f, StreamY + 1.2f, 80f), new Vector3(16f, LedgeY, 80f), time: Day(1)),
            new CourseShots.View("C1 oak from the clearing", new Vector3(1f, 7.7f, 105f), new Vector3(8f, 15f, 123f), time: Day(2)),
            new CourseShots.View("C2 lower terrace", Around(OldOak, HeroFeet[0], T1Y + 1.7f, 255f, 5.8f), Around(OldOak, HeroFeet[0], T1Y + 1f, 0f, 6f), time: Day(2)),
            new CourseShots.View("C3 cages from below", Around(OldOak, HeroFeet[0], T1Y + 1.7f, -5f, 6f), Around(OldOak, HeroFeet[0], T2Y + 2f, 0f, 6f), time: Day(2)),
            new CourseShots.View("C4 hoops from the thrower", Around(OldOak, HeroFeet[0], T2Y + 1.7f, 206f, 4.2f), Around(OldOak, HeroFeet[0], T2Y + 1.6f, 250f, 15f), time: Day(2)),
            new CourseShots.View("C5 clue from the balcony", Around(OldOak, HeroFeet[0], T2Y + 1.7f, 249f, 16.2f), Around(OldOak, HeroFeet[0], T2Y + 1.5f, 252f, 13.5f), time: Day(2)),
            new CourseShots.View("C6 bridges", Around(OldOak, HeroFeet[0], T2Y + 1.7f, 160f, 4f), Around(OldOak, HeroFeet[0], T2Y, 132f, 22f), time: Day(2)),
            new CourseShots.View("D1 the two walks", OwlLanding + new Vector3(1.5f, 1.7f, -1.5f), new Vector3(-16f, 20f, 150f), time: Day(3)),
            new CourseShots.View("D2 high gate from below", new Vector3(-17.2f, LowY + 1.7f, 140.6f), new Vector3(-10.8f, HighY + 1.5f, OwlLanding.z + 9.5f), time: Day(3)),
            new CourseShots.View("D3 crag lookout", new Vector3(-12f, HighY + 1.7f, 151.6f), new Vector3(-24f, 25f, 160f), time: Day(3)),
            new CourseShots.View("E1 porch and house", PorchCenter + new Vector3(5f, 1.7f, -3.5f), HouseCenter + new Vector3(-2f, 3f, 0f), time: Day(4)),
            new CourseShots.View("E2 larder and chutes", HouseCenter + new Vector3(-1.5f, 1.7f, -2.5f), HouseCenter + new Vector3(4.9f, 1.2f, 0f), time: Day(4)),
            new CourseShots.View("E3 upper floor", HouseCenter + new Vector3(0f, 5.5f + 1.7f, 2f), HouseCenter + new Vector3(4f, 5.5f + 0.5f, -2.5f), time: Day(4)),
            new CourseShots.View("F1 crown bridge", TerraceCenter + new Vector3(0.6f, 1.7f, 0.5f), TerraceCenter + new Vector3(20f, 1f, 18f), time: Day(5)),
            new CourseShots.View("F2 twin fir and tower", Around(TwinFir, HeroFeet[2], TwinY + 1.7f, 230f, 5f), Around(TwinFir, HeroFeet[2], TwinY, 128f, 20f), time: Day(5)),
            new CourseShots.View("F3 race to the lookout", Around(TwinFir, HeroFeet[2], TwinY + 1.7f, 128f, 22f), Around(LookoutFir, HeroFeet[3], LookoutY, 0f, 0f), time: Day(5)),
            new CourseShots.View("F4 lookout and bell", Around(LookoutFir, HeroFeet[3], LookoutY + 1.7f, 120f, 5f), Around(LookoutFir, HeroFeet[3], LookoutY + 1f, -6f, 8f), time: Day(5)),
            new CourseShots.View("Under the upper terrace", Around(OldOak, HeroFeet[0], 7.5f, 300f, 9f), Around(OldOak, HeroFeet[0], T2Y, 60f, 3f), time: Day(2)),
            new CourseShots.View("Air oak and walks", new Vector3(35f, 55f, 115f), new Vector3(-12f, 18f, 148f), 60f, time: Day(2)),
            new CourseShots.View("Air house and crowns", new Vector3(-70f, 70f, 150f), new Vector3(-15f, 32f, 190f), 60f, time: Day(4)),
            new CourseShots.View("Air canopy to lookout", new Vector3(45f, 80f, 180f), new Vector3(-5f, 38f, 225f), 60f, time: Day(5)),
            new CourseShots.View("Air camp to ravine", new Vector3(58f, 62f, 8f), new Vector3(4f, 0f, 70f), 60f, time: Day(0)),
            new CourseShots.View("Air whole course", new Vector3(150f, 170f, 40f), new Vector3(-5f, 15f, 160f), 55f, time: Day(3)),
            new CourseShots.View("Air from the west", new Vector3(-150f, 90f, 150f), new Vector3(0f, 20f, 160f), 55f, time: Day(3)),
        };

        /// <summary>The day's position (TimeOfDayBlender) while the section run from checkpoint <paramref name="id"/> is played.</summary>
        static float Day(int id) => TimeOfDayBlender.TargetFor(PatataRangerLook.CheckpointTimes(CheckpointCount), id);

        [MenuItem("HotPatata/Course/Review Shots PatataRanger")]
        public static void ReviewShots() => CourseShots.Shoot("Temp/RangerShots", "", ReviewViews);

        // ------------------------------------------------------------------ the land the timber stands on

        /// <summary>Where the timber of an act finds ground, trunks and anchors at height <paramref name="y"/>.</summary>
        static TimberKit.Site SiteAt(float y, float maxPost = 4f) => new TimberKit.Site
        {
            ground = p => GroundAt(p.x, p.y),
            trunks = TrunksAt(y),
            anchors = Anchors,
            maxPost = maxPost,
        };

        static readonly List<Vector3> Anchors = new List<Vector3>();

        /// <summary>The giants' trunks at height <paramref name="y"/> (those it is inside of).</summary>
        static List<TimberKit.Trunk> TrunksAt(float y)
        {
            var list = new List<TimberKit.Trunk>();
            foreach (var (tree, foot) in Giants())
                if (y > foot.y && y < foot.y + tree.spec.height * 0.85f) list.Add(TrunkAt(tree, foot, y));
            return list;
        }

        static IEnumerable<(NatureTreeBuilder.HeroTree tree, Vector3 foot)> Giants()
        {
            if (OldOak != null) yield return (OldOak, HeroFeet[0]);
            if (LeaningPine != null) yield return (LeaningPine, HeroFeet[1]);
            if (TwinFir != null) yield return (TwinFir, HeroFeet[2]);
            if (LookoutFir != null) yield return (LookoutFir, HeroFeet[3]);
            if (Snag != null) yield return (Snag, HeroFeet[4]);
        }

        /// <summary>A point on the ground (its height from the terrain), a little above it.</summary>
        static Vector3 G(float x, float z, float above = 0f) => new Vector3(x, GroundAt(x, z) + above, z);

        // ------------------------------------------------------------------ outlines

        /// <summary>
        /// A soft outline round <paramref name="c"/>: an ellipse (<paramref name="rx"/> × <paramref name="rz"/>, turned by
        /// <paramref name="yaw"/>) whose edge swells and dips by <paramref name="wobble"/> (two slow waves: never a regular disc).
        /// </summary>
        static List<Vector2> Blob(Vector2 c, float rx, float rz, float wobble, int seed, float yaw = 0f, int samples = 36)
        {
            float s = seed * 1.7f;
            var rot = Quaternion.Euler(0f, 0f, yaw);
            var p = new List<Vector2>();
            for (int i = 0; i < samples; i++)
            {
                float a = i / (float)samples * Mathf.PI * 2f;
                float k = 1f + wobble * (0.6f * Mathf.Sin(2f * a + s) + 0.4f * Mathf.Sin(3f * a + 2f * s));
                var local = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rz) * k;
                p.Add(c + (Vector2)(rot * local));
            }
            return p;
        }

        /// <summary>Cuts a polygon by a line: keeps the side opposite <paramref name="normal"/> (Sutherland–Hodgman, one plane).</summary>
        static List<Vector2> Clip(IList<Vector2> poly, Vector2 point, Vector2 normal)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                float da = Vector2.Dot(a - point, normal), db = Vector2.Dot(b - point, normal);
                if (da <= 0f) result.Add(a);
                if ((da <= 0f) != (db <= 0f)) result.Add(Vector2.Lerp(a, b, da / (da - db)));
            }
            return result;
        }

        static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);
        static Vector3 V3(Vector2 v, float y) => new Vector3(v.x, y, v.y);

        // ------------------------------------------------------------------ timber pieces

        /// <summary>A deck inside <paramref name="outline"/>, its surface level at <paramref name="y"/>.</summary>
        static GameObject Deck(Transform p, string name, IList<Vector2> outline, float y, float grain, bool rails = true, float maxPost = 4f,
                               params TimberKit.Opening[] openings) =>
            TimberKit.Deck(p, name, outline, _ => y, new TimberKit.DeckSpec { grain = grain, rails = rails, openings = openings }, SiteAt(y, maxPost));

        /// <summary>A floor held by walls (a house's storeys): boards, joists and bearers, no supports of its own.</summary>
        static GameObject Floor(Transform p, string name, IList<Vector2> outline, float y, float grain, bool rails = false, params TimberKit.Opening[] openings) =>
            TimberKit.Deck(p, name, outline, _ => y, new TimberKit.DeckSpec { grain = grain, rails = rails, openings = openings, held = true }, SiteAt(y));

        /// <summary>A walkway along a spline through <paramref name="control"/> (its walking surface), stairs where it climbs.</summary>
        static GameObject Walk(Transform p, string name, IList<Vector3> control, float width, bool railLeft, bool railRight, bool onGround = false, float maxPost = 4f,
                               bool held = false)
        {
            var path = Curves.Spline(control, 0.4f);
            float y = control.Average(c => c.y);
            var go = TimberKit.Walkway(p, name, path, width, new TimberKit.WalkSpec { railLeft = railLeft, railRight = railRight, onGround = onGround, held = held },
                                       SiteAt(y, maxPost));
            Walks.Add((name, new List<Vector3>(TimberKit.LastWalkSurface), width));
            return go;
        }

        static GameObject RopeSpan(Transform p, string name, Vector3 a, Vector3 b, float width = 1.6f, float sag = -1f)
        {
            if (sag < 0f) sag = Mathf.Min(0.06f * Vector3.Distance(a, b), 1.2f);
            Walks.Add((name, Curves.Sag(a, b, sag, 24), width));
            return TimberKit.RopeBridge(p, name, a, b, width, sag, new TimberKit.BridgeSpec { sideWalls = true });
        }

        /// <summary>A timber frame or any hand-built piece (renderers only): <paramref name="build"/> fills its mesh.</summary>
        static GameObject Timber(Transform p, string name, Action<MeshBuilder> build)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            var mb = TimberKit.Visual();
            build(mb);
            TimberKit.AddVisual(go, name, mb);
            go.AddComponent<CourseDecoration>();
            return go;
        }

        /// <summary>A solid box of timber that collides (a wall, a post a player must not walk through), drawn with the timber kit.</summary>
        static GameObject SolidTimber(Transform p, string name, Vector3 center, Quaternion rotation, Vector3 size, int slot = TimberKit.Timber)
        {
            var go = new GameObject(name) { layer = Layer("Environment") };
            go.transform.SetParent(p, false);
            go.transform.SetPositionAndRotation(center, rotation);
            go.AddComponent<BoxCollider>().size = size;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var mb = TimberKit.Visual();
            mb.Box(go.transform.position, go.transform.rotation, size, slot);
            TimberKit.AddVisual(visual, name + " box", mb);
            return go;
        }

        // ------------------------------------------------------------------ trials (the kit, wired)

        static PressurePlate HourglassPlate(Transform p, string name, Vector3 at, float seconds)
        {
            var plate = Place(p, PlateHourglass, name, at, Quaternion.identity).GetComponent<PressurePlate>();
            SetField(plate, "memorySeconds", v => v.floatValue = seconds);
            return plate;
        }

        static PressurePlate HandsFreePlate(Transform p, string name, Vector3 at) =>
            Place(p, PlateHandsFree, name, at, Quaternion.identity).GetComponent<PressurePlate>();

        static PressurePlate PlainPlate(Transform p, string name, Vector3 at) =>
            Place(p, Plate, name, at, Quaternion.identity).GetComponent<PressurePlate>();

        /// <summary>
        /// A drawbridge hinged at <paramref name="hinge"/> (its walking surface there), lying toward <paramref name="tip"/> when its source
        /// is active (pitched to meet the tip's height), standing <paramref name="raise"/> degrees up when not.
        /// </summary>
        static GameObject Drawbridge(Transform p, string name, Vector3 hinge, Vector3 tip, float width, MonoBehaviour source, float raise = 78f, float seconds = 2.2f)
        {
            var flat = tip - hinge;
            flat.y = 0f;
            float length = Mathf.Sqrt(flat.sqrMagnitude + Sq(tip.y - hinge.y));
            float pitch = Mathf.Atan2(hinge.y - tip.y, flat.magnitude) * Mathf.Rad2Deg;   // positive: the tip lower than the hinge
            const float Thick = 0.45f;
            var go = Place(p, Bridge, name, hinge, Quaternion.LookRotation(flat.normalized));
            ResizeActuator(go, new Vector3(width, Thick, length), Vector3.zero);
            var a = go.GetComponent<SignalActuator>();
            foreach (Transform part in a.Platform) part.localPosition += new Vector3(0f, -Thick / 2f, length / 2f);   // the hinge is the pivot
            a.WaypointClosed.localRotation = Quaternion.Euler(-raise, 0f, 0f);
            a.WaypointOpen.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            a.Platform.localRotation = a.WaypointClosed.localRotation;
            SetField(a, "rotateWithWaypoints", v => v.boolValue = true);
            SetField(a, "travelSeconds", v => v.floatValue = seconds);
            Wire(go, source);
            return go;
        }

        /// <summary>A gangway that slides out from under <paramref name="from"/> to reach <paramref name="to"/> while its source is active.</summary>
        static GameObject SlideBridge(Transform p, string name, Vector3 from, Vector3 to, float width, MonoBehaviour source, float seconds = 1.6f)
        {
            var flat = to - from;
            flat.y = 0f;
            float length = flat.magnitude;
            var dir = flat.normalized;
            const float Thick = 0.4f;
            var closed = from - dir * (length * 0.5f - 0.6f) + Vector3.down * (Thick / 2f + 0.55f);
            var go = Place(p, Bridge, name, closed, Quaternion.LookRotation(dir));
            ResizeActuator(go, new Vector3(width, Thick, length), Vector3.zero);
            var a = go.GetComponent<SignalActuator>();
            var open = (from + to) * 0.5f + Vector3.down * (Thick / 2f);
            a.WaypointOpen.localPosition = go.transform.InverseTransformPoint(open);
            SetField(a, "travelSeconds", v => v.floatValue = seconds);
            Wire(go, source);
            return go;
        }

        /// <summary>A cage lift: its floor at <paramref name="floor"/> (walking surface), rising <paramref name="rise"/> while its source is active.</summary>
        static GameObject Cage(Transform p, string name, Vector3 floor, Vector2 size, float yaw, float rise, MonoBehaviour source, float seconds = 3f)
        {
            var go = Actuator(p, Lift, name, floor + Vector3.down * 0.25f, new Vector3(size.x, 0.5f, size.y), new Vector3(0f, rise, 0f), source);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            SetField(go.GetComponent<SignalActuator>(), "travelSeconds", v => v.floatValue = seconds);
            return go;
        }

        /// <summary>A hoop the bomb is thrown through (a signal ring), facing along <paramref name="facing"/>, held for <paramref name="hold"/> seconds.</summary>
        static BombGate Hoop(Transform p, string name, Vector3 center, Vector3 facing, float hold)
        {
            facing.y = 0f;
            var gate = Place(p, GateRing, name, center, Quaternion.LookRotation(facing.normalized)).GetComponent<BombGate>();
            SetField(gate, "holdSeconds", v => v.floatValue = hold);
            return gate;
        }

        /// <summary>A checkpoint and its fire beside it.</summary>
        static Checkpoint CP(Transform p, int id, Vector3 position, Vector3 fireOffset)
        {
            var cp = AddCheckpoint(p, $"CP_{id:00}", id, position, FuseFor(id)).GetComponent<Checkpoint>();
            NatureKit.CampfireCheckpoint(p, cp, position + fireOffset);
            return cp;
        }

        /// <summary>
        /// A rotten plank (a falling platform): its top at <paramref name="top"/>, <paramref name="width"/> across and
        /// <paramref name="length"/> along <paramref name="yaw"/>; it gives way under the first who steps on it.
        /// </summary>
        static GameObject RottenPlank(Transform p, string name, Vector3 top, float width, float length, float yaw)
        {
            const float Thick = 0.3f;
            var go = Place(p, PlatformsDir + "Platform_Falling", name, top + Vector3.down * (Thick / 2f), Quaternion.Euler(0f, yaw, 0f));
            var body = go.transform.Find("Body");
            body.Find("Visual").localScale = new Vector3(width, Thick, length);
            body.Find("Collision").GetComponent<BoxCollider>().size = new Vector3(width, Thick, length);
            var trigger = go.transform.Find("Trigger");
            trigger.localPosition = new Vector3(0f, Thick / 2f + 0.5f, 0f);
            trigger.GetComponent<BoxCollider>().size = new Vector3(Mathf.Max(0.4f, width - 0.3f), 1f, Mathf.Max(0.4f, length - 0.3f));
            return go;
        }

        /// <summary>A gate across a walk (a door that rises while its source is active) and fixed wings 3 m out over the void each side.</summary>
        static GameObject WingedGate(Transform p, string name, Vector3 floor, Vector3 along, float walkWidth, MonoBehaviour source, float height = 4.2f)
        {
            along.y = 0f;
            var rot = Quaternion.LookRotation(along.normalized);
            var door = Actuator(p, Door, name, floor + Vector3.up * (height / 2f), new Vector3(walkWidth + 0.5f, height, 0.7f), new Vector3(0f, height + 0.4f, 0f), source);
            door.transform.rotation = rot;
            var side = rot * Vector3.right;
            foreach (float s in new[] { -1f, 1f })
                SolidTimber(p, name + " wing", floor + side * s * (walkWidth / 2f + 0.25f + 1.6f) + Vector3.up * (height / 2f - 1.2f), rot,
                            new Vector3(3.2f, height + 2.4f, 0.5f), TimberKit.PlankAged);
            Timber(p, name + " frame", mb =>
            {
                foreach (float s in new[] { -1f, 1f })
                    mb.Beam(floor + side * s * (walkWidth / 2f + 0.35f) + Vector3.down * 1.2f, floor + side * s * (walkWidth / 2f + 0.35f) + Vector3.up * (2f * height + 0.8f), 0.26f, 0.26f, TimberKit.Timber);
                mb.Beam(floor - side * (walkWidth / 2f + 0.6f) + Vector3.up * (2f * height + 0.6f), floor + side * (walkWidth / 2f + 0.6f) + Vector3.up * (2f * height + 0.6f), 0.26f, 0.3f, TimberKit.Timber);
            });
            return door;
        }

        static void Kill(Transform p, string name, Vector3 center, Vector3 size)
        {
            var go = Place(p, GameplayDir + "KillZone", name, center, Quaternion.identity);
            go.transform.localScale = size;
        }

        /// <summary>Adds route points (for the course's audio and progress) along an act.</summary>
        static void RoutePoints(params Vector3[] points)
        {
            foreach (var pt in points)
            {
                Route.Add(pt + Vector3.up);
                Outdoors.Add(true);
            }
        }

        // ------------------------------------------------------------------ falls

        /// <summary>
        /// Where the forest floor is a fall (under the trees the post climbs, north of the old oak's clearing): kill volumes over the
        /// ground in tiles, each kept two metres under the lowest walkable timber above it.
        /// </summary>
        static void GroundKill(Transform group)
        {
            var root = new GameObject("Falls").transform;
            root.SetParent(group, false);
            const float Tile = 12f, StartZ = 126f, EndZ = 292f;
            int mask = LayerMask.GetMask("Environment");
            for (float x = -72f; x < 72f; x += Tile)
                for (float z = StartZ; z < EndZ; z += Tile)
                {
                    float groundMax = float.MinValue, groundMin = float.MaxValue, timberMin = float.MaxValue;
                    for (int i = 0; i <= 4; i++)
                        for (int j = 0; j <= 4; j++)
                        {
                            float sx = x + Tile * i / 4f, sz = z + Tile * j / 4f;
                            float g = GroundAt(sx, sz);
                            groundMax = Mathf.Max(groundMax, g);
                            groundMin = Mathf.Min(groundMin, g);
                            foreach (var hit in Physics.RaycastAll(new Vector3(sx, 200f, sz), Vector3.down, 400f, mask, QueryTriggerInteraction.Ignore))
                            {
                                var proxy = hit.collider.GetComponent<WalkableProxy>();
                                if (proxy != null && proxy.ProxyKind == WalkableProxy.Kind.Terrain) continue;
                                if (hit.point.y > g + 0.5f) timberMin = Mathf.Min(timberMin, hit.point.y);
                            }
                        }
                    if (groundMin > 30f) continue;   // the valley's walls
                    float top = Mathf.Min(groundMax + 1.2f, timberMin - 2f);
                    float bottom = groundMin - 4f;
                    if (top - bottom < 1f) continue;
                    Kill(root, $"Forest floor {x:F0} {z:F0}", new Vector3(x + Tile / 2f, (top + bottom) / 2f, z + Tile / 2f), new Vector3(Tile + 0.2f, top - bottom, Tile + 0.2f));
                }
        }

        /// <summary>A last kill zone under everything: nothing falls out of the world.</summary>
        static void SafetyNet(Transform group)
        {
            var go = Place(group, GameplayDir + "KillZone", "Safety net", new Vector3(0f, RavineFloor - 20f, 120f), Quaternion.identity);
            go.transform.localScale = new Vector3(600f, 4f, 700f);
        }
    }
}
