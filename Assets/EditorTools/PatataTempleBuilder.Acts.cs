using HotPatata;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using static HotPatata.Editor.BombObstacleKitBuilder;

namespace HotPatata.Editor
{
    /// <summary>
    /// The fifteen sections of PatataTemple (PROJECT_SPEC §15e), each in its own space: +Z forward, x across, the floor of its start
    /// at y = 0; it starts on the previous junction deck (z -10..10) and ends on its own (z L-10..L+10). Each declares its contract:
    /// what it forces, why two players fail, and the shortcuts its geometry locks (gaps of 14 m, faces of 5 m, walls up to the roof).
    /// The grammar for three (§15e): a holder never stands where the carrier can throw to them, a carrier alone outlasts the fuse,
    /// and a holder left behind is let through by someone already across.
    /// </summary>
    public static partial class PatataTempleBuilder
    {
        // ================================================================== Act 1 - La Chaussée (misty dawn): the causeway, one system at a time

        /// <summary>
        /// The heavy slab (§13.21): two empty-handed players raise the bridge for the carrier, who throws back through the ring over
        /// the gap to raise the second bridge for the two.
        /// </summary>
        static void DalleLourde(Transform p)
        {
            Deck(p, "Near terrace", -8, 8, 10, 30, 0);
            Deck(p, "Far terrace", -8, 8, 44, 54, 0);
            var heavy = HeavyPlate(p, "Heavy slab", new Vector3(4, 0, 22));
            RisingBridge(p, "Slab bridge", 4, 30, 44, 0, 4, heavy);
            var back = Pass(p, "Back through the ring", new Vector3(-4, 1.5f, 47), new Vector3(-4, 1.5f, 26), PassCorridor.ArcKind.Low, opening: 1.82f);
            var ring = RingOnPass(p, "Return ring", back, 8);
            RisingBridge(p, "Ring bridge", -4, 30, 44, 0, 4, ring);
            Trio(p, "Two empty-handed players stand on the heavy slab to raise the bridge; the carrier crosses, then throws back through the ring " +
                    "over the gap, which raises the second bridge for the two.",
                 "only one of you is empty-handed, so the slab never lifts (a throw in the air still belongs to its thrower), and nobody " +
                 "reaches the far side to throw through the ring.",
                 GapLock("the gap", new Vector3(0, 0, 30), new Vector3(0, 0, 44)));
            CP(p, 1, new Vector3(0, 0, 64));
        }

        /// <summary>
        /// The relay (§13.22): the holder in the alcove raises the herse for the pair; inside, an empty hand taps the hourglass plate,
        /// whose herse lets the holder in while the sand runs. The alcove faces away: nobody throws to its holder.
        /// </summary>
        static void LaReleve(Transform p)
        {
            Deck(p, "Hall", -8, 8, 10, 74, 0);
            WallAlong(p, "Alcove side", -2, 0, Roof, 10, 24);
            WallAcross(p, "Alcove back", 24, 0, Roof, -8.5f, -1.5f);
            var alcove = HandsFreePlate(p, "Alcove plate", new Vector3(-5, 0, 17));
            WallAcross(p, "Relay wall", 34, 0, Roof, -10, 10, new Hole(2, 6, 0, PassageHeight), new Hole(-7, -3, 0, PassageHeight));
            Herse(p, "Relay herse", 2, 6, 34, 0, alcove);
            var hourglass = HourglassPlate(p, "Hourglass plate", new Vector3(-5, 0, 42), 6f);
            Herse(p, "Return herse", -7, -3, 34, 0, hourglass);
            Torches(p, 7.3f, 14, 70, step: 14);
            Trio(p, "The holder in the alcove raises the herse; the pair takes the bomb through; inside, an empty hand taps the hourglass " +
                    "plate, whose herse stays up six seconds to let the holder in.",
                 "the holder stands in the alcove, out of any throw, and the carrier is alone behind the wall: the hourglass plate only " +
                 "counts empty hands and the bomb has no way back.",
                 LobLock("over the relay wall", new Vector3(0, 0, 28), new Vector3(0, Roof, 34)));
            CP(p, 2, new Vector3(0, 0, 84));
        }

