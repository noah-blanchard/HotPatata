using HotPatata;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using static HotPatata.Editor.BombObstacleKitBuilder;

namespace HotPatata.Editor
{
    /// <summary>
    /// The twenty-five sections of PatataWilds (PROJECT_SPEC §15c). Each is built in its own space: +Z forward, x across the 24 m
    /// section, the floor of its start at y = 0. Pass lengths, jumps and obstacle timings follow PatataWorks and the industrial
    /// plant (sections marked "from the plant" reuse their proven layouts, re-dressed); every intended pass is declared.
    /// </summary>
    public static partial class PatataWildsBuilder
    {
        // ================================================================== Act 1 - Misty Hollow (dawn)

        /// <summary>Warm-up passes between boulders, then a vine ring opens the palisade (§13.1, §13.15). From the plant's gatehouse.</summary>
        static void TrailheadGlade(Transform p)
        {
            Floor(p, "Trailhead", -12, 60);
            foreach (float z in new[] { 14f, 26f })
                foreach (float x in new[] { -8f, 8f })
                    NatureKit.Rock(p, "Boulder", new Vector3(x, 1.4f, z), new Vector3(4, 2.8f, 4));
            var gate = Gate(p, "Vine ring", new Vector3(0, 0, 32), 2.5f, 8);
            Palisade(p, "Trailhead gate", 40, 0, gate);
            Pass(p, "Trailhead warmup", new Vector3(-3, 1.5f, 10), new Vector3(3, 1.5f, 18));
            Pass(p, "Trailhead ring", new Vector3(0, 1.5f, 28), new Vector3(0, 1.5f, 36), opening: 2.4f);
            CP(p, 1, new Vector3(0, 0, 50), -90);
            ActSound(p, 56);
        }

        /// <summary>A relay up rock terraces of fern and moss (§13.3): solid steps a player climbs, passes up each rise.</summary>
        static void FernTerraces(Transform p)
        {
            Floor(p, "Terrace foot", -12, 12);
            Step(p, "Terrace step 1", 12, 18, 1.2f);
            Step(p, "Terrace step 2", 18, 24, 2.4f);
            Step(p, "Lower terrace", 24, 34, 3.5f);
            Step(p, "Terrace step 3", 34, 40, 4.7f);
            Step(p, "Terrace step 4", 40, 46, 5.9f);
            Floor(p, "Upper terrace", 46, 64, 7);
            Step(p, "Upper terrace bank", 46, 64, 6.0f);
            foreach (float x in new[] { -9f, 9f })
            {
                NatureKit.Rock(p, "Terrace outcrop", new Vector3(x, 4.5f, 29), new Vector3(3, 2f, 4), "Nature_RockPitted");
                NatureKit.Rock(p, "Terrace outcrop", new Vector3(-x * 0.9f, 8f, 52), new Vector3(3, 2f, 3), "Nature_RockPitted");
            }
            Pass(p, "Terrace relay 1", new Vector3(-3, 3.9f, 21), new Vector3(3, 5f, 30));
            Pass(p, "Terrace relay 2", new Vector3(3, 6.2f, 37), new Vector3(-3, 8.5f, 48));
            CP(p, 2, new Vector3(0, 7, 58));
            ActSound(p, 64);
        }

        /// <summary>The first water: stepping stones the receiver crosses first, then a log raft over the pool (§13.1, §13.2).</summary>
        static void BrookCrossing(Transform p)
        {
            Floor(p, "Brook bank", -12, 16);
            NatureKit.Water(p, "Brook", -12, 12, 16, 52, WaterSurface, WaterBed);
            foreach (var (x, z) in new[] { (-4f, 19f), (2f, 25f), (-3f, 31f) })
                NatureKit.Rock(p, "Stepping stone", new Vector3(x, (WaterBed + 0f) / 2f, z), new Vector3(3.6f, -WaterBed, 3.6f), "Nature_MossyRock");
            AddMover(p, "Platform_Moving", "Log raft", new Vector3(0, -0.5f, 37), new Vector3(0, -0.5f, 45), new Vector3(6, 1, 5), 2, 0, 0.3f);
            NatureKit.Rock(p, "Stepping stone", new Vector3(2, WaterBed / 2f, 49.5f), new Vector3(3.6f, -WaterBed, 3.6f), "Nature_MossyRock");
            Floor(p, "Far bank", 52, 84);
            Pass(p, "Brook first stone", new Vector3(0, 1.5f, 10), new Vector3(-4, 1.5f, 19));
            Pass(p, "Brook raft", new Vector3(-3, 1.5f, 31), new Vector3(0, 1.5f, 40), timed: true);
            Pass(p, "Brook far bank", new Vector3(2, 1.5f, 49.5f), new Vector3(0, 1.5f, 58));
            Sound(p, new Vector3(0, 1, 34), NatureAudioFactory.Ambience.River);
            CP(p, 3, new Vector3(0, 0, 64), 90);
            ActSound(p, 72);
        }

