using NUnit.Framework;

namespace HotPatata.Tests
{
    /// <summary>M6: how typed player names are cleaned up before anyone sees them.</summary>
    public class PlayerNamesTests
    {
        [TestCase("Noah", "Noah")]
        [TestCase("  Noah  ", "Noah")]
        [TestCase("Hot   \t Patata", "Hot Patata")]
        [TestCase("<b>Big</b>", "bBig/b")]
        [TestCase("Tab\u0007Bell​", "TabBell")]
        public void Sanitize_CleansTheName(string typed, string expected) =>
            Assert.AreEqual(expected, PlayerNames.Sanitize(typed, 0));

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("​\u0007")]
        [TestCase("<>")]
        public void Sanitize_NothingLeft_FallsBackToTheSlotName(string typed) =>
            Assert.AreEqual("Player 3", PlayerNames.Sanitize(typed, 2));

        [Test]
        public void Sanitize_LongName_IsCutToTheMaximum()
        {
            string name = PlayerNames.Sanitize("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 0);
            Assert.AreEqual(PlayerNames.MaxLength, name.Length);
            Assert.AreEqual("ABCDEFGHIJKLMNOP", name);
        }

        [Test]
        public void Sanitize_CutAfterASpace_HasNoTrailingSpace() =>
            Assert.AreEqual("ABCDEFGHIJKLMNO", PlayerNames.Sanitize("ABCDEFGHIJKLMNO PQR", 0));

        [Test]
        public void Sanitize_NeverSplitsASurrogatePair()
        {
            string name = PlayerNames.Sanitize(new string('a', PlayerNames.MaxLength - 1) + "\U0001F954\U0001F954", 0);
            Assert.AreEqual(PlayerNames.MaxLength - 1, name.Length);
            Assert.IsFalse(char.IsHighSurrogate(name[name.Length - 1]));
        }

        [Test]
        public void Default_IsOneBased() => Assert.AreEqual("Player 1", PlayerNames.Default(0));
    }
}
