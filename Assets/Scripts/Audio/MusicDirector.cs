using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata
{
    /// <summary>
    /// The music (ARCHITECTURE §26, #47): the main menu's, the lobby's, each course's by act, the results'. Each is a folder of
    /// tracks (<see cref="MusicContext"/>); several tracks in one folder play as a shuffled playlist, never the same twice in a
    /// row, and a pair X_intro + X_loop plays the intro once, then loops. A change of place crossfades
    /// (<see cref="GameTuning.musicCrossfadeSeconds"/>); a level load fades to silence first and the course's track starts once
    /// its run is ready. Each beep dips the music a little so the fuse stays readable. Presentation only.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        // Two decks crossfade; each has an intro source and a body source, so an intro hands over to its loop sample-exact.
        sealed class Deck
        {
            public AudioSource intro, body;
            public Cue cue;
            public float level, target, rate;   // fade 0..1
            public double ends;                 // dsp time the track ends; infinity for a looping track
            public bool Active => cue != null;
        }

        const double ScheduleLead = 0.05;   // seconds ahead a track is scheduled, so its first samples are never cut
        const float InstantRate = 1e6f;     // a fade of 0 seconds (finite: a paused frame's dt of 0 must not make NaN)

        AudioDirector director;
        Deck a, b, current;
        string playingKey;
        float duck = 1f;
        bool levelLoading;
        NetworkSceneManager watchedScenes;

        public void Init(AudioDirector owner)
        {
            director = owner;
            a = NewDeck("Music A");
            b = NewDeck("Music B");
            BombAudio.AnyBeeped += OnBeep;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        void OnDestroy()
        {
            BombAudio.AnyBeeped -= OnBeep;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (watchedScenes != null) watchedScenes.OnSceneEvent -= OnSceneEvent;
        }

        Deck NewDeck(string name)
        {
            return new Deck { intro = NewSource(name + " intro"), body = NewSource(name) };
        }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.priority = 0;
            s.outputAudioMixerGroup = director.Bank.MusicGroup;
            return s;
        }

        void OnBeep(FuseStage stage) => duck = Mathf.Min(duck, director.Tuning.musicBeepDuck);

        void OnActiveSceneChanged(Scene from, Scene to) => levelLoading = false;

        // A level load is announced by Netcode before the menu scene goes: the lobby track fades out while it loads.
        void OnSceneEvent(SceneEvent e)
        {
            if (e.SceneEventType == SceneEventType.Load && RunManager.Instance == null) levelLoading = true;
        }

        void WatchSceneEvents()
        {
            var nm = NetworkManager.Singleton;
            var scenes = nm != null && nm.IsListening ? nm.SceneManager : null;
            if (scenes == watchedScenes) return;
            if (watchedScenes != null) watchedScenes.OnSceneEvent -= OnSceneEvent;
            watchedScenes = scenes;
            if (watchedScenes != null) watchedScenes.OnSceneEvent += OnSceneEvent;
        }

        void Update()
        {
            var t = director.Tuning;
            float dt = Time.unscaledDeltaTime;
            WatchSceneEvents();

            var boot = NetworkBootstrap.Instance;
            NetworkBootstrap.Mode? phase = boot != null ? boot.Phase : (NetworkBootstrap.Mode?)null;
            if (levelLoading) phase = NetworkBootstrap.Mode.InGame;
            var run = RunManager.Instance;
            var place = MusicContext.Resolve(phase, run != null, run != null ? run.State : (RunState?)null);
            var key = director.Bank.FirstPresent(MusicContext.Candidates(place, SceneManager.GetActiveScene().name, CourseAudio.CurrentAct));

            if (key != playingKey)
            {
                playingKey = key;
                if (key == null) FadeOutAll(place == MusicContext.Place.Loading ? t.musicLoadFadeSeconds : t.musicCrossfadeSeconds);
                else StartTrack(key, t.musicCrossfadeSeconds);
            }
            else if (current != null && current.Active && current.cue.clips.Count > 1
                     && current.ends - AudioSettings.dspTime <= t.musicCrossfadeSeconds)
            {
                StartTrack(playingKey, t.musicCrossfadeSeconds);   // the playlist's next track
            }

            duck = Mathf.MoveTowards(duck, 1f, (1f - t.musicBeepDuck) / t.musicBeepDuckRecover * dt);
            Step(a, dt);
            Step(b, dt);
        }

        void StartTrack(string key, float fade)
        {
            if (!director.Bank.TryGet(key, out var cue)) return;
            var next = current == a ? b : a;
            if (current != null && current.Active) FadeTo(current, 0f, fade);
            Stop(next);
            var track = cue.Pick();
            if (track.clip == null) return;

            double start = AudioSettings.dspTime + ScheduleLead;
            double introLength = 0;
            if (track.intro != null)
            {
                next.intro.clip = track.intro;
                next.intro.loop = false;
                next.intro.PlayScheduled(start);
                introLength = (double)track.intro.samples / track.intro.frequency;
            }
            bool playlist = cue.clips.Count > 1;
            next.body.clip = track.clip;
            next.body.loop = !playlist;
            next.body.PlayScheduled(start + introLength);
            next.ends = playlist ? start + introLength + (double)track.clip.samples / track.clip.frequency : double.PositiveInfinity;
            next.cue = cue;
            next.level = 0f;
            FadeTo(next, 1f, current != null && current.Active ? fade : Mathf.Min(fade, director.Tuning.musicLoadFadeSeconds));
            current = next;
        }

        void FadeOutAll(float seconds)
        {
            FadeTo(a, 0f, seconds);
            FadeTo(b, 0f, seconds);
            current = null;
        }

        static void FadeTo(Deck d, float target, float seconds)
        {
            d.target = target;
            d.rate = seconds > 0f ? 1f / seconds : InstantRate;
        }

        void Step(Deck d, float dt)
        {
            if (!d.Active) return;
            d.level = Mathf.MoveTowards(d.level, d.target, d.rate * dt);
            if (d.level <= 0f && d.target <= 0f)
            {
                Stop(d);
                return;
            }
            // Equal power: the sum of two crossfading decks keeps its loudness.
            float volume = Mathf.Sin(d.level * Mathf.PI * 0.5f) * d.cue.settings.volume * duck;
            d.intro.volume = volume;
            d.body.volume = volume;
            if (d != current && !d.body.isPlaying && !d.intro.isPlaying && AudioSettings.dspTime > d.ends) Stop(d);
        }

        static void Stop(Deck d)
        {
            d.intro.Stop();
            d.body.Stop();
            d.intro.clip = null;
            d.body.clip = null;
            d.cue = null;
            d.level = 0f;
            d.target = 0f;
        }
    }
}
