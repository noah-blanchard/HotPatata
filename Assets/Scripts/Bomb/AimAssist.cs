using System.Collections.Generic;
using UnityEngine;

namespace Beep
{
    /// <summary>What the release-time aim assist did to one throw.</summary>
    public struct AssistResult
    {
        public Player Target;
        /// <summary>Degrees between the raw aim and the target's body (0 = dead on).</summary>
        public float Angle;
        /// <summary>0..1 fraction of the way the direction was allowed to turn toward the solution (before the caps).</summary>
        public float Strength;
        /// <summary>The throw's own speed can reach the target. False = heading-only correction, it will fall short.</summary>
        public bool Reachable;
        /// <summary>Final launch direction (unit).</summary>
        public Vector3 Direction;
        /// <summary>Degrees the launch direction was actually turned.</summary>
        public float Correction;
    }

    /// <summary>
    /// Release-time aim assist (PROJECT_SPEC §8.3). Pure functions, used by the thrower's lock preview and by the
    /// throw itself, so what the brackets promise is what the throw does.
    ///
    /// It only ever turns the launch DIRECTION, a few degrees, once, at release:
    ///  - the receiver must be in line of sight, within <see cref="GameTuning.assistMaxRange"/> and within
    ///    <see cref="GameTuning.assistConeDegrees"/> of the raw aim (their body counts as a small disc);
    ///  - the target direction is the low arc that reaches them at the throw's OWN speed (plus the thrower's
    ///    inherited velocity), slightly ahead of a moving receiver;
    ///  - the turn is capped (<see cref="GameTuning.assistMaxCorrectionDegrees"/>, of which at most
    ///    <see cref="GameTuning.assistMaxElevationDegrees"/> upward), so it can add only a little range;
    ///  - a throw too weak to reach only has its heading corrected, and falls short.
    /// Nothing here changes speed, and nothing touches the bomb once it has left the hand.
    /// </summary>
    public static class AimAssist
    {
        /// <summary>Radius of a receiver's body as a target: a close receiver is not harder to hit than a far one.</summary>
        public const float BodyRadius = 0.4f;
        const int LeadIterations = 3;
        const float MinDistance = 0.75f;
        const float TypicalPassSpeed = 20f;   // m/s, only to guess where a moving receiver is being led to

        /// <summary>
        /// Picks the receiver the assist would help (null if none) and returns the assisted launch direction for a
        /// throw at <paramref name="speed"/> along <paramref name="rawDirection"/>.
        /// </summary>
        public static AssistResult Apply(GameTuning t, Vector3 eye, Vector3 aimForward, Vector3 origin, Vector3 rawDirection,
            float speed, Vector3 inherited, Player thrower, IReadOnlyList<Player> players, int blockMask)
        {
            var target = PickTarget(t, eye, aimForward, thrower, players, blockMask, out float angle, out float distance);
            if (target == null) return new AssistResult { Direction = rawDirection };

            var result = Correct(t, origin, rawDirection, speed, inherited, target.CatchVolume.CatchCenter, target.Velocity, angle, distance);
            result.Target = target;
            return result;
        }

        /// <summary>The receiver closest to the aim inside the cone, in range and in line of sight; null if none.</summary>
        public static Player PickTarget(GameTuning t, Vector3 eye, Vector3 aimForward, Player thrower, IReadOnlyList<Player> players,
            int blockMask, out float angle, out float distance)
        {
            Player best = null;
            angle = distance = 0f;
            if (t == null || players == null || t.assistStrength <= 0f) return null;

            float bestAngle = float.MaxValue;
            foreach (var p in players)
            {
                if (p == null || p == thrower || p.CatchVolume == null) continue;

                Vector3 center = p.CatchVolume.CatchCenter;
                Vector3 to = center - eye;
                float d = to.magnitude;
                float flat = new Vector2(to.x, to.z).magnitude;
                if (d < MinDistance || flat > t.assistMaxRange) continue;

                // Aiming where a moving receiver is going counts as aiming at them.
                Vector3 v = p.Velocity;
                Vector3 led = to + new Vector3(v.x, 0f, v.z) * (t.assistLeadFactor * d / TypicalPassSpeed);
                float a = Mathf.Min(OffAimAngle(aimForward, to), OffAimAngle(aimForward, led));
                if (a > t.assistConeDegrees || a >= bestAngle) continue;
                if (Physics.Linecast(eye, center, blockMask, QueryTriggerInteraction.Ignore)) continue;   // no assist through walls

                best = p;
                bestAngle = a;
                distance = flat;
            }

            angle = best != null ? bestAngle : 0f;
            return best;
        }

