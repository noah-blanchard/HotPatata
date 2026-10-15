using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// The nature pieces of an authored level (ARCHITECTURE §27): giant trees grown from their data (sawn where decks wrap them) and
    /// water with its kill zone. Called by <see cref="LevelPieceBuilder"/>.
    /// </summary>
    public static class LevelNature
    {
        /// <summary>A deck within this of a trunk (at its height) wraps it: the crown's limbs are sawn off round it.</summary>
        public const float WrapReach = 4f;
        /// <summary>The band sawn round a wrapping deck: from this far under its surface to this far over it (m).</summary>
        public const float SawBelow = 1f, SawAbove = 3f;

        // ------------------------------------------------------------------ giant trees

        /// <summary>
        /// The height bands (above the tree's foot) where decks wrap the trunk: there the crown's random limbs are stubs, so no limb
        /// runs through a deck, its rail or the heads above it. Hand-placed limbs are never sawn.
        /// </summary>
        public static List<Vector2> SawnBands(HeroTreeShape tree)
        {
            var bands = new List<Vector2>();
            float foot = tree.transform.position.y;
            foreach (var deck in TimberLinks.Pieces(tree.gameObject.scene).OfType<TimberDeckShape>())
            {
                float y = deck.transform.position.y;
                if (!Wraps(tree, deck)) continue;
                bands.Add(new Vector2(y - foot - SawBelow, y - foot + SawAbove));
            }
            return bands.OrderBy(b => b.x).ToList();
        }

        /// <summary>True when <paramref name="deck"/> wraps <paramref name="tree"/>'s trunk: it names it, or its edge comes within <see cref="WrapReach"/>.</summary>
        public static bool Wraps(HeroTreeShape tree, TimberDeckShape deck)
        {
            if (deck.TrunkComponent == tree) return true;
            if (deck.Outline.Count < 3 || !tree.TryTrunkAt(deck.transform.position.y, out var center, out float radius)) return false;
            var poly = deck.WorldOutline();
            var c = new Vector2(center.x, center.z);
            return Poly2.Contains(poly, c) || Poly2.DistanceToEdge(poly, c) - radius < WrapReach;
        }

        public static void BuildTree(HeroTreeShape tree, Transform root, string name)
        {
            var spec = new NatureTreeBuilder.HeroSpec
            {
                name = name,
                seed = tree.Seed,
                conifer = tree.TreeKind != HeroTreeShape.Kind.Broadleaf,
                dead = tree.TreeKind == HeroTreeShape.Kind.Dead,
                height = tree.Height,
                baseRadius = tree.BaseRadius,
                topRadius = Mathf.Min(tree.TopRadius, tree.BaseRadius),
                lean = tree.Lean,
                crownStart = Mathf.Clamp(tree.CrownStart, 4f, tree.Height - 3f),
                crownLimbs = tree.CrownLimbs,
                limbs = tree.Limbs.Select(l => new NatureTreeBuilder.HeroLimb(l.height, l.yaw, Mathf.Max(0.5f, l.length), l.rise, Mathf.Max(0.05f, l.radius), l.leaves)).ToArray(),
                clearings = SawnBands(tree).ToArray(),
            };
            var hero = NatureTreeBuilder.BuildHero(spec);
            NatureTreeBuilder.PlaceHero(root, hero, tree.transform.position, tree.transform.eulerAngles.y);
            var anchors = new List<Vector3>();
            for (int i = 0; i < hero.limbs.Count; i++)
            {
                anchors.Add(hero.LimbPoint(i, 0.55f));
                anchors.Add(hero.LimbPoint(i, 0.9f));
            }
            tree.Measure(hero.axis.ToArray(), hero.radii.ToArray(), anchors.ToArray());
        }

        // ------------------------------------------------------------------ water

        static bool waterReady;

        public static void BuildWater(WaterShape water, Transform root, string name)
        {
            if (!waterReady)
            {
                NatureKit.EnsureWater();
                waterReady = true;
            }
            var t = water.transform;
            var turn = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            var surface = new GameObject(name + " surface");
            surface.transform.SetParent(root, false);
            surface.transform.SetPositionAndRotation(t.position, turn);
            surface.transform.localScale = new Vector3(water.Size.x, 1f, water.Size.y);
            surface.AddComponent<MeshFilter>().sharedMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(NatureKit.WaterMeshPath);
            var r = surface.AddComponent<MeshRenderer>();
            r.sharedMaterial = NatureMaterialBuilder.Load(NatureKit.WaterMaterialName);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            surface.AddComponent<NatureWater>();
            float top = t.position.y - NatureKit.KillBelowWater, bottom = Mathf.Min(t.position.y - water.Depth, top - 0.6f);
            var kill = CourseKit.Place(root, CourseKit.GameplayDir + "KillZone", name + " (drowning)", new Vector3(t.position.x, (top + bottom) / 2f, t.position.z), turn);
            kill.transform.localScale = new Vector3(water.Size.x, top - bottom, water.Size.y);
        }

        /// <summary>The ground under (x, z): the terrain where there is one (fast), else the highest non-timber collider.</summary>
        public static float GroundAt(Scene scene, Vector2 p)
        {
            foreach (var terrain in LevelTerrain.Terrains(scene))
                if (LevelTerrain.Covers(terrain, p)) return terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
            return SceneSite.GroundAt(p, 2000f, 4000f);
        }
    }
}
