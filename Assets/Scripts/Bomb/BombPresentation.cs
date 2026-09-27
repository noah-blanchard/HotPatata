using System.Collections;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Emissive pulse that mirrors the beep, catch pop, and a placeholder explosion flash. The visual also moves
    /// naturally: it tumbles in flight (axis and speed slightly random, scaled by throw speed), then settles into a
    /// gentle sway in the hand with a small jolt on every catch. Rotation is cosmetic only and never touches physics.
    /// Urgency is carried by pulse rate, brightness AND size, not by colour alone. Reads bomb state only.
    /// VFX: the wick throws sparks faster as the fuse burns down; a thrown potato leaves a tapered trail and smoke
    /// puffs (wider for harder throws, warmer as the fuse runs out); the explosion is a pooled <see cref="ExplosionFx"/>.
    /// </summary>
    [RequireComponent(typeof(BombController), typeof(BombAudio))]
    public class BombPresentation : MonoBehaviour
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Renderer bodyRenderer;
        [SerializeField] Transform visual;
        [SerializeField] Material explosionMaterial;
        [SerializeField] Color baseEmission = new Color(1f, 0.25f, 0.05f);
        [SerializeField] Color criticalEmission = new Color(1f, 0.9f, 0.7f);
        [SerializeField, Tooltip("Overall emission multiplier: the pulse should glow through bloom, not white the potato out.")]
        float emissionScale = 0.3f;

        [Header("VFX (optional)")]
        [SerializeField, Tooltip("Sparks at the wick tip; rate per fuse stage from GameTuning.fuseSparkRates.")]
        ParticleSystem fuseSparks;
        [SerializeField] TrailRenderer trail;
        [SerializeField, Tooltip("Smoke puffs left behind in flight (rate over distance).")]
        ParticleSystem flightPuffs;
        [SerializeField] ExplosionFx explosionPrefab;
        [SerializeField] Gradient trailCalm;
        [SerializeField] Gradient trailCritical;

        // baseline glow, extra glow on each beep, and beep scale pop - per stage (calm..critical)
        static readonly float[] Baseline = { 0.25f, 0.6f, 1.1f, 1.8f };
        static readonly float[] PulsePeak = { 1.5f, 2.2f, 3.0f, 4.0f };
        static readonly float[] PopScale = { 0.10f, 0.15f, 0.22f, 0.30f };
        static readonly float[] Decay = { 4f, 6f, 10f, 16f };

        BombController bomb;
        BombAudio bombAudio;
        MaterialPropertyBlock block;
        Vector3 baseScale;
        float pulse;          // 0..1, set to 1 on each beep
        float catchPop;       // 0..1, set to 1 on catch
        FuseStage stage;

        // natural motion
        Vector3 spin;                  // world-space angular velocity while flying, degrees per second
        Vector3 spin2;                 // a second tumble on another random axis: the potato turns every which way
        Vector3 wobbleAxis;            // the main spin axis itself drifts around this axis during the flight
        float wobbleRate;              // degrees per second
        bool needSpinAxis;             // pick the tumble axis once the direction of travel is known
        Vector3 lastPosition;
        Vector3 kick;                  // decaying random jolt (euler degrees) added to the hand sway
        float swayPhase;

        // catch snap: the visual starts where the bomb was caught and slides into the hands (presentation only)
        const float SnapDuration = 0.07f, MaxSnapDistance = 2.5f;
        Vector3 visualBaseLocal;
        Vector3 snapFrom;              // world offset from the hand at the moment of the catch
        float snapUntil = -1f;

        ExplosionFx explosion;
        float throwSpeed01;

        void Awake()
        {
            bomb = GetComponent<BombController>();
            bombAudio = GetComponent<BombAudio>();
            block = new MaterialPropertyBlock();
            baseScale = visual.localScale;
            visualBaseLocal = visual.localPosition;
            swayPhase = Random.value * 10f;
            lastPosition = transform.position;
            if (explosionPrefab != null)
            {
                explosion = Instantiate(explosionPrefab);
                explosion.name = "BombExplosionFx";
                explosion.gameObject.SetActive(false);
            }
            SetFlightFx(false);
        }

        void OnDestroy()
        {
            if (explosion != null) Destroy(explosion.gameObject);
        }

        void OnEnable()
        {
            bombAudio.Beeped += OnBeep;
            bomb.BombCaught += OnCaught;
            bomb.BombThrown += OnThrown;
            bomb.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            bombAudio.Beeped -= OnBeep;
            bomb.BombCaught -= OnCaught;
            bomb.BombThrown -= OnThrown;
            bomb.StateChanged -= OnStateChanged;
        }

        void Update()
        {
            stage = bomb.Fuse.Stage;
            int s = (int)stage;

            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * Decay[s]);
            catchPop = Mathf.MoveTowards(catchPop, 0f, Time.deltaTime * 5f);

            float intensity = Baseline[s] + pulse * PulsePeak[s] + catchPop * 3f;
            Color tint = Color.Lerp(baseEmission, criticalEmission, s / 3f);
            Color emission = tint * (intensity * emissionScale);

            block ??= new MaterialPropertyBlock();   // survives a script reload during Play Mode
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionColor, emission);
            bodyRenderer.SetPropertyBlock(block);

            visual.localScale = baseScale * (1f + pulse * PopScale[s] + catchPop * 0.35f);

            UpdateNaturalMotion();
            UpdateCatchSnap();
            UpdateVfx(s);
        }

        void UpdateVfx(int s)
        {
            var tuning = bomb.Tuning;
            bool live = bomb.State == BombState.Held || bomb.State == BombState.CaughtGrace || bomb.State == BombState.Thrown;
            if (fuseSparks != null)
            {
                var emission = fuseSparks.emission;
                float rate = tuning != null && tuning.fuseSparkRates != null && tuning.fuseSparkRates.Length > s ? tuning.fuseSparkRates[s] : 20f;
                // The carrier sees the wick up close: fewer sparks in their own face.
                bool inMyFace = bomb.Carrier != null && FirstPersonCamera.Instance != null && FirstPersonCamera.Instance.Target == bomb.Carrier;
                emission.rateOverTime = live ? rate * (inMyFace ? 0.5f : 1f) : 0f;
            }
            if (trail != null && trail.emitting && trailCalm != null && trailCritical != null)
                trail.colorGradient = s >= 2 ? trailCritical : trailCalm;
        }

        void SetFlightFx(bool on)
        {
            var tuning = bomb != null ? bomb.Tuning : null;
            if (trail != null)
            {
                if (on)
                {
                    trail.Clear();
                    trail.time = tuning != null ? tuning.trailTime : 0.22f;
                    trail.widthMultiplier = (tuning != null ? tuning.trailWidth : 0.16f) * Mathf.Lerp(0.55f, 1f, throwSpeed01);
                }
                trail.emitting = on;
            }
            if (flightPuffs != null)
            {
                var emission = flightPuffs.emission;
                emission.rateOverDistance = on ? (tuning != null ? tuning.flightPuffsPerMetre : 1.6f) : 0f;
            }
        }

        void UpdateCatchSnap()
        {
            if (snapUntil < 0f) return;
            float k = Mathf.Clamp01(1f - (snapUntil - Time.time) / SnapDuration);
            if (k >= 1f)
            {
                snapUntil = -1f;
                visual.localPosition = visualBaseLocal;
                return;
            }
            float ease = 1f - (1f - k) * (1f - k);
            visual.position = transform.TransformPoint(visualBaseLocal) + snapFrom * (1f - ease);
        }

        void UpdateNaturalMotion()
        {
            float dt = Time.deltaTime;
            var tuning = bomb.Tuning;
            Vector3 travel = transform.position - lastPosition;
            lastPosition = transform.position;

            if (bomb.State == BombState.Thrown)
            {
                float speed = dt > 0f ? travel.magnitude / dt : 0f;
                if (needSpinAxis && speed > 1f)
                {
                    ChooseSpin(travel.normalized, speed, tuning);
                    throwSpeed01 = tuning != null ? Mathf.Clamp01(speed / Mathf.Max(1f, tuning.throwSpeedMax)) : 0.5f;
                    SetFlightFx(true);   // once the speed is known, so the trail width matches the throw
                }
                if (spin.sqrMagnitude > 0.01f)
                {
                    spin = Quaternion.AngleAxis(wobbleRate * dt, wobbleAxis) * spin;   // the axis wanders: no steady spin
                    visual.rotation = Quaternion.AngleAxis(spin.magnitude * dt, spin.normalized) *
                                      Quaternion.AngleAxis(spin2.magnitude * dt, spin2.normalized) * visual.rotation;
                }
                return;
            }

            // Not flying: settle into the hand. A slow, slightly irregular sway keeps it alive, plus the catch jolt.
            spin = spin2 = Vector3.zero;
            kick *= Mathf.Exp(-10f * dt);
            float t = Time.time + swayPhase;
            float sway = tuning != null ? tuning.handSwayDegrees : 0f;
            Quaternion target = Quaternion.Euler(
                Mathf.Sin(t * 1.6f) * sway + kick.x,
                Mathf.Sin(t * 1.1f) * sway * 1.3f + kick.y,
                Mathf.Sin(t * 2.3f) * sway * 0.6f + kick.z);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target, 1f - Mathf.Exp(-12f * dt));
        }

        void ChooseSpin(Vector3 direction, float speed, GameTuning tuning)
        {
            needSpinAxis = false;
            float randomness = tuning != null ? tuning.tumbleRandomness : 0.35f;
            float degrees = tuning != null ? tuning.tumbleDegreesPerSecond : 480f;

            // A lobbed potato tumbles chaotically: a main spin (end-over-end at low randomness, any axis at high
            // randomness) plus a second spin on another random axis, and the main axis keeps wandering in flight.
            Vector3 across = Vector3.Cross(Vector3.up, direction);
            if (across.sqrMagnitude < 0.01f) across = Vector3.right;
            Vector3 axis = Vector3.Slerp(across.normalized, Random.onUnitSphere, randomness).normalized;

            float speedFactor = Mathf.Clamp01(speed / 24f);              // faster throws spin faster
            float rate = degrees * Mathf.Lerp(0.55f, 1.35f, speedFactor) * Random.Range(0.8f, 1.2f);
            if (Random.value < 0.25f) rate = -rate;                       // occasionally tumbles the other way
            spin = axis * rate;
            spin2 = Random.onUnitSphere * (Mathf.Abs(rate) * Mathf.Lerp(0.15f, 0.8f, randomness));
            wobbleAxis = Random.onUnitSphere;
            wobbleRate = Mathf.Lerp(60f, 420f, randomness) * Random.Range(0.8f, 1.2f);
        }

        void OnThrown(Player thrower)
        {
            needSpinAxis = true;
            lastPosition = transform.position;
        }

        void OnBeep(FuseStage beepStage) => pulse = 1f;

        void OnCaught(Player receiver)
        {
            // Where it was last drawn, relative to the hand it just snapped to: slide in from there.
            snapFrom = Vector3.ClampMagnitude(lastPosition - transform.position, MaxSnapDistance);
            snapUntil = Time.time + SnapDuration;
            SetFlightFx(false);
            if (trail != null) trail.Clear();
            catchPop = 1f;
            kick = Random.insideUnitSphere * 14f;   // a small jolt as it lands in the hand
        }

        void OnStateChanged(BombState from, BombState to)
        {
            if (to != BombState.Thrown) SetFlightFx(false);
            if (to == BombState.Exploding)
            {
                visual.gameObject.SetActive(false);
                if (trail != null) trail.Clear();
                if (explosion != null) explosion.Play(transform.position, bomb.Tuning != null ? bomb.Tuning.flashReduction : 0f);
                else StartCoroutine(ExplosionFlash(transform.position));
            }
            else if (from == BombState.Exploding || from == BombState.Resetting)
            {
                visual.gameObject.SetActive(true);
                visual.localRotation = Quaternion.identity;
                visual.localPosition = visualBaseLocal;
                snapUntil = -1f;
                spin = Vector3.zero;
                pulse = 0f;
                catchPop = 0f;
            }
        }

        IEnumerator ExplosionFlash(Vector3 position)
        {
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "ExplosionFlash";
            Destroy(flash.GetComponent<Collider>());
            if (explosionMaterial != null) flash.GetComponent<Renderer>().sharedMaterial = explosionMaterial;
            flash.transform.position = position;

            const float duration = 0.45f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                flash.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 5f, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            Destroy(flash);
        }
    }
}