        /// <summary>
        /// The pivot (§13.24): the holder in the shrine turns the slab to the east dock; the pair boards it; when the holder lets go the
        /// slab turns back and carries the pair, passing, into the enclosure; from there a throw through the ring raises the bridge
        /// that brings the holder in.
        /// </summary>
        static void LePivot(Transform p)
        {
            Deck(p, "West walk", -30, -10, 2, 10, 0);
            Deck(p, "West ledge", -30, -21, 10, 70, 0);
            Deck(p, "East walk", 10, 26, 2, 10, 0);
            Deck(p, "East dock", 17, 26, 10, 46, 0);
            Deck(p, "Enclosure", -6, 6, 56.5f, 80, 0);
            WallAcross(p, "Enclosure front", 56, 0, Roof, -7, 7, new Hole(-3.5f, 3.5f, 0, PassageHeight));
            WallAlong(p, "Enclosure west", -6.5f, 0, Roof, 56, 80, new Hole(62, 66, 0, PassageHeight));
            WallAlong(p, "Enclosure east", 6.5f, 0, Roof, 56, 80);
            Shrine(p, "Pivot shrine", new Vector3(-25.5f, 0, 30), 4f, -1);
            var plate = HandsFreePlate(p, "Shrine plate", new Vector3(-25.5f, 0, 30));
            Pivot(p, "Pivot slab", new Vector3(0, 0, 40), new Vector3(3, 0.5f, 32), 0, 90, 12, plate);
            var back = Pass(p, "Back to the ledge", new Vector3(-3, 1.5f, 64), new Vector3(-25, 1.5f, 64), PassCorridor.ArcKind.Low, opening: 1.82f);
            var ring = RingOnPass(p, "Ledge ring", back, 8);
            RisingBridgeBetween(p, "Ledge bridge", new Vector3(-21, 0, 64), new Vector3(-6, 0, 64), 4, ring);
            Pass(p, "Across the dock", new Vector3(21.5f, 1.5f, 24), new Vector3(21.5f, 1.5f, 36));
            Torches(p, 5.6f, 60, 78, step: 18);
            StoneRoof(p, "West ledge roof", -31, -CoverHalf, Hub, 72, Roof);
            StoneRoof(p, "East dock roof", CoverHalf, 27, Hub, 48, Roof);
            Trio(p, "The holder in the shrine turns the slab to the east dock; the pair boards; let go, the slab turns back for twelve seconds " +
                    "and carries the pair, passing, into the enclosure; their throw through the ring raises the holder's bridge.",
                 "the holder in the shrine is out of any throw, so the carrier rides alone for longer than a fuse (the enclosure's door only " +
                 "meets the slab at the end), and nobody is across to throw through the ring.",
                 GapLock("the south void", new Vector3(0, 0, 10), new Vector3(0, 0, 24)),
                 GapLock("the ledge to the enclosure", new Vector3(-21, 0, 64), new Vector3(-6, 0, 64)),
                 GapLock("the dock to the enclosure", new Vector3(17, 0, 46), new Vector3(6, 0, 56.5f)),
                 LobLock("over the enclosure", new Vector3(14, 0, 50), new Vector3(6.5f, Roof, 60)));
            CP(p, 3, new Vector3(0, 0, 90), -1);
        }

        /// <summary>
        /// The eye of the sun (§13.23): a rider on the shuttle cuts the beam over the chasm, which raises the herse for the pair; the
        /// rider reaches the far ledge and joins them through the lasers (the bomb never can).
        /// </summary>
        static void OeilDuSoleil(Transform p)
        {
            Deck(p, "Hall", -8, 8, 10, 30, 0);
            Deck(p, "Antechamber", 0.5f, 8, 30, 84, 0);
            Deck(p, "Far ledge", -8, -0.5f, 74.5f, 82, 0);
            WallAlong(p, "Divide", 0, 0, Roof, 30, 84, new Hole(77, 80.5f, 0, PassageHeight));
            CurtainAlong(p, "Ledge lasers", 77, 80.5f, 0, 0, PassageHeight);
            WallAcross(p, "Ledge end", 82.5f, 0, Roof, -10, -0.5f);
            WallAcross(p, "Herse wall", 30, 0, Roof, 0.5f, 10, new Hole(2, 6, 0, PassageHeight));
            AddMover(p, "Platform_Moving", "Sun shuttle", new Vector3(-4, -0.5f, 33), new Vector3(-4, -0.5f, 71.5f), new Vector3(6, 1, 6), 2f, 0f, 0.25f);
            var beam = Beam(p, "Sun beam", new Vector3(-4, 1.1f, 37), Vector3.forward, 16);
            Herse(p, "Sun herse", 2, 6, 30, 0, beam);
            Torches(p, 7.3f, 36, 80, step: 22);
            Trio(p, "A rider on the shuttle cuts the sun beam over the chasm and the herse rises while the light is cut: the pair takes the bomb " +
                    "through then; the rider reaches the far ledge and joins them through the lasers.",
                 "the rider is the only one who can cut the beam, so the carrier goes through alone and waits longer than a fuse; riding " +
                 "together leaves the bomb on the far ledge, behind lasers it never crosses.",
                 GapLock("the chasm", new Vector3(-4, 0, 30), new Vector3(-4, 0, 74.5f)),
                 LobLock("over the herse wall", new Vector3(4, 0, 24), new Vector3(4, Roof, 30)),
                 LobLock("over the divide", new Vector3(-4, 0, 60), new Vector3(0, Roof, 60)),
                 LobLock("over the ledge end", new Vector3(-4, 0, 78), new Vector3(-4, Roof, 82.5f)));
            CP(p, 4, new Vector3(0, 0, 94));
        }

