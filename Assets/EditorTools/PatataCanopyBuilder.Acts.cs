using HotPatata;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using static HotPatata.Editor.BombObstacleKitBuilder;

namespace HotPatata.Editor
{
    /// <summary>
    /// The twenty-five sections of PatataCanopy (PROJECT_SPEC Â§15d). Each is built in its own space: +Z forward, x across, the floor
    /// of its start at y = 0; it starts on the previous junction deck (z -10..10) and ends on its own (z L-10..L+10). Each declares its
    /// contract: what it forces and the shortcuts its geometry locks (gaps of 14 m, faces over 3 m, walls up to the leaf roof).
    /// </summary>
    public static partial class PatataCanopyBuilder
    {
        // ================================================================== Act 1 - L'OrÃ©e (dawn): one system at a time

        /// <summary>Islands over the void: the first jumps and the first passes across a gap (Â§13.1).</summary>
        static void PremierPont(Transform p)
        {
            Deck(p, "Rope bridge", -2, 2, 10, 18, 0);
            Deck(p, "Island A", -5, 5, 18, 26, 0);
            Deck(p, "Island B", -5, 5, 30, 38, 1);
            Deck(p, "Island C", -5, 5, 42.5f, 50, 0);
            Pass(p, "First gap", new Vector3(0, 1.5f, 23), new Vector3(0, 2.5f, 34));
            Pass(p, "Second gap", new Vector3(0, 2.5f, 35), new Vector3(0, 1.5f, 46));
            Contract(p, "Jump the 4 and 4.5 m gaps between the islands: the receiver crosses first, turns and catches.");
            CP(p, 1, new Vector3(0, 0, 60));
        }

        /// <summary>
        /// The bramble screen (Â§13.18) beside a laser curtain: the bomb goes through the brambles, the runners through the lasers.
        /// Both walls reach the leaf roof, so the bomb has no other way.
        /// </summary>
        static void LeFilet(Transform p)
        {
            Deck(p, "Hall", -7, 7, 10, 74, 0);
            WallAcross(p, "Bramble wall", 32, 0, Roof, -10, 10, new Hole(-6, -1, 0, Roof), new Hole(2, 6, 0, PassageHeight));
            ScreenAcross(p, "Brambles", -6, -1, 32, 0, Roof);
            CurtainAcross(p, "Lasers", 2, 6, 32, 0, PassageHeight);
            WallAcross(p, "Bramble window wall", 52, 0, Roof, -10, 10, new Hole(-6, -2, 0, PassageHeight), new Hole(2, 6, 1, 4));
            CurtainAcross(p, "Lasers 2", -6, -2, 52, 0, PassageHeight);
            ScreenAcross(p, "Bramble window", 2, 6, 52, 1, 3);
            Pass(p, "Through the brambles", new Vector3(-3.5f, 1.5f, 28), new Vector3(-3.5f, 1.5f, 36), opening: 4.3f);
            Pass(p, "Through the bramble window", new Vector3(4, 1.5f, 48), new Vector3(4, 1.5f, 56), opening: 2.65f);
            Torches(p, -6.7f, 16, 64, step: 24);
            Contract(p, "The bomb crosses each wall through the brambles; the runners cross through the lasers (the carrier never can).",
                     LobLock("over the bramble wall", new Vector3(0, 0, 26), new Vector3(0, Roof, 32)),
                     LobLock("over the window wall", new Vector3(0, 0, 46), new Vector3(0, Roof, 52)));
            CP(p, 2, new Vector3(0, 0, 84));
        }

        /// <summary>A vine ring raises the bridge over a 14 m gap for 8 s (Â§13.15).</summary>
        static void AnneauDeLianes(Transform p)
        {
            Deck(p, "Ring deck", -8, 8, 10, 30, 0);
            Deck(p, "Far deck", -8, 8, 44, 54, 0);
            var ring = Ring(p, "Vine ring", new Vector3(0, 0, 22), 2.6f, 8, alongX: true);
            RisingBridge(p, "Vine bridge", 0, 30, 44, 0, 4, ring);
            Pass(p, "Through the vine ring", new Vector3(-5, 1.5f, 22), new Vector3(5, 1.5f, 22), opening: 1.82f);
            Contract(p, "Pass through the vine ring to raise the bridge, then cross within 8 s.",
                     GapLock("the gap", new Vector3(0, 0, 30), new Vector3(0, 0, 44)));
            CP(p, 3, new Vector3(0, 0, 64), -1);
        }

        /// <summary>
        /// The hands-free plate (Â§13.19): an empty-handed holder raises one bridge for the carrier; the carrier then throws back
        /// through the ring over the gap, which raises the second bridge for the holder.
        /// </summary>
        static void LaPlaque(Transform p)
        {
            Deck(p, "Plate deck", -8, 8, 10, 30, 0);
            Deck(p, "Far deck", -8, 8, 44, 62, 0);
            var plate = HandsFreePlate(p, "Hands-free plate", new Vector3(5, 0, 25.5f));
            RisingBridge(p, "Plate bridge", 5, 30, 44, 0, 4, plate);
            var back = Pass(p, "Back through the ring", new Vector3(-5, 1.5f, 45.5f), new Vector3(-5, 1.5f, 28.5f), PassCorridor.ArcKind.Low, opening: 1.82f);
            var ring = RingOnPass(p, "Return vine ring", back, 8);
            RisingBridge(p, "Ring bridge", -5, 30, 44, 0, 4, ring);
            Contract(p, "An empty-handed runner holds the plate while the carrier crosses; the carrier throws back through the ring " +
                        "to raise the holder's bridge.",
                     GapLock("the gap", new Vector3(0, 0, 30), new Vector3(0, 0, 44)));
            CP(p, 4, new Vector3(0, 0, 72));
        }

