using UnityEngine;
using UnityEngine.Audio;

namespace HotPatata
{
    /// <summary>
    /// Presentation only: while the bomb is inside a tube or cannon, the active exit's lamps brighten, reaching full
    /// glow for the last <see cref="warnSeconds"/>, when a rising tone plays at that exit; the receiver knows where
    /// and when (PROJECT_SPEC §13.16). Brightness ramps, it never flashes. Reads the transit, never changes it.
    /// </summary>
    [RequireComponent(typeof(BombTransit))]
    public class TransitPresentation : MonoBehaviour
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static AudioClip riseClip;

        [SerializeField, Tooltip("One lamp per exit, same order as the transit's exits.")] Renderer[] exitLamps;
        [SerializeField, ColorUsage(false, true)] Color warnEmission = new Color(1.6f, 1.3f, 0.5f);
        [SerializeField, Min(0.05f)] float warnSeconds = 0.5f;
        [SerializeField, Range(0f, 1f)] float toneVolume = 0.7f;
        [SerializeField, Tooltip("Mixer group of the tone (HotPatataMixer SFX), so the effects volume applies.")]
        AudioMixerGroup output;

        BombTransit transit;
        MaterialPropertyBlock block;
        double warnedFor = -1.0;

        void Awake()
        {
            transit = GetComponent<BombTransit>();
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            int active = transit.ActiveExit;
            float left = active >= 0 ? (float)(transit.ReleaseAt - NetMode.ServerTime) : float.MaxValue;
            for (int i = 0; i < exitLamps.Length; i++)
            {
                var lamp = exitLamps[i];
                if (lamp == null) continue;
                float glow = 0f;
                if (i == active) glow = left <= warnSeconds ? 1f : Mathf.Clamp01(1f - left / Mathf.Max(0.05f, transit.Delay)) * 0.6f;
                lamp.GetPropertyBlock(block);
                block.SetColor(EmissionColor, warnEmission * glow);
                lamp.SetPropertyBlock(block);
            }

            if (active >= 0 && left <= warnSeconds && warnedFor != transit.ReleaseAt)
            {
                warnedFor = transit.ReleaseAt;
                var muzzle = transit.GetExit(active).muzzle;
                riseClip ??= ProceduralSfx.Rise(warnSeconds);
                if (muzzle != null) PlayAt(riseClip, muzzle.position);
            }
        }

        /// <summary>Like AudioSource.PlayClipAtPoint, but through <see cref="output"/>.</summary>
        void PlayAt(AudioClip clip, Vector3 position)
        {
            var go = new GameObject("TransitTone");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = output;
            source.spatialBlend = 1f;
            source.PlayOneShot(clip, toneVolume);
            Destroy(go, clip.length + 0.1f);
        }
    }
}
