using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Active while at least one player stands on it (PROJECT_SPEC §13.15). With <see cref="countCarrier"/> off it is a
    /// hands-free plate (§13.19): the carrier standing on it does not count, nor, while the bomb flies or rides a tube, the
    /// player who threw it (§13.21: a bomb in flight is still its thrower's), so whoever holds the way open must have passed
    /// the bomb first. A heavy plate (§13.21) needs <see cref="requiredBodies"/> counted players at once; an hourglass plate
    /// (§13.22) stays active <see cref="memorySeconds"/> after the last one stepped off.
    /// It asks <see cref="PlayerZone.Collect"/> through its <see cref="Zone"/> every physics step. Only the host's answer
    /// drives the actuator (which is replicated); a client's answer is only used for the pad's own look, and an hourglass
    /// plate's release time is replicated (<see cref="NetworkPressurePlate"/>) so its sand reads the same everywhere.
    /// </summary>
    [RequireComponent(typeof(Zone))]
    public class PressurePlate : MonoBehaviour, ISignalSource, IObstacleState, IResettable
    {
        /// <summary><see cref="ActiveUntil"/> while the plate is held: open-ended.</summary>
        public const double HeldForever = double.MaxValue;

        readonly HashSet<Player> inside = new HashSet<Player>();
        Zone zone;

        [SerializeField, Tooltip("Off: a hands-free plate, the player carrying the bomb (or whose throw is in the air) does not hold it down.")]
        bool countCarrier = true;
        [SerializeField, Min(1), Tooltip("Counted players needed on it at once (2: a heavy plate, PROJECT_SPEC §13.21).")]
        int requiredBodies = 1;
        [SerializeField, Min(0f), Tooltip("Seconds it stays active after the last counted player stepped off (an hourglass plate, §13.22). 0 = none.")]
        float memorySeconds;

        double activeUntil = double.NegativeInfinity;
        bool mirrored;

        /// <summary>Host: raised when the plate is held again or released (its open-ended or timed end), for replication.</summary>
        public event Action<double> ActiveUntilChanged;

        public bool Active { get; private set; }
        /// <summary>Enough counted players stand on it right now (the hourglass aside).</summary>
        public bool Held { get; private set; }
        public bool CountsCarrier => countCarrier;
        public int RequiredBodies => requiredBodies;
        public float MemorySeconds => memorySeconds;
        /// <summary>Counted players on it in the last physics step (for its look: one footprint lit of two).</summary>
        public int CountedBodies { get; private set; }
        /// <summary>Server time the plate stops being active (<see cref="HeldForever"/> while held, -inf at rest).</summary>
        public double ActiveUntil => activeUntil;

        /// <summary>Held: 1. On an hourglass plate after release: the share of the sand left. At rest: 0.</summary>
        float IObstacleState.Progress
        {
            get
            {
                if (Held || memorySeconds <= 0f) return Active ? 1f : 0f;
                return Mathf.Clamp01((float)(activeUntil - SimulationClock.ServerNow) / memorySeconds);
            }
        }

        void Awake() => zone = GetComponent<Zone>();

        void FixedUpdate()
        {
            zone.CollectPlayers(inside);
            CountedBodies = Counted(inside, Excluded(BombController.Instance));
            Held = CountedBodies >= requiredBodies;
            if (memorySeconds <= 0f)
            {
                Active = Held;
                return;
            }

            // An hourglass plate: the host decides the release time; a mirrored client only reads it.
            double now = SimulationClock.ServerNow;
            if (!mirrored)
            {
                if (Held && activeUntil != HeldForever) SetActiveUntil(HeldForever);
                else if (!Held && activeUntil == HeldForever) SetActiveUntil(ReleaseEnd(now, memorySeconds));
            }
            Active = IsActive(activeUntil, now);
        }

        /// <summary>Clients with a replicated plate: take the host's value and stop deciding it locally.</summary>
        public void Mirror(double until)
        {
            mirrored = true;
            SetActiveUntil(until, false);
        }

        public void SetActiveUntil(double until, bool notify = true)
        {
            activeUntil = until;
            if (notify) ActiveUntilChanged?.Invoke(until);
        }

        public void ResetState()
        {
            if (!mirrored) SetActiveUntil(double.NegativeInfinity);
            Active = false;
        }

        /// <summary>
        /// Who does not count on a hands-free plate: the carrier, or while the bomb flies (or rides a tube) its thrower.
        /// A plain plate excludes nobody.
        /// </summary>
        Player Excluded(BombController bomb)
        {
            if (countCarrier || bomb == null) return null;
            return ExcludedPlayer(bomb.State, bomb.Carrier, bomb.LastThrower);
        }

        // ------------------------------------------------------------------ rules (pure, EditMode tested)

        /// <summary>The player a hands-free plate ignores in bomb state <paramref name="state"/>.</summary>
        public static Player ExcludedPlayer(BombState state, Player carrier, Player lastThrower) =>
            carrier != null ? carrier : state == BombState.Thrown || state == BombState.InTransit ? lastThrower : null;

        /// <summary>How many of <paramref name="standing"/> count, <paramref name="excluded"/> not counting.</summary>
        public static int Counted(ICollection<Player> standing, Player excluded) =>
            standing.Count - (excluded != null && standing.Contains(excluded) ? 1 : 0);

        /// <summary>Is the plate held by <paramref name="standing"/>, the <paramref name="excluded"/> player not counting?</summary>
        public static bool IsHeld(ICollection<Player> standing, Player excluded, int required = 1) =>
            Counted(standing, excluded) >= Mathf.Max(1, required);

        /// <summary>The server time an hourglass plate released at <paramref name="now"/> stops.</summary>
        public static double ReleaseEnd(double now, float memorySeconds) => now + Mathf.Max(0f, memorySeconds);

        public static bool IsActive(double activeUntil, double now) => activeUntil == HeldForever || now < activeUntil;
    }
}
