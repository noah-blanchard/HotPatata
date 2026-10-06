using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (ARCHITECTURE §25.3, spec §19): a PatataWilds checkpoint is a campfire in a clearing, cold until the team
    /// reaches it. It reads <see cref="Checkpoint.Activated"/> (set by the host, mirrored on clients) and nothing else: when it
    /// turns on, the fire catches over <see cref="GameTuning.campfireIgniteSeconds"/> (flames, a burst of embers, a warm light, a
    /// crackle, a column of smoke seen from afar); a checkpoint already reached when this first looks (a practice start, a late
    /// join) is simply burning; a new run puts it out. Lit and cold differ by shape and motion (fire, smoke), never by colour
    /// alone. The burst and the flicker honour flash reduction. With no checkpoint it is the summit beacon: lit when the run is
    /// complete.
    /// </summary>
    public class CampfirePresentation : MonoBehaviour
    {
        [SerializeField] GameTuning tuning;
        [SerializeField, Tooltip("The checkpoint this fire marks; empty = the summit beacon (lit at the finish).")] Checkpoint checkpoint;
        [SerializeField] ParticleSystem flames;
        [SerializeField] ParticleSystem embers;
        [SerializeField] ParticleSystem smoke;
        [SerializeField] Light fireLight;
        [SerializeField] AudioSource crackle;
        [SerializeField, Tooltip("Scales the light (the beacon is bigger).")] float size = 1f;

        float flamesRate, embersRate, smokeRate;
        bool known, lit;
        float lighting;   // 0 cold .. 1 burning

        public bool Lit => lit;
        public float Lighting => lighting;
        public Checkpoint Checkpoint => checkpoint;

        void Awake()
        {
            if (flames != null) flamesRate = flames.emission.rateOverTimeMultiplier;
            if (embers != null) embersRate = embers.emission.rateOverTimeMultiplier;
            if (smoke != null) smokeRate = smoke.emission.rateOverTimeMultiplier;
            Show(0f);
            if (crackle != null) crackle.Stop();
        }

        bool ShouldBurn()
        {
            if (checkpoint != null) return checkpoint.Activated;
            var run = RunManager.Instance;
            return run != null && run.State == RunState.Completed;
        }

        float FlashReduction => tuning != null ? Settings.FlashReduction(tuning) : 0f;

        void Update()
        {
            bool burn = ShouldBurn();
            if (!known)
            {
                known = true;
                lit = burn;
                lighting = burn ? 1f : 0f;
                if (burn) StartFire(false);
                Show(lighting);
                return;
            }
            if (burn != lit)
            {
                lit = burn;
                if (burn) StartFire(true);
                else
                {
                    lighting = 0f;
                    foreach (var ps in new[] { flames, embers, smoke }) if (ps != null) ps.Clear(true);
                    if (crackle != null) crackle.Stop();
                }
            }
            if (lit && lighting < 1f)
            {
                float seconds = tuning != null ? tuning.campfireIgniteSeconds : 1.4f;
                lighting = seconds <= 0f ? 1f : Mathf.Min(1f, lighting + Time.deltaTime / seconds);
            }
            Show(lighting);
        }

        void StartFire(bool burst)
        {
            foreach (var ps in new[] { flames, embers, smoke }) if (ps != null && !ps.isPlaying) ps.Play(true);
            if (burst && embers != null)
            {
                int count = Mathf.RoundToInt((tuning != null ? tuning.campfireIgniteBurst : 40) * (1f - FlashReduction));
                if (count > 0) embers.Emit(count);
            }
            if (crackle != null && crackle.clip != null && !crackle.isPlaying) crackle.Play();
        }

        void Show(float amount)
        {
            if (flames != null) { var e = flames.emission; e.rateOverTimeMultiplier = flamesRate * amount; }
            if (embers != null) { var e = embers.emission; e.rateOverTimeMultiplier = embersRate * amount; }
            if (smoke != null) { var e = smoke.emission; e.rateOverTimeMultiplier = smokeRate * amount; }
            if (crackle != null) crackle.volume = amount;
            if (fireLight != null)
            {
                float intensity = (tuning != null ? tuning.campfireLightIntensity : 4.5f) * size;
                float flicker = (tuning != null ? tuning.campfireFlicker : 0.18f) * (1f - FlashReduction);
                float n = Mathf.PerlinNoise(Time.time * 7.3f, transform.position.x * 0.13f) - 0.5f;
                fireLight.intensity = intensity * amount * (1f + flicker * 2f * n);
                fireLight.range = (tuning != null ? tuning.campfireLightRange : 14f) * size;
                fireLight.enabled = amount > 0.001f;
            }
        }

        /// <summary>Editor builders: wires the fire (NatureKit).</summary>
        public void Configure(GameTuning gameTuning, Checkpoint marks, ParticleSystem flameSystem, ParticleSystem emberSystem, ParticleSystem smokeSystem,
                              Light light, AudioSource sound, float scale)
        {
            tuning = gameTuning;
            checkpoint = marks;
            flames = flameSystem;
            embers = emberSystem;
            smoke = smokeSystem;
            fireLight = light;
            crackle = sound;
            size = scale;
        }
    }
}
