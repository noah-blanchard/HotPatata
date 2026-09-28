namespace HotPatata
{
    /// <summary>
    /// Choices made in the menu / lobby that the level reads when the run starts. Only the authority's value
    /// matters (offline, or the host online): remote clients mirror the run the host starts.
    /// </summary>
    public static class RunOptions
    {
        /// <summary>
        /// Checkpoint id the team starts at (0 = the course start). Practice aid: the run begins as if the team had
        /// just reached that checkpoint (its spawns, carrier slot and fuse). A rematch starts there again.
        /// </summary>
        public static int StartCheckpoint;
    }
}