        /// <summary>
        /// A boardwalk over a bog under fallen trunks: low throws (§13.5), rotten planks (§13.7), a raft to reach the far deck
        /// (§13.2). From the plant's silo catwalks.
        /// </summary>
        static void RottenBoardwalk(Transform p)
        {
            Floor(p, "Bog bank", -12, 12);
            Ramp(p, "Boardwalk climb", new Vector3(0, 0, 12), new Vector3(0, 4, 24), 8);
            Floor(p, "Low boardwalk", 24, 36, 4, 10, role: KitRole.Grating);
            Block(p, "Fallen trunk canopy", new Vector3(0, 8.5f, 30), new Vector3(12, 1, 12), KitRole.Truss).AddComponent<CourseCeiling>();
            for (int i = 0; i < 3; i++)
                Place(p, PlatformsDir + "Platform_Falling", "Rotten planks", new Vector3(0, 3.75f, 39 + i * 6), Quaternion.identity);
            AddMover(p, "Platform_Moving", "Bog raft", new Vector3(-3, 3.5f, 53), new Vector3(3, 3.5f, 58), new Vector3(8, 1, 6), 2, 0, 0.25f);
            Floor(p, "Far deck", 60, 76, 4, role: KitRole.Grating);
            NatureKit.Water(p, "Bog", -12, 12, 18, 60, WaterSurface, WaterBed);
            foreach (float x in new[] { -9f, 9f })
                for (float z = 24; z <= 60; z += 18)
                    NatureKit.Rock(p, "Bog rock", new Vector3(x, 1.5f, z), new Vector3(3, 9, 3));
            Pass(p, "Low boardwalk throw", new Vector3(0, 5.5f, 26), new Vector3(0, 5.5f, 34), PassCorridor.ArcKind.Low);
            Pass(p, "Rotten relay", new Vector3(0, 5.5f, 39), new Vector3(0, 5.5f, 49), timed: true);
            Sound(p, new Vector3(0, 1, 40), NatureAudioFactory.Ambience.River);
            CP(p, 4, new Vector3(0, 4, 63), 90);
            ActSound(p, 64);
        }

        /// <summary>The team splits around a rock spine; a hollow log carries the bomb to the other lane (§13.4, §13.16). From the plant's sorting line.</summary>
        static void HollowLogJunction(Transform p)
        {
            Floor(p, "Junction", -12, 108);
            for (float z = 20; z < 76; z += 16)
                NatureKit.Rock(p, "Rock spine", new Vector3(0, 3, z), new Vector3(1.4f, 6, 8));
            var tube = Place(p, Tube, "Hollow log", Vector3.zero, Quaternion.identity);
            ConfigureTube(tube, new[] {
                new TubeSlot(new Vector3(-6, 1.6f, 46), 0, new Vector3(6, 4, 57), new Vector3(6, 0.3f, 64), 0.9f),
                new TubeSlot(new Vector3(6, 1.6f, 46), 0, new Vector3(-6, 4, 57), new Vector3(-6, 0.3f, 64), 0.9f) });
            foreach (float x in new[] { -6f, 6f })
                Floor(p, "Log landing", 60, 68, 0.3f, 8, x, KitRole.Grating);
            TransitPasses(p, tube);
            var gate = Gate(p, "Junction vine ring", new Vector3(0, 0, 81), 2.5f, 8);
            Palisade(p, "Junction gate", 90, 0, gate);
            AddMover(p, "Obstacle_Piston", "Rising stone", new Vector3(-6, 5, 72), new Vector3(-6, 1.7f, 72), new Vector3(7, 2, 2), 1, 0.2f, 0.4f);
            Pass(p, "Junction window", new Vector3(-6, 1.5f, 30), new Vector3(6, 1.5f, 30), opening: 8);
            Pass(p, "Junction ring", new Vector3(0, 1.5f, 77), new Vector3(0, 1.5f, 85), opening: 2.4f);
            CP(p, 5, new Vector3(0, 0, 97), -90);
            ActSound(p, 96);
        }

        // ================================================================== Act 2 - River Run (noon)

        /// <summary>Two log drives running opposite ways over the river (§13.9), no-carry weirs (§13.13), a window between the lanes. From the plant's sorting line.</summary>
        static void LogDriveLanes(Transform p)
        {
            Floor(p, "Lanes entry", -12, 14);
            Floor(p, "Lanes exit", 78, 108);
            NatureKit.Water(p, "River", -12, 12, 14, 78, WaterSurface - 0.5f, WaterBed);
            foreach (float x in new[] { -6f, 6f })
            {
                AddConveyor(p, "Log drive", new Vector3(x, -0.5f, 46), new Vector3(8, 1, 64), x < 0 ? 2 : -2);
                Zone(p, ZoneForbidden, "No-carry weir", new Vector3(x, 0, 52), new Vector3(8, 3, 3));
            }
            for (float z = 20; z < 76; z += 16)
                NatureKit.Rock(p, "Lane rock", new Vector3(0, 2.2f, z), new Vector3(1.4f, 6.4f, 8));
            var gate = Gate(p, "Lanes vine ring", new Vector3(0, 0, 81), 2.5f, 8);
            Palisade(p, "Lanes gate", 90, 0, gate);
            Pass(p, "Lanes window", new Vector3(-6, 1.5f, 30), new Vector3(6, 1.5f, 30), opening: 8);
            Pass(p, "Lanes weir handoff", new Vector3(6, 1.5f, 46), new Vector3(6, 1.5f, 56), timed: true);
            Pass(p, "Lanes ring", new Vector3(0, 1.5f, 77), new Vector3(0, 1.5f, 85), opening: 2.4f);
            Sound(p, new Vector3(0, 1, 30), NatureAudioFactory.Ambience.River);
            Sound(p, new Vector3(0, 1, 62), NatureAudioFactory.Ambience.River);
            CP(p, 6, new Vector3(0, 0, 97), -90);
            ActSound(p, 96);
        }

