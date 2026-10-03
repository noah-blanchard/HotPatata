using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// The settings screen (#17, spec §19, ARCHITECTURE §6.2; layout Assets/UI/Screens/Settings.uxml, the rows' labels
    /// and order included), reachable from the main menu and the pause menu. Four
    /// sections: accessibility (camera effects, flash reduction, warning beep), look (mouse and stick sensitivity,
    /// invert Y, field of view), audio (master, effects) and display (window mode, resolution, vsync, frame cap).
    /// It edits a copy of the player's <see cref="Settings"/>: every change applies at once (<see cref="Settings.Preview"/>)
    /// and the file is written when the screen closes (<see cref="Settings.Save"/>). The shared <see cref="GameTuning"/>
    /// is only read, for the defaults. Key bindings are kept as they are (their page is #18).
    /// </summary>
    public class SettingsScreen : UIScreen
    {
        readonly GameTuning tuning;
        SettingsData data;
        bool dirty;

        SliderRow effects, flashes, beep, mouse, stick, fov, master, sfx;
        ToggleRow invertY, vSync;
        ChoiceRow windowMode, resolution, frameCap;
        List<Vector2Int> resolutions;
        ScrollView scroll;

        public SettingsScreen(GameTuning tuning) => this.tuning = tuning;

        /// <summary>The values on screen (the active settings once a row changed).</summary>
        public SettingsData Data => data;

        protected override VisualElement Build()
        {
            data = Settings.Editable(tuning);
            var root = FromTemplate(Templates.settings);
            scroll = Require<ScrollView>("scroll");

            effects = SliderSetting("view-effects", 0f, 1f, data.viewEffectsStrength, 0.05f, SliderRow.Percent, v => data.viewEffectsStrength = v);
            effects.AddToClassList(FirstFocusClass);
            flashes = SliderSetting("flash-reduction", 0f, 1f, data.flashReduction, 0.05f, SliderRow.Percent, v => data.flashReduction = v);
            beep = SliderSetting("beep-volume", 0f, 1f, data.beepVolume, 0.05f, SliderRow.Percent, v => data.beepVolume = v);

            mouse = SliderSetting("mouse-sensitivity", SettingsData.MinMouseSensitivity, SettingsData.MaxMouseSensitivity,
                           data.mouseSensitivity, 0.01f, v => v.ToString("0.00"), v => data.mouseSensitivity = v);
            stick = SliderSetting("stick-sensitivity", SettingsData.MinStickLookSpeed, SettingsData.MaxStickLookSpeed,
                           data.stickLookSpeed, 10f, v => Mathf.RoundToInt(v).ToString(), v => data.stickLookSpeed = v);
            invertY = ToggleSetting("invert-y", data.invertY, v => data.invertY = v);
            fov = SliderSetting("field-of-view", SettingsData.MinFieldOfView, SettingsData.MaxFieldOfView,
                         data.fieldOfView, 1f, v => Mathf.RoundToInt(v) + "°", v => data.fieldOfView = Mathf.Round(v));

            master = SliderSetting("master-volume", 0f, 1f, data.masterVolume, 0.05f, SliderRow.Percent, v => data.masterVolume = v);
            sfx = SliderSetting("sfx-volume", 0f, 1f, data.sfxVolume, 0.05f, SliderRow.Percent, v => data.sfxVolume = v);

            windowMode = ChoiceSetting("window-mode", DisplayOptions.WindowModeNames, DisplayOptions.WindowModeIndex(CurrentMode()),
                                i => data.fullScreenMode = (int)DisplayOptions.WindowModes[i]);
            resolutions = DisplayOptions.Resolutions(Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)), CurrentResolution());
            resolution = ChoiceSetting("resolution", resolutions.Select(r => $"{r.x} x {r.y}").ToList(), resolutions.IndexOf(CurrentResolution()),
                                i =>
                                {
                                    data.resolutionWidth = resolutions[i].x;
                                    data.resolutionHeight = resolutions[i].y;
                                });
            vSync = ToggleSetting("vsync", CurrentVSync(), v =>
            {
                data.vSyncCount = v ? 1 : 0;
                frameCap.SetEnabled(!v);
            });
            frameCap = ChoiceSetting("frame-cap", DisplayOptions.FrameCapNames, DisplayOptions.FrameCapIndex(data.targetFrameRate),
                              i => data.targetFrameRate = DisplayOptions.FrameCaps[i]);
            frameCap.SetEnabled(!vSync.Value);
            Show(Require<Label>("editor-note"), Application.isEditor);

            Navigable(Require<Button>("reset")).clicked += ResetToDefaults;
            Navigable(Require<Button>("back")).clicked += () => Stack.Pop();
            return root;
        }

        // Each row: found in the layout, given its range / choices and value, in the up/down walk, scrolled into view
        // when focused, and every change previewed.
        SliderRow SliderSetting(string name, float min, float max, float value, float step, System.Func<float, string> format, System.Action<float> write)
        {
            var row = AddRow(Require<SliderRow>(name));
            row.Setup(min, max, value, step, format);
            row.Changed += v => Apply(() => write(v));
            return row;
        }

        ToggleRow ToggleSetting(string name, bool value, System.Action<bool> write)
        {
            var row = AddRow(Require<ToggleRow>(name));
            row.SetValueWithoutNotify(value);
            row.Changed += v => Apply(() => write(v));
            return row;
        }

        ChoiceRow ChoiceSetting(string name, IList<string> options, int index, System.Action<int> write)
        {
            var row = AddRow(Require<ChoiceRow>(name));
            row.SetOptions(options, index);
            row.Changed += v => Apply(() => write(v));
            return row;
        }

        T AddRow<T>(T row) where T : SettingRow
        {
            Navigable(row);
            row.RegisterCallback<FocusInEvent>(_ => scroll.ScrollTo(row));
            return row;
        }

        void Apply(System.Action change)
        {
            change();
            dirty = true;
            Settings.Preview(data, tuning);
        }

        /// <summary>Back to the designer defaults (the tuning values), keeping the key bindings; applied at once.</summary>
        void ResetToDefaults()
        {
            var defaults = SettingsData.FromTuning(tuning);
            defaults.bindingOverrides = data.bindingOverrides;
            data = defaults;
            dirty = true;
            Settings.Preview(data, tuning);
            Refresh();
        }

        void Refresh()
        {
            effects.SetValueWithoutNotify(data.viewEffectsStrength);
            flashes.SetValueWithoutNotify(data.flashReduction);
            beep.SetValueWithoutNotify(data.beepVolume);
            mouse.SetValueWithoutNotify(data.mouseSensitivity);
            stick.SetValueWithoutNotify(data.stickLookSpeed);
            invertY.SetValueWithoutNotify(data.invertY);
            fov.SetValueWithoutNotify(data.fieldOfView);
            master.SetValueWithoutNotify(data.masterVolume);
            sfx.SetValueWithoutNotify(data.sfxVolume);
            windowMode.SetIndexWithoutNotify(DisplayOptions.WindowModeIndex(CurrentMode()));
            resolution.SetIndexWithoutNotify(resolutions.IndexOf(CurrentResolution()));
            vSync.SetValueWithoutNotify(CurrentVSync());
            frameCap.SetIndexWithoutNotify(DisplayOptions.FrameCapIndex(data.targetFrameRate));
            frameCap.SetEnabled(!vSync.Value);
        }

        /// <summary>Closing the screen (Back, or the stack cleared) writes the file once.</summary>
        public override void OnHide()
        {
            if (!dirty) return;
            Settings.Save(data, tuning);
            dirty = false;
        }

        FullScreenMode CurrentMode() => data.fullScreenMode != SettingsData.Unset ? (FullScreenMode)data.fullScreenMode : Screen.fullScreenMode;

        Vector2Int CurrentResolution() => data.resolutionWidth != SettingsData.Unset
            ? new Vector2Int(data.resolutionWidth, data.resolutionHeight)
            : new Vector2Int(Screen.width, Screen.height);

        bool CurrentVSync() => (data.vSyncCount != SettingsData.Unset ? data.vSyncCount : QualitySettings.vSyncCount) > 0;
    }

    /// <summary>The display choices of the settings screen. Pure, unit tested.</summary>
    public static class DisplayOptions
    {
        public static readonly FullScreenMode[] WindowModes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        public static readonly string[] WindowModeNames = { "Fullscreen", "Borderless window", "Windowed" };

        /// <summary>Frame caps offered with vsync off; <see cref="SettingsData.Unset"/> (-1) = no cap.</summary>
        public static readonly int[] FrameCaps = { 30, 60, 120, 144, 240, SettingsData.Unset };
        public static readonly string[] FrameCapNames = { "30", "60", "120", "144", "240", "No cap" };

        /// <summary>The closest offered mode (a platform's maximised window reads as borderless).</summary>
        public static int WindowModeIndex(FullScreenMode mode)
        {
            int i = System.Array.IndexOf(WindowModes, mode);
            return i >= 0 ? i : mode == FullScreenMode.MaximizedWindow ? 1 : 0;
        }

        public static int FrameCapIndex(int frameRate)
        {
            int i = System.Array.IndexOf(FrameCaps, frameRate);
            return i >= 0 ? i : FrameCaps.Length - 1;
        }

        /// <summary>The screen's resolutions without refresh-rate duplicates, smallest first, always including <paramref name="current"/>.</summary>
        public static List<Vector2Int> Resolutions(IEnumerable<Vector2Int> available, Vector2Int current)
        {
            var list = available.Where(r => r.x > 0 && r.y > 0).Distinct().ToList();
            if (!list.Contains(current)) list.Add(current);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }
    }
}
