using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A lethal volume (trigger). A bomb entering it explodes (handled by BombController), so a bomb can
    /// never fall out of the level and vanish. A player entering it fails the section for the whole team,
    /// which resets everyone to the checkpoint: deterministic, and never a soft-lock.
    ///
    /// A zone under a time-driven mover (<see cref="ITimePosed"/>: a rotating bar, a crusher, a closing door) is a
    /// moving hazard. Online, the host does not judge a REMOTE player against it from its own copy, which is drawn about
    /// a round trip late: <see cref="HazardRewind"/> judges them at the time they saw it (docs/netcode-deterministic-plan.md
    /// §2.4). The host's own player, every static zone (water, void) and offline play keep the trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KillZone : MonoBehaviour
    {
        static readonly List<KillZone> moving = new List<KillZone>();

        /// <summary>Every enabled zone under a time-driven mover.</summary>
        public static IReadOnlyList<KillZone> Moving => moving;

        Collider zone;
        ITimePosed posed;

        /// <summary>The mover this zone rides on, or null for a static zone.</summary>
        public ITimePosed Posed => posed;
        public Collider Zone => zone;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void Awake()
        {
            zone = GetComponent<Collider>();
            zone.isTrigger = true;
            posed = GetComponentInParent<ITimePosed>();
        }

        void OnEnable()
        {
            if (posed != null && !moving.Contains(this)) moving.Add(this);
        }

        void OnDisable() => moving.Remove(this);

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<Player>();
            if (player == null || RunManager.Instance == null) return;

            if (NetMode.IsNetworked && NetMode.IsAuthority && !player.IsLocal)
            {
                int rtt = player.Net != null && player.Net.IsSpawned ? NetMode.RttMsFor(player.Net.OwnerClientId) : 0;
                if (posed != null)
                {
                    // The host's copy is a round trip late: only the rewound verdict counts. Logged to compare the two.
                    SyncStats.HazardTriggerHits++;
                    PatataLog.Sync($"hazard trigger (ignored, judged rewound) zone={name} player={player} rtt={rtt}ms");
                    return;
                }
                SyncStats.StaticKills++;
                PatataLog.Sync($"hazard static zone={name} player={player} rtt={rtt}ms");
            }
            RunManager.Instance.FailSection("PlayerFell", $"player={player} zone={name}");
        }
    }
}
