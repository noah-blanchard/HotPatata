using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    public enum FuseZoneKind
    {
        Cold,
        Hot,
        Forbidden
    }

    /// <summary>
    /// Changes how fast the CARRIER's fuse burns while the carrier stands in the zone (PROJECT_SPEC §7.3):
    /// forbidden = explodes, hot = x2, cold = x0.5 (rates in <see cref="GameTuning"/>). Other players are never
    /// affected. When zones overlap the most severe wins, so nothing can cancel a forbidden zone.
    /// The rule itself is applied by <see cref="BombController"/> (host), which asks <see cref="RateFor"/>.
    /// </summary>
    [RequireComponent(typeof(Zone))]
    public class FuseZone : MonoBehaviour
    {
        static readonly List<FuseZone> all = new List<FuseZone>();
        static readonly HashSet<Player> inside = new HashSet<Player>();

        [SerializeField] FuseZoneKind kind = FuseZoneKind.Forbidden;

        Zone zone;

        public FuseZoneKind Kind => kind;

        void Awake() => zone = GetComponent<Zone>();
        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public bool Contains(Player player) => zone.CollectPlayers(inside) > 0 && inside.Contains(player);

        /// <summary>The fuse rate multiplier for one kind of zone (forbidden = +infinity).</summary>
        public static float RateOf(FuseZoneKind kind, GameTuning tuning) => kind switch
        {
            FuseZoneKind.Forbidden => float.PositiveInfinity,
            FuseZoneKind.Hot => tuning.hotZoneFuseRate,
            _ => tuning.coldZoneFuseRate
        };

        /// <summary>
        /// The rate for a carrier standing in the given zones: the most severe one (forbidden > hot > cold), or 1 in
        /// none. Pure (EditMode tested).
        /// </summary>
        public static float Combine(IEnumerable<FuseZoneKind> kinds, GameTuning tuning)
        {
            int worst = -1;
            foreach (var k in kinds) worst = Mathf.Max(worst, (int)k);
            return worst < 0 ? 1f : RateOf((FuseZoneKind)worst, tuning);
        }

        static readonly List<FuseZoneKind> scratch = new List<FuseZoneKind>();

        /// <summary>The fuse rate for <paramref name="carrier"/> right now, from every zone they stand in.</summary>
        public static float RateFor(Player carrier, GameTuning tuning)
        {
            scratch.Clear();
            if (carrier != null)
                foreach (var z in all)
                    if (z.Contains(carrier)) scratch.Add(z.kind);
            return Combine(scratch, tuning);
        }
    }
}
