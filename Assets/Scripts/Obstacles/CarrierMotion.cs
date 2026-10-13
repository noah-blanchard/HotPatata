using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Shared helpers for carriers (<see cref="IPlatformCarrier"/>), see docs/OBSTACLES.md §4.4.
    ///  - <see cref="Moved(IPlatformCarrier, Transform, Rigidbody, Vector3, IReadOnlyList{Collider})"/>: a carrier moves its
    ///    transform in Update, but the physics scene only reads transforms at the next fixed step (autoSyncTransforms is off).
    ///    A rider's CharacterController would then stand on where the deck WAS: sunk into a deck going up, floating over one
    ///    going down, losing its footing every other frame (#97). Every carrier calls Moved once it has moved: its riders are
    ///    carried, anyone a rising deck came up into is put on top of it, and its collider is where it is drawn, so riders,
    ///    ground probes and casts this frame see the deck that is drawn.
    ///  - <see cref="VelocityAt(IPlatformCarrier, Vector3)"/>: how fast a point riding a carrier moves this frame (a throw
    ///    keeps it, a jump keeps its rise, PROJECT_SPEC §8.2 and §11).
    /// </summary>
    public static class CarrierMotion
    {
        const float DeckMinFlatness = 0.9f;   // cosine between a box's most upright axis and up: below it, a wall or a ramp, not a deck
        const float FootprintSlack = 0.5f;    // share of the capsule radius the feet may overhang a deck's edge and still be caught by it
        const float DeckCatchDepth = 0.25f;   // metres under the old deck top a player's feet may be and still be caught by the rising deck
        const float DeckTouchTolerance = 0.01f;   // metres the feet may be under the deck top and still count as standing on it

        /// <summary>
        /// Called by a carrier right after it wrote its moving part's new pose (and its <see cref="IPlatformCarrier.FrameDelta"/>)
        /// in Update. <paramref name="moved"/> is how far the part really moved this frame (a snap included, which carries
        /// nobody); <paramref name="deck"/> its colliders. Moves its local riders with it and gives the physics scene the new
        /// pose, in the order that never leaves the deck inside a player: a deck that rises carries its riders up first, puts
        /// anyone else it came up into on top of it (<see cref="PlayerMotor.LiftOntoDeck"/>), then moves its collider under
        /// them; any other motion moves the collider first, then the riders follow it down or along.
        /// </summary>
        public static void Moved(IPlatformCarrier carrier, Transform part, Rigidbody body, Vector3 moved, IReadOnlyList<Collider> deck)
        {
            float rise = moved.y;
            if (rise <= 0f) SyncPhysics(part, body);
            PlayerMotor.CarryRiders(carrier);
            if (rise <= 0f) return;
            PlayerMotor.LiftOntoDeck(carrier, deck, rise);
            SyncPhysics(part, body);
        }

        /// <summary>
        /// The same for a custom carrier that keeps no list of its colliders (found under <paramref name="part"/> every call) and
        /// moves only by its <see cref="IPlatformCarrier.FrameDelta"/>.
        /// </summary>
        public static void Moved(IPlatformCarrier carrier, Transform part, Rigidbody body) =>
            Moved(carrier, part, body, carrier.FrameDelta, part != null ? part.GetComponentsInChildren<Collider>() : null);

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
        /// How far up (metres) a player whose feet are at <paramref name="feet"/> (capsule radius <paramref name="radius"/>) must
        /// move to stand on a deck box (<paramref name="center"/>, <paramref name="rotation"/>, <paramref name="halfExtents"/>, as
        /// drawn now) that has just risen by <paramref name="rise"/>: from their feet to its top plus the controller's
        /// <paramref name="skin"/>, when the deck came up into them this frame. 0 when it did not: beside it, under it, above
        /// it, or a wall. Pure (EditMode tested).
        /// </summary>
        public static float DeckLift(Vector3 feet, float radius, Vector3 center, Quaternion rotation, Vector3 halfExtents, float rise, float skin)
        {
            if (rise <= 0f) return 0f;
            Quaternion toLocal = Quaternion.Inverse(rotation);
            Vector3 up = toLocal * Vector3.up;
            int k = 0;
            if (Mathf.Abs(up.y) > Mathf.Abs(up[k])) k = 1;
            if (Mathf.Abs(up.z) > Mathf.Abs(up[k])) k = 2;
            if (Mathf.Abs(up[k]) < DeckMinFlatness) return 0f;

            Vector3 sphere = toLocal * (feet + Vector3.up * radius - center);   // the capsule's bottom sphere, in the box's frame
            for (int a = 0; a < 3; a++)
                if (a != k && Mathf.Abs(sphere[a]) > halfExtents[a] + radius * FootprintSlack) return 0f;   // not over the deck

            Vector3 topLocal = sphere;
            topLocal[k] = Mathf.Sign(up[k]) * halfExtents[k];
            float top = (center + rotation * topLocal).y;   // the deck's top right under the player
            if (feet.y >= top - DeckTouchTolerance || feet.y < top - rise - DeckCatchDepth) return 0f;   // on it already, or under it all along
            return top + skin - feet.y;
        }

        /// <summary><see cref="DeckLift"/> against a box collider as its transform draws it now (the physics pose may lag).</summary>
        public static float DeckLift(Vector3 feet, float radius, BoxCollider box, float rise, float skin)
        {
            var tr = box.transform;
            Vector3 s = tr.lossyScale;
            Vector3 half = new Vector3(Mathf.Abs(box.size.x * s.x), Mathf.Abs(box.size.y * s.y), Mathf.Abs(box.size.z * s.z)) * 0.5f;
            return DeckLift(feet, radius, tr.TransformPoint(box.center), tr.rotation, half, rise, skin);
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
