using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using static HotPatata.Editor.BombObstacleKitBuilder;

namespace HotPatata.Editor
{
    /// <summary>
    /// PatataRanger's six acts (PROJECT_SPEC §15f), in world space on the land of <c>.Forest.cs</c>. Each act is one section from its
    /// checkpoint to the next and chains several trials under one contract.
    /// </summary>
    public static partial class PatataRangerBuilder
    {
        // ------------------------------------------------------------------ the trails on the ground

        static IEnumerable<List<Vector3>> TrailPoints()
        {
            // the camp to the yard gate and on to the ravine (act A)
            yield return new List<Vector3> { new Vector3(0, 0, 6), new Vector3(-2.5f, 0, 14), new Vector3(1.5f, 0, 20.5f), new Vector3(-1f, 0, 27),
                new Vector3(4f, 0, 33), new Vector3(GateX, 0, 37.5f), new Vector3(GateX, 0, 43), new Vector3(6, 0, 49), new Vector3(3, 0, 54), new Vector3(2, 0, 58) };
            // the south rim: west to the bridgehead, east to the winch and the stair (act B)
            yield return new List<Vector3> { new Vector3(BridgeX, 0, 64), new Vector3(-2, 0, 60), new Vector3(2, 0, 58), new Vector3(8, 0, 63),
                new Vector3(13, 0, 65), new Vector3(21, 0, 66.5f), new Vector3(29, 0, 66) };
            // the north rim: the cage's landing to the bridgehead, then up to the old oak's clearing
            yield return new List<Vector3> { new Vector3(13, 0, 95), new Vector3(4, 0, 93.5f), new Vector3(BridgeX, 0, 92) };
            yield return new List<Vector3> { new Vector3(BridgeX, 0, 92), new Vector3(-4, 0, 98), new Vector3(0, 0, 103), new Vector3(2, 0, 107) };
        }

        // ------------------------------------------------------------------ act A: the camp (the start, on the ground)

        /// <summary>
        /// Le Camp: the ranger's camp in a clearing at dawn, the trail winding north between boulders (the first passes, nothing at
        /// stake), and the yard gate in the ridge: an empty-handed player cranks its winch (an hourglass plate: a carrier does not
        /// count) and the sand holds the gate while the cranker follows.
        /// </summary>
        static void ActCamp(Transform p)
        {
            Camp(p);
            // boulders the trail winds between, half sunk in the ground
            (float x, float z, float sx, float sy, float sz, float yaw)[] boulders =
            {
                (-6.8f, 13f, 4.2f, 2.6f, 3.4f, 20f), (5.4f, 16.5f, 3.6f, 2.2f, 3f, -35f), (-5f, 22.5f, 3.2f, 3.2f, 4f, 60f),
                (6.8f, 26f, 4.4f, 2.6f, 3.2f, 10f), (-4.2f, 31.5f, 2.8f, 1.9f, 2.4f, -20f), (12.5f, 30f, 3f, 2.2f, 3f, 45f), (-11f, 27f, 5f, 3f, 4f, -10f),
            };
            foreach (var b in boulders)
                NatureKit.Rock(p, "Boulder", G(b.x, b.z, b.sy * 0.3f), new Vector3(b.sx, b.sy, b.sz), "Nature_MossyRock", Quaternion.Euler(0f, b.yaw, 0f));

            // the yard gate in the ridge's notch, its winch in a booth beside it
            var floor = G(GateX, RidgeZ);
            var winch = HourglassPlate(p, "Yard winch", G(GateX + 5.6f, RidgeZ - 4.6f), 5f);
            Actuator(p, Door, "Yard gate", new Vector3(GateX, floor.y + 2.1f, RidgeZ), new Vector3(2f * GateHalf + 1f, 4.2f, 0.8f), new Vector3(0f, 4.6f, 0f), winch);
            GateTower(p, floor);
            WinchBooth(p, winch.transform.position, Quaternion.Euler(0f, -35f, 0f), new Vector3(GateX + 3.6f, floor.y + 8.8f, RidgeZ));

            CP(p, 1, G(2f, 58f), new Vector3(-3.6f, 0f, 1.2f));
            Contract(p, "The yard gate: an empty-handed player cranks the winch (an hourglass: a carrier does not count), its sand holds the " +
                        "gate while the cranker follows; the bomb is passed over the gate or carried through.",
                     ClimbLock("the ridge west of the gate", G(GateX - 14f, RidgeZ - 6f), new Vector3(GateX - 14f, RidgeHeight - 0.8f, RidgeZ)),
                     ClimbLock("the ridge east of the gate", G(GateX + 14f, RidgeZ - 6f), new Vector3(GateX + 14f, RidgeHeight - 0.8f, RidgeZ)));
            RoutePoints(G(0f, 8f), G(1f, 24f), G(GateX, RidgeZ), G(2f, 56f));
        }

        // ------------------------------------------------------------------ act B: the ravine

        /// <summary>The ravine's south wall foot and north wall foot (z at x), just clear of the rock.</summary>
        static float WallS(float x) => RavineZ(x) - RavineHalf(x) + 1.45f;
        static float WallN(float x) => RavineZ(x) + RavineHalf(x) - 1.45f;

        const float LedgeY = -5f;
        const float CageX = 12.8f;