        /// <summary>Log rafts out of step over the rapids: jump and catch on moving rafts (§13.2, §13.8).</summary>
        static void RapidsRafts(Transform p)
        {
            Floor(p, "Rapids bank", -12, 16);
            NatureKit.Water(p, "Rapids", -12, 12, 16, 48, WaterSurface, WaterBed);
            for (int i = 0; i < 4; i++)
            {
                float z = 19 + i * 8;
                var a = new Vector3(-5, -0.5f, z);
                var b = new Vector3(5, -0.5f, z);
                AddMover(p, "Platform_Moving", "Rapids raft " + (i + 1), i % 2 == 0 ? a : b, i % 2 == 0 ? b : a, new Vector3(6, 1, 6), 2, 0, 0.3f);
            }
            Floor(p, "Rapids far bank", 48, 72);
            Pass(p, "Rapids first raft", new Vector3(0, 1.5f, 10), new Vector3(0, 1.5f, 19), timed: true);
            Pass(p, "Rapids raft relay", new Vector3(0, 1.5f, 27), new Vector3(0, 1.5f, 35), timed: true);
            Pass(p, "Rapids landing", new Vector3(0, 1.5f, 43), new Vector3(0, 1.5f, 52), timed: true);
            Sound(p, new Vector3(0, 1, 24), NatureAudioFactory.Ambience.River);
            Sound(p, new Vector3(0, 1, 42), NatureAudioFactory.Ambience.River);
            CP(p, 7, new Vector3(0, 0, 62));
            ActSound(p, 72);
        }

        /// <summary>
        /// Two Banks, One Bomb: the river splits the team; two hollow logs (green, one pip; violet, two pips) reach the two banks,
        /// the thrower picks the bank the receiver ran to (§13.16). From the plant's sorting line.
        /// </summary>
        static void TwoBanks(Transform p)
        {
            Floor(p, "Banks entry", -12, 14);
            Floor(p, "Banks exit", 78, 108);
            foreach (float x in new[] { -7f, 7f }) Floor(p, "River bank", 14, 78, 0, 10, x);
            NatureKit.Water(p, "River channel", -2, 2, 14, 78, WaterSurface, WaterBed);
            for (float z = 20; z < 76; z += 16)
                NatureKit.Rock(p, "Channel rock", new Vector3(0, 2.2f, z), new Vector3(2, 6.4f, 8));
            var tube = Place(p, Tube, "Two banks logs", Vector3.zero, Quaternion.identity);
            ConfigureTube(tube, new[] {
                new TubeSlot(new Vector3(-6, 1.6f, 46), 0, new Vector3(6, 4, 57), new Vector3(6, 0.3f, 64), 0.9f),
                new TubeSlot(new Vector3(6, 1.6f, 46), 0, new Vector3(-6, 4, 57), new Vector3(-6, 0.3f, 64), 0.9f) });
            foreach (float x in new[] { -6f, 6f })
                Floor(p, "Bank landing", 60, 68, 0.3f, 8, x, KitRole.Grating);
            TransitPasses(p, tube);
            var gate = Gate(p, "Banks vine ring", new Vector3(0, 0, 81), 2.5f, 8);
            Palisade(p, "Banks gate", 90, 0, gate);
            AddMover(p, "Obstacle_Piston", "Bank rising stone", new Vector3(6, 5, 72), new Vector3(6, 1.7f, 72), new Vector3(7, 2, 2), 1, 0.2f, 0.4f);
            Pass(p, "Banks across", new Vector3(-6, 1.5f, 30), new Vector3(6, 1.5f, 30), opening: 8);
            Pass(p, "Banks ring", new Vector3(0, 1.5f, 77), new Vector3(0, 1.5f, 85), opening: 2.4f);
            Sound(p, new Vector3(0, 1, 46), NatureAudioFactory.Ambience.River);
            CP(p, 8, new Vector3(0, 0, 97), 90);
            ActSound(p, 96);
        }

        /// <summary>A mud chute down the gully, with no-carry riffles to pass over (§13.12).</summary>
        static void Mudslide(Transform p)
        {
            Floor(p, "Slide top", -12, 12);
            Ramp(p, "Mud chute", new Vector3(0, 0, 12), new Vector3(0, -18, 48), 14, true);
            Floor(p, "Slide foot", 48, 84, -18);
            float Y(float z) => -(z - 12) * 18 / 36;
            for (int i = 0; i < 2; i++)
            {
                float z = 22 + 12 * i;
                Zone(p, ZoneForbidden, "No-carry riffle", new Vector3(0, Y(z), z), new Vector3(14, 3, 2));
            }
            Pass(p, "Chute relay", new Vector3(-3, Y(16) + 1.5f, 16), new Vector3(3, Y(26) + 1.5f, 26));
            Pass(p, "Chute riffle", new Vector3(0, Y(30) + 1.5f, 30), new Vector3(0, Y(40) + 1.5f, 40));
            Kill(p, new Vector3(0, -26, 40), new Vector3(24, 2, 72));
            CP(p, 9, new Vector3(0, -18, 71), -90);
            ActSound(p, 72);
        }

