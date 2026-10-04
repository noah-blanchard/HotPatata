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

        [Header("Movement - flow (turning and momentum)")]
        [Tooltip("Degrees per second the run direction swings toward the stick at walking speed. Lower = more carve, higher = snappier.")]
        [Min(0f)] public float groundTurnRate = 900f;
        [Tooltip("Turn rate at sprint speed and above: fast runs carve wider, so they have weight.")]
        [Min(0f)] public float sprintTurnRate = 460f;
        [Tooltip("A change of direction sharper than this (degrees) is a reversal: brake and re-accelerate instead of carving round.")]
        [Range(90f, 180f)] public float reverseAngle = 140f;
        [Tooltip("Units/s² a grounded player above their target speed (after a slide, or letting go of sprint) sheds speed. Low = momentum.")]
        [Min(0f)] public float overspeedDeceleration = 9f;
        [Tooltip("Turn rate while airborne, degrees per second.")]
        [Min(0f)] public float airTurnRate = 220f;
        [Tooltip("Units/s² of drag in the air with no stick input (the jump keeps its momentum; pull back to brake).")]
        [Min(0f)] public float airDrag = 3f;
        [Tooltip("Units/s² of drag in the air above sprint speed, so chained jumps cannot keep slide speed forever (no bunny-hop).")]
        [Min(0f)] public float airOverspeedDrag = 2.5f;

        [Header("Movement - sprint (hold, no stamina)")]
        [Min(0f)] public float sprintSpeed = 11f;
        [Tooltip("Units/s² from run speed up to sprint speed. Lower than Acceleration, so a sprint builds instead of snapping.")]
        [Min(0f)] public float sprintAcceleration = 18f;
        [Tooltip("Sprint only applies while the stick points mostly forward: its forward share must be at least this (0.5 = within 60°).")]
        [Range(0f, 1f)] public float sprintForwardDot = 0.5f;

        [Header("Movement - slide and crouch")]
        [Tooltip("Pressing crouch on the ground at this speed or faster starts a slide; slower, it is a crouch-walk.")]
        [Min(0f)] public float slideMinEntrySpeed = 7f;
        [Tooltip("Speed added when a slide starts.")]
        [Min(0f)] public float slideBoost = 3.5f;
        [Tooltip("Seconds after a boosted slide before another slide boosts again (no slide spamming).")]
        [Min(0f)] public float slideBoostCooldown = 1.0f;
        [Tooltip("Units/s² a slide loses on flat ground. Sets how long a slide lasts.")]
        [Min(0f)] public float slideFriction = 7f;
        [Tooltip("A slide ends below this speed (crouch-walk if crouch is still held).")]
        [Min(0f)] public float slideExitSpeed = 5f;
        [Min(0f)] public float slideMaxSpeed = 18f;
        [Tooltip("Degrees per second the stick can steer a slide.")]
        [Min(0f)] public float slideSteerRate = 90f;
        [Tooltip("Multiplier on gravity along a slope while sliding (downhill speeds you up, uphill slows you).")]
        [Min(0f)] public float slideSlopeAcceleration = 1f;
        [Min(0f)] public float crouchSpeed = 4f;
        [Tooltip("Capsule height while sliding or crouched, in metres (standing height comes from the CharacterController).")]
        [Min(0.6f)] public float crouchHeight = 1.0f;
        [Tooltip("How fast the eye and catch height follow crouching (1/s).")]
        [Min(1f)] public float crouchTransitionRate = 14f;

        [Header("Movement - mantle (automatic ledge climb)")]
        [Tooltip("Highest ledge (above the feet) that can be climbed by moving into it while airborne.")]
        [Min(0f)] public float mantleMaxHeight = 1.4f;
        [Tooltip("A ledge top at least this far above the feet is climbed (in the air nothing steps up, so this is small).")]
        [Min(0f)] public float mantleMinHeight = 0.1f;
        [Tooltip("How far ahead of the capsule the ledge may be, in metres.")]
        [Min(0.05f)] public float mantleReach = 0.45f;
        [Min(0.05f)] public float mantleDuration = 0.28f;

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
        [Min(0f)] public float fovKickAtSpeed = 4f;
        [Tooltip("Extra field of view at sprint speed, in degrees (between run and sprint it blends).")]
        [Min(0f)] public float fovKickAtSprint = 9f;
        [Tooltip("Extra field of view at the fastest slides, in degrees (reached at 3x the run-to-sprint step).")]
        [Min(0f)] public float fovKickMax = 13f;
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
        [Tooltip("Vignette strength while sliding (0..1). Running and sprinting stay clean.")]
        [Range(0f, 0.6f)] public float speedVignette = 0.22f;
        [Tooltip("Anime speed lines at the screen edges: 0 = off, 1 = full. They start above run speed.")]
        [Range(0f, 1f)] public float speedLinesStrength = 0.8f;
        [Tooltip("Wind streak particles flying past above run speed: 0 = off, 1 = full.")]
        [Range(0f, 1f)] public float windStreaksStrength = 1f;
        [Tooltip("Degrees the view rolls into a slide (toward the steering side).")]
        [Min(0f)] public float slideRollDegrees = 3.5f;
        [Tooltip("Camera shake from a nearby explosion, in degrees at point blank.")]
        [Min(0f)] public float explosionShake = 2.2f;
        [Tooltip("Accessibility (spec §19): 0 = full flashes, 1 = no bright flashes (explosion flash, light, glare).")]
        [Range(0f, 1f)] public float flashReduction = 0f;

        [Header("Bomb - fuse")]
        [Tooltip("Seconds a carrier may hold the bomb. Refreshed by every valid catch.")]
        [Min(0.1f)] public float holdFuseDuration = 6f;
        [Tooltip("Final seconds of the fuse that count as the warning phase.")]
        [Min(0f)] public float warningDuration = 2f;
        [Tooltip("After a catch the bomb is inert for this long before it counts as Held again.")]
        [Min(0f)] public float caughtGraceDuration = 0.35f;

        [Header("Bomb - fuse zones (PROJECT_SPEC §7.3; forbidden zones explode)")]
        [Tooltip("Fuse rate multiplier while the carrier stands in a hot zone.")]
        [Min(1f)] public float hotZoneFuseRate = 2f;
        [Tooltip("Fuse rate multiplier while the carrier stands in a cold zone: a breather, never a pause.")]
        [Range(0.05f, 1f)] public float coldZoneFuseRate = 0.5f;

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
        [Tooltip("The run speed a throw can inherit from is capped here, so sliding or sprinting does not make passes wildly long.")]
        [Min(0f)] public float throwInheritMaxSpeed = 11f;
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
        [Min(0f)] public float tumbleDegreesPerSecond = 1200f;
        [Tooltip("0 = a clean end-over-end spin, 1 = tumbles every which way (two spins on random axes, the main axis wandering in flight).")]
        [Range(0f, 1f)] public float tumbleRandomness = 0.85f;
        [Tooltip("Gentle sway of the potato in the hand, in degrees.")]
        [Min(0f)] public float handSwayDegrees = 3f;

        [Header("Bomb - VFX (visual only)")]
        [Tooltip("Seconds the flight trail lingers behind a thrown potato.")]
        [Min(0f)] public float trailTime = 0.22f;
        [Tooltip("Trail width at the potato, in metres (at the fastest throw; slower throws are thinner).")]
        [Min(0f)] public float trailWidth = 0.16f;
        [Tooltip("Fuse sparks per second for the four fuse stages (calm .. critical).")]
        public float[] fuseSparkRates = { 14f, 30f, 60f, 130f };
        [Tooltip("Smoke puffs left per metre of flight.")]
        [Min(0f)] public float flightPuffsPerMetre = 1.6f;

        [Header("Players - identity (spec §19: never colour alone)")]
        [Tooltip("Body colour per slot. Chosen to stay distinct under protanopia, deuteranopia and tritanopia, and away from hazard red, the potato orange and the carrier yellow (ARCHITECTURE §25).")]
        public Color[] playerColors =
        {
            new Color(0.1f, 0.32f, 1f), new Color(0f, 0.75f, 1f), new Color(0.6f, 0f, 0.4f), new Color(0.95f, 0.95f, 0.95f)
        };
        [Tooltip("Shape per slot, shown next to the colour wherever a player is identified (lobby, results).")]
        public PlayerShape[] playerShapes = { PlayerShape.Circle, PlayerShape.Triangle, PlayerShape.Square, PlayerShape.Diamond };

        [Header("Menu (in-world stations, ARCHITECTURE §6.2)")]
        [Tooltip("Seconds the menu camera takes to fly between stations (eased). At camera effects 0 it cuts instead.")]
        [Min(0f)] public float menuTravelSeconds = 1.1f;
        [Tooltip("Seconds a menu mannequin takes to hop to its lobby spot, or back to the show.")]
        [Min(0f)] public float menuStepSeconds = 0.6f;

        [Header("Run")]
        [Tooltip("Seconds between an explosion and the section being playable again.")]
        [Min(0f)] public float resetDelay = 1.0f;
    }
}
