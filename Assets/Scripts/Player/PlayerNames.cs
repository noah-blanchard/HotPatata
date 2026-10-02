using System.Globalization;
using System.Text;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Player names: the one typed in the menu (saved locally) and the rules every machine applies before showing
    /// one. Online, the host sanitises the owner's name once and replicates the result (<see cref="NetworkPlayer"/>);
    /// the lobby sanitises the session's player properties for display.
    /// </summary>
    public static class PlayerNames
    {
        public const int MaxLength = 16;
        /// <summary>Session player property that carries the typed name into the lobby.</summary>
        public const string SessionProperty = "name";

        const string PrefsKey = "HotPatata.PlayerName";

        static string processOverride;

        public static string Default(int slot) => "Player " + (Mathf.Abs(slot) + 1);

        /// <summary>
        /// Trimmed, whitespace collapsed, control/format characters and rich-text brackets removed, at most
        /// <see cref="MaxLength"/> characters. Falls back to "Player N" when nothing is left.
        /// </summary>
        public static string Sanitize(string raw, int slot)
        {
            if (string.IsNullOrEmpty(raw)) return Default(slot);

            var sb = new StringBuilder(raw.Length);
            bool pendingSpace = false;
            foreach (char c in raw)
            {
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = sb.Length > 0;
                    continue;
                }
                var category = char.GetUnicodeCategory(c);
                if (category == UnicodeCategory.Control || category == UnicodeCategory.Format || c == '<' || c == '>') continue;

                if (pendingSpace) sb.Append(' ');
                pendingSpace = false;
                sb.Append(c);
            }

            if (sb.Length > MaxLength)
            {
                int cut = MaxLength;
                if (char.IsHighSurrogate(sb[cut - 1])) cut--;   // never split a surrogate pair
                sb.Length = cut;
            }

            string name = sb.ToString().TrimEnd();
            return name.Length == 0 ? Default(slot) : name;
        }

        /// <summary>The name typed in the menu (may be empty). <c>-patataName</c> overrides it for this process.</summary>
        public static string Local
        {
            get => processOverride ?? PlayerPrefs.GetString(PrefsKey, "");
            set
            {
                PlayerPrefs.SetString(PrefsKey, value ?? "");
                PlayerPrefs.Save();
            }
        }

        /// <summary>The name this process shares online. Bots keep "Player N" unless <c>-patataName</c> names them.</summary>
        public static string Shared => !PlayerBot.Enabled || processOverride != null ? Local : "";

        /// <summary>Command line: two instances on one machine share PlayerPrefs, so each can be given its own name.</summary>
        public static void OverrideForThisProcess(string name) => processOverride = name ?? "";
    }
}