        /// <summary>
        /// Mill Race: the water wheel guards the only window for the bomb; runners take the tunnel under the log stamp (§13.11,
        /// §13.5). From the plant's furnace loop.
        /// </summary>
        static void MillRace(Transform p)
        {
            Floor(p, "Mill yard", -12, 76);
            Wall(p, "Mill wall", 32, 0, 10, -12, 12, new[] { new Hole(-7, -3, 1, 5), new Hole(4, 8, 0, 2.6f) });
            Place(p, ObstaclesDir + "Obstacle_Windmill", "Water wheel", new Vector3(-5, 3, 31.2f), Quaternion.identity);
            AddCrusher(p, "Log stamp", new Vector3(6, 0, 32), 3.8f, 2, 0.4f);
            Pass(p, "Wheel window", new Vector3(-5, 2.2f, 27), new Vector3(-5, 2.2f, 37), PassCorridor.ArcKind.Low, true, 4);
            Zone(p, ZoneCold, "Spring shade", new Vector3(-5, 0, 42), new Vector3(6, 3, 6));
            Sound(p, new Vector3(-5, 2, 31), NatureAudioFactory.Ambience.River);
            CP(p, 10, new Vector3(0, 0, 56), 90, -1f);
            ActSound(p, 64);
        }

        // ================================================================== Act 3 - Granite Cliffs (late afternoon)

        /// <summary>A relay up granite ledges (§13.3); a rope lift on the right is the other way up.</summary>
        static void CliffBaseRelay(Transform p)
        {
            Floor(p, "Cliff base", -12, 12);
            Step(p, "Ledge step 1", 12, 18, 1.2f);
            Step(p, "Ledge step 2", 18, 24, 2.4f);
            Step(p, "Ledge A", 24, 34, 3.6f);
            Step(p, "Ledge step 3", 34, 38, 4.8f, 12, -6);
            Step(p, "Ledge step 4", 38, 42, 6f, 12, -6);
            Step(p, "Ledge A under the lift", 34, 42, 3.6f, 12, 6);
            Step(p, "Ledge B", 42, 50, 7.2f);
            Step(p, "Ledge step 5", 50, 54, 8.6f);
            Floor(p, "Cliff top", 54, 76, 10);
            Step(p, "Cliff top bank", 54, 64, 9f);
            AddMover(p, "Platform_Moving", "Rope lift", new Vector3(6, 3.1f, 38), new Vector3(6, 6.7f, 38), new Vector3(4, 1, 4), 1.5f, 0, 0.35f);
            Pass(p, "Ledge relay 1", new Vector3(0, 3.9f, 21), new Vector3(0, 5.1f, 30));
            Pass(p, "Ledge relay 2", new Vector3(-6, 7.5f, 40), new Vector3(0, 8.7f, 47));
            Pass(p, "Cliff top relay", new Vector3(0, 10.1f, 52), new Vector3(0, 11.5f, 60));
            CP(p, 11, new Vector3(0, 10, 62));
            ActSound(p, 64);
        }

        /// <summary>
        /// Hold the Rope: a runner holds a stone plate on the ledge to keep the rope lift up while the carrier passes across the
        /// gorge; the pass lands on that ledge, so with two players the plate holder is also the receiver; a vine ring on the far
        /// side opens the gate that brings the plate holder back (§13.15). From the plant's atrium.
        /// </summary>
        static void HoldTheRope(Transform p)
        {
            Floor(p, "Gorge foot", -12, 12);
            Ramp(p, "West ledge path", new Vector3(-7, 0, 12), new Vector3(-7, 7, 30), 6);
            Floor(p, "Middle ledge", 30, 36, 7);
            Ramp(p, "East ledge path", new Vector3(7, 7, 36), new Vector3(7, 14, 52), 6);
            var plate = Place(p, Plate, "Rope plate", new Vector3(-5, 7, 33), Quaternion.identity).GetComponent<PressurePlate>();
            var lift = Actuator(p, Lift, "Rope lift", new Vector3(5, -0.25f, 23), new Vector3(6, 0.5f, 10), new Vector3(0, 7, 0), plate);
            SetField(lift.GetComponent<SignalActuator>(), "travelSeconds", v => v.floatValue = 2.5f);
            var gate = Gate(p, "Return vine ring", new Vector3(-1, 7, 33), 2.5f, 10);
            gate.transform.rotation = Quaternion.Euler(0, 90, 0);
            Actuator(p, Door, "Return gate", new Vector3(7, 9.5f, 37), new Vector3(6, 5, 0.8f), new Vector3(0, 5.5f, 0), gate);
            Pass(p, "Rope upward relay", new Vector3(-7, 5.8f, 23), new Vector3(-5, 8.5f, 33));
            Pass(p, "Rope return ring", new Vector3(-5, 8.5f, 33), new Vector3(5, 8.5f, 33), opening: 2.4f);
            Pass(p, "Rope top", new Vector3(7, 13.6f, 47), new Vector3(0, 15.5f, 58));
            Kill(p, new Vector3(0, -5, 34), new Vector3(24, 2, 44));
            CP(p, 12, new Vector3(0, 14, 62), -90);
            ActSound(p, 64);
        }

