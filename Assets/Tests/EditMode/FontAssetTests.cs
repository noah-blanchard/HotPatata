using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace HotPatata.Tests
{
    /// <summary>ARCHITECTURE §6.2: font dependencies must ship on every desktop platform.</summary>
    public class FontAssetTests
    {
        const string Folder = "Assets/UI/Fonts/";

        [TestCase("Fallback-SDF")]
        [TestCase("LilitaOne-SDF")]
        [TestCase("Nunito-SDF")]
        [TestCase("Nunito-Bold-SDF")]
        [TestCase("Nunito-Black-SDF")]
        public void FontAndSubassets_CanBeIncludedInAPlayer(string name)
        {
            string path = Folder + name + ".asset";
            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            Assert.IsNotNull(font);
            Assert.AreNotEqual(AtlasPopulationMode.DynamicOS, font.atlasPopulationMode,
                name + " must not depend on an Editor or operating-system font");
            Assert.IsNotNull(font.sourceFontFile);
            StringAssert.StartsWith(Folder, AssetDatabase.GetAssetPath(font.sourceFontFile));
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                Assert.IsTrue(EditorUtility.IsPersistent(asset), asset.name);
                Assert.AreEqual(0, (int)(asset.hideFlags & HideFlags.DontSaveInBuild),
                    asset.name + " is excluded from player serialization");
            }
            foreach (var fallback in font.fallbackFontAssetTable)
            {
                Assert.IsNotNull(fallback);
                Assert.AreNotSame(font, fallback, "fallback chains must not refer to themselves");
                StringAssert.StartsWith(Folder, AssetDatabase.GetAssetPath(fallback));
            }
        }

        [Test]
        public void Fallback_ContainsCodeDialArrows_AndMissingGlyphSquare()
        {
            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(Folder + "Fallback-SDF.asset");
            foreach (uint character in new uint[] { 0x25B2, 0x25BC, 0x25A1 })
                Assert.IsTrue(font.HasCharacter(character, false, true), "Missing U+" + character.ToString("X4"));
        }
    }
}
