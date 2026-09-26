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
            if (controller.State == BombState.Thrown)
                body.AddForce(Physics.gravity * tuning.bombGravityScale, ForceMode.Acceleration);
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
    }
}