        /// <summary>
        /// The threshold: a heavy slab with an hourglass of four seconds raises the great herse 26 m away; everyone lets go together
        /// and runs; then one clean pass through the arch to a teammate on the checkpoint (§13.17).
        /// </summary>
        static void LeSeuil(Transform p)
        {
            Deck(p, "Forecourt", -8, 8, 10, 58, 0);
            var heavy = HeavyPlate(p, "Threshold slab", new Vector3(0, 0, 18));
            MakeHourglass(heavy.gameObject, 4f);
            WallAcross(p, "Great wall", 44, 0, 6, -12, 12, new Hole(-2, 2, 0, PassageHeight));
            Herse(p, "Great herse", -2, 2, 44, 0, heavy);
            ArchCP(p, 5, new Vector3(0, 0, 72), new Vector3(0, 0, 65));
            Pass(p, "Through the arch", new Vector3(0, 1.5f, 56), new Vector3(0, 1.5f, 70), PassCorridor.ArcKind.Low, opening: 3f);
            Trio(p, "Two empty-handed players hold the threshold slab; let go, it keeps the great herse up four seconds: everyone runs the 26 m, " +
                    "passing on the way; then one pass through the arch.",
                 "the slab needs two empty hands and one of you always holds the bomb (or has it in the air).",
                 ClimbLock("the great wall", new Vector3(0, 0, 43), new Vector3(0, 6, 43)));
        }

        // ================================================================== Act 2 - Les Trois Ailes (white noon): the ring round the pyramid

        // Three wings side by side under one roof (the west wing, the nave, the east wing), split by divides up to the roof at x = ±4.
        const float WestWing = -8f, Nave = 0f, EastWing = 8f;

        /// <summary>The three wing floors from <paramref name="z0"/> to <paramref name="z1"/> (each its own: the divides stand between them).</summary>
        static void Wings(Transform p, float z0, float z1, bool west = true, bool nave = true, bool east = true)
        {
            if (west) Deck(p, "West wing", WestWing - WingHalf, WestWing + WingHalf, z0, z1, 0);
            if (nave) Deck(p, "Nave", Nave - WingHalf, Nave + WingHalf, z0, z1, 0);
            if (east) Deck(p, "East wing", EastWing - WingHalf, EastWing + WingHalf, z0, z1, 0);
        }

        /// <summary>
        /// A divide up to the roof along <paramref name="x"/> (±4) from z0 to z1, with bramble windows (the bomb only, centred on each
        /// <paramref name="windows"/> z) and laser doorways (the runners only, from each <paramref name="lasers"/> z, 3.5 m long).
        /// </summary>
        static void DivideWall(Transform p, string name, float x, float z0, float z1, float[] windows, float[] lasers = null)
        {
            var holes = new System.Collections.Generic.List<Hole>();
            foreach (float z in windows) holes.Add(new Hole(z - 1.5f, z + 1.5f, 1f, 3.6f));
            foreach (float z in lasers ?? new float[0]) holes.Add(new Hole(z, z + 3.5f, 0f, PassageHeight));
            WallAlong(p, name, x, 0, Roof, z0, z1, holes.ToArray());
            foreach (float z in windows)
            {
                ScreenAlong(p, name + " brambles", z - 1.5f, z + 1.5f, x, 1f, 2.6f);
                Pass(p, $"{name} window {z}", new Vector3(x - 3.75f, 1.5f, z), new Vector3(x + 3.75f, 1.5f, z), opening: 2.25f);
            }
            foreach (float z in lasers ?? new float[0]) CurtainAlong(p, name + " lasers", z, z + 3.5f, x, 0, PassageHeight);
        }

        /// <summary>A wall across one wing at <paramref name="z"/>, up to the roof, with a 4 m door hole at the wing's centre.</summary>
        static void WingWall(Transform p, string name, float wing, float z) =>
            WallAcross(p, name, z, 0, Roof, wing == Nave ? -Divide : wing - 5f, wing == Nave ? Divide : wing + 5f, new Hole(wing - 2f, wing + 2f, 0, PassageHeight));

        /// <summary>A laser curtain across the nave at <paramref name="z"/>, and the masonry over it up to the roof (the bomb never goes over).</summary>
        static void NaveLasers(Transform p, string name, float z)
        {
            WallAcross(p, name + " lintel", z, 0, Roof, -Divide, Divide, new Hole(-WingHalf, WingHalf, 0, PassageHeight));
            CurtainAcross(p, name, -WingHalf, WingHalf, z, 0, PassageHeight);
        }

        static SectionContract.Shortcut DivideLock(string name, float x, float z) => LobLock(name, new Vector3(x - 4f, 0, z), new Vector3(x, Roof, z));

