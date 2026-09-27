using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The cartoon explosion (presentation only, reused: one instance per bomb). Replays its particle bursts
    /// (fireball puffs, mashed-potato debris, a ground ring), animates a quick flash sphere and light, and pops a
    /// comic "BOOM!" that faces the camera. Flash, light and glare scale down with
    /// <see cref="GameTuning.flashReduction"/> (spec §19).
    /// </summary>
    public class ExplosionFx : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] ParticleSystem[] bursts;
        [SerializeField] Renderer flash;
        [SerializeField] Light flashLight;
        [SerializeField, Tooltip("The text and its outline copies, all under one pivot that billboards and pops.")]
        Transform boom;
        [SerializeField] float flashDuration = 0.14f;
        [SerializeField] float flashMaxScale = 4f;
        [SerializeField] float lightIntensity = 9f;
        [SerializeField] float boomDuration = 0.85f;

        MaterialPropertyBlock block;
        Color flashColor = Color.white;
        float startedAt = -99f, flashStrength = 1f, boomTilt;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (flash != null && flash.sharedMaterial != null && flash.sharedMaterial.HasProperty(BaseColor))
                flashColor = flash.sharedMaterial.GetColor(BaseColor);
            SetVisible(false);
        }

        public void Play(Vector3 position, float flashReduction)
        {
            transform.position = position;
            startedAt = Time.time;
            flashStrength = 1f - Mathf.Clamp01(flashReduction);
            boomTilt = Random.Range(-14f, 14f);
            gameObject.SetActive(true);
            SetVisible(true);
            foreach (var ps in bursts)
            {
                if (ps == null) continue;
                ps.Clear(true);
                ps.Play(true);
            }
            Update();
        }

        void SetVisible(bool visible)
        {
            if (flash != null) flash.enabled = visible && flashStrength > 0.01f;
            if (flashLight != null) flashLight.enabled = visible && flashStrength > 0.01f;
            if (boom != null) boom.gameObject.SetActive(visible);
        }

        void Update()
        {
            float t = Time.time - startedAt;

            // Flash: a bright sphere that swells and fades in a blink.
            float f = Mathf.Clamp01(t / flashDuration);
            if (flash != null && flash.enabled)
            {
                flash.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, flashMaxScale, 1f - (1f - f) * (1f - f));
                var c = flashColor;
                c.a *= (1f - f) * flashStrength;
                block.SetColor(BaseColor, c * new Color(1f, 1f, 1f, 1f));
                flash.SetPropertyBlock(block);
                if (f >= 1f) flash.enabled = false;
            }
            if (flashLight != null && flashLight.enabled)
            {
                float l = Mathf.Clamp01(t / (flashDuration * 1.8f));
                flashLight.intensity = lightIntensity * flashStrength * (1f - l) * (1f - l);
                if (l >= 1f) flashLight.enabled = false;
            }

            // BOOM!: pop in with an overshoot, hold, shrink away. Always faces the camera.
            if (boom != null && boom.gameObject.activeSelf)
            {
                float b = t / boomDuration;
                float scale = b < 0.18f ? Mathf.Lerp(0f, 1.25f, b / 0.18f)
                    : b < 0.3f ? Mathf.Lerp(1.25f, 1f, (b - 0.18f) / 0.12f)
                    : b < 0.8f ? 1f
                    : Mathf.Lerp(1f, 0f, (b - 0.8f) / 0.2f);
                boom.localScale = Vector3.one * Mathf.Max(0f, scale);
                var view = Camera.main;
                if (view != null)
                {
                    boom.position = transform.position + Vector3.up * 1.4f;
                    boom.rotation = Quaternion.LookRotation(boom.position - view.transform.position, view.transform.up) *
                                    Quaternion.Euler(0f, 0f, boomTilt);
                }
                if (b >= 1f) boom.gameObject.SetActive(false);
            }

            if (t > 2.5f) gameObject.SetActive(false);   // bursts are done; sleep until the next explosion
        }
    }
}