        /// <summary>A switch (Â§13.19): the hands-free plate beyond the lasers cuts them; the catch on the plate brings them back.</summary>
        static void LesLucioles(Transform p)
        {
            Deck(p, "Hall", -7, 7, 10, 70, 0);
            WallAcross(p, "Firefly wall", 40, 0, Roof, -10, 10, new Hole(-2, 2, 0, PassageHeight));
            var lasers = CurtainAcross(p, "Firefly lasers", -2, 2, 40, 0, PassageHeight);
            var plate = HandsFreePlate(p, "Firefly plate", new Vector3(0, 0, 47));
            SwitchFor(p, "Firefly switch", new Vector3(0, 0, 44), plate, false, lasers);
            Pass(p, "Through the cut lasers", new Vector3(0, 1.5f, 35), new Vector3(0, 1.5f, 45), opening: 3.2f);
            Torches(p, -6.7f, 14, 66);
            Contract(p, "A runner crosses the lasers and stands on the plate beyond to cut them; the bomb flies through; the catch on " +
                        "the plate brings the lasers back.",
                     LobLock("over the firefly wall", new Vector3(0, 0, 34), new Vector3(0, Roof, 40)));
            CP(p, 5, new Vector3(0, 0, 80));
        }

        // ================================================================== Act 2 - Les Ponts suspendus (noon): combined

        /// <summary>
        /// Two branches split by a bark wall up to the roof (Â§13.4): each branch has lasers where the other does not, so the bomb
        /// must change branch through the bramble windows, and the team must split.
        /// </summary>
        static void DeuxBranches(Transform p)
        {
            Deck(p, "West branch 1", -7, -0.5f, 10, 34, 0);
            Deck(p, "West branch 2", -7, -0.5f, 37.5f, 54, 0);
            Deck(p, "West branch 3", -7, -0.5f, 58, 86, 0);
            Deck(p, "East branch 1", 0.5f, 7, 10, 72, 0);
            for (int i = 0; i < 2; i++)
                Place(p, PlatformsDir + "Platform_Falling", "Rotten twig", new Vector3(3.75f, -0.25f, 74.5f + i * 4.5f), Quaternion.identity);
            Deck(p, "East branch 2", 0.5f, 7, 81, 86, 0);
            var windows = new[] { 23.5f, 43.5f, 63.5f };
            var holes = new Hole[windows.Length];
            for (int i = 0; i < windows.Length; i++) holes[i] = new Hole(windows[i] - 1.5f, windows[i] + 1.5f, 1, 3.6f);
            WallAlong(p, "Bark divide", 0, 0, Roof, 10, 86, holes);
            foreach (float z in windows)
            {
                ScreenAlong(p, "Bramble window", z - 1.5f, z + 1.5f, 0, 1, 2.6f);
                Pass(p, $"Window at {z}", new Vector3(-3.75f, 1.5f, z), new Vector3(3.75f, 1.5f, z), opening: 2.25f);
            }
            foreach (float z in new[] { 30f, 70f })
            {
                WallAcross(p, $"West lasers {z}", z, 0, Roof, -10, -0.5f, new Hole(-6.5f, -1, 0, PassageHeight));
                CurtainAcross(p, $"West laser curtain {z}", -6.5f, -1, z, 0, PassageHeight);
            }
            WallAcross(p, "East lasers", 50, 0, Roof, 0.5f, 10, new Hole(1, 6.5f, 0, PassageHeight));
            CurtainAcross(p, "East laser curtain", 1, 6.5f, 50, 0, PassageHeight);
            Torches(p, -6.7f, 14, 82, step: 24);
            Torches(p, 6.7f, 20, 68, step: 24);
            Contract(p, "One runner per branch: the bomb crosses to the other branch through a bramble window before each laser curtain.",
                     LobLock("over the divide", new Vector3(-3.75f, 0, 40), new Vector3(0, Roof, 40)));
            CP(p, 6, new Vector3(0, 0, 96));
        }

        /// <summary>
        /// Crossed drawbridges: the east runner's hands-free plate raises the west bridge; the west carrier (waiting on cold moss)
        /// crosses and throws through the ring in the divide, which raises the east bridge (Â§13.15, Â§13.14).
        /// </summary>
        static void PontLevisCroise(Transform p)
        {
            Deck(p, "West branch 1", -7, -0.5f, 10, 30, 0);
            Deck(p, "West branch 2", -7, -0.5f, 30 + 13.5f, 86, 0);
            Deck(p, "East branch 1", 0.5f, 7, 10, 50, 0);
            Deck(p, "East branch 2", 0.5f, 7, 50 + 13.5f, 86, 0);
            WallAlong(p, "Bark divide", 0, 0, Roof, 10, 86, new Hole(44.4f, 47.6f, 0.8f, 4f));
            var ring = Place(p, GateRing, "Divide ring", new Vector3(0, 2.4f, 46), Quaternion.Euler(0, 90, 0)).GetComponent<BombGate>();
            SetField(ring, "holdSeconds", v => v.floatValue = 8f);
            ScreenAlong(p, "Divide brambles", 44.4f, 47.6f, 0.35f, 0.8f, 3.2f);
            var plate = HandsFreePlate(p, "East plate", new Vector3(3.75f, 0, 38));
            RisingBridge(p, "West drawbridge", -3.75f, 30, 43.5f, 0, 4, plate);
            RisingBridge(p, "East drawbridge", 3.75f, 50, 63.5f, 0, 4, ring);
            Zone(p, ZoneCold, "Waiting moss", new Vector3(-3.75f, 0, 25), new Vector3(6, 3, 8));
            Pass(p, "Through the divide ring", new Vector3(-3.75f, 1.5f, 46), new Vector3(3.75f, 1.5f, 46), opening: 2.25f);
            foreach (float z in new[] { 14f, 50f, 72f }) NatureKit.Torch(p, new Vector3(-6.7f, 0, z), 7f, 13f);
            foreach (float z in new[] { 14f, 36f, 72f }) NatureKit.Torch(p, new Vector3(6.7f, 0, z), 7f, 13f);
            Contract(p, "The east runner holds the hands-free plate for the west carrier, who throws through the ring in the divide " +
                        "to raise the east bridge.",
                     GapLock("west gap", new Vector3(-3.75f, 0, 30), new Vector3(-3.75f, 0, 43.5f)),
                     GapLock("east gap", new Vector3(3.75f, 0, 50), new Vector3(3.75f, 0, 63.5f)),
                     LobLock("over the divide", new Vector3(-3.75f, 0, 40), new Vector3(0, Roof, 40)));
            CP(p, 7, new Vector3(0, 0, 96), -1);
        }