        /// <summary>
        /// The share (§13.22): three herses, each raised from another wing by an hourglass plate (west from the nave, the nave from the
        /// east, the east from the west); the three stand on their plates juggling the bomb through the brambles, then let go together.
        /// </summary>
        static void LePartage(Transform p)
        {
            Wings(p, 10, 86);
            DivideWall(p, "West divide", -Divide, 10, 86, new[] { 44f, 74f });
            DivideWall(p, "East divide", Divide, 10, 86, new[] { 44f, 74f });
            var west = HourglassPlate(p, "West plate", new Vector3(WestWing, 0, 44), 5f);
            var nave = HourglassPlate(p, "Nave plate", new Vector3(Nave, 0, 44), 5f);
            var east = HourglassPlate(p, "East plate", new Vector3(EastWing, 0, 44), 5f);
            WingWall(p, "West wall", WestWing, 62);
            WingWall(p, "Nave wall", Nave, 62);
            WingWall(p, "East wall", EastWing, 62);
            Herse(p, "West herse", WestWing - 2, WestWing + 2, 62, 0, nave);
            Herse(p, "Nave herse", Nave - 2, Nave + 2, 62, 0, east);
            Herse(p, "East herse", EastWing - 2, EastWing + 2, 62, 0, west);
            Torches(p, -11.3f, 20, 80, step: 30);
            Torches(p, 11.3f, 20, 80, step: 30);
            Trio(p, "One per wing: each plate raises another wing's herse (the nave's the west's, the east's the nave's, the west's the east's). " +
                    "Stand on all three, keep the bomb moving through the brambles (the one holding it lets their sand run), then let go together.",
                 "a plate is empty and its herse never rises; one runner cannot hold two plates (76 m apart round the divides, longer than the sand).",
                 DivideLock("over the west divide", -Divide, 30), DivideLock("over the east divide", Divide, 30),
                 SpreadLock("west to nave", new Vector3(WestWing, 0, 44), new Vector3(Nave, 0, 44), new Vector3(WestWing, 0, 10), new Vector3(Nave, 0, 10)),
                 SpreadLock("nave to east", new Vector3(Nave, 0, 44), new Vector3(EastWing, 0, 44), new Vector3(Nave, 0, 10), new Vector3(EastWing, 0, 10)),
                 SpreadLock("east to west", new Vector3(EastWing, 0, 44), new Vector3(WestWing, 0, 44), new Vector3(EastWing, 0, 10), new Vector3(WestWing, 0, 10)));
            CP(p, 6, new Vector3(0, 0, 96));
        }

        /// <summary>
        /// The stone heads (§13.16): the nave is shut by a herse with lasers behind it, so the bomb crosses only by the two heads, to the
        /// west or the east exit; the herse's plate stands far beyond the west lasers, 26 m from the west exit.
        /// </summary>
        static void TetesDePierre(Transform p)
        {
            Wings(p, 10, 86);
            DivideWall(p, "West divide", -Divide, 10, 86, new[] { 56f });
            DivideWall(p, "East divide", Divide, 10, 86, new[] { 56f });
            WingWall(p, "Nave wall", Nave, 40);
            NaveLasers(p, "Nave lasers", 41.5f);
            WallAcross(p, "West laser wall", 40, 0, Roof, WestWing - 5f, -Divide, new Hole(WestWing - 3f, WestWing + 3f, 0, PassageHeight));
            CurtainAcross(p, "West lasers", WestWing - 3f, WestWing + 3f, 40, 0, PassageHeight);
            WallAcross(p, "East laser wall", 40, 0, Roof, Divide, EastWing + 5f, new Hole(EastWing - 3f, EastWing + 3f, 0, PassageHeight));
            CurtainAcross(p, "East lasers", EastWing - 3f, EastWing + 3f, 40, 0, PassageHeight);
            var plate = HandsFreePlate(p, "Far west plate", new Vector3(WestWing, 0, 76));
            Herse(p, "Nave herse", -2, 2, 40, 0, plate);
            var heads = Place(p, Tube, "Stone heads", Vector3.zero, Quaternion.identity);
            ConfigureTube(heads, new[]
            {
                new TubeSlot(new Vector3(-1.5f, 1.6f, 30), 0, new Vector3(WestWing, 4.5f, 46), new Vector3(WestWing, 0, 50), 0.9f),
                new TubeSlot(new Vector3(1.5f, 1.6f, 30), 0, new Vector3(EastWing, 4.5f, 46), new Vector3(EastWing, 0, 50), 0.9f)
            });
            TransitPasses(p, heads);
            Torches(p, -11.3f, 20, 80, step: 30);
            Torches(p, 11.3f, 20, 80, step: 30);
            Trio(p, "One holds the plate beyond the west lasers, which raises the nave's herse; the thrower sends the bomb into a stone head " +
                    "and goes through; the third waits at that head's exit and passes the bomb to the thrower through the brambles.",
                 "the plate's holder is 26 m from the west exit and the thrower can only go through while it is held: nobody waits at an exit, " +
                 "and the lasers behind the herse stop the bomb carried through.",
                 LobLock("over the nave wall", new Vector3(0, 0, 34), new Vector3(0, Roof, 40)),
                 DivideLock("over the west divide", -Divide, 60), DivideLock("over the east divide", Divide, 60));
            CP(p, 7, new Vector3(0, 0, 96));
        }

