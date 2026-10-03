using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>#17: the display choices of the settings screen (window modes, resolutions, frame caps).</summary>
    public class DisplayOptionsTests
    {
        [Test]
        public void Resolutions_DropRefreshRateDuplicates_SortThem_AndKeepTheCurrentOne()
        {
            var list = DisplayOptions.Resolutions(new[]
            {
                new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(0, 0)
            }, new Vector2Int(1600, 900));
            CollectionAssert.AreEqual(new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) }, list);
        }

        [TestCase(FullScreenMode.ExclusiveFullScreen, 0)]
        [TestCase(FullScreenMode.FullScreenWindow, 1)]
        [TestCase(FullScreenMode.MaximizedWindow, 1)]
        [TestCase(FullScreenMode.Windowed, 2)]
        public void WindowMode_MapsToTheOfferedChoice(FullScreenMode mode, int index) =>
            Assert.AreEqual(index, DisplayOptions.WindowModeIndex(mode));

        [Test]
        public void FrameCap_NoCapIsUnset_AndAnUnknownCapShowsAsNoCap()
        {
            Assert.AreEqual(SettingsData.Unset, DisplayOptions.FrameCaps[DisplayOptions.FrameCapIndex(SettingsData.Unset)]);
            Assert.AreEqual(60, DisplayOptions.FrameCaps[DisplayOptions.FrameCapIndex(60)]);
            Assert.AreEqual(SettingsData.Unset, DisplayOptions.FrameCaps[DisplayOptions.FrameCapIndex(75)]);
            Assert.AreEqual(DisplayOptions.FrameCaps.Length, DisplayOptions.FrameCapNames.Length);
            Assert.AreEqual(DisplayOptions.WindowModes.Length, DisplayOptions.WindowModeNames.Length);
        }
    }
}
