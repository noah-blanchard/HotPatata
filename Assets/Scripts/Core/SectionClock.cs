using UnityEngine;

namespace Beep
{
    /// <summary>
    /// The single time base for gameplay-critical level motion. It restarts on every section reset, so a
    /// platform whose position is a function of this clock is at the same place for everyone and returns
    /// to its start state after a reset. Falls back to Time.time when no RunManager exists (prefab tests).
    /// </summary>
    public static class SectionClock
    {
        public static float Now => RunManager.Instance != null ? RunManager.Instance.SectionTime : Time.time;
    }
}
