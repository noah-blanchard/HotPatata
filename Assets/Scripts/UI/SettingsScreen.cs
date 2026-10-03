using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// The settings screen (#17, spec §19, ARCHITECTURE §6.2), reachable from the main menu and the pause menu. Four
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

        public SettingsScreen(GameTuning tuning) => this.tuning = tuning;

        /// <summary>The values on screen (the active settings once a row changed).</summary>
        public SettingsData Data => data;

        protected override VisualElement Build()
        {
            data = Settings.Editable(tuning);
            var root = new VisualElement();
            root.AddToClassList("hp-overlay");
            var panel = new VisualElement();
            panel.AddToClassList("hp-panel");
            panel.AddToClassList("hp-panel--wide");
            root.Add(panel);
            var title = new Label("Settings");
            title.AddToClassList("hp-title");
            panel.Add(title);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("hp-scroll");
            panel.Add(scroll);

            Section(scroll, "Accessibility");
            effects = Row(scroll, new SliderRow("Camera effects (shake, bob, roll, FOV kick)", 0f, 1f, data.viewEffectsStrength, 0.05f, SliderRow.Percent),
                          v => data.viewEffectsStrength = v);
            effects.AddToClassList(FirstFocusClass);
            flashes = Row(scroll, new SliderRow("Flash reduction", 0f, 1f, data.flashReduction, 0.05f, SliderRow.Percent), v => data.flashReduction = v);
            beep = Row(scroll, new SliderRow("Fuse warning volume", 0f, 1f, data.beepVolume, 0.05f, SliderRow.Percent), v => data.beepVolume = v);

            Section(scroll, "Look");
            mouse = Row(scroll, new SliderRow("Mouse sensitivity", SettingsData.MinMouseSensitivity, SettingsData.MaxMouseSensitivity,
                                              data.mouseSensitivity, 0.01f, v => v.ToString("0.00")), v => data.mouseSensitivity = v);
            stick = Row(scroll, new SliderRow("Stick sensitivity", SettingsData.MinStickLookSpeed, SettingsData.MaxStickLookSpeed,
                                              data.stickLookSpeed, 10f, v => Mathf.RoundToInt(v).ToString()), v => data.stickLookSpeed = v);
            invertY = Row(scroll, new ToggleRow("Invert Y", data.invertY), v => data.invertY = v);
            fov = Row(scroll, new SliderRow("Field of view", SettingsData.MinFieldOfView, SettingsData.MaxFieldOfView,
                                            data.fieldOfView, 1f, v => Mathf.RoundToInt(v) + "°"), v => data.fieldOfView = Mathf.Round(v));

            Section(scroll, "Audio");
            master = Row(scroll, new SliderRow("Master volume", 0f, 1f, data.masterVolume, 0.05f, SliderRow.Percent), v => data.masterVolume = v);
            sfx = Row(scroll, new SliderRow("Effects volume", 0f, 1f, data.sfxVolume, 0.05f, SliderRow.Percent), v => data.sfxVolume = v);

            Section(scroll, "Display");
            windowMode = Row(scroll, new ChoiceRow("Window mode", DisplayOptions.WindowModeNames, DisplayOptions.WindowModeIndex(CurrentMode())),
                             i => data.fullScreenMode = (int)DisplayOptions.WindowModes[i]);
            resolutions = DisplayOptions.Resolutions(Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)), CurrentResolution());
            resolution = Row(scroll, new ChoiceRow("Resolution", resolutions.Select(r => $"{r.x} x {r.y}").ToList(), resolutions.IndexOf(CurrentResolution())),
                             i =>
                             {
                                 data.resolutionWidth = resolutions[i].x;
                                 data.resolutionHeight = resolutions[i].y;
                             });
            vSync = Row(scroll, new ToggleRow("Vertical sync", CurrentVSync()), v =>
            {
                data.vSyncCount = v ? 1 : 0;
                frameCap.SetEnabled(!v);
            });
            frameCap = Row(scroll, new ChoiceRow("Frame cap (vsync off)", DisplayOptions.FrameCapNames, DisplayOptions.FrameCapIndex(data.targetFrameRate)),
                           i => data.targetFrameRate = DisplayOptions.FrameCaps[i]);
            frameCap.SetEnabled(!vSync.Value);
            if (Application.isEditor)
            {
                var note = new Label("Window mode and resolution apply in the built game (the Game view owns them in the Editor).");
                note.AddToClassList("hp-hint");
                scroll.Add(note);
            }

            var footer = new VisualElement();
            footer.AddToClassList("hp-footer");
            panel.Add(footer);
            var reset = Navigable(new Button(ResetToDefaults) { text = "Reset to defaults" });
            reset.AddToClassList("hp-button");
            reset.AddToClassList("hp-button--secondary");
            footer.Add(reset);
            var back = Navigable(new Button(() => Stack.Pop()) { text = "Back" });
            back.AddToClassList("hp-button");
            footer.Add(back);
            return root;
        }

        static void Section(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList("hp-section");
            parent.Add(label);
        }

        // Each row: in the up/down order, scrolled into view when focused, and every change previewed.
        SliderRow Row(ScrollView scroll, SliderRow row, System.Action<float> write)
        {
            AddRow(scroll, row);
            row.Changed += v => Apply(() => write(v));
            return row;
        }

        ToggleRow Row(ScrollView scroll, ToggleRow row, System.Action<bool> write)
        {
            AddRow(scroll, row);
            row.Changed += v => Apply(() => write(v));
            return row;
        }

        ChoiceRow Row(ScrollView scroll, ChoiceRow row, System.Action<int> write)
        {
            AddRow(scroll, row);
            row.Changed += v => Apply(() => write(v));
            return row;
        }

        void AddRow(ScrollView scroll, SettingRow row)
        {
            scroll.Add(Navigable(row));
            row.RegisterCallback<FocusInEvent>(_ => scroll.ScrollTo(row));
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
