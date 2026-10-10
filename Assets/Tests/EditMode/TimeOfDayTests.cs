using System.Linq;
using HotPatata.Editor;
using NUnit.Framework;
using UnityEditor;

namespace HotPatata.Tests
{
    /// <summary>The nature courses' days (ARCHITECTURE §25.3): the blender's pure rules and the presets LookBuilder writes.</summary>
    public class TimeOfDayTests
    {
        [Test]
        public void Step_BlendsForward_ButSnapsBackAndOverBigJumps()
        {
            Assert.AreEqual(0.1f, TimeOfDayBlender.Step(0f, 0.16f, 2.4f, 24f, 0.6f), 1e-4f, "forward at one act per blend time");
            Assert.AreEqual(0.16f, TimeOfDayBlender.Step(0.15f, 0.16f, 1f, 24f, 0.6f), 1e-4f, "never past the target");
            Assert.AreEqual(0f, TimeOfDayBlender.Step(2.2f, 0f, 0.016f, 24f, 0.6f), "a restart is shown at once");
            Assert.AreEqual(2.08f, TimeOfDayBlender.Step(0f, 2.08f, 0.016f, 24f, 0.6f), 1e-4f, "a practice start is shown at once");
        }

        [Test]
        public void Segment_PicksTheTwoPresetsAround()
        {
            Assert.AreEqual((0, 0f), TimeOfDayBlender.Segment(5, 0f));
            var (i, f) = TimeOfDayBlender.Segment(5, 2.25f);
            Assert.AreEqual(2, i); Assert.AreEqual(0.25f, f, 1e-4f);
            Assert.AreEqual((3, 1f), TimeOfDayBlender.Segment(5, 4f));
            Assert.AreEqual((3, 1f), TimeOfDayBlender.Segment(5, 9f));
            Assert.AreEqual(0f, TimeOfDayBlender.TargetFor(null, 3));
            Assert.AreEqual(2f, TimeOfDayBlender.TargetFor(new[] { 0f, 1f, 2f }, 7), "clamped to the table");
        }

        [Test]
        public void CheckpointTimes_GoFromDawnToDusk()
        {
            var times = PatataWildsLook.CheckpointTimes(PatataWildsBuilder.CheckpointCount);
            Assert.AreEqual(PatataWildsBuilder.CheckpointCount + 1, times.Length);
            Assert.AreEqual(0f, times[0]);
            Assert.AreEqual(PatataWildsLook.MomentCount - 1, times[times.Length - 1], 1e-4f);
            for (int i = 1; i < times.Length; i++) Assert.Greater(times[i], times[i - 1]);
        }

        static TimeOfDayPreset[] PresetsIn(string folder) =>
            AssetDatabase.FindAssets("t:TimeOfDayPreset", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetDirectoryName(p).Replace('\\', '/') == folder).OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<TimeOfDayPreset>).ToArray();

        [TestCase("Assets/Settings/Look/TimeOfDay")]
        [TestCase("Assets/Settings/Look/TimeOfDay/Canopy")]
        public void Presets_MakeADay_DawnLow_NoonHigh_DuskLowAndCool(string folder)
        {
            var presets = PresetsIn(folder);
            Assert.AreEqual(5, presets.Length, "LookBuilder writes five moments (HotPatata/Look/Apply Look To All Scenes)");
            Assert.Less(presets[0].sunElevation, presets[1].sunElevation);
            Assert.Greater(presets[1].sunElevation, presets[2].sunElevation);
            Assert.Greater(presets[2].sunElevation, presets[3].sunElevation);
            Assert.Greater(presets[3].sunElevation, presets[4].sunElevation);
            Assert.Less(presets[4].sunIntensity, presets[1].sunIntensity);
            Assert.Greater(presets[4].sunColor.b, presets[4].sunColor.r, "dusk light is cool");
            Assert.Greater(presets[3].sunColor.r, presets[3].sunColor.b, "sunset light is warm");
            foreach (var p in presets)
            {
                Assert.IsNotNull(p.sky, p.name);
                Assert.IsNotNull(new SerializedObject(p).FindProperty("grade").objectReferenceValue, p.name);
            }
        }

        [TestCase("Assets/Settings/Look/TimeOfDay")]
        [TestCase("Assets/Settings/Look/TimeOfDay/Canopy")]
        public void Mist_ThickAtDawn_ThinAtNoon_BackAtDusk(string folder)
        {
            var presets = PresetsIn(folder);
            Assert.Greater(presets[0].mistDensity, presets[1].mistDensity * 2f, "a morning mist");
            Assert.Greater(presets[4].mistDensity, presets[1].mistDensity * 2f, "the mist comes back at dusk");
            Assert.Greater(presets[3].mistDensity, presets[2].mistDensity, "and gathers from sunset");
        }

        [Test]
        public void Canopy_HasItsOwnCoolerDay_KeepingPassesClear()
        {
            var wilds = PresetsIn("Assets/Settings/Look/TimeOfDay");
            var canopy = PresetsIn("Assets/Settings/Look/TimeOfDay/Canopy");
            for (int i = 0; i < 5; i++)
            {
                Assert.AreNotSame(wilds[i], canopy[i]);
                float warmthWilds = wilds[i].fogColor.r - wilds[i].fogColor.b, warmthCanopy = canopy[i].fogColor.r - canopy[i].fogColor.b;
                Assert.Less(warmthCanopy, warmthWilds, canopy[i].name + ": a cooler haze than PatataWilds");
                Assert.GreaterOrEqual(canopy[i].fogStart, 30f, canopy[i].name + ": the haze starts beyond the longest pass (14.5 m)");
            }
            Assert.AreEqual(5, PatataCanopyLook.MomentCount);
        }
    }
}