        /// <summary>
        /// Le Ravin: one player goes down the south stair to the ledge, over the old footbridge low above the stream and into the cage
        /// on the far ledge, which the other hoists from the south rim (an empty-handed body on the winch: the climber holds the bomb).
        /// Then the far winch (an hourglass, empty hands) lowers the drawbridge for the one left on the south rim. The bomb crosses by
        /// passes the whole way: down into the ravine, across it, up to the rims.
        /// </summary>
        static void ActRavine(Transform p)
        {
            const float BridgeY = 2.3f;
            float southEdge = RavineZ(BridgeX) - RavineHalf(BridgeX), northEdge = RavineZ(BridgeX) + RavineHalf(BridgeX);

            // the bridgeheads on both rims
            var south = Clip(Blob(new Vector2(BridgeX, southEdge - 3f), 3.6f, 3.3f, 0.12f, 3, 10f), new Vector2(0f, southEdge + 0.2f), Vector2.up);
            Deck(p, "South bridgehead", south, BridgeY, 8f, true, 14f,
                 new TimberKit.Opening(new Vector2(BridgeX, southEdge + 0.2f), 2.9f), new TimberKit.Opening(new Vector2(BridgeX + 2.9f, southEdge - 4.6f), 2.2f));
            var north = Clip(Blob(new Vector2(BridgeX, northEdge + 3.2f), 3.8f, 3.4f, 0.12f, 4, -12f), new Vector2(0f, northEdge - 0.25f), Vector2.down);
            Deck(p, "North bridgehead", north, BridgeY, -6f, true, 14f,
                 new TimberKit.Opening(new Vector2(BridgeX, northEdge - 0.25f), 2.9f), new TimberKit.Opening(new Vector2(BridgeX + 3.4f, northEdge + 4.6f), 2.2f),
                 new TimberKit.Opening(new Vector2(BridgeX - 1.5f, northEdge + 6.6f), 2.2f));

            // the far winch lowers the drawbridge (raised against the north side)
            var farWinch = HourglassPlate(p, "Bridge winch", new Vector3(BridgeX + 2.1f, BridgeY, northEdge + 3.6f), 6f);
            Drawbridge(p, "Drawbridge", new Vector3(BridgeX, BridgeY, northEdge - 0.2f), new Vector3(BridgeX, BridgeY, southEdge - 0.6f), 2.4f, farWinch);

            // the south stair: from the rim east of the winch down along the wall to the ledge
            var stair = new List<Vector3>
            {
                G(30.5f, 66.8f, 0.05f), new Vector3(30.5f, RimY, WallS(30.5f) + 0.9f), new Vector3(29.6f, RimY, WallS(29.6f) + 0.95f),
                new Vector3(26f, RimY - 2.3f, WallS(26f) + 0.95f), new Vector3(24.6f, RimY - 2.3f, WallS(24.6f) + 0.95f),
                new Vector3(17.8f, LedgeY, WallS(17.8f) + 0.95f), new Vector3(17.1f, LedgeY, WallS(17.1f) + 1.1f),
            };
            Walk(p, "South stair", stair, 1.5f, false, true, false, 16f);

            // the ledges low over the stream, the footbridge between them, the cage on the far ledge
            Deck(p, "South ledge", Ledge(11.6f, 18.4f, x => WallS(x) + 0.15f, x => WallS(x) + 2.9f), LedgeY, 84f, true, 14f,
                 new TimberKit.Opening(new Vector2(16f, WallS(16f) + 2.9f), 2.2f), new TimberKit.Opening(new Vector2(18.2f, WallS(18.2f) + 1f), 2f));
            Deck(p, "North ledge", Ledge(10.6f, 18.8f, x => WallN(x) - 2.9f, x => WallN(x) - 0.15f), LedgeY, 96f, true, 14f,
                 new TimberKit.Opening(new Vector2(16f, WallN(16f) - 2.9f), 2.2f), new TimberKit.Opening(new Vector2(CageX, WallN(CageX) - 2.9f), 2.6f));
            RopeSpan(p, "Old footbridge", new Vector3(16f, LedgeY, WallS(16f) + 2.75f), new Vector3(16f, LedgeY, WallN(16f) - 2.75f), 1.4f, 0.75f);

            var hoist = HourglassPlate(p, "Cage winch", G(CageX, WallS(CageX) - 4.8f), 4f);
            float cageZ = WallN(CageX) - 1.45f;
            Cage(p, "Cage", new Vector3(CageX, LedgeY, cageZ), new Vector2(2.4f, 2.4f), 0f, RimY + 0.3f - LedgeY, hoist, 3.2f);
            var landing = Clip(Blob(new Vector2(CageX, cageZ + 4.4f), 3.4f, 3.2f, 0.1f, 6, 20f), new Vector2(0f, cageZ + 1.25f), Vector2.down);
            Deck(p, "Cage landing", landing, RimY + 0.3f, 90f, true, 14f,
                 new TimberKit.Opening(new Vector2(CageX, cageZ + 1.25f), 2.6f), new TimberKit.Opening(new Vector2(CageX - 3.2f, cageZ + 4.2f), 2.2f));
            WinchBooth(p, hoist.transform.position, Quaternion.Euler(0f, 180f, 0f), new Vector3(CageX, RimY + 7.5f, cageZ));

            CP(p, 2, G(2f, 106f), new Vector3(3.8f, 0f, -0.8f));
            Contract(p, "One goes low (the south stair, the footbridge, the cage the other hoists from the south winch with empty hands); " +
                        "then the far winch (empty hands, an hourglass) lowers the drawbridge for the one left on the south rim. The bomb " +
                        "crosses by passes the whole way.",
                     GapLock("the ravine under the drawbridge", new Vector3(BridgeX, BridgeY, southEdge + 0.2f), new Vector3(BridgeX, BridgeY, northEdge - 0.2f)),
                     GapLock("the ravine at the cage", new Vector3(CageX, RimY, WallS(CageX) - 1.6f), new Vector3(CageX, RimY, WallN(CageX) + 1.6f)),
                     ClimbLock("the north wall over the ledge", new Vector3(CageX + 3f, LedgeY, WallN(CageX + 3f) - 1f), new Vector3(CageX + 3f, RimY, WallN(CageX + 3f) + 2f)));
            RoutePoints(G(8f, 63f), G(29f, 66f), new Vector3(16f, LedgeY, RavineZ(16f)), new Vector3(CageX, RimY, cageZ + 3f), new Vector3(BridgeX, 2.3f, northEdge + 3f), G(2f, 104f));
        }

