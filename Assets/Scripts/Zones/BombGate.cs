using System;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A ring the bomb must fly through (PROJECT_SPEC §13.15, §13.17). The host records the (server) time of the last
    /// pass; everything else derives from that one number, so every machine agrees once it is replicated
    /// (<see cref="NetworkBombGate"/>). As a <see cref="ISignalSource"/> it is active for <see cref="holdSeconds"/> after
    /// the pass (0 = until the section resets, used by checkpoint arches). Only a flying bomb counts: carrying the
    /// bomb through does nothing. Cleared on every section reset.
    /// </summary>
    [RequireComponent(typeof(Zone))]
    public class BombGate : MonoBehaviour, IBombZoneEffect, ISignalSource, IResettable
    {
        [SerializeField, Min(0f), Tooltip("Seconds the gate stays active after a pass (0 = until the section resets).")]
        float holdSeconds = 8f;

        double passedAt = -1.0;

        /// <summary>Raised when the pass time changes (the host replicates it).</summary>
        public event Action<double> PassedAtChanged;

        public double PassedAt => passedAt;
        public float HoldSeconds => holdSeconds;
        /// <summary>The bomb has flown through since the last section reset.</summary>
        public bool PassedThisSection => passedAt >= 0.0;
        /// <summary>Seconds left before the gate closes again (infinity for a latched gate, 0 when inactive).</summary>
        public float RemainingSeconds
        {
            get
            {
                if (!PassedThisSection) return 0f;
                if (holdSeconds <= 0f) return float.PositiveInfinity;
                return Mathf.Max(0f, holdSeconds - (float)(NetMode.ServerTime - passedAt));
            }
        }
        public bool Active => RemainingSeconds > 0f;

        public void OnBombPassed(BombController bomb)
        {
            if (!NetMode.IsAuthority) return;
            if (!PassedThisSection) PatataLog.Run($"{name} passed by the bomb");
            SetPassedAt(NetMode.ServerTime);   // each step through the ring re-arms it; the last one counts
        }

        public void SetPassedAt(double time, bool notify = true)
        {
            passedAt = time;
            if (notify) PassedAtChanged?.Invoke(time);
        }

        public void ResetState() => SetPassedAt(-1.0);
    }
}
