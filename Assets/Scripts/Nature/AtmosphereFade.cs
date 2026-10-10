using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (ARCHITECTURE §25.3, PatataCanopy): scales a particle system's emission by the time of day shown
    /// (<see cref="TimeOfDayBlender.Current"/>, in moments: 0 dawn .. 4 dusk), so the fireflies come out from sunset. Particles already
    /// alive live out their (short) lives: nothing pops, nothing flashes. It reads the day and never touches a rule.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class AtmosphereFade : MonoBehaviour
    {
        [SerializeField, Tooltip("Emission multiplier by the time of day (x: 0 dawn .. 4 dusk).")]
        AnimationCurve byTime = AnimationCurve.Linear(0f, 1f, 4f, 1f);

        ParticleSystem system;
        float rate;
        TimeOfDayBlender day;

        void OnEnable()
        {
            system = GetComponent<ParticleSystem>();
            rate = system.emission.rateOverTimeMultiplier;
            day = FindFirstObjectByType<TimeOfDayBlender>();
            Apply();
        }

        void OnDisable()
        {
            if (system == null) return;
            var emission = system.emission;
            emission.rateOverTimeMultiplier = rate;
        }

        void Update() => Apply();

        void Apply()
        {
            var emission = system.emission;
            emission.rateOverTimeMultiplier = rate * Mathf.Max(0f, byTime.Evaluate(day != null ? day.Current : 0f));
        }

        /// <summary>Editor builders: the curve.</summary>
        public void Configure(AnimationCurve curve) => byTime = curve;
    }
}
