using NUnit.Framework;

namespace HotPatata.Tests
{
    /// <summary>
    /// #79: the game code dials, so a gamepad can enter a code. Up / down turn the current dial through letters then
    /// digits (wrapping), typing fills a dial and moves on, Backspace clears back, and a pasted code is normalised.
    /// </summary>
    public class CodeDialsTests
    {
        [Test]
        public void StartsEmpty_WithSixDials()
        {
            var dials = new CodeDials();
            Assert.AreEqual("", dials.Value);
            Assert.AreEqual(SessionService.CodeLength, dials.childCount);
            Assert.IsTrue(dials.focusable, "one stop in the board's up / down walk");
        }

        [Test]
        public void Spin_TurnsTheCurrentDial_AndWraps()
        {
            var dials = new CodeDials();
            dials.Spin(1);
            Assert.AreEqual("A", dials.Value, "an empty dial starts at the first character");
            dials.Spin(1);
            Assert.AreEqual("B", dials.Value);
            dials.Spin(-2);
            Assert.AreEqual("9", dials.Value, "down from A wraps to the last digit");

            var fresh = new CodeDials();
            fresh.Spin(-1);
            Assert.AreEqual("9", fresh.Value, "down on an empty dial starts at the last character");
        }

        [Test]
        public void Typing_FillsAndMovesOn_BackspaceClearsBack()
        {
            var dials = new CodeDials();
            foreach (char c in "k7p2qx") Assert.IsTrue(dials.Type(c));
            Assert.AreEqual("K7P2QX", dials.Value);
            Assert.AreEqual(SessionService.CodeLength - 1, dials.Cursor, "the cursor stays on the last dial");
            Assert.IsFalse(dials.Type('-'), "only letters and digits");

            dials.Erase();
            Assert.AreEqual("K7P2Q", dials.Value);
            dials.Erase();
            Assert.AreEqual("K7P2", dials.Value, "an empty dial clears the one before it");
        }

        [Test]
        public void MoveCursor_StaysOnTheRow()
        {
            var dials = new CodeDials();
            dials.MoveCursor(-3);
            Assert.AreEqual(0, dials.Cursor);
            dials.MoveCursor(99);
            Assert.AreEqual(SessionService.CodeLength - 1, dials.Cursor);
        }

        [Test]
        public void SetValue_NormalisesAPastedCode()
        {
            var dials = new CodeDials();
            dials.SetValueWithoutNotify(" ab-12 cd ef ");
            Assert.AreEqual("AB12CD", dials.Value, "letters and digits only, upper case, cut to the code length");
        }

        [Test]
        public void Changed_ReportsTheCode()
        {
            var dials = new CodeDials();
            string seen = null;
            dials.Changed += v => seen = v;
            dials.Type('z');
            Assert.AreEqual("Z", seen);
        }

        [Test]
        public void Editing_ShowsOnTheRow()
        {
            var dials = new CodeDials();
            dials.SetEditing(true);
            Assert.IsTrue(dials.ClassListContains(CodeDials.EditingClass));
            dials.SetEditing(false);
            Assert.IsFalse(dials.ClassListContains(CodeDials.EditingClass));
        }
    }
}