        /// <summary>Swinging branches over the void: a ride longer than the fuse, so the bomb changes hands while moving (Â§13.2).</summary>
        static void BranchesBalancoires(Transform p)
        {
            Deck(p, "Take-off deck", -8, 8, 10, 22, 0);
            Deck(p, "Middle island", -6, 6, 44, 52, 0);
            Deck(p, "Landing deck", -8, 8, 70, 74, 0);
            AddMover(p, "Platform_Moving", "Swinging branch 1", new Vector3(0, -0.5f, 25), new Vector3(0, -0.5f, 41), new Vector3(6, 1, 6), 2.2f, 0f, 0.25f);
            AddMover(p, "Platform_Moving", "Swinging branch 2", new Vector3(0, -0.5f, 55), new Vector3(0, -0.5f, 67), new Vector3(6, 1, 6), 2.2f, 0.5f, 0.25f);
            Pass(p, "To the rider", new Vector3(0, 1.5f, 19), new Vector3(0, 1.5f, 28), timed: true);
            Pass(p, "Rider to the island", new Vector3(0, 1.5f, 38), new Vector3(0, 1.5f, 48), timed: true);
            Pass(p, "Island to the rider", new Vector3(0, 1.5f, 50), new Vector3(0, 1.5f, 58), timed: true);
            Pass(p, "Rider to the landing", new Vector3(0, 1.5f, 64), new Vector3(0, 1.5f, 72), timed: true);
            Contract(p, "Ride the swinging branches across 22 and 18 m of void; a ride outlasts the fuse, so pass while moving.",
                     GapLock("first void", new Vector3(0, 0, 22), new Vector3(0, 0, 44)),
                     GapLock("second void", new Vector3(0, 0, 52), new Vector3(0, 0, 70)));
            CP(p, 8, new Vector3(0, 0, 84));
        }

        /// <summary>Rotten twigs in four lines: each falls once stepped on, so each runner takes a line and commits (Â§13.7).</summary>
        static void BranchesPourries(Transform p)
        {
            Deck(p, "Rotten edge", -9, 9, 10, 20, 0);
            Deck(p, "Sound wood", -9, 9, 52, 62, 0);
            foreach (float x in new[] { -6.75f, -2.25f, 2.25f, 6.75f })
                for (float z = 23.5f; z <= 48.6f; z += 5f)
                    Place(p, PlatformsDir + "Platform_Falling", "Rotten twig", new Vector3(x, -0.25f, z), Quaternion.identity);
            Pass(p, "Across the lines", new Vector3(-6.75f, 1.5f, 28.5f), new Vector3(2.25f, 1.5f, 38.5f));
            Pass(p, "Back across", new Vector3(2.25f, 1.5f, 38.5f), new Vector3(-2.25f, 1.5f, 48.5f));
            Contract(p, "Each runner commits to a line of rotten twigs (one use each); the bomb passes between the lines.",
                     GapLock("the void", new Vector3(0, 0, 20), new Vector3(0, 0, 52)));
            CP(p, 9, new Vector3(0, 0, 72));
        }

        /// <summary>
        /// The lock: a ring opens the door for everyone; the sap chamber burns the fuse twice as fast; at its end the runners take
        /// the lasers and the bomb the ring in the bramble window, which raises the last bridge (Â§13.15, Â§13.14, Â§13.18).
        /// </summary>
        static void Ecluse(Transform p)
        {
            Deck(p, "Lock floor", -7, 7, 10, 76, 0);
            var ring1 = Ring(p, "Lock ring", new Vector3(0, 0, 26), 2.6f, 8, alongX: true);
            WallAcross(p, "Lock gate wall", 40, 0, Roof, -10, 10, new Hole(-2, 2, 0, PassageHeight));
            Actuator(p, Door, "Lock gate", new Vector3(0, PassageHeight / 2, 40), new Vector3(4, PassageHeight, 0.8f), new Vector3(0, PassageHeight + 0.3f, 0), ring1);
            Zone(p, ZoneHot, "Sap heat", new Vector3(0, 0, 49), new Vector3(14, 3, 14));
            WallAcross(p, "Lock window wall", 70, 0, Roof, -10, 10, new Hole(2, 6, 0, PassageHeight), new Hole(-6, -2, 1, 4.2f));
            CurtainAcross(p, "Lock lasers", 2, 6, 70, 0, PassageHeight);
            var ring2 = Place(p, GateRing, "Window ring", new Vector3(-4, 2.6f, 70), Quaternion.identity).GetComponent<BombGate>();
            SetField(ring2, "holdSeconds", v => v.floatValue = 8f);
            ScreenAcross(p, "Window brambles", -6, -2, 70.35f, 1, 3.2f);
            RisingBridge(p, "Lock bridge", 0, 76, 90, 0, 4, ring2);
            Pass(p, "Lock ring", new Vector3(-5, 1.5f, 26), new Vector3(5, 1.5f, 26), opening: 1.82f);
            Pass(p, "Window ring", new Vector3(-4, 1.5f, 66), new Vector3(-4, 1.5f, 74), opening: 2.5f);
            Torches(p, -6.7f, 14, 74);
            Contract(p, "A pass through the ring opens the gate for 8 s; across the sap chamber the runners take the lasers and the bomb " +
                        "the ring in the bramble window, which raises the bridge.",
                     GapLock("the last gap", new Vector3(0, 0, 76), new Vector3(0, 0, 90)),
                     LobLock("over the gate wall", new Vector3(0, 0, 34), new Vector3(0, Roof, 40)),
                     LobLock("over the window wall", new Vector3(0, 0, 64), new Vector3(0, Roof, 70)));
            CP(p, 10, new Vector3(0, 0, 100));
        }

