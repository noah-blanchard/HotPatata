using NUnit.Framework;

namespace HotPatata.Tests
{
    /// <summary>#14: the cursor is locked only while gameplay wants it and no UI screen needs the mouse (ARCHITECTURE §6.2).</summary>
    public class CursorPolicyTests
    {
        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        public void Resolve_ScreenWinsOverGameplay(bool gameplayWantsLock, bool screenWantsCursor, bool locked) =>
            Assert.AreEqual(locked, CursorPolicy.Resolve(gameplayWantsLock, screenWantsCursor));
    }
}