        /// <summary>A ledge's outline along the ravine from x0 to x1 between two wall-following lines (its corners softened).</summary>
        static List<Vector2> Ledge(float x0, float x1, System.Func<float, float> zA, System.Func<float, float> zB)
        {
            var a = new List<Vector2>();
            var b = new List<Vector2>();
            for (float x = x0; x <= x1 + 0.01f; x += 0.5f)
            {
                float t = (x - x0) / (x1 - x0);
                float shrink = 0.35f * (1f - Mathf.Sin(t * Mathf.PI));   // the outer edge comes in at the ends
                a.Add(new Vector2(x, zA(x)));
                b.Add(new Vector2(x, Mathf.Lerp(zB(x), zA(x), shrink * 0.6f)));
            }
            b.Reverse();
            return a.Concat(b).ToList();
        }

        // ------------------------------------------------------------------ shared: points round a trunk

        /// <summary>A point at <paramref name="radius"/> from a giant's axis at height <paramref name="y"/>, toward <paramref name="degrees"/> (0 east, 90 north).</summary>
        static Vector3 Around(NatureTreeBuilder.HeroTree tree, Vector3 foot, float y, float degrees, float radius)
        {
            var t = TrunkAt(tree, foot, y);
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(t.center.x + Mathf.Cos(a) * radius, y, t.center.y + Mathf.Sin(a) * radius);
        }

        static Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        /// <summary>A terrace round a trunk from <paramref name="a0"/> to <paramref name="a1"/> degrees (counter-clockwise), its outer edge at <paramref name="outer"/>(degrees).</summary>
        static List<Vector2> Terrace(NatureTreeBuilder.HeroTree tree, Vector3 foot, float y, float a0, float a1, System.Func<float, float> outer, int samples = 40)
        {
            var t = TrunkAt(tree, foot, y);
            return Poly2.Arc(t.center, t.radius + 0.12f, a => outer(a * Mathf.Rad2Deg), a0 * Mathf.Deg2Rad, a1 * Mathf.Deg2Rad, samples);
        }

        /// <summary>A ring deck all round a trunk (an outline with the trunk as a notch from one side: the deck closes round it).</summary>
        static List<Vector2> Ring(NatureTreeBuilder.HeroTree tree, Vector3 foot, float y, float seam, System.Func<float, float> outer, int samples = 48) =>
            Terrace(tree, foot, y, seam + 0.6f, seam + 359.4f, outer, samples);

        /// <summary>A ring deck round a trunk (<see cref="Ring"/>): no rail runs along its seam.</summary>
        static GameObject RingDeck(Transform p, string name, NatureTreeBuilder.HeroTree tree, Vector3 foot, float y, float seam, System.Func<float, float> outer,
                                   float grain, params TimberKit.Opening[] openings)
        {
            var t = TrunkAt(tree, foot, y);
            bool OnSeam(Vector2 q)
            {
                var d = q - t.center;
                float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                return Mathf.Abs(Mathf.DeltaAngle(a, seam)) < 2.5f && d.magnitude < outer(seam) - 0.35f;
            }
            return TimberKit.Deck(p, name, Ring(tree, foot, y, seam, outer), _ => y,
                                  new TimberKit.DeckSpec { grain = grain, rails = true, openings = openings, railSkip = OnSeam }, SiteAt(y));
        }

        // ------------------------------------------------------------------ act C: the foot of the old oak

        const float T1Y = 11.5f, T2Y = 19.5f;
        const float LiftA = -22f, LiftB = 20f;   // the two cages round the oak (degrees)

        static float OakT1Outer(float a)
        {
            float r = Mathf.Deg2Rad * a;
            return TrunkAt(OldOak, HeroFeet[0], T1Y).radius + 3.1f + 1.4f * (0.5f + 0.5f * Mathf.Sin(2.2f * r + 0.5f)) + 0.5f * Mathf.Cos(3.7f * r);
        }

        /// <summary>The upper terrace's edge: its own wave, pulled in to the lower one's over the cages (they rise beside both).</summary>
        static float OakT2Outer(float a)
        {
            float r = Mathf.Deg2Rad * a;
            float own = TrunkAt(OldOak, HeroFeet[0], T2Y).radius + 3.3f + 1.6f * (0.5f + 0.5f * Mathf.Sin(1.7f * r + 2f)) + 0.6f * Mathf.Cos(4.3f * r);
            float near = Mathf.Max(0f, 1f - Mathf.Min(Mathf.Abs(Mathf.DeltaAngle(a, LiftA)), Mathf.Abs(Mathf.DeltaAngle(a, LiftB))) / 18f);
            float cage = OakT1Outer(a) - 0.1f;
            return near > 0f ? Mathf.Min(own, Mathf.Lerp(own, cage, Mathf.Clamp01(near * 1.6f))) : own;
        }