        // ================================================================== Act 3 - Le Grand ChÃªne (late afternoon): inside the hollow oak

        /// <summary>Bark walls of the hollow oak along both sides (not at the junctions) and its roof.</summary>
        static void OakShell(Transform p, float length, float floor, float roof)
        {
            foreach (int side in new[] { -1, 1 })
                Block(p, "Oak wall", new Vector3(side * 10.5f, (floor - 2f + roof) / 2f, length / 2f), new Vector3(1, roof - floor + 2f, length - 2 * Hub), KitRole.Wall);
            LeafRoof(p, "Oak roof", -CoverHalf, CoverHalf, -Hub, length + Hub, roof);
        }

        /// <summary>
        /// The pulleys: a hands-free plate below lifts the carrier; the carrier throws down to the holder (whose catch drops that lift)
        /// and stands on a plain plate above, which lifts the holder (Â§13.19, Â§13.3).
        /// </summary>
        static void LaPoulie(Transform p)
        {
            OakShell(p, 50, 0, 22);
            Deck(p, "Oak floor", -8, 8, 10, 34, 0);
            Deck(p, "Pulley ledge", -8, 8, 34, 40, 14);
            var plateA = HandsFreePlate(p, "Pulley plate below", new Vector3(-4, 0, 25));
            var liftA = Actuator(p, Lift, "Pulley lift A", new Vector3(-4, -0.25f, 31), new Vector3(5, 0.5f, 5), new Vector3(0, 14, 0), plateA);
            SetField(liftA.GetComponent<SignalActuator>(), "travelSeconds", v => v.floatValue = 3f);
            var plateB = Place(p, Plate, "Pulley plate above", new Vector3(4, 14, 37), Quaternion.identity).GetComponent<PressurePlate>();
            var liftB = Actuator(p, Lift, "Pulley lift B", new Vector3(4, -0.25f, 31), new Vector3(5, 0.5f, 5), new Vector3(0, 14, 0), plateB);
            SetField(liftB.GetComponent<SignalActuator>(), "travelSeconds", v => v.floatValue = 3f);
            Pass(p, "Down the pulley", new Vector3(2, 15.5f, 35), new Vector3(-4, 1.5f, 25));
            Torches(p, -7.3f, 12, 30, step: 9);
            Torches(p, 7.3f, 12, 30, step: 9);
            Contract(p, "An empty-handed runner holds the plate below to lift the carrier; the carrier throws down to them and holds the " +
                        "plate above to lift them up.",
                     ClimbLock("the oak's inner wall", new Vector3(0, 0, 34), new Vector3(0, 14, 34)));
            CP(p, 11, new Vector3(0, 14, 50), -1);
        }

        /// <summary>
        /// The hollow branch: the hall is roofed and its climb is behind lasers, so the bomb goes up only through the hollow branch
        /// (Â§13.16) while the receiver climbs; the carrier waits on cold moss (Â§13.14).
        /// </summary>
        static void TroncCreux(Transform p)
        {
            OakShell(p, 64, 0, 20);
            WallAcross(p, "Oak front", 10, 0, 20, -10, 10, new Hole(-6, -1, 0, PassageHeight));
            Deck(p, "Oak hall", -8, 2, 10, 41, 0);
            LeafRoof(p, "Hall roof", -10, 2.5f, 10, 41, 6);
            WallAcross(p, "Hall back", 41, 0, 6, -10, 2.5f);
            WallAlong(p, "Climb wall", 2.5f, 0, 12, 10.5f, 54, new Hole(12, 15, 0, PassageHeight));
            CurtainAlong(p, "Climb lasers", 12, 15, 2.5f, 0, PassageHeight);
            Deck(p, "Climb foot", 3, 8, 10.5f, 15, 0);
            Ramp(p, "Climb ramp 1", new Vector3(5.5f, 0, 15), new Vector3(5.5f, 6, 33), 5);
            Deck(p, "Climb landing", 3, 8, 33, 37, 6);
            Ramp(p, "Climb ramp 2", new Vector3(5.5f, 6, 37), new Vector3(5.5f, 12, 55), 5);
            Deck(p, "Upper hall", -8, 2.5f, 44, 54, 12);
            Zone(p, ZoneCold, "Waiting moss", new Vector3(-4, 0, 30), new Vector3(6, 3, 6));
            var tube = Place(p, Tube, "Hollow branch", Vector3.zero, Quaternion.identity);
            ConfigureTube(tube, new[] { new TubeSlot(new Vector3(-4, 1.6f, 37), 0, new Vector3(-4, 16, 49), new Vector3(-4, 12, 52), 0.9f) });
            TransitPasses(p, tube);
            Torches(p, -7.3f, 14, 38, step: 12);
            Contract(p, "The bomb goes up only through the hollow branch (the climb is behind lasers, the hall is roofed); the carrier " +
                        "waits on the cold moss until the receiver is up.",
                     ClimbLock("hall to upper hall", new Vector3(-4, 0, 41), new Vector3(-4, 12, 44)));
            CP(p, 12, new Vector3(0, 12, 64));
        }

