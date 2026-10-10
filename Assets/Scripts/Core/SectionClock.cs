namespace HotPatata
{
    /// <summary>
    /// The single time base for gameplay-critical level motion. It restarts on every section reset, so a
    /// platform whose position is a function of this clock is at the same place for everyone and returns
    /// to its start state after a reset. A façade over <see cref="SimulationClock.SectionTime"/> (one sample per frame,
    /// monotonic on clients); without a RunManager (prefab tests) it is the local time.
    /// </summary>
    public static class SectionClock
    {
        public static float Now => SimulationClock.SectionTime;
    }
}
