namespace HotPatata
{
    /// <summary>
    /// The state every obstacle system shows to presentation (ARCHITECTURE §10.7, docs/OBSTACLES.md §4): how far along its motion
    /// it is and whether it is "on". Read-only: presentation (<see cref="ObstacleVisualDriver"/>, a custom visual's own script)
    /// reads it on every machine and never writes back, so any visual can be put on any system.
    /// </summary>
    public interface IObstacleState
    {
        /// <summary>0..1 along the system's motion (a platform from A to B, a door from closed to open, a fall, a turn, a fuse in a tube).</summary>
        float Progress { get; }

        /// <summary>The system is on: heading to B, open or opening, triggered, holding the bomb, a source active, a checkpoint reached.</summary>
        bool Active { get; }
    }
}