        /// <summary>Three rising levels, each swept wall to wall by a lethal branch (Â§13.11): jump it while the bomb keeps moving.</summary>
        static void LaSpirale(Transform p)
        {
            OakShell(p, 56, 0, 15.5f);
            Solid(p, "Spiral level 0", -6, 6, 10, 22, 0);
            Solid(p, "Spiral level 1", -6, 6, 22, 34, 2.5f);
            Solid(p, "Spiral level 2", -6, 6, 34, 46, 5f);
            float[] y = { 0, 2.5f, 5f };
            float[] z = { 16, 28, 40 };
            for (int i = 0; i < 3; i++)
                AddRotator(p, ObstaclesDir + "Obstacle_Sweeper", "Sweeping branch " + (i + 1), new Vector3(0, y[i], z[i]), 16.8f, i % 2 == 0 ? 80f : -80f, i * 60f);
            Pass(p, "Up a level", new Vector3(-3, 1.5f, 19), new Vector3(3, 4f, 29), timed: true);
            Pass(p, "Up again", new Vector3(3, 4f, 31), new Vector3(-3, 6.5f, 41), timed: true);
            for (int i = 0; i < 3; i++) NatureKit.Torch(p, new Vector3(-5.6f, y[i], z[i] + 4f), 7f, 13f);
            Contract(p, "Every level is swept wall to wall: jump the branch, climb, keep passing.");
            CP(p, 13, new Vector3(0, 7.5f, 56));
        }

        /// <summary>
        /// A real choice (Â§13.14): a short gallery that burns the fuse twice as fast, or a long cold one weaving round baffles; bramble
        /// windows join them.
        /// </summary>
        static void LesGaleries(Transform p)
        {
            Deck(p, "Short gallery", -7, -0.5f, 10, 86, 0);
            Deck(p, "Long gallery", 0.5f, 7, 10, 86, 0);
            var windows = new[] { 27f, 48f, 69f };
            var holes = new Hole[windows.Length];
            for (int i = 0; i < windows.Length; i++) holes[i] = new Hole(windows[i] - 1.5f, windows[i] + 1.5f, 1, 3.6f);
            WallAlong(p, "Gallery divide", 0, 0, Roof, 10, 86, holes);
            foreach (float z in windows)
            {
                ScreenAlong(p, "Gallery window", z - 1.5f, z + 1.5f, 0, 1, 2.6f);
                Pass(p, $"Gallery window {z}", new Vector3(-3.75f, 1.5f, z), new Vector3(3.75f, 1.5f, z), opening: 2.25f);
            }
            Zone(p, ZoneHot, "Ember gallery", new Vector3(-3.75f, 0, 48), new Vector3(6.5f, 3, 72));
            Zone(p, ZoneCold, "Dew gallery", new Vector3(3.75f, 0, 48), new Vector3(6.5f, 3, 72));
            float[] baffles = { 18, 36, 42, 57, 62, 78 };
            for (int i = 0; i < baffles.Length; i++)
            {
                if (i % 2 == 0) WallAcross(p, "Baffle", baffles[i], 0, Roof, 0.5f, 5.5f);
                else WallAcross(p, "Baffle", baffles[i], 0, Roof, 2f, 10f);
            }
            Torches(p, -6.7f, 14, 82, step: 22);
            Contract(p, "Choose: the short ember gallery (fuse x2, pass constantly) or the long dew gallery (fuse x0.5, weave the baffles).",
                     LobLock("over the divide", new Vector3(-3.75f, 0, 40), new Vector3(0, Roof, 40)));
            CP(p, 14, new Vector3(0, 0, 96));
        }

        /// <summary>The heart: a shuttle across a 14 m pit, then one clean pass through the arch to a teammate on the checkpoint (Â§13.17).</summary>
        static void CoeurDuChene(Transform p)
        {
            Deck(p, "Heart rim", -8, 8, 10, 26, 0);
            Deck(p, "Heart floor", -8, 8, 40, 54, 0);
            AddMover(p, "Platform_Moving", "Heart shuttle", new Vector3(0, -0.5f, 29), new Vector3(0, -0.5f, 37), new Vector3(6, 1, 6), 2f, 0f, 0.3f);
            ArchCheckpoint(p, "CP_15", 15, new Vector3(0, 0, 64), FuseFor(15), new Vector3(0, 0, 57));
            NatureKit.CampfireCheckpoint(p, p.Find("CP_15").GetComponent<Checkpoint>(), new Vector3(-3.8f, 0, 64));
            Pass(p, "To the shuttle", new Vector3(0, 1.5f, 22), new Vector3(0, 1.5f, 31), timed: true);
            Pass(p, "Heart arch", new Vector3(0, 1.5f, 52), new Vector3(0, 1.5f, 62), opening: 3f);
            foreach (float z in new[] { 14f, 50f }) NatureKit.Torch(p, new Vector3(-6.7f, 0, z), 7f, 13f);
            Contract(p, "Ride the shuttle over the pit; the checkpoint only counts once the bomb flew through the arch.",
                     GapLock("the pit", new Vector3(0, 0, 26), new Vector3(0, 0, 40)));
        }

        // ================================================================== Act 4 - La Cime dans le vent (sunset, 5 s fuse): speed

