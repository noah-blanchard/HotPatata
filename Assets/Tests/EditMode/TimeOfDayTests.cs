using System.Linq;
using HotPatata.Editor;
using NUnit.Framework;
using UnityEditor;

namespace HotPatata.Tests
{
    /// <summary>PatataWilds' day (ARCHITECTURE §25.3): the blender's pure rules and the presets LookBuilder writes.</summary>
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

        [Test]
        public void Presets_MakeADay_DawnLow_NoonHigh_DuskLowAndCool()
        {
            var presets = AssetDatabase.FindAssets("t:TimeOfDayPreset").Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<TimeOfDayPreset>).ToArray();
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
    }
}
