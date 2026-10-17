using System.Linq;
using HotPatata.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>
    /// The authored levels' look (ARCHITECTURE §27, M18.3b): the kit's forest variants are real Prefab Variants that keep the kit's
    /// collider and draw their own visual; the prop library's props are at their size, drawn with the nature shader, decoration until
    /// made solid, and Solid fits a box on Environment round what a prop draws.
    /// </summary>
    public class LevelLookTests
    {
        GameObject made;

        [TearDown]
        public void Clean()
        {
            if (made != null) Object.DestroyImmediate(made);
        }

        [Test]
        public void ForestLooks_AreVariantsOfTheKit_WithTheirOwnVisual()
        {
            var looks = ForestKitBuilder.Variants().ToList();
            Assert.AreEqual(15, looks.Count);
            foreach (var (variant, basePath) in looks)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(variant);
                Assert.IsNotNull(prefab, variant + " is built (HotPatata/Kit/Build Forest Looks)");
                Assert.IsTrue(PrefabUtility.IsPartOfVariantPrefab(prefab), variant + " is a Prefab Variant");
                Assert.AreEqual(AssetDatabase.LoadAssetAtPath<GameObject>(basePath), PrefabUtility.GetCorrespondingObjectFromSource(prefab), variant + "'s base");
                Assert.IsEmpty(ObstaclePrefabValidator.Validate(prefab), variant + " keeps the obstacle contract");
                if (variant == ForestKitBuilder.Chute)
                {
                    // the tube's pipe follows its path when built: the chute keeps it, in wood
                    var pipes = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).ToList();
                    Assert.IsFalse(pipes.Any(m => m.name.StartsWith("Tube_Pipe")), "the chute's pipe is still the kit's");
                    Assert.IsTrue(pipes.Any(m => m == NatureMaterialBuilder.Load(TimberKit.TimberMaterial)), "the chute is not in wood");
                    continue;
                }
                var visual = prefab.GetComponentInChildren<CustomVisual>(true);
                Assert.IsNotNull(visual, variant + " draws its own visual");
                Assert.IsTrue(visual.GetComponentsInChildren<Renderer>(true).Length > 0, variant + "'s visual has renderers");
                Assert.IsEmpty(visual.GetComponentsInChildren<Collider>(true), variant + "'s visual is renderers only");
            }
        }

        [Test]
        public void ForestLooks_KeepTheKitsColliders()
        {
            foreach (var (variant, basePath) in ForestKitBuilder.Variants())
            {
                var mine = AssetDatabase.LoadAssetAtPath<GameObject>(variant).GetComponentsInChildren<Collider>(true);
                var kit = AssetDatabase.LoadAssetAtPath<GameObject>(basePath).GetComponentsInChildren<Collider>(true);
                Assert.AreEqual(kit.Length, mine.Length, variant + " adds or removes a collider");
                for (int i = 0; i < kit.Length; i++)
                    Assert.AreEqual(kit[i].bounds.size, mine[i].bounds.size, variant + ": " + mine[i].name + " is not the kit's size");
            }
        }

        [Test]
        public void Props_AreDecorationDrawnInTheNatureLook()
        {
            var props = AssetDatabase.FindAssets("t:Prefab", new[] { PropLibraryBuilder.Folder.TrimEnd('/') })
                                     .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.Greater(props.Count, 50, "the prop library is built (HotPatata/Level/Build Prop Library)");
            foreach (var prop in props)
            {
                var solid = prop.GetComponent<PropSolid>();
                Assert.IsNotNull(solid, prop.name + " has its Solid switch");
                Assert.AreEqual(solid.Solid ? 1 : 0, prop.GetComponentsInChildren<Collider>(true).Length, prop.name + ": a box only when solid");
                foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
                    Assert.IsTrue(r.sharedMaterials.All(m => m != null && m.shader.name.StartsWith("HotPatata/")), prop.name + "/" + r.name + " is not fogged like the courses");
            }
        }

        [Test]
        public void ScannedRuins_AreAtTheirRealHeight()
        {
            var pillar = AssetDatabase.LoadAssetAtPath<GameObject>(PropLibraryBuilder.PrefabPath("maya_relief_pillar"));
            Assume.That(pillar != null, "the pillar's model is fetched");
            made = Object.Instantiate(pillar);
            var b = made.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            Assert.AreEqual(2.4f, b.size.y, 0.02f, "the scan brought to a unit height is set to its real height");
            Assert.AreEqual(0f, b.min.y, 0.02f, "it stands on its base");
        }

        [Test]
        public void Solid_FitsABoxOnEnvironment_AndTakesItAway()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PropLibraryBuilder.PrefabPath("wooden_crate_01"));
            Assume.That(prefab != null, "the crate's model is fetched");
            made = Object.Instantiate(prefab);
            made.transform.position = new Vector3(4000f, 2f, 4000f);
            made.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            var prop = made.GetComponent<PropSolid>();
            var drawn = made.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });

            PropSolidEditor.SetSolid(prop, true, false);
            Assert.IsTrue(prop.Solid);
            Assert.IsNotNull(prop.Box);
            Assert.AreEqual(LayerMask.NameToLayer("Environment"), prop.Box.gameObject.layer);
            Assert.IsFalse(prop.Box.isTrigger);
            Physics.SyncTransforms();
            Assert.IsTrue(prop.Box.bounds.Contains(drawn.center), "the box is round the crate");
            Assert.AreEqual(drawn.size.y, prop.Box.bounds.size.y, 0.05f, "the box is the crate's height");

            PropSolidEditor.SetSolid(prop, false, false);
            Assert.IsFalse(prop.Solid);
            Assert.IsEmpty(made.GetComponentsInChildren<Collider>(true), "decoration has no collider");
        }

        [Test]
        public void TheWorkbenchAndTheWoodpile_AreSolid()
        {
            foreach (var name in new[] { "Workbench", "Woodpile" })
            {
                var set = AssetDatabase.LoadAssetAtPath<GameObject>(PropLibraryBuilder.SetFolder + name + ".prefab");
                Assert.IsNotNull(set, name);
                Assert.IsTrue(set.GetComponent<PropSolid>().Solid, name + " is in the way: solid");
            }
        }
    }
}