        /// <summary>Leaf trampolines up two 5 m faces; the pads stand in spores, so the bomb is thrown up while the runners fly (Â§13.3).</summary>
        static void FeuillesTrampolines(Transform p)
        {
            Deck(p, "Leaf floor", -8, 8, 10, 26, 0);
            Solid(p, "First crown tier", -8, 8, 26, 40, 5);
            Solid(p, "Second crown tier", -8, 8, 40, 54, 10);
            foreach (float x in new[] { -4f, 4f })
            {
                SporePad(p, "Leaf trampoline", new Vector3(x, 0, 23));
                SporePad(p, "Upper leaf trampoline", new Vector3(x, 5, 37));
            }
            Pass(p, "Up to the first tier", new Vector3(0, 1.5f, 18), new Vector3(0, 6.5f, 30));
            Pass(p, "Up to the crown", new Vector3(0, 6.5f, 32), new Vector3(0, 11.5f, 46));
            Contract(p, "Only empty hands fly: the pads stand in spores, so the bomb is thrown up each tier to a runner already there.",
                     ClimbLock("first face", new Vector3(0, 0, 26), new Vector3(0, 5, 26)),
                     ClimbLock("second face", new Vector3(0, 5, 40), new Vector3(0, 10, 40)));
            CP(p, 16, new Vector3(0, 10, 64));
        }

        /// <summary>A rolling log against you, a shuttle over 14 m, then two log drives running opposite ways (Â§13.9).</summary>
        static void BranchesMouvantes(Transform p)
        {
            Deck(p, "Wind deck", -8, 8, 10, 20, 0);
            AddConveyor(p, "Rolling log", new Vector3(0, -0.5f, 30), new Vector3(4, 1, 20), -3);
            AddMover(p, "Platform_Moving", "Gust shuttle", new Vector3(0, -0.5f, 43), new Vector3(0, -0.5f, 51), new Vector3(6, 1, 6), 2.5f, 0f, 0.3f);
            Deck(p, "Gust island", -6, 6, 54, 60, 0);
            AddConveyor(p, "West log drive", new Vector3(-4, -0.5f, 70), new Vector3(3, 1, 20), 3);
            AddConveyor(p, "East log drive", new Vector3(4, -0.5f, 70), new Vector3(3, 1, 20), -3);
            Pass(p, "Up the rolling log", new Vector3(0, 1.5f, 22), new Vector3(0, 1.5f, 32), timed: true);
            Pass(p, "To the shuttle", new Vector3(0, 1.5f, 38), new Vector3(0, 1.5f, 46), timed: true);
            Pass(p, "Across the drives", new Vector3(-4, 1.5f, 66), new Vector3(4, 1.5f, 74), timed: true);
            Contract(p, "Lead the receiver on the moving logs; the shuttle is the only way over the 14 m gust gap.",
                     GapLock("gust gap", new Vector3(0, 0, 40), new Vector3(0, 0, 54)));
            CP(p, 17, new Vector3(0, 0, 90));
        }

        /// <summary>A slalom of four walls: the runners zig-zag through the lasers, the bomb flies straight through the brambles (Â§13.18).</summary>
        static void CouloirDeRonces(Transform p)
        {
            Deck(p, "Bramble corridor", -7, 7, 10, 74, 0);
            var locks = new SectionContract.Shortcut[4];
            float[] walls = { 24, 36, 48, 60 };
            for (int i = 0; i < walls.Length; i++)
            {
                float z = walls[i];
                float a = i % 2 == 0 ? 3f : -6.5f, b = i % 2 == 0 ? 6.5f : -3f;
                WallAcross(p, $"Bramble baffle {i + 1}", z, 0, Roof, -10, 10, new Hole(-1.5f, 1.5f, 0, Roof), new Hole(a, b, 0, PassageHeight));
                ScreenAcross(p, $"Baffle brambles {i + 1}", -1.5f, 1.5f, z, 0, Roof);
                CurtainAcross(p, $"Baffle lasers {i + 1}", a, b, z, 0, PassageHeight);
                Pass(p, $"Through baffle {i + 1}", new Vector3(0, 1.5f, z - 5), new Vector3(0, 1.5f, z + 5), opening: 2.3f);
                locks[i] = LobLock($"over baffle {i + 1}", new Vector3(0, 0, z - 6), new Vector3(0, Roof, z));
            }
            Torches(p, -6.7f, 14, 70, step: 12);
            Contract(p, "Leapfrog: a runner passes the lasers, the bomb follows straight through the brambles, four times on a 5 s fuse.", locks);
            CP(p, 18, new Vector3(0, 0, 84));
        }

        /// <summary>
        /// The seed catapult: 70 m of void under a low branch roof (no throw reaches across), the shuttle carries spores (no carrier
        /// rides it); the catapult holds the bomb 5 s before firing it to the far pad, the time the receiver has to ride over (Â§13.16).
        /// </summary>
        static void CanonAGraines(Transform p)
        {
            Deck(p, "Catapult deck", -8, 8, 10, 26, 0);
            Deck(p, "Far landing", -8, 8, 96, 100, 0);
            Zone(p, ZoneCold, "Waiting moss", new Vector3(-4, 0, 18), new Vector3(6, 3, 6));
            var shuttle = AddMover(p, "Platform_Moving", "Spore shuttle", new Vector3(0, -0.5f, 29), new Vector3(0, -0.5f, 93), new Vector3(6, 1, 6), 11f, 0f, 0.3f);
            var spores = Zone(p, ZoneForbidden, "Shuttle spores", new Vector3(0, 0, 29), new Vector3(6, 3, 6));
            spores.transform.SetParent(shuttle.GetComponent<MovingPlatform>().Platform, true);
            var catapult = Place(p, Cannon, "Seed catapult", new Vector3(4, 0, 22), Quaternion.identity);
            ConfigureCannon(catapult, new Vector3(4, 0, 98), 1.3f);
            SetField(catapult.GetComponent<BombTransit>(), "delay", v => v.floatValue = 5f);
            TransitPasses(p, catapult);
            Contract(p, "Only the catapult crosses with the bomb: no throw reaches 70 m under the branch roof, and the shuttle carries " +
                        "spores. Throw it in as the receiver boards; it fires 5 s later.",
                     GapLock("the void", new Vector3(0, 0, 26), new Vector3(0, 0, 96)));
            CP(p, 19, new Vector3(0, 0, 110), -1);
        }