        /// <summary>
        /// Crossed beams (§13.23): a sun beam down each wing raises another wing's herse while a body stands in it (the west's the east's,
        /// the east's the nave's, the nave's the west's); each beam ends on a pillar where its runner must leave the light. Order: the
        /// nave through first, then the east, then the west, each staying in the light until the next is through.
        /// </summary>
        static void RayonsCroises(Transform p)
        {
            Wings(p, 10, 86);
            DivideWall(p, "West divide", -Divide, 10, 86, new[] { 30f, 70f });
            DivideWall(p, "East divide", Divide, 10, 86, new[] { 30f, 70f });
            var westBeam = Beam(p, "West beam", new Vector3(WestWing, 1.1f, 14), Vector3.forward, 38);
            var eastBeam = Beam(p, "East beam", new Vector3(EastWing, 1.1f, 14), Vector3.forward, 28);
            var naveBeam = Beam(p, "Nave beam", new Vector3(Nave, 1.1f, 14), Vector3.forward, 48);
            Solid(p, "West pillar", WestWing - 1.5f, WestWing + 1.5f, 52, 55, Roof);
            Solid(p, "East pillar", EastWing - 1.5f, EastWing + 1.5f, 42, 45, Roof);
            Solid(p, "Nave pillar", Nave - 1.5f, Nave + 1.5f, 62, 65, Roof);
            WingWall(p, "Nave wall", Nave, 40);
            WingWall(p, "East wall", EastWing, 50);
            WingWall(p, "West wall", WestWing, 60);
            Herse(p, "Nave herse", -2, 2, 40, 0, eastBeam);
            Herse(p, "East herse", EastWing - 2, EastWing + 2, 50, 0, westBeam);
            Herse(p, "West herse", WestWing - 2, WestWing + 2, 60, 0, naveBeam);
            Trio(p, "Each runner walks in their wing's light: the east's opens the nave (herse at 40), the west's the east (50), the nave's the west " +
                    "(60). The nave goes through first and waits in its light; the east, then the west; nobody leaves their light before the next is through.",
                 "one wing has nobody in its light, so the herse it raises never does.",
                 DivideLock("over the west divide", -Divide, 50), DivideLock("over the east divide", Divide, 50));
            CP(p, 8, new Vector3(0, 0, 96));
        }

        /// <summary>
        /// The rose (§13.24): the wings end round a pit under the pyramid's flank; its pivot lies from the nave's dock to the exit and turns
        /// to join the west and east docks while the nave's holder stands on the plate behind the lasers. The pair boards its east end;
        /// let go, the slab turns back for twelve seconds and carries them, passing, to the exit; then the holder walks it.
        /// </summary>
        static void LaRose(Transform p)
        {
            const float O = 74f;   // the pivot's centre
            Deck(p, "West wing", WestWing - WingHalf, WestWing + WingHalf, 10, 56, 0);
            Deck(p, "East wing", EastWing - WingHalf, EastWing + WingHalf, 10, 56, 0);
            Deck(p, "Nave", -WingHalf, WingHalf, 10, O - 16.5f, 0);
            Deck(p, "West walk", -26, WestWing - WingHalf, 48, 56, 0);
            Deck(p, "West dock", -26, -17.5f, 56, 80, 0);
            Deck(p, "East walk", EastWing + WingHalf, 26, 48, 56, 0);
            Deck(p, "East dock", 17.5f, 26, 56, 80, 0);
            Deck(p, "Exit", -6, 6, O + 16.5f, 98, 0);
            DivideWall(p, "West divide", -Divide, 10, O - 16.5f, new[] { 24f });
            DivideWall(p, "East divide", Divide, 10, O - 16.5f, new[] { 24f });
            NaveLasers(p, "Nave lasers", 32);
            var plate = HandsFreePlate(p, "Rose plate", new Vector3(0, 0, 42));
            Pivot(p, "Rose", new Vector3(0, 0, O), new Vector3(3, 0.5f, 32), 0, 90, 12, plate);
            StoneRoof(p, "West dock roof", -27, -CoverHalf, Hub, 90, Roof);
            StoneRoof(p, "East dock roof", CoverHalf, 27, Hub, 90, Roof);
            Torches(p, -25.3f, 58, 78, step: 20);
            Torches(p, 25.3f, 58, 78, step: 20);
            Trio(p, "The nave's runner crosses the lasers and stands on the plate: the slab turns to join the west and east docks (twelve seconds). " +
                    "The pair boards its east end; let go, it turns back and carries them, passing, to the exit; the holder then walks the slab.",
                 "the plate is behind the lasers, where the bomb never goes, so the carrier waits at a dock and rides alone, far longer than a fuse.",
                 GapLock("west dock to the exit", new Vector3(-17.5f, 0, 80), new Vector3(-6, 0, O + 16.5f)),
                 GapLock("east dock to the exit", new Vector3(17.5f, 0, 80), new Vector3(6, 0, O + 16.5f)),
                 GapLock("across the pit", new Vector3(-17.5f, 0, O), new Vector3(17.5f, 0, O)),
                 DivideLock("over the west divide", -Divide, 40), DivideLock("over the east divide", Divide, 40));
            CP(p, 9, new Vector3(0, 0, 110));
        }

