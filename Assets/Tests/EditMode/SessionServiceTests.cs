using NUnit.Framework;
using Unity.Services.Multiplayer;

namespace Beep.Tests
{
    /// <summary>M4: how game codes are cleaned up and how errors are worded for players.</summary>
    public class SessionServiceTests
    {
        [TestCase("7prl9w", "7PRL9W")]
        [TestCase("  7PR-L9W ", "7PRL9W")]
        [TestCase("7 p r l 9 w", "7PRL9W")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void NormalizeCode_ForgivesSpacesDashesAndCase(string typed, string expected) =>
            Assert.AreEqual(expected, SessionService.NormalizeCode(typed));

        [TestCase("7PRL9W", true)]
        [TestCase("7PRL9", false)]
        [TestCase("7PRL9WX", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsPlausibleCode_NeedsExactlySixCharacters(string code, bool expected) =>
            Assert.AreEqual(expected, SessionService.IsPlausibleCode(code));

        [Test]
        public void Describe_UnknownCode_IsAClearSentence()
        {
            var e = new SessionException("lobby not found", SessionError.SessionNotFound, null);
            StringAssert.Contains("No game found", SessionService.Describe(e));
        }

        [Test]
        public void Describe_CodeWithAnInvalidCharacter_IsAClearSentence()
        {
            var e = new SessionException("lobby code 'ZZZZZZ' contains an invalid character 'Z' (U+005A) at index 0", SessionError.Unknown, null);
            StringAssert.Contains("not valid", SessionService.Describe(e));
            StringAssert.DoesNotContain("U+005A", SessionService.Describe(e));
        }

        [Test]
        public void Describe_FullGame_IsRecognisedFromTheMessage()
        {
            var e = new SessionException("The lobby is full", SessionError.Unknown, null);
            StringAssert.Contains("full", SessionService.Describe(e));
        }

        [TestCase(SessionError.RateLimitExceeded, "Too many attempts")]
        [TestCase(SessionError.SessionDeleted, "already ended")]
        [TestCase(SessionError.InvalidParameter, "not valid")]
        public void Describe_CommonErrors_AreReadable(SessionError error, string expectedFragment) =>
            StringAssert.Contains(expectedFragment, SessionService.Describe(new SessionException("x", error, null)));
    }
}
