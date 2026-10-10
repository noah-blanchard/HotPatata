using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A level object whose pose is a pure function of time (docs/netcode-deterministic-plan.md §2.2): it can say where
    /// its moving part was, or will be, at ANY server time, not only now. The host uses this to judge a remote player
    /// against a moving hazard at the time that player saw it (<see cref="HazardRewind"/>, plan §2.4): the level is
    /// rewound for free, with no history buffer. Implemented by <see cref="MovingPlatform"/>,
    /// <see cref="RotatingObstacle"/> and <see cref="SignalActuator"/>; a custom mover implements it too
    /// (docs/OBSTACLES.md §4).
    /// </summary>
    public interface ITimePosed
    {
        /// <summary>The transform that moves (a lethal zone under it moves rigidly with it).</summary>
        Transform PosedTransform { get; }

        /// <summary>
        /// World pose of <see cref="PosedTransform"/> at <paramref name="serverTime"/> (<see cref="SimulationClock.ServerNow"/>
        /// time base). False when it cannot be known (before the current section began, not set up).
        /// </summary>
        bool TryPoseAt(double serverTime, out Vector3 position, out Quaternion rotation);

        /// <summary>Is <paramref name="zone"/> (a lethal volume under this object) armed at <paramref name="serverTime"/>?</summary>
        bool LethalAt(double serverTime, Collider zone);
    }
}
