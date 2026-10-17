using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata
{
    /// <summary>
    /// Geometry queries for a flight segment (#117 P2). The host never lets PhysX decide a flying bomb: it sweeps the analytic
    /// arc, a short chord at a time, against the world and the kill zones. A chord in the past (a back-dated throw being
    /// fast-forwarded) is tested against every moving level piece posed at THAT time: each mover is a pure function of time
    /// (<see cref="ITimePosed"/>), so the chord is carried into the mover's present frame
    /// (p' = M_mover(now) · M_mover(t)⁻¹ · p) and cast against its colliders where they are now.
    /// </summary>
    public static class BombFlightSweep
    {
        /// <summary>A chord ending less than this before now is "the present": movers are where they are drawn.</summary>
        const double PresentSeconds = 1e-3;

        public struct WorldHit
        {
            /// <summary>0..1 along the chord.</summary>
            public float Fraction;
            /// <summary>The bomb's centre at contact.</summary>
            public Vector3 Point;
            public Collider Collider;
        }

        static readonly RaycastHit[] hits = new RaycastHit[64];
        static readonly Collider[] overlaps = new Collider[32];
        static readonly Dictionary<Collider, ITimePosed> moverOf = new Dictionary<Collider, ITimePosed>();
        static readonly List<ITimePosed> movers = new List<ITimePosed>();
        static bool moversFound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            Forget();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Forget();

        static void Forget()
        {
            moverOf.Clear();
            movers.Clear();
            moversFound = false;
        }

        /// <summary>What a flying bomb collides with: the Bomb layer's collision matrix (players are not world).</summary>
        public static int WorldMask
        {
            get
            {
                int layer = LayerMask.NameToLayer("Bomb");
                if (layer < 0) return LayerMask.GetMask("Default", "Environment", "Hazard");
                int mask = 0;
                for (int i = 0; i < 32; i++)
                    if (!Physics.GetIgnoreLayerCollision(layer, i)) mask |= 1 << i;
                return mask;
            }
        }

        /// <summary>
        /// First world contact of a bomb of <paramref name="radius"/> flying the chord a→b, which it flies from server time
        /// <paramref name="ta"/> to <paramref name="tb"/>. <paramref name="now"/> is the server time movers are drawn at.
        /// </summary>
        public static bool CastWorld(Vector3 a, Vector3 b, double ta, double tb, double now, float radius, int mask, out WorldHit hit)
        {
            hit = default;
            bool past = now - tb > PresentSeconds;
            float best = float.PositiveInfinity;
            Collider bestCollider = null;

            Vector3 chord = b - a;
            float length = chord.magnitude;
            int n = Cast(a, chord, length, radius, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (past && MoverOf(hits[i].collider) != null) continue;   // drawn where it is now, not where it was then
                float f = length > 1e-5f ? hits[i].distance / length : 0f;
                if (f < best)
                {
                    best = f;
                    bestCollider = hits[i].collider;
                }
            }

            if (past)
            {
                FindMovers();
                foreach (var mover in movers)
                {
                    if (mover is Object alive && alive == null) continue;   // destroyed with its level piece
                    var part = mover.PosedTransform;
                    if (part == null || !mover.TryPoseAt(ta, out Vector3 pa, out Quaternion ra) || !mover.TryPoseAt(tb, out Vector3 pb, out Quaternion rb)) continue;
                    Vector3 a2 = ToNow(a, pa, ra, part.position, part.rotation);
                    Vector3 b2 = ToNow(b, pb, rb, part.position, part.rotation);
                    Vector3 chord2 = b2 - a2;
                    float length2 = chord2.magnitude;
                    int m = Cast(a2, chord2, length2, radius, mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < m; i++)
                    {
                        if (MoverOf(hits[i].collider) != mover) continue;
                        float f = length2 > 1e-5f ? hits[i].distance / length2 : 0f;
                        if (f < best)
                        {
                            best = f;
                            bestCollider = hits[i].collider;
                        }
                    }
                }
            }

            if (bestCollider == null) return false;
            hit = new WorldHit { Fraction = best, Point = Vector3.Lerp(a, b, best), Collider = bestCollider };
            return true;
        }

        /// <summary>The first enabled kill zone the chord a→b touches, and where along it (0..1).</summary>
        public static KillZone FirstKillZone(Vector3 a, Vector3 b, float radius, out float fraction)
        {
            fraction = 0f;
            KillZone found = null;
            Vector3 chord = b - a;
            float length = chord.magnitude;
            if (length < 1e-5f)
            {
                int k = Physics.OverlapSphereNonAlloc(a, radius, overlaps, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < k; i++)
                    if (IsKillZone(overlaps[i], out var zone)) return zone;
                return null;
            }

            float best = float.PositiveInfinity;
            int n = Cast(a, chord, length, radius, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                if (!IsKillZone(hits[i].collider, out var zone)) continue;
                float f = hits[i].distance / length;
                if (f < best)
                {
                    best = f;
                    found = zone;
                }
            }
            if (found != null) fraction = best;
            return found;
        }

        static bool IsKillZone(Collider c, out KillZone zone)
        {
            zone = null;
            return c != null && c.isTrigger && c.TryGetComponent(out zone) && zone.isActiveAndEnabled;
        }

        /// <summary>A point of the chord at mover time t, carried into the mover's present frame. Pure (EditMode tested).</summary>
        public static Vector3 ToNow(Vector3 point, Vector3 thenPosition, Quaternion thenRotation, Vector3 nowPosition, Quaternion nowRotation) =>
            nowPosition + nowRotation * (Quaternion.Inverse(thenRotation) * (point - thenPosition));

        static int Cast(Vector3 from, Vector3 chord, float length, float radius, int mask, QueryTriggerInteraction triggers)
        {
            if (length < 1e-5f) return 0;
            return Physics.SphereCastNonAlloc(from, radius, chord / length, hits, length, mask, triggers);
        }

        static ITimePosed MoverOf(Collider c)
        {
            if (c == null) return null;
            if (!moverOf.TryGetValue(c, out var mover))
            {
                mover = c.GetComponentInParent<ITimePosed>();
                moverOf[c] = mover;
            }
            return mover;
        }

        static void FindMovers()
        {
            if (moversFound) return;
            moversFound = true;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is ITimePosed posed) movers.Add(posed);
        }
    }
}
