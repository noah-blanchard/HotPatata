using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Host only: every physics step of a Thrown bomb, sweeps the stretch it is ABOUT to fly (position → position +
    /// this step's ballistic motion, cut short where it would hit the world) against every <see cref="Zone"/>, and hands
    /// the zones it touches, nearest first, to their <see cref="IBombZoneEffect"/>s. Looking one step ahead lets a transit
    /// mouth capture the bomb before it reaches the tube's rim, and a barrier explode it on the step it crosses.
    /// Triggers are not used: continuous collision does not sweep them, and a fast bomb crosses a thin volume between steps.
    /// </summary>
    [RequireComponent(typeof(BombController))]
    public class BombZoneSweep : MonoBehaviour
    {
        struct Hit
        {
            public Zone Zone;
            public float Enter;
        }

        readonly List<Hit> hits = new List<Hit>();
        BombController bomb;
        SphereCollider sphere;
        int worldMask;

        void Awake()
        {
            bomb = GetComponent<BombController>();
            sphere = GetComponent<SphereCollider>();
            worldMask = LayerMask.GetMask("Environment", "Hazard");
        }

        float Radius => sphere != null ? sphere.radius * Mathf.Abs(transform.lossyScale.x) : 0.15f;

        void FixedUpdate()
        {
            if (!NetMode.IsAuthority || bomb.State != BombState.Thrown || bomb.Body.Frozen || bomb.ExplosionPending) return;

            float dt = Time.fixedDeltaTime;
            Vector3 from = transform.position;
            Vector3 to = ThrowBallistics.PositionAt(from, bomb.Body.Velocity, ThrowBallistics.Gravity(bomb.Tuning), dt);
            Vector3 step = to - from;
            float length = step.magnitude;
            if (length > 1e-4f && Physics.SphereCast(from, Radius, step / length, out var wall, length, worldMask, QueryTriggerInteraction.Ignore))
                to = from + step / length * wall.distance;

            Sweep(from, to);
        }

        /// <summary>Reports every zone the segment touches, nearest first, while the bomb is still flying.</summary>
        public void Sweep(Vector3 from, Vector3 to)
        {
            hits.Clear();
            foreach (var zone in Zone.All)
                if (zone.BombEffects.Count > 0 && zone.SegmentTouches(from, to, Radius, out float enter))
                    hits.Add(new Hit { Zone = zone, Enter = enter });
            hits.Sort((x, y) => x.Enter.CompareTo(y.Enter));

            foreach (var hit in hits)
                foreach (var effect in hit.Zone.BombEffects)
                {
                    if (bomb.State != BombState.Thrown || bomb.ExplosionPending) return;
                    effect.OnBombPassed(bomb);
                }
        }
    }
}
