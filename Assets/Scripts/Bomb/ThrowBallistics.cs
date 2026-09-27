using System.Collections.Generic;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Pure maths of a thrown bomb: a plain ballistic arc under the bomb's own gravity (no drag, no steering).
    /// Shared by the aim assist, the debug overlay, the host's launch fast-forward, the bots and the tests,
    /// so they all agree on where a throw goes.
    /// </summary>
    public static class ThrowBallistics
    {
        /// <summary>Downward acceleration of a thrown bomb, m/s² (positive).</summary>
        public static float Gravity(GameTuning t) => -Physics.gravity.y * (t != null ? t.bombGravityScale : 1f);

        public static Vector3 PositionAt(Vector3 origin, Vector3 velocity, float gravity, float time) =>
            origin + velocity * time + Vector3.down * (0.5f * gravity * time * time);

        public static Vector3 VelocityAt(Vector3 velocity, float gravity, float time) =>
            velocity + Vector3.down * (gravity * time);

        /// <summary>
        /// Low-arc launch direction that reaches <paramref name="to"/> (relative to the launch point) at
        /// <paramref name="speed"/>. False when the target is out of range at that speed: nothing here adds speed.
        /// </summary>
        public static bool TrySolveLowArc(Vector3 to, float speed, float gravity, out Vector3 direction, out float flightTime)
        {
            direction = default;
            flightTime = 0f;
            var flat = new Vector3(to.x, 0f, to.z);
            float d = flat.magnitude;
            if (d < 0.25f || speed <= 0.01f || gravity <= 0f) return false;

            float v2 = speed * speed;
            float disc = v2 * v2 - gravity * (gravity * d * d + 2f * to.y * v2);
            if (disc < 0f) return false;

            float tanTheta = (v2 - Mathf.Sqrt(disc)) / (gravity * d);
            direction = (flat / d + Vector3.up * tanTheta).normalized;
            float horizontalSpeed = speed * new Vector2(direction.x, direction.z).magnitude;
            flightTime = horizontalSpeed > 0.01f ? d / horizontalSpeed : 0f;
            return true;
        }

        /// <summary>Closest distance between the segment a→b and point p.</summary>
        public static float SegmentPointDistance(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float k = len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            return Vector3.Distance(a + ab * k, p);
        }

        /// <summary>
        /// Closest approach of the arc to <paramref name="point"/> within <paramref name="maxTime"/> seconds,
        /// sampled every <paramref name="step"/> seconds (segment-exact between samples).
        /// </summary>
        public static float ClosestApproach(Vector3 origin, Vector3 velocity, float gravity, Vector3 point,
            float maxTime, out float atTime, float step = 0.01f)
        {
            float best = float.MaxValue;
            atTime = 0f;
            Vector3 prev = origin;
            for (float t = step; t <= maxTime + 1e-4f; t += step)
            {
                Vector3 next = PositionAt(origin, velocity, gravity, t);
                float d = SegmentPointDistance(prev, next, point);
                if (d < best)
                {
                    best = d;
                    atTime = t;
                }
                prev = next;
            }
            return best;
        }

        /// <summary>
        /// Samples the arc into <paramref name="points"/> until it hits something on <paramref name="mask"/>
        /// (sphere of <paramref name="radius"/>) or <paramref name="maxTime"/> runs out. Returns true on a hit.
        /// </summary>
        public static bool Trace(Vector3 origin, Vector3 velocity, float gravity, float maxTime, float step,
            int mask, float radius, List<Vector3> points, out RaycastHit hit)
        {
            hit = default;
            points?.Clear();
            points?.Add(origin);
            Vector3 prev = origin;
            for (float t = step; t <= maxTime + 1e-4f; t += step)
            {
                Vector3 next = PositionAt(origin, velocity, gravity, t);
                Vector3 delta = next - prev;
                float length = delta.magnitude;
                if (length > 1e-5f && Physics.SphereCast(prev, radius, delta / length, out hit, length, mask, QueryTriggerInteraction.Ignore))
                {
                    points?.Add(prev + delta / length * hit.distance);
                    return true;
                }
                points?.Add(next);
                prev = next;
            }
            return false;
        }
    }
}
