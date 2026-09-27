using System;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Beep cadence by fuse stage plus throw / catch / explosion one-shots. Consumes bomb state and
    /// never changes it. <see cref="Beeped"/> lets the visual pulse stay in sync with the audio.
    /// </summary>
    [RequireComponent(typeof(AudioSource), typeof(BombController))]
    public class BombAudio : MonoBehaviour
    {
        [SerializeField] GameTuning tuning;

        AudioSource source;
        BombController bomb;
        AudioClip[] beeps;
        AudioClip catchClip, throwClip, explosionClip;
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

            beeps = new AudioClip[4];
            for (int i = 0; i < beeps.Length; i++) beeps[i] = ProceduralSfx.Beep((FuseStage)i);
            catchClip = ProceduralSfx.Catch();
            throwClip = ProceduralSfx.Throw();
            explosionClip = ProceduralSfx.Explosion();
        }

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
            source.PlayOneShot(beeps[(int)stage], tuning.beepVolume);
            Beeped?.Invoke(stage);
            nextBeepTime = Time.time + tuning.beepIntervals[(int)stage];
        }

        void OnStateChanged(BombState from, BombState to)
        {
            // Give a fresh bomb a beat of silence before its first beep.
            if (from == BombState.Resetting) nextBeepTime = Time.time + 0.4f;
        }

        void OnCaught(Player receiver) => source.PlayOneShot(catchClip, 1f);
        void OnThrown(Player thrower) => source.PlayOneShot(throwClip, 0.8f);
        void OnExploded(BombFailReason reason, string detail) => source.PlayOneShot(explosionClip, 1f);
    }
}
