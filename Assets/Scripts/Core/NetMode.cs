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

        /// <summary>
        /// Round-trip time in ms: a client's own ping to the host; on the host, the worst ping among its clients.
        /// 0 offline or when unknown.
        /// </summary>
        public static int RttMs
        {
            get
            {
                if (!IsNetworked) return 0;
                var nm = NetworkManager.Singleton;
                var transport = nm.NetworkConfig.NetworkTransport as Unity.Netcode.Transports.UTP.UnityTransport;
                if (transport == null) return 0;
                if (!nm.IsServer) return (int)transport.GetCurrentRtt(NetworkManager.ServerClientId);

                ulong worst = 0;
                foreach (ulong id in nm.ConnectedClientsIds)
                    if (id != nm.LocalClientId) worst = System.Math.Max(worst, transport.GetCurrentRtt(id));
                return (int)worst;
            }
        }

        /// <summary>Shared clock: the server's network time when online, local time offline.</summary>
        public static double ServerTime => IsNetworked ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
    }
}