        /// <summary>
        /// The reunion (§13.21): the west and nave wings end at a wall; the heavy slab in the nave (with a five-second hourglass) raises the
        /// east wing's bridge; its two holders reach it through the lasers; then one pass through the arch.
        /// </summary>
        static void LaReunion(Transform p)
        {
            Wings(p, 10, 50, east: false);
            Deck(p, "East wing", EastWing - WingHalf, EastWing + WingHalf, 10, 48, 0);
            Deck(p, "East far", EastWing - WingHalf, EastWing + WingHalf, 62, 80, 0);
            DivideWall(p, "West divide", -Divide, 10, 50, new float[0], new[] { 30f });
            DivideWall(p, "East divide", Divide, 10, 50, new[] { 25f, 38f }, new[] { 44f });
            WallAcross(p, "End wall", 50, 0, Roof, WestWing - 5f, Divide);
            var heavy = HeavyPlate(p, "Reunion slab", new Vector3(0, 0, 40));
            MakeHourglass(heavy.gameObject, 5f);
            RisingBridge(p, "East bridge", EastWing, 48, 62, 0, 4, heavy);
            ArchCP(p, 10, new Vector3(0, 0, 90), new Vector3(4, 0, 80));
            Pass(p, "Through the arch", new Vector3(8, 1.5f, 72), new Vector3(1, 1.5f, 88), PassCorridor.ArcKind.Low, opening: 3f);
            Trio(p, "The west runner crosses into the nave by the lasers; two empty hands on the slab raise the east bridge, five seconds more once " +
                    "they let go: they run through the east lasers and over with the carrier; then one pass through the arch.",
                 "the slab needs two empty hands and one of you always holds the bomb.",
                 GapLock("the east gap", new Vector3(EastWing, 0, 48), new Vector3(EastWing, 0, 62)),
                 DivideLock("over the east divide", Divide, 20));
        }

        // ================================================================== Act 3 - Le Sanctuaire (tropical storm): up, round, the altar

        /// <summary>
        /// The well (§13.21): a heavy slab with a five-second hourglass brings the lift down the 30 m shaft; everyone boards before the sand
        /// runs out and rides up to the skyway together.
        /// </summary>
        static void LePuits(Transform p)
        {
            const float Top = 30f;
            Deck(p, "Well hall", -8, 8, 10, 34, 0);
            StoneRoof(p, "Hall roof", -CoverHalf, CoverHalf, Hub, 36, Roof);
            var heavy = HeavyPlate(p, "Well slab", new Vector3(0, 0, 20));
            MakeHourglass(heavy.gameObject, 5f);
            var lift = Actuator(p, Lift, "Well lift", new Vector3(0, Top - 0.25f, 40), new Vector3(6, 0.5f, 6), new Vector3(0, -Top, 0), heavy);
            SetField(lift.GetComponent<SignalActuator>(), "travelSeconds", v => v.floatValue = 5f);
            WallAlong(p, "Shaft west", -3.5f, 0, Top + 2f, 36.5f, 43.5f);
            WallAlong(p, "Shaft east", 3.5f, 0, Top + 2f, 36.5f, 43.5f);
            WallAcross(p, "Shaft front", 36.5f, 0, Top + 2f, -4f, 4f, new Hole(-3f, 3f, 0, PassageHeight));
            WallAcross(p, "Shaft back", 43.5f, 0, Top + 2f, -4f, 4f, new Hole(-3f, 3f, Top, Top + PassageHeight));
            Deck(p, "Sanctuary floor", -8, 8, 44, 50, Top);
            StoneRoof(p, "Upper roof", -CoverHalf, CoverHalf, 36, 50, Top + Roof);
            Torches(p, 7.3f, 14, 30, step: 16);
            Trio(p, "Two empty hands on the slab bring the lift down; let go, it waits five seconds at the bottom: all board and ride the 30 m up.",
                 "the slab needs two empty hands and one of you always holds the bomb; nobody is up there to throw it to.",
                 ClimbLock("the shaft", new Vector3(0, 0, 34), new Vector3(0, Top, 44)));
            CP(p, 11, new Vector3(0, Top, 60));
        }

