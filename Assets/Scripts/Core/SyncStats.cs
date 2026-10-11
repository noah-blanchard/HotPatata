namespace HotPatata
{
    /// <summary>
    /// Development counters for the network time-frame diagnostics (docs/netcode-deterministic-plan.md stage 0). Gameplay
    /// code bumps them next to its <see cref="PatataLog.Sync"/> line; the dev overlay (<c>NetSyncProbe</c>) reads them.
    /// Observation only: nothing ever decides anything from these numbers.
    /// </summary>
    public static class SyncStats
    {
        /// <summary>Host: a remote player touched a moving lethal zone on the host's copy (the trigger's verdict).</summary>
        public static int HazardTriggerHits;
        /// <summary>Host: a remote player was found inside a moving lethal zone at the time they saw it (the rewound verdict).</summary>
        public static int HazardRewindHits;
        /// <summary>Host: a remote player died on a static lethal zone (water, void).</summary>
        public static int StaticKills;

        /// <summary>Client: throws whose local prediction matched the host's outcome, and those that did not.</summary>
        public static int ThrowAgreed, ThrowDisagreed;

        /// <summary>
        /// Owner (also offline): changes of the carrier the local player stands on (a clean ride is 2: board, leave), and
        /// ground/air flips while on or next to one (a clean ride is 0). The #97 signature was dozens per second on a lift.
        /// </summary>
        public static int CarrierChanges, CarrierGroundFlips;
        /// <summary>Viewer: frames where a remote rider jumped more than 0.3 m relative to their carrier.</summary>
        public static int RiderPops;

        public static void Clear()
        {
            HazardTriggerHits = HazardRewindHits = StaticKills = 0;
            ThrowAgreed = ThrowDisagreed = 0;
            CarrierChanges = CarrierGroundFlips = RiderPops = 0;
        }
    }
}
