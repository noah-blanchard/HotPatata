using System.Linq;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>Shared pieces of the menu station boards (ARCHITECTURE §6.2).</summary>
    static class MenuParts
    {
        public const int RefreshMs = 200;   // how often a board re-reads the bootstrap's state (messages, lobby list)
        public const int AddressMaxLength = 64;

        /// <summary>A text field of the layout: its value, its length limit, and typed (not delayed) changes.</summary>
        public static TextField Field(TextField field, string value, int maxLength)
        {
            field.maxLength = maxLength;
            field.isDelayed = false;
            field.SetValueWithoutNotify(value ?? "");
            return field;
        }

        /// <summary>The level and spawn-point choices (the Level station); the spawn list follows the level.</summary>
        public static void LevelChoices(ChoiceRow level, ChoiceRow spawn, NetworkBootstrap bootstrap)
        {
            SyncLevelChoices(level, spawn, bootstrap);
            level.Changed += i =>
            {
                bootstrap.SceneIndex = i;
                SyncLevelChoices(level, spawn, bootstrap);
            };
            spawn.Changed += i => RunOptions.StartCheckpoint = i;
        }

        /// <summary>Shows the bootstrap's current level and spawn point (they may have changed since the rows were built).</summary>
        public static void SyncLevelChoices(ChoiceRow level, ChoiceRow spawn, NetworkBootstrap bootstrap)
        {
            level.SetOptions(bootstrap.GameplayScenes.Select((_, i) => LevelLabel(bootstrap.DisplayNameOf(i), bootstrap.MinPlayersOf(i), bootstrap.MaxPlayersOf(i))).ToList(), bootstrap.SceneIndex);
            spawn.SetOptions(NetworkBootstrap.SpawnLabels(bootstrap.SceneCheckpointCount), RunOptions.StartCheckpoint);
            spawn.style.display = bootstrap.SceneCheckpointCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// A level's name in the list, with its players when they are not the usual two to four: "The Long Climb (2 players)" for a
        /// level for exactly two, "PatataTemple (3+ players)", "Duo Run (2-3 players)".
        /// </summary>
        public static string LevelLabel(string name, int minPlayers, int maxPlayers = 4)
        {
            maxPlayers = UnityEngine.Mathf.Max(minPlayers, maxPlayers);
            if (maxPlayers == minPlayers) return $"{name} ({minPlayers} player{(minPlayers > 1 ? "s" : "")})";
            if (maxPlayers < 4) return $"{name} ({minPlayers}-{maxPlayers} players)";
            return minPlayers > 2 ? $"{name} ({minPlayers}+ players)" : name;
        }

        /// <summary>"PatataWilds, from CP3": the level and spawn point the host has picked.</summary>
        public static string LevelSummary(NetworkBootstrap bootstrap)
        {
            string level = LevelLabel(bootstrap.DisplayNameOf(bootstrap.SceneIndex), bootstrap.SceneMinPlayers, bootstrap.SceneMaxPlayers);
            return RunOptions.StartCheckpoint > 0 ? $"{level}, from CP{RunOptions.StartCheckpoint}" : level;
        }
    }
}
