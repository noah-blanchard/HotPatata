using Unity.Netcode;
using UnityEngine;

namespace HotPatata
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
                if (!nm.IsServer) return RttMsFor(NetworkManager.ServerClientId);

                int worst = 0;
                foreach (ulong id in nm.ConnectedClientsIds)
                    if (id != nm.LocalClientId) worst = Mathf.Max(worst, RttMsFor(id));
                return worst;
            }
        }

        /// <summary>Round-trip time in ms to one connection (on the host: a client; on a client: the server). 0 if unknown.</summary>
        public static int RttMsFor(ulong clientId)
        {
            if (!IsNetworked) return 0;
            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as Unity.Netcode.Transports.UTP.UnityTransport;
            return transport != null ? (int)transport.GetCurrentRtt(clientId) : 0;
        }

        /// <summary>Shared clock: the server's network time when online, local time offline.</summary>
        public static double ServerTime => IsNetworked ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
    }
}
