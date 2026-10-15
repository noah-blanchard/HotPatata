using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// How one cue (one folder of Assets/Audio, ARCHITECTURE §26) plays: its volume, how much each play varies, how often it
    /// may play, and where it sits in space. The bank builder fills it from <c>CueDefaults</c> the first time a folder holds
    /// a sound; a value changed in the bank's Inspector is kept on every rebuild.
    /// </summary>
    [Serializable]
    public class CueSettings
    {
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Each play's volume varies by up to this fraction (0.1 = ±10 %).")]
        [Range(0f, 0.5f)] public float volumeJitter;
        [Tooltip("Each play's pitch varies by up to this fraction (0.08 = ±8 %).")]
        [Range(0f, 0.5f)] public float pitchJitter;
        [Tooltip("Seconds before this cue may play again (0 = any time).")]
        [Min(0f)] public float cooldown;
        [Tooltip("Copies of this cue that may sound at once.")]
        [Min(1)] public int maxVoices = 4;
        [Tooltip("0 = flat (UI, music, beds), 1 = fully placed in the world. Ignored when a sound has no position.")]
        [Range(0f, 1f)] public float spatialBlend = 1f;
        [Min(0.01f)] public float minDistance = 2f;
        [Min(0.1f)] public float maxDistance = 40f;
        [Tooltip("0 = most important, 256 = least. The beeps stay highest.")]
        [Range(0, 256)] public int priority = 128;

        [Header("Scatter folders only (one-shots placed around the listener)")]
        [Tooltip("Seconds between two scattered sounds: random in this range.")]
        public Vector2 scatterInterval = new Vector2(4f, 12f);
        [Tooltip("How far from the listener a scattered sound plays (metres): random in this range.")]
        public Vector2 scatterDistance = new Vector2(10f, 35f);
        [Tooltip("How high above the listener a scattered sound plays (metres): random in this range.")]
        public Vector2 scatterHeight = new Vector2(2f, 12f);

        public CueSettings Clone() => (CueSettings)MemberwiseClone();
    }

    /// <summary>One sound file of a cue. A music track may have an intro that plays once before its loop.</summary>
    [Serializable]
    public struct CueClip
    {
        public AudioClip clip;
        [Tooltip("Music only: played once, then the clip loops (files named X_intro and X_loop).")]
        public AudioClip intro;
    }

    /// <summary>A folder of Assets/Audio: every sound file in it is a variation, picked at random, never the same twice in a row.</summary>
    [Serializable]
    public class Cue
    {
        [Tooltip("The folder, relative to Assets/Audio (e.g. SFX/Player/Footsteps/Grass).")]
        public string key;
        public List<CueClip> clips = new List<CueClip>();
        public CueSettings settings = new CueSettings();

        [NonSerialized] int last = -1;
        [NonSerialized] internal float nextAllowed;

        public bool IsEmpty => clips == null || clips.Count == 0;

        /// <summary>A random variation, never the one played last (when there are two or more).</summary>
        public CueClip Pick()
        {
            if (IsEmpty) return default;
            last = CuePicker.Next(clips.Count, last, UnityEngine.Random.Range);
            return clips[last];
        }

        /// <summary>Forgets the play history (a new play session).</summary>
        internal void ResetRuntime()
        {
            last = -1;
            nextAllowed = 0f;
        }
    }

    /// <summary>Pure picking rules (unit tested).</summary>
    public static class CuePicker
    {
        /// <summary>
        /// A random index in [0, count), never <paramref name="last"/> when count is two or more, each other index equally likely.
        /// <paramref name="range"/> is (min inclusive, max exclusive), as <c>UnityEngine.Random.Range(int, int)</c>.
        /// </summary>
        public static int Next(int count, int last, Func<int, int, int> range)
        {
            if (count <= 1) return 0;
            if (last < 0 || last >= count) return range(0, count);
            int r = range(0, count - 1);
            return r >= last ? r + 1 : r;
        }

        /// <summary>A factor of 1 ± <paramref name="jitter"/>, from a 0..1 random value.</summary>
        public static float Jitter(float jitter, float random01) => 1f + jitter * (random01 * 2f - 1f);
    }
}