        /// <summary>
        /// Le Pied du Vieux Chêne: a stair from the clearing climbs round a boulder to the oak's lower terrace; two pulley cages lift
        /// the team to the upper one (one rises while an empty-handed body holds the counterweight below, the other while someone
        /// stands on the winch above); then the signal hoops: three hoops, three bridges, one leads on. The glyph that does is on
        /// the board at the catcher's balcony, the thrower sees only the hoops' glyphs. A wrong hoop lowers a bridge to a dead end
        /// and costs its time; every hoop lets go again by itself.
        /// </summary>
        static void ActOak(Transform p)
        {
            var oak = OldOak;
            var foot = HeroFeet[0];
            Anchors.Clear();
            for (int l = 0; l < oak.limbs.Count; l++) Anchors.Add(foot + oak.LimbPoint(l, 0.7f));

            // the stair from the clearing round its boulder to the lower terrace's west end
            var t1Entry = Around(oak, foot, T1Y, 200f, OakT1Outer(200f) - 0.4f);
            NatureKit.Rock(p, "Stair boulder", G(-5.9f, 113.6f, 1.2f), new Vector3(3.6f, 4f, 3.4f), "Nature_MossyRock", Quaternion.Euler(0f, 25f, 0f));
            Walk(p, "Oak stair", new[]
            {
                G(1.4f, 108.4f, 0.05f), new Vector3(-0.4f, GroundAt(-0.4f, 111.2f) + 0.4f, 111.2f), new Vector3(-1.6f, 8.6f, 114.6f), new Vector3(-1.6f, 8.6f, 115.8f),
                new Vector3(-0.5f, 10.5f, 118.6f), new Vector3(t1Entry.x - 1.6f, T1Y, t1Entry.z - 1.4f), t1Entry,
            }, 1.6f, true, true, false, 9f);

            // the lower terrace (south and east of the trunk) and the upper one (east, north, west)
            Deck(p, "Lower terrace", Terrace(oak, foot, T1Y, 165f, 375f, OakT1Outer), T1Y, 15f, true, 9f,
                 new TimberKit.Opening(XZ(t1Entry), 2.2f),
                 new TimberKit.Opening(XZ(Around(oak, foot, T1Y, LiftA, OakT1Outer(LiftA))), 2.6f),
                 new TimberKit.Opening(XZ(Around(oak, foot, T1Y, LiftB, OakT1Outer(LiftB))), 2.6f));
            Deck(p, "Upper terrace", Terrace(oak, foot, T2Y, -45f, 220f, OakT2Outer, 56), T2Y, -25f, true, 14f,
                 new TimberKit.Opening(XZ(Around(oak, foot, T2Y, LiftA, OakT2Outer(LiftA))), 2.6f),
                 new TimberKit.Opening(XZ(Around(oak, foot, T2Y, LiftB, OakT2Outer(LiftB))), 2.6f),
                 new TimberKit.Opening(XZ(Around(oak, foot, T2Y, 132f, OakT2Outer(132f))), 2.6f),
                 new TimberKit.Opening(XZ(Around(oak, foot, T2Y, 80f, OakT2Outer(80f))), 2.6f),
                 new TimberKit.Opening(XZ(Around(oak, foot, T2Y, 186f, OakT2Outer(186f))), 2.6f),
                 new TimberKit.Opening(XZ(Around(oak, foot, T2Y, 200f, OakT2Outer(200f))), 2f));

            // the two cages: the counterweight (hands-free) below lifts the first, the winch above lifts the second
            var counterweight = HandsFreePlate(p, "Counterweight", Around(oak, foot, T1Y, -1f, OakT1Outer(-1f) - 1.3f));
            var winch = PlainPlate(p, "Oak winch", Around(oak, foot, T2Y, 38f, OakT2Outer(38f) - 1.4f));
            foreach (var (a, src, name) in new[] { (LiftA, (MonoBehaviour)counterweight, "Cage A"), (LiftB, (MonoBehaviour)winch, "Cage B") })
            {
                var floor = Around(oak, foot, T1Y, a, OakT1Outer(a) + 1.25f);
                Cage(p, name, floor, new Vector2(2.3f, 2.3f), 90f - a, T2Y - T1Y, src, 3f);
                PulleyBeam(p, Around(oak, foot, T2Y + 4.5f, a, 0f), floor + Vector3.up * (T2Y - T1Y + 4.4f));
            }

            // the signal hoops: the thrower on the upper terrace's south-west end, the catcher on the balcony beyond the hoops
            var throwSpot = Around(oak, foot, T2Y, 206f, OakT2Outer(206f) - 1.4f);
            var balconyArc = Terrace(oak, foot, T2Y, 228f, 276f, a => TrunkAt(oak, foot, T2Y).radius + 16.6f + 0.6f * Mathf.Sin(a * 0.11f), 20);
            var balcony = Clip(balconyArc, XZ(Around(oak, foot, T2Y, 252f, 13.2f)), -Dir(252f));
            Deck(p, "Signal balcony", balcony, T2Y, 72f, true, 2f, new TimberKit.Opening(XZ(Around(oak, foot, T2Y, 228.5f, 15f)), 1.9f));
            Walk(p, "Balcony catwalk", new[] { Around(oak, foot, T2Y, 200f, OakT2Outer(200f) - 0.6f), Around(oak, foot, T2Y, 210f, 11.5f), Around(oak, foot, T2Y, 219f, 14.3f),
                                               Around(oak, foot, T2Y, 230f, 15.1f) },
                 1.5f, true, true, false, 2f);
            ClueBoard(p, Around(oak, foot, T2Y, 252f, 13.5f), Around(oak, foot, T2Y, 252f, 17f), Glyph.Owl);

            // three bridges from the upper terrace: the owl's leads on (checkpoint 3), the leaf's and the axe's to dead ends
            var targets = new (Glyph glyph, float angle, bool on)[] { (Glyph.Owl, 132f, true), (Glyph.Leaf, 186f, false), (Glyph.Axe, 80f, false) };
            int[] hoopOrder = { 1, 0, 2 };   // left, middle, right as the thrower sees them: leaf, owl, axe
            var receive = new[] { 238f, 252f, 266f };
            for (int k = 0; k < 3; k++)
            {
                var (glyph, angle, on) = targets[hoopOrder[k]];
                var catchAt = Around(oak, foot, T2Y, receive[k], 15.4f);
                var pass = Pass(p, $"Hoop {glyph}", throwSpot + Vector3.up * 1.5f, catchAt + Vector3.up * 1.5f, timed: true, opening: 2.4f);
                var hoop = HoopOnPass(p, $"Hoop {glyph}", pass, 0.72f, 11f);
                GlyphSign(p, hoop.transform.position + Vector3.up * 1.85f, hoop.transform.rotation * Vector3.back, glyph);
                float edge = OakT2Outer(angle);
                float stubRadius = on ? 3.6f : 2.1f;
                var from = Around(oak, foot, T2Y, angle, edge);
                var to = Around(oak, foot, T2Y, angle, edge + 14.2f);
                SlideBridge(p, $"{glyph} bridge", from - (to - from).normalized * 0.4f, to + (to - from).normalized * 0.5f, 2f, hoop, 1.8f);
                var center = Around(oak, foot, T2Y, angle, edge + 14.2f + stubRadius);
                var outline = Blob(XZ(center), stubRadius + 0.2f, stubRadius, 0.12f, (int)angle, angle);
                if (on)
                    Deck(p, "Owl landing", outline, T2Y, angle + 90f, true, 14f, new TimberKit.Opening(XZ(to), 2.6f),
                         new TimberKit.Opening(XZ(center) + new Vector2(0.2f, 3.7f), 2.2f), new TimberKit.Opening(XZ(center) + new Vector2(-3.6f, -0.9f), 2.2f));
                else Deck(p, $"{glyph} stub", outline, T2Y, angle + 90f, true, 14f, new TimberKit.Opening(XZ(to), 2.6f));
                GlyphSign(p, from + Vector3.up * 2.8f - (to - from).normalized * 0.6f, -(to - from).normalized, glyph);
                if (on)
                {
                    CP(p, 3, center + (center - from).normalized * 0.6f, Quaternion.Euler(0f, 90f, 0f) * (center - from).normalized * 2.6f);
                    OwlLanding = center;
                }
            }

            var t2Near = Around(oak, foot, T2Y, 132f, OakT2Outer(132f));
            Contract(p, "Two cages, one each way: an empty-handed body on the counterweight lifts the first, the bomb is thrown down, then " +
                        "the winch above lifts the second. Then the hoops: the catcher reads the glyph on the balcony board, the thrower " +
                        "sends the bomb through that hoop; its bridge leads on, the others to dead ends.",
                     ClimbLock("the trunk face from the lower terrace", Around(oak, foot, T1Y, 290f, OakT1Outer(290f) - 1f), Around(oak, foot, T2Y, 290f, 3f)),
                     GapLock("the upper terrace to the owl landing", t2Near, Around(oak, foot, T2Y, 132f, OakT2Outer(132f) + 14.2f)));
            RoutePoints(G(0f, 112f), t1Entry, Around(oak, foot, T2Y, 90f, 5f), throwSpot, OwlLanding);
        }

