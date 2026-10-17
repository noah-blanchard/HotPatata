using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Host only: tests each chord of a Thrown bomb's flight (<see cref="BombPhysics.AdvanceTo"/>, cut short where it hits
    /// the world) against every <see cref="Zone"/>, and hands the zones it touches, nearest first, to their
    /// <see cref="IBombZoneEffect"/>s. Zones run before the world contact of the same chord, so a transit mouth captures the
    /// bomb before it reaches the tube's rim, and a barrier explodes it on the chord it crosses. Before an effect runs, the
    /// bomb is placed where the chord enters the zone. Triggers are not used: a fast bomb crosses a thin volume between steps.
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

        void Awake() => bomb = GetComponent<BombController>();

        /// <summary>Reports every zone the chord touches, nearest first, while the bomb is still flying.</summary>
        public void Sweep(Vector3 from, Vector3 to, float radius)
        {
            hits.Clear();
            foreach (var zone in Zone.All)
                if (zone.BombEffects.Count > 0 && zone.SegmentTouches(from, to, radius, out float enter))
                    hits.Add(new Hit { Zone = zone, Enter = enter });
            if (hits.Count == 0) return;
            hits.Sort((x, y) => x.Enter.CompareTo(y.Enter));

            foreach (var hit in hits)
                foreach (var effect in hit.Zone.BombEffects)
                {
                    if (bomb.State != BombState.Thrown || bomb.ExplosionPending) return;
                    bomb.Body.MarkChordContact(hit.Enter);
                    effect.OnBombPassed(bomb);
                }
        }
    }
}
