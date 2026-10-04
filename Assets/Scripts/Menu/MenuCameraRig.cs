using Unity.Cinemachine;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The menu camera (ARCHITECTURE §6.2): the one component that decides where it looks. Each station has a Cinemachine
    /// camera spot; <see cref="Travel"/> gives the target spot the priority and the brain flies there with an eased blend
    /// (<see cref="GameTuning.menuTravelSeconds"/>, unscaled time: the menu is offline). Camera effects at 0 (spec §19,
    /// <see cref="Settings.ViewEffectsStrength"/>) make it a cut: no swoop, and nothing flashes. The station's framing
    /// (a vignette, <see cref="MenuStation.Focus"/>) takes the camera-effects strength as its weight, so it is off at 0. Presentation only.
    /// </summary>
    [RequireComponent(typeof(CinemachineBrain))]
    public class MenuCameraRig : MonoBehaviour
    {
        const int Live = 10, Idle = 0;

        [SerializeField] GameTuning tuning;

        CinemachineBrain brain;
        MenuStation current;

        public void Configure(GameTuning gameTuning) => tuning = gameTuning;

        /// <summary>The station the camera is at or flying to.</summary>
        public MenuStation Current => current;

        /// <summary>True while the camera flies between two stations.</summary>
        public bool IsTravelling => Brain.IsBlending;

        CinemachineBrain Brain
        {
            get
            {
                if (brain != null) return brain;
                brain = GetComponent<CinemachineBrain>();
                brain.IgnoreTimeScale = true;
                return brain;
            }
        }

        /// <summary>
        /// The blend between two stations: eased over <paramref name="seconds"/>, or a cut when the camera effects are off
        /// (<paramref name="viewEffects"/> 0) or the move is instant.
        /// </summary>
        public static CinemachineBlendDefinition BlendFor(float viewEffects, float seconds) =>
            viewEffects <= 0f || seconds <= 0f
                ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
                : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, seconds);

        /// <summary>Flies to <paramref name="station"/> (or cuts there when <paramref name="instant"/>, e.g. when the menu appears).</summary>
        public void Travel(MenuStation station, bool instant = false)
        {
            if (station == null || station.Spot == null) return;
            float strength = tuning != null ? Settings.ViewEffectsStrength(tuning) : 1f;
            float seconds = instant || tuning == null ? 0f : tuning.menuTravelSeconds;
            Brain.DefaultBlend = BlendFor(strength, seconds);
            foreach (var s in MenuStation.All)
            {
                if (s.Spot != null) s.Spot.Priority = s == station ? Live : Idle;
                if (s.Focus != null) s.Focus.weight = s == station ? FocusWeight(strength) : 0f;
            }
            current = station;
        }

        /// <summary>The station's vignette follows the camera-effects setting (0 = off).</summary>
        public static float FocusWeight(float viewEffects) => Mathf.Clamp01(viewEffects);

        void LateUpdate()
        {
            // A settings change applies at once, not only on the next move.
            if (current == null || current.Focus == null) return;
            current.Focus.weight = FocusWeight(tuning != null ? Settings.ViewEffectsStrength(tuning) : 1f);
        }
    }
}
