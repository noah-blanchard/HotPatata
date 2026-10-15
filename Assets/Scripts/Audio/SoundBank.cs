using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace HotPatata
{
    /// <summary>
    /// Every sound of the game, generated from the folders of Assets/Audio by <c>SoundBankBuilder</c> (ARCHITECTURE §26):
    /// one <see cref="Cue"/> per folder that holds sound files, the mixer groups each kind of sound plays on, and which
    /// footstep surface each material is. Rebuilt on its own whenever a sound file is added, moved or removed; never edit
    /// the cue list by hand (a cue's settings, and a material's surface, may be changed in the Inspector and are kept).
    /// Lives in a Resources folder so <see cref="AudioDirector"/> finds it from any scene.
    /// </summary>
    public class SoundBank : ScriptableObject
    {
        /// <summary>Resources path of the bank (Assets/Audio/Resources/HotPatataSoundBank.asset).</summary>
        public const string ResourcePath = "HotPatataSoundBank";

        [Serializable]
        public struct SurfaceMaterial
        {
            public Material material;
            [Tooltip("A footstep folder name: SFX/Player/Footsteps/<surface>.")]
            public string surface;
        }

        [SerializeField] GameTuning tuning;

        [Header("Mixer (HotPatataMixer, ARCHITECTURE §6.1)")]
        [SerializeField] AudioMixer mixer;
        [SerializeField] AudioMixerGroup music;
        [SerializeField] AudioMixerGroup ambience;
        [SerializeField] AudioMixerGroup sfx;
        [SerializeField] AudioMixerGroup bomb;
        [SerializeField] AudioMixerGroup movement;
        [SerializeField] AudioMixerGroup world;
        [SerializeField] AudioMixerGroup ui;
        [SerializeField, Tooltip("The normal mix.")] AudioMixerSnapshot normal;
        [SerializeField, Tooltip("The pause menu's mix: muffled, the bomb and the menus left clear.")] AudioMixerSnapshot paused;

        [Header("Generated from the folders (settings are kept on rebuild)")]
        [SerializeField] List<Cue> cues = new List<Cue>();
        [SerializeField] List<SurfaceMaterial> surfaces = new List<SurfaceMaterial>();

        Dictionary<string, Cue> byKey;
        Dictionary<Material, string> bySurface;

        public GameTuning Tuning => tuning;
        public AudioMixer Mixer => mixer;
        public AudioMixerGroup MusicGroup => music != null ? music : sfx;
        public AudioMixerGroup AmbienceGroup => ambience != null ? ambience : sfx;
        public AudioMixerSnapshot Normal => normal;
        public AudioMixerSnapshot Paused => paused;
        public IReadOnlyList<Cue> Cues => cues;
        public IReadOnlyList<SurfaceMaterial> Surfaces => surfaces;

        void OnEnable()
        {
            byKey = null;
            bySurface = null;
            if (cues != null) foreach (var cue in cues) cue.ResetRuntime();
        }

        /// <summary>The cue of folder <paramref name="key"/>, if it holds at least one sound (case is ignored).</summary>
        public bool TryGet(string key, out Cue cue)
        {
            cue = null;
            if (string.IsNullOrEmpty(key)) return false;
            if (byKey == null)
            {
                byKey = new Dictionary<string, Cue>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in cues) if (c != null && !c.IsEmpty && !string.IsNullOrEmpty(c.key)) byKey[c.key] = c;
            }
            return byKey.TryGetValue(key, out cue);
        }

        public bool Has(string key) => TryGet(key, out _);

        /// <summary>The first key of <paramref name="keys"/> that holds a sound, or null.</summary>
        public string FirstPresent(IReadOnlyList<string> keys)
        {
            if (keys == null) return null;
            for (int i = 0; i < keys.Count; i++) if (Has(keys[i])) return keys[i];
            return null;
        }

        /// <summary>The footstep surface of <paramref name="material"/>, or null when it is not mapped.</summary>
        public string SurfaceOf(Material material)
        {
            if (material == null) return null;
            if (bySurface == null)
            {
                bySurface = new Dictionary<Material, string>();
                foreach (var s in surfaces) if (s.material != null && !string.IsNullOrEmpty(s.surface)) bySurface[s.material] = s.surface;
            }
            return bySurface.TryGetValue(material, out var surface) ? surface : null;
        }

        /// <summary>The mixer group a cue plays on, from its folder: Music, Ambience, SFX/Bomb, SFX/Player, SFX/UI, other SFX.</summary>
        public AudioMixerGroup GroupFor(string key)
        {
            var group = Route(key) switch
            {
                AudioRoute.Music => music,
                AudioRoute.Ambience => ambience,
                AudioRoute.Bomb => bomb,
                AudioRoute.Movement => movement,
                AudioRoute.Ui => ui,
                _ => world,
            };
            return group != null ? group : sfx;
        }

        public enum AudioRoute { Music, Ambience, Bomb, Movement, Ui, World }

        /// <summary>Pure: which group a folder belongs to (unit tested).</summary>
        public static AudioRoute Route(string key)
        {
            if (string.IsNullOrEmpty(key)) return AudioRoute.World;
            if (key.StartsWith("Music/", StringComparison.OrdinalIgnoreCase)) return AudioRoute.Music;
            if (key.StartsWith("Ambience/", StringComparison.OrdinalIgnoreCase)) return AudioRoute.Ambience;
            if (key.StartsWith("SFX/Bomb/", StringComparison.OrdinalIgnoreCase)) return AudioRoute.Bomb;
            if (key.StartsWith("SFX/Player/", StringComparison.OrdinalIgnoreCase)) return AudioRoute.Movement;
            if (key.StartsWith("SFX/UI/", StringComparison.OrdinalIgnoreCase)) return AudioRoute.Ui;
            return AudioRoute.World;
        }

        /// <summary>Editor only (SoundBankBuilder): the bank's references.</summary>
        public void ConfigureMixer(GameTuning tuning, AudioMixer mixer, AudioMixerGroup music, AudioMixerGroup ambience, AudioMixerGroup sfx,
                                   AudioMixerGroup bomb, AudioMixerGroup movement, AudioMixerGroup world, AudioMixerGroup ui,
                                   AudioMixerSnapshot normal, AudioMixerSnapshot paused)
        {
            this.tuning = tuning;
            this.mixer = mixer;
            this.music = music;
            this.ambience = ambience;
            this.sfx = sfx;
            this.bomb = bomb;
            this.movement = movement;
            this.world = world;
            this.ui = ui;
            this.normal = normal;
            this.paused = paused;
        }

        /// <summary>Editor only (SoundBankBuilder): the scanned cues and the material surfaces.</summary>
        public void ConfigureContent(List<Cue> cues, List<SurfaceMaterial> surfaces)
        {
            this.cues = cues;
            this.surfaces = surfaces;
            byKey = null;
            bySurface = null;
        }
    }
}