        /// <summary>
        /// The corridor of traps (§13.22): three relays of the relay. At each wall the holder in the shrine raises the herse for the pair;
        /// beyond it, an empty hand taps the hourglass plate whose gate lets the holder in. The roles turn; a sweeper between the last two.
        /// </summary>
        static void CouloirDesPieges(Transform p)
        {
            Deck(p, "Corridor", -6, 6, 10, 100, 0);
            var locks = new System.Collections.Generic.List<SectionContract.Shortcut>();
            float[] walls = { 34f, 60f, 86f };
            for (int k = 0; k < walls.Length; k++)
            {
                float z = walls[k];
                Deck(p, $"Shrine ledge {k + 1}", 6, 11.5f, z - 15, z - 7, 0);
                Shrine(p, $"Trap shrine {k + 1}", new Vector3(8.75f, 0, z - 11), 4f, -1);
                var shrine = HandsFreePlate(p, $"Shrine plate {k + 1}", new Vector3(8.75f, 0, z - 11));
                WallAcross(p, $"Trap wall {k + 1}", z, 0, Roof, -8, 8, new Hole(-2, 2, 0, PassageHeight), new Hole(-5.6f, -2.6f, 0, PassageHeight));
                Herse(p, $"Trap herse {k + 1}", -2, 2, z, 0, shrine);
                var sand = HourglassPlate(p, $"Hourglass {k + 1}", new Vector3(-4, 0, z + 6), 4f);
                Herse(p, $"Trap gate {k + 1}", -5.6f, -2.6f, z, 0, sand);
                locks.Add(LobLock($"over trap wall {k + 1}", new Vector3(0, 0, z - 6), new Vector3(0, Roof, z)));
            }
            AddRotator(p, ObstaclesDir + "Obstacle_Sweeper", "Trap sweeper", new Vector3(0, 0, 74), 11.8f, 80f, 0f);
            Torches(p, -5.7f, 16, 96, step: 20);
            Trio(p, "Three times: the holder in the shrine raises the herse; the pair takes the bomb through; inside, an empty hand taps the " +
                    "hourglass plate, whose gate stays up four seconds to let the holder in. Swap roles; jump the sweeper.",
                 "the holder stands in a shrine, out of any throw, and the carrier is alone behind each wall: the hourglass only counts " +
                 "empty hands, and the bomb has no way back.", locks.ToArray());
            CP(p, 12, new Vector3(0, 0, 110));
        }

        /// <summary>
        /// The ford of three stones (§13.23): three stones cross 42 m of void in step. The rider of the east stone cuts the sun beam over its
        /// last stretch, which raises the herse at the west stone's landing for the pair; the rider joins them through the east lasers.
        /// </summary>
        static void GueDesTroisPierres(Transform p)
        {
            Deck(p, "Ford bank", -12, 12, 10, 24, 0);
            const float Speed = 2.5f, Dwell = 0.3f, Far = 63f, Near = 27f, EastFar = 49f;
            AddMover(p, "Platform_Moving", "West stone", new Vector3(-8, -0.5f, Near), new Vector3(-8, -0.5f, Far), new Vector3(6, 1, 6), Speed, 0f, Dwell);
            AddMover(p, "Platform_Moving", "Middle stone", new Vector3(0, -0.5f, Near), new Vector3(0, -0.5f, Far), new Vector3(6, 1, 6), Speed, 0.5f, Dwell);
            // the east stone's path is shorter: it keeps step with the west one (the same cycle) and lands on its own ledge
            AddMover(p, "Platform_Moving", "East stone", new Vector3(9, -0.5f, Near), new Vector3(9, -0.5f, EastFar), new Vector3(6, 1, 6),
                     Speed * (EastFar - Near) / (Far - Near), 0f, Dwell);
            var beam = Beam(p, "Ford beam", new Vector3(9, 1.1f, 37), Vector3.forward, 15);
            WallAlong(p, "Ford divide", 5.25f, 0, Roof, 27, 69.5f);
            Deck(p, "Ford ledge", -12, 4.75f, 66.5f, 69.5f, 0);
            Deck(p, "East landing", 6, 12, 53, 69.5f, 0);
            WallAcross(p, "Ford wall", 69.5f, 0, Roof, -14, 14, new Hole(-10, -6, 0, PassageHeight), new Hole(7, 11, 0, PassageHeight));
            Herse(p, "Ford herse", -10, -6, 69.5f, 0, beam);
            CurtainAcross(p, "East lasers", 7, 11, 69.5f, 0, PassageHeight);
            Deck(p, "Far bank", -12, 12, 69.5f, 88, 0);
            Pass(p, "Across the stones", new Vector3(-8, 1.5f, 40), new Vector3(0, 1.5f, 48), timed: true);
            Torches(p, -11.7f, 72, 86, step: 14);
            Trio(p, "Three stones cross in step. The east rider cuts the sun beam over its last stretch and at the landing, which raises the herse " +
                    "at the west stone's landing: the pair, passing all the way, walks through; then the rider leaves the light and takes the lasers.",
                 "the beam is over the east stone only, behind a divide: one rides it to cut the light while the carrier rides alone for longer " +
                 "than a fuse; riding together leaves nobody in the light.",
                 GapLock("the ford", new Vector3(0, 0, 24), new Vector3(0, 0, 66.5f)),
                 LobLock("over the ford wall", new Vector3(-8, 0, 64), new Vector3(-8, Roof, 69.5f)),
                 LobLock("over the ford divide", new Vector3(1, 0, 60), new Vector3(5.25f, Roof, 60)));
            CP(p, 13, new Vector3(0, 0, 100));
        }

