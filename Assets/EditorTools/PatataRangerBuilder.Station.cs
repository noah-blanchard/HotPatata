using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using static HotPatata.Editor.BombObstacleKitBuilder;

namespace HotPatata.Editor
{
    /// <summary>
    /// PatataRanger's buildings and machines (ARCHITECTURE §25.5): the camp, the gate tower, the winch booths, the winch house and
    /// the lookout. Timber frames with braces, plank walls, shingle roofs, drums, cranks and ropes that run to what they work.
    /// Decoration only (renderers), except walls a body must not pass, which collide as boxes.
    /// </summary>
    public static partial class PatataRangerBuilder
    {
        const int Plank = TimberKit.Plank, Aged = TimberKit.PlankAged, Wood = TimberKit.Timber, Iron = TimberKit.Iron, Rope = TimberKit.Rope;

        static void Post(MeshBuilder mb, Vector3 foot, float height, float size = 0.22f) => TimberBuildings.Post(mb, foot, height, size);

        static void Roof(MeshBuilder mb, Vector3 eaveCenter, float halfX, float halfZ, float rise, Quaternion rot, int slot = Aged, float overhang = 0.45f) =>
            TimberBuildings.Roof(mb, eaveCenter, halfX, halfZ, rise, rot, slot, overhang);

