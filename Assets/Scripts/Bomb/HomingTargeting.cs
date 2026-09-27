using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Soft homing (PROJECT_SPEC §8.3): which player, if any, a throw is bent toward, and how strongly.
    /// One pure function used by both the thrower's on-screen lock preview and the host when the bomb is released,
    /// so what you see is what the host will do. The lock is deliberately imperfect: a narrow cone, a strength that
    /// fades toward the cone's edge, and (in BombController) a small random error on the aim point.
    /// </summary>
    public static class HomingTargeting
    {
        public static bool TryPick(GameTuning t, Vector3 origin, Vector3 velocity, Player thrower,
            IReadOnlyList<Player> players, out Player target, out float angle, out float quality)
        {
            target = null;
            angle = 0f;
            quality = 0f;
            if (t == null || t.homingStrength <= 0f || velocity.sqrMagnitude < 0.01f) return false;

            Vector3 dir = velocity.normalized;
            float best = t.homingConeDegrees;

            foreach (var p in players)
            {
                if (p == null || p == thrower || p.CatchVolume == null) continue;

                Vector3 to = p.CatchVolume.CatchCenter - origin;
                float distance = to.magnitude;
                if (distance < 0.5f || distance > t.homingRange) continue;

                float a = Vector3.Angle(dir, to);
                if (a < best)
                {
                    best = a;
                    target = p;
                }
            }

            if (target == null) return false;

            angle = best;
            // Full strength dead centre, fading toward the edge of the cone.
            quality = t.homingStrength * Mathf.Pow(1f - angle / t.homingConeDegrees, 0.7f);
            return true;
        }
    }
}
