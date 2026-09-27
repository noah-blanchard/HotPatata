using System.Collections;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Emissive pulse that mirrors the beep, catch pop, and a placeholder explosion flash.
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

        void Awake()
        {
            bomb = GetComponent<BombController>();
            bombAudio = GetComponent<BombAudio>();
            block = new MaterialPropertyBlock();
            baseScale = visual.localScale;
        }

        void OnEnable()
        {
            bombAudio.Beeped += OnBeep;
            bomb.BombCaught += OnCaught;
            bomb.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            bombAudio.Beeped -= OnBeep;
            bomb.BombCaught -= OnCaught;
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

            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionColor, emission);
            bodyRenderer.SetPropertyBlock(block);

            visual.localScale = baseScale * (1f + pulse * PopScale[s] + catchPop * 0.35f);
        }

        void OnBeep(FuseStage beepStage) => pulse = 1f;

        void OnCaught(Player receiver) => catchPop = 1f;

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