        /// <summary>
        /// A cave lit by torches: steam vents (hot zones) and a cold spring (cold pockets) along a long hall (§13.14), cairn laser
        /// windows at the end (§13.13). From the plant's cold storage.
        /// </summary>
        static void EmberCave(Transform p)
        {
            Floor(p, "Cave floor", -12, 120);
            for (int i = 0; i < 3; i++)
            {
                float z = 24 + i * 20;
                Zone(p, ZoneCold, "Cold spring", new Vector3(-6, 0, z), new Vector3(8, 3, 12));
                Zone(p, ZoneHot, "Steam vent", new Vector3(6, 0, z), new Vector3(7, 3, 12));
                NatureKit.Rock(p, "Cave rock", new Vector3(i % 2 == 0 ? -9 : -2, 2, z + 8), new Vector3(6, 4, 2), "Nature_RockPitted");
                NatureKit.Rock(p, "Cave spine", new Vector3(0, 3, z), new Vector3(1.4f, 6, 12), "Nature_RockPitted");
                Pass(p, "Cold handoff " + i, new Vector3(-6, 1.5f, z - 4), new Vector3(-6, 1.5f, z + 4));
                foreach (float x in new[] { -11.3f, 11.3f }) NatureKit.Torch(p, new Vector3(x, 0, z - 6));
            }
            NatureKit.Torch(p, new Vector3(-11.3f, 0, 4));
            NatureKit.Torch(p, new Vector3(11.3f, 0, 84));
            NatureKit.Torch(p, new Vector3(-11.3f, 0, 98));
            LaserWall(p, "Cairn window", 90, 0, 9, -4, new[] { 6f });
            Pass(p, "Cairn laser", new Vector3(-4, 1.5f, 87), new Vector3(-4, 1.5f, 93), PassCorridor.ArcKind.Low, opening: 2);
            CP(p, 13, new Vector3(0, 0, 106), 0);
        }

        /// <summary>A swinging log sweeps the ledge (§13.11); relay over it. From the plant's boiler approach.</summary>
        static void LedgeTraverse(Transform p)
        {
            Floor(p, "Ledge", -12, 60);
            AddRotator(p, ObstaclesDir + "Obstacle_Sweeper", "Swinging log", new Vector3(0, 0, 24), 18, 75, 0);
            Pass(p, "Ledge relay", new Vector3(-4, 1.5f, 32), new Vector3(4, 1.5f, 38));
            foreach (float x in new[] { -9f, 9f }) NatureKit.Rock(p, "Ledge boulder", new Vector3(x, 3, 40), new Vector3(5, 6, 7), "Nature_RockFace");
            CP(p, 14, new Vector3(0, 0, 46), -90);
            ActSound(p, 48);
        }

        /// <summary>
        /// Up the Cliff: log rafts rise up the chimney; the stump catapult fires the bomb to the cliff top while the receiver
        /// rides up (§13.16, §13.8). From the plant's boiler shaft.
        /// </summary>
        static void UpTheCliff(Transform p)
        {
            Floor(p, "Chimney foot", -12, 36);
            for (int i = 0; i < 4; i++)
            {
                float low = i * 8.5f, high = (i + 1) * 8.5f;
                float x = i % 2 == 0 ? -7 : 7, z = 4 + i * 5;
                AddMover(p, "Platform_Moving", "Rising raft " + i, new Vector3(x, low - 0.5f, z), new Vector3(x, high - 0.5f, z), new Vector3(7, 1, 7), 4, 0, 0.3f);
                Floor(p, "West ledge " + i, z + 4, 36, high, 8, -7);
                Floor(p, "East ledge " + i, z + 4, 36, high, 8, 7);
                Floor(p, "Ledge bridge " + i, 32, 36, high, 22, 0, KitRole.Grating);
            }
            Floor(p, "Cliff catch ledge", 22, 28, 34);
            Floor(p, "Cliff top bridge", 32, 36, 34, 2 * Half, 0, KitRole.Grating);
            var cannon = Place(p, Cannon, "Stump catapult", new Vector3(0, 25.5f, 34), Quaternion.identity);
            ConfigureCannon(cannon, new Vector3(0, 34, 25), 1.6f);
            TransitPasses(p, cannon);
            CP(p, 15, new Vector3(0, 34, 27), -90, fireOffset: new Vector3(4.5f, 0, 0));   // beside the catch ledge, off the catapult's arc
        }

        // ================================================================== Act 4 - Waterfall Gorge (sunset)