        /// <summary>Race the ring: the pass opens the gate 50 m away for 7 s, over gaps and steps (Â§13.15).</summary>
        static void CourseContreLAnneau(Transform p)
        {
            Deck(p, "Start deck", -8, 8, 10, 24, 0);
            var ring = Ring(p, "Race ring", new Vector3(0, 0, 18), 2.6f, 7, alongX: true);
            Deck(p, "Branch 1", -4, 4, 28, 34, 0);
            Deck(p, "Branch 2", -4, 4, 38, 44, 2);
            Deck(p, "Branch 3", -4, 4, 48, 54, 2);
            Deck(p, "Branch 4", -4, 4, 58, 64, 4);
            Deck(p, "Gate deck", -8, 8, 66, 74, 4);
            WallAcross(p, "Race gate wall", 70, 4, 6, -11, 11, new Hole(-2, 2, 0, 4));
            Actuator(p, Door, "Race gate", new Vector3(0, 6, 70), new Vector3(4, 4, 0.8f), new Vector3(4.2f, 0, 0), ring);
            Pass(p, "Race ring", new Vector3(-5, 1.5f, 18), new Vector3(5, 1.5f, 18), opening: 1.82f);
            Pass(p, "Race relay", new Vector3(0, 3.5f, 44), new Vector3(0, 5.5f, 58));
            Contract(p, "The pass through the ring opens the gate 50 m away for 7 s: sprint, jump, keep the bomb moving.",
                     ClimbLock("the gate wall", new Vector3(0, 4, 69), new Vector3(0, 10, 69)));
            CP(p, 20, new Vector3(0, 4, 84));
        }

        // ================================================================== Act 5 - Le Sommet (dusk, 4.5 s fuse): twisted

        /// <summary>The ring parts the bramble hedge for 6 s (a switch, Â§13.19); beyond it, lasers for the runners, a window for the bomb.</summary>
        static void HaieQuiSouvre(Transform p)
        {
            Deck(p, "Hedge hall", -7, 7, 10, 74, 0);
            var ring = Ring(p, "Hedge ring", new Vector3(0, 0, 28), 2.6f, 6, alongX: true);
            WallAcross(p, "Hedge frame", 40, 0, Roof, -10, 10, new Hole(-7, 7, 0, Roof));
            var hedge = ScreenAcross(p, "Bramble hedge", -7, 7, 40, 0, Roof);
            SwitchFor(p, "Hedge switch", new Vector3(0, 0, 38), ring, false, hedge.transform.Find("Screen").gameObject);
            WallAcross(p, "Hedge lasers wall", 56, 0, Roof, -10, 10, new Hole(2, 6, 0, PassageHeight), new Hole(-6, -2, 1, 4));
            CurtainAcross(p, "Hedge lasers", 2, 6, 56, 0, PassageHeight);
            ScreenAcross(p, "Hedge window", -6, -2, 56, 1, 3);
            Pass(p, "Hedge ring", new Vector3(-5, 1.5f, 28), new Vector3(5, 1.5f, 28), opening: 1.82f);
            Pass(p, "Hedge window", new Vector3(-4, 1.5f, 52), new Vector3(-4, 1.5f, 60), opening: 2.65f);
            Torches(p, -6.7f, 14, 70);
            Contract(p, "The ring parts the hedge for 6 s: everyone through, then the runners take the lasers and the bomb the bramble window.",
                     LobLock("over the hedge", new Vector3(0, 0, 34), new Vector3(0, Roof, 40)),
                     LobLock("over the lasers wall", new Vector3(0, 0, 50), new Vector3(0, Roof, 56)));
            CP(p, 21, new Vector3(0, 0, 84));
        }

        /// <summary>
        /// The spore bridge: 48 m of spores under a low branch (no throw reaches across); the hands-free plate at the far end clears
        /// them; the catch on the plate brings them back (Â§13.19).
        /// </summary>
        static void PontDesSpores(Transform p)
        {
            Deck(p, "Spore landing", -6, 6, 10, 22, 0);
            Deck(p, "Spore bridge", -2, 2, 22, 70, 0);
            Deck(p, "Far landing", -6, 6, 70, 86, 0);
            Zone(p, ZoneCold, "Waiting moss", new Vector3(-3.5f, 0, 16), new Vector3(5, 3, 6));
            var spores = Zone(p, ZoneForbidden, "Bridge spores", new Vector3(0, 0, 46), new Vector3(4, 3, 48));
            var plate = HandsFreePlate(p, "Spore plate", new Vector3(0, 0, 74));
            SwitchFor(p, "Spore switch", new Vector3(0, 0, 72), plate, false, spores);
            LeafRoof(p, "Low branch", -8, 8, 20, 72, 4.5f);
            Pass(p, "Spore relay", new Vector3(0, 1.5f, 52), new Vector3(0, 1.5f, 73), PassCorridor.ArcKind.Low);
            Torches(p, -5.7f, 74, 84, step: 10);
            Contract(p, "A runner crosses the spores to the plate; the carrier runs the cleared bridge and throws to the holder before " +
                        "the catch brings the spores back. No throw reaches 48 m under the low branch.");
            CP(p, 22, new Vector3(0, 0, 96));
        }

