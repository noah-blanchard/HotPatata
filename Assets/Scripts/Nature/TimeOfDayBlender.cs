using UnityEngine;
using UnityEngine.Rendering;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (ARCHITECTURE §25.3): PatataWilds' day moves from dawn to dusk as the team climbs. The time of day is a
    /// function of the current checkpoint alone (<see cref="RunManager.CurrentCheckpoint"/>, decided by the host and mirrored on
    /// clients), so every machine shows the same sky; a section reset keeps the checkpoint, so it never changes the light. A step
    /// forward (a new checkpoint) blends over <see cref="GameTuning.timeOfDayBlendSeconds"/> per act; a step back (a restart) or a
    /// big jump (a practice start, a late join) is shown at once. The presets are authored by <c>LookBuilder</c>; this only
    /// interpolates them: the sun, the sky fill, the haze, the height mist and the light shafts (through the scene's
    /// <see cref="MistField"/>), the two-cubemap sky, the grade volumes' weights, the plants' wind and the sky the surfaces
    /// reflect. It never touches a rule.
    /// </summary>
    public class TimeOfDayBlender : MonoBehaviour
    {
        [SerializeField] GameTuning tuning;
        [SerializeField, Tooltip("Dawn first; consecutive presets are blended (written by LookBuilder).")] TimeOfDayPreset[] presets;
        [SerializeField, Tooltip("The time of day at each checkpoint id (0 = the start), in presets (1 = one act later).")] float[] checkpointTimes;
        [SerializeField] Light sun;
        [SerializeField, Tooltip("One global volume per preset, holding its grade (weights blended here).")] Volume[] grades;
        [SerializeField, Tooltip("The scene's sky material (HotPatata/SkyBlend); a runtime copy is what changes.")] Material skyTemplate;
        [SerializeField, Tooltip("World XZ direction the wind blows towards.")] Vector2 windDirection = new Vector2(0.8f, 0.6f);
        [SerializeField, Tooltip("Gust speed of the swaying plants.")] float windGustSpeed = 1f;

        static readonly int SkyA = Shader.PropertyToID("_SkyA"), SkyB = Shader.PropertyToID("_SkyB"), ExposureA = Shader.PropertyToID("_ExposureA"),
            ExposureB = Shader.PropertyToID("_ExposureB"), RotationA = Shader.PropertyToID("_RotationA"), RotationB = Shader.PropertyToID("_RotationB"),
            Blend = Shader.PropertyToID("_Blend"), Wind = Shader.PropertyToID("_HP_Wind");

        Material sky;
        bool started;
        Texture reflection;

        /// <summary>The time of day shown now, in presets (0 = dawn).</summary>
        public float Current { get; private set; }

        public float Target => TargetFor(checkpointTimes, CurrentCheckpointId());

        public System.Collections.Generic.IReadOnlyList<TimeOfDayPreset> Presets => presets;

        void OnEnable()
        {
            started = false;
            if (skyTemplate != null)
            {
                sky = new Material(skyTemplate) { name = skyTemplate.name + " (runtime)" };
                RenderSettings.skybox = sky;
            }
        }

        void OnDisable()
        {
            Shader.SetGlobalVector(Wind, Vector4.zero);
            if (sky != null) Destroy(sky);
            sky = null;
        }

        static int CurrentCheckpointId()
        {
            var run = RunManager.Instance;
            return run != null && run.CurrentCheckpoint != null ? run.CurrentCheckpoint.Id : 0;
        }

        void Update()
        {
            if (presets == null || presets.Length == 0) return;
            float target = Target;
            float next = started ? Step(Current, target, Time.deltaTime, tuning != null ? tuning.timeOfDayBlendSeconds : 24f, tuning != null ? tuning.timeOfDaySnapActs : 0.6f) : target;
            if (!started || !Mathf.Approximately(next, Current))
            {
                started = true;
                Current = next;
                Apply(Current);
            }
            ApplyMist(Current);
        }

        /// <summary>The height mist and the light shafts of the moment, every frame (the field may have woken after the blender).</summary>
        void ApplyMist(float t)
        {
            var field = MistField.Active;
            if (field == null) return;
            var (i, f) = Segment(presets.Length, t);
            var a = presets[i];
            var b = presets[Mathf.Min(i + 1, presets.Length - 1)];
            field.Push(Color.Lerp(a.mistColor, b.mistColor, f), Mathf.Lerp(a.mistDensity, b.mistDensity, f), Mathf.Lerp(a.mistFalloff, b.mistFalloff, f),
                       Mathf.Lerp(a.mistGlow, b.mistGlow, f), Mathf.Lerp(a.shaftStrength, b.shaftStrength, f));
        }

        /// <summary>The time of day at checkpoint <paramref name="id"/> (clamped to the table; 0 when there is none).</summary>
        public static float TargetFor(float[] times, int id)
        {
            if (times == null || times.Length == 0) return 0f;
            return times[Mathf.Clamp(id, 0, times.Length - 1)];
        }

        /// <summary>One frame towards <paramref name="target"/>: forward at one act per <paramref name="blendSeconds"/>; back, or further than <paramref name="snapActs"/>, at once.</summary>
        public static float Step(float current, float target, float dt, float blendSeconds, float snapActs)
        {
            if (target < current - 1e-4f || Mathf.Abs(target - current) > snapActs) return target;
            return Mathf.MoveTowards(current, target, dt / Mathf.Max(0.01f, blendSeconds));
        }

        /// <summary>The two presets around <paramref name="t"/> and how far between them.</summary>
        public static (int index, float blend) Segment(int count, float t)
        {
            if (count <= 1) return (0, 0f);
            t = Mathf.Clamp(t, 0f, count - 1);
            int i = Mathf.Min(Mathf.FloorToInt(t), count - 2);
            return (i, t - i);
        }

        void Apply(float t)
        {
            var (i, f) = Segment(presets.Length, t);
            var a = presets[i];
            var b = presets[Mathf.Min(i + 1, presets.Length - 1)];
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(Mathf.LerpAngle(a.sunElevation, b.sunElevation, f), Mathf.LerpAngle(a.sunYaw, b.sunYaw, f), 0f);
                sun.color = Color.Lerp(a.sunColor, b.sunColor, f);
                sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, f);
                sun.shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, f);
            }
            RenderSettings.ambientSkyColor = Color.Lerp(a.ambientSky, b.ambientSky, f);
            RenderSettings.ambientEquatorColor = Color.Lerp(a.ambientEquator, b.ambientEquator, f);
            RenderSettings.ambientGroundColor = Color.Lerp(a.ambientGround, b.ambientGround, f);
            RenderSettings.fogColor = Color.Lerp(a.fogColor, b.fogColor, f);
            RenderSettings.fogStartDistance = Mathf.Lerp(a.fogStart, b.fogStart, f);
            RenderSettings.fogEndDistance = Mathf.Lerp(a.fogEnd, b.fogEnd, f);
            if (sky != null)
            {
                sky.SetTexture(SkyA, a.sky);
                sky.SetTexture(SkyB, b.sky);
                sky.SetFloat(ExposureA, a.skyExposure);
                sky.SetFloat(ExposureB, b.skyExposure);
                sky.SetFloat(RotationA, a.skyRotation);
                sky.SetFloat(RotationB, b.skyRotation);
                sky.SetFloat(Blend, f);
            }
            var nearest = f < 0.5f ? a.sky : b.sky;
            if (nearest != null && nearest != reflection)
            {
                reflection = nearest;
                RenderSettings.customReflectionTexture = nearest;
            }
            if (grades != null)
                for (int k = 0; k < grades.Length; k++)
                    if (grades[k] != null) grades[k].weight = k == i ? 1f - f : k == i + 1 ? f : 0f;
            var dir = windDirection.sqrMagnitude > 0f ? windDirection.normalized : Vector2.right;
            Shader.SetGlobalVector(Wind, new Vector4(dir.x, dir.y, Mathf.Lerp(a.wind, b.wind, f), windGustSpeed));
        }

        /// <summary>Editor builders: wires the blender (LookBuilder).</summary>
        public void Configure(GameTuning gameTuning, TimeOfDayPreset[] dayPresets, float[] times, Light sceneSun, Volume[] gradeVolumes, Material skyMaterial)
        {
            tuning = gameTuning;
            presets = dayPresets;
            checkpointTimes = times;
            sun = sceneSun;
            grades = gradeVolumes;
            skyTemplate = skyMaterial;
        }
    }
}
