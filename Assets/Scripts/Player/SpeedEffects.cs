using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Beep
{
    /// <summary>
    /// Speed you can see and hear, driven by the followed player's <see cref="PlayerViewFeel"/>:
    /// a runtime post-processing volume (vignette, chromatic aberration, a touch of lens distortion) that
    /// grows with speed squared so walking stays clean, a wind loop that rises in volume and pitch, plus
    /// footsteps and landing thumps. Everything scales with <see cref="GameTuning.viewEffectsStrength"/>.
    /// Lives on the camera object; nothing to author in the scene.
    /// </summary>
    [RequireComponent(typeof(FirstPersonCamera))]
    public class SpeedEffects : MonoBehaviour
    {
        [SerializeField] GameTuning tuning;

        FirstPersonCamera cam;
        Volume volume;
        VolumeProfile profile;
        Vignette vignette;
        ChromaticAberration chromatic;
        LensDistortion lens;
        AudioSource wind, foley;
        AudioClip stepClip, landClip;
        PlayerViewFeel subscribed;

        void Awake()
        {
            cam = GetComponent<FirstPersonCamera>();

            // Post-processing has to be switched on per camera in URP.
            GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing = true;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignette = profile.Add<Vignette>(true);
            chromatic = profile.Add<ChromaticAberration>(true);
            lens = profile.Add<LensDistortion>(true);
            vignette.intensity.Override(0f);
            vignette.smoothness.Override(0.5f);
            chromatic.intensity.Override(0f);
            lens.intensity.Override(0f);

            var go = new GameObject("SpeedEffectsVolume");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            wind = gameObject.AddComponent<AudioSource>();
            wind.clip = ProceduralSfx.Wind();
            wind.loop = true;
            wind.spatialBlend = 0f;
            wind.volume = 0f;
            wind.playOnAwake = false;
            wind.Play();

            foley = gameObject.AddComponent<AudioSource>();
            foley.spatialBlend = 0f;
            foley.playOnAwake = false;
            stepClip = ProceduralSfx.Step();
            landClip = ProceduralSfx.Land();
        }

        void OnDestroy()
        {
            Subscribe(null);
            if (profile != null) Destroy(profile);
        }

        void Update()
        {
            var feel = cam.Target != null ? cam.Target.Feel : null;
            Subscribe(feel);

            float speed = feel != null ? feel.SpeedFraction : 0f;
            float strength = tuning != null ? tuning.viewEffectsStrength : 1f;
            float k = speed * speed * strength;   // effects only really show when running fast

            vignette.intensity.value = k * (tuning != null ? tuning.speedVignette : 0.28f);
            chromatic.intensity.value = k * 0.22f;
            lens.intensity.value = -k * 0.15f;

            float windMax = tuning != null ? tuning.windVolume : 0.3f;
            wind.volume = Mathf.Lerp(wind.volume, k * windMax, 1f - Mathf.Exp(-6f * Time.deltaTime));
            wind.pitch = 0.85f + speed * 0.5f;
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

        void OnStep(float speedFraction)
        {
            float strength = tuning != null ? tuning.viewEffectsStrength : 1f;
            foley.pitch = Random.Range(0.9f, 1.1f);
            foley.PlayOneShot(stepClip, (0.15f + 0.25f * speedFraction) * Mathf.Max(0.3f, strength));
        }

        void OnLand(float impact)
        {
            foley.pitch = Random.Range(0.92f, 1.05f);
            foley.PlayOneShot(landClip, 0.25f + 0.6f * impact);
        }
    }
}
