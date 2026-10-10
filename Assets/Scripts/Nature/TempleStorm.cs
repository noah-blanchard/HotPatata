using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (ARCHITECTURE §25.4, PatataTemple's last act): the tropical storm. Its rain follows the camera (the drops
    /// die on roofs and floors, so it never rains indoors), and its lightning lights the scene for a moment now and then. Both grow
    /// with the time of day shown (<see cref="TimeOfDayBlender.Current"/>: from the heavy afternoon to the storm). Every flash is
    /// scaled by <c>flashReduction</c> and <c>viewEffectsStrength</c> (spec §19; at full reduction there is none) and rises and
    /// falls over a quarter of a second, never a strobe. No wind: nothing bends a flight (§17.2). It reads the day and touches no rule.
    /// </summary>
    public class TempleStorm : MonoBehaviour
    {
        [SerializeField] GameTuning tuning;
        [SerializeField] ParticleSystem rain;
        [SerializeField, Tooltip("A directional light, off between flashes (no shadows).")] Light lightning;
        [SerializeField, Tooltip("Storm strength by the time of day (x: 0 dawn .. 4 storm).")]
        AnimationCurve byTime = AnimationCurve.Linear(3f, 0f, 4f, 1f);
        [SerializeField, Min(0f)] float flashIntensity = 2.2f;
        [SerializeField, Tooltip("Seconds between two flashes at full storm (randomised by half).")] float flashInterval = 14f;
        [SerializeField, Min(0.05f)] float flashSeconds = 0.6f;

        TimeOfDayBlender day;
        float rate, nextFlash, flashStart = -100f;
        readonly System.Random random = new System.Random(4242);

        void OnEnable()
        {
            day = FindFirstObjectByType<TimeOfDayBlender>();
            if (rain != null) rate = rain.emission.rateOverTimeMultiplier;
            nextFlash = Time.time + flashInterval;
            if (lightning != null) lightning.intensity = 0f;
        }

        void LateUpdate()
        {
            float storm = Mathf.Clamp01(byTime.Evaluate(day != null ? day.Current : 0f));
            var cam = Camera.main;
            if (rain != null)
            {
                if (cam != null) rain.transform.position = cam.transform.position + Vector3.up * 14f;
                var emission = rain.emission;
                emission.rateOverTimeMultiplier = rate * storm;
            }
            if (lightning == null || tuning == null) return;
            if (storm > 0.2f && Time.time >= nextFlash)
            {
                flashStart = Time.time;
                nextFlash = Time.time + flashInterval * (0.5f + (float)random.NextDouble()) / storm;
            }
            float strength = flashIntensity * storm * Settings.ViewEffectsStrength(tuning) * (1f - Settings.FlashReduction(tuning));
            lightning.intensity = strength * Pulse(Time.time - flashStart, flashSeconds);
        }

        /// <summary>
        /// The flash's shape at <paramref name="t"/> seconds into it: a quarter-second rise, a double flicker, a slow fall; 0 outside.
        /// Pure (EditMode tested): never a step from dark to full.
        /// </summary>
        public static float Pulse(float t, float duration)
        {
            if (t < 0f || t > duration) return 0f;
            float k = t / duration;
            float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.4f)) * (1f - Mathf.SmoothStep(0.4f, 1f, k));
            return envelope * (0.75f + 0.25f * Mathf.Cos(k * Mathf.PI * 6f));
        }

        /// <summary>Editor builders: the references and the curve.</summary>
        public void Configure(GameTuning t, ParticleSystem rainSystem, Light flash, AnimationCurve curve)
        {
            tuning = t;
            rain = rainSystem;
            lightning = flash;
            byTime = curve;
        }
    }
}
