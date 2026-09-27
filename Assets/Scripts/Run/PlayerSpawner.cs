using Unity.Netcode;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Creates the players. Offline it builds the two-player local test rig; online the host spawns exactly
    /// one networked Player per connection (owned by that client) and frees the slot when they leave.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PlayerSpawner : MonoBehaviour
    {
        static readonly Color[] Palette =
        {
            new Color(1f, 0.55f, 0.1f), new Color(0.2f, 0.8f, 1f), new Color(0.6f, 1f, 0.3f), new Color(1f, 0.4f, 0.8f)
        };

        [SerializeField] GameObject playerPrefab;
        [SerializeField, Range(1, 4)] int offlinePlayers = 2;
        [SerializeField, Range(1, 4)] int maxPlayers = 4;

        void Awake()
        {
            if (!NetMode.IsNetworked) SpawnOfflineRig();
        }

        void Start()
        {
            if (!NetMode.IsNetworked || !NetworkManager.Singleton.IsServer) return;

            var nm = NetworkManager.Singleton;
            nm.OnClientConnectedCallback += SpawnFor;
            foreach (ulong id in nm.ConnectedClientsIds) SpawnFor(id);
        }

        void OnDestroy()
        {
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.OnClientConnectedCallback -= SpawnFor;
        }

        // ------------------------------------------------------------------ offline

        void SpawnOfflineRig()
        {
            for (int i = 0; i < offlinePlayers; i++)
            {
                var go = Instantiate(playerPrefab, SpawnPoint(i), Quaternion.identity);
                go.name = "Player_" + (i + 1);
                go.GetComponent<Player>().Configure(i, "Player " + (i + 1), Palette[i]);
            }
        }

        // ------------------------------------------------------------------ online (host only)

        void SpawnFor(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null) return;

            int slot = FirstFreeSlot();
            if (slot < 0)
            {
                Debug.LogWarning($"[Run] Server full, rejecting client {clientId}");
                nm.DisconnectClient(clientId);
                return;
            }

            var go = Instantiate(playerPrefab, SpawnPoint(slot), Quaternion.identity);
            go.GetComponent<NetworkPlayer>().InitialSlot = slot;
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, destroyWithScene: true);
            BeepLog.Run($"Spawned player slot {slot} for client {clientId}");
        }

        static int FirstFreeSlot()
        {
            for (int slot = 0; slot < 4; slot++)
            {
                bool used = false;
                foreach (var p in Player.All)
                    if (p.PlayerId == slot && p.Net != null && p.Net.IsSpawned) { used = true; break; }
                if (!used) return slot;
            }
            return -1;
        }

        static Vector3 SpawnPoint(int slot)
        {
            foreach (var s in FindObjectsByType<PlayerSpawn>(FindObjectsSortMode.None))
                if (s.Slot == slot && s.transform.parent != null && s.transform.parent.name == "SectionRoot") return s.transform.position;
            return Vector3.zero;
        }
    }
}