        /// <summary>Degrees between the aim and the edge of a body-sized disc around the target (0 when the aim is on the body).</summary>
        public static float OffAimAngle(Vector3 aimForward, Vector3 toTarget)
        {
            float d = toTarget.magnitude;
            if (d < 1e-4f) return 0f;
            float bodyAngle = Mathf.Atan2(BodyRadius, d) * Mathf.Rad2Deg;
            return Mathf.Max(0f, Vector3.Angle(aimForward, toTarget) - bodyAngle);
        }

        /// <summary>
        /// The capped correction of <paramref name="rawDirection"/> toward a target at <paramref name="center"/>
        /// (moving at <paramref name="targetVelocity"/>). Pure: no scene queries, unit tested directly.
        /// </summary>
        public static AssistResult Correct(GameTuning t, Vector3 origin, Vector3 rawDirection, float speed, Vector3 inherited,
            Vector3 center, Vector3 targetVelocity, float angle, float distance)
        {
            var r = new AssistResult { Angle = angle, Direction = rawDirection };
            r.Strength = StrengthFor(t, angle, distance);
            if (r.Strength <= 0f) return r;

            float g = ThrowBallistics.Gravity(t);
            Vector3 lead = new Vector3(targetVelocity.x, 0f, targetVelocity.z) * t.assistLeadFactor;
            Vector3 drift = lead - inherited;

            // Solve in the frame that moves with the launch's inherited velocity, aiming where the receiver will be.
            Vector3 solved = default;
            float time = 0f;
            r.Reachable = false;
            for (int i = 0; i < LeadIterations; i++)
            {
                Vector3 to = center - origin + drift * time;
                if (!ThrowBallistics.TrySolveLowArc(to, speed, g, out solved, out time)) break;
                r.Reachable = true;
            }

            Vector3 desired;
            if (r.Reachable)
            {
                desired = Vector3.Slerp(rawDirection, solved, r.Strength);
            }
            else
            {
                // Too weak to get there: heading only, so it visibly falls short. Never any extra lift.
                Vector3 flatTo = center - origin;
                float yawTo = Mathf.Atan2(flatTo.x, flatTo.z) * Mathf.Rad2Deg;
                float yawRaw = Mathf.Atan2(rawDirection.x, rawDirection.z) * Mathf.Rad2Deg;
                desired = Quaternion.AngleAxis(Mathf.DeltaAngle(yawRaw, yawTo) * r.Strength, Vector3.up) * rawDirection;
            }

            r.Direction = Clamp(rawDirection, desired, t.assistMaxCorrectionDegrees, t.assistMaxElevationDegrees);
            r.Correction = Vector3.Angle(rawDirection, r.Direction);
            return r;
        }

        /// <summary>0 outside the cone, full strength dead on, fading toward the cone's edge and with distance.</summary>
        public static float StrengthFor(GameTuning t, float angle, float distance)
        {
            if (t.assistConeDegrees <= 0f || angle > t.assistConeDegrees) return 0f;
            float cone = 1f - angle / t.assistConeDegrees;
            float far = Mathf.InverseLerp(t.assistFullStrengthDistance, Mathf.Max(t.assistFullStrengthDistance + 0.01f, t.assistMaxRange), distance);
            float falloff = Mathf.Lerp(1f, t.assistFarStrength, far);
            return Mathf.Clamp01(t.assistStrength * cone * falloff);
        }

        /// <summary>Limits the turn from <paramref name="from"/> to <paramref name="to"/>: total and upward caps, in degrees.</summary>
        public static Vector3 Clamp(Vector3 from, Vector3 to, float maxTotal, float maxUp)
        {
            Angles(from, out float yaw0, out float elev0);
            Angles(to, out float yaw1, out float elev1);

            float dYaw = Mathf.DeltaAngle(yaw0, yaw1);
            float dElev = Mathf.Min(elev1 - elev0, maxUp);
            float yawArc = dYaw * Mathf.Cos(elev0 * Mathf.Deg2Rad);   // yaw degrees shrink toward the vertical
            float total = Mathf.Sqrt(yawArc * yawArc + dElev * dElev);
            if (total > maxTotal && total > 1e-5f)
            {
                float k = maxTotal / total;
                dYaw *= k;
                dElev *= k;
            }
            Vector3 result = FromAngles(yaw0 + dYaw, elev0 + dElev);

            // The yaw/elevation split is an approximation of the true angle: make the total cap exact.
            float actual = Vector3.Angle(from, result);
            if (actual > maxTotal && actual > 1e-5f) result = Vector3.Slerp(from, result, maxTotal / actual).normalized;
            return result;
        }

        static void Angles(Vector3 d, out float yaw, out float elevation)
        {
            yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            elevation = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        }

        static Vector3 FromAngles(float yaw, float elevation) => Quaternion.Euler(-elevation, yaw, 0f) * Vector3.forward;
    }
}
