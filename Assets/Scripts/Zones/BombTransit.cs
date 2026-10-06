using System;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A tube or a cannon (PROJECT_SPEC §5 <c>InTransit</c>, §13.16): a thrown bomb entering one of its mouths
    /// (<see cref="TransitMouth"/>) is carried by the level and leaves the exit linked to that mouth after
    /// <see cref="delay"/>, on a fixed arc that reaches the exit's receiver pad at catch height after
    /// <see cref="Exit.flightTime"/>. A tube holds the bomb inside its pipe (out of sight); a cannon holds it in its
    /// basket. No fuse burns meanwhile. The bomb state machine itself lives in <see cref="BombController"/>; this
    /// component only answers where the bomb waits, when it leaves and on which arc. The active exit and release time
    /// are replicated (<see cref="NetworkBombTransit"/>) for the exit's warning light and tone.
    /// Generic (docs/OBSTACLES.md §4): a tube and a cannon differ only by their data (delay, exits) and their look; every exit is
    /// a set of references (hold, muzzle, pad), so any model can be a transit.
    /// </summary>
    public class BombTransit : MonoBehaviour, IResettable, IObstacleState
    {
        [Serializable]
        public class Exit
        {
            [Tooltip("Where the bomb waits during the transit (inside the pipe, or in the cannon's basket).")]
            public Transform hold;
            [Tooltip("Where the bomb leaves the transit.")]
            public Transform muzzle;
            [Tooltip("The receiver pad on the floor: the exit arc reaches catch height above it.")]
            public Transform pad;
            [Min(0.2f), Tooltip("Seconds of flight from the muzzle to catch height above the pad.")]
            public float flightTime = 1.1f;
        }

        [SerializeField, Min(0f), Tooltip("Seconds the bomb spends inside before it comes out (a cannon: short).")]
        float delay = 1.2f;
        [SerializeField] Exit[] exits = Array.Empty<Exit>();
        [SerializeField] GameTuning tuning;

        int activeExit = -1;
        double releaseAt = -1.0;

        /// <summary>Raised when a transit starts or is cleared (exit index or -1, server release time).</summary>
        public event Action<int, double> TransitChanged;

        public float Delay => delay;
        public int ExitCount => exits.Length;
        /// <summary>The exit the bomb will leave from, or -1 when this transit is empty.</summary>
        public int ActiveExit => activeExit;
        /// <summary>Server time at which the bomb leaves the active exit.</summary>
        public double ReleaseAt => releaseAt;
        public Exit GetExit(int i) => exits[i];
        bool IObstacleState.Active => activeExit >= 0;
        /// <summary>From 0 when the bomb goes in to 1 when it comes out.</summary>
        float IObstacleState.Progress => activeExit < 0 ? 0f : delay <= 0f ? 1f : Mathf.Clamp01(1f - (float)(releaseAt - NetMode.ServerTime) / delay);

        /// <summary>
        /// The launch velocity that carries the bomb from <paramref name="from"/> to <paramref name="to"/> in
        /// <paramref name="time"/> seconds under bomb gravity <paramref name="gravity"/> (m/s², positive). Pure (EditMode tested).
        /// </summary>
        public static Vector3 ExitVelocity(Vector3 from, Vector3 to, float gravity, float time)
        {
            time = Mathf.Max(0.05f, time);
            return (to - from) / time + 0.5f * gravity * time * Vector3.up;
        }

        /// <summary>The point above the exit's pad that the exit arc passes through: a standing receiver's catch centre.</summary>
        public Vector3 AimPoint(int exit) => exits[exit].pad.position + Vector3.up * tuning.catchCenterHeight;

        public Vector3 LaunchVelocity(int exit) =>
            ExitVelocity(exits[exit].muzzle.position, AimPoint(exit), ThrowBallistics.Gravity(tuning), exits[exit].flightTime);

        /// <summary>A mouth of this transit swallowed the flying bomb: hand it to the bomb's state machine. Authority only.</summary>
        public void Capture(BombController bomb, int exit)
        {
            if (!NetMode.IsAuthority || exit < 0 || exit >= exits.Length) return;
            bomb.EnterTransit(this, exit);
        }

        /// <summary>Called by <see cref="BombController"/> when the bomb goes in (exit, server release time) or out (-1).</summary>
        public void SetTransit(int exit, double release, bool notify = true)
        {
            activeExit = exit;
            releaseAt = release;
            if (notify) TransitChanged?.Invoke(exit, release);
        }

        public void ResetState() => SetTransit(-1, -1.0);

        void OnDrawGizmos()
        {
            if (tuning == null) return;
            float g = ThrowBallistics.Gravity(tuning);
            for (int i = 0; i < exits.Length; i++)
            {
                var e = exits[i];
                if (e == null || e.muzzle == null || e.pad == null) continue;
                Vector3 v = ExitVelocity(e.muzzle.position, AimPoint(i), g, e.flightTime);
                Gizmos.color = Color.magenta;
                Vector3 prev = e.muzzle.position;
                for (float t = 0.05f; t <= e.flightTime + 0.2f; t += 0.05f)
                {
                    Vector3 next = ThrowBallistics.PositionAt(e.muzzle.position, v, g, t);
                    Gizmos.DrawLine(prev, next);
                    prev = next;
                }
            }
        }
    }
}