        /// <summary>A winch drum with its crank: a log drum on two cheeks, an iron crank handle.</summary>
        static void Drum(MeshBuilder mb, Vector3 center, Quaternion rot, float length = 1.3f, float radius = 0.28f)
        {
            var axis = rot * Vector3.right;
            mb.Tube(new[] { center - axis * length * 0.5f, center + axis * length * 0.5f }, new[] { radius, radius }, 14, Wood, true);
            foreach (float s in new[] { -1f, 1f })
            {
                var cheek = center + axis * s * (length * 0.5f + 0.08f);
                mb.Tube(new[] { cheek - axis * 0.05f, cheek + axis * 0.05f }, new[] { radius + 0.12f, radius + 0.12f }, 14, Wood, true);
                mb.Beam(cheek + Vector3.down * 1.1f, cheek + Vector3.up * 0.1f, 0.14f, 0.14f, Wood);
            }
            var hub = center + axis * (length * 0.5f + 0.25f);
            mb.Tube(new[] { center, hub }, new[] { 0.05f, 0.05f }, 8, Iron, true);
            var arm = rot * Vector3.up * 0.45f;
            mb.Beam(hub, hub + arm, 0.06f, 0.06f, Iron);
            mb.Tube(new[] { hub + arm, hub + arm + axis * 0.3f }, new[] { 0.04f, 0.04f }, 8, Wood, true);
            // rope wound on the drum
            for (int k = 0; k < 7; k++)
            {
                float x = -length * 0.35f + k * length * 0.1f;
                var ring = new List<Vector3>();
                for (int i = 0; i <= 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f;
                    ring.Add(center + axis * x + rot * new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a)) * (radius + 0.03f));
                }
                mb.Tube(ring, ring.Select(_ => 0.03f).ToList(), 5, Rope);
            }
        }

        /// <summary>
        /// The booth over a winch plate: four posts, a pitched roof, the drum against its back and a rope from the drum up to
        /// <paramref name="ropeTo"/> (the thing it works: a gate tower's pulley, a bridge's mast).
        /// </summary>
        static void WinchBooth(Transform p, Vector3 plate, Quaternion rot, Vector3 ropeTo)
        {
            Timber(p, "Winch booth", mb =>
            {
                const float hx = 1.9f, hz = 1.6f, h = 2.7f;
                foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    Post(mb, plate + rot * new Vector3(sx * hx, 0f, sz * hz), h + 0.1f * sx, 0.2f);
                foreach (int sz in new[] { -1, 1 })
                    mb.Beam(plate + rot * new Vector3(-hx - 0.2f, h, sz * hz), plate + rot * new Vector3(hx + 0.2f, h + 0.1f, sz * hz), 0.16f, 0.2f, Wood);
                foreach (int sx in new[] { -1, 1 })
                    mb.Beam(plate + rot * new Vector3(sx * hx, h, -hz - 0.2f), plate + rot * new Vector3(sx * hx, h, hz + 0.2f), 0.16f, 0.2f, Wood);
                Roof(mb, plate + Vector3.up * (h + 0.15f), hx, hz, 0.9f, rot);
                var drum = plate + rot * new Vector3(0f, 1.15f, hz - 0.35f);
                Drum(mb, drum, rot);
                mb.Tube(new[] { drum + Vector3.up * 0.3f, Vector3.Lerp(drum, ropeTo, 0.5f) + Vector3.down * 0.4f, ropeTo }, new[] { 0.03f, 0.03f, 0.03f }, 6, Rope);
            });
        }

        /// <summary>The tower over the yard gate: two leg pairs braced, a head frame the gate rises into, a pulley wheel, a little roof.</summary>
        static void GateTower(Transform p, Vector3 floor)
        {
            Timber(p, "Gate tower", mb =>
            {
                float half = GateHalf + 0.75f, top = 9.6f;
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -0.75f, 0.75f })
                        Post(mb, new Vector3(GateX + sx * half, floor.y, RidgeZ + sz), top + 0.4f, 0.32f);
                    mb.Beam(new Vector3(GateX + sx * half, floor.y + 1.2f, RidgeZ - 0.75f), new Vector3(GateX + sx * half, floor.y + top - 1f, RidgeZ + 0.75f), 0.14f, 0.18f, Wood);
                    mb.Beam(new Vector3(GateX + sx * half, floor.y + 1.2f, RidgeZ + 0.75f), new Vector3(GateX + sx * half, floor.y + top - 1f, RidgeZ - 0.75f), 0.14f, 0.18f, Wood);
                }
                foreach (float sz in new[] { -0.75f, 0.75f })
                {
                    mb.Beam(new Vector3(GateX - half - 0.4f, floor.y + top, RidgeZ + sz), new Vector3(GateX + half + 0.4f, floor.y + top, RidgeZ + sz), 0.26f, 0.32f, Wood);
                    mb.Beam(new Vector3(GateX - half, floor.y + top - 2.2f, RidgeZ + sz), new Vector3(GateX - 1.2f, floor.y + top - 0.15f, RidgeZ + sz), 0.16f, 0.18f, Wood);
                    mb.Beam(new Vector3(GateX + half, floor.y + top - 2.2f, RidgeZ + sz), new Vector3(GateX + 1.2f, floor.y + top - 0.15f, RidgeZ + sz), 0.16f, 0.18f, Wood);
                }
                Roof(mb, new Vector3(GateX, floor.y + top + 0.2f, RidgeZ), 0.9f, half + 0.3f, 0.8f, Quaternion.Euler(0f, 90f, 0f));
                // the pulley the winch's rope runs over, an iron hook down to the gate
                var wheel = new Vector3(GateX + 3.6f, floor.y + top - 0.75f, RidgeZ);
                var prof = new[] { new Vector2(0.05f, -0.08f), new Vector2(0.42f, -0.08f), new Vector2(0.36f, 0f), new Vector2(0.42f, 0.08f), new Vector2(0.05f, 0.08f) };
                mb.Lathe(wheel, Quaternion.Euler(0f, 0f, 90f), prof, 14, Wood);
                mb.Tube(new[] { wheel, new Vector3(GateX, floor.y + 4.9f, RidgeZ) }, new[] { 0.03f, 0.03f }, 6, Rope);
            });
        }

        /// <summary>The ranger's camp: the hut, the woodshed, a lean-to over the workbench, posts with lanterns.</summary>
        static void Camp(Transform p)
        {
            var hut = G(-12f, 7f);
            Hut(p, "Ranger hut", hut, Quaternion.Euler(0f, 100f, 0f), 3.2f, 2.6f);
            var shed = G(10.5f, 4f);
            Timber(p, "Woodshed", mb =>
            {
                var rot = Quaternion.Euler(0f, -70f, 0f);
                foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    Post(mb, shed + rot * new Vector3(sx * 2.2f, 0f, sz * 1.1f), sz < 0 ? 2.3f : 2.9f, 0.18f);
                for (int k = 0; k < 6; k++)
                    mb.Box(shed + rot * new Vector3(-2.3f + k * 0.92f, 2.65f, 0f), rot * Quaternion.Euler(-14f, 0f, 0f), new Vector3(0.9f, 0.05f, 3f), Aged);
                // stacked logs under it
                for (int row = 0; row < 4; row++)
                    for (int k = 0; k < 9 - row; k++)
                    {
                        var c = shed + rot * new Vector3(-1.9f + k * 0.42f + row * 0.21f, 0.2f + row * 0.36f, 0.1f);
                        mb.Tube(new[] { c + rot * Vector3.forward * -0.8f, c + rot * Vector3.forward * 0.8f }, new[] { 0.19f, 0.19f }, 8, Wood, true, k * 0.3f);
                    }
            });
            // the colliders of the woodpile (a body stops at it)
            var pile = new GameObject("Woodpile body") { layer = Layer("Environment") };
            pile.transform.SetParent(p, false);
            pile.transform.SetPositionAndRotation(shed + Vector3.up * 0.8f, Quaternion.Euler(0f, -70f, 0f));
            pile.AddComponent<BoxCollider>().size = new Vector3(4.2f, 1.6f, 1.8f);
        }

        static void Hut(Transform p, string name, Vector3 foot, Quaternion rot, float halfX, float halfZ) => TimberBuildings.Hut(p, name, foot, rot, halfX, halfZ);

        static void SolidTimberInvisible(Transform p, string name, Vector3 center, Quaternion rot, Vector3 size) =>
            TimberBuildings.SolidInvisible(p, name, center, rot, size);

        // ------------------------------------------------------------------ pulleys, signs, trestles

        /// <summary>A beam out from a trunk over a cage, braced to the trunk below, with its pulley wheel and a hook.</summary>
        static void PulleyBeam(Transform p, Vector3 trunkAxis, Vector3 overCage)
        {
            Timber(p, "Pulley beam", mb =>
            {
                var flat = overCage - trunkAxis;
                flat.y = 0f;
                var dir = flat.normalized;
                var a = new Vector3(trunkAxis.x, overCage.y, trunkAxis.z) + dir * 0.8f;
                var b = overCage + dir * 0.7f;
                mb.Beam(a, b, 0.24f, 0.3f, Wood);
                mb.Beam(a + Vector3.down * 2.8f + dir * 0.4f, Vector3.Lerp(a, b, 0.62f) + Vector3.down * 0.12f, 0.16f, 0.18f, Wood);
                var side = Vector3.Cross(Vector3.up, dir).normalized;
                var wheel = overCage + Vector3.down * 0.45f;
                var prof = new[] { new Vector2(0.05f, -0.07f), new Vector2(0.38f, -0.07f), new Vector2(0.32f, 0f), new Vector2(0.38f, 0.07f), new Vector2(0.05f, 0.07f) };
                mb.Lathe(wheel, Quaternion.LookRotation(Vector3.up, dir) * Quaternion.Euler(90f, 0f, 0f), prof, 14, Wood);
                mb.Beam(overCage + side * 0.12f, wheel + side * 0.12f, 0.05f, 0.05f, Iron);
                mb.Beam(overCage - side * 0.12f, wheel - side * 0.12f, 0.05f, 0.05f, Iron);
                mb.Tube(new[] { wheel + dir * 0.36f, wheel + dir * 0.36f + Vector3.down * 1.2f }, new[] { 0.03f, 0.03f }, 6, Rope);
            });
        }

        /// <summary>A glyph painted on a small board facing <paramref name="facing"/> (readable from that side only).</summary>
        static void GlyphSign(Transform p, Vector3 center, Vector3 facing, Glyph glyph, float size = 0.75f)
        {
            facing.y = 0f;
            facing.Normalize();
            var rot = Quaternion.LookRotation(Vector3.up, facing);   // local x right, local z up, local y out of the board
            Timber(p, glyph + " board", mb => mb.Box(center - facing * 0.03f, Quaternion.LookRotation(Vector3.up, facing), new Vector3(size * 1.25f, 0.05f, size * 1.25f), Aged));
            float s = size * 0.5f;
            switch (glyph)
            {
                case Glyph.Owl:
                    PaintedShape(p, "Owl glyph", center, rot, Poly2.Radial(Vector2.zero, a => s * (1f + 0.08f * Mathf.Cos(2f * a)), 28), NatureMaterialBuilder.PaintAmber);
                    foreach (float e in new[] { -0.36f, 0.36f })
                        PaintedShape(p, "Owl eye", center + facing * 0.02f, rot, Poly2.Radial(new Vector2(e * s, 0.18f * s), _ => 0.2f * s, 14), NatureMaterialBuilder.PaintBlue);
                    break;
                case Glyph.Leaf:
                    var leaf = new List<Vector2>();
                    for (int i = 0; i <= 12; i++) { float t = i / 12f; leaf.Add(new Vector2(Mathf.Sin(t * Mathf.PI) * 0.45f * s, Mathf.Lerp(-s, s, t))); }
                    for (int i = 11; i > 0; i--) { float t = i / 12f; leaf.Add(new Vector2(-Mathf.Sin(t * Mathf.PI) * 0.45f * s, Mathf.Lerp(-s, s, t))); }
                    PaintedShape(p, "Leaf glyph", center, rot, leaf, NatureMaterialBuilder.PaintGreen);
                    break;
                default:
                    var head = new List<Vector2> { new Vector2(-0.15f * s, 0.15f * s), new Vector2(0.55f * s, 0.7f * s), new Vector2(0.75f * s, 0.1f * s), new Vector2(0.55f * s, -0.5f * s), new Vector2(-0.15f * s, -0.05f * s) };
                    PaintedShape(p, "Axe glyph", center, rot, head, NatureMaterialBuilder.PaintBlue);
                    PaintedShape(p, "Axe handle", center, rot, new List<Vector2> { new Vector2(-0.12f * s, -s), new Vector2(0.02f * s, -s), new Vector2(0.02f * s, 0.95f * s), new Vector2(-0.12f * s, 0.95f * s) },
                                 NatureMaterialBuilder.PaintAmber);
                    break;
            }
        }

        /// <summary>A flat painted shape (polygon in the board's plane: x right, y up) a few millimetres proud of its board.</summary>
        static void PaintedShape(Transform p, string name, Vector3 center, Quaternion rot, IList<Vector2> shape, string material)
        {
            var mb = new MeshBuilder();
            mb.Prism(shape.Select(v => new Vector2(v.x, v.y)).ToList(), 0f, 0.025f, 0, 0);
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.AddComponent<MeshFilter>().sharedMesh = OrganicAssets.Save(mb.ToMesh(name, out _));
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = NatureMaterialBuilder.Load(material);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<CourseDecoration>();
        }

        /// <summary>The balcony's board: on two posts at <paramref name="at"/>, its face toward <paramref name="toward"/>, the glyph that leads on.</summary>
        static void ClueBoard(Transform p, Vector3 at, Vector3 toward, Glyph glyph)
        {
            var facing = toward - at;
            facing.y = 0f;
            facing.Normalize();
            var side = Vector3.Cross(Vector3.up, facing).normalized;
            Timber(p, "Clue board", mb =>
            {
                foreach (float s in new[] { -0.75f, 0.75f }) Post(mb, at + side * s, 2.4f, 0.14f);
                mb.Box(at + Vector3.up * 1.75f - facing * 0.05f, Quaternion.LookRotation(Vector3.up, facing), new Vector3(1.7f, 0.06f, 1.2f), Plank);
                Roof(mb, at + Vector3.up * 2.45f, 0.35f, 1f, 0.25f, Quaternion.LookRotation(side), Aged, 0.2f);
            });
            GlyphSign(p, at + Vector3.up * 1.75f + facing * 0.02f, facing, glyph, 0.95f);
        }

        // ------------------------------------------------------------------ the winch house (act E)

        static Vector3 HouseCenter, TerraceCenter;

        static void PlankWall(Transform p, string name, Vector2 a, Vector2 b, float y0, float y1, params (float at, float width, float height)[] doors) =>
            TimberBuildings.PlankWall(p, name, a, b, y0, y1, doors);

        /// <summary>A floor outline: the rectangle less a notch round the trunk from the west wall (or a slit to its hole) and an optional square cut at the north-east corner.</summary>
        static List<Vector2> FloorOutline(float x0, float x1, float z0, float z1, TimberKit.Trunk trunk, float cut)
        {
            var pts = new List<Vector2> { new Vector2(x0, z0), new Vector2(x1, z0) };
            if (cut > 0f)
            {
                pts.Add(new Vector2(x1, z1 - cut));
                pts.Add(new Vector2(x1 - cut, z1 - cut));
                pts.Add(new Vector2(x1 - cut, z1));
            }
            else pts.Add(new Vector2(x1, z1));
            pts.Add(new Vector2(x0, z1));
            float rN = trunk.radius + 0.12f;
            float tx = trunk.center.x, tz = trunk.center.y;
            float d = (x0 - tx) / rN;
            bool notch = d > -1f;
            float th0 = notch ? Mathf.Acos(Mathf.Clamp(d, -1f, 1f)) : Mathf.PI - 0.01f;
            if (!notch) pts.Add(new Vector2(x0, tz + 0.012f));
            for (int i = 0; i <= 28; i++)
            {
                float a = th0 - i / 28f * 2f * th0;
                pts.Add(new Vector2(tx + Mathf.Cos(a) * rN, tz + Mathf.Sin(a) * rN));
            }
            if (!notch) pts.Add(new Vector2(x0, tz - 0.012f));
            return pts;
        }

        /// <summary>
        /// The winch house (act E, PROJECT_SPEC §15f): ground floor on the porch's level round the leaning pine, upper floor 5.5 m above,
        /// a roof over both and the north terrace; the stairwell tower west of it (its door raised by the porch's hoop, sparks inside it);
        /// the cold larder and its three chutes, the cage and its winch; checkpoint 5 on the terrace.
        /// </summary>
        static void WinchHouse(Transform p, NatureTreeBuilder.HeroTree pine, Vector3 foot, Vector3 porch)
        {
            const float G0 = PorchY, U = 32.5f, Eave = 35.9f;
            var t0 = TrunkAt(pine, foot, G0);
            var t1 = TrunkAt(pine, foot, U);
            float x0 = t0.center.x - 1.2f, x1 = x0 + 9.8f, z0 = t0.center.y - 4.5f, z1 = t0.center.y + 4.5f;
            float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f;
            HouseCenter = new Vector3(cx, G0, cz);
            var house = new GameObject("Winch house").transform;
            house.SetParent(p, false);
            Anchors.Clear();
            Anchors.Add(Around(pine, foot, 44f, 0f, 0f));

            // floors (the upper floor cut for the cage at the north-east corner)
            Floor(house, "Ground floor", FloorOutline(x0, x1, z0, z1, t0, 0f), G0, 0f);
            Floor(house, "Upper floor", FloorOutline(x0, x1, z0, z1, t1, 2.75f), U, 90f);

            // walls: the ground floor's door to the porch; upstairs, the door to the terrace and the stairwell's arrival
            float doorX = porch.x + 0.2f;
            PlankWall(house, "South wall", new Vector2(x0, z0), new Vector2(x1, z0), G0, U, (doorX - x0, 1.8f, 2.5f));
            PlankWall(house, "East wall", new Vector2(x1, z0), new Vector2(x1, z1), G0, U);
            PlankWall(house, "North wall", new Vector2(x1, z1), new Vector2(x0, z1), G0, U);
            PlankWall(house, "West wall", new Vector2(x0, z1), new Vector2(x0, z0), G0, U);
            float terraceX0 = x0 + 1f, terraceX1 = x1 - 0.6f;
            TerraceCenter = new Vector3((terraceX0 + terraceX1) / 2f, U, z1 + 1.65f);
            PlankWall(house, "Upper south wall", new Vector2(x0, z0), new Vector2(x1, z0), U, Eave);
            PlankWall(house, "Upper east wall", new Vector2(x1, z0), new Vector2(x1, z1), U, Eave);
            PlankWall(house, "Upper north wall", new Vector2(x1, z1), new Vector2(x0, z1), U, Eave, (x1 - TerraceCenter.x, 2.2f, 2.5f));
            PlankWall(house, "Upper west wall", new Vector2(x0, z1), new Vector2(x0, z0), U, Eave, (0.9f, 1.4f, 2.4f));

            // the terrace under the eaves, closed at both ends
            Deck(house, "Terrace", new List<Vector2> { new Vector2(terraceX0, z1), new Vector2(terraceX1, z1), new Vector2(terraceX1, z1 + 3.3f), new Vector2(terraceX0, z1 + 3.3f) },
                 U, 0f, true, 14f, new TimberKit.Opening(new Vector2(TerraceCenter.x, z1), 2.6f), new TimberKit.Opening(new Vector2(TerraceCenter.x + 0.6f, z1 + 3.3f), 2f));
            PlankWall(house, "Terrace west end", new Vector2(terraceX0, z1 + 3.4f), new Vector2(terraceX0, z1), U, Eave);
            PlankWall(house, "Terrace east end", new Vector2(terraceX1, z1), new Vector2(terraceX1, z1 + 3.4f), U, Eave);

            // the roof over the house and the terrace: drawn pitched, colliding as a ceiling slab (nothing is lobbed in)
            float rz0 = z0 - 0.7f, rz1 = z1 + 3.9f;
            Timber(house, "Roof", mb =>
            {
                Roof(mb, new Vector3(cx, Eave + 0.1f, (rz0 + rz1) / 2f), (rz1 - rz0) / 2f, (x1 - x0) / 2f + 0.5f, 2.4f, Quaternion.Euler(0f, 90f, 0f), Aged, 0.2f);
                foreach (float x in new[] { x0, cx, x1 })
                    mb.Beam(new Vector3(x, Eave, rz0), new Vector3(x, Eave, rz1), 0.18f, 0.24f, Wood);
            });
            var ceiling = SolidTimberInvisibleGet(house, "Ceiling", new Vector3(cx, Eave + 0.2f, (rz0 + rz1) / 2f), Quaternion.identity, new Vector3(x1 - x0 + 1.2f, 0.4f, rz1 - rz0));
            ceiling.AddComponent<CourseCeiling>();

            // the stairwell tower west of the house: its door on the porch, sparks inside it, the stair up to the upper floor's west door
            float tx0 = x0 - 2.7f, tz0 = z0 - 1.6f, tz1 = z1 + 1.2f;
            float tdoor = (tx0 + x0) / 2f;
            PlankWall(house, "Tower south", new Vector2(tx0, tz0), new Vector2(x0, tz0), G0, Eave + 0.6f, (tdoor - tx0, 1.5f, 2.6f));
            PlankWall(house, "Tower west", new Vector2(tx0, tz1), new Vector2(tx0, tz0), G0, Eave + 0.6f);
            PlankWall(house, "Tower north", new Vector2(x0, tz1), new Vector2(tx0, tz1), G0, Eave + 0.6f);
            PlankWall(house, "Tower east", new Vector2(x0, tz0), new Vector2(x0, z0), G0, Eave + 0.6f);
            PlankWall(house, "Tower east upper", new Vector2(x0, z1), new Vector2(x0, tz1), G0, Eave + 0.6f);
            Timber(house, "Tower roof", mb => Roof(mb, new Vector3((tx0 + x0) / 2f, Eave + 0.7f, (tz0 + tz1) / 2f), (x0 - tx0) / 2f, (tz1 - tz0) / 2f, 1f, Quaternion.identity, Aged, 0.3f));
            SolidTimberInvisible(house, "Tower ceiling", new Vector3((tx0 + x0) / 2f, Eave + 0.8f, (tz0 + tz1) / 2f), Quaternion.identity, new Vector3(x0 - tx0 + 0.4f, 0.4f, tz1 - tz0 + 0.4f));
            Walk(house, "Stairwell stair", new[] { new Vector3(tdoor, G0, tz0 + 0.3f), new Vector3(tdoor, G0, tz0 + 1.1f), new Vector3(tdoor, U, z1 - 1.3f), new Vector3(tdoor, U, z1 - 0.7f) },
                 1.5f, false, false, false, 14f, true);
            Floor(house, "Stairwell landing", new List<Vector2> { new Vector2(tx0 + 0.15f, z1 - 0.8f), new Vector2(x0 + 0.5f, z1 - 0.8f), new Vector2(x0 + 0.5f, z1 + 1.05f), new Vector2(tx0 + 0.15f, z1 + 1.05f) },
                  U, 0f);
            var hoopPass = Pass(house, "Porch hoop", porch + new Vector3(6.4f, 1.5f, -1.4f), new Vector3(tdoor, G0 + 1.5f, tz0 - 1.6f), timed: true, opening: 2.4f);
            var hoop = HoopOnPass(house, "Porch hoop", hoopPass, 0.55f, 6f);
            var door = Actuator(house, Door, "Stairwell door", new Vector3(tdoor, G0 + 1.3f, tz0), new Vector3(1.6f, 2.6f, 0.35f), new Vector3(0f, 2.8f, 0f), hoop);
            var curtain = Place(house, LaserCurtain, "Sparks", new Vector3(tdoor, G0, tz0 + 0.45f), Quaternion.identity);
            ResizeCurtain(curtain, 1.4f, 2.6f);
            SolidTimber(house, "Sparks lintel", new Vector3(tdoor, G0 + 3.1f, tz0 + 0.45f), Quaternion.identity, new Vector3(2.4f, 1f, 0.8f));

            // the cold larder by the chutes, the chutes (one comes out upstairs), the cage and its winch
            Zone(house, ZoneCold, "Larder", new Vector3(x1 - 2f, G0, cz - 0.6f), new Vector3(3.4f, 2.6f, 7f));
            var tube = Place(house, Tube, "Log chutes", new Vector3(x1 - 0.8f, G0, cz), Quaternion.identity);
            float[] mz = { cz - 3.1f, cz - 0.6f, cz + 1.9f };
            ConfigureTube(tube, new[]
            {
                new TubeSlot(new Vector3(x1 - 0.55f, G0 + 1.35f, mz[0]), 90f, new Vector3(x1 - 1f, G0 + 2.6f, mz[0] + 0.4f), new Vector3(x1 - 3.8f, G0, mz[0] + 0.3f), 0.8f),
                new TubeSlot(new Vector3(x1 - 0.55f, G0 + 1.35f, mz[1]), 90f, new Vector3(x1 - 1f, G0 + 2.6f, mz[1] + 0.4f), new Vector3(x1 - 3.8f, G0, mz[1] + 0.4f), 0.8f),
                new TubeSlot(new Vector3(x1 - 0.55f, G0 + 1.35f, mz[2]), 90f, new Vector3(x1 - 1.2f, U + 0.9f, cz - 2.6f), new Vector3(x1 - 4f, U, cz - 2.4f), 0.55f),
            });
            TransitPasses(house, tube);
            var upperWinch = PlainPlate(house, "Upper winch", new Vector3(x1 - 4.1f, U, z1 - 1.4f));
            Cage(house, "House cage", new Vector3(x1 - 1.45f, G0, z1 - 1.45f), new Vector2(2.3f, 2.3f), 0f, U - G0, upperWinch, 2.6f);
            PulleyBeamHouse(house, new Vector3(x1 - 1.45f, Eave - 0.3f, z1 - 1.45f));

            CP(house, 5, TerraceCenter + new Vector3(-2.2f, 0f, 0.1f), new Vector3(3f, 0f, 0.4f));
            Contract(house, "A pass through the porch's hoop raises the stairwell door for six seconds; the runner climbs behind the sparks while " +
                            "the carrier waits in the cold larder; the runner sees which chute comes out upstairs and calls it; the upper winch " +
                            "lifts the carrier's cage. The house is roofed: nothing is lobbed in.",
                     LobLock("over the house's south wall", porch + new Vector3(4f, 0f, -2f), new Vector3(cx, Eave - 0.05f, z0)),
                     ClimbLock("the ground floor to the upper floor", new Vector3(x1 - 2.5f, G0, cz), new Vector3(x1 - 2.5f, U, cz)));
        }

        static GameObject SolidTimberInvisibleGet(Transform p, string name, Vector3 center, Quaternion rot, Vector3 size)
        {
            var go = new GameObject(name) { layer = Layer("Environment") };
            go.transform.SetParent(p, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>The pulley over the house's cage, hung from the roof's beams.</summary>
        static void PulleyBeamHouse(Transform p, Vector3 at)
        {
            Timber(p, "House pulley", mb =>
            {
                mb.Beam(at + Vector3.left * 1.6f, at + Vector3.right * 1.6f, 0.22f, 0.26f, Wood);
                var prof = new[] { new Vector2(0.05f, -0.07f), new Vector2(0.38f, -0.07f), new Vector2(0.32f, 0f), new Vector2(0.38f, 0.07f), new Vector2(0.05f, 0.07f) };
                mb.Lathe(at + Vector3.down * 0.5f, Quaternion.Euler(0f, 0f, 90f), prof, 14, Wood);
            });
        }

    }
}
