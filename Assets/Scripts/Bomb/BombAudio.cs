using System;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Beep cadence by fuse stage plus throw / catch / explosion one-shots, a fuse loop while it burns, and the "thunk" of a
    /// tube or cannon. Consumes bomb state and never changes it. <see cref="Beeped"/> lets the visual pulse stay in sync with
    /// the audio. The sounds are folders of Assets/Audio/SFX/Bomb (<see cref="CueKeys"/>, ARCHITECTURE §26); an empty folder
    /// plays the procedural placeholder from <see cref="ProceduralSfx"/>, so nothing goes silent.
    /// </summary>
    [RequireComponent(typeof(AudioSource), typeof(BombController))]
    public class BombAudio : MonoBehaviour
    {
        const float FirstBeepDelay = 0.4f;     // a fresh bomb's beat of silence before its first beep
        const float TransitVolume = 0.5f;      // the tube's thunk, under a catch
        const float ThrowVolume = 0.8f;
        const float FuseLoopFade = 0.2f;       // seconds the fuse loop takes to start or stop

        [SerializeField] GameTuning tuning;

        AudioSource source;
        BombController bomb;
        AudioClip[] placeholderBeeps;
        AudioClip placeholderCatch, placeholderThrow, placeholderExplosion;
        float nextBeepTime;
        LoopHandle fuseLoop;

        /// <summary>Raised on every beep with the stage it was played at.</summary>
        public event Action<FuseStage> Beeped;

        /// <summary>Any bomb beeped (the music dips under it, #47).</summary>
        public static event Action<FuseStage> AnyBeeped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => AnyBeeped = null;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            bomb = GetComponent<BombController>();

            source.playOnAwake = false;
            source.spatialBlend = 0.8f;
            source.maxDistance = 45f;
            source.rolloffMode = AudioRolloffMode.Linear;
            // The bomb's group: the pause menu never muffles it (online the game goes on, the beeps must stay readable).
            var director = AudioDirector.Instance;
            if (director != null) source.outputAudioMixerGroup = director.Bank.GroupFor(CueKeys.BombThrow);
        }

        // A sound from its folder if it holds one, else the procedural placeholder. Placeholders are made when first played
        // (not cached in Awake), so a script reload during Play Mode, which drops runtime-made clips, never leaves a null.
        void Play(string key, ref AudioClip placeholder, Func<AudioClip> make, float volume)
        {
            var clip = Sfx.Pick(key, out float cueVolume);
            if (clip == null)
            {
                clip = placeholder != null ? placeholder : placeholder = make();
                cueVolume = 1f;
            }
            source.PlayOneShot(clip, volume * cueVolume);
        }

        void PlayBeep(FuseStage stage, float volume)
        {
            int i = (int)stage;
            placeholderBeeps ??= new AudioClip[CueKeys.BombBeeps.Length];
            Play(CueKeys.BombBeeps[i], ref placeholderBeeps[i], () => ProceduralSfx.Beep(stage), volume);
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
            fuseLoop.Stop();
        }

        void Update()
        {
            bool alive = bomb.State == BombState.Held || bomb.State == BombState.Thrown || bomb.State == BombState.CaughtGrace;
            UpdateFuseLoop(alive);
            if (!alive || Time.time < nextBeepTime) return;

            var stage = bomb.Fuse.Stage;
            PlayBeep(stage, Settings.BeepVolume(tuning));
            Beeped?.Invoke(stage);
            AnyBeeped?.Invoke(stage);
            // A fuse zone changes the burn rate: the beep speeds up in a hot zone and slows in a cold one (spec §7.3).
            nextBeepTime = Time.time + tuning.beepIntervals[(int)stage] / Mathf.Clamp(bomb.Fuse.Rate, 0.25f, 4f);
        }

        // The fuse's hiss while it burns, at the fuse warning volume like the beeps it goes with.
        void UpdateFuseLoop(bool burning)
        {
            if (burning && !fuseLoop.IsPlaying) fuseLoop = Sfx.Loop(CueKeys.BombFuseLoop, transform, 1f, FuseLoopFade);
            else if (!burning && fuseLoop.IsPlaying) fuseLoop.Stop(FuseLoopFade);
            if (burning) fuseLoop.SetGain(Settings.BeepVolume(tuning));
        }

        void OnStateChanged(BombState from, BombState to)
        {
            if (from == BombState.Resetting) nextBeepTime = Time.time + FirstBeepDelay;
            // Swallowed by a tube or cannon: a low "thunk" (the exit's rising tone is TransitPresentation's).
            if (to == BombState.InTransit)
            {
                if (Sfx.Has(CueKeys.BombTransitIn)) Sfx.Play(CueKeys.BombTransitIn, transform.position);
                else Play(CueKeys.BombCatch, ref placeholderCatch, ProceduralSfx.Catch, TransitVolume);
            }
        }

        void OnCaught(Player receiver) => Play(CueKeys.BombCatch, ref placeholderCatch, ProceduralSfx.Catch, 1f);
        void OnThrown(Player thrower) => Play(CueKeys.BombThrow, ref placeholderThrow, ProceduralSfx.Throw, ThrowVolume);
        void OnExploded(BombFailReason reason, string detail) =>
            Play(CueKeys.BombExplosion, ref placeholderExplosion, ProceduralSfx.Explosion, 1f);
    }
}