        static Vector3 OwlLanding;

        enum Glyph { Owl, Leaf, Axe }

        /// <summary>A ring hung where <paramref name="pass"/>'s arc is <paramref name="along"/> of the way, facing the pass, on ropes from above.</summary>
        static BombGate HoopOnPass(Transform p, string name, PassCorridor pass, float along, float hold)
        {
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/ScriptableObjects/Tuning/GameTuning.asset");
            var samples = new List<Vector3>();
            if (!pass.TrySample(tuning, samples)) throw new System.InvalidOperationException("unreachable " + pass.name);
            var center = samples[Mathf.Clamp(Mathf.RoundToInt(samples.Count * along), 0, samples.Count - 1)];
            var flat = pass.To - pass.From;
            flat.y = 0;
            var rot = Quaternion.LookRotation(flat.normalized);
            var gate = Place(p, GateRing, name, center, rot).GetComponent<BombGate>();
            SetField(gate, "holdSeconds", v => v.floatValue = hold);
            Timber(p, name + " ropes", mb =>
            {
                var side = rot * Vector3.right;
                foreach (float s in new[] { -1f, 1f })
                    mb.Tube(new[] { center + side * s * 1.1f + Vector3.up * 0.75f, center + side * s * 0.6f + Vector3.up * 4.2f }, new[] { 0.025f, 0.025f }, 5, Rope);
            });
            return gate;
        }

        // ------------------------------------------------------------------ act D: the walkways

        const float LowY = 16f, HighY = 24.5f, PorchY = 27f;

