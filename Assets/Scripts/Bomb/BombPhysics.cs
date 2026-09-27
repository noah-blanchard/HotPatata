using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Rigidbody/collider configuration for the two physical modes of the bomb, and the source of
    /// gameplay-critical contact reports. Held: kinematic, collider off, parented to the hand
    /// (cannot hurt its carrier). Thrown: dynamic, continuous collision, custom gravity.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(BombController))]
    public class BombPhysics : MonoBehaviour
    {
        // Shape of the homing curve (the strength itself is tuned in GameTuning).
        const float HomingReferenceSpeed = 14f;   // m/s at which homingTurnRate applies as-is (see its tooltip)
        const float MinTurnScale = 0.6f, MaxTurnScale = 2f;
        const float MagnetTurnBoost = 4f;         // extra turn rate at magnetStrength = 1
        const float MinSteerSpeed = 1f;

        [SerializeField] GameTuning tuning;

        Rigidbody body;
        SphereCollider sphere;
        BombController controller;

        public Vector3 Velocity => body.isKinematic ? Vector3.zero : body.linearVelocity;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            sphere = GetComponent<SphereCollider>();
            controller = GetComponent<BombController>();
            body.useGravity = false;   // gravity is applied manually so it can be scaled
            EnterInert();
        }

        void FixedUpdate()
        {
            if (!NetMode.IsAuthority || controller.State != BombState.Thrown) return;

            body.AddForce(Physics.gravity * tuning.bombGravityScale, ForceMode.Acceleration);
            Steer();
        }

        /// <summary>
        /// Soft homing: bend the velocity toward the locked receiver at a limited turn rate. Fast throws bend too
        /// (the rate scales with speed). It aims at where the receiver IS, not where they are going, and never bends
        /// around geometry, so it can miss. Inside the magnet radius, with their catch window open, it pulls into their hands.
        /// </summary>
        void Steer()
        {
            var target = controller.HomingTarget;
            if (target == null || target.CatchVolume == null) return;

            Vector3 v = body.linearVelocity;
            float speed = v.magnitude;
            if (speed < MinSteerSpeed) return;

            Vector3 center = target.CatchVolume.CatchCenter;
            float distance = Vector3.Distance(body.position, center);
            bool magnet = distance < tuning.magnetRadius && target.Catcher.WindowOpen;
            Vector3 point = magnet ? center : center + controller.HomingOffset;

            Vector3 to = point - body.position;
            if (Vector3.Dot(v, to) <= 0f)
            {
                controller.ClearHoming();   // already past them: let it fly
                return;
            }

            float g = -Physics.gravity.y * tuning.bombGravityScale;
            float timeToGo = to.magnitude / speed;
            Vector3 aim = to + Vector3.up * (0.5f * g * timeToGo * timeToGo);   // aim high enough to still get there after the drop

            float turn = tuning.homingTurnRate * Mathf.Clamp(speed / HomingReferenceSpeed, MinTurnScale, MaxTurnScale) * controller.HomingQuality;
            if (magnet) turn *= 1f + MagnetTurnBoost * tuning.magnetStrength;

            Vector3 dir = Vector3.RotateTowards(v / speed, aim.normalized, turn * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            body.linearVelocity = dir * speed;
        }

        public void SetTuning(GameTuning value) => tuning = value;

        /// <summary>Attach to a hand anchor, with all motion cleared.</summary>
        public void EnterHeld(Transform anchor)
        {
            EnterInert();
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>Detach into free flight from <paramref name="position"/> with <paramref name="velocity"/>.</summary>
        public void EnterThrown(Vector3 position, Vector3 velocity)
        {
            transform.SetParent(null, true);
            transform.position = position;
            body.position = position;

            sphere.enabled = true;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
        }

        /// <summary>Stop dead where it is (explosion / reset). Keeps the current parent.</summary>
        public void EnterInert()
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            sphere.enabled = false;
        }

        void OnCollisionEnter(Collision collision) => controller.ReportWorldContact(collision.collider);

        void OnTriggerEnter(Collider other) => controller.ReportTriggerContact(other);

        // A catch window can open while the bomb is already inside the volume.
        void OnTriggerStay(Collider other) => controller.ReportTriggerContact(other);
    }
}
