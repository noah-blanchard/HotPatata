using UnityEngine;
using UnityEngine.Audio;

namespace HotPatata
{
    /// <summary>
    /// Drives the HotPatataMixer's exposed volumes from the player's <see cref="Settings"/> (M6.5). The master volume is
    /// <c>AudioListener.volume</c> (applied by <see cref="Settings"/>, so it reaches every sound); each mixer group
    /// (SFX now, music and UI later) gets its own slider here. Lives on the persistent AppRoot.
    /// </summary>
    public class AudioVolumes : MonoBehaviour
    {
        const string SfxParameter = "SfxVolume";
        const float SilentDb = -80f;   // the mixer's floor

        [SerializeField] AudioMixer mixer;

        // AudioMixer.SetFloat is ignored before Start, so the first apply waits for it.
        void Start()
        {
            Settings.Changed += Apply;
            Apply();
        }

        void OnDestroy() => Settings.Changed -= Apply;

        void Apply()
        {
            if (mixer != null) mixer.SetFloat(SfxParameter, ToDecibels(Settings.SfxVolume));
        }

        /// <summary>Slider 0..1 to mixer decibels: 1 = 0 dB, 0 = silent, perceptually even in between.</summary>
        public static float ToDecibels(float linear) =>
            linear <= 0.0001f ? SilentDb : Mathf.Max(SilentDb, 20f * Mathf.Log10(linear));
    }
}
