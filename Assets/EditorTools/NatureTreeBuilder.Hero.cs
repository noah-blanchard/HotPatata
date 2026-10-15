using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HotPatata.Editor
{
    public static partial class NatureTreeBuilder
    {
        // ------------------------------------------------------------------ hero trees (PatataRanger, ARCHITECTURE §25.5)

        /// <summary>One limb of a hero tree, placed by hand: it leaves the trunk at <see cref="height"/> toward <see cref="yaw"/>.</summary>
        public struct HeroLimb
        {
            public float height;     // where it leaves the trunk (m above the foot)
            public float yaw;        // its heading, degrees from +Z toward +X
            public float length;
            public float rise;       // degrees above level at the trunk (it bends up toward its tip)
            public float radius;     // at the trunk
            public bool leaves;      // clusters of leaves along it and at its tip
            public bool bare => !leaves;

            public HeroLimb(float height, float yaw, float length, float rise, float radius, bool leaves = true)
            {
                this.height = height;
                this.yaw = yaw;
                this.length = length;
                this.rise = rise;
                this.radius = radius;
                this.leaves = leaves;
            }
        }

        /// <summary>
        /// A hero tree: a giant authored for its place in a course (the trunks a ranger post is built round). Its trunk leans and
        /// curves back up, flares into buttress roots, and its limbs are where the course needs them; the crown is clusters of leaves
        /// on limbs with sky between them, never a cap.
        /// </summary>
        public struct HeroSpec
        {
            public string name;
            public int seed;
            public bool conifer;          // a redwood-like fir (whorls of needle sprays) or a broadleaf (limbs ending in leaf clusters)
            public bool dead;             // a snag: bare limbs, no leaves
            public float height;
            public float baseRadius;      // at the foot above the flare
            public float topRadius;       // where the trunk ends in the crown
            public Vector2 lean;          // the top's offset from the foot (x, z), metres; the trunk curves into it
            public float crownStart;      // the crown's random limbs start here
            public int crownLimbs;
            public HeroLimb[] limbs;
            /// <summary>Height bands (above the foot) where the rangers sawed the crown's limbs off round their platforms: stubs only.</summary>
            public Vector2[] clearings;

            public bool Cleared(float h) => clearings != null && clearings.Any(c => h > c.x && h < c.y);
        }

        /// <summary>A built hero tree: its meshes per level of detail and where its trunk and limbs are (for decks and colliders).</summary>
        public sealed class HeroTree
        {
            public HeroSpec spec;
            public Mesh[] bole = new Mesh[3];    // bark: trunk, roots, limbs
            public Mesh[] crown = new Mesh[3];   // leaves
            public List<Vector3> axis = new List<Vector3>();
            public List<float> radii = new List<float>();
            /// <summary>Each limb's polyline and radius at its base (local to the foot).</summary>
            public List<(Vector3[] points, float radius)> limbs = new List<(Vector3[], float)>();

            /// <summary>The trunk's axis at height <paramref name="y"/> above the foot (local).</summary>
            public Vector3 AxisAt(float y) => TrunkPoint(axis, y / spec.height);

            public float RadiusAt(float y)
            {
                float f = Mathf.Clamp01(y / spec.height) * (radii.Count - 1);
                int i = Mathf.Min(Mathf.FloorToInt(f), radii.Count - 2);
                return Mathf.Lerp(radii[i], radii[i + 1], f - i);
            }

            /// <summary>The tip of the limb <paramref name="index"/> (local), for hangers and anchors.</summary>
            public Vector3 LimbPoint(int index, float t)
            {
                var p = limbs[index].points;
                float f = Mathf.Clamp01(t) * (p.Length - 1);
                int i = Mathf.Min(Mathf.FloorToInt(f), p.Length - 2);
                return Vector3.Lerp(p[i], p[i + 1], f - i);
            }
        }

        static readonly int[] HeroSides = { 22, 12, 6 };

        /// <summary>Grows a hero tree (its three levels) and saves its meshes with <see cref="OrganicAssets"/>.</summary>
        public static HeroTree BuildHero(HeroSpec spec)
        {
            var tree = new HeroTree { spec = spec };
            for (int lod = 0; lod < 3; lod++)
            {
                var (bark, leaves) = GrowHero(spec, lod, lod == 0 ? tree : null);
                tree.bole[lod] = OrganicAssets.Save(bark.ToMesh($"{spec.name}_Bark_LOD{lod}", true));
                // leaves only: one submesh, the foliage material
                tree.crown[lod] = leaves.V.Count > 0 ? OrganicAssets.Save(leaves.ToMesh($"{spec.name}_Leaves_LOD{lod}")) : null;
            }
            return tree;
        }

        static (Builder bark, Builder leaves) GrowHero(HeroSpec spec, int lod, HeroTree record)
        {
            var rng = new Random(spec.seed * 31 + 7);
            var bark = new Builder { Height = spec.height };
            var leaves = new Builder { Height = spec.height };
            int sides = HeroSides[lod];
            int segments = lod == 0 ? 28 : lod == 1 ? 14 : 7;

            // the trunk: leans out low and curves back toward the vertical (a tree reaching for light), burls and a flared foot
            var trunk = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments, y = t * spec.height;
                float bend = Mathf.Sin(t * Mathf.PI * 0.5f);   // most of the lean happens low
                float wx = (Mathf.Sin(t * 7.3f + spec.seed) * 0.5f + Mathf.Sin(t * 17.1f + spec.seed * 2f) * 0.18f) * (spec.conifer ? 0.25f : 1f);
                float wz = (Mathf.Cos(t * 6.1f + spec.seed * 3f) * 0.5f + Mathf.Sin(t * 13.7f + spec.seed) * 0.18f) * (spec.conifer ? 0.25f : 1f);
                trunk.Add(new Vector3(spec.lean.x * bend + wx * Mathf.Sin(t * Mathf.PI), y, spec.lean.y * bend + wz * Mathf.Sin(t * Mathf.PI)));
                float r = Mathf.Lerp(spec.baseRadius, spec.topRadius, Mathf.Pow(t, 0.85f));
                r *= 1f + (spec.conifer ? 0.06f : 0.11f) * Mathf.Sin(t * 27f + spec.seed) * (1f - t) + (spec.conifer ? 0f : 0.07f * Mathf.Sin(t * 61f + spec.seed));
                r *= 1f + 0.7f * Mathf.Max(0f, 0.05f - t) / 0.05f;   // the flare
                radii.Add(r);
            }
            bark.Tube(trunk, radii, sides, false);
            if (record != null) { record.axis.AddRange(trunk); record.radii.AddRange(radii); }

            // buttress roots: thick, plunging into the ground, sunk so no foot floats
            int roots = lod == 2 ? 0 : 8;
            for (int k = 0; k < roots; k++)
            {
                float yaw = k * 360f / roots + R(rng, -16f, 16f);
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float reach = spec.baseRadius + R(rng, 2.5f, 5.5f);
                var a = dir * spec.baseRadius * 0.5f + Vector3.up * R(rng, 4f, 7f);
                var b = dir * (spec.baseRadius + 0.9f) + Vector3.up * R(rng, 1.4f, 2.4f);
                var c = dir * (reach * 0.75f) + Vector3.up * 0.2f;
                var d = dir * reach + Vector3.down * 0.8f + Quaternion.Euler(0f, 90f, 0f) * dir * R(rng, -1f, 1f);
                bark.Tube(new[] { a, b, c, d }, new[] { spec.baseRadius * 0.45f, spec.baseRadius * 0.34f, 0.32f, 0.12f }, lod == 0 ? 9 : 5, false);
            }

            var crownCenter = new Vector3(spec.lean.x, (spec.crownStart + spec.height) * 0.5f, spec.lean.y);
            var all = new List<HeroLimb>(spec.limbs ?? new HeroLimb[0]);
            if (spec.conifer)
            {
                // whorls of drooping limbs up the crown, long at its foot and short at its top (a spire), five to a whorl
                float step = lod == 0 ? 1.3f : lod == 1 ? 2.4f : 4.5f;
                int whorl = 0;
                for (float h = spec.crownStart; h < spec.height - 2.5f; h += step * R(rng, 0.85f, 1.15f), whorl++)
                {
                    float u = (h - spec.crownStart) / (spec.height - spec.crownStart);
                    float len = Mathf.Lerp(8.5f, 1.6f, Mathf.Pow(u, 0.8f)) * R(rng, 0.85f, 1.15f) * (spec.dead ? 0.55f : 1f);
                    int count = spec.dead ? 2 : lod == 2 ? 3 : 5;
                    float start = R(rng, 0f, 360f);
                    bool sawn = spec.Cleared(h);
                    for (int k = 0; k < count; k++)
                        all.Add(new HeroLimb(h, start + k * 360f / count + R(rng, -15f, 15f), sawn ? R(rng, 0.5f, 0.9f) : len, R(rng, -18f, -4f),
                                             Mathf.Lerp(0.26f, 0.07f, u), !spec.dead && !sawn));
                }
            }
            else
            {
                // two leaders carry the trunk on up into the crown, then the crown's own limbs spread all round, wide and low
                for (int k = 0; k < 2; k++)
                    all.Add(new HeroLimb(spec.crownStart + 2f + k * 4f, k * 160f + R(rng, -20f, 20f), spec.height - spec.crownStart - 6f, R(rng, 62f, 72f),
                                         spec.topRadius * 0.85f, !spec.dead));
                for (int k = 0; k < spec.crownLimbs; k++)
                {
                    float u = (k + 0.5f) / spec.crownLimbs;
                    float h = Mathf.Lerp(spec.crownStart, spec.height - 4f, u * 0.85f) + R(rng, -1.5f, 1.5f);
                    float len = Mathf.Lerp(20f, 9f, u) * R(rng, 0.8f, 1.2f);
                    bool sawn = spec.Cleared(h);
                    all.Add(new HeroLimb(h, k * 137.5f + R(rng, -20f, 20f), sawn ? 1f : len, R(rng, 8f, 34f), Mathf.Lerp(spec.topRadius * 0.75f, 0.22f, u),
                                         !spec.dead && !sawn));
                }
            }
            int index = 0;
            foreach (var limb in all)
            {
                var start = TrunkPoint(trunk, limb.height / spec.height);
                var dir = Quaternion.Euler(-limb.rise, limb.yaw, 0f) * Vector3.forward;
                float rTrunk = Mathf.Lerp(radii[0], radii[radii.Count - 1], limb.height / spec.height);
                start += new Vector3(dir.x, 0f, dir.z).normalized * rTrunk * 0.4f;
                // a limb bends up toward its tip (broadleaf) or droops (conifer)
                int n = lod == 0 ? 6 : lod == 1 ? 4 : 3;
                var pts = new Vector3[n];
                var rs = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)(n - 1);
                    var p = start + dir * limb.length * t;
                    p += Vector3.up * (spec.conifer ? -limb.length * 0.12f * t * t : limb.length * (0.22f * t - 0.12f * t * t));
                    p += new Vector3(Mathf.Sin(t * 5f + index), 0f, Mathf.Cos(t * 4f + index)) * 0.25f * t;
                    pts[i] = p;
                    rs[i] = Mathf.Lerp(limb.radius, limb.radius * 0.25f, t);
                }
                if (record != null && index < (spec.limbs?.Length ?? 0)) record.limbs.Add((pts, limb.radius));
                if (lod < 2 || limb.radius > 0.3f) bark.Tube(pts, rs, lod == 0 ? 9 : 5, true);
                if (limb.leaves)
                {
                    if (spec.conifer) Sprays(leaves, rng, pts, limb.length, crownCenter, lod);
                    else
                    {
                        var tip = pts[n - 1];
                        // a secondary fork or two, each ending in a cluster: the tip of a limb is never one ball
                        int forks = lod == 2 ? 1 : 3;
                        Clusters(leaves, rng, tip, R(rng, 3.6f, 4.8f), crownCenter, lod);
                        Clusters(leaves, rng, Vector3.Lerp(pts[n / 2], tip, 0.55f) + Vector3.up * 1.2f, R(rng, 2.8f, 3.6f), crownCenter, lod);
                        for (int f = 0; f < forks; f++)
                        {
                            var from = Vector3.Lerp(pts[n / 2], tip, R(rng, 0.2f, 0.8f));
                            var sdir = Quaternion.Euler(-R(rng, 15f, 45f), limb.yaw + R(rng, -70f, 70f), 0f) * Vector3.forward;
                            var to = from + sdir * limb.length * R(rng, 0.25f, 0.4f);
                            if (lod == 0) bark.Tube(new[] { from, Vector3.Lerp(from, to, 0.5f) + Vector3.up * 0.3f, to }, new[] { limb.radius * 0.3f, limb.radius * 0.2f, 0.05f }, 5, true);
                            Clusters(leaves, rng, to, R(rng, 2.8f, 3.8f), crownCenter, lod);
                        }
                    }
                }
                index++;
            }
            if (spec.conifer && !spec.dead)
            {
                // the leader: the trunk's tip in a spire of sprays
                var top = trunk[trunk.Count - 1];
                for (int k = 0; k < (lod == 2 ? 2 : 5); k++)
                    leaves.CrossCard(top + Vector3.down * (k * 1.6f + 1f), Vector3.up, 3f, 2.4f - k * 0.2f, crownCenter, k * 37f, 0.4f);
            }
            return (bark, leaves);
        }

        /// <summary>A loose cluster of leaf cards round <paramref name="c"/>: cards face out, some sky shows through.</summary>
        static void Clusters(Builder b, Random rng, Vector3 c, float radius, Vector3 crown, int lod)
        {
            int cards = lod == 0 ? 34 : lod == 1 ? 13 : 5;
            float size = lod == 2 ? radius * 1.5f : lod == 1 ? 2.8f : 2.1f;
            LeafCluster(b, rng, c, radius, crown, lod, cards, size);
        }

        /// <summary>Needle sprays along a conifer limb (crossed cards along it, shorter toward the tip).</summary>
        static void Sprays(Builder b, Random rng, Vector3[] limb, float length, Vector3 crown, int lod)
        {
            int cards = lod == 2 ? 1 : Mathf.Max(2, Mathf.CeilToInt(length / (lod == 0 ? 0.75f : 1.6f)));
            var start = limb[0];
            var tip = limb[limb.Length - 1];
            for (int c = 0; c < cards; c++)
            {
                float t = (c + 0.5f) / cards;
                var p = Vector3.Lerp(start, tip, 0.15f + t * 0.85f);
                float w = (lod == 2 ? length * 1.3f : lod == 1 ? 4f : 3.3f) * (1f - t * 0.4f);
                b.CrossCard(p - (tip - start).normalized * 0.4f, (tip - start).normalized, lod == 2 ? length : 3.2f, w, crown, R(rng, -30f, 30f), t);
            }
        }

        /// <summary>
        /// Places a built hero tree at <paramref name="foot"/>: a renderer per part with an LOD group, capsule colliders along its trunk
        /// and its thick limbs (on Environment: a body stops at it, a bomb that hits it explodes).
        /// </summary>
        public static GameObject PlaceHero(Transform parent, HeroTree tree, Vector3 foot, float yaw)
        {
            var root = new GameObject(tree.spec.name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(foot, Quaternion.Euler(0f, yaw, 0f));
            var (barkMat, leafMat) = tree.spec.dead ? ("Nature_RoughWood", "Nature_Leaves_Fir")
                : tree.spec.conifer ? ("Nature_PineBark", "Nature_Leaves_Fir") : ("Nature_BarkBrown", "Nature_Leaves_Broad");
            var group = root.AddComponent<LODGroup>();
            var levels = new LOD[3];
            float[] heights = { 0.25f, 0.07f, 0.008f };
            for (int lod = 0; lod < 3; lod++)
            {
                var renderers = new List<Renderer>();
                var go = new GameObject("LOD" + lod);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = tree.bole[lod];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = NatureMaterialBuilder.Load(barkMat);
                renderers.Add(r);
                if (tree.crown[lod] != null)
                {
                    var leaves = new GameObject("Leaves" + lod);
                    leaves.transform.SetParent(go.transform, false);
                    leaves.AddComponent<MeshFilter>().sharedMesh = tree.crown[lod];
                    var lr = leaves.AddComponent<MeshRenderer>();
                    lr.sharedMaterial = NatureMaterialBuilder.Load(leafMat);
                    lr.shadowCastingMode = lod < 2 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderers.Add(lr);
                }
                levels[lod] = new LOD(heights[lod], renderers.ToArray());
            }
            group.SetLODs(levels);
            group.RecalculateBounds();

            var colliders = new GameObject("Collision") { layer = CourseKit.Layer("Environment") };
            colliders.transform.SetParent(root.transform, false);
            var axis = tree.axis;
            for (int i = 0; i + 2 < axis.Count; i += 2)
                Capsule(colliders.transform, "Trunk " + i, axis[i], axis[i + 2], Mathf.Min(tree.radii[i], tree.radii[i + 2]) * 0.95f);
            for (int l = 0; l < tree.limbs.Count; l++)
            {
                var (pts, radius) = tree.limbs[l];
                if (radius < 0.22f) continue;
                for (int i = 0; i + 1 < pts.Length; i++)
                {
                    float r = Mathf.Lerp(radius, radius * 0.25f, (i + 0.5f) / (pts.Length - 1)) * 0.9f;
                    if (r < 0.12f) break;
                    Capsule(colliders.transform, $"Limb {l}.{i}", pts[i], pts[i + 1], r);
                }
            }
            return root;
        }

        static void Capsule(Transform parent, string name, Vector3 a, Vector3 b, float radius)
        {
            var go = new GameObject(name) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            var c = go.AddComponent<CapsuleCollider>();
            c.direction = 1;
            c.radius = radius;
            c.height = Vector3.Distance(a, b) + radius * 2f;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
    }
}
