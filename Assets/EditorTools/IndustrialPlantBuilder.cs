using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using static HotPatata.Editor.IndustrialKit;
using static HotPatata.Editor.BombObstacleKitBuilder;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// The industrial showcase course (ARCHITECTURE §25.2, issue #87): a full, closed factory of eleven rooms (about 680 m, nine
    /// checkpoints) with every obstacle of the kit, drawn in the industrial look only. Rooms are built locally along +Z and turned
    /// as units, as in PatataWorks: the route climbs (an atrium with a plate lift, a shaft of rising platforms and a cannon),
    /// descends (a mega slide), turns six times, crosses pits on catwalks, falling grates and a crane, and ends in a control
    /// room. The shell is solid (walls, roofs, no sun): the only light is warm lamps, so nothing depends on where the player stands.
    /// The obstacle layouts are PatataWorks' proven ones (pass lengths, jump distances); the arrangement, shell and look are new.
    /// </summary>
    public static class IndustrialPlantBuilder
    {
        public const string ScenePath = "Assets/Scenes/IndustrialPlant.unity";
        const string GroupName = "IndustrialPlant";
        const float Half = 12f;                  // half the width of a room (the floors are 24 m wide, as in PatataWorks)

        static readonly Color Warm = new Color(1f, 0.68f, 0.38f);
        static readonly Color Furnace = new Color(1f, 0.34f, 0.1f);
        static readonly Color Cold = new Color(0.55f, 0.75f, 1f);
        static readonly Color Sodium = new Color(1f, 0.55f, 0.2f);

        struct RoomSpec
        {
            public string name;
            public float length, dy, ceiling;
            public int turn;                      // after this room: -1 left, 0 straight, +1 right
            public bool pit;                      // a solid floor far below: the void of this room has a bottom
            public CourseKit.SurfaceTheme theme;
            public Color lamp;
            public float lampIntensity;
            public Action<Transform> contents;
            public bool cornerFloor;              // at a turn into this room, its floor covers the corner square (else the previous room's does)
        }

        // The stretch of floor the room being built owns, in its local z: floors at its start level are cut at the start, floors at its
        // end level at the end. Two rooms' floors never overlap at the same height, so no two materials fight over one surface.
        static float floorStart, floorEnd, floorEndLevel;

        static CourseKit.SurfaceTheme T(string floor, string wall, string ceiling) =>
            new CourseKit.SurfaceTheme(IndustrialDir + "Industrial_Floor_" + floor, IndustrialDir + "Industrial_Wall_" + wall, IndustrialDir + "Industrial_Ceiling_" + ceiling);

        static RoomSpec R(string name, float length, float dy, float ceiling, int turn, bool pit, CourseKit.SurfaceTheme theme, Color lamp, float lampIntensity,
                          Action<Transform> contents, bool cornerFloor = true) =>
            new RoomSpec { name = name, length = length, dy = dy, ceiling = ceiling, turn = turn, pit = pit, theme = theme, lamp = lamp, lampIntensity = lampIntensity,
                           contents = contents, cornerFloor = cornerFloor };

        // Straight joints keep the same absolute ceiling; the turn sequence was searched so no room overlaps another.
        static RoomSpec[] Rooms() => new[]
        {
            R("00 Gatehouse", 48, 0, 11, 0, false, T("Concrete", "Plaster", "Concrete"), Warm, 54f, Gatehouse),
            R("01 Sorting line", 96, 0, 11, 1, true, T("Plate", "Panel", "Panel"), Warm, 54f, SortingLine),
            R("02 Atrium", 64, 14, 26, 0, true, T("Concrete", "Brick", "Concrete"), Warm, 102f, Atrium),
            R("03 Void catwalks", 64, 4, 12, 1, true, T("Grit", "Block", "Panel"), Warm, 51f, VoidCatwalks),
            R("04 Cold storage", 108, 0, 11, 1, true, T("Tile", "Plaster", "Panel"), Cold, 44f, ColdStorage, cornerFloor: false),   // the catwalks' crane gap and exit stay
            R("05 Chute", 72, -28, 12, -1, true, T("Grit", "Block", "Concrete"), Sodium, 51f, Chute),
            R("06 Furnace intake", 48, 0, 11, 0, false, T("Grit", "Brick", "Concrete"), Furnace, 48f, FurnaceIntake),
            R("07 Furnace loop", 64, 0, 11, 1, false, T("Plate", "Brick", "Panel"), Furnace, 48f, FurnaceLoop),
            R("08 Boiler approach", 48, 0, 11, -1, false, T("Concrete", "Panel", "Panel"), Warm, 51f, BoilerApproach),
            R("09 Boiler shaft", 24, 34, 48, -1, true, T("Plate", "Block", "Concrete"), Warm, 102f, BoilerShaft),
            R("10 Control room", 40, 0, 14, 0, true, T("Tile", "Plaster", "Panel"), Warm, 58f, ControlRoom, cornerFloor: false)   // the shaft's top floors, lift wells and cannon gap stay
        };

        [MenuItem("HotPatata/Course/Build Industrial Plant")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            IndustrialMaterialBuilder.Ensure(false);
            var scene = IndustrialLabBuilder.PrepareScene(ScenePath, out var section);
            var rooms = Rooms();
            int restyled = 0;
            RebuildGroup(section, GroupName, root =>
            {
                using (UseLookSet(LookSet.Industrial))
                {
                    var origin = Vector3.zero;
                    float yaw = 0;
                    var absCeiling = new float[rooms.Length];
                    var absLowest = new float[rooms.Length];
                    var y = 0f;
                    for (int i = 0; i < rooms.Length; i++) { absCeiling[i] = y + rooms[i].ceiling; absLowest[i] = Mathf.Min(y, y + rooms[i].dy); y += rooms[i].dy; }
                    for (int i = 0; i < rooms.Length; i++)
                    {
                        var spec = rooms[i];
                        bool first = i == 0, last = i == rooms.Length - 1;
                        floorStart = first ? -Half : rooms[i - 1].turn == 0 ? 0 : spec.cornerFloor ? -Half : Half;
                        floorEnd = last ? spec.length + Half : spec.turn == 0 ? spec.length : rooms[i + 1].cornerFloor ? spec.length - Half : spec.length + Half;
                        floorEndLevel = spec.dy;
                        var room = new GameObject(spec.name).transform;
                        room.SetParent(root, false);
                        int turnIn = i == 0 ? 0 : rooms[i - 1].turn;
                        float wallTop = Mathf.Max(absCeiling[i], i > 0 ? absCeiling[i - 1] : absCeiling[i], i + 1 < rooms.Length ? absCeiling[i + 1] : absCeiling[i]) + 1f - origin.y;
                        // Walls reach down to the lowest floor of this room and of both neighbours: a corner square owned by the next
                        // room sits above the previous room's lower floors (a rising shaft), and nothing may be open there.
                        float wallBottom = Mathf.Min(absLowest[i], i > 0 ? absLowest[i - 1] : absLowest[i], i + 1 < rooms.Length ? absLowest[i + 1] : absLowest[i]) - 30f - origin.y;
                        float nextCeiling = i + 1 < rooms.Length ? absCeiling[i + 1] - origin.y : spec.ceiling;
                        using (UseTheme(spec.theme))
                        {
                            Shell(room, spec, i, rooms.Length, turnIn, wallTop, wallBottom, nextCeiling);
                            spec.contents(room);
                            Dress(room, spec, turnIn);
                        }
                        restyled += IndustrialRestyle.Apply(room, spec.theme);
                        room.SetPositionAndRotation(origin, Quaternion.Euler(0, yaw, 0));
                        var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                        origin += forward * spec.length + Vector3.up * spec.dy;
                        yaw += 90f * spec.turn;
                    }
                }
            });
            LookBuilder.ApplyToScene(scene);
            Physics.SyncTransforms();
            int decals = 0;
            foreach (var spec in rooms) decals += IndustrialDecals.Scatter(section.Find(GroupName + "/" + spec.name), spec.length, spec.ceiling, 17);
            Debug.Log($"[IndustrialPlantBuilder] {restyled} KayKit renderers redrawn, {decals} decals scattered");
            PatataWorksBuilder.ValidatePasses();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[IndustrialPlantBuilder] Industrial plant built: eleven closed rooms, nine checkpoints, every obstacle of the kit.");
        }

        // ------------------------------------------------------------------ helpers

        static void Floor(Transform p, string name, float a, float b, float y = 0, float width = 2 * Half, float x = 0)
        {
            if (Mathf.Abs(y) < 0.01f) a = Mathf.Max(a, floorStart);
            if (Mathf.Abs(y - floorEndLevel) < 0.01f) b = Mathf.Min(b, floorEnd);
            if (b - a < 0.01f) return;   // wholly in a neighbour's stretch: its floor is there
            Block(p, name, new Vector3(x, y - 0.5f, (a + b) / 2), new Vector3(width, 1, b - a), KitRole.Floor);
            if (b - a < 8 || width < 8) return;
            for (float z = a + 6; z < b - 1; z += 6) Seam(p, name + " seam", x, y, z, width);
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

        static void CP(Transform p, int id, Vector3 position, float yaw = 0)
        {
            var go = AddCheckpoint(p, $"CP_{id:00}", id, position, id >= 7 ? 5f : 0);
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        static void Kill(Transform p, Vector3 position, Vector3 size)
        {
            var go = Place(p, GameplayDir + "KillZone", "Pit recovery", position, Quaternion.identity);
            go.transform.localScale = size;
        }

        static void Doorway(Transform p, string name, float z, float y, MonoBehaviour source)
        {
            Wall(p, name + " wall", z, y, 9, -12, 12, new[] { new Hole(-3, 3, 0, 5) });
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

        /// <summary>A solid machine along a wall: a painted body with a rubber hatch band and a warning lamp.</summary>
        static void Machine(Transform p, string name, Vector3 center, Vector3 size)
        {
            Block(p, name, center, size, KitRole.Pillar);
            Detail(p, name + " hatch", center + new Vector3(0, size.y * 0.15f, 0), new Vector3(size.x + 0.04f, size.y * 0.3f, size.z - 1f), KitRole.Rubber);
            Detail(p, name + " lamp", center + new Vector3(0, size.y * 0.42f, -size.z / 2 - 0.05f), new Vector3(0.25f, 0.25f, 0.12f), KitRole.Lamp);
        }

        // ------------------------------------------------------------------ the shell (solid, closed, per room)

        /// <summary>
        /// Walls, ceiling and pit floor of a room. Straight joints share their plane; at a turn the outer wall runs through the
        /// corner square, the inner wall stops short of it and the next room owns the corner's ceiling.
        /// </summary>
        static void Shell(Transform room, RoomSpec spec, int index, int count, int turnIn, float wallTop, float bottom, float nextCeiling)
        {
            float L = spec.length;
            bool first = index == 0, last = index == count - 1;
            foreach (int side in new[] { -1, 1 })
            {
                float za = first ? -Half : turnIn == 0 ? 0 : side == turnIn ? Half : -Half;
                float zb = last ? L + Half : spec.turn == 0 ? L : side == spec.turn ? L - Half : L + Half;
                Block(room, "Outer wall", new Vector3(side * (Half + 0.5f), (bottom + wallTop) / 2, (za + zb) / 2), new Vector3(1, wallTop - bottom, zb - za), KitRole.Wall);
            }
            if (first) Block(room, "End wall", new Vector3(0, (bottom + wallTop) / 2, -Half - 0.5f), new Vector3(2 * Half + 2, wallTop - bottom, 1), KitRole.Wall);
            if (last) Block(room, "End wall", new Vector3(0, (bottom + wallTop) / 2, L + Half + 0.5f), new Vector3(2 * Half + 2, wallTop - bottom, 1), KitRole.Wall);
            float ca = first ? -Half : turnIn == 0 ? 0 : -Half;
            float cb = last ? L + Half : spec.turn == 0 ? L : L - Half;
            Block(room, "Ceiling", new Vector3(0, spec.ceiling + 0.5f, (ca + cb) / 2), new Vector3(2 * Half, 1, cb - ca), KitRole.Ceiling).AddComponent<CourseCeiling>();
            // At a turn the next room owns the corner's ceiling: when it is higher or lower than this one, a wall across the joint
            // closes the gap between the two slabs (otherwise the corner would look out over the lower room's roof).
            if (!last && spec.turn != 0 && Mathf.Abs(nextCeiling - spec.ceiling) > 0.01f)
            {
                float lo = Mathf.Min(nextCeiling, spec.ceiling), hi = Mathf.Max(nextCeiling, spec.ceiling) + 1f;
                Block(room, "Ceiling step wall", new Vector3(0, (lo + hi) / 2, L - Half), new Vector3(2 * Half + 2, hi - lo, 1), KitRole.Wall);
            }
            // A solid foundation under the same stretch as the room's floors, down to the walls' foot: a pit room shows its top 20 m
            // down, any other room hides it just under its floors. Solid rather than a slab, so that next to a deeper pit there is
            // no opening under the shallower one, and a corner square left to this room (cornerFloor: false) keeps a bottom.
            float top = Mathf.Min(0, spec.dy) - (spec.pit ? 20f : 1.05f);
            Block(room, spec.pit ? "Pit floor" : "Foundation", new Vector3(0, (top + bottom) / 2, (floorStart + floorEnd) / 2),
                  new Vector3(2 * Half, top - bottom, floorEnd - floorStart), KitRole.Floor);
        }

        /// <summary>The room's dressing (visual only): ceiling trusses and hanging lamps, wall pilasters, ducts, wall lamps.</summary>
        static void Dress(Transform room, RoomSpec spec, int turnIn)
        {
            float L = spec.length, c = spec.ceiling;
            float start = turnIn == 0 ? 6 : 10, end = spec.turn == 0 ? L - 4 : L - 14;
            int lamp = 0;
            for (float z = start; z < end; z += 12)
            {
                Detail(room, "Roof truss", new Vector3(0, c - 0.4f, z), new Vector3(2 * Half - 0.4f, 0.5f, 0.35f), KitRole.Truss);
                Detail(room, "Roof truss flange", new Vector3(0, c - 0.1f, z), new Vector3(2 * Half - 0.4f, 0.06f, 0.7f), KitRole.Truss);
                float x = (lamp++ % 2 == 0 ? -1 : 1) * 4.5f;
                HangingLamp(room, new Vector3(x, Mathf.Min(c - 1.8f, 9.2f), z), c - 0.4f, spec.lamp, spec.lampIntensity, Mathf.Max(30f, c * 1.4f));
            }
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * (Half - 0.05f);
                for (float z = start + 6; z < end; z += 24)
                {
                    Detail(room, "Pilaster", new Vector3(side * (Half - 0.3f), c / 2, z), new Vector3(0.6f, c, 0.8f), KitRole.Pillar);
                    Detail(room, "Pilaster cap", new Vector3(side * (Half - 0.3f), c - 0.15f, z), new Vector3(0.8f, 0.3f, 1.1f), KitRole.Truss);
                }
                Detail(room, "Duct", new Vector3(side * (Half - 0.5f), c - 1.4f, (start + end) / 2), new Vector3(0.55f, 0.55f, end - start), spec.lamp == Furnace || spec.lamp == Sodium ? KitRole.Rust : KitRole.Grating);
                for (float z = start + 2; z < end; z += 6) Detail(room, "Duct clamp", new Vector3(side * (Half - 0.5f), c - 1.4f, z), new Vector3(0.68f, 0.68f, 0.12f), KitRole.Truss);
                if (spec.dy == 0 && c <= 14)
                    for (float z = start + 12; z < end; z += 24)
                        WallLamp(room, x, 5f, z, spec.lamp, spec.lampIntensity * 0.45f);
            }
        }

        /// <summary>A hanging lamp: chain, shade, glowing bulb and the light itself (a point light, no shadows).</summary>
        static void HangingLamp(Transform p, Vector3 position, float from, Color color, float intensity, float range)
        {
            if (from > position.y + 0.3f)
                Detail(p, "Lamp chain", new Vector3(position.x, (position.y + from) / 2, position.z), new Vector3(0.06f, from - position.y, 0.06f), KitRole.Truss);
            Detail(p, "Lamp shade", position + Vector3.up * 0.18f, new Vector3(1.1f, 0.22f, 1.1f), KitRole.Truss);
            Detail(p, "Lamp bulb", position, new Vector3(0.7f, 0.14f, 0.7f), KitRole.Lamp);
            LookBuilder.PracticalLamp(p, position - Vector3.up * 0.6f, color, intensity, range, "Lamp light");
        }

        static void WallLamp(Transform p, float x, float y, float z, Color color, float intensity)
        {
            float side = Mathf.Sign(x);
            Detail(p, "Wall lamp bracket", new Vector3(x - side * 0.25f, y, z), new Vector3(0.5f, 0.1f, 0.1f), KitRole.Truss);
            Detail(p, "Wall lamp bulb", new Vector3(x - side * 0.55f, y, z), new Vector3(0.22f, 0.22f, 0.22f), KitRole.Lamp);
            LookBuilder.PracticalLamp(p, new Vector3(x - side * 1.2f, y - 0.3f, z), color, intensity, 18f, "Wall lamp light");
        }

        /// <summary>Wall lamps at several heights (tall or sloping rooms, where ceiling lamps are too far from the floors).</summary>
        static void WallTiers(Transform p, Color color, float intensity, float[] heights, float zA, float zB, float step)
        {
            foreach (int side in new[] { -1, 1 })
                foreach (float y in heights)
                    for (float z = zA; z <= zB; z += step) WallLamp(p, side * (Half - 0.05f), y, z, color, intensity);
        }

        // ------------------------------------------------------------------ 00 Gatehouse

        static void Gatehouse(Transform p)
        {
            Floor(p, "Loading dock", -12, 60);
            foreach (float z in new[] { 14f, 26f })
                foreach (float x in new[] { -8f, 8f })
                {
                    Block(p, "Cargo", new Vector3(x, 1.5f, z), new Vector3(4, 3, 4), KitRole.Brick);
                    Detail(p, "Cargo band", new Vector3(x, 1.5f, z), new Vector3(4.04f, 0.3f, 4.04f), KitRole.Frame);
                }
            var gate = Gate(p, "Dock gate", new Vector3(0, 0, 32), 2.5f, 8);
            Doorway(p, "Dock door", 40, 0, gate);
            Pass(p, "Dock warmup", new Vector3(-3, 1.5f, 10), new Vector3(3, 1.5f, 18));
            Pass(p, "Dock gate", new Vector3(0, 1.5f, 28), new Vector3(0, 1.5f, 36), opening: 2.4f);
            CP(p, 1, new Vector3(0, 0, 46));
        }

        // ------------------------------------------------------------------ 01 Sorting line

        static void SortingLine(Transform p)
        {
            Floor(p, "Sorting entry", -12, 14);
            Floor(p, "Sorting exit", 78, 108);
            foreach (float x in new[] { -6f, 6f })
            {
                AddConveyor(p, "Sorting belt", new Vector3(x, -0.5f, 46), new Vector3(8, 1, 64), x < 0 ? 2 : -2);
                Zone(p, ZoneForbidden, "No carry inspection", new Vector3(x, 0, 52), new Vector3(8, 3, 3));
            }
            for (float z = 20; z < 76; z += 16)
                Block(p, "Lane divider", new Vector3(0, 3, z), new Vector3(1, 6, 8), KitRole.Brick);
            var tube = Place(p, Tube, "Sorting two routes", Vector3.zero, Quaternion.identity);
            ConfigureTube(tube, new[] {
                new TubeSlot(new Vector3(-6, 1.6f, 46), 0, new Vector3(6, 4, 57), new Vector3(6, 0.3f, 64), 0.9f),
                new TubeSlot(new Vector3(6, 1.6f, 46), 0, new Vector3(-6, 4, 57), new Vector3(-6, 0.3f, 64), 0.9f) });
            foreach (float x in new[] { -6f, 6f })
                Floor(p, "Sorting receiver platform", 60, 68, 0.3f, 8, x);
            TransitPasses(p, tube);
            var gate = Gate(p, "Sorting timed gate", new Vector3(0, 0, 81), 2.5f, 8);
            Doorway(p, "Sorting timed door", 90, 0, gate);
            AddMover(p, "Obstacle_Piston", "Sorting press", new Vector3(-6, 5, 72), new Vector3(-6, 1.7f, 72), new Vector3(7, 2, 2), 1, 0.2f, 0.4f);
            Pass(p, "Sorting window", new Vector3(-6, 1.5f, 30), new Vector3(6, 1.5f, 30), opening: 8);
            Pass(p, "Sorting ring", new Vector3(0, 1.5f, 77), new Vector3(0, 1.5f, 85), opening: 2.4f);
            Kill(p, new Vector3(0, -4, 46), new Vector3(24, 2, 64));
            // overhead gantries across the belts (decoration, well above the throws)
            for (float z = 22; z < 74; z += 16)
            {
                Detail(p, "Sorting gantry", new Vector3(0, 8.4f, z), new Vector3(2 * Half - 0.4f, 0.5f, 0.6f), KitRole.Frame);
                foreach (float x in new[] { -10.8f, 10.8f }) Detail(p, "Sorting gantry leg", new Vector3(x, 4.2f, z), new Vector3(0.5f, 8.4f, 0.5f), KitRole.Frame);
            }
            CP(p, 2, new Vector3(0, 0, 95), -90);
        }

        // ------------------------------------------------------------------ 02 Atrium

        static void Atrium(Transform p)
        {
            Floor(p, "Atrium entry", -12, 12);
            Ramp(p, "West stair ramp", new Vector3(-7, 0, 12), new Vector3(-7, 7, 30), 6);
            Floor(p, "Middle landing", 30, 38, 7);
            Ramp(p, "East stair ramp", new Vector3(7, 7, 38), new Vector3(7, 14, 56), 6);
            Floor(p, "Top landing", 56, 76, 14);
            var plate = Place(p, Plate, "Atrium receiver plate", new Vector3(-5, 7, 33), Quaternion.identity).GetComponent<PressurePlate>();
            var lift = Actuator(p, Lift, "Atrium plate lift", new Vector3(5, -0.25f, 23), new Vector3(6, 0.5f, 10), new Vector3(0, 7, 0), plate);
            SetField(lift.GetComponent<SignalActuator>(), "travelSeconds", v => v.floatValue = 2.5f);
            var gate = Gate(p, "Atrium return gate", new Vector3(-1, 7, 33), 2.5f, 10);
            gate.transform.rotation = Quaternion.Euler(0, 90, 0);
            Actuator(p, Door, "Atrium return door", new Vector3(7, 9.5f, 37), new Vector3(6, 5, 0.8f), new Vector3(0, 5.5f, 0), gate);
            Pass(p, "Atrium upward relay", new Vector3(-7, 5.8f, 23), new Vector3(-5, 8.5f, 33));
            Pass(p, "Atrium return ring", new Vector3(-5, 8.5f, 33), new Vector3(5, 8.5f, 33), opening: 2.4f);
            Pass(p, "Atrium top", new Vector3(7, 12.5f, 48), new Vector3(0, 15.5f, 58));
            Kill(p, new Vector3(0, -5, 34), new Vector3(24, 2, 44));
            // the great hall: tall columns against both walls (decoration)
            foreach (float z in new[] { 6f, 22f, 44f, 62f })
                foreach (float x in new[] { -10.6f, 10.6f })
                    Detail(p, "Atrium column", new Vector3(x, 12, z), new Vector3(1.2f, 24, 1.2f), KitRole.Pillar);
            WallTiers(p, Warm, 24f, new[] { 6f, 13f, 20f }, 8, 60, 16);
            CP(p, 3, new Vector3(0, 14, 62), 90);
        }

        // ------------------------------------------------------------------ 03 Void catwalks

        static void VoidCatwalks(Transform p)
        {
            Floor(p, "Silo entry", -12, 12);
            Ramp(p, "Silo climb", new Vector3(0, 0, 12), new Vector3(0, 4, 24), 8);
            Floor(p, "Silo low throw deck", 24, 36, 4, 10);
            Block(p, "Low throw ceiling", new Vector3(0, 8.5f, 30), new Vector3(12, 1, 12), KitRole.Ceiling).AddComponent<CourseCeiling>();
            for (int i = 0; i < 3; i++)
                Place(p, PlatformsDir + "Platform_Falling", "Silo falling grate", new Vector3(0, 3.75f, 39 + i * 6), Quaternion.identity);
            AddMover(p, "Platform_Moving", "Silo crane", new Vector3(-3, 3.5f, 53), new Vector3(3, 3.5f, 58), new Vector3(8, 1, 6), 2, 0, 0.25f);
            Floor(p, "Silo exit", 60, 76, 4);
            foreach (float x in new[] { -9f, 9f })
                for (float z = 24; z <= 60; z += 18)
                {
                    Block(p, "Silo column", new Vector3(x, 2, z), new Vector3(3, 16, 3), KitRole.Pillar);
                    ColumnTrim(p, "Silo column trim", new Vector3(x, -6, z), 16, 3);
                }
            for (float z = 39; z <= 53; z += 7) Beam(p, "Grate beam", new Vector3(0, 2.9f, z), 3.6f, 0.4f);
            Pass(p, "Low silo throw", new Vector3(0, 5.5f, 26), new Vector3(0, 5.5f, 34), PassCorridor.ArcKind.Low);
            Pass(p, "Falling relay", new Vector3(0, 5.5f, 39), new Vector3(0, 5.5f, 49), timed: true);
            Kill(p, new Vector3(0, -4, 42), new Vector3(24, 2, 48));
            WallTiers(p, Warm, 16f, new[] { 3f }, 6, 62, 14);
            CP(p, 4, new Vector3(0, 4, 63), -90);
        }

        // ------------------------------------------------------------------ 04 Cold storage

        static void ColdStorage(Transform p)
        {
            Floor(p, "Cold storage", -12, 120);
            for (int i = 0; i < 3; i++)
            {
                float z = 24 + i * 20;
                Zone(p, ZoneCold, "Cold pocket", new Vector3(-6, 0, z), new Vector3(8, 3, 12));
                Zone(p, ZoneHot, "Hot shortcut", new Vector3(6, 0, z), new Vector3(7, 3, 12));
                Block(p, "Storage shelf", new Vector3(i % 2 == 0 ? -9 : -2, 2, z + 8), new Vector3(6, 4, 2), KitRole.Brick);
                Block(p, "Lane wall", new Vector3(0, 3, z), new Vector3(1, 6, 12), KitRole.Brick);
                Pass(p, "Cold handoff " + i, new Vector3(-6, 1.5f, z - 4), new Vector3(-6, 1.5f, z + 4));
                Detail(p, "Hot pipe", new Vector3(6, 7.4f, z), new Vector3(0.7f, 0.7f, 12), KitRole.Grating);
            }
            LaserWall(p, "Cold laser window", 90, 0, 9, -4, new[] { 6f });
            Pass(p, "Cold laser", new Vector3(-4, 1.5f, 87), new Vector3(-4, 1.5f, 93), PassCorridor.ArcKind.Low, opening: 2);
            CP(p, 5, new Vector3(0, 0, 106), -90);
        }

        // ------------------------------------------------------------------ 05 Chute

        static void Chute(Transform p)
        {
            Floor(p, "Chute top", -12, 12);
            Ramp(p, "Mega slide", new Vector3(0, 0, 12), new Vector3(0, -28, 60), 14, true);
            Floor(p, "Chute catch court", 60, 84, -28);
            var tube = Place(p, Tube, "Chute tube", Vector3.zero, Quaternion.identity);
            ConfigureTube(tube, new[] { new TubeSlot(new Vector3(-5, 1.6f, 9), 0, new Vector3(-5, -24, 62), new Vector3(-5, -28, 69), 0.9f) });
            SetField(tube.GetComponent<BombTransit>(), "delay", v => v.floatValue = 4f);
            TransitPasses(p, tube);
            for (int i = 0; i < 3; i++)
            {
                float z = 24 + 12 * i, y = -(z - 12) * 28 / 48;
                Zone(p, ZoneForbidden, "Chute pass strip", new Vector3(0, y, z), new Vector3(14, 3, 2));
            }
            Kill(p, new Vector3(0, -34, 40), new Vector3(24, 2, 72));
            for (float z = 24; z < 60; z += 12)
            {
                float y = -(z - 12) * 28 / 48;
                Detail(p, "Slide gantry", new Vector3(0, y + 9.5f, z), new Vector3(2 * Half - 0.4f, 0.5f, 0.6f), KitRole.Frame);
            }
            foreach (int side in new[] { -1, 1 })
                for (float z = 16; z <= 60; z += 11) WallLamp(p, side * (Half - 0.05f), -(z - 12) * 28 / 48 + 4.5f, z, Sodium, 22f);
            WallTiers(p, Sodium, 20f, new[] { -23f }, 66, 80, 14);
            CP(p, 6, new Vector3(0, -28, 71), 90);
        }

        // ------------------------------------------------------------------ 06 Furnace intake, 07 Furnace loop, 08 Boiler approach

        static void FurnaceIntake(Transform p)
        {
            Floor(p, "Furnace intake", -12, 60);
            Zone(p, ZoneHot, "Furnace heat", new Vector3(0, 0, 26), new Vector3(23, 4, 22));
            AddCrusher(p, "Furnace press", new Vector3(0, 0, 24), 10, 3, 0.2f);
            Pass(p, "Furnace press timing", new Vector3(0, 1.5f, 19), new Vector3(0, 1.5f, 29), timed: true);
            for (float z = 18; z < 44; z += 16)
            {
                Block(p, "Boiler casing", new Vector3(-9, 4, z), new Vector3(4, 8, 8), KitRole.Frame);
                LookBuilder.PracticalLamp(p, new Vector3(-6.4f, 2f, z), Furnace, 18f, 20f, "Furnace glow");
                Detail(p, "Furnace mouth", new Vector3(-6.97f, 2f, z), new Vector3(0.05f, 1.3f, 2f), KitRole.Glow);
            }
        }

        static void FurnaceLoop(Transform p)
        {
            Floor(p, "Furnace split court", -12, 76);
            Wall(p, "Furnace wall", 32, 0, 10, -12, 12, new[] { new Hole(-7, -3, 1, 5), new Hole(4, 8, 0, 2.6f) });
            Place(p, ObstaclesDir + "Obstacle_Windmill", "Furnace windmill", new Vector3(-5, 3, 31.2f), Quaternion.identity);
            AddCrusher(p, "Furnace tunnel press", new Vector3(6, 0, 32), 3.8f, 2, 0.4f);
            Pass(p, "Windmill window", new Vector3(-5, 2.2f, 27), new Vector3(-5, 2.2f, 37), PassCorridor.ArcKind.Low, true, 4);
            Zone(p, ZoneCold, "Furnace respite", new Vector3(-5, 0, 42), new Vector3(6, 3, 6));
            foreach (float x in new[] { -9f, 9f })
                LookBuilder.PracticalLamp(p, new Vector3(x, 3f, 50), Furnace, 14f, 18f, "Furnace glow");
        }

        static void BoilerApproach(Transform p)
        {
            Floor(p, "Boiler approach", -12, 60);
            AddRotator(p, ObstaclesDir + "Obstacle_Sweeper", "Boiler sweeper", new Vector3(0, 0, 24), 18, 75, 0);
            Pass(p, "Boiler relay", new Vector3(-4, 1.5f, 32), new Vector3(4, 1.5f, 38));
            foreach (float x in new[] { -9f, 9f }) Machine(p, "Boiler", new Vector3(x, 4, 40), new Vector3(5, 8, 7));
            CP(p, 7, new Vector3(0, 0, 46), 90);
        }

        // ------------------------------------------------------------------ 09 Boiler shaft

        static void BoilerShaft(Transform p)
        {
            Floor(p, "Shaft base", -12, 36);
            for (int i = 0; i < 4; i++)
            {
                float low = i * 8.5f, high = (i + 1) * 8.5f;
                float x = i % 2 == 0 ? -7 : 7, z = 4 + i * 5;
                AddMover(p, "Platform_Moving", "Boiler rising platform " + i,
                    new Vector3(x, low - 0.5f, z), new Vector3(x, high - 0.5f, z), new Vector3(7, 1, 7), 4, 0, 0.3f);
                Floor(p, "Boiler west landing " + i, z + 4, 36, high, 8, -7);
                Floor(p, "Boiler east landing " + i, z + 4, 36, high, 8, 7);
                Floor(p, "Boiler landing bridge " + i, 32, 36, high, 22);
            }
            Floor(p, "Shaft upper catch court", 22, 28, 34);
            Floor(p, "Shaft upper bridge", 32, 36, 34);
            var cannon = Place(p, Cannon, "Boiler cannon", new Vector3(0, 25.5f, 34), Quaternion.identity);
            ConfigureCannon(cannon, new Vector3(0, 34, 25), 1.6f);
            TransitPasses(p, cannon);
            WallTiers(p, Warm, 26f, new[] { 6f, 15f, 24f, 33f }, 6, 30, 12);
            CP(p, 8, new Vector3(0, 34, 27), -90);
        }

        // ------------------------------------------------------------------ 10 Control room

        static void ControlRoom(Transform p)
        {
            // The corner square is the top of the boiler shaft: its own floors, with the lift wells and the gap the cannon fires through
            // (cornerFloor: false), so this floor starts past it.
            Floor(p, "Control room floor", -12, 52);
            EdgeStrip(p, "Shaft edge strip", 0, 0, 12.15f, 0.3f, 2 * Half);
            ArchCheckpoint(p, "CP_09", 9, new Vector3(0, 0, 14), 5, new Vector3(0, 0, 9));
            Pass(p, "Control room arch", new Vector3(0, 1.5f, 5), new Vector3(0, 1.5f, 14), opening: 4);
            Place(p, GameplayDir + "FinishZone", "FinishZone", new Vector3(0, 0, 36), Quaternion.identity);
            foreach (float x in new[] { -9f, 9f })
                foreach (float z in new[] { 24f, 34f })
                {
                    Block(p, "Console", new Vector3(x, 1, z), new Vector3(5, 2, 2), KitRole.Pillar);
                    Detail(p, "Console screen", new Vector3(x, 2.15f, z - 0.4f), new Vector3(3.6f, 0.08f, 0.9f), KitRole.Glow);
                }
            Detail(p, "Finish panel", new Vector3(0, 7, 49.7f), new Vector3(16, 6, 0.3f), KitRole.Frame);
            Detail(p, "Finish panel light", new Vector3(0, 7, 49.5f), new Vector3(13.6f, 4, 0.1f), KitRole.Glow);
            LookBuilder.PracticalLamp(p, new Vector3(0, 7, 46), Warm, 14f, 18f, "Finish light");
        }
    }
}
