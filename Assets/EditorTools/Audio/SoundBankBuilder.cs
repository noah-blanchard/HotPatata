using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace HotPatata.Editor
{
    /// <summary>
    /// Builds the <see cref="SoundBank"/> from the folders of Assets/Audio (ARCHITECTURE §26): each folder holding sound files
    /// becomes a cue (its key is the folder's path under Assets/Audio), with its settings from <see cref="CueDefaults"/> the
    /// first time and as left in the bank's Inspector after that. Music files named X_intro and X_loop make one track. It also
    /// maps every material of the project to a footstep surface by its name (kept once set, so a fix by hand stays).
    /// Runs on its own a moment after any sound file is added, moved or removed under Assets/Audio; the menu forces it.
    /// </summary>
    public class SoundBankBuilder : AssetPostprocessor
    {
        public const string AudioRoot = "Assets/Audio";
        public const string BankPath = "Assets/Audio/Resources/HotPatataSoundBank.asset";
        const string MixerPath = "Assets/Audio/HotPatataMixer.mixer";
        const string TuningPath = "Assets/ScriptableObjects/Tuning/GameTuning.asset";
        const string LegacyAmbient = "Assets/Audio/Ambient";   // the generated placeholders, referenced by the builders directly
        const string IntroSuffix = "_intro", LoopSuffix = "_loop";
        /// <summary>A generated stand-in (NatureAudioFactory): dropped from its folder as soon as the folder holds a real sound.</summary>
        public const string PlaceholderPrefix = "placeholder_";

        static readonly string[] SoundExtensions = { ".wav", ".ogg", ".mp3", ".aif", ".aiff", ".flac" };

        // A material name's words, in order: the first that matches gives the surface ("RockPath" is rock, not a path).
        static readonly (string word, string surface)[] SurfaceWords =
        {
            ("water", "Water"), ("mud", "Mud"), ("gravel", "Gravel"), ("riverbed", "Gravel"),
            ("rock", "Rock"), ("mountainside", "Rock"), ("cliff", "Rock"), ("granite", "Rock"), ("boulder", "Rock"), ("pebble", "Gravel"),
            ("grass", "Grass"), ("meadow", "Grass"), ("lawn", "Grass"),
            ("leaf", "Leaves"), ("leaves", "Leaves"), ("litter", "Leaves"), ("fern", "Leaves"),
            ("plank", "Wood"), ("wood", "Wood"), ("bark", "Wood"), ("log", "Wood"), ("timber", "Wood"), ("crate", "Wood"),
            ("trunk", "Wood"), ("stump", "Wood"), ("root", "Wood"), ("ladder", "Wood"), ("barrel", "Wood"),
            ("temple", "Stone"), ("stone", "Stone"), ("brick", "Stone"), ("tile", "Stone"), ("marble", "Stone"), ("palenque", "Stone"), ("carvedplate", "Stone"),
            ("concrete", "Concrete"), ("asphalt", "Concrete"),
            ("metal", "Metal"), ("steel", "Metal"), ("iron", "Metal"), ("grat", "Metal"), ("rust", "Metal"), ("truss", "Metal"),
            ("pipe", "Metal"), ("plate", "Metal"), ("industrial", "Concrete"),
            ("trail", "Dirt"), ("path", "Dirt"), ("dirt", "Dirt"), ("soil", "Dirt"), ("forestfloor", "Dirt"), ("sand", "Dirt"),
            ("moss", "Grass"), ("terrain", "Grass"), ("ground", "Dirt"),
        };

        static bool scheduled;

        // ------------------------------------------------------------------ automatic rebuild

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(Concerns)) Schedule();
        }

        // A sound file, or a folder (a folder of sounds deleted at once), under Assets/Audio.
        static bool Concerns(string path)
        {
            if (!path.StartsWith(AudioRoot + "/") || path.StartsWith(LegacyAmbient)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == "" || SoundExtensions.Contains(ext);
        }

        static void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += () =>
            {
                scheduled = false;
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    Schedule();
                    return;
                }
                Build();
            };
        }

        // ------------------------------------------------------------------ the bank

        [MenuItem("HotPatata/Audio/Rebuild Sound Bank")]
        public static void Build()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null)
            {
                EnsureFolder(Path.GetDirectoryName(BankPath).Replace('\\', '/'));
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }

            ConfigureMixer(bank);
            var cues = ScanCues(bank);
            var surfaces = MapSurfaces(bank);
            bank.ConfigureContent(cues, surfaces);
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            int sounds = cues.Sum(c => c.clips.Count);
            Debug.Log($"[SoundBank] {cues.Count} sound folders, {sounds} sounds, {surfaces.Count} materials with a footstep surface.");
        }

        static void ConfigureMixer(SoundBank bank)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath);
            AudioMixerGroup Group(string path)
            {
                var found = mixer != null ? mixer.FindMatchingGroups(path) : null;
                var group = found != null ? found.FirstOrDefault(g => g.name == path.Split('/').Last()) : null;
                if (group == null) Debug.LogWarning($"[SoundBank] the mixer has no group {path} (ARCHITECTURE §6.1)");
                return group;
            }
            bank.ConfigureMixer(tuning, mixer, Group("Master/Music"), Group("Master/Ambience"), Group("Master/SFX"),
                                Group("Master/SFX/Bomb"), Group("Master/SFX/Movement"), Group("Master/SFX/World"), Group("Master/UI"),
                                mixer != null ? mixer.FindSnapshot("Snapshot") : null, mixer != null ? mixer.FindSnapshot("Paused") : null);
        }

        /// <summary>Every folder of Assets/Audio holding sounds, as a cue; settings kept from the bank where it had the folder.</summary>
        static List<Cue> ScanCues(SoundBank bank)
        {
            var previous = new Dictionary<string, CueSettings>(StringComparer.OrdinalIgnoreCase);
            foreach (var cue in bank.Cues)
                if (cue != null && !string.IsNullOrEmpty(cue.key) && cue.settings != null) previous[cue.key] = cue.settings;

            var byFolder = new SortedDictionary<string, List<AudioClip>>(StringComparer.OrdinalIgnoreCase);
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(LegacyAmbient + "/")) continue;
                string key = KeyOf(path);
                if (key == null) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                if (!byFolder.TryGetValue(key, out var list)) byFolder[key] = list = new List<AudioClip>();
                list.Add(clip);
            }

            var cues = new List<Cue>();
            foreach (var pair in byFolder)
            {
                if (pair.Value.Any(c => !IsPlaceholder(c))) pair.Value.RemoveAll(IsPlaceholder);
                var cue = new Cue
                {
                    key = pair.Key,
                    clips = pair.Key.StartsWith("Music/", StringComparison.OrdinalIgnoreCase)
                        ? PairTracks(pair.Value, pair.Key)
                        : pair.Value.OrderBy(c => c.name, StringComparer.OrdinalIgnoreCase).Select(c => new CueClip { clip = c }).ToList(),
                    settings = previous.TryGetValue(pair.Key, out var kept) ? kept.Clone() : CueDefaults.For(pair.Key),
                };
                if (cue.clips.Count > 0) cues.Add(cue);
            }
            return cues;
        }

        static bool IsPlaceholder(AudioClip clip) => clip.name.StartsWith(PlaceholderPrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>Pure: a sound file's cue key, its folder under Assets/Audio (null for a file at the root).</summary>
        public static string KeyOf(string assetPath)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (folder == null || !folder.StartsWith(AudioRoot + "/")) return null;
            return folder.Substring(AudioRoot.Length + 1);
        }

        /// <summary>Music: X_intro + X_loop (or X) are one track; an intro with no loop is skipped.</summary>
        public static List<CueClip> PairTracks(IEnumerable<AudioClip> clips, string key)
        {
            var list = clips.ToList();
            var intros = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in list.Where(c => c.name.EndsWith(IntroSuffix, StringComparison.OrdinalIgnoreCase)))
                intros[c.name.Substring(0, c.name.Length - IntroSuffix.Length)] = c;   // two files of one name: the last wins
            var used = new HashSet<AudioClip>();
            var tracks = new List<CueClip>();
            foreach (var clip in list.Where(c => !c.name.EndsWith(IntroSuffix, StringComparison.OrdinalIgnoreCase)).OrderBy(c => c.name, StringComparer.OrdinalIgnoreCase))
            {
                string name = clip.name.EndsWith(LoopSuffix, StringComparison.OrdinalIgnoreCase)
                    ? clip.name.Substring(0, clip.name.Length - LoopSuffix.Length) : clip.name;
                intros.TryGetValue(name, out var intro);
                if (intro != null) used.Add(intro);
                tracks.Add(new CueClip { clip = clip, intro = intro });
            }
            foreach (var intro in intros.Values.Where(i => !used.Contains(i)))
                Debug.LogWarning($"[SoundBank] {key}: {intro.name} has no {LoopSuffix} track to hand over to; it is not played.");
            return tracks;
        }

        // ------------------------------------------------------------------ footstep surfaces

        static List<SoundBank.SurfaceMaterial> MapSurfaces(SoundBank bank)
        {
            var result = new List<SoundBank.SurfaceMaterial>();
            var seen = new HashSet<Material>();
            foreach (var kept in bank.Surfaces)
                if (kept.material != null && seen.Add(kept.material)) result.Add(kept);
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || seen.Contains(material)) continue;
                string surface = SurfaceFromName(material.name);
                if (surface == null) continue;
                seen.Add(material);
                result.Add(new SoundBank.SurfaceMaterial { material = material, surface = surface });
            }
            return result.OrderBy(s => s.material.name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Pure: the footstep surface a material's name suggests, or null.</summary>
        public static string SurfaceFromName(string materialName)
        {
            string name = materialName.ToLowerInvariant().Replace("_", "").Replace(" ", "");
            foreach (var (word, surface) in SurfaceWords) if (name.Contains(word)) return surface;
            return null;
        }

        // ------------------------------------------------------------------ folders

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
