using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Single home for every gameplay tuning value (PROJECT_SPEC §20/§21).
    /// Gameplay scripts read from this asset; they never hard-code these numbers.
    /// </summary>
    [CreateAssetMenu(fileName = "GameTuning", menuName = "HotPatata/Game Tuning")]
    public class GameTuning : ScriptableObject
    {
        [Header("Movement")]
        [Min(0f)] public float moveSpeed = 8f;
        [Tooltip("Acceleration at the start of a run, as a fraction of Acceleration. It ramps to 1 by half speed, so a run builds up instead of snapping to speed.")]
        [Range(0.1f, 1f)] public float startAccelerationMultiplier = 0.5f;
        [Tooltip("Units/s² while speeding up on the ground.")]
        [Min(0f)] public float acceleration = 55f;
        [Tooltip("Units/s² while slowing down on the ground.")]
        [Min(0f)] public float braking = 60f;
        [Tooltip("Multiplier on acceleration/braking while airborne (1 = same as ground).")]
        [Range(0f, 1f)] public float airControl = 0.65f;
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
        [Min(0f)] public float chargeFovZoom = 2f;
        [Tooltip("Vignette strength at full speed (0..1).")]
        [Range(0f, 0.6f)] public float speedVignette = 0.28f;
        [Tooltip("Wind volume at full speed (0..1).")]
        [Range(0f, 1f)] public float windVolume = 0.3f;
        [Tooltip("Footstep and landing sounds (0 = off).")]
        [Range(0f, 1f)] public float footstepVolume = 0.2f;

        [Header("Bomb - fuse")]
        [Tooltip("Seconds a carrier may hold the bomb. Refreshed by every valid catch.")]
        [Min(0.1f)] public float holdFuseDuration = 6f;
        [Tooltip("Final seconds of the fuse that count as the warning phase.")]
        [Min(0f)] public float warningDuration = 2f;
        [Tooltip("After a catch the bomb is inert for this long before it counts as Held again.")]
        [Min(0f)] public float caughtGraceDuration = 0.35f;

        [Header("Bomb - throw")]
        [Tooltip("Launch speed of a tap (no charge). Sets the shortest pass.")]
        [Min(0f)] public float throwSpeedMin = 14f;
        [Tooltip("Launch speed at full charge. Longer, faster throws.")]
        [Min(0f)] public float throwSpeedMax = 28f;
        [Tooltip("Seconds of holding the throw button to reach full charge.")]
        [Min(0.05f)] public float throwChargeTime = 0.45f;
        [Tooltip("Normalized point in THROW where the arm waits while the button stays held.")]
        [Range(0.05f, 0.9f)] public float throwAnimationHoldNormalized = 0.38f;
        [Tooltip("Minimum time before the same throw wind-up can be triggered again on a player.")]
        [Min(0f)] public float throwAnimationReplayGuard = 0.65f;
        [Tooltip("Degrees the throw is pitched up from the aim direction (counters the drop on short passes).")]
        [Range(0f, 45f)] public float throwUpAngle = 6f;
        [Tooltip("Multiplier on Physics.gravity for the thrown bomb. Above 1 the potato feels heavy and drops quickly; " +
                 "below 1 it floats (flatter, much longer lobs).")]
        [Min(0f)] public float bombGravityScale = 1.5f;
        [Tooltip("Share of the thrower's run speed ALONG the aim direction that the throw keeps (running forward throws harder). " +
                 "Sideways and vertical motion are never added, so strafing or jumping does not push the throw off your aim.")]
        [Range(0f, 1f)] public float throwInheritForward = 0.5f;
        [Tooltip("Max distance of the aim ray that decides where the throw is pointed.")]
        [Min(1f)] public float aimMaxDistance = 40f;

        [Header("Bomb - aim assist (at release, direction only, never automatic)")]
        [Tooltip("0 = no assist. How far (0..1) the launch direction turns toward the receiver, dead on and up close; capped below.")]
        [Range(0f, 1f)] public float assistStrength = 0.6f;
        [Tooltip("The receiver's body must be within this many degrees of your aim to get any help.")]
        [Range(0f, 15f)] public float assistConeDegrees = 6f;
        [Tooltip("No assist beyond this horizontal distance, in metres.")]
        [Min(0f)] public float assistMaxRange = 14f;
        [Tooltip("Full strength up to this distance; it fades to assistFarStrength at assistMaxRange.")]
        [Min(0f)] public float assistFullStrengthDistance = 8f;
        [Range(0f, 1f)] public float assistFarStrength = 0.4f;
        [Tooltip("Most the assist may turn a throw, in degrees.")]
        [Range(0f, 10f)] public float assistMaxCorrectionDegrees = 3.5f;
        [Tooltip("Most of that turn that may point UP, in degrees (upward turns add range; this keeps the assist from adding much).")]
        [Range(0f, 5f)] public float assistMaxElevationDegrees = 1f;
        [Tooltip("How much the assist aims ahead of a moving receiver (0 = where they are, 1 = where they will be).")]
        [Range(0f, 1f)] public float assistLeadFactor = 0.8f;

        [Header("Catch")]
        [Tooltip("Radius of the receiver's catch sphere.")]
        [Min(0.1f)] public float catchRadius = 1.0f;
        [Tooltip("Extra reach (metres) when the bomb arrives from in front of the receiver's view: facing the pass makes it easier.")]
        [Min(0f)] public float catchFacingBonus = 0.3f;
        [Tooltip("The bomb counts as 'in front' within this many degrees of where the receiver looks.")]
        [Range(0f, 180f)] public float catchFacingAngle = 70f;
        [Tooltip("Vertical reach as a fraction of the horizontal reach: arms reach out to the sides more than down to the feet, " +
                 "so a throw arriving at the receiver's knees is a miss while one passing beside their shoulder is not.")]
        [Range(0.3f, 1f)] public float catchVerticalScale = 0.6f;
        [Tooltip("A catch press this long (seconds) after the bomb was in reach still catches, if it has not hit anything yet.")]
        [Range(0f, 0.15f)] public float catchLateGrace = 0.06f;
        [Tooltip("Pressing catch opens a window this long; the bomb must reach the receiver inside it. Smaller = harder.")]
        [Min(0.02f)] public float catchWindowDuration = 0.4f;
        [Tooltip("After a window closes, catch cannot be pressed again for this long (stops button mashing).")]
        [Min(0f)] public float catchCooldown = 0.35f;
        [Tooltip("How far in front of the receiver the catch sphere is pushed.")]
        [Min(0f)] public float catchFrontBias = 0.25f;
        [Tooltip("Height of the catch sphere centre above the player's feet (upper torso / hands).")]
        [Min(0f)] public float catchCenterHeight = 1.3f;

        [Header("Network - catch lag compensation")]
        [Tooltip("Online only. How far back (seconds) the host accepts a remote player's catch that they saw happen on their screen, " +
                 "and how long it may hold a lethal contact right after the bomb passed near a remote receiver. 0 = off. " +
                 "Also the cap against abuse: the lag it can hide is about this value minus ~0.1 s.")]
        [Range(0f, 0.5f)] public float catchLagCompensation = 0.35f;

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
