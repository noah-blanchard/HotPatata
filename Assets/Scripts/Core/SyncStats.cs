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

        /// <summary>Client: NGO's server-time estimate jumped by more than 0.15 s in a frame (a hard reset), and the level clock snapped.</summary>
        public static int ClockJumps, ClockSnaps;

        /// <summary>
        /// Host: client times that hit a cap (#117 §5.5): hazard stamps older than <see cref="GameTuning.hazardRewindCap"/>,
        /// catch claims older than <see cref="GameTuning.catchLagCompensation"/>, throws older than
        /// <see cref="GameTuning.throwBackdateCap"/>. Each one is a verdict judged later than the player saw it.
        /// </summary>
        public static int StampsClamped, ClaimsClamped, ThrowsClamped;

        /// <summary>Host: time-stamped catch claims accepted and refused (#117 P3).</summary>
        public static int ClaimsAccepted, ClaimsRefused;

        /// <summary>Client: catches shown at once on this screen and then confirmed, or refused, by the host.</summary>
        public static int PredictedCatchesConfirmed, PredictedCatchesRefused;

        /// <summary>Host: a remote carrier's fuse ran out and the explosion waited for a throw already on its way (F4).</summary>
        public static int FuseHolds, FuseHoldSaves;

        /// <summary>Viewer: distance (m) between where a flight was drawn and its authoritative arc when the arc arrived (P2).</summary>
        public static readonly RollingSamples FlightPops = new RollingSamples(64);

        public static void Clear()
        {
            HazardTriggerHits = HazardRewindHits = StaticKills = 0;
            ThrowAgreed = ThrowDisagreed = 0;
            CarrierChanges = CarrierGroundFlips = RiderPops = 0;
            ClockJumps = ClockSnaps = 0;
            StampsClamped = ClaimsClamped = ThrowsClamped = 0;
            ClaimsAccepted = ClaimsRefused = 0;
            PredictedCatchesConfirmed = PredictedCatchesRefused = 0;
            FuseHolds = FuseHoldSaves = 0;
            FlightPops.Clear();
        }
    }
}