        /// <summary>
        /// Les Passerelles: two walks from the owl landing to the leaning pine's porch, a low one round the crag and a high one over
        /// it, never apart long enough to stop passing. The high walk's gate rises while an empty-handed body stands on the low walk's
        /// plate; the low walk's gate while the high walk's hourglass runs. One each way, the bomb passed between them; then the high
        /// walk crosses rotten planks to the porch and the low one climbs the pine's stair.
        /// </summary>
        static void ActWalkways(Transform p)
        {
            var d0 = OwlLanding;
            var pine = LeaningPine;
            var pineFoot = HeroFeet[1];
            var porchC = Around(pine, pineFoot, PorchY, 0f, 0f) + new Vector3(3f, 0f, -8.2f);
            Anchors.Clear();
            Anchors.Add(Around(pine, pineFoot, 40f, 0f, 0f));
            Anchors.Add(Around(OldOak, HeroFeet[0], 30f, 0f, 0f));
            for (int l = 0; l < OldOak.limbs.Count; l++) Anchors.Add(HeroFeet[0] + OldOak.LimbPoint(l, 0.8f));

            // the high walk: up from the landing, through its gate, over the crag
            var crag = new Vector3(-15.4f, HighY, 153.2f);
            var high = new[]
            {
                new Vector3(d0.x + 0.2f, d0.y, d0.z + 3.2f), new Vector3(d0.x + 0.3f, d0.y + 2.5f, d0.z + 7.1f), new Vector3(d0.x + 0.1f, d0.y + 2.5f, d0.z + 8.3f),
                new Vector3(d0.x - 3.8f, HighY, d0.z + 9.3f), new Vector3(-10.4f, HighY, d0.z + 9.5f), new Vector3(-12f, HighY, 151.6f),
            };
            Walk(p, "High walk", high, 1.8f, true, true, false, 20f);
            var lowPlate = HandsFreePlate(p, "Low plate", new Vector3(-17.2f, LowY, 141.6f));
            WingedGate(p, "High gate", new Vector3(-10.8f, HighY, d0.z + 9.55f), new Vector3(-1f, 0f, 0.15f), 1.8f, lowPlate);
            Deck(p, "Crag lookout", Blob(new Vector2(crag.x, crag.z), 3.6f, 3.1f, 0.14f, 21, 30f), HighY, 40f, true, 14f,
                 new TimberKit.Opening(new Vector2(-12f, 151.8f), 2.2f), new TimberKit.Opening(new Vector2(-18.3f, 155.4f), 2f));
            var highPlate = HourglassPlate(p, "Crag winch", new Vector3(-17.2f, HighY, 151.1f), 6f);
            // rotten planks from the crag up to the porch's south edge, each half a metre above the last: hop up them before they go
            var plankFrom = new Vector3(-18.3f, HighY, 155.4f);
            var porchEdge = porchC + new Vector3(3f, 0f, -3.9f);
            var dirP = porchEdge - plankFrom;
            dirP.y = 0f;
            const int Planks = 5;
            float yawP = Mathf.Atan2(dirP.x, dirP.z) * Mathf.Rad2Deg;
            float plankLength = (dirP.magnitude - 0.6f) / Planks;
            for (int i = 0; i < Planks; i++)
            {
                var c = plankFrom + dirP.normalized * (0.3f + (i + 0.5f) * plankLength);
                c.y = HighY + (PorchY - HighY) * (i + 1f) / Planks;
                RottenPlank(p, "Rotten plank", c, 1.6f, plankLength - 0.15f, yawP + (i % 2 == 0 ? 3f : -3f));
            }

            // the low walk: down from the landing, round the crag's west flank, through its gate, under the swinging log, up the pine's stair
            Walk(p, "Low stair", new[] { d0 + new Vector3(-3.2f, 0f, -0.9f), d0 + new Vector3(-4.4f, 0f, -1f), new Vector3(-15.4f, LowY, 138.6f), new Vector3(-16f, LowY, 139f) },
                 1.6f, true, true, false, 14f);
            var low = new[] { new Vector3(-16f, LowY, 139f), new Vector3(-17.2f, LowY, 141.2f), new Vector3(-20.6f, LowY, 143.6f), new Vector3(-23.3f, LowY, 147.6f),
                              new Vector3(-24.6f, LowY, 152f), new Vector3(-25.4f, LowY, 155.6f) };
            Walk(p, "Low walk", low, 1.8f, true, true, false, 14f);
            WingedGate(p, "Low gate", new Vector3(-21.5f, LowY, 144.6f), new Vector3(-0.66f, 0f, 1f), 1.8f, highPlate);
            AddRotator(p, ObstaclesDir + "Obstacle_Sweeper", "Swinging log", new Vector3(-24.1f, LowY, 150f), 3.6f, 70f, 0f);
            // the pine's stair: three flights round its trestle up to the porch's west end
            Walk(p, "Pine stair", new[]
            {
                new Vector3(-25.4f, LowY, 155.6f), new Vector3(-26.2f, LowY, 156.6f), new Vector3(-32.2f, LowY + 3.67f, 156.2f), new Vector3(-33.4f, LowY + 3.67f, 156f),
                new Vector3(-38.8f, LowY + 7.33f, 153.6f), new Vector3(-39.8f, LowY + 7.33f, 154.4f),
                new Vector3(porchC.x - 7.5f, PorchY, porchC.z - 2.7f), new Vector3(porchC.x - 6.7f, PorchY, porchC.z - 2.2f), new Vector3(porchC.x - 5.6f, PorchY, porchC.z - 1.6f),
            }, 1.6f, true, true, false, 14f);

            // the porch (checkpoint 4)
            Deck(p, "Porch", Blob(new Vector2(porchC.x, porchC.z), 6.6f, 3.8f, 0.08f, 23, 4f), PorchY, 0f, true, 14f,
                 new TimberKit.Opening(new Vector2(porchC.x + 3f, porchC.z - 3.8f), 2.4f), new TimberKit.Opening(new Vector2(porchC.x - 6.2f, porchC.z - 1.9f), 2.6f),
                 new TimberKit.Opening(new Vector2(porchC.x + 1f, porchC.z + 3.8f), 14f));
            PorchCenter = porchC;
            CP(p, 4, porchC + new Vector3(2.2f, 0f, -1.2f), new Vector3(-2.8f, 0f, 0.2f));

            Contract(p, "Two walks: an empty-handed body on the low walk's plate raises the high walk's gate, the high walk's hourglass raises " +
                        "the low one's; one walker each way, the bomb passed between them, then rotten planks above and the stair below.",
                     ClimbLock("from the low walk to the high one", new Vector3(-17.2f, LowY, 141.6f), new Vector3(-13f, HighY, 147f)));
            RoutePoints(d0, new Vector3(-12.4f, HighY, 148.5f), new Vector3(-20.6f, LowY, 143.6f), porchC);
        }

