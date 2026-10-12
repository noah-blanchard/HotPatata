using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Shared helpers for carriers (<see cref="IPlatformCarrier"/>), see docs/OBSTACLES.md §4.4.
    ///  - <see cref="Moved"/>: a carrier moves its transform in Update, but the physics scene only reads transforms at the
    ///    next fixed step (autoSyncTransforms is off). A rider's CharacterController would then stand on where the deck WAS:
    ///    sunk into a deck going up, floating over one going down, losing its footing every other frame (#97). Every carrier
    ///    calls <see cref="Moved"/> once it has moved: its riders are carried and its collider is where it is drawn, so
    ///    riders, ground probes and casts this frame see the deck that is drawn.
    ///  - <see cref="VelocityAt"/>: how fast a point riding a carrier moves this frame (a throw and a player leaving a
    ///    carrier keep it, PROJECT_SPEC §8.2 and §11).
    /// </summary>
    public static class CarrierMotion
    {
        /// <summary>
        /// Called by a carrier right after it wrote its moving part's new pose (and its <see cref="IPlatformCarrier.FrameDelta"/>)
        /// in Update. Moves its local riders with it and gives the physics scene the new pose, in the order that never puts
        /// the deck inside a rider: a deck that rises carries its riders up first, then moves its collider under them; any
        /// other motion moves the collider first, then the riders follow it down or along.
        /// </summary>
        public static void Moved(IPlatformCarrier carrier, Transform part, Rigidbody body)
        {
            bool rising = carrier.FrameDelta.y > 0f;
            if (!rising) SyncPhysics(part, body);
            PlayerMotor.CarryRiders(carrier);
            if (rising) SyncPhysics(part, body);
        }

        /// <summary>
        /// Gives the physics scene <paramref name="part"/>'s current pose now. A kinematic body is teleported directly (cheap,
        /// this body only); a moving part without one (a custom carrier) needs a global sync.
        /// </summary>
        public static void SyncPhysics(Transform part, Rigidbody body)
        {
            if (part == null) return;
            if (body != null)
            {
                body.position = part.position;
                body.rotation = part.rotation;
                return;
            }
            Physics.SyncTransforms();
        }

        /// <summary>
        /// World velocity (m/s) of <paramref name="point"/> carried by a carrier that slid by <paramref name="delta"/> and
        /// turned by <paramref name="turn"/> about its new <paramref name="anchor"/> over <paramref name="dt"/>. Pure (EditMode tested).
        /// </summary>
        public static Vector3 VelocityAt(Vector3 point, Vector3 delta, Vector3 anchor, Quaternion turn, float dt) =>
            dt > 1e-5f ? PlayerMotor.Carry(point, delta, anchor, turn) / dt : Vector3.zero;

        /// <summary>World velocity of <paramref name="point"/> on <paramref name="carrier"/> this frame; zero for none.</summary>
        public static Vector3 VelocityAt(IPlatformCarrier carrier, Vector3 point) =>
            carrier is Object o && o != null
                ? VelocityAt(point, carrier.FrameDelta, carrier.AnchorPosition, carrier.FrameRotation, Time.deltaTime)
                : Vector3.zero;
    }
}
