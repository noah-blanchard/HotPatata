using System;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A door, bridge or lift driven by ONE <see cref="ISignalSource"/> (PROJECT_SPEC §13.15): it travels to its open
    /// waypoint while the source is active and back when it is not. The host only records the moment the direction
    /// changed (time, progress at that moment, direction); the position is a pure function of that and the server
    /// clock (<see cref="Progress"/>), so it is identical on every machine once replicated
    /// (<see cref="NetworkSignalActuator"/>). Riders are carried (<see cref="IPlatformCarrier"/>). A door's lethal
    /// lower edge (<see cref="lethalWhileClosing"/>) is only armed while it closes. Closed again on every section reset.
    /// Generic (docs/OBSTACLES.md §4): the moving part, the waypoints and the source are references, never names; with
    /// <see cref="rotateWithWaypoints"/> the part also turns from the closed waypoint's rotation to the open one's (a swinging gate,
    /// a drawbridge), and subclasses may override <see cref="PositionAt"/> / <see cref="RotationAt"/>.
    /// </summary>
    [DefaultExecutionOrder(-50)]   // move before players update, so riders get this frame's delta
    public class SignalActuator : MonoBehaviour, IPlatformCarrier, IResettable, IObstacleState
    {
        [SerializeField, Tooltip("A component implementing ISignalSource (BombGate, PressurePlate).")]
        MonoBehaviour source;
        [SerializeField, Tooltip("The part that moves (its visual and colliders live under it). Never this object itself.")]
        Transform platform;
        [SerializeField] Transform waypointClosed;
        [SerializeField] Transform waypointOpen;
        [SerializeField, Min(0.05f), Tooltip("Seconds from fully closed to fully open (and back).")]
        float travelSeconds = 1f;
        [SerializeField, Tooltip("Optional kill volume armed only while the actuator closes (a door's lower edge).")]
        Collider lethalWhileClosing;
        [SerializeField, Tooltip("Also turn the moving part from the closed waypoint's rotation to the open one's (a swinging gate, a drawbridge).")]
        bool rotateWithWaypoints;

        double changeTime;
        float fromProgress;
        bool opening;
        bool initialised;

        /// <summary>Raised on the host when the direction changes (time, progress then, opening).</summary>
        public event Action<double, float, bool> StateChanged;

        public ISignalSource Source => source as ISignalSource;
        public bool Opening => opening;
        public double ChangeTime => changeTime;
        public float FromProgress => fromProgress;
        /// <summary>0 = closed, 1 = open.</summary>
        public float CurrentProgress => Progress(changeTime, fromProgress, opening, travelSeconds, SimulationClock.ServerNow);
        public Vector3 FrameDelta { get; private set; }

        public int CarrierId { get; private set; }
        /// <summary>A switch (no moving part) carries nobody.</summary>
        public bool Moves => platform != null;
        public Vector3 AnchorPosition => platform != null ? platform.position : transform.position;

        public Transform Platform => platform;
        public Transform WaypointClosed => waypointClosed;
        public Transform WaypointOpen => waypointOpen;
        public Collider LethalEdge => lethalWhileClosing;
        public float TravelSeconds => travelSeconds;
        float IObstacleState.Progress => CurrentProgress;
        bool IObstacleState.Active => opening;

        /// <summary>The world position of the moving part at <paramref name="progress"/> (0 closed .. 1 open). Override for another path.</summary>
        protected virtual Vector3 PositionAt(Transform closed, Transform open, float progress) =>
            Vector3.Lerp(closed.position, open.position, Mathf.SmoothStep(0f, 1f, progress));

        /// <summary>The world rotation of the moving part at <paramref name="progress"/>, or null to leave it (the default unless <see cref="rotateWithWaypoints"/>).</summary>
        protected virtual Quaternion? RotationAt(Transform closed, Transform open, float progress) =>
            rotateWithWaypoints ? Quaternion.Slerp(closed.rotation, open.rotation, Mathf.SmoothStep(0f, 1f, progress)) : null;

        /// <summary>Where the actuator is (0 closed .. 1 open) at <paramref name="now"/>. Pure (EditMode tested).</summary>
        public static float Progress(double changeTime, float fromProgress, bool opening, float travelSeconds, double now)
        {
            float moved = (float)(now - changeTime) / Mathf.Max(0.05f, travelSeconds);
            return Mathf.Clamp01(fromProgress + (opening ? moved : -moved));
        }

        void OnValidate()
        {
            if (source != null && source is not ISignalSource)
            {
                Debug.LogWarning($"{name}: the source must implement ISignalSource", this);
                source = null;
            }
        }

        void OnEnable() => CarrierId = CarrierRegistry.Register(this);

        void OnDisable() => CarrierRegistry.Unregister(CarrierId, this);

        void FixedUpdate()
        {
            if (!NetMode.IsAuthority || Source == null) return;
            bool want = Source.Active;
            if (want != opening) SetState(NetMode.ServerTime, CurrentProgress, want);
        }

        /// <summary>Called every frame on every machine with the current progress (0 closed .. 1 open), before the part moves. Pure presentation of replicated state.</summary>
        protected virtual void ApplyProgress(float progress) { }

        void Update()
        {
            float p = CurrentProgress;
            ApplyProgress(p);
            if (platform == null || waypointClosed == null || waypointOpen == null) return;
            Vector3 target = PositionAt(waypointClosed, waypointOpen, p);
            Vector3 delta = target - platform.position;
            // A reset (or first frame) snaps the platform; never drag riders along with a snap.
            FrameDelta = initialised && delta.sqrMagnitude < 4f ? delta : Vector3.zero;
            platform.position = target;
            var rotation = RotationAt(waypointClosed, waypointOpen, p);
            if (rotation.HasValue) platform.rotation = rotation.Value;
            initialised = true;

            if (lethalWhileClosing != null) lethalWhileClosing.enabled = !opening && p > 0f && p < 1f;
        }

        public void SetState(double time, float progress, bool isOpening, bool notify = true)
        {
            changeTime = time;
            fromProgress = progress;
            opening = isOpening;
            if (notify) StateChanged?.Invoke(time, progress, isOpening);
        }

        public void ResetState() => SetState(NetMode.ServerTime, 0f, false);

        void OnDrawGizmos()
        {
            if (waypointClosed == null || waypointOpen == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(waypointClosed.position, waypointOpen.position);
            if (source != null)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
                Gizmos.DrawLine(source.transform.position, platform != null ? platform.position : transform.position);
            }
        }
    }
}
