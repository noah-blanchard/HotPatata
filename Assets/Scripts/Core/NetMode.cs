using Unity.Netcode;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Answers "are we networked, and do we own the game rules?" for code that must work both offline
    /// (local sandbox, tests) and online. Offline counts as authority, so the same rules code runs in both.
    /// </summary>
    public static class NetMode
    {
        public static bool IsNetworked => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        /// <summary>True offline, and on the host/server when online. Only the authority decides gameplay.</summary>
        public static bool IsAuthority => !IsNetworked || NetworkManager.Singleton.IsServer;

        public static bool IsRemoteClient => IsNetworked && !NetworkManager.Singleton.IsServer;

        /// <summary>Shared clock: the server's network time when online, local time offline.</summary>
        public static double ServerTime => IsNetworked ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
    }
}
