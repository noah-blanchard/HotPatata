using Unity.Netcode;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Creates the players. Offline it builds the two-player local test rig; online the host spawns exactly
    /// one networked Player per connection (owned by that client) and frees the slot when they leave.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PlayerSpawner : MonoBehaviour
    {
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
                GetStartPose(i, out var position, out var rotation);
                Instantiate(playerPrefab, position, rotation).GetComponent<Player>().ConfigureSlot(i);
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

            GetStartPose(slot, out var position, out var rotation);
            var go = Instantiate(playerPrefab, position, rotation);
            go.GetComponent<NetworkPlayer>().InitialSlot = slot;
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, destroyWithScene: true);
            PatataLog.Run($"Spawned player slot {slot} for client {clientId}");
        }

        int FirstFreeSlot()
        {
            int slots = Mathf.Min(maxPlayers, Player.MaxSlots);
            for (int slot = 0; slot < slots; slot++)
            {
                bool used = false;
                foreach (var p in Player.All)
                    if (p.PlayerId == slot && p.Net != null && p.Net.IsSpawned) { used = true; break; }
                if (!used) return slot;
            }
            return -1;
        }

        /// <summary>
        /// The run's starting spawn for a slot: a <see cref="PlayerSpawn"/> that does not belong to a checkpoint
        /// (checkpoint spawns are respawn points). RunManager teleports everyone there again when the run starts.
        /// </summary>
        static void GetStartPose(int slot, out Vector3 position, out Quaternion rotation)
        {
            foreach (var s in FindObjectsByType<PlayerSpawn>(FindObjectsSortMode.None))
            {
                if (s.Slot != slot || s.GetComponentInParent<Checkpoint>() != null) continue;
                position = s.transform.position;
                rotation = s.transform.rotation;
                return;
            }
            position = Vector3.zero;
            rotation = Quaternion.identity;
        }
    }
}
