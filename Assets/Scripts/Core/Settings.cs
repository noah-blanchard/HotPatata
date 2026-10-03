using System;
using System.IO;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// One player's own settings (spec §19, M6.5), saved as JSON on this machine. They override the designer values in
    /// <see cref="GameTuning"/>, which is shared and must never be written at runtime (in the Editor that would change
    /// the asset). <see cref="Sanitize"/> keeps a hand-edited or old file in range.
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        public const int Unset = -1;

        // Slider limits. The gameplay defaults are not repeated here: they come from GameTuning (FromTuning).
        public const float MinMouseSensitivity = 0.005f, MaxMouseSensitivity = 2f;
        public const float MinStickLookSpeed = 10f, MaxStickLookSpeed = 1000f;
        public const float MinFieldOfView = 40f, MaxFieldOfView = 110f;   // same as GameTuning.fieldOfView's range
        public const int MinFrameRate = 30;

        // Accessibility
        public float viewEffectsStrength;
        public float flashReduction;
        public float beepVolume;

        // Look
        public float mouseSensitivity;
        public float stickLookSpeed;
        public bool invertY;
        public float fieldOfView;

        // Controls: InputActionAsset.SaveBindingOverridesAsJson of HotPatataControls ("" = authored bindings), see InputRebinding
        public string bindingOverrides = "";

        // Audio: master is AudioListener.volume (every sound); the others are groups of the HotPatataMixer (AudioVolumes)
        public float masterVolume = 1f;
        public float sfxVolume = 1f;

        // Display: Unset leaves the current value alone
        public int fullScreenMode = Unset;   // UnityEngine.FullScreenMode
        public int resolutionWidth = Unset;
        public int resolutionHeight = Unset;
        public int vSyncCount = Unset;       // 0 = off, 1 = every frame
        public int targetFrameRate = Unset;  // frame cap with vsync off; Unset (-1) = no cap (the platform default)

        /// <summary>First launch (and any field missing from an older file): the designer values from the tuning asset.</summary>
        public static SettingsData FromTuning(GameTuning t)
        {
            var s = new SettingsData();
            s.viewEffectsStrength = t.viewEffectsStrength;
            s.flashReduction = t.flashReduction;
            s.beepVolume = t.beepVolume;
            s.mouseSensitivity = t.mouseSensitivity;
            s.stickLookSpeed = t.stickLookSpeed;
            s.fieldOfView = t.fieldOfView;
            return s;
        }

        public SettingsData Clone() => (SettingsData)MemberwiseClone();

        /// <summary>Clamps every value into the supported range; a broken number (NaN, infinity) takes the default.</summary>
        public void Sanitize(SettingsData defaults)
        {
            viewEffectsStrength = Clamp(viewEffectsStrength, 0f, 1f, defaults.viewEffectsStrength);
            flashReduction = Clamp(flashReduction, 0f, 1f, defaults.flashReduction);
            beepVolume = Clamp(beepVolume, 0f, 1f, defaults.beepVolume);
            mouseSensitivity = Clamp(mouseSensitivity, MinMouseSensitivity, MaxMouseSensitivity, defaults.mouseSensitivity);
            stickLookSpeed = Clamp(stickLookSpeed, MinStickLookSpeed, MaxStickLookSpeed, defaults.stickLookSpeed);
            fieldOfView = Clamp(fieldOfView, MinFieldOfView, MaxFieldOfView, defaults.fieldOfView);
            masterVolume = Clamp(masterVolume, 0f, 1f, defaults.masterVolume);
            sfxVolume = Clamp(sfxVolume, 0f, 1f, defaults.sfxVolume);
            bindingOverrides ??= "";

            if (fullScreenMode != Unset && !Enum.IsDefined(typeof(FullScreenMode), fullScreenMode)) fullScreenMode = Unset;
            if (resolutionWidth <= 0 || resolutionHeight <= 0) resolutionWidth = resolutionHeight = Unset;
            if (vSyncCount != Unset) vSyncCount = Mathf.Clamp(vSyncCount, 0, 1);
            if (targetFrameRate != Unset && targetFrameRate < MinFrameRate) targetFrameRate = Unset;
        }

        static float Clamp(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }

    /// <summary>
    /// Access point for <see cref="SettingsData"/>. Loaded once at the game's entry (<see cref="BootstrapEntry"/>).
    /// Gameplay systems read through the accessors below, never the tuning fields they shadow.
    /// Until then (tests, a gameplay scene played directly in the Editor) and when nothing was ever saved,
    /// <see cref="Current"/> is null and every accessor returns the <see cref="GameTuning"/> value, so the
    /// designer values stay the defaults.
    /// </summary>
    public static class Settings
    {
        const string FileName = "settings.json";

        public static SettingsData Current { get; private set; }

        /// <summary>Raised after <see cref="Save"/>, so systems that cache a value can refresh.</summary>
        public static event Action Changed;

        /// <summary>The folder of settings.json (default: persistentDataPath). Tests point it at a temporary folder so they never touch the player's file.</summary>
        public static string Folder { get; set; }

        static string FilePath => Path.Combine(Folder ?? Application.persistentDataPath, FileName);

        /// <summary>True once <see cref="LoadOnce"/> has read the file in this play session.</summary>
        static bool loaded;

        // Enter Play Mode Options may skip the domain reload: never carry settings from one play session (or a
        // Bootstrap run) into the next, e.g. into tests that expect the tuning values.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            Changed = null;
            loaded = false;
            Folder = null;
        }

        // ------------------------------------------------------------------ reads (fall back to the tuning asset)

        public static float ViewEffectsStrength(GameTuning t) => Current != null ? Current.viewEffectsStrength : t.viewEffectsStrength;
        public static float FlashReduction(GameTuning t) => Current != null ? Current.flashReduction : t.flashReduction;
        public static float BeepVolume(GameTuning t) => Current != null ? Current.beepVolume : t.beepVolume;
        public static float MouseSensitivity(GameTuning t) => Current != null ? Current.mouseSensitivity : t.mouseSensitivity;
        public static float StickLookSpeed(GameTuning t) => Current != null ? Current.stickLookSpeed : t.stickLookSpeed;
        public static float FieldOfView(GameTuning t) => Current != null ? Current.fieldOfView : t.fieldOfView;
        public static bool InvertY => Current != null && Current.invertY;
        public static float SfxVolume => Current != null ? Current.sfxVolume : 1f;

        // ------------------------------------------------------------------ load / save

        /// <summary>Game entry: <see cref="Load"/> the first time only (the Bootstrap scene is reloaded after each game).</summary>
        public static void LoadOnce(GameTuning t)
        {
            if (loaded) return;
            loaded = true;
            Load(t);
        }

        /// <summary>Reads the saved file (if any) and applies the global parts (audio, display).</summary>
        public static void Load(GameTuning t)
        {
            try
            {
                Current = File.Exists(FilePath) ? Parse(File.ReadAllText(FilePath), t) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] could not read {FilePath}, using defaults: {e.Message}");
                Current = null;
            }
            if (Current != null) ApplyGlobal(Current);
        }

        /// <summary>A copy to edit in a settings screen: the saved values, or the tuning defaults on first launch.</summary>
        public static SettingsData Editable(GameTuning t) => (Current ?? SettingsData.FromTuning(t)).Clone();

        /// <summary>
        /// Makes <paramref name="data"/> the active settings and applies it, without writing the file: a settings
        /// screen previews every change live (a slider moves many times a second) and calls <see cref="Save"/> once.
        /// </summary>
        public static void Preview(SettingsData data, GameTuning t)
        {
            var copy = data.Clone();
            copy.Sanitize(SettingsData.FromTuning(t));
            Current = copy;
            ApplyGlobal(copy);
            Changed?.Invoke();
        }

        /// <summary>Makes <paramref name="data"/> the active settings, applies it and writes it to disk.</summary>
        public static void Save(SettingsData data, GameTuning t)
        {
            Preview(data, t);
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] could not write {FilePath}: {e.Message}");
            }
        }

        /// <summary>Forgets the saved settings: everything returns to the tuning defaults.</summary>
        public static void ResetToDefaults()
        {
            Current = null;
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] could not delete {FilePath}: {e.Message}");
            }
            AudioListener.volume = 1f;
            Changed?.Invoke();
        }

        /// <summary>Pure: JSON to sanitised settings (unit tested). Fields missing from the file keep the tuning defaults.</summary>
        public static SettingsData Parse(string json, GameTuning t)
        {
            var defaults = SettingsData.FromTuning(t);
            var data = defaults.Clone();
            if (!string.IsNullOrWhiteSpace(json)) JsonUtility.FromJsonOverwrite(json, data);
            data.Sanitize(defaults);
            return data;
        }

        static void ApplyGlobal(SettingsData s)
        {
            AudioListener.volume = s.masterVolume;

            if (s.vSyncCount != SettingsData.Unset) QualitySettings.vSyncCount = s.vSyncCount;
            Application.targetFrameRate = s.targetFrameRate;   // Unset (-1) is Unity's own "no cap"

            if (Application.isEditor) return;   // the Game view owns resolution and window mode in the Editor
            // Only when something differs: Preview runs on every slider step, and a needless mode switch flickers.
            var mode = s.fullScreenMode != SettingsData.Unset ? (FullScreenMode)s.fullScreenMode : Screen.fullScreenMode;
            if (s.resolutionWidth != SettingsData.Unset)
            {
                if (Screen.width != s.resolutionWidth || Screen.height != s.resolutionHeight || Screen.fullScreenMode != mode)
                    Screen.SetResolution(s.resolutionWidth, s.resolutionHeight, mode);
            }
            else if (Screen.fullScreenMode != mode)
            {
                Screen.fullScreenMode = mode;
            }
        }
    }
}