        /// <summary>
        /// The crossing: the wings again on a 4.5 s fuse, each opened from another by a different system: the west's hourglass raises the
        /// nave's bridge, the nave's hourglass the east herse, the east's sun beam the west herse.
        /// </summary>
        static void LaCroisee(Transform p)
        {
            Wings(p, 10, 86, nave: false);
            Deck(p, "Nave", -WingHalf, WingHalf, 10, 52, 0);
            Deck(p, "Nave far", -WingHalf, WingHalf, 66, 86, 0);
            DivideWall(p, "West divide", -Divide, 10, 86, new[] { 30f, 74f });
            DivideWall(p, "East divide", Divide, 10, 86, new[] { 30f, 74f });
            var west = HourglassPlate(p, "West plate", new Vector3(WestWing, 0, 40), 4f);
            var nave = HourglassPlate(p, "Nave plate", new Vector3(Nave, 0, 40), 4f);
            var beam = Beam(p, "East beam", new Vector3(EastWing, 1.1f, 14), Vector3.forward, 30);
            Solid(p, "East pillar", EastWing - 1.5f, EastWing + 1.5f, 44, 47, Roof);
            RisingBridge(p, "Nave bridge", Nave, 52, 66, 0, 4, west);
            WingWall(p, "East wall", EastWing, 56);
            WingWall(p, "West wall", WestWing, 56);
            Herse(p, "East herse", EastWing - 2, EastWing + 2, 56, 0, nave);
            Herse(p, "West herse", WestWing - 2, WestWing + 2, 56, 0, beam);
            Trio(p, "The west's hourglass raises the nave's bridge, the nave's hourglass the east herse, the east's light the west herse: hold, " +
                    "juggle the bomb through the brambles, then go, in the four seconds of sand.",
                 "one wing is empty and what it opens stays shut; one runner cannot hold the west and the nave plates (68 m apart round the divides).",
                 GapLock("the nave gap", new Vector3(Nave, 0, 52), new Vector3(Nave, 0, 66)),
                 DivideLock("over the west divide", -Divide, 50), DivideLock("over the east divide", Divide, 50),
                 SpreadLock("west to nave", new Vector3(WestWing, 0, 40), new Vector3(Nave, 0, 40), new Vector3(WestWing, 0, 10), new Vector3(Nave, 0, 10)));
            CP(p, 14, new Vector3(0, 0, 96));
        }

        /// <summary>
        /// The altar, over the pyramid's summit: the heavy slab's bridge, then the last pivot. The holder stands in the light behind the
        /// lasers, the slab turns to the docks, the pair rides it back to the altar; one pass through the arch; the beacon.
        /// </summary>
        static void LAutel(Transform p)
        {
            const float O = 88f;
            Deck(p, "Forecourt", -8, 8, 10, 30, 0);
            var heavy = HeavyPlate(p, "Altar slab", new Vector3(0, 0, 18));
            MakeHourglass(heavy.gameObject, 4f);
            RisingBridge(p, "Altar bridge", 0, 30, 44, 0, 5, heavy);
            Deck(p, "Terrace", -8, 8, 44, 52, 0);
            Deck(p, "West walk", -26, -8, 44, 52, 0);
            Deck(p, "West dock", -26, -17.5f, 52, 96, 0);
            Deck(p, "East walk", 8, 26, 44, 52, 0);
            Deck(p, "East dock", 17.5f, 26, 52, 96, 0);
            Deck(p, "Light dock", -WingHalf, WingHalf, 52, O - 16.5f, 0);
            WallAlong(p, "West screen", -Divide, 0, Roof, 52, O - 16.5f);
            WallAlong(p, "East screen", Divide, 0, Roof, 52, O - 16.5f);
            NaveLasers(p, "Light lasers", 52.5f);
            var light = Beam(p, "Altar light", new Vector3(-WingHalf + 0.2f, 1.1f, 60), Vector3.right, 2 * WingHalf - 0.4f);
            Pivot(p, "Altar pivot", new Vector3(0, 0, O), new Vector3(3, 0.5f, 32), 0, 90, 12, light);
            Deck(p, "Altar", -10, 10, O + 16.5f, 130, 0);
            StoneRoof(p, "West dock roof", -27, -CoverHalf, Hub, 100, Roof);
            StoneRoof(p, "East dock roof", CoverHalf, 27, Hub, 100, Roof);
            ArchCP(p, 15, new Vector3(0, 0, 116), new Vector3(0, 0, 109));
            Place(p, GameplayDir + "FinishZone", "FinishZone", new Vector3(0, 0, 124), Quaternion.identity);
            NatureKit.Campfire(p, new Vector3(0, 0, 128), null, 2.4f);
            Pass(p, "Through the altar arch", new Vector3(0, 1.5f, 106), new Vector3(0, 1.5f, 114), timed: true, opening: 3.2f);
            Trio(p, "Two empty hands on the slab raise the bridge for all (four seconds of sand); then one stands in the light behind the lasers: " +
                    "the pivot turns to the docks, the pair boards its east end and, let go, it carries them to the altar; the arch, the beacon.",
                 "the slab needs two empty hands, and the light is behind lasers the bomb never crosses: the carrier would ride alone.",
                 GapLock("the forecourt gap", new Vector3(0, 0, 30), new Vector3(0, 0, 44)),
                 GapLock("west dock to the altar", new Vector3(-17.5f, 0, 96), new Vector3(-6, 0, O + 16.5f)),
                 GapLock("east dock to the altar", new Vector3(17.5f, 0, 96), new Vector3(6, 0, O + 16.5f)));
        }
    }
}