        /// <summary>Two plank bridges over the gorge, one with rotten planks (§13.7); a timed vine ring holds the palisade open (§13.15).</summary>
        static void SprayBridges(Transform p)
        {
            Floor(p, "Gorge rim", -12, 22);
            Floor(p, "West bridge", 22, 30, 0, 3.5f, -5, KitRole.Grating);
            Place(p, PlatformsDir + "Platform_Falling", "Rotten bridge plank", new Vector3(-5, -0.25f, 33), Quaternion.identity);
            Place(p, PlatformsDir + "Platform_Falling", "Rotten bridge plank", new Vector3(-5, -0.25f, 39), Quaternion.identity);
            Floor(p, "West bridge end", 42, 54, 0, 3.5f, -5, KitRole.Grating);
            Floor(p, "East bridge", 22, 54, 0, 3.5f, 5, KitRole.Grating);
            Floor(p, "Far rim", 54, 84);
            var gate = Gate(p, "Gorge vine ring", new Vector3(0, 0, 58), 2.5f, 8);
            Palisade(p, "Gorge gate", 64, 0, gate);
            Pass(p, "Bridge to bridge", new Vector3(5, 1.5f, 28), new Vector3(-5, 1.5f, 36), timed: true);
            Pass(p, "Bridge relay", new Vector3(-5, 1.5f, 44), new Vector3(5, 1.5f, 50));
            Pass(p, "Gorge ring", new Vector3(0, 1.5f, 54), new Vector3(0, 1.5f, 62), opening: 2.4f);
            Kill(p, new Vector3(0, -12, 38), new Vector3(24, 2, 34));
            NatureKit.Waterfall(p, "Gorge fall", new Vector3(-11.4f, 0, 38), 10f, 8f, -20f, 90f);
            Sound(p, new Vector3(-10, -4, 38), NatureAudioFactory.Ambience.Waterfall);
            CP(p, 16, new Vector3(0, 0, 70), 0);
            ActSound(p, 72);
        }

        /// <summary>The ledge behind the waterfall: a low overhang makes low throws (§13.5), cold spray pockets slow the fuse (§13.14).</summary>
        static void BehindTheFalls(Transform p)
        {
            Floor(p, "Falls ledge", -12, 68, 0, 16, -4);
            Floor(p, "Falls landing", -12, 14, 0, 8, 8);
            Floor(p, "Falls far landing", 46, 68, 0, 8, 8);
            NatureKit.Water(p, "Plunge pool", 4, 12, 14, 46, WaterSurface, WaterBed);
            Block(p, "Overhang", new Vector3(-4, 5f, 30), new Vector3(16, 1, 20), KitRole.Ceiling).AddComponent<CourseCeiling>();
            Zone(p, ZoneCold, "Spray", new Vector3(-4, 0, 30), new Vector3(10, 3, 14));
            Pass(p, "Under the overhang", new Vector3(-4, 1.5f, 22), new Vector3(-4, 1.5f, 30), PassCorridor.ArcKind.Low);
            Pass(p, "Behind the fall", new Vector3(-4, 1.5f, 30), new Vector3(-4, 1.5f, 38), PassCorridor.ArcKind.Low);
            Block(p, "Fall lip", new Vector3(8, 8.75f, 30), new Vector3(8, 1.5f, 26), KitRole.Wall);
            NatureKit.Waterfall(p, "The fall", new Vector3(5f, 0, 30), 24f, 8f, WaterSurface, -90f);
            Sound(p, new Vector3(8, 2, 30), NatureAudioFactory.Ambience.Waterfall);
            CP(p, 17, new Vector3(-4, 0, 54), 90, -1f);
            ActSound(p, 56);
        }

        /// <summary>A cairn window with a laser curtain for the runners, then a willow hoop turning in a wall (§13.13 and the hoop variant).</summary>
        static void WheelGorge(Transform p)
        {
            Floor(p, "Gorge path", -12, 38);
            LaserWall(p, "Cairn wall", 16, 0, 9, -4, new[] { 6f });
            LaserWall(p, "Willow wall", 32, 0, 9, 4, new[] { -6f }, hoop: true);
            Ramp(p, "Gorge rise", new Vector3(0, 0, 38), new Vector3(0, 6, 50), 10);
            Step(p, "Gorge rise bank west", 38, 50, 3f, 7, -8.5f);
            Step(p, "Gorge rise bank east", 38, 50, 3f, 7, 8.5f);
            Floor(p, "Gorge shelf", 50, 76, 6);
            Pass(p, "Cairn window", new Vector3(-4, 1.5f, 13), new Vector3(-4, 1.5f, 19), PassCorridor.ArcKind.Low, opening: 2);
            Pass(p, "Willow hoop", new Vector3(4, 2.4f, 28), new Vector3(4, 2.4f, 36), timed: true, opening: 3.2f);
            CP(p, 18, new Vector3(0, 6, 62), 90);
            ActSound(p, 64);
        }

