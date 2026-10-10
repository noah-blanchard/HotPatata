using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Continuously rotates around an axis. The angle is a pure function of <see cref="SectionClock"/>,
    /// so it is deterministic and consistent after a section reset. Rotates the object it is on: put the visual and the colliders
    /// (and a child <c>KillZone</c> for a lethal one) under it. Subclasses may override <see cref="RotationAt"/> (docs/OBSTACLES.md §4).
    /// </summary>
    public class RotatingObstacle : MonoBehaviour, IObstacleState, ITimePosed
    {
        [SerializeField] Vector3 axis = Vector3.up;
        [SerializeField, Tooltip("Degrees per second (negative reverses).")] float degreesPerSecond = 60f;
        [SerializeField, Tooltip("Angle at section time 0, in degrees.")] float phaseDegrees;

        Quaternion baseRotation;

        public float CurrentAngle => AngleAt(SectionClock.Now);
        public Vector3 Axis => axis;
        public float DegreesPerSecond => degreesPerSecond;
        float IObstacleState.Progress => Mathf.Repeat(CurrentAngle, 360f) / 360f;
        bool IObstacleState.Active => !Mathf.Approximately(degreesPerSecond, 0f);

        /// <summary>The angle in degrees at <paramref name="sectionTime"/>. Pure.</summary>
        public float AngleAt(float sectionTime) => phaseDegrees + degreesPerSecond * sectionTime;

        public Transform PosedTransform => transform;

        /// <summary>The bar's world pose at any server time of the current section (<see cref="ITimePosed"/>). It turns in place.</summary>
        public bool TryPoseAt(double serverTime, out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            double start = SimulationClock.SectionStart;
            var local = RotationAt(baseRotation, AngleAt((float)(serverTime - start)));
            rotation = transform.parent != null ? transform.parent.rotation * local : local;
            return serverTime >= start;
        }

        public bool LethalAt(double serverTime, Collider zone) => true;

        /// <summary>The local rotation at <paramref name="angle"/> degrees from the rest pose. Override for another motion (still a pure function of the angle).</summary>
        protected virtual Quaternion RotationAt(Quaternion rest, float angle) => rest * Quaternion.AngleAxis(angle, axis.normalized);

        void Awake() => baseRotation = transform.localRotation;

        void Update() => transform.localRotation = RotationAt(baseRotation, CurrentAngle);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.TransformDirection(axis.normalized) * 2f);
        }
    }
}
