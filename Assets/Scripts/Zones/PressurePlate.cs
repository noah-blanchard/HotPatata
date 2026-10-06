using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Active while at least one player stands on it (PROJECT_SPEC §13.15). With <see cref="countCarrier"/> off it is a
    /// hands-free plate (§13.19): the carrier standing on it does not count, so whoever holds the way open must have passed
    /// the bomb first. Stateless: it asks <see cref="PlayerZone.Collect"/> through its <see cref="Zone"/> every physics step.
    /// Only the host's answer drives the actuator (which is replicated); a client's answer is only used for the pad's own look.
    /// </summary>
    [RequireComponent(typeof(Zone))]
    public class PressurePlate : MonoBehaviour, ISignalSource, IObstacleState
    {
        readonly HashSet<Player> inside = new HashSet<Player>();
        Zone zone;

        [SerializeField, Tooltip("Off: a hands-free plate, the player carrying the bomb does not hold it down.")]
        bool countCarrier = true;

        public bool Active { get; private set; }
        public bool CountsCarrier => countCarrier;
        float IObstacleState.Progress => Active ? 1f : 0f;

        void Awake() => zone = GetComponent<Zone>();

        void FixedUpdate()
        {
            zone.CollectPlayers(inside);
            var bomb = BombController.Instance;
            Active = IsHeld(inside, !countCarrier && bomb != null ? bomb.Carrier : null);
        }

        /// <summary>Is the plate held by <paramref name="standing"/>, the <paramref name="excluded"/> player (the carrier on a hands-free plate) not counting? Pure (EditMode tested).</summary>
        public static bool IsHeld(ICollection<Player> standing, Player excluded) =>
            standing.Count > (excluded != null && standing.Contains(excluded) ? 1 : 0);
    }
}
