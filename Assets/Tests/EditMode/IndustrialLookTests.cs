using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HotPatata.Editor;

namespace HotPatata.Tests
{
    /// <summary>
    /// ARCHITECTURE §25.2 (issue #87): the bevelled box stays inside its box, the industrial materials survive rebuilds, the
    /// texture importer reads the usual file names, and the look set changes nothing outside its scope.
    /// </summary>
    public class IndustrialLookTests
    {
        [TestCase(8f, 0.5f, 3f)]
        [TestCase(1f, 1f, 1f)]
        [TestCase(12f, 6f, 1f)]
        [TestCase(0.15f, 1.1f, 32f)]
        public void BevelBox_StaysInsideTheBox_AndFacesOutward(float x, float y, float z)
        {
            var go = new GameObject("BevelTest");
            try
            {
                var scale = new Vector3(x, y, z);
                go.transform.localScale = scale;
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
                go.AddComponent<KitSkin>().Configure(null, KitShape.BevelBox, KitColor.Neutral, Vector3.one, false);
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsNotNull(mesh);
                var vertices = new List<Vector3>(); mesh.GetVertices(vertices);
                var normals = new List<Vector3>(); mesh.GetNormals(normals);
                var triangles = mesh.triangles;
                for (int a = 0; a < 3; a++)
                {
                    Assert.AreEqual(0.5f, mesh.bounds.max[a], 1e-3f, "the faces reach the full box");
                    Assert.AreEqual(-0.5f, mesh.bounds.min[a], 1e-3f);
                }
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    // in metres the box is convex around the origin: every triangle's geometric normal points away from it
                    Vector3 p0 = Vector3.Scale(vertices[triangles[i]], scale), p1 = Vector3.Scale(vertices[triangles[i + 1]], scale), p2 = Vector3.Scale(vertices[triangles[i + 2]], scale);
                    Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
                    Assert.Greater(Vector3.Dot(n, (p0 + p1 + p2) / 3f), 0f, "triangle " + i / 3 + " faces inward");
                }
                foreach (var n in normals) Assert.AreEqual(1f, n.magnitude, 1e-3f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Chamfer_IsBetweenTwoAndSixCentimetres_AndNeverEatsAThinBox()
        {
            Assert.AreEqual(0.06f, KitSkin.ChamferFor(new Vector3(20, 20, 20)), 1e-5f);
            Assert.AreEqual(0.02f, KitSkin.ChamferFor(new Vector3(0.15f, 1.1f, 32)), 1e-5f);
            Assert.LessOrEqual(KitSkin.ChamferFor(new Vector3(0.02f, 5, 5)), 0.02f * 0.4f + 1e-6f);
        }

        [Test]
        public void Shader_IsSupported_AndExposesEveryTextureSlot()
        {
            var shader = Shader.Find(IndustrialMaterialBuilder.ShaderName);
            Assert.IsNotNull(shader);
            Assert.IsTrue(shader.isSupported);
            var mat = new Material(shader);
            try
            {
                foreach (var slot in new[] { "_BaseMap", "_BumpMap", "_GlossMap", "_MetallicMap", "_OcclusionMap", "_TileSize", "_GlossIsRoughness", "_FlipNormalY" })
                    Assert.IsTrue(mat.HasProperty(slot), slot);
            }
            finally { Object.DestroyImmediate(mat); }
        }

        [Test]
        public void Materials_Exist_AndAreNotRewrittenByARebuild()
        {
            IndustrialMaterialBuilder.Ensure(false);
            foreach (var name in new[] { "Industrial_Concrete", "Industrial_PaintedMetal", "Industrial_RawMetal", "Industrial_Rubber", "Industrial_PaintedMetal_Yellow", "Industrial_PaintedMetal_Safety" })
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(IndustrialMaterialBuilder.Dir + name + ".mat"), name);
            var concrete = AssetDatabase.LoadAssetAtPath<Material>(IndustrialMaterialBuilder.Dir + "Industrial_Concrete.mat");
            float original = concrete.GetFloat("_TileSize");
            try
            {
                concrete.SetFloat("_TileSize", 3.3f);
                IndustrialMaterialBuilder.Ensure(false);
                Assert.AreEqual(3.3f, concrete.GetFloat("_TileSize"), 1e-5f, "an existing material is never rewritten");
            }
            finally { concrete.SetFloat("_TileSize", original); EditorUtility.SetDirty(concrete); }
            var yellow = AssetDatabase.LoadAssetAtPath<Material>(IndustrialMaterialBuilder.Dir + "Industrial_PaintedMetal_Yellow.mat");
            Assert.IsTrue(yellow.isVariant);
            Assert.AreEqual("Industrial_PaintedMetal", yellow.parent.name, "accents inherit the painted metal's textures");
        }

        [TestCase("Concrete034_2K-PNG_Color.png", IndustrialTextureImporter.MapKind.Color)]
        [TestCase("Concrete_BaseColor.png", IndustrialTextureImporter.MapKind.Color)]
        [TestCase("concrete_wall_008_diff_2k.jpg", IndustrialTextureImporter.MapKind.Color)]   // Poly Haven puts the size last
        [TestCase("concrete_wall_008_nor_gl_4k.exr", IndustrialTextureImporter.MapKind.Normal)]
        [TestCase("concrete_wall_diff.jpg", IndustrialTextureImporter.MapKind.Color)]
        [TestCase("Metal032_2K-PNG_NormalGL.png", IndustrialTextureImporter.MapKind.Normal)]
        [TestCase("metal_plate_nor_gl.exr", IndustrialTextureImporter.MapKind.Normal)]
        [TestCase("Metal032_2K-PNG_NormalDX.png", IndustrialTextureImporter.MapKind.NormalDirectX)]
        [TestCase("metal_plate_nor_dx.png", IndustrialTextureImporter.MapKind.NormalDirectX)]
        [TestCase("Rubber_Roughness.png", IndustrialTextureImporter.MapKind.Linear)]
        [TestCase("metal_plate_rough.png", IndustrialTextureImporter.MapKind.Linear)]
        [TestCase("Metal032_2K-PNG_Metalness.png", IndustrialTextureImporter.MapKind.Linear)]
        [TestCase("Metal032_2K-PNG_AmbientOcclusion.png", IndustrialTextureImporter.MapKind.Linear)]
        [TestCase("metal_plate_ao.png", IndustrialTextureImporter.MapKind.Linear)]
        [TestCase("Concrete_Displacement.png", IndustrialTextureImporter.MapKind.Unknown)]
        public void TextureImporter_ClassifiesTheUsualNames(string file, IndustrialTextureImporter.MapKind expected)
        {
            Assert.AreEqual(expected, IndustrialTextureImporter.Classify("Assets/Art/Textures/Industrial/Concrete/" + file));
        }

        [Test]
        public void LookSet_ChangesOnlyItsScope_AndKeepsHazardsStriped()
        {
            Assert.AreEqual(CourseKit.LookSet.KayKit, CourseKit.CurrentLookSet);
            var before = CourseKit.Look(KitRole.Floor);
            using (CourseKit.UseLookSet(CourseKit.LookSet.Industrial))
            {
                Assert.AreEqual(KitShape.BevelBox, CourseKit.Look(KitRole.Floor).shape);
                Assert.AreEqual(CourseKit.ConcreteMaterial, CourseKit.Look(KitRole.Brick).material);
                Assert.AreEqual(CourseKit.KitHazardMaterial, CourseKit.Look(KitRole.Hazard).material, "hazards keep their stripes (spec §19)");
            }
            Assert.AreEqual(CourseKit.LookSet.KayKit, CourseKit.CurrentLookSet);
            Assert.AreEqual(before, CourseKit.Look(KitRole.Floor));
        }
    }
}
