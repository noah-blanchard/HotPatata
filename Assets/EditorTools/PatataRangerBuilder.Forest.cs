using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HotPatata.Editor
{
    /// <summary>
    /// The land PatataRanger is built in (ARCHITECTURE §4 and §25.5), laid out before the route: a valley closed by rock walls, the
    /// camp's clearing at its foot, a ridge the yard gate cuts through, the ravine and its stream, slopes rising north to four
    /// giant trees the ranger post was built round (the old oak, the leaning pine, the twin fir and the lookout fir). The acts hang
    /// their timber on these trees and on this ground. The terrain is drawn in chunks and walked on through a smoothed proxy.
    /// </summary>
    public static partial class PatataRangerBuilder
    {
        // the valley (world metres): x east, z north, the camp at the origin
        const float ValleyMinX = -150f, ValleyMaxX = 150f, ValleyMinZ = -90f, ValleyMaxZ = 330f;
        const float Cell = 1.5f;              // the walkable terrain's grid
        const float RidgeZ = 40f;             // the ridge the yard gate cuts
        const float RidgeHeight = 7.5f;
        const float GateX = 8f, GateHalf = 3.2f;
        const float RimY = 2f;                // the ravine's rims
        const float StreamY = -7.5f;          // its water (the floor is under it)
        const float RavineFloor = -9f;

        /// <summary>The ravine's centre line (z at x).</summary>
        static float RavineZ(float x) => 77f + 4.5f * Mathf.Sin(x * 0.045f + 0.4f);

        /// <summary>The ravine's half width at its rims: narrowest (16.4 m) where the drawbridge spans it.</summary>
        static float RavineHalf(float x) => 8.2f + 2.4f * (1f - Mathf.Exp(-Sq((x - BridgeX) / 15f)));

        const float BridgeX = -6f;            // the drawbridge's line across the ravine
        const float FootbridgeX = 20f;        // the old footbridge, low over the stream

        static float Sq(float v) => v * v;

        static float Smooth(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float Noise(float x, float z, float scale, int seed) => NatureShapes.Noise(new Vector3(x * scale, 0.37f, z * scale), seed);

        /// <summary>The valley floor's rise north of the ravine (before the walls and features).</summary>
        static float Slope(float z) =>
            z < 90f ? RimY : RimY + 4f * Smooth(90f, 112f, z) + 3.5f * Smooth(125f, 165f, z) + 3f * Smooth(170f, 205f, z) + 2.5f * Smooth(215f, 250f, z);

        /// <summary>
        /// The ground's height at (x, z): the camp's flat clearing, the ridge and its cut, the plateau to the ravine, the ravine (walls
        /// over 80°), the slopes north, the crag, the valley's rock walls; low noise where nothing is levelled.
        /// </summary>
        public static float GroundAt(float x, float z)
        {
            // the floor south of the ravine: the camp (0), the ridge, the plateau rising to the rim
            float h;
            if (z < RidgeZ + 6f) h = 0f;
            else h = Mathf.Lerp(0f, RimY, Smooth(RidgeZ + 6f, RidgeZ + 22f, z));
            // north of the ravine
            float rz = RavineZ(x), half = RavineHalf(x);
            if (z > rz) h = Mathf.Max(h, Slope(z));
            // gentle relief, kept off the levelled places
            float flat = Flatness(x, z);
            h += (Noise(x, z, 0.035f, 11) - 0.5f) * 1.6f * (1f - flat) + (Noise(x, z, 0.12f, 12) - 0.5f) * 0.35f * (1f - flat);

            // the ridge: a band of rock 7.5 m high, cut by the yard gate's notch (its floor ramps from the camp up to the plateau)
            float ridge = Smooth(RidgeZ - 3.5f, RidgeZ - 1.6f, z) * (1f - Smooth(RidgeZ + 3.2f, RidgeZ + 5f, z));
            float wiggle = 1.2f * Mathf.Sin(x * 0.08f) + 0.8f * Mathf.Sin(x * 0.21f + 1f);
            ridge *= RidgeHeight + wiggle + (Noise(x, z, 0.2f, 13) - 0.5f) * 1.4f;
            float cut = 1f - Smooth(GateHalf, GateHalf + 1.1f, Mathf.Abs(x - GateX));
            h = Mathf.Max(h, ridge * (1f - cut) + Mathf.Lerp(0f, 0.6f, Smooth(RidgeZ - 4f, RidgeZ + 5f, z)) * cut);

            // the ravine: steep walls down to a stony floor under the stream
            float d = Mathf.Abs(z - rz);
            float wall = 1f - Smooth(half - 1.3f, half + 0.2f, d);
            float floor = RavineFloor + (Noise(x, z, 0.3f, 14) - 0.5f) * 0.6f;
            h = Mathf.Lerp(h, floor, wall);

            // the crag (Pierre-Haute) in the woods between the oak and the pine, its top a broken shelf
            float crag = Mathf.Max(0f, 1f - Sq(new Vector2(x - CragAt.x, z - CragAt.y).magnitude / 8.5f));
            if (crag > 0f)
                h = Mathf.Max(h, Slope(z) + Mathf.Pow(crag, 0.35f) * (CragTop - Slope(z)) * Smooth(0f, 0.08f, crag) + (Noise(x, z, 0.4f, 15) - 0.5f) * 1.5f * crag);

            // the valley's walls (east, west, behind the camp, past the lookout): steep rock
            float wallHalf = 62f + 9f * Mathf.Sin(z * 0.03f) + 5f * Mathf.Sin(z * 0.071f + 2f);
            float side = Smooth(wallHalf, wallHalf + 13f, Mathf.Abs(x));
            float back = Smooth(-38f, -52f, z) + Smooth(285f, 300f, z);
            float rise = Mathf.Max(side, back);
            h += rise * (34f + (Noise(x, z, 0.05f, 16) - 0.5f) * 12f);
            return h;
        }

        static readonly Vector2 CragAt = new Vector2(-14f, 151f);
        const float CragTop = 18f;

        /// <summary>How levelled a place is (1 = flat): the camp, the checkpoints' clearings, the gate's notch, the rims at the bridge.</summary>
        static float Flatness(float x, float z)
        {
            float f = 0f;
            foreach (var (c, r) in Levelled)
                f = Mathf.Max(f, 1f - Smooth(r * 0.6f, r, new Vector2(x - c.x, z - c.y).magnitude));
            return f;
        }

        static readonly (Vector2 at, float radius)[] Levelled =
        {
            (new Vector2(0f, 6f), 16f),          // the camp
            (new Vector2(GateX, RidgeZ), 9f),    // the gate's notch
            (new Vector2(2f, 58f), 12f),         // the south rim at the bridge (checkpoint 1)
            (new Vector2(BridgeX, 92f), 9f),     // the north rim at the bridge
            (new Vector2(BridgeX, 65f), 7f),     // the south bridgehead
            (new Vector2(13f, 66f), 7f),         // the south winch, across from the cage
            (new Vector2(13f, 94f), 7f),         // the cage's landing on the north rim
            (new Vector2(29f, 66f), 6f),         // the head of the south stair
            (new Vector2(2f, 110f), 15f),        // the old oak's clearing (checkpoint 2)
        };

        // ------------------------------------------------------------------ the four giants the post is built round

        public static NatureTreeBuilder.HeroTree OldOak, LeaningPine, TwinFir, LookoutFir, Snag;
        static readonly Vector3[] HeroFeet = new Vector3[5];

        static NatureTreeBuilder.HeroSpec OakSpec() => new NatureTreeBuilder.HeroSpec
        {
            name = "OldOak", seed = 11, conifer = false, height = 58f, baseRadius = 2.8f, topRadius = 1.35f, lean = new Vector2(2.5f, 1.5f),
            crownStart = 27f, crownLimbs = 15,
            limbs = new[]
            {
                new NatureTreeBuilder.HeroLimb(16.5f, -105f, 15f, 6f, 0.95f, false),   // 0: the west limb the upper catwalk runs along (act D)
                new NatureTreeBuilder.HeroLimb(21f, -35f, 13f, 14f, 0.8f, true),      // 1: the north-west limb the signal hoops hang from (act C)
                new NatureTreeBuilder.HeroLimb(17f, 75f, 10f, 10f, 0.7f, true),       // 2: the east limb the pulleys hang from (act C)
                new NatureTreeBuilder.HeroLimb(26f, 165f, 13f, 22f, 0.75f, true),     // 3: south
                new NatureTreeBuilder.HeroLimb(11f, -160f, 7f, 18f, 0.5f, false),     // 4: a low broken limb
            }
        };

        static NatureTreeBuilder.HeroSpec PineSpec() => new NatureTreeBuilder.HeroSpec
        {
            name = "LeaningPine", seed = 23, conifer = true, height = 58f, baseRadius = 2.1f, topRadius = 0.5f, lean = new Vector2(13f, -3f),
            crownStart = 30f, crownLimbs = 22, clearings = new[] { new Vector2(15f, 31f) },
            limbs = new[]
            {
                new NatureTreeBuilder.HeroLimb(14f, 200f, 5f, -4f, 0.4f, false),      // dead stubs low on the trunk
                new NatureTreeBuilder.HeroLimb(17f, 40f, 4f, -6f, 0.35f, false),
                new NatureTreeBuilder.HeroLimb(31f, 110f, 9f, -2f, 0.55f, true),      // over the winch house's roof
            }
        };

        static NatureTreeBuilder.HeroSpec TwinSpec() => new NatureTreeBuilder.HeroSpec
        {
            name = "TwinFir", seed = 37, conifer = true, height = 66f, baseRadius = 2.3f, topRadius = 0.45f, lean = new Vector2(-2f, 3f),
            crownStart = 21f, crownLimbs = 26, clearings = new[] { new Vector2(22.5f, 29.5f) },
            limbs = new[] { new NatureTreeBuilder.HeroLimb(18f, 250f, 4f, -8f, 0.35f, false) }
        };

        static NatureTreeBuilder.HeroSpec LookoutSpec() => new NatureTreeBuilder.HeroSpec
        {
            name = "LookoutFir", seed = 41, conifer = true, height = 72f, baseRadius = 2.5f, topRadius = 0.5f, lean = new Vector2(1.5f, -2.5f),
            crownStart = 24f, crownLimbs = 28, clearings = new[] { new Vector2(26.5f, 34f) },
            limbs = new[] { new NatureTreeBuilder.HeroLimb(22f, 120f, 5f, -6f, 0.4f, false), new NatureTreeBuilder.HeroLimb(25f, 300f, 4f, -6f, 0.35f, false) }
        };

        /// <summary>A dead fir between the pine and the twin fir: the old signal platform's post in the crowns (act F).</summary>
        static NatureTreeBuilder.HeroSpec SnagSpec() => new NatureTreeBuilder.HeroSpec
        {
            name = "Snag", seed = 53, conifer = true, dead = true, height = 44f, baseRadius = 1.5f, topRadius = 0.35f, lean = new Vector2(-1.5f, 1f),
            crownStart = 28f, crownLimbs = 9, clearings = new[] { new Vector2(19f, 27f) },
            limbs = new[] { new NatureTreeBuilder.HeroLimb(20f, 40f, 5f, -10f, 0.3f, false), new NatureTreeBuilder.HeroLimb(16f, 200f, 4f, -14f, 0.28f, false) }
        };

        /// <summary>Where each giant stands (its foot on the ground).</summary>
        static Vector3 Foot(Vector2 at) => new Vector3(at.x, GroundAt(at.x, at.y) - 0.4f, at.y);

        /// <summary>A giant's trunk at an absolute height: its centre (x, z) and radius there, for the timber that hugs it.</summary>
        static TimberKit.Trunk TrunkAt(NatureTreeBuilder.HeroTree tree, Vector3 foot, float y)
        {
            var c = foot + tree.AxisAt(y - foot.y);
            return new TimberKit.Trunk(new Vector2(c.x, c.z), tree.RadiusAt(y - foot.y));
        }

        static void GrowGiants(Transform forest)
        {
            HeroFeet[0] = Foot(new Vector2(9f, 123f));
            HeroFeet[1] = Foot(new Vector2(-40f, 172f));
            HeroFeet[2] = Foot(new Vector2(14f, 203f));
            HeroFeet[3] = Foot(new Vector2(-12f, 242f));
            HeroFeet[4] = Foot(new Vector2(-6f, 189f));
            OldOak = NatureTreeBuilder.BuildHero(OakSpec());
            Snag = NatureTreeBuilder.BuildHero(SnagSpec());
            LeaningPine = NatureTreeBuilder.BuildHero(PineSpec());
            TwinFir = NatureTreeBuilder.BuildHero(TwinSpec());
            LookoutFir = NatureTreeBuilder.BuildHero(LookoutSpec());
            var trees = new GameObject("Giants").transform;
            trees.SetParent(forest, false);
            NatureTreeBuilder.PlaceHero(trees, OldOak, HeroFeet[0], 0f);
            NatureTreeBuilder.PlaceHero(trees, LeaningPine, HeroFeet[1], 0f);
            NatureTreeBuilder.PlaceHero(trees, TwinFir, HeroFeet[2], 0f);
            NatureTreeBuilder.PlaceHero(trees, LookoutFir, HeroFeet[3], 0f);
            NatureTreeBuilder.PlaceHero(trees, Snag, HeroFeet[4], 0f);
        }

        // ------------------------------------------------------------------ terrain

        static Vector3[,] terrainPoints;

        /// <summary>
        /// The valley's ground: drawn in chunks (rock where it is steep, a trail where the route walks, leaf litter elsewhere) and
        /// walked on through a proxy of the same grid (a closed slab); the far walls are drawn coarser and never walked.
        /// </summary>
        static string Terrain(Transform forest)
        {
            int nx = Mathf.CeilToInt((ValleyMaxX - ValleyMinX) / Cell) + 1, nz = Mathf.CeilToInt((ValleyMaxZ - ValleyMinZ) / Cell) + 1;
            var points = terrainPoints = new Vector3[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x = ValleyMinX + i * Cell, z = ValleyMinZ + j * Cell;
                    points[i, j] = new Vector3(x, GroundAt(x, z), z);
                }
            var drawn = new GameObject("Ground").transform;
            drawn.SetParent(forest, false);
            // one material for the whole valley: leaf litter wherever the ground can be walked, rock on the faces steeper than that
            var ground = NatureMaterialBuilder.Load("Nature_ValleyGround");
            int Tag(int i, int j) => 0;
            // the drawn chunks have their own folder (the terrain builder replaces its own files; OrganicAssets cleans the rest)
            string chunkDir = GeneratedDir + "Terrain/";
            System.IO.Directory.CreateDirectory(chunkDir.TrimEnd('/'));
            int chunks = NatureTerrainMesh.Build(drawn, points, (a, b, c) => true, Tag, _ => ground, chunkDir, "Terrain");
            foreach (Transform chunk in drawn) GameObjectUtility.SetStaticEditorFlags(chunk.gameObject, StaticEditorFlags.BatchingStatic);

            // the walkable proxy: the floor of the valley inside its walls, one closed slab per block of cells
            var proxyRoot = new GameObject("Ground proxy");
            proxyRoot.transform.SetParent(forest, false);
            const int Block = 48;
            int blocks = 0;
            for (int bi = 0; bi < nx - 1; bi += Block)
                for (int bj = 0; bj < nz - 1; bj += Block)
                {
                    int i1 = Mathf.Min(bi + Block, nx - 1), j1 = Mathf.Min(bj + Block, nz - 1);
                    var mb = new MeshBuilder();
                    HeightSlab(mb, points, bi, i1, bj, j1, -60f);
                    var go = new GameObject($"Ground {bi}_{bj}");
                    go.transform.SetParent(proxyRoot.transform, false);
                    TimberKit.AddProxy(go, $"Terrain proxy {bi}_{bj}", mb, WalkableProxy.Kind.Terrain, 0.02f);
                    blocks++;
                }
            // the mist lies on the valley floor (thick in the ravine, thinning as the post climbs out of it)
            var area = Rect.MinMaxRect(ValleyMinX, ValleyMinZ, ValleyMaxX, ValleyMaxZ);
            var (mistFloor, mistRect) = NatureTerrainMesh.BakeMistFloor(MistFloor, area, 256, 4, GeneratedDir + "PatataRanger_MistFloor.asset");
            var mist = new GameObject("Mist");
            mist.transform.SetParent(forest, false);
            mist.AddComponent<MistField>().Configure(mistFloor, mistRect, 0.93f);
            return $"{chunks} terrain chunks, {blocks} ground proxies";
        }

        /// <summary>A closed slab of the grid cells i0..i1, j0..j1: their surface on top, skirts down to <paramref name="bottom"/>, a floor.</summary>
        static void HeightSlab(MeshBuilder mb, Vector3[,] p, int i0, int i1, int j0, int j1, float bottom)
        {
            // the top as quads (two triangles each, the same diagonal as the drawn chunks)
            for (int i = i0; i < i1; i++)
                for (int j = j0; j < j1; j++)
                    mb.Quad(p[i, j], p[i, j + 1], p[i + 1, j + 1], p[i + 1, j], Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
            Vector3 B(Vector3 v) => new Vector3(v.x, bottom, v.z);
            // skirts round the block, facing out, and the floor
            for (int i = i0; i < i1; i++)
            {
                mb.Quad(B(p[i, j0]), B(p[i + 1, j0]), p[i + 1, j0], p[i, j0], Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
                mb.Quad(B(p[i + 1, j1]), B(p[i, j1]), p[i, j1], p[i + 1, j1], Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
            }
            for (int j = j0; j < j1; j++)
            {
                mb.Quad(B(p[i0, j + 1]), B(p[i0, j]), p[i0, j], p[i0, j + 1], Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
                mb.Quad(B(p[i1, j]), B(p[i1, j + 1]), p[i1, j + 1], p[i1, j], Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
            }
            mb.Quad(B(p[i0, j0]), B(p[i1, j0]), B(p[i1, j1]), B(p[i0, j1]), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
        }

        /// <summary>The ground's rise over run at a grid point (largest of its neighbours).</summary>
        static float Steepness(Vector3[,] p, int i, int j)
        {
            float s = 0f;
            int nx = p.GetLength(0), nz = p.GetLength(1);
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int a = Mathf.Clamp(i + di, 0, nx - 1), b = Mathf.Clamp(j + dj, 0, nz - 1);
                s = Mathf.Max(s, Mathf.Abs(p[a, b].y - p[i, j].y) / Cell);
            }
            return s;
        }

        /// <summary>The trails on the ground (camp to the oak), each a polyline: the terrain draws them, plants keep off them.</summary>
        static readonly List<List<Vector3>> Trail = new List<List<Vector3>>();

        static float TrailDistance(float x, float z)
        {
            float best = float.MaxValue;
            var q = new Vector2(x, z);
            foreach (var line in Trail)
                for (int i = 0; i + 1 < line.Count; i++)
                {
                    var a = new Vector2(line[i].x, line[i].z);
                    var b = new Vector2(line[i + 1].x, line[i + 1].z);
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                    best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
                }
            return best;
        }

        /// <summary>
        /// Where the mist lies: two and a half metres under the valley floor (a veil the team walks in), on the ravine's floor itself
        /// (the ravine fills with it).
        /// </summary>
        static float MistFloor(float x, float z)
        {
            float d = Mathf.Abs(z - RavineZ(x));
            float inRavine = 1f - Smooth(RavineHalf(x) - 1f, RavineHalf(x) + 4f, d);
            return GroundAt(x, z) - 2.5f * (1f - inRavine);
        }

        /// <summary>The stream in the ravine: water over the floor and the kill zone under its surface (a fall into it fails the section).</summary>
        static void Stream(Transform forest)
        {
            var root = new GameObject("Stream").transform;
            root.SetParent(forest, false);
            // strips 8 m long along the ravine, side by side (never overlapping), each as wide as the ravine over its length
            for (float x = ValleyMinX + 30f; x < ValleyMaxX - 30f; x += 8f)
            {
                float zMin = float.MaxValue, zMax = float.MinValue;
                for (float s = x; s <= x + 8f; s += 1f)
                {
                    zMin = Mathf.Min(zMin, RavineZ(s) - RavineHalf(s) - 0.5f);
                    zMax = Mathf.Max(zMax, RavineZ(s) + RavineHalf(s) + 0.5f);
                }
                NatureKit.Water(root, $"Stream {x:F0}", x, x + 8f, zMin, zMax, StreamY, RavineFloor - 0.5f);
            }
        }
    }
}
