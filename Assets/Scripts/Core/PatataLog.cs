using System.Diagnostics;

namespace HotPatata
{
    /// <summary>
    /// Concise development logging (ARCHITECTURE §19). Compiled out of release builds,
    /// and switchable at runtime via <see cref="Enabled"/>.
    /// </summary>
    public static class PatataLog
    {
        public static bool Enabled = true;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Bomb(string message)
        {
            if (Enabled) UnityEngine.Debug.Log("[Bomb] " + message);
        }

        /// <summary>One summary line per throw (aim, assist, speed, closest approach, outcome) for tuning the pass feel.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Throw(string message)
        {
            if (Enabled) UnityEngine.Debug.Log("[Throw] " + message);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Run(string message)
        {
            if (Enabled) UnityEngine.Debug.Log("[Run] " + message);
        }
    }
}