        /// <summary>
        /// Down the Rapids: the runners slide down the wet chute while the bomb takes a hollow log to the plunge-pool court
        /// (§13.12, §13.16). From the plant's chute.
        /// </summary>
        static void DownTheRapids(Transform p)
        {
            Floor(p, "Rapids top", -12, 12);
            Ramp(p, "Wet chute", new Vector3(0, 0, 12), new Vector3(0, -28, 60), 14, true);
            Floor(p, "Pool court", 60, 84, -28);
            var tube = Place(p, Tube, "Rapids log", Vector3.zero, Quaternion.identity);
            ConfigureTube(tube, new[] { new TubeSlot(new Vector3(-5, 1.6f, 9), 0, new Vector3(-5, -24, 62), new Vector3(-5, -28, 69), 0.9f) });
            SetField(tube.GetComponent<BombTransit>(), "delay", v => v.floatValue = 4f);
            TransitPasses(p, tube);
            for (int i = 0; i < 3; i++)
            {
                float z = 24 + 12 * i, y = -(z - 12) * 28 / 48;
                Zone(p, ZoneForbidden, "No-carry riffle", new Vector3(0, y, z), new Vector3(14, 3, 2));
            }
            Kill(p, new Vector3(0, -34, 40), new Vector3(24, 2, 72));
            NatureKit.Waterfall(p, "Rapids fall", new Vector3(11.4f, 0, 36), 12f, 2f, -30f, -90f);
            Sound(p, new Vector3(10, -14, 36), NatureAudioFactory.Ambience.Waterfall);
            CP(p, 19, new Vector3(0, -28, 71), -90);
        }

        /// <summary>
        /// Gorge Lock: rafts rise up the lock; the bomb goes up by a hollow log to the top ledge while the team climbs (§13.2,
        /// §13.16). The plant's boiler shaft, with a log for the cannon.
        /// </summary>
        static void GorgeLock(Transform p)
        {
            Floor(p, "Lock foot", -12, 36);
            for (int i = 0; i < 4; i++)
            {
                float low = i * 8.5f, high = (i + 1) * 8.5f;
                float x = i % 2 == 0 ? 7 : -7, z = 4 + i * 5;
                AddMover(p, "Platform_Moving", "Lock raft " + i, new Vector3(x, low - 0.5f, z), new Vector3(x, high - 0.5f, z), new Vector3(7, 1, 7), 4, 0, 0.3f);
                Floor(p, "West lock ledge " + i, z + 4, 36, high, 8, -7);
                Floor(p, "East lock ledge " + i, z + 4, 36, high, 8, 7);
                Floor(p, "Lock bridge " + i, 32, 36, high, 22, 0, KitRole.Grating);
            }
            Floor(p, "Lock catch ledge", 22, 28, 34);
            Floor(p, "Lock top bridge", 32, 36, 34, 2 * Half, 0, KitRole.Grating);
            var tube = Place(p, Tube, "Lock log", Vector3.zero, Quaternion.identity);
            ConfigureTube(tube, new[] { new TubeSlot(new Vector3(0, 1.6f, -4), 0, new Vector3(0, 37, 21), new Vector3(0, 34, 25), 0.9f) });
            TransitPasses(p, tube);
            NatureKit.Waterfall(p, "Lock fall", new Vector3(-11.4f, 0, 12), 8f, 40f, 0f, 90f);
            Sound(p, new Vector3(-10, 8, 12), NatureAudioFactory.Ambience.Waterfall);
            CP(p, 20, new Vector3(0, 34, 27), -90, fireOffset: new Vector3(4.5f, 0, 0));   // beside the catch ledge, off the log's arc
        }

        // ================================================================== Act 5 - The Summit (dusk; shorter fuse from checkpoint 21)

        /// <summary>Moss mounds launch the runners up the meadow; catch at the top of the jump (§13.8). Steps on the side are the slow way.</summary>
        static void AlpineMeadow(Transform p)
        {
            Floor(p, "Meadow", -12, 30);
            foreach (float x in new[] { -4f, 4f }) Place(p, ObstaclesDir + "LaunchPad", "Moss mound", new Vector3(x, 0, 26), Quaternion.identity);
            Step(p, "Meadow terrace", 30, 46, 4f);
            Step(p, "Meadow side step 1", 21, 24, 1f, 5, -9.5f);
            Step(p, "Meadow side step 2", 24, 27, 2f, 5, -9.5f);
            Step(p, "Meadow side step 3", 27, 30, 3f, 5, -9.5f);
            Step(p, "Meadow rise 1", 46, 49, 5.3f);
            Step(p, "Meadow rise 2", 49, 52, 6.6f);
            Floor(p, "High meadow", 52, 76, 8);
            Pass(p, "Apex catch", new Vector3(0, 1.5f, 18), new Vector3(-4, 4.5f, 26));
            Pass(p, "Meadow relay", new Vector3(0, 5.5f, 38), new Vector3(0, 8.1f, 50));
            CP(p, 21, new Vector3(0, 8, 70), 90);
            ActSound(p, 64);
        }

