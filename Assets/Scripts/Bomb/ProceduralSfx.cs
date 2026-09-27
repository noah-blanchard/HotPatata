using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Placeholder sound effects synthesised at runtime, so the sandbox has distinct beep / catch /
    /// throw / explosion audio without any audio assets. Replace with real clips later.
    /// </summary>
    public static class ProceduralSfx
    {
        const int SampleRate = 44100;

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Short sine blip; higher stages are higher and get a second harmonic.</summary>
        public static AudioClip Beep(FuseStage stage)
        {
            float[] freq = { 740f, 880f, 1040f, 1320f };
            float f = freq[(int)stage];
            float length = stage == FuseStage.Critical ? 0.05f : 0.07f;
            int n = (int)(SampleRate * length);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 45f) * Mathf.Clamp01(i / 60f);
                float s = Mathf.Sin(2f * Mathf.PI * f * t);
                if (stage >= FuseStage.Urgent) s = 0.7f * s + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 2f * t);
                d[i] = s * env * 0.8f;
            }
            return Make("beep_" + stage, d);
        }

        /// <summary>Bright two-step rising chirp: unmistakably "got it".</summary>
        public static AudioClip Catch()
        {
            int n = (int)(SampleRate * 0.22f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float f = t < 0.09f ? 660f : 990f;
                float local = t < 0.09f ? t : t - 0.09f;
                float env = Mathf.Exp(-local * 18f) * Mathf.Clamp01(i / 40f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * f * 2f * t)) * env * 0.55f;
            }
            return Make("catch", d);
        }

        /// <summary>Short falling whoosh.</summary>
        public static AudioClip Throw()
        {
            int n = (int)(SampleRate * 0.16f);
            var d = new float[n];
            var rng = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float k = Mathf.Lerp(0.5f, 0.08f, i / (float)n);   // low-pass closes over time
                lp += (noise - lp) * k;
                d[i] = lp * Mathf.Sin(Mathf.PI * i / n) * 0.7f;
            }
            return Make("throw", d);
        }

        /// <summary>Noise burst with a low thump.</summary>
        public static AudioClip Explosion()
        {
            int n = (int)(SampleRate * 0.7f);
            var d = new float[n];
            var rng = new System.Random(13);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * Mathf.Lerp(0.6f, 0.05f, Mathf.Clamp01(t / 0.5f));
                float thump = Mathf.Sin(2f * Mathf.PI * (70f - 40f * t) * t) * Mathf.Exp(-t * 6f);
                float env = Mathf.Exp(-t * 5f);
                d[i] = Mathf.Clamp((lp * 1.4f * env) + thump * 0.9f, -1f, 1f);
            }
            return Make("explosion", d);
        }
    }
}
