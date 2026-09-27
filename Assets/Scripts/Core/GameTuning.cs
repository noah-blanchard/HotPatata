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
        [Tooltip("Acceleration at the start of a run, as a fraction of Acceleration. It ramps to 1 by half speed, so a run builds up instead of snapping to speed.")]
        [Range(0.1f, 1f)] public float startAccelerationMultiplier = 0.5f;
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

        [Header("View feel (first person)")]
        [Tooltip("Master scale for every camera effect below (accessibility: 0 = a perfectly steady camera).")]
        [Range(0f, 1f)] public float viewEffectsStrength = 1f;
        [Tooltip("Extra field of view at full run speed, in degrees.")]
        [Min(0f)] public float fovKickAtSpeed = 12f;
        [Tooltip("Degrees the view leans when strafing at full speed.")]
        [Min(0f)] public float rollDegrees = 2.2f;
        [Tooltip("Height of the walking bob in metres at full speed.")]
        [Min(0f)] public float bobAmplitude = 0.035f;
        [Tooltip("Metres per footstep (drives the bob rhythm and footstep sounds).")]
        [Min(0.3f)] public float stepLength = 1.15f;
        [Tooltip("How far the view dips on a hard landing, in metres.")]
        [Min(0f)] public float landingDip = 0.10f;
        [Tooltip("Degrees the field of view narrows while charging a throw (focus).")]
        [Min(0f)] public float chargeFovZoom = 6f;
        [Tooltip("Vignette strength at full speed (0..1).")]
        [Range(0f, 0.6f)] public float speedVignette = 0.28f;
        [Tooltip("Wind volume at full speed (0..1).")]
        [Range(0f, 1f)] public float windVolume = 0.3f;

        [Header("Bomb - fuse")]
        [Tooltip("Seconds a carrier may hold the bomb. Refreshed by every valid catch.")]
        [Min(0.1f)] public float holdFuseDuration = 6f;
        [Tooltip("Final seconds of the fuse that count as the warning phase.")]
        [Min(0f)] public float warningDuration = 2f;
        [Tooltip("After a catch the bomb is inert for this long before it counts as Held again.")]
        [Min(0f)] public float caughtGraceDuration = 0.35f;

        [Header("Bomb - throw")]
        [Tooltip("Launch speed of a tap (no charge). Sets the shortest pass.")]
        [Min(0f)] public float throwSpeedMin = 10f;
        [Tooltip("Launch speed at full charge. Longer, faster throws.")]
        [Min(0f)] public float throwSpeedMax = 24f;
        [Tooltip("Seconds of holding the throw button to reach full charge.")]
        [Min(0.05f)] public float throwChargeTime = 1.0f;
        [Tooltip("Normalized point in THROW where the arm waits while the button stays held.")]
        [Range(0.05f, 0.9f)] public float throwAnimationHoldNormalized = 0.38f;
        [Tooltip("Seconds between starting the THROW animation and releasing the potato from the hands.")]
        [Min(0f)] public float throwAnimationReleaseDelay = 0.22f;
        [Tooltip("Minimum time before the same throw wind-up can be triggered again on a player.")]
        [Min(0f)] public float throwAnimationReplayGuard = 0.65f;
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

        [Header("Bomb - soft homing (visible, never perfect)")]
        [Tooltip("0 = no homing, 1 = the lock is as strong as it can be. Fades toward the edge of the cone.")]
        [Range(0f, 1f)] public float homingStrength = 0.7f;
        [Tooltip("Half-angle of the cone (around your throw direction) in which a receiver can be locked.")]
        [Range(5f, 60f)] public float homingConeDegrees = 28f;
        [Min(0f)] public float homingRange = 25f;
        [Tooltip("How fast the flight path can bend toward the target, degrees per second at 14 m/s (scales with speed, so hard throws bend too).")]
        [Min(0f)] public float homingTurnRate = 200f;
        [Tooltip("Random error on the aim point, in degrees, chosen per throw. This is what lets a homing throw still miss.")]
        [Range(0f, 15f)] public float homingSpreadDegrees = 5f;
        [Tooltip("Inside this distance from the target, with their catch window open, the bomb is pulled into their hands.")]
        [Min(0f)] public float magnetRadius = 2f;
        [Range(0f, 1f)] public float magnetStrength = 0.8f;

        [Header("Catch")]
        [Tooltip("Radius of the receiver's catch sphere.")]
        [Min(0.1f)] public float catchRadius = 0.9f;
        [Tooltip("Pressing catch opens a window this long; the bomb must reach the receiver inside it. Smaller = harder.")]
        [Min(0.02f)] public float catchWindowDuration = 0.4f;
        [Tooltip("After a window closes, catch cannot be pressed again for this long (stops button mashing).")]
        [Min(0f)] public float catchCooldown = 0.5f;
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

        [Header("Bomb - natural motion (visual only)")]
        [Tooltip("Average tumble speed in flight, degrees per second. Faster throws spin faster.")]
        [Min(0f)] public float tumbleDegreesPerSecond = 480f;
        [Tooltip("0 = a clean end-over-end spin, 1 = very wobbly and unpredictable.")]
        [Range(0f, 1f)] public float tumbleRandomness = 0.35f;
        [Tooltip("Gentle sway of the potato in the hand, in degrees.")]
        [Min(0f)] public float handSwayDegrees = 3f;

        [Header("Run")]
        [Tooltip("Seconds between an explosion and the section being playable again.")]
        [Min(0f)] public float resetDelay = 1.0f;
    }
}
