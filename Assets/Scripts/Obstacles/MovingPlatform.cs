using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A platform that goes back and forth between two waypoints. Its position is a pure function of
    /// <see cref="SectionClock"/>, so it is deterministic for every player and returns to the same phase after a
    /// section reset. Players standing on it are carried by PlayerMotor via <see cref="FrameDelta"/>.
    /// <see cref="Motion.PingPong"/> travels at a constant speed; <see cref="Motion.Dwell"/> waits at each end for
    /// part of the cycle and then eases across (pistons, crushers, elevators).
    /// Generic (docs/OBSTACLES.md §4): the moving part and the waypoints are references, never names, so any prefab with any
    /// visual under its moving part behaves as a moving platform; subclasses may override <see cref="PositionAt"/> (a curved path).
    /// </summary>
    [DefaultExecutionOrder(-50)]   // move before players update, so riders get this frame's delta
    public class MovingPlatform : MonoBehaviour, IPlatformCarrier, IObstacleState
    {
        public enum Motion
        {
            PingPong,
            Dwell
        }

        [SerializeField, Tooltip("The part that moves (its visual and colliders live under it). Never this object itself.")]
        Transform platform;
        [SerializeField] Transform waypointA;
        [SerializeField] Transform waypointB;
        [SerializeField, Min(0.01f), Tooltip("Metres per second (Dwell: the average over a cycle, waits included).")]
        float speed = 2f;
        [SerializeField, Range(0f, 1f), Tooltip("Where in the A-B-A cycle the platform is at section time 0.")] float startPhase;
        [SerializeField] Motion motion = Motion.PingPong;
        [SerializeField, Range(0f, 0.95f), Tooltip("Dwell: the share of each half-cycle spent waiting at an end.")]
        float dwellFraction = 0.5f;

        bool initialised;
        float lastU;

        /// <summary>How far the platform moved this frame (used to carry riders).</summary>
        public Vector3 FrameDelta { get; private set; }

        public int CarrierId { get; private set; }
        public bool Moves => platform != null;
        public Vector3 AnchorPosition => platform != null ? platform.position : transform.position;

        public Transform Platform => platform;
        public Transform WaypointA => waypointA;
        public Transform WaypointB => waypointB;
        public float Speed => speed;

        /// <summary>Where the platform is along A→B this frame (0 = at A, 1 = at B).</summary>
        public float Progress { get; private set; }

        /// <summary>The platform is heading to (or waiting at) B.</summary>
        public bool Active { get; private set; }

        /// <summary>Seconds for a full A-B-A cycle.</summary>
        public float CycleDuration => 2f * PathLength / speed;
        float PathLength => waypointA != null && waypointB != null ? Vector3.Distance(waypointA.position, waypointB.position) : 0f;

        /// <summary>
        /// Where the platform is along A→B (0 = at A, 1 = at B) at a given section time. Pure, so every machine
        /// computes the same thing from the shared clock.
        /// </summary>
        public static float Evaluate(float time, float length, float speed, float startPhase, Motion motion, float dwellFraction)
        {
            if (length < 0.01f || speed <= 0f) return 0f;
            float cycle = Mathf.Repeat(time * speed / (2f * length) + startPhase, 1f);   // 0..1 over A-B-A
            bool outbound = cycle < 0.5f;
            float half = outbound ? cycle * 2f : cycle * 2f - 1f;                         // 0..1 within one leg
            float k = motion == Motion.Dwell ? DwellLeg(half, dwellFraction) : half;
            return outbound ? k : 1f - k;
        }

        // Wait at the start of the leg, then ease across.
        static float DwellLeg(float t, float dwell)
        {
            if (t <= dwell) return 0f;
            return Mathf.SmoothStep(0f, 1f, (t - dwell) / Mathf.Max(0.0001f, 1f - dwell));
        }

        /// <summary>The world position of the moving part at <paramref name="u"/> along A→B. Override for another path (still a pure function of u).</summary>
        protected virtual Vector3 PositionAt(Vector3 a, Vector3 b, float u) => Vector3.Lerp(a, b, u);

        void OnEnable() => CarrierId = CarrierRegistry.Register(this);

        void OnDisable() => CarrierRegistry.Unregister(CarrierId, this);

        void Update()
        {
            if (platform == null || waypointA == null || waypointB == null) return;
            Vector3 a = waypointA.position, b = waypointB.position;
            float length = Vector3.Distance(a, b);
            if (length < 0.01f) return;

            float u = Evaluate(SectionClock.Now, length, speed, startPhase, motion, dwellFraction);
            Vector3 target = PositionAt(a, b, u);

            Vector3 delta = target - platform.position;
            // A reset (or first frame) snaps the platform; never drag riders along with a snap.
            FrameDelta = initialised && delta.sqrMagnitude < 4f ? delta : Vector3.zero;
            platform.position = target;
            if (u > lastU + 1e-5f) Active = true;
            else if (u < lastU - 1e-5f) Active = false;
            else if (!initialised) Active = u >= 0.5f;
            lastU = u;
            Progress = u;
            initialised = true;
        }

        void OnDrawGizmos()
        {
            if (waypointA == null || waypointB == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(waypointA.position, waypointB.position);
            Gizmos.DrawWireSphere(waypointA.position, 0.3f);
            Gizmos.DrawWireSphere(waypointB.position, 0.3f);
        }
    }
}
