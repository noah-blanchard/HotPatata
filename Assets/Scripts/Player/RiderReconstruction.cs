using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Where to draw a remote player who rides a moving carrier (docs/netcode-deterministic-plan.md §2.3). The owner
    /// sends which carrier it stands on and its offset from it; this machine puts the player at that offset from the
    /// carrier <em>as this machine draws it</em>, so the rider stays glued to the platform whatever the latency. Off a
    /// carrier, the replicated world position is used. Boarding and leaving blend between the two over
    /// <see cref="GameTuning.riderBlendSeconds"/>. Only the offset is smoothed, never the absolute position.
    /// Plain C# with no scene dependency (EditMode tested); <see cref="NetworkPlayer"/> drives it.
    /// </summary>
    public sealed class RiderReconstruction
    {
        /// <summary>The carrier being used (kept while blending out after leaving it); 0 = none.</summary>
        public int CarrierId { get; private set; }
        /// <summary>Smoothed offset from the carrier's anchor.</summary>
        public Vector3 Offset { get; private set; }
        /// <summary>0 = the replicated world position .. 1 = rebuilt on the carrier.</summary>
        public float Weight { get; private set; }

        public bool Active => CarrierId != 0;

        /// <summary>Exponential smoothing factor for one frame at <paramref name="rate"/> per second.</summary>
        public static float SmoothFactor(float rate, float dt) => 1f - Mathf.Exp(-Mathf.Max(0f, rate) * Mathf.Max(0f, dt));

        /// <summary>
        /// Takes the owner's latest state: <paramref name="carrierId"/> (0 = not riding) and its offset. Boarding (or
        /// switching carriers) adopts the offset at once; while riding the same carrier the offset is smoothed. Leaving
        /// keeps the last carrier and offset so the blend out has something to start from.
        /// </summary>
        public void Retarget(int carrierId, Vector3 offset, float dt, float smoothing)
        {
            if (carrierId == 0) return;
            if (carrierId != CarrierId)
            {
                CarrierId = carrierId;
                Offset = offset;
                return;
            }
            Offset = Vector3.Lerp(Offset, offset, SmoothFactor(smoothing, dt));
        }

        /// <summary>
        /// The position to draw this frame. <paramref name="world"/> is the replicated world position,
        /// <paramref name="anchor"/> the current anchor of carrier <see cref="CarrierId"/> on this machine.
        /// </summary>
        public Vector3 Blend(Vector3 world, Vector3 anchor, bool riding, float dt, float blendSeconds, float snapDistance)
        {
            Vector3 rebuilt = anchor + Offset;
            if (!riding && (rebuilt - world).sqrMagnitude > snapDistance * snapDistance)
            {
                Clear();   // left by a teleport (reset, respawn): snap, never sweep through the level
                return world;
            }

            float step = blendSeconds > 0f ? dt / blendSeconds : 1f;
            Weight = Mathf.MoveTowards(Weight, riding ? 1f : 0f, step);
            if (!riding && Weight <= 0f)
            {
                Clear();
                return world;
            }
            return Vector3.Lerp(world, rebuilt, Mathf.SmoothStep(0f, 1f, Weight));
        }

        public void Clear()
        {
            CarrierId = 0;
            Offset = Vector3.zero;
            Weight = 0f;
        }
    }
}