        static Vector3 PorchCenter;

        // ------------------------------------------------------------------ act E: the winch house

        /// <summary>
        /// Le Poste de Treuil: the two-storey winch house on the leaning pine (its trunk through its west wall), roofed, so nothing is
        /// lobbed into it. A pass through the porch's hoop opens the stairwell's door for a few seconds; the runner climbs behind a
        /// curtain of sparks (no bomb crosses it) while the carrier waits in the cold larder (the fuse at half speed) by three log
        /// chutes. Only one chute comes out upstairs: the runner sees which, and calls it. A wrong chute drops the bomb back on the
        /// ground floor. Upstairs, the winch lifts the carrier's cage; the north terrace (under the eaves) is checkpoint 5.
        /// </summary>
        static void ActWinchHouse(Transform p)
        {
            WinchHouse(p, LeaningPine, HeroFeet[1], PorchCenter);
            RoutePoints(PorchCenter, HouseCenter, TerraceCenter);
        }

        // ------------------------------------------------------------------ act F: the canopy

        const float SnagY = 34f, TwinY = 36f, TowerY = 36f, PerchY = 41f, LookoutY = 44f;

        /// <summary>
        /// La Canopée: a long rope bridge from the terrace into the crowns (a swinging log sweeps it), rotten planks to the twin fir,
        /// its winch bridge (an hourglass: empty hands hold it, the other crosses), the old signal tower, then the race: a pass
        /// through the tower's hoop lowers the lookout's drawbridge for a few seconds, up a hanging stair beyond. The lookout: the
        /// bell arch (the bomb flies through it) and the whole team aboard.
        /// </summary>
        static void ActCanopy(Transform p)
        {
            var twin = TwinFir;
            var look = LookoutFir;
            var snag = Snag;
            Anchors.Clear();
            Anchors.Add(Around(twin, HeroFeet[2], 47f, 0f, 0f));
            Anchors.Add(Around(look, HeroFeet[3], 50f, 0f, 0f));
            Anchors.Add(Around(snag, HeroFeet[4], 41f, 0f, 0f));

            // the snag's deck and the long bridge from the terrace to it
            var snagC = Around(snag, HeroFeet[4], SnagY, 0f, 0f);
            var snagR = TrunkAt(snag, HeroFeet[4], SnagY).radius;
            var terraceEdge = TerraceCenter + new Vector3(0.6f, 0f, 1.6f);
            var toSnag = snagC - terraceEdge;
            toSnag.y = 0f;
            var snagEntry = snagC - toSnag.normalized * (snagR + 3.2f);
            snagEntry.y = SnagY;
            RopeSpan(p, "Crown bridge", terraceEdge + toSnag.normalized * 0.4f, snagEntry, 1.6f, 1.1f);
            var mid = Vector3.Lerp(terraceEdge, snagEntry, 0.55f);
            AddRotator(p, ObstaclesDir + "Obstacle_Sweeper", "Swinging log (crown)", mid + Vector3.down * 1.2f, 3.4f, 60f, 90f);
            RingDeck(p, "Snag deck", snag, HeroFeet[4], SnagY, 200f, a => snagR + 3.3f + 0.6f * Mathf.Sin(a * Mathf.Deg2Rad * 2f + 1f), 30f,
                 new TimberKit.Opening(XZ(snagEntry), 1.8f), new TimberKit.Opening(XZ(snagC) + Dir(34f) * (snagR + 3.5f), 1.8f));

            // rotten planks up to the twin fir's ring
            var twinC = Around(twin, HeroFeet[2], TwinY, 0f, 0f);
            var twinR = TrunkAt(twin, HeroFeet[2], TwinY).radius;
            var plankFrom = new Vector3(snagC.x, SnagY, snagC.z) + V3(Dir(34f), 0f) * (snagR + 3.4f);
            var toTwin = twinC - plankFrom;
            toTwin.y = 0f;
            var plankTo = new Vector3(twinC.x, TwinY, twinC.z) - toTwin.normalized * (twinR + 4.1f);
            int planks = 6;
            float yaw = Mathf.Atan2(toTwin.x, toTwin.z) * Mathf.Rad2Deg;
            float stepLength = Vector3.Distance(new Vector3(plankFrom.x, 0, plankFrom.z), new Vector3(plankTo.x, 0, plankTo.z)) / planks;
            for (int i = 0; i < planks; i++)
            {
                var c = Vector3.Lerp(plankFrom, plankTo, (i + 0.5f) / planks);
                c.y = SnagY + (TwinY - SnagY) * (i + 1f) / (planks + 1f);
                RottenPlank(p, "Rotten plank (crown)", c, 1.6f, stepLength - 0.1f, yaw + (i % 2 == 0 ? 4f : -2f));
            }
            RingDeck(p, "Twin fir ring", twin, HeroFeet[2], TwinY, 300f, a => twinR + 3.4f + 0.8f * Mathf.Sin(a * Mathf.Deg2Rad * 3f), -20f,
                 new TimberKit.Opening(XZ(plankTo), 1.8f), new TimberKit.Opening(XZ(twinC) + Dir(128f) * (twinR + 3.6f), 2.4f));

            // the twin fir's winch bridge to the old signal tower (an hourglass: empty hands hold it)
            var bridgeFrom = new Vector3(twinC.x, TwinY, twinC.z) + V3(Dir(128f), 0f) * (twinR + 3.6f);
            var bridgeTo = bridgeFrom + V3(Dir(128f), 0f) * 14.4f;
            var towerC = bridgeTo + V3(Dir(128f), 0f) * 3.2f;
            var winch = HourglassPlate(p, "Twin winch", new Vector3(twinC.x, TwinY, twinC.z) + V3(Dir(170f), 0f) * (twinR + 2.2f), 6f);
            SlideBridge(p, "Winch bridge", bridgeFrom - V3(Dir(128f), 0f) * 0.4f, bridgeTo + V3(Dir(128f), 0f) * 0.5f, 2f, winch, 1.6f);
            Deck(p, "Signal tower", Blob(new Vector2(towerC.x, towerC.z), 3.4f, 3.2f, 0.1f, 31, 15f), TowerY, 128f, true, 40f,
                 new TimberKit.Opening(XZ(bridgeTo), 2.4f), new TimberKit.Opening(new Vector2(towerC.x, towerC.z) + Dir(60f) * 3.2f, 1.8f));

            // the race: the hoop on the tower lowers the lookout's drawbridge; a hanging stair up to the perch; the drawbridge; the lookout
            var lookC = Around(look, HeroFeet[3], LookoutY, 0f, 0f);
            var lookR = TrunkAt(look, HeroFeet[3], LookoutY).radius;
            var lookEdge = new Vector3(lookC.x, LookoutY, lookC.z) + V3(Dir(-6f), 0f) * (lookR + 4.2f);
            var perchC = lookEdge + V3(Dir(-6f), 0f) * (14.6f + 2.4f);
            perchC.y = PerchY;
            var stairStart = new Vector3(towerC.x, TowerY, towerC.z) + V3(Dir(60f), 0f) * 3.3f;
            var perchEntry = perchC + (stairStart - perchC).normalized * 2.2f;
            perchEntry.y = PerchY;
            Walk(p, "Hanging stair", new[] { stairStart, Vector3.Lerp(stairStart, perchEntry, 0.35f) + Vector3.up * 0.4f, Vector3.Lerp(stairStart, perchEntry, 0.7f) + Vector3.up * 3.2f, perchEntry },
                 1.6f, true, true, false, 8f);
            Deck(p, "Perch", Blob(new Vector2(perchC.x, perchC.z), 2.6f, 2.4f, 0.1f, 33, 0f), PerchY, 84f, true, 8f,
                 new TimberKit.Opening(XZ(perchEntry), 1.8f), new TimberKit.Opening(XZ(perchC) + Dir(174f) * 2.4f, 2.4f));
            var hoopPass = Pass(p, "Race hoop", new Vector3(towerC.x, TowerY + 1.5f, towerC.z) + V3(Dir(220f), 0f) * 2.4f,
                                new Vector3(towerC.x, TowerY + 1.5f, towerC.z) + V3(Dir(40f), 0f) * 2.6f, timed: true, opening: 2.4f);
            var raceHoop = HoopOnPass(p, "Race hoop", hoopPass, 0.5f, 13f);
            Drawbridge(p, "Lookout drawbridge", lookEdge + V3(Dir(-6f), 0f) * 0.2f, new Vector3(perchC.x, PerchY, perchC.z) + V3(Dir(174f), 0f) * 1.6f, 2.2f, raceHoop, 76f, 2f);

            // the lookout: a ring round the fir, the bell arch at the drawbridge, the finish
            RingDeck(p, "Lookout", look, HeroFeet[3], LookoutY, 180f, a => lookR + 4.4f + 0.9f * Mathf.Sin(a * Mathf.Deg2Rad * 2f + 0.6f) + 0.4f * Mathf.Cos(a * Mathf.Deg2Rad * 5f),
                     10f, new TimberKit.Opening(XZ(lookEdge), 2.6f));
            var pad = new Vector3(lookC.x, LookoutY, lookC.z) + V3(Dir(60f), 0f) * (lookR + 2.6f);
            var archFloor = new Vector3(lookC.x, LookoutY, lookC.z) + V3(Dir(-4f), 0f) * (lookR + 2.6f);
            ArchCheckpoint(p, "CP_06", 6, pad, FuseFor(6), archFloor);
            var arch = p.Find("CP_06_Arch");
            arch.rotation = Quaternion.LookRotation(V3(Dir(176f), 0f));
            NatureKit.CampfireCheckpoint(p, p.Find("CP_06").GetComponent<Checkpoint>(), new Vector3(lookC.x, LookoutY, lookC.z) + V3(Dir(120f), 0f) * (lookR + 2.8f));
            Place(p, GameplayDir + "FinishZone", "FinishZone", new Vector3(lookC.x, LookoutY, lookC.z) + V3(Dir(200f), 0f) * (lookR + 2.6f), Quaternion.identity);
            Pass(p, "Through the bell arch", archFloor + V3(Dir(-4f), 0f) * 4.5f + Vector3.up * 1.5f, archFloor - V3(Dir(-4f), 0f) * 1.2f + Vector3.up * 1.5f, timed: true, opening: 3f);

            Contract(p, "The long bridge into the crowns under a swinging log, rotten planks, the twin fir's winch bridge (empty hands hold " +
                        "its hourglass while the other crosses), then the race: a pass through the tower's hoop lowers the lookout's " +
                        "drawbridge for thirteen seconds, both run; the bomb through the bell arch, the team aboard.",
                     GapLock("the twin fir to the signal tower", bridgeFrom, bridgeTo),
                     GapLock("the perch to the lookout", new Vector3(perchC.x, PerchY, perchC.z) + V3(Dir(174f), 0f) * 2.4f, lookEdge));
            RoutePoints(snagC, twinC, towerC, perchC, lookC);
        }
    }
}
