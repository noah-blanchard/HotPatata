using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// How late a remote player's time-stamped message can arrive, measured, never guessed (#117 §5.5). A client draws the
    /// level at its <see cref="SimulationClock.ServerNow"/>, which trails the host by one way + NGO's buffer
    /// (<see cref="TimelineBuffer"/>) + its view delay (<see cref="ViewDelay"/>); what it does at time t then takes one more
    /// way to come back. So a message stamped t reaches the host about rtt + buffer + view delay after the host's own t,
    /// plus the jitter. Every host wait (an explosion held for a catch, a fuse held for a throw) uses this, capped.
    /// </summary>
    public static class LagBudget
    {
        /// <summary>NGO's <c>NetworkTimeSystem.ServerBufferSec</c>: a client's server time runs this much behind what it received.</summary>
        public const float TimelineBuffer = 0.05f;

        /// <summary>
        /// The extra delay a client draws the level at, from the jitter it measures (P4): what NGO's buffer does not cover,
        /// at most <paramref name="max"/>. Pure, the same on every machine.
        /// </summary>
        public static float ViewDelay(float jitter, float max) => Mathf.Clamp(jitter - TimelineBuffer, 0f, Mathf.Max(0f, max));

        /// <summary>
        /// Host: seconds after server time t that a message the owner of <paramref name="player"/> stamped t may still arrive,
        /// at most <paramref name="cap"/>. 0 for the host's own player and offline.
        /// </summary>
        public static float LateMessageWait(Player player, GameTuning t, float cap)
        {
            if (t == null || player == null || player.IsLocal || player.Net == null || !player.Net.IsSpawned) return 0f;
            float rtt = NetMode.RttMsFor(player.Net.OwnerClientId) / 1000f;
            float jitter = NetQuality.JitterSeconds(player.PlayerId);
            return Mathf.Min(cap, rtt + TimelineBuffer + ViewDelay(jitter, t.viewDelayMax) + jitter + t.netHoldMargin);
        }
    }
}
