using System;
using UnityEngine;

namespace Beep
{
    public enum FuseStage
    {
        Calm,
        Medium,
        Urgent,
        Critical
    }

    /// <summary>
    /// The per-carrier hold fuse. Ticked by <see cref="BombController"/> only while the bomb is Held;
    /// refreshed on every valid catch and on reset. Presentation reads the normalized values.
    /// </summary>
    public class BombFuse : MonoBehaviour
    {
        [SerializeField] GameTuning tuning;

        float remaining;
        float durationOverride;
        bool expiredRaised;

        /// <summary>Raised exactly once per fuse cycle when the fuse reaches zero.</summary>
        public event Action Expired;

        /// <summary>Hold time in seconds: the tuning value, unless the current checkpoint overrides it.</summary>
        public float Duration => durationOverride > 0f ? durationOverride : tuning.holdFuseDuration;
        public float Remaining => remaining;
        /// <summary>0 = full fuse, 1 = about to explode.</summary>
        public float Consumed01 => Mathf.Clamp01(1f - remaining / Duration);
        public bool InWarningPhase => remaining <= tuning.warningDuration;
        public bool IsExpired => remaining <= 0f;

        public FuseStage Stage
        {
            get
            {
                float c = Consumed01;
                var th = tuning.stageThresholds;
                if (c >= th[2]) return FuseStage.Critical;
                if (c >= th[1]) return FuseStage.Urgent;
                if (c >= th[0]) return FuseStage.Medium;
                return FuseStage.Calm;
            }
        }

        void Awake() => Refresh();

        /// <summary>Overrides the hold time (0 = back to the tuning value) and refills the fuse.</summary>
        public void SetDurationOverride(float seconds)
        {
            durationOverride = seconds;
            Refresh();
        }

        public void SetTuning(GameTuning value)
        {
            tuning = value;
            Refresh();
        }

        public void Refresh()
        {
            remaining = tuning != null ? Duration : 0f;
            expiredRaised = false;
        }

        /// <summary>Remote clients only: show the host's fuse (the host is the only one that ticks it).</summary>
        public void MirrorConsumed(float consumed01) => remaining = Duration * (1f - Mathf.Clamp01(consumed01));

        public void Tick(float deltaTime)
        {
            if (expiredRaised) return;

            remaining -= deltaTime;
            if (remaining > 0f) return;

            remaining = 0f;
            expiredRaised = true;
            Expired?.Invoke();
        }
    }
}
