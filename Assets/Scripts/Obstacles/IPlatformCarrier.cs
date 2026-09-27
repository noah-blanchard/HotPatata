using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A surface that carries whoever stands on it (moving platforms, elevators, conveyor belts). PlayerMotor adds
    /// <see cref="FrameDelta"/> to its own move each frame while grounded on one. Carriers update before players
    /// (execution order -50) so the delta is this frame's.
    /// </summary>
    public interface IPlatformCarrier
    {
        /// <summary>How far a rider is carried this frame (world space, metres).</summary>
        Vector3 FrameDelta { get; }
    }
}
