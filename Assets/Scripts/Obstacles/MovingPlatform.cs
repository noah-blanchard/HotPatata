using UnityEngine;

namespace Beep
{
    /// <summary>
    /// A platform that ping-pongs between two waypoints at a constant speed. Its position is a pure
    /// function of <see cref="SectionClock"/>, so it is deterministic for every player and returns to
    /// the same phase after a section reset. Players standing on it are carried by PlayerMotor via
    /// <see cref="FrameDelta"/>.
    /// </summary>
    [DefaultExecutionOrder(-50)]   // move before players update, so riders get this frame's delta
    public class MovingPlatform : MonoBehaviour
    {
        [SerializeField, Tooltip("The part that moves (Visual + Collision live under it).")]
        Transform platform;
        [SerializeField] Transform waypointA;
        [SerializeField] Transform waypointB;
        [SerializeField, Min(0.01f), Tooltip("Metres per second.")] float speed = 2f;
        [SerializeField, Range(0f, 1f), Tooltip("Where in the A-B-A cycle the platform is at section time 0.")] float startPhase;

        bool initialised;

        /// <summary>How far the platform moved this frame (used to carry riders).</summary>
        public Vector3 FrameDelta { get; private set; }

        void Update()
        {
            Vector3 a = waypointA.position, b = waypointB.position;
            float length = Vector3.Distance(a, b);
            if (length < 0.01f) return;

            float u = Mathf.PingPong(SectionClock.Now * speed / length + startPhase * 2f, 1f);
            Vector3 target = Vector3.Lerp(a, b, u);

            Vector3 delta = target - platform.position;
            // A reset (or first frame) snaps the platform; never drag riders along with a snap.
            FrameDelta = initialised && delta.sqrMagnitude < 4f ? delta : Vector3.zero;
            platform.position = target;
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
