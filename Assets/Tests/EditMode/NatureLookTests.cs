using System.Linq;
using HotPatata.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>The nature look of PatataWilds (ARCHITECTURE §25.3): materials, import rules, generated shapes and vegetation.</summary>
    public class NatureLookTests
    {
        [Test]
        public void Library_UsesTheNatureShader_WithItsTextures()
        {
            Assert.GreaterOrEqual(NatureMaterialBuilder.Library.Length, 35);
            foreach (var surface in NatureMaterialBuilder.Library)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(NatureMaterialBuilder.Path(surface.name));
                Assert.IsNotNull(mat, surface.name + " missing (HotPatata/Nature/Build Nature Materials)");
                Assert.AreEqual(NatureMaterialBuilder.ShaderName, mat.shader.name, surface.name);
                Assert.IsNotNull(mat.GetTexture("_BaseMap"), surface.name + " has no colour map");
                if (surface.folder != "Generated")   // the drawn needle and blade sprays are flat cards
                    Assert.IsNotNull(mat.GetTexture("_BumpMap"), surface.name + " has no normal map");
                Assert.AreEqual(surface.mapping == NatureMaterialBuilder.Mapping.Triplanar, mat.IsKeywordEnabled("_MAPPING_TRIPLANAR"), surface.name);
                Assert.IsTrue(mat.enableInstancing, surface.name + " must allow instancing");
            }
        }

        [Test]
        public void Foliage_IsCutOut_TwoSided_AndSways()
        {
            foreach (var surface in NatureMaterialBuilder.Library.Where(s => s.foliage))
            {
                var mat = NatureMaterialBuilder.Load(surface.name);
                Assert.IsTrue(mat.IsKeywordEnabled("_ALPHATEST_ON"), surface.name);
                Assert.IsNotNull(mat.GetTexture("_AlphaMap"), surface.name);
                Assert.AreEqual((float)UnityEngine.Rendering.CullMode.Off, mat.GetFloat("_Cull"), surface.name);
                Assert.IsTrue(mat.IsKeywordEnabled("_WIND"), surface.name);
            }
        }

        [Test]
        public void GameplayVariants_KeepTheirCues()
        {
            Assert.Greater(NatureMaterialBuilder.Load(NatureMaterialBuilder.HazardName).GetFloat("_StripeStrength"), 0.5f, "hazards are banded (spec §19)");
            for (int slot = 1; slot <= 3; slot++)
                Assert.IsNotNull(NatureMaterialBuilder.Load(NatureMaterialBuilder.PipeName(slot)), "tube slot " + slot);
            Assert.Greater(NatureMaterialBuilder.Load(NatureMaterialBuilder.LanternName).GetColor("_EmissionColor").maxColorComponent, 1f);
            using (CourseKit.UseLookSet(CourseKit.LookSet.Nature))
            {
                Assert.AreEqual(NatureMaterialBuilder.HazardName, System.IO.Path.GetFileName(CourseKit.Look(KitRole.Hazard).material));
                Assert.AreEqual(NatureMaterialBuilder.FallingName, System.IO.Path.GetFileName(CourseKit.Look(KitRole.Falling).material));
                Assert.AreEqual(KitShape.RoughBox, CourseKit.Look(KitRole.Floor).shape);
                Assert.AreEqual(KitShape.Logs, CourseKit.Look(KitRole.Mover).shape);
            }
        }

        [TestCase("forest_floor_diff_2k.jpg", NatureAssetImporter.NatureMapKind.Color)]
        [TestCase("forest_floor_nor_gl_2k.jpg", NatureAssetImporter.NatureMapKind.Normal)]
        [TestCase("forest_floor_rough_2k.jpg", NatureAssetImporter.NatureMapKind.Linear)]
        [TestCase("forest_floor_disp_2k.jpg", NatureAssetImporter.NatureMapKind.Linear)]
        [TestCase("fern_02_alpha_1k.png", NatureAssetImporter.NatureMapKind.Alpha)]
        [TestCase("needles_fir_alpha.png", NatureAssetImporter.NatureMapKind.Alpha)]
        [TestCase("boulder_01_rough_1k.exr", NatureAssetImporter.NatureMapKind.Linear)]
        [TestCase("forest_floor_arm_2k.jpg", NatureAssetImporter.NatureMapKind.Unknown)]
        public void Importer_ReadsPolyHavenNames(string file, NatureAssetImporter.NatureMapKind expected)
        {
            Assert.AreEqual(expected, NatureAssetImporter.Classify(NatureAssetImporter.TextureFolder + "Any/" + file));
        }

        [Test]
        public void Skies_AreReflectionCubemaps()
        {
            var skies = AssetDatabase.FindAssets("t:Cubemap", new[] { NatureAssetImporter.HdriFolder.TrimEnd('/') });
            Assert.GreaterOrEqual(skies.Length, 5);
        }

        [TestCase(24f, 1f, 30f)]
        [TestCase(1f, 9f, 18f)]
        [TestCase(3f, 0.5f, 3f)]
        [TestCase(6f, 1.2f, 1.2f)]
        [TestCase(0.35f, 2f, 0.35f)]
        public void GeneratedShapes_StayInsideTheirBox_AndRepeat(float x, float y, float z)
        {
            var size = new Vector3(x, y, z);
            // rock may bulge out of its sides by at most 30 cm (less than a player's half width); never above its top
            var shapes = new (System.Func<Vector3, Vector3, Mesh> make, float side)[]
            {
                (NatureShapes.RoughBox, 0.31f), (NatureShapes.Crag, 0.31f), (NatureShapes.Boulder, 1e-3f), (NatureShapes.Logs, 1e-3f), (NatureShapes.Planks, 1e-3f)
            };
            foreach (var (make, side) in shapes)
            {
                var mesh = make(size, Vector3.one);
                var again = make(size, Vector3.one);
                try
                {
                    var b = mesh.bounds;
                    Assert.LessOrEqual(b.max.x, x / 2 + side, mesh.name); Assert.GreaterOrEqual(b.min.x, -x / 2 - side, mesh.name);
                    Assert.LessOrEqual(b.max.y, y / 2 + 1e-3f, mesh.name + " rises above its top"); Assert.GreaterOrEqual(b.min.y, -y / 2 - 1e-3f, mesh.name);
                    Assert.LessOrEqual(b.max.z, z / 2 + side, mesh.name); Assert.GreaterOrEqual(b.min.z, -z / 2 - side, mesh.name);
                    Assert.AreEqual(mesh.vertexCount, again.vertexCount, mesh.name);
                    CollectionAssert.AreEqual(mesh.vertices, again.vertices, mesh.name + " must be the same on every build");
                    Assert.AreEqual(mesh.vertexCount, mesh.tangents.Length, mesh.name + " needs tangents (normal maps)");
                }
                finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(again); }
            }
        }

        [Test]
        public void Skirt_HidesUnderItsSlab_AndWidensBelow()
        {
            var size = new Vector3(6f, 8f, 10f);
            var mesh = NatureShapes.Skirt(size, Vector3.one);
            try
            {
                Assert.LessOrEqual(mesh.bounds.max.y, size.y / 2 + 1e-3f);
                foreach (var v in mesh.vertices)
                    if (v.y > size.y / 2 - 0.2f)
                        Assert.IsTrue(Mathf.Abs(v.x) <= size.x / 2 + 1e-3f && Mathf.Abs(v.z) <= size.z / 2 + 1e-3f, "its top stays under the slab");
                Assert.Greater(mesh.bounds.size.x, size.x, "it widens below");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Trees_AreDeterministic_AndWithinTheirTriangleBudgets()
        {
            foreach (NatureTreeBuilder.Species s in System.Enum.GetValues(typeof(NatureTreeBuilder.Species)))
                for (int lod = 0; lod < 3; lod++)
                {
                    var a = NatureTreeBuilder.Grow(s, 0, lod);
                    var b = NatureTreeBuilder.Grow(s, 0, lod);
                    try
                    {
                        Assert.AreEqual(a.vertices, b.vertices, $"{s} LOD{lod} must be the same on every build");
                        int triangles = Enumerable.Range(0, a.subMeshCount).Sum(i => a.GetTriangles(i).Length) / 3;
                        Assert.LessOrEqual(triangles, NatureTreeBuilder.LodBudget[lod], $"{s} LOD{lod}");
                        Assert.AreEqual(a.vertexCount, a.colors.Length, $"{s} LOD{lod} carries its wind weights");
                    }
                    finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
                }
        }
    }
}
