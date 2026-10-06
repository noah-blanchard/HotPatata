using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Active while at least one player, carrier included, stands on it (PROJECT_SPEC §13.15). Stateless: it asks
    /// <see cref="PlayerZone.Collect"/> through its <see cref="Zone"/> every physics step. Only the host's answer drives
    /// the actuator (which is replicated); a client's answer is only used for the pad's own look.
    /// </summary>
    [RequireComponent(typeof(Zone))]
    public class PressurePlate : MonoBehaviour, ISignalSource, IObstacleState
    {
        readonly HashSet<Player> inside = new HashSet<Player>();
        Zone zone;

        public bool Active { get; private set; }
        float IObstacleState.Progress => Active ? 1f : 0f;

        void Awake() => zone = GetComponent<Zone>();

        void FixedUpdate() => Active = zone.CollectPlayers(inside) > 0;
    }
}
