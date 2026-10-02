using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>M6.5: the per-player settings layer - defaults from GameTuning, parsing, clamping, fallback.</summary>
    public class SettingsTests
    {
        GameTuning tuning;

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<GameTuning>();   // never the shared asset
            tuning.viewEffectsStrength = 0.7f;
            tuning.flashReduction = 0.2f;
            tuning.mouseSensitivity = 0.12f;
            tuning.fieldOfView = 80f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(tuning);

        [Test]
        public void Parse_NoFile_GivesTheTuningDefaults()
        {
            var s = Settings.Parse("", tuning);
            Assert.AreEqual(0.7f, s.viewEffectsStrength);
            Assert.AreEqual(0.2f, s.flashReduction);
            Assert.AreEqual(0.12f, s.mouseSensitivity);
            Assert.AreEqual(80f, s.fieldOfView);
            Assert.AreEqual(tuning.beepVolume, s.beepVolume);
            Assert.AreEqual(tuning.stickLookSpeed, s.stickLookSpeed);
            Assert.IsFalse(s.invertY);
            Assert.AreEqual(1f, s.masterVolume);
            Assert.AreEqual(1f, s.sfxVolume);
            Assert.AreEqual(SettingsData.Unset, s.fullScreenMode);
        }

        [Test]
        public void Parse_OlderFile_MissingFieldsKeepTheTuningDefaults()
        {
            var s = Settings.Parse("{\"viewEffectsStrength\":0,\"invertY\":true}", tuning);
            Assert.AreEqual(0f, s.viewEffectsStrength);
            Assert.IsTrue(s.invertY);
            Assert.AreEqual(80f, s.fieldOfView, "not in the file: tuning default");
            Assert.AreEqual(0.12f, s.mouseSensitivity, "not in the file: tuning default");
        }

        [Test]
        public void Parse_RoundTrip_KeepsEveryValue()
        {
            var original = SettingsData.FromTuning(tuning);
            original.flashReduction = 1f;
            original.invertY = true;
            original.masterVolume = 0.3f;
            original.sfxVolume = 0.4f;
            original.vSyncCount = 0;
            original.targetFrameRate = 144;

            var s = Settings.Parse(JsonUtility.ToJson(original), tuning);
            Assert.AreEqual(JsonUtility.ToJson(original), JsonUtility.ToJson(s));
        }

        [Test]
        public void Parse_OutOfRange_IsClamped()
        {
            var s = Settings.Parse("{\"viewEffectsStrength\":3,\"flashReduction\":-1,\"fieldOfView\":500,\"mouseSensitivity\":0,\"masterVolume\":2,\"sfxVolume\":-1}", tuning);
            Assert.AreEqual(1f, s.viewEffectsStrength);
            Assert.AreEqual(0f, s.flashReduction);
            Assert.AreEqual(SettingsData.MaxFieldOfView, s.fieldOfView);
            Assert.AreEqual(SettingsData.MinMouseSensitivity, s.mouseSensitivity);
            Assert.AreEqual(1f, s.masterVolume);
            Assert.AreEqual(0f, s.sfxVolume);
        }

        [Test]
        public void Parse_BrokenDisplayValues_LeaveTheDisplayAlone()
        {
            var s = Settings.Parse("{\"fullScreenMode\":42,\"resolutionWidth\":1920,\"resolutionHeight\":0,\"vSyncCount\":4,\"targetFrameRate\":5}", tuning);
            Assert.AreEqual(SettingsData.Unset, s.fullScreenMode);
            Assert.AreEqual(SettingsData.Unset, s.resolutionWidth);
            Assert.AreEqual(SettingsData.Unset, s.resolutionHeight);
            Assert.AreEqual(1, s.vSyncCount);
            Assert.AreEqual(SettingsData.Unset, s.targetFrameRate);
        }

        [Test]
        public void Sanitize_BrokenNumbers_TakeTheDefault()
        {
            var defaults = SettingsData.FromTuning(tuning);
            var s = defaults.Clone();
            s.viewEffectsStrength = float.NaN;
            s.fieldOfView = float.PositiveInfinity;
            s.Sanitize(defaults);
            Assert.AreEqual(0.7f, s.viewEffectsStrength);
            Assert.AreEqual(80f, s.fieldOfView);
        }

        [Test]
        public void Accessors_WithNothingLoaded_ReadTheTuning()
        {
            Assume.That(Settings.Current, Is.Null, "settings are only loaded by the Bootstrap entry");
            Assert.AreEqual(0.7f, Settings.ViewEffectsStrength(tuning));
            Assert.AreEqual(0.2f, Settings.FlashReduction(tuning));
            Assert.AreEqual(80f, Settings.FieldOfView(tuning));
            Assert.IsFalse(Settings.InvertY);
            Assert.AreEqual(1f, Settings.SfxVolume);
        }

        [Test]
        public void MixerVolume_IsZeroDecibelsAtFull_AndSilentAtZero()
        {
            Assert.AreEqual(0f, AudioVolumes.ToDecibels(1f), 1e-4f);
            Assert.AreEqual(-6.02f, AudioVolumes.ToDecibels(0.5f), 0.01f);
            Assert.AreEqual(-80f, AudioVolumes.ToDecibels(0f));
            Assert.AreEqual(-80f, AudioVolumes.ToDecibels(0.00001f), "never below the mixer floor");
        }

        [Test]
        public void Editable_IsACopy_AndNeverTouchesTheTuning()
        {
            var edit = Settings.Editable(tuning);
            edit.viewEffectsStrength = 0f;
            edit.fieldOfView = 100f;
            Assert.AreEqual(0.7f, tuning.viewEffectsStrength);
            Assert.AreEqual(80f, tuning.fieldOfView);
        }
    }
}
