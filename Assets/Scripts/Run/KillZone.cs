using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A lethal volume (trigger). A bomb entering it explodes (handled by BombController), so a bomb can
    /// never fall out of the level and vanish. A player entering it fails the section for the whole team,
    /// which resets everyone to the checkpoint: deterministic, and never a soft-lock.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KillZone : MonoBehaviour
    {
        void Reset() => GetComponent<Collider>().isTrigger = true;

        void Awake() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<Player>();
            if (player == null || RunManager.Instance == null) return;

            if (NetMode.IsNetworked && NetMode.IsAuthority && !player.IsLocal) NoteRemoteHit(player);
            RunManager.Instance.FailSection("PlayerFell", $"player={player} zone={name}");
        }

        // Diagnostics (docs/netcode-deterministic-plan.md stage 0): the host judged a remote player from its own copy.
        void NoteRemoteHit(Player player)
        {
            bool moving = GetComponentInParent<MovingPlatform>() != null || GetComponentInParent<RotatingObstacle>() != null ||
                          GetComponentInParent<SignalActuator>() != null;
            if (moving) SyncStats.HazardTriggerHits++;
            else SyncStats.StaticKills++;
            int rtt = player.Net != null && player.Net.IsSpawned ? NetMode.RttMsFor(player.Net.OwnerClientId) : 0;
            PatataLog.Sync($"hazard trigger {(moving ? "moving" : "static")} zone={name} player={player} rtt={rtt}ms");
        }
    }
}
