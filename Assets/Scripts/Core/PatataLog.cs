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

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Run(string message)
        {
            if (Enabled) UnityEngine.Debug.Log("[Run] " + message);
        }
    }
}
