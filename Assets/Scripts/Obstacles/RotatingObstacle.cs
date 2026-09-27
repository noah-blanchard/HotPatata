using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Continuously rotates around an axis. The angle is a pure function of <see cref="SectionClock"/>,
    /// so it is deterministic and consistent after a section reset.
    /// </summary>
    public class RotatingObstacle : MonoBehaviour
    {
        [SerializeField] Vector3 axis = Vector3.up;
        [SerializeField, Tooltip("Degrees per second (negative reverses).")] float degreesPerSecond = 60f;
        [SerializeField, Tooltip("Angle at section time 0, in degrees.")] float phaseDegrees;

        Quaternion baseRotation;

        public float CurrentAngle => phaseDegrees + degreesPerSecond * SectionClock.Now;

        void Awake() => baseRotation = transform.localRotation;

        void Update() =>
            transform.localRotation = baseRotation * Quaternion.AngleAxis(CurrentAngle, axis.normalized);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.TransformDirection(axis.normalized) * 2f);
        }
    }
}
