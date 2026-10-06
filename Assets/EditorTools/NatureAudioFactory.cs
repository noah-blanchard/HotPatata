using System.IO;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The world ambience of PatataWilds (ARCHITECTURE §25.3), synthesised here as seamless loops so the game needs no recorded
    /// third-party sound: a crackling fire, a river, a waterfall, wind, birds and crickets, written once as 22 kHz mono WAVs
    /// under <c>Assets/Audio/Ambient</c> (the importer compresses them). A file already there is kept, so a real CC0 recording
    /// can replace it under the same name. These are fixed world sounds, never movement sounds (AGENTS).
    /// </summary>
    public static class NatureAudioFactory
    {
        public enum Ambience { Fire, River, Waterfall, Wind, Birds, Crickets }

        const int Rate = 22050;
        public const string Folder = NatureAssetImporter.AmbientFolder;

        public static string PathOf(Ambience kind) => Folder + kind.ToString().ToLowerInvariant() + ".wav";

        public static AudioClip Clip(Ambience kind)
        {
            EnsureClips();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(PathOf(kind));
        }

        public static void EnsureClips()
        {
            bool wrote = false;
            Directory.CreateDirectory(Folder.TrimEnd('/'));
            foreach (Ambience kind in System.Enum.GetValues(typeof(Ambience)))
            {
                if (File.Exists(PathOf(kind))) continue;
                WriteWav(PathOf(kind), Synthesise(kind));
                wrote = true;
            }
            if (wrote) AssetDatabase.Refresh();
        }

        static float[] Synthesise(Ambience kind)
        {
            var rng = new System.Random(77 + (int)kind * 13);
            float N() => (float)rng.NextDouble() * 2f - 1f;
            float seconds = kind switch { Ambience.Fire => 8f, Ambience.River => 12f, Ambience.Waterfall => 10f, Ambience.Wind => 14f, Ambience.Birds => 16f, _ => 10f };
            int n = (int)(seconds * Rate), fade = Rate / 2;
            var x = new float[n + fade];
            float lp1 = 0f, lp2 = 0f, lp3 = 0f, brown = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float white = N();
                brown = Mathf.Clamp(brown + white * 0.02f, -1f, 1f) * 0.998f;
                switch (kind)
                {
                    case Ambience.River:
                    {
                        lp1 += (white - lp1) * 0.18f;                 // a soft rush
                        lp2 += (lp1 - lp2) * 0.3f;
                        float swell = 0.75f + 0.25f * Mathf.Sin(t * 0.7f) * Mathf.Sin(t * 0.23f + 1f);
                        x[i] = (lp2 * 1.6f + brown * 0.6f) * swell * 0.5f;
                        break;
                    }
                    case Ambience.Waterfall:
                        lp1 += (white - lp1) * 0.45f;
                        x[i] = (lp1 * 0.8f + brown * 0.9f) * 0.55f;
                        break;
                    case Ambience.Wind:
                    {
                        float gust = 0.45f + 0.55f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 0.45f + Mathf.Sin(t * 0.13f) * 2f), 2f);
                        float cutoff = 0.01f + 0.05f * gust;
                        lp1 += (white - lp1) * cutoff;
                        lp2 += (lp1 - lp2) * cutoff;
                        x[i] = lp2 * gust * 5f;
                        break;
                    }
                    case Ambience.Fire:
                        lp1 += (brown - lp1) * 0.05f;                 // a low rumble of the flames
                        x[i] = lp1 * 0.9f;
                        break;
                    default:
                        x[i] = 0f;
                        break;
                }
                lp3 = lp3 * 0.5f + x[i] * 0.5f;
            }
            if (kind == Ambience.Fire)
            {
                // crackles: short, bright bursts at random times
                for (float t = 0f; t < seconds + 0.5f; t += (float)(-System.Math.Log(1.0 - rng.NextDouble()) / 9.0))
                {
                    int start = (int)(t * Rate), length = (int)(Rate * (0.004f + (float)rng.NextDouble() * 0.02f));
                    float amp = 0.2f + (float)rng.NextDouble() * 0.6f;
                    for (int k = 0; k < length && start + k < x.Length; k++) x[start + k] += N() * amp * Mathf.Exp(-k / (length * 0.25f));
                }
            }
            if (kind == Ambience.Birds)
            {
                // phrases of quick whistled chirps, with pauses
                for (float t = 0.3f; t < seconds; t += 1.2f + (float)rng.NextDouble() * 2.4f)
                {
                    int notes = rng.Next(2, 7);
                    float baseFreq = 2200f + (float)rng.NextDouble() * 2600f, amp = 0.12f + (float)rng.NextDouble() * 0.18f;
                    float tt = t;
                    for (int k = 0; k < notes; k++)
                    {
                        float length = 0.05f + (float)rng.NextDouble() * 0.09f, sweep = (float)rng.NextDouble() * 1400f - 500f;
                        int s0 = (int)(tt * Rate), len = (int)(length * Rate);
                        double phase = 0;
                        for (int j = 0; j < len && s0 + j < x.Length; j++)
                        {
                            float u = j / (float)len;
                            float f = baseFreq + sweep * u + 300f * Mathf.Sin(u * 30f);
                            phase += 2.0 * System.Math.PI * f / Rate;
                            x[s0 + j] += Mathf.Sin((float)phase) * amp * Mathf.Sin(u * Mathf.PI);
                        }
                        tt += length + 0.03f + (float)rng.NextDouble() * 0.06f;
                    }
                }
                for (int i = 0; i < x.Length; i++) x[i] += N() * 0.004f;   // the faint air of the forest
            }
            if (kind == Ambience.Crickets)
            {
                for (float t = 0f; t < seconds; t += 0.32f + (float)rng.NextDouble() * 0.1f)
                    for (int pulse = 0; pulse < 3; pulse++)
                    {
                        int s0 = (int)((t + pulse * 0.045f) * Rate), len = (int)(0.025f * Rate);
                        for (int j = 0; j < len && s0 + j < x.Length; j++)
                            x[s0 + j] += Mathf.Sin(2f * Mathf.PI * 4600f * j / Rate) * 0.12f * Mathf.Sin(j / (float)len * Mathf.PI);
                    }
            }
            // seamless loop: the tail fades into the head
            var y = new float[n];
            for (int i = 0; i < n; i++) y[i] = x[i];
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                y[i] = Mathf.Lerp(x[n + i], x[i], w);
            }
            float peak = 0.001f;
            foreach (var v in y) peak = Mathf.Max(peak, Mathf.Abs(v));
            float gain = 0.8f / peak;
            for (int i = 0; i < n; i++) y[i] *= gain;
            return y;
        }

        static void WriteWav(string path, float[] samples)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            int bytes = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);       // PCM
            w.Write((short)1);       // mono
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(bytes);
            foreach (var s in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
    }
}
