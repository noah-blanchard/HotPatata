using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HotPatata
{
    /// <summary>
    /// Speed you can see and hear, driven by the followed player's <see cref="PlayerViewFeel"/> and motor:
    ///  - running stays clean; above run speed anime speed lines grow at the screen edges (a Full Screen Pass
    ///    renderer feature reading the global <c>_HotPatataSpeedLines</c>) and wind streaks fly past the camera;
    ///  - a slide adds a light vignette pulse and a scrape loop;
    ///  - a wind loop that rises in volume and pitch, footsteps (pitched up when sprinting) and landing thumps.
    /// Everything scales with <see cref="GameTuning.viewEffectsStrength"/>. Lives on the camera object.
    /// </summary>
    [RequireComponent(typeof(FirstPersonCamera))]
    public class SpeedEffects : MonoBehaviour
    {
        static readonly int SpeedLinesId = Shader.PropertyToID("_HotPatataSpeedLines");

        [SerializeField] GameTuning tuning;

        [Header("Wind streaks (optional)")]
        [SerializeField, Tooltip("A world-space ParticleSystem prefab with no emission of its own; streaks are emitted from here.")]
        ParticleSystem windStreaksPrefab;
        [SerializeField, Min(0f)] float streaksPerSecond = 90f;

        [Header("Real sounds (optional, from Assets/Audio/SFX; empty = procedural placeholder)")]
        [SerializeField] AudioClip windClip;
        [SerializeField, Tooltip("One is picked at random for each footfall (never the same twice in a row).")] AudioClip[] stepClips;
        [SerializeField] AudioClip landClip;
        [SerializeField, Tooltip("Looped while sliding.")] AudioClip slideClip;

        FirstPersonCamera cam;
        Volume volume;
        VolumeProfile profile;
        Vignette vignette;
        AudioSource wind, foley, scrape;
        AudioClip placeholderStep, placeholderLand;
        PlayerViewFeel subscribed;
        ParticleSystem streaks;
        float streakBudget, lines, slideLevel;
        int lastStepClip = -1;

        void Awake()
        {
            cam = GetComponent<FirstPersonCamera>();

            // Post-processing has to be switched on per camera in URP.
            GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing = true;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0f);
            vignette.smoothness.Override(0.5f);

            var go = new GameObject("SpeedEffectsVolume");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            if (windStreaksPrefab != null)
            {
                streaks = Instantiate(windStreaksPrefab);
                streaks.name = "WindStreaks";
                var emission = streaks.emission;
                emission.enabled = false;   // emitted by hand, around the camera, against the direction of travel
            }

            wind = gameObject.AddComponent<AudioSource>();
            wind.clip = windClip != null ? windClip : ProceduralSfx.Wind();
            wind.loop = true;
            wind.spatialBlend = 0f;
            wind.volume = 0f;
            wind.playOnAwake = false;
            wind.Play();

            scrape = gameObject.AddComponent<AudioSource>();
            scrape.clip = slideClip != null ? slideClip : wind.clip;
            scrape.loop = true;
            scrape.spatialBlend = 0f;
            scrape.volume = 0f;
            scrape.pitch = slideClip != null ? 1f : 2.2f;   // the wind placeholder, pitched up, reads as a scrape
            scrape.playOnAwake = false;
            scrape.Play();

            foley = gameObject.AddComponent<AudioSource>();
            foley.spatialBlend = 0f;
            foley.playOnAwake = false;
        }

        // Real clips when assigned, else placeholders made on first use (survives a script reload in Play Mode).
        AudioClip StepClip()
        {
            if (stepClips != null && stepClips.Length > 0)
            {
                int i = Random.Range(0, stepClips.Length);
                if (stepClips.Length > 1 && i == lastStepClip) i = (i + 1) % stepClips.Length;
                lastStepClip = i;
                if (stepClips[i] != null) return stepClips[i];
            }
            return placeholderStep != null ? placeholderStep : placeholderStep = ProceduralSfx.Step();
        }

        AudioClip LandClip() =>
            landClip != null ? landClip : placeholderLand != null ? placeholderLand : placeholderLand = ProceduralSfx.Land();

        void OnDisable() => Shader.SetGlobalFloat(SpeedLinesId, 0f);

        void OnDestroy()
        {
            Subscribe(null);
            if (profile != null) Destroy(profile);
            if (streaks != null) Destroy(streaks.gameObject);
        }

        void Update()
        {
            var target = cam.Target;
            var feel = target != null ? target.Feel : null;
            Subscribe(feel);

            float dt = Time.deltaTime;
            float strength = tuning != null ? tuning.viewEffectsStrength : 1f;
            float motion = feel != null ? feel.MotionFraction : 0f;
            float over = Mathf.Clamp01((motion - 1.1f) / 1.4f);   // 0 at run speed, 1 at a fast slide
            bool sliding = target != null && target.Motor.IsSliding;

            // Speed lines: none while running, faint at sprint, strong in a fast slide.
            float linesTarget = over * (tuning != null ? tuning.speedLinesStrength : 0.8f) * strength;
            lines = Mathf.Lerp(lines, linesTarget, 1f - Mathf.Exp(-(linesTarget > lines ? 8f : 4f) * dt));
            Shader.SetGlobalFloat(SpeedLinesId, lines);

            // Vignette only as a light pulse while sliding.
            slideLevel = Mathf.MoveTowards(slideLevel, sliding ? 1f : 0f, dt * 5f);
            vignette.intensity.value = slideLevel * (tuning != null ? tuning.speedVignette : 0.22f) * strength;

            EmitStreaks(target, over * (tuning != null ? tuning.windStreaksStrength : 1f) * strength, dt);

            float sf = feel != null ? feel.SpeedFraction : 0f;
            float k = (sf * sf * 0.6f + over * 0.6f) * strength;
            float windMax = tuning != null ? tuning.windVolume : 0.3f;
            wind.volume = Mathf.Lerp(wind.volume, Mathf.Min(1f, k) * windMax, 1f - Mathf.Exp(-6f * dt));
            wind.pitch = 0.85f + sf * 0.4f + over * 0.35f;

            float scrapeTarget = sliding ? FootstepVolume * 1.6f * Mathf.Clamp01(motion / 2f) : 0f;
            scrape.volume = Mathf.MoveTowards(scrape.volume, scrapeTarget, dt * 4f);
        }

        void EmitStreaks(Player target, float amount, float dt)
        {
            if (streaks == null || target == null || amount <= 0.01f) return;
            streakBudget += amount * streaksPerSecond * dt;
            Vector3 velocity = target.Velocity;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.sqrMagnitude < 1f) return;
            Vector3 dir = flat.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir);

            var p = new ParticleSystem.EmitParams();
            while (streakBudget >= 1f)
            {
                streakBudget -= 1f;
                // Spawn ahead of the camera on a ring around the direction of travel (never in the middle of the view).
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(1.6f, 3.6f);
                p.position = transform.position + dir * Random.Range(5f, 9f) + right * ring.x + Vector3.up * ring.y;
                p.velocity = -flat * Random.Range(1.3f, 1.8f);
                p.startLifetime = Random.Range(0.25f, 0.4f);
                p.startSize = Random.Range(0.02f, 0.04f);
                streaks.Emit(p, 1);
            }
        }

        void Subscribe(PlayerViewFeel feel)
        {
            if (feel == subscribed) return;
            if (subscribed != null)
            {
                subscribed.Stepped -= OnStep;
                subscribed.Landed -= OnLand;
            }
            subscribed = feel;
            if (subscribed != null)
            {
                subscribed.Stepped += OnStep;
                subscribed.Landed += OnLand;
            }
        }

        float FootstepVolume => tuning != null ? tuning.footstepVolume : 0.2f;

        void OnStep(float speedFraction)
        {
            float strength = tuning != null ? tuning.viewEffectsStrength : 1f;
            float volume = FootstepVolume * Mathf.Lerp(0.5f, 1f, speedFraction) * Mathf.Max(0.3f, strength);
            if (volume <= 0f) return;
            bool sprinting = cam.Target != null && cam.Target.Motor.IsSprinting;
            foley.pitch = Random.Range(0.92f, 1.08f) * (sprinting ? 1.08f : 1f);
            foley.PlayOneShot(StepClip(), volume * (sprinting ? 1.15f : 1f));
        }

        void OnLand(float impact)
        {
            float volume = Mathf.Min(1f, FootstepVolume * (1f + 2.5f * impact));
            if (volume <= 0f) return;
            foley.pitch = Random.Range(0.85f, 1.0f);
            foley.PlayOneShot(LandClip(), volume);
        }
    }
}