        /// <summary>Switchback paths up the mountainside with scree no-carry strips (§13.13); passes across the gully between the paths.</summary>
        static void Switchbacks(Transform p)
        {
            Floor(p, "Switchback foot", -12, 12);
            Ramp(p, "First path", new Vector3(-7, 0, 12), new Vector3(-7, 8, 32), 8);
            Floor(p, "Switchback landing", 32, 40, 8);
            Ramp(p, "Second path", new Vector3(7, 8, 40), new Vector3(7, 16, 60), 8);
            Floor(p, "Switchback top", 60, 84, 16);
            float Y1(float z) => (z - 12) * 8 / 20;
            float Y2(float z) => 8 + (z - 40) * 8 / 20;
            Zone(p, ZoneForbidden, "Scree", new Vector3(-7, Y1(22), 22), new Vector3(8, 3, 2));
            Zone(p, ZoneForbidden, "Scree", new Vector3(7, Y2(50), 50), new Vector3(8, 3, 2));
            Pass(p, "Path relay", new Vector3(-7, Y1(16) + 1.5f, 16), new Vector3(-7, Y1(26) + 1.5f, 26));
            Pass(p, "Across the gully", new Vector3(-2, 9.5f, 37), new Vector3(7, Y2(42) + 1.5f, 42));
            Pass(p, "Second path relay", new Vector3(7, Y2(46) + 1.5f, 46), new Vector3(7, Y2(56) + 1.5f, 56));
            Kill(p, new Vector3(0, -8, 36), new Vector3(24, 2, 48));
            CP(p, 22, new Vector3(0, 16, 68), 90);
            ActSound(p, 72);
        }

        /// <summary>A log ram over sun-baked stones that heat the fuse (§13.14, §13.11), rising stone pillars (§13.10). From the plant's furnace intake.</summary>
        static void BoulderRun(Transform p)
        {
            Floor(p, "Boulder field", -12, 60);
            Zone(p, ZoneHot, "Sun-baked stones", new Vector3(0, 0, 26), new Vector3(23, 4, 22));
            AddCrusher(p, "Log ram", new Vector3(0, 0, 24), 10, 3, 0.2f);
            AddMover(p, "Obstacle_Piston", "Rising pillar", new Vector3(-7, 4.5f, 44), new Vector3(-7, 1.2f, 44), new Vector3(4, 2, 4), 1, 0f, 0.4f);
            AddMover(p, "Obstacle_Piston", "Rising pillar", new Vector3(7, 4.5f, 44), new Vector3(7, 1.2f, 44), new Vector3(4, 2, 4), 1, 0.5f, 0.4f);
            Pass(p, "Ram timing", new Vector3(0, 1.5f, 19), new Vector3(0, 1.5f, 29), timed: true);
            Pass(p, "Pillar relay", new Vector3(0, 1.5f, 38), new Vector3(0, 1.5f, 48), timed: true);
            for (float z = 18; z < 44; z += 16)
                NatureKit.Rock(p, "Boulder", new Vector3(-9, 3, z), new Vector3(4, 6, 6), "Nature_RockFace");
            CP(p, 23, new Vector3(0, 0, 56), 0);
            ActSound(p, 60);
        }

        /// <summary>A narrow path up the ridge with a drop on both sides, then a timed vine ring and the last palisade (§13.15).</summary>
        static void KnifeEdge(Transform p)
        {
            Floor(p, "Ridge foot", -12, 10);
            Ramp(p, "Knife edge", new Vector3(0, 0, 10), new Vector3(0, 12, 40), 6);
            Floor(p, "Ridge top", 40, 76, 12);
            var gate = Gate(p, "Ridge vine ring", new Vector3(0, 12, 43), 2.5f, 8);
            Palisade(p, "Ridge gate", 49, 12, gate);
            float Y(float z) => (z - 10) * 12 / 30;
            Pass(p, "Edge relay", new Vector3(0, Y(16) + 1.5f, 16), new Vector3(0, Y(26) + 1.5f, 26));
            Pass(p, "Ridge ring", new Vector3(0, 13.5f, 39), new Vector3(0, 13.5f, 47), opening: 2.4f);
            Kill(p, new Vector3(0, -22, 26), new Vector3(24, 2, 40));
            CP(p, 24, new Vector3(0, 12, 41), 0);
            ActSound(p, 64);
        }

        /// <summary>The summit: the checkpoint arch (one clean pass through it, §13.17), a last relay and the beacon at the finish. From the plant's control room.</summary>
        static void SummitArch(Transform p)
        {
            Floor(p, "Summit", -12, 64);
            ArchCheckpoint(p, "CP_25", 25, new Vector3(0, 0, 14), FuseFor(25), new Vector3(0, 0, 9));
            var checkpoint = p.Find("CP_25").GetComponent<Checkpoint>();
            DressCheckpoint(p, checkpoint, new Vector3(3.8f, 0, 14));
            Pass(p, "Summit arch", new Vector3(0, 1.5f, 5), new Vector3(0, 1.5f, 14), opening: 4);
            Pass(p, "Summit relay", new Vector3(-3, 1.5f, 22), new Vector3(3, 1.5f, 31));
            Place(p, GameplayDir + "FinishZone", "FinishZone", new Vector3(0, 0, 36), Quaternion.identity);
            NatureKit.Campfire(p, new Vector3(0, 0, 46), null, 2.2f);
            foreach (float x in new[] { -8f, 8f }) NatureKit.Rock(p, "Summit cairn", new Vector3(x, 1.5f, 44), new Vector3(2, 3, 2), "Nature_StoneWall");
            ActSound(p, 52);
        }
    }
}
