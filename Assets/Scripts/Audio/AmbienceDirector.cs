using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata
{
    /// <summary>
    /// The world's ambience (ARCHITECTURE §26, #49), in layers, each a folder with Beds (loops, all layered, flat) and Scatter
    /// (one-shots such as bird calls, played now and then at random spots around the listener):
    ///   the menu: Ambience/Menu.
    ///   a course: its own folder (everywhere), its environment (the section's, from <see cref="CourseAudio"/>, or the
    ///   <see cref="AmbienceZone"/> the listener is in), and its act's folder on top. An act's Replace folder takes the
    ///   environment's place for that act.
    /// Layers fade in and out over <see cref="GameTuning.ambienceCrossfadeSeconds"/>. Never follows a player's movement
    /// (the fixed emitters placed by the builders are <see cref="AmbientLoop"/>s). Presentation only.
    /// </summary>
    public sealed class AmbienceDirector : MonoBehaviour
    {
        AudioDirector director;
        readonly Dictionary<string, List<LoopHandle>> beds = new Dictionary<string, List<LoopHandle>>();
        readonly Dictionary<string, float> nextScatter = new Dictionary<string, float>();
        readonly HashSet<string> wantedBeds = new HashSet<string>();
        readonly HashSet<string> wantedScatter = new HashSet<string>();
        readonly List<string> scratch = new List<string>();

        public void Init(AudioDirector owner) => director = owner;

        void Update()
        {
            Desired();
            UpdateBeds(director.Tuning.ambienceCrossfadeSeconds);
            UpdateScatter();
        }

        // ------------------------------------------------------------------ which layers

        void Desired()
        {
            wantedBeds.Clear();
            wantedScatter.Clear();
            var bank = director.Bank;
            if (RunManager.Instance == null)
            {
                Want(CueKeys.AmbienceMenu);
                return;
            }

            string scene = SceneManager.GetActiveScene().name;
            int act = CourseAudio.CurrentAct;
            Want(CueKeys.AmbienceCourse(scene));
            Want(CueKeys.AmbienceAct(scene, act));

            var listener = director.Listener;
            string environment = listener != null ? AmbienceZone.At(listener.position) : null;
            if (environment == null && CourseAudio.Current != null) environment = CourseAudio.Current.EnvironmentAt(CourseAudio.CurrentCheckpointId);
            string environmentLayer = string.IsNullOrEmpty(environment) ? null : CueKeys.AmbienceEnvironment(environment);
            string replace = CueKeys.AmbienceActReplace(scene, act);

            string replaceBeds = CueKeys.Beds(replace), replaceScatter = CueKeys.Scatter(replace);
            if (bank.Has(replaceBeds)) wantedBeds.Add(replaceBeds);
            else if (environmentLayer != null) wantedBeds.Add(CueKeys.Beds(environmentLayer));
            if (bank.Has(replaceScatter)) wantedScatter.Add(replaceScatter);
            else if (environmentLayer != null) wantedScatter.Add(CueKeys.Scatter(environmentLayer));
        }

        void Want(string layer)
        {
            wantedBeds.Add(CueKeys.Beds(layer));
            wantedScatter.Add(CueKeys.Scatter(layer));
        }

        // ------------------------------------------------------------------ beds

        void UpdateBeds(float fade)
        {
            scratch.Clear();
            foreach (var key in beds.Keys) if (!wantedBeds.Contains(key)) scratch.Add(key);
            foreach (var key in scratch)
            {
                foreach (var loop in beds[key]) loop.Stop(fade);
                beds.Remove(key);
            }

            foreach (var key in wantedBeds)
            {
                if (beds.ContainsKey(key) || !director.Bank.TryGet(key, out var cue)) continue;
                var loops = new List<LoopHandle>();
                foreach (var clip in cue.clips)
                    if (clip.clip != null) loops.Add(director.LoopClip(cue, clip.clip, null, 1f, fade));
                beds[key] = loops;
            }
        }

        // ------------------------------------------------------------------ scatter

        void UpdateScatter()
        {
            scratch.Clear();
            foreach (var key in nextScatter.Keys) if (!wantedScatter.Contains(key)) scratch.Add(key);
            foreach (var key in scratch) nextScatter.Remove(key);

            var listener = director.Listener;
            float now = Time.unscaledTime;
            foreach (var key in wantedScatter)
            {
                if (!director.Bank.TryGet(key, out var cue)) continue;
                var s = cue.settings;
                if (!nextScatter.TryGetValue(key, out float due))
                {
                    nextScatter[key] = now + RandomIn(s.scatterInterval);   // a new layer waits before its first call
                    continue;
                }
                if (now < due) continue;
                nextScatter[key] = now + RandomIn(s.scatterInterval);
                if (listener == null) continue;
                var flat = Random.insideUnitCircle.normalized * RandomIn(s.scatterDistance);
                var at = listener.position + new Vector3(flat.x, RandomIn(s.scatterHeight), flat.y);
                director.Play(key, at);
            }
        }

        static float RandomIn(Vector2 range) => Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }
}
