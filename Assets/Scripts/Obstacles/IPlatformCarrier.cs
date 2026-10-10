using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A surface that carries whoever stands on it (moving platforms, elevators, conveyor belts). PlayerMotor adds
    /// <see cref="FrameDelta"/> to its own move each frame while grounded on one. Carriers update before players
    /// (execution order -50) so the delta is this frame's.
    ///
    /// Online, a rider on a carrier that <see cref="Moves"/> is synced relative to it (docs/netcode-deterministic-plan.md
    /// §2.3): its owner sends <see cref="CarrierId"/> and its offset from <see cref="AnchorPosition"/>, and every other
    /// machine rebuilds it against the carrier as that machine draws it, so the rider never lags behind the platform.
    /// </summary>
    public interface IPlatformCarrier
    {
        /// <summary>How far a rider is carried this frame (world space, metres).</summary>
        Vector3 FrameDelta { get; }

        /// <summary>The same number on every machine for the same carrier (<see cref="CarrierRegistry"/>); 0 = none.</summary>
        int CarrierId { get; }

        /// <summary>The carried part moves, so riders are synced relative to it. False for a belt (a static surface).</summary>
        bool Moves { get; }

        /// <summary>World position of the carried part this frame: the origin of a rider's offset.</summary>
        Vector3 AnchorPosition { get; }

        /// <summary>World rotation of the carried part this frame: the frame of a rider's offset (identity unless it turns).</summary>
        Quaternion AnchorRotation => Quaternion.identity;

        /// <summary>How far the carried part turned this frame about <see cref="AnchorPosition"/> (world space; identity unless it turns).</summary>
        Quaternion FrameRotation => Quaternion.identity;
    }
}
