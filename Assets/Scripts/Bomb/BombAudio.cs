using System;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Beep cadence by fuse stage plus throw / catch / explosion one-shots. Consumes bomb state and
    /// never changes it. <see cref="Beeped"/> lets the visual pulse stay in sync with the audio.
    /// </summary>
    [RequireComponent(typeof(AudioSource), typeof(BombController))]
    public class BombAudio : MonoBehaviour
    {
        [SerializeField] GameTuning tuning;

        [Header("Real sounds (optional, from Assets/Audio/SFX; empty = procedural placeholder)")]
        [SerializeField, Tooltip("Calm, medium, urgent, critical. Leave empty (or any entry empty) for the placeholder.")]
        AudioClip[] beeps = new AudioClip[4];
        [SerializeField] AudioClip catchClip;
        [SerializeField] AudioClip throwClip;
        [SerializeField] AudioClip explosionClip;

        AudioSource source;
        BombController bomb;
        AudioClip[] placeholderBeeps;
        AudioClip placeholderCatch, placeholderThrow, placeholderExplosion;
        float nextBeepTime;

        /// <summary>Raised on every beep with the stage it was played at.</summary>
        public event Action<FuseStage> Beeped;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            bomb = GetComponent<BombController>();

            source.playOnAwake = false;
            source.spatialBlend = 0.8f;
            source.maxDistance = 45f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }

        // Real clip if one is assigned, else the procedural placeholder. Resolved when played (not cached in Awake),
        // so a script reload during Play Mode, which drops runtime-made clips, never leaves a null.
        AudioClip BeepClip(FuseStage stage)
        {
            int i = (int)stage;
            if (beeps != null && i < beeps.Length && beeps[i] != null) return beeps[i];
            placeholderBeeps ??= new AudioClip[4];
            return placeholderBeeps[i] != null ? placeholderBeeps[i] : placeholderBeeps[i] = ProceduralSfx.Beep(stage);
        }

        static AudioClip Pick(AudioClip assigned, ref AudioClip placeholder, Func<AudioClip> make) =>
            assigned != null ? assigned : placeholder != null ? placeholder : placeholder = make();

        void OnEnable()
        {
            bomb.BombCaught += OnCaught;
            bomb.BombThrown += OnThrown;
            bomb.BombExploded += OnExploded;
            bomb.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            bomb.BombCaught -= OnCaught;
            bomb.BombThrown -= OnThrown;
            bomb.BombExploded -= OnExploded;
            bomb.StateChanged -= OnStateChanged;
        }

        void Update()
        {
            bool alive = bomb.State == BombState.Held || bomb.State == BombState.Thrown || bomb.State == BombState.CaughtGrace;
            if (!alive || Time.time < nextBeepTime) return;

            var stage = bomb.Fuse.Stage;
            source.PlayOneShot(BeepClip(stage), Settings.BeepVolume(tuning));
            Beeped?.Invoke(stage);
            // A fuse zone changes the burn rate: the beep speeds up in a hot zone and slows in a cold one (spec §7.3).
            nextBeepTime = Time.time + tuning.beepIntervals[(int)stage] / Mathf.Clamp(bomb.Fuse.Rate, 0.25f, 4f);
        }

        void OnStateChanged(BombState from, BombState to)
        {
            // Give a fresh bomb a beat of silence before its first beep.
            if (from == BombState.Resetting) nextBeepTime = Time.time + 0.4f;
            // Swallowed by a tube or cannon: a low "thunk" (the exit's rising tone is TransitPresentation's).
            if (to == BombState.InTransit) source.PlayOneShot(Pick(catchClip, ref placeholderCatch, ProceduralSfx.Catch), 0.5f);
        }

        void OnCaught(Player receiver) => source.PlayOneShot(Pick(catchClip, ref placeholderCatch, ProceduralSfx.Catch), 1f);
        void OnThrown(Player thrower) => source.PlayOneShot(Pick(throwClip, ref placeholderThrow, ProceduralSfx.Throw), 0.8f);
        void OnExploded(BombFailReason reason, string detail) =>
            source.PlayOneShot(Pick(explosionClip, ref placeholderExplosion, ProceduralSfx.Explosion), 1f);
    }
}
