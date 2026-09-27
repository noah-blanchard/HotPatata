using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Everything that went into one throw (or the throw you would make if you released now): the charge,
    /// the raw aim, the assisted launch velocity and why. Read by the reticle, the debug overlay and the logs.
    /// </summary>
    public struct ThrowShot
    {
        public bool Valid;
        public float Charge01;
        /// <summary>Launch speed from the charge alone (before any inherited run speed).</summary>
        public float Speed;
        public Vector3 Origin;
        /// <summary>Where the eyes were looking.</summary>
        public Vector3 AimForward;
        /// <summary>Launch velocity with no assist at all.</summary>
        public Vector3 RawVelocity;
        /// <summary>The launch velocity actually used.</summary>
        public Vector3 Velocity;

        public Player AssistTarget;
        /// <summary>Degrees between the raw aim and the target's body (0 = dead on).</summary>
        public float AssistAngle;
        /// <summary>Degrees the launch direction was turned by the assist.</summary>
        public float AssistCorrection;
        /// <summary>The throw could not reach the target at its own speed: only its heading was corrected.</summary>
        public bool AssistYawOnly;

        public bool HasAssist => AssistTarget != null;
    }
}