        /// <summary>Down the great branch: bramble hedges leave a gap only a slider fits under, and it holds spores (Â§13.12, Â§13.18).</summary>
        static void Glissade(Transform p)
        {
            Deck(p, "Branch top", -7, 7, 10, 16, 0);
            Ramp(p, "Great branch", new Vector3(0, 0, 16), new Vector3(0, -24, 64), 10, true);
            Deck(p, "Branch foot", -7, 7, 64, 86, -24);
            float Y(float z) => -(z - 16) * 24 / 48;
            foreach (float z in new[] { 30f, 48f })
            {
                var hedge = Place(p, BombObstacleKitBuilder.Screen, "Slide hedge", new Vector3(0, Y(z) + 1.3f, z), Quaternion.identity);
                ResizeScreen(hedge, 10f, 4f);
                Zone(p, ZoneForbidden, "Hedge gap spores", new Vector3(0, Y(z) - 0.5f, z), new Vector3(10, 3f, 2));
            }
            Pass(p, "Slide relay", new Vector3(-2, Y(24) + 1.5f, 24), new Vector3(2, Y(36) + 1.5f, 36));
            Pass(p, "Slide relay 2", new Vector3(2, Y(42) + 1.5f, 42), new Vector3(-2, Y(54) + 1.5f, 54));
            Contract(p, "Only a slider fits under each hedge, and its gap holds spores: the bomb flies over or through to a slider ahead.");
            CP(p, 23, new Vector3(0, -24, 96), -1);
        }

        /// <summary>
        /// The final lock: brambles and lasers, a sap chamber, spore trampolines up two faces, the hollow branch for the bomb, a ring
        /// that raises the last bridge for 6 s.
        /// </summary>
        static void EcluseFinale(Transform p)
        {
            LeafRoof(p, "Low roof", -CoverHalf, CoverHalf, -Hub, 30, Roof);
            LeafRoof(p, "High roof", -CoverHalf, CoverHalf, 30, 104 + Hub, 17);
            WallAcross(p, "Roof step", 30, Roof, 10, -CoverHalf, CoverHalf);
            Deck(p, "Final floor", -7, 7, 10, 60, 0);
            WallAcross(p, "Final bramble wall", 30, 0, Roof, -10, 10, new Hole(2, 6, 0, PassageHeight), new Hole(-6, -2, 1, 4));
            CurtainAcross(p, "Final lasers", 2, 6, 30, 0, PassageHeight);
            ScreenAcross(p, "Final window", -6, -2, 30, 1, 3);
            Zone(p, ZoneHot, "Sap heat", new Vector3(0, 0, 45), new Vector3(14, 3, 26));
            Solid(p, "Final tier 1", -7, 7, 60, 66, 5);
            Solid(p, "Final tier 2", -7, 7, 66, 80, 10);
            foreach (float x in new[] { -5f, 5f })
            {
                SporePad(p, "Final trampoline", new Vector3(x, 0, 57));
                SporePad(p, "Upper final trampoline", new Vector3(x, 5, 63));
            }
            var tube = Place(p, Tube, "Final hollow branch", Vector3.zero, Quaternion.identity);
            ConfigureTube(tube, new[] { new TubeSlot(new Vector3(0, 1.6f, 50), 0, new Vector3(0, 14, 72), new Vector3(0, 10, 76), 0.9f) });
            TransitPasses(p, tube);
            var ring = Ring(p, "Final ring", new Vector3(0, 10, 70), 2.6f, 6, alongX: true);
            RisingBridge(p, "Final bridge", 0, 80, 94, 10, 4, ring);
            Pass(p, "Final window", new Vector3(-4, 1.5f, 26), new Vector3(-4, 1.5f, 34), opening: 2.65f);
            Pass(p, "Final ring", new Vector3(-5, 11.5f, 70), new Vector3(5, 11.5f, 70), opening: 1.82f);
            Torches(p, -6.7f, 14, 56, step: 14);
            Contract(p, "Everything at once on a 4.5 s fuse: window, sap, spore trampolines, the hollow branch, the ring and the bridge.",
                     LobLock("over the bramble wall", new Vector3(0, 0, 24), new Vector3(0, Roof, 30)),
                     ClimbLock("first face", new Vector3(0, 0, 60), new Vector3(0, 5, 60)),
                     ClimbLock("second face", new Vector3(0, 5, 66), new Vector3(0, 10, 66)),
                     GapLock("the last gap", new Vector3(0, 10, 80), new Vector3(0, 10, 94)));
            CP(p, 24, new Vector3(0, 10, 104));
        }

        /// <summary>The summit: a shuttle over the last gap, one clean pass through the arch, the finish and the beacon (Â§13.17).</summary>
        static void ArcheDuSommet(Transform p)
        {
            Deck(p, "Last deck", -8, 8, 10, 30, 0);
            AddMover(p, "Platform_Moving", "Summit shuttle", new Vector3(0, -0.5f, 33), new Vector3(0, -0.5f, 41), new Vector3(6, 1, 6), 2f, 0f, 0.3f);
            Deck(p, "Summit", -10, 10, 44, 84, 0);
            ArchCheckpoint(p, "CP_25", 25, new Vector3(0, 0, 54), FuseFor(25), new Vector3(0, 0, 48));
            NatureKit.CampfireCheckpoint(p, p.Find("CP_25").GetComponent<Checkpoint>(), new Vector3(3.8f, 0, 54));
            Place(p, GameplayDir + "FinishZone", "FinishZone", new Vector3(0, 0, 68), Quaternion.identity);
            NatureKit.Campfire(p, new Vector3(0, 0, 78), null, 2.2f);
            Pass(p, "Summit arch", new Vector3(0, 1.5f, 42), new Vector3(0, 1.5f, 52), timed: true, opening: 3.2f);
            Contract(p, "Ride the last shuttle; one clean pass through the arch to a teammate on the checkpoint.",
                     GapLock("the last gap", new Vector3(0, 0, 30), new Vector3(0, 0, 44)));
        }
    }
}
