using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HotPatata
{
    /// <summary>
    /// Speed you can see, driven by the followed player's <see cref="PlayerViewFeel"/> and motor:
    ///  - running stays clean; above run speed anime speed lines grow at the screen edges (a Full Screen Pass
    ///    renderer feature reading the global <c>_HotPatataSpeedLines</c>) and wind streaks fly past the camera;
    ///  - a slide adds a light vignette pulse.
    /// No movement sounds (removed on purpose: no footsteps, landings, wind or slide scrape).
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

        FirstPersonCamera cam;
        Volume volume;
        VolumeProfile profile;
        Vignette vignette;
        ParticleSystem streaks;
        float streakBudget, lines, slideLevel;

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
        }

        void OnDisable() => Shader.SetGlobalFloat(SpeedLinesId, 0f);

        void OnDestroy()
        {
            if (profile != null) Destroy(profile);
            if (streaks != null) Destroy(streaks.gameObject);
        }

        void Update()
        {
            var target = cam.Target;
            var feel = target != null ? target.Feel : null;

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
    }
}
