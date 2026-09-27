using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Single home for every gameplay tuning value (PROJECT_SPEC §20/§21).
    /// Gameplay scripts read from this asset; they never hard-code these numbers.
    /// </summary>
    [CreateAssetMenu(fileName = "GameTuning", menuName = "BEEP/Game Tuning")]
    public class GameTuning : ScriptableObject
    {
        [Header("Movement")]
        [Min(0f)] public float moveSpeed = 7f;
        [Tooltip("Units/s² while speeding up on the ground.")]
        [Min(0f)] public float acceleration = 55f;
        [Tooltip("Units/s² while slowing down on the ground.")]
        [Min(0f)] public float braking = 70f;
        [Tooltip("Multiplier on acceleration/braking while airborne (1 = same as ground).")]
        [Range(0f, 1f)] public float airControl = 0.5f;
        [Min(0f)] public float jumpHeight = 1.6f;
        [Tooltip("Downward acceleration in units/s².")]
        [Min(0f)] public float gravity = 28f;
        [Min(0f)] public float coyoteTime = 0.1f;
        [Min(0f)] public float jumpBuffer = 0.1f;

        [Header("Camera / Look (first person)")]
        [Tooltip("Degrees per mouse-delta pixel.")]
        [Min(0f)] public float mouseSensitivity = 0.1f;
        [Tooltip("Degrees per second at full stick deflection.")]
        [Min(0f)] public float stickLookSpeed = 180f;
        [Tooltip("Most you can look up (negative degrees).")]
        public float pitchMin = -80f;
        [Tooltip("Most you can look down.")]
        public float pitchMax = 80f;
        [Range(40f, 110f)] public float fieldOfView = 75f;

        [Header("Bomb - fuse")]
        [Tooltip("Seconds a carrier may hold the bomb. Refreshed by every valid catch.")]
        [Min(0.1f)] public float holdFuseDuration = 6f;
        [Tooltip("Final seconds of the fuse that count as the warning phase.")]
        [Min(0f)] public float warningDuration = 2f;
        [Tooltip("After a catch the bomb is inert for this long before it counts as Held again.")]
        [Min(0f)] public float caughtGraceDuration = 0.35f;

        [Header("Bomb - throw")]
        [Min(0f)] public float throwSpeed = 14f;
        [Tooltip("Degrees the throw is pitched up from the aim direction.")]
        [Range(0f, 45f)] public float throwUpAngle = 8f;
        [Tooltip("Multiplier on Physics.gravity for the thrown bomb (flatter arcs = easier passes).")]
        [Min(0f)] public float bombGravityScale = 0.6f;
        [Tooltip("Max distance of the aim ray that decides where the throw is pointed.")]
        [Min(1f)] public float aimMaxDistance = 40f;

        [Header("Bomb - aim assist (small, never automatic)")]
        [Tooltip("A receiver must be within this many degrees of the throw direction to get assistance.")]
        [Range(0f, 30f)] public float aimAssistAngle = 8f;
        [Min(0f)] public float aimAssistDistance = 16f;
        [Tooltip("0 = no assist, 1 = fully corrected toward the receiver's catch point.")]
        [Range(0f, 1f)] public float aimAssistStrength = 0.5f;

        [Header("Catch")]
        [Tooltip("Radius of the receiver's catch sphere.")]
        [Min(0.1f)] public float catchRadius = 0.9f;
        [Tooltip("How far in front of the receiver the catch sphere is pushed.")]
        [Min(0f)] public float catchFrontBias = 0.25f;
        [Tooltip("Height of the catch sphere centre above the player's feet (upper torso / hands).")]
        [Min(0f)] public float catchCenterHeight = 1.3f;

        [Header("Bomb - feedback")]
        [Tooltip("Seconds between beeps for the four fuse stages: calm, medium, urgent, critical.")]
        public float[] beepIntervals = { 1.0f, 0.55f, 0.28f, 0.12f };
        [Tooltip("Fraction of the fuse consumed at which stages medium / urgent / critical begin (spec §7.2).")]
        public float[] stageThresholds = { 0.5f, 0.75f, 0.9f };
        [Range(0f, 1f)] public float beepVolume = 0.6f;

        [Header("Run")]
        [Tooltip("Seconds between an explosion and the section being playable again.")]
        [Min(0f)] public float resetDelay = 1.0f;
    }
}
