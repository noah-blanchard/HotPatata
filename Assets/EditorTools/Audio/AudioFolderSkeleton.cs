using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Creates every sound folder the game listens to (ARCHITECTURE §26), so each slot is there to be filled: the fixed ones of
    /// <see cref="CueKeys"/>, each course's music and ambience with one folder per act, and On / Off / Loop for every obstacle
    /// system. An empty folder is silent. Each folder holds a hidden .gitkeep (Unity ignores it) so git keeps it.
    /// </summary>
    public static class AudioFolderSkeleton
    {
        const string NetworkPrefab = "Assets/Prefabs/Network/NetworkManager.prefab";
        const string KeepFile = ".gitkeep";

        /// <summary>Acts of each course (its builder's sections); a course not listed gets its own folders and no act folders.</summary>
        static readonly Dictionary<string, int> CourseActs = new Dictionary<string, int>();

        [MenuItem("HotPatata/Audio/Create Folder Skeleton")]
        public static void Create()
        {
            var keys = new List<string>(CueKeys.Fixed());
            foreach (var scene in Courses())
                keys.AddRange(CueKeys.ForCourse(scene, CourseActs.TryGetValue(scene, out int acts) ? acts : 0));
            foreach (var type in ObstacleTypes())
            {
                keys.Add(CueKeys.Obstacle(type, CueKeys.ObstacleOn));
                keys.Add(CueKeys.Obstacle(type, CueKeys.ObstacleOff));
                keys.Add(CueKeys.Obstacle(type, CueKeys.ObstacleLoop));
            }

            int made = 0;
            foreach (var key in keys.Distinct())
            {
                string folder = Path.Combine(SoundBankBuilder.AudioRoot, key);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    made++;
                }
                string keep = Path.Combine(folder, KeepFile);
                if (!File.Exists(keep)) File.WriteAllText(keep, "");
            }
            AssetDatabase.Refresh();
            Debug.Log($"[Audio] {made} new sound folders ({keys.Count} in all) under {SoundBankBuilder.AudioRoot}. Drop .wav/.ogg/.mp3 files in them.");
        }

        static IEnumerable<string> Courses()
        {
            var boot = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefab)?.GetComponent<NetworkBootstrap>();
            return boot != null ? (IEnumerable<string>)boot.GameplayScenes : CourseActs.Keys;
        }

        // Every obstacle system ObstacleSounds listens to (checkpoints and tubes have their own sounds).
        static IEnumerable<string> ObstacleTypes() =>
            TypeCache.GetTypesDerivedFrom<IObstacleState>()
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract && t != typeof(Checkpoint) && t != typeof(BombTransit))
                .Select(t => t.Name)
                .OrderBy(n => n);
    }
}
