using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>
    /// ARCHITECTURE §25.1: the KayKit palette has every family a kit role draws with, and <see cref="KitSkin"/> tiles a
    /// box exactly (no gap, no overflow), with the pack's pieces unstretched when the box is on the KayKit grid.
    /// </summary>
    public class KitSkinTests
    {
        const string PalettePath = "Assets/ScriptableObjects/Kit/KayKitPalette.asset";

        KitPalette palette;

        [SetUp]
        public void Load()
        {
            palette = AssetDatabase.LoadAssetAtPath<KitPalette>(PalettePath);
            Assert.IsNotNull(palette, "run HotPatata/Course/Build KayKit Kit");
        }

        [TestCase(KitShape.Platform, KitColor.Green)]
        [TestCase(KitShape.Platform, KitColor.Blue)]
        [TestCase(KitShape.Platform, KitColor.Yellow)]
        [TestCase(KitShape.Barrier, KitColor.Neutral)]
        [TestCase(KitShape.Barrier, KitColor.Red)]
        [TestCase(KitShape.Barrier, KitColor.Yellow)]
        [TestCase(KitShape.Barrier, KitColor.Blue)]
        [TestCase(KitShape.Arrow, KitColor.Blue)]
        [TestCase(KitShape.Arrow, KitColor.Green)]
        [TestCase(KitShape.Pipe, KitColor.Neutral)]
        public void Palette_HasTheFamily(KitShape shape, KitColor color)
        {
            var meshes = palette.Meshes(shape, color);
            Assert.IsNotEmpty(meshes);
            foreach (var m in meshes) Assert.IsTrue(m.isReadable, m.name + " must be readable to be combined in a build");
        }

        [TestCase(16f, 2f, 32f)]
        [TestCase(3f, 0.5f, 3f)]
        [TestCase(14.5f, 7f, 1f)]
        [TestCase(0.6f, 6f, 30f)]
        public void Layout_FillsTheBoxExactly(float x, float y, float z)
        {
            var size = new Vector3(x, y, z);
            var shape = y > Mathf.Min(x, z) ? KitShape.Barrier : KitShape.Platform;
            var tiles = KitSkin.Layout(palette.Meshes(shape, KitColor.Green), shape, size);
            Assert.IsNotEmpty(tiles);
            float volume = tiles.Sum(t => t.Cell.x * t.Cell.y * t.Cell.z);
            Assert.AreEqual(x * y * z, volume, x * y * z * 1e-4f, "the cells add up to the box");
            foreach (var t in tiles)
                for (int a = 0; a < 3; a++)
                {
                    Assert.LessOrEqual(t.Center[a] + t.Cell[a] / 2f, size[a] / 2f + 1e-4f, "no cell overflows the box");
                    Assert.GreaterOrEqual(t.Center[a] - t.Cell[a] / 2f, -size[a] / 2f - 1e-4f);
                }
        }

        [Test]
        public void Layout_OnTheGrid_UsesUnstretchedPieces()
        {
            var tiles = KitSkin.Layout(palette.Meshes(KitShape.Platform, KitColor.Green), KitShape.Platform, new Vector3(12f, 2f, 24f));
            foreach (var t in tiles)
            {
                Vector3 n = t.Mesh.bounds.size;
                Vector3 native = t.Turned ? new Vector3(n.z, n.y, n.x) : n;
                Assert.AreEqual(native.x, t.Cell.x, 0.01f, t.Mesh.name);
                Assert.AreEqual(native.y, t.Cell.y, 0.01f, t.Mesh.name);
                Assert.AreEqual(native.z, t.Cell.z, 0.01f, t.Mesh.name);
            }
        }

        [Test]
        public void Skin_DrawsTheScaledBox_AndAnEdgeStandingPlatformAsBlocks()
        {
            var go = new GameObject("KitSkinTest");
            try
            {
                go.transform.localScale = new Vector3(8f, 3.5f, 1f);   // a piston gate
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
                var skin = go.AddComponent<KitSkin>();
                skin.Configure(palette, KitShape.Platform, KitColor.Blue, Vector3.one, false);
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsNotNull(mesh);
                StringAssert.Contains("Barrier", mesh.name);
                Vector3 local = mesh.bounds.size;   // in the object's own (scaled) space: the unit box
                Assert.AreEqual(1f, local.x, 0.02f);
                Assert.AreEqual(1f, local.y, 0.02f);
                Assert.AreEqual(1f, local.z, 0.02f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
