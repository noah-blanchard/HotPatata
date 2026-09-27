using System.Collections;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Emissive pulse that mirrors the beep, catch pop, and a placeholder explosion flash. The visual also moves
    /// naturally: it tumbles in flight (axis and speed slightly random, scaled by throw speed), then settles into a
    /// gentle sway in the hand with a small jolt on every catch. Rotation is cosmetic only and never touches physics.
    /// Urgency is carried by pulse rate, brightness AND size, not by colour alone. Reads bomb state only.
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
        bool needSpinAxis;             // pick the tumble axis once the direction of travel is known
        Vector3 lastPosition;
        Vector3 kick;                  // decaying random jolt (euler degrees) added to the hand sway
        float swayPhase;

        // catch snap: the visual starts where the bomb was caught and slides into the hands (presentation only)
        const float SnapDuration = 0.07f, MaxSnapDistance = 2.5f;
        Vector3 visualBaseLocal;
        Vector3 snapFrom;              // world offset from the hand at the moment of the catch
        float snapUntil = -1f;

        void Awake()
        {
            bomb = GetComponent<BombController>();
            bombAudio = GetComponent<BombAudio>();
            block = new MaterialPropertyBlock();
            baseScale = visual.localScale;
            visualBaseLocal = visual.localPosition;
            swayPhase = Random.value * 10f;
            lastPosition = transform.position;
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
            Color emission = tint * intensity;

            block ??= new MaterialPropertyBlock();   // survives a script reload during Play Mode
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionColor, emission);
            bodyRenderer.SetPropertyBlock(block);

            visual.localScale = baseScale * (1f + pulse * PopScale[s] + catchPop * 0.35f);

            UpdateNaturalMotion();
            UpdateCatchSnap();
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
                if (needSpinAxis && speed > 1f) ChooseSpin(travel.normalized, speed, tuning);
                if (spin.sqrMagnitude > 0.01f)
                    visual.rotation = Quaternion.AngleAxis(spin.magnitude * dt, spin.normalized) * visual.rotation;
                return;
            }

            // Not flying: settle into the hand. A slow, slightly irregular sway keeps it alive, plus the catch jolt.
            spin = Vector3.zero;
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

            // A lobbed object tumbles mostly end-over-end: about the horizontal axis across its path,
            // with some random tilt and a random secondary wobble.
            Vector3 across = Vector3.Cross(Vector3.up, direction);
            if (across.sqrMagnitude < 0.01f) across = Vector3.right;
            Vector3 axis = (across.normalized + Random.onUnitSphere * randomness).normalized;

            float speedFactor = Mathf.Clamp01(speed / 24f);              // faster throws spin faster
            float rate = degrees * Mathf.Lerp(0.55f, 1.35f, speedFactor) * Random.Range(0.8f, 1.2f);
            if (Random.value < 0.25f) rate = -rate;                       // occasionally tumbles the other way
            spin = axis * rate;
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
            catchPop = 1f;
            kick = Random.insideUnitSphere * 14f;   // a small jolt as it lands in the hand
        }

        void OnStateChanged(BombState from, BombState to)
        {
            if (to == BombState.Exploding)
            {
                visual.gameObject.SetActive(false);
                StartCoroutine(ExplosionFlash(transform.position));
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
