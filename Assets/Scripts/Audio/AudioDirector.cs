using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata
{
    /// <summary>
    /// Plays the game's sounds from the <see cref="SoundBank"/> (ARCHITECTURE §26). It starts itself on the first scene when
    /// the bank exists (never in a headless build), survives scene loads, and holds the music, ambience, run, obstacle, menu
    /// and player sound components, all presentation: they read state and never change it.
    /// One-shots and loops share a pool of <see cref="GameTuning.audioVoices"/> AudioSources, each on its cue's mixer group;
    /// a cue's own limits (cooldown, voices) apply. Time is unscaled, so menus sound while an offline pause freezes the game.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        sealed class Voice
        {
            public AudioSource source;
            public Cue cue;
            public int generation;
            public bool busy, loop, follows;
            public Transform follow;
            public float baseVolume, gain = 1f;
            public float fade = 1f, fadeTarget = 1f, fadeRate;
            public bool stopAtSilence;
            public float endTime;
        }

        readonly List<Voice> voices = new List<Voice>();
        readonly HashSet<Player> equipped = new HashSet<Player>();
        SoundBank bank;
        AudioListener listener;
        int listenerSearchFrame = -1;
        bool paused;

        public SoundBank Bank => bank;
        public GameTuning Tuning => bank.Tuning;

        /// <summary>Where the game is heard from (the scene's AudioListener), or null.</summary>
        public Transform Listener
        {
            get
            {
                // Searched at most once a frame: several components ask, and a scene may have none for a while.
                if ((listener == null || !listener.isActiveAndEnabled) && listenerSearchFrame != Time.frameCount)
                {
                    listenerSearchFrame = Time.frameCount;
                    listener = FindFirstObjectByType<AudioListener>();
                }
                return listener != null && listener.isActiveAndEnabled ? listener.transform : null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        // Before the first scene, so its objects (the bomb, the campfires) find the director in their Awake.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Instance != null || Application.isBatchMode) return;   // headless bots and CI make no sound
            var bank = Resources.Load<SoundBank>(SoundBank.ResourcePath);
            if (bank == null || bank.Tuning == null) return;
            var go = new GameObject("AudioDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioDirector>().Init(bank);
        }

        void Init(SoundBank soundBank)
        {
            Instance = this;
            bank = soundBank;
            for (int i = 0; i < bank.Tuning.audioVoices; i++) voices.Add(NewVoice(i));
            gameObject.AddComponent<MusicDirector>().Init(this);
            gameObject.AddComponent<AmbienceDirector>().Init(this);
            gameObject.AddComponent<CourseSounds>().Init(this);
            gameObject.AddComponent<ObstacleSounds>().Init(this);
            gameObject.AddComponent<UiSounds>().Init(this);
            SceneManager.activeSceneChanged += OnSceneChanged;
            if (bank.Normal != null) bank.Normal.TransitionTo(0f);
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            if (Instance == this) Instance = null;
        }

        void OnSceneChanged(Scene from, Scene to)
        {
            listener = null;
            PlayerSounds.ClearSurfaceCache();
        }

        Voice NewVoice(int index)
        {
            var go = new GameObject("Voice " + index);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.outputAudioMixerGroup = bank.GroupFor(null);
            return new Voice { source = source };
        }

        // ------------------------------------------------------------------ playing

        /// <summary>
        /// Plays one variation of cue <paramref name="key"/>: flat when there is no position, else placed at
        /// <paramref name="position"/> (or following <paramref name="follow"/>). Returns false when the folder is empty,
        /// the cue is cooling down or at its voice limit, or every voice is busy.
        /// </summary>
        public bool Play(string key, Vector3? position = null, float volume = 1f, Transform follow = null)
        {
            if (!bank.TryGet(key, out var cue)) return false;
            var pick = cue.Pick();
            return StartVoice(cue, pick.clip, key, position, follow, volume, false, 0f) != null;
        }

        /// <summary>Starts cue <paramref name="key"/> looping (one variation), fading in over <paramref name="fadeIn"/> seconds.</summary>
        public LoopHandle Loop(string key, Transform follow = null, float volume = 1f, float fadeIn = 0f, Vector3? position = null)
        {
            if (!bank.TryGet(key, out var cue)) return default;
            return LoopClip(cue, cue.Pick().clip, follow, volume, fadeIn, position);
        }

        /// <summary>Starts <paramref name="clip"/> of <paramref name="cue"/> looping at a random point (ambience beds play every clip).</summary>
        public LoopHandle LoopClip(Cue cue, AudioClip clip, Transform follow, float volume, float fadeIn, Vector3? position = null)
        {
            var voice = StartVoice(cue, clip, cue.key, position, follow, volume, true, fadeIn);
            if (voice == null) return default;
            voice.source.time = Random.Range(0f, clip.length * 0.95f);
            return new LoopHandle(this, voice.generation, voices.IndexOf(voice));
        }

        Voice StartVoice(Cue cue, AudioClip clip, string key, Vector3? position, Transform follow, float volume, bool loop, float fadeIn)
        {
            if (clip == null) return null;
            float now = Time.unscaledTime;
            if (!loop && now < cue.nextAllowed) return null;
            var s = cue.settings;
            int playing = 0;
            Voice free = null;
            foreach (var v in voices)
            {
                if (v.busy && v.cue == cue) playing++;
                else if (!v.busy && free == null) free = v;
            }
            if (playing >= s.maxVoices || free == null) return null;
            cue.nextAllowed = now + s.cooldown;

            var source = free.source;
            bool placed = position.HasValue || follow != null;
            source.transform.position = follow != null ? follow.position : position ?? Vector3.zero;
            source.clip = clip;
            source.loop = loop;
            source.outputAudioMixerGroup = bank.GroupFor(key);
            source.spatialBlend = placed ? s.spatialBlend : 0f;
            source.minDistance = s.minDistance;
            source.maxDistance = Mathf.Max(s.minDistance + 0.1f, s.maxDistance);
            source.priority = s.priority;
            source.pitch = CuePicker.Jitter(s.pitchJitter, Random.value);

            free.busy = true;
            free.cue = cue;
            free.generation++;
            free.loop = loop;
            free.follow = follow;
            free.follows = follow != null;
            free.baseVolume = s.volume * Mathf.Max(0f, volume) * CuePicker.Jitter(s.volumeJitter, Random.value);
            free.gain = 1f;
            free.stopAtSilence = false;
            free.fadeTarget = 1f;
            free.fade = fadeIn > 0f ? 0f : 1f;
            free.fadeRate = fadeIn > 0f ? 1f / fadeIn : 0f;
            free.endTime = loop ? float.PositiveInfinity : now + clip.length / Mathf.Max(0.05f, Mathf.Abs(source.pitch)) + 0.05f;
            Apply(free);
            source.Play();
            return free;
        }

        static void Apply(Voice v) => v.source.volume = v.baseVolume * v.gain * v.fade;

        void Release(Voice v)
        {
            v.source.Stop();
            v.source.clip = null;
            v.busy = false;
            v.cue = null;
            v.follow = null;
            v.follows = false;
            v.generation++;
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            foreach (var v in voices)
            {
                if (!v.busy) continue;
                if (v.follows)
                {
                    if (v.follow == null) { Release(v); continue; }   // its object was destroyed (a scene change)
                    v.source.transform.position = v.follow.position;
                }
                if (v.fade != v.fadeTarget && v.fadeRate > 0f) v.fade = Mathf.MoveTowards(v.fade, v.fadeTarget, v.fadeRate * dt);
                else v.fade = v.fadeTarget;
                Apply(v);
                if ((v.stopAtSilence && v.fade <= 0f) || (!v.loop && now >= v.endTime)) Release(v);
            }
            UpdatePause();
            EquipPlayers();
        }

        // ------------------------------------------------------------------ loops (LoopHandle)

        Voice Resolve(int index, int generation) =>
            index >= 0 && index < voices.Count && voices[index].busy && voices[index].generation == generation ? voices[index] : null;

        internal bool IsAlive(int index, int generation) => Resolve(index, generation) != null;

        internal void SetGain(int index, int generation, float gain)
        {
            var v = Resolve(index, generation);
            if (v != null) v.gain = Mathf.Max(0f, gain);
        }

        internal void FadeTo(int index, int generation, float target, float seconds, bool stop)
        {
            var v = Resolve(index, generation);
            if (v == null) return;
            v.fadeTarget = Mathf.Clamp01(target);
            v.fadeRate = seconds > 0f ? 1f / seconds : 0f;
            v.stopAtSilence = stop;
            if (seconds <= 0f)
            {
                v.fade = v.fadeTarget;
                if (stop && v.fade <= 0f) Release(v);
            }
        }

        // ------------------------------------------------------------------ the mix

        // The pause menu muffles the mix (the Paused snapshot); the bomb and the menus stay clear: online, the game goes on.
        void UpdatePause()
        {
            bool open = PauseMenu.IsOpen;
            if (open == paused) return;
            paused = open;
            var snapshot = paused ? bank.Paused : bank.Normal;
            if (snapshot != null) snapshot.TransitionTo(bank.Tuning.pauseMuffleSeconds);
        }

        // Every player gets its movement sounds (own and teammates'), whatever spawned it.
        void EquipPlayers()
        {
            if (equipped.Count > 0) equipped.RemoveWhere(p => p == null);
            foreach (var p in Player.All)
            {
                if (p == null || equipped.Contains(p)) continue;
                equipped.Add(p);
                if (p.GetComponent<PlayerSounds>() == null) p.gameObject.AddComponent<PlayerSounds>();
            }
        }
    }

    /// <summary>A looping sound started by <see cref="AudioDirector.Loop"/>. Safe to keep after its voice is gone (it does nothing).</summary>
    public readonly struct LoopHandle
    {
        readonly AudioDirector director;
        readonly int generation, index;

        internal LoopHandle(AudioDirector director, int generation, int index)
        {
            this.director = director;
            this.generation = generation;
            this.index = index;
        }

        public bool IsPlaying => director != null && director.IsAlive(index, generation);

        /// <summary>Scales the loop's volume (0..1+), on top of its fade.</summary>
        public void SetGain(float gain) { if (director != null) director.SetGain(index, generation, gain); }

        public void FadeTo(float target, float seconds) { if (director != null) director.FadeTo(index, generation, target, seconds, false); }

        /// <summary>Fades out over <paramref name="seconds"/> and frees the voice.</summary>
        public void Stop(float seconds = 0f) { if (director != null) director.FadeTo(index, generation, 0f, seconds, true); }
    }

    /// <summary>Short calls for the presentation code: <c>Sfx.Play(CueKeys.BombThrow, position)</c>. Silent without a director.</summary>
    public static class Sfx
    {
        public static bool Play(string key, Vector3? position = null, float volume = 1f, Transform follow = null) =>
            AudioDirector.Instance != null && AudioDirector.Instance.Play(key, position, volume, follow);

        public static LoopHandle Loop(string key, Transform follow = null, float volume = 1f, float fadeIn = 0f) =>
            AudioDirector.Instance != null ? AudioDirector.Instance.Loop(key, follow, volume, fadeIn) : default;

        /// <summary>True when folder <paramref name="key"/> holds a sound.</summary>
        public static bool Has(string key) => AudioDirector.Instance != null && AudioDirector.Instance.Bank.Has(key);

        /// <summary>
        /// A variation of <paramref name="key"/> to play on a component's own AudioSource (the bomb keeps its source), or null.
        /// </summary>
        public static AudioClip Pick(string key, out float volume)
        {
            volume = 1f;
            if (AudioDirector.Instance == null || !AudioDirector.Instance.Bank.TryGet(key, out var cue)) return null;
            volume = cue.settings.volume * CuePicker.Jitter(cue.settings.volumeJitter, Random.value);
            return cue.Pick().clip;
        }
    }
}
