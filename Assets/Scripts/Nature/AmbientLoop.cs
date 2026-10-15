using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (ARCHITECTURE §25.3, §26): a fixed world ambience loop (river, falls, birds, wind) placed by a builder.
    /// It plays a sound from its emitter folder (Assets/Audio/Ambience/Emitters/&lt;emitter&gt;) when that folder holds one, else
    /// the generated placeholder it was built with, on the Ambience group, from a random point, so neighbouring sources playing
    /// the same clip never sound in step. Never a movement sound.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbientLoop : MonoBehaviour
    {
        [SerializeField, Tooltip("Emitter folder name (Ambience/Emitters/<name>): River, Waterfall, Wind, Birds... Empty = the clip as built.")]
        string emitter = "";

        void Start()
        {
            var source = GetComponent<AudioSource>();
            Adopt(source, emitter);
            if (source.clip == null) return;
            source.time = Random.Range(0f, source.clip.length);
            if (!source.isPlaying) source.Play();
        }

        /// <summary>
        /// Gives a builder-placed ambience source its emitter's sound (if the folder holds one) and the Ambience group.
        /// Also used by the campfire's crackle.
        /// </summary>
        public static void Adopt(AudioSource source, string emitter)
        {
            var director = AudioDirector.Instance;
            if (director == null) return;
            source.outputAudioMixerGroup = director.Bank.AmbienceGroup;
            if (string.IsNullOrEmpty(emitter)) return;
            var clip = Sfx.Pick(CueKeys.AmbienceEmitter(emitter), out _);
            if (clip == null) return;
            bool playing = source.isPlaying;
            source.clip = clip;
            if (playing) source.Play();
        }

        /// <summary>Editor only (NatureKit).</summary>
        public void Configure(string emitter) => this.emitter = emitter;
    }
}
