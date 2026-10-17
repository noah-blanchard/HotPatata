using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The single place that decides whether a flying bomb is caught. First valid claim wins: the bomb leaves the
    /// Thrown state immediately, so a second receiver in the same physics step is rejected. Player scripts never
    /// decide catches.
    ///
    /// Forgiveness lives here, on the receiver's side, never in the flight (PROJECT_SPEC §9):
    ///  - the test is SWEPT: every chord of the flight's sweep (<see cref="BombPhysics.AdvanceTo"/>) is checked against
    ///    each receiver's reach, so a fast bomb never slips between two samples;
    ///  - the reach is <see cref="GameTuning.catchRadius"/>, plus <see cref="GameTuning.catchFacingBonus"/> when the
    ///    bomb comes from in front of where the receiver looks (<see cref="ReachFor"/>), and a little less
    ///    vertically than sideways (<see cref="GameTuning.catchVerticalScale"/>);
    ///  - a press up to <see cref="GameTuning.catchLateGrace"/> after the bomb was in reach still catches, as long as
    ///    it has not exploded (a lethal contact right after it passed a receiver waits out that grace).
    ///
    /// Online (#117 P3, ARCHITECTURE §13.2): the host's sweep only serves the players this machine owns. A remote receiver's
    /// own machine draws the very same flight (a pure function of server time, <see cref="FlightSegment"/>), sees it reach
    /// its catch sphere and claims the catch with the time it saw it (<see cref="TryResolveClaim"/>). The host checks the
    /// claim exactly: the time within <see cref="GameTuning.catchLagCompensation"/>, the window open then, the bomb at that
    /// time within reach of the claimed centre, and that centre where the receiver's own stamps put them. A lethal contact
    /// right after the bomb passed near a remote receiver waits for their claim (<see cref="ExplosionHoldFor"/>), as long as
    /// the measured lag says it can still come (<see cref="LagBudget"/>).
    /// </summary>
    [RequireComponent(typeof(BombController))]
    public class CatchResolver : MonoBehaviour
    {
        const float Never = -999f;
        const float HoldScanStep = 0.02f;     // seconds between samples of the recent flight when looking for a remote receiver
        const float HoldScanExtra = 0.5f;     // metres beyond the reach: a near pass that the receiver may have caught on their screen
        const float StampExtrapolation = 0.1f;   // seconds a receiver's newest stamp may be carried forward
        const float MaxRunSpeed = 12f;           // m/s: how far a receiver can get from their newest stamp, for the hold only

        readonly float[] lastInReach = new float[Player.MaxSlots];   // host: when the swept path last came within reach
        BombController bomb;

        void Awake() => bomb = GetComponent<BombController>();

        void OnEnable() => bomb.BombThrown += OnThrown;
        void OnDisable() => bomb.BombThrown -= OnThrown;

        void OnThrown(Player thrower)
        {
            for (int i = 0; i < lastInReach.Length; i++) lastInReach[i] = Never;
        }

        bool Online => NetMode.IsNetworked && NetMode.IsAuthority;
        bool Compensating => Online && bomb.Tuning.catchLagCompensation > 0f;

        /// <summary>
        /// How close (metres, in reach space, see <see cref="ReachDistance"/>) a bomb travelling along
        /// <paramref name="travel"/> must come to <paramref name="receiver"/>'s catch centre to be catchable: the catch
        /// radius, plus the facing bonus when it comes from in front of their view (they can see it arrive).
        /// Pure geometry, shared with the remote client's claim and the diagnostics.
        /// </summary>
        public static float ReachFor(Player receiver, Vector3 travel)
        {
            var t = receiver.Tuning;
            bool facing = travel.sqrMagnitude < 1e-6f ||
                          Vector3.Angle(receiver.CameraTarget.forward, -travel) <= t.catchFacingAngle;
            return t.catchRadius + (facing ? t.catchFacingBonus : 0f);
        }

        /// <summary>
        /// Closest approach of the segment a→b to a receiver's catch centre, measured in "reach space" (vertical distances
        /// stretched by 1 / <see cref="GameTuning.catchVerticalScale"/>), so it compares directly with <see cref="ReachFor"/>.
        /// </summary>
        public static float ReachDistance(GameTuning t, Vector3 a, Vector3 b, Vector3 center, out Vector3 closest) =>
            ReachDistance(t, a, b, center, out closest, out _);

        /// <summary>As above; <paramref name="along"/> is where the closest point is on a→b (0..1).</summary>
        public static float ReachDistance(GameTuning t, Vector3 a, Vector3 b, Vector3 center, out Vector3 closest, out float along)
        {
            float stretch = 1f / Mathf.Max(0.05f, t.catchVerticalScale);
            Vector3 s = new Vector3(1f, stretch, 1f);
            Vector3 sa = Vector3.Scale(a - center, s), sb = Vector3.Scale(b - center, s);
            Vector3 ab = sb - sa;
            along = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(-Vector3.Dot(sa, ab) / ab.sqrMagnitude) : 0f;
            closest = Vector3.Lerp(a, b, along);
            return (sa + ab * along).magnitude;
        }

        /// <summary>The host decides this player's catches with its own sweep: offline everyone, online its own players only.</summary>
        static bool SweptHere(Player p) => !NetMode.IsNetworked || p.IsLocal;

        void FixedUpdate()
        {
            if (!NetMode.IsAuthority || bomb.State != BombState.Thrown) return;
            TryLateCatch();
        }

        /// <summary>
        /// One chord a→b of the flight (<see cref="BombPhysics.AdvanceTo"/>), checked against every receiver this machine sweeps.
        /// A receiver whose window is open catches it (the closest wins). True when it was caught.
        /// </summary>
        public bool SweepChord(Vector3 from, Vector3 to)
        {
            if (bomb.State != BombState.Thrown) return false;

            Player best = null;
            float bestDistance = float.MaxValue;
            foreach (var p in Player.All)
            {
                if (!IsEligible(p) || !SweptHere(p)) continue;
                float d = ReachDistance(p.Tuning, from, to, p.CatchVolume.CatchCenter, out _);
                if (d > ReachFor(p, to - from)) continue;

                if (p.PlayerId >= 0 && p.PlayerId < lastInReach.Length) lastInReach[p.PlayerId] = Time.time;
                p.Catcher.NoteBombInReach();
                if (p.Catcher.WindowOpen && d < bestDistance)
                {
                    best = p;
                    bestDistance = d;
                }
            }

            if (best == null || bomb.ExplosionPending) return false;
            PatataLog.Bomb($"Catch accepted {best} ({bestDistance:F2} m from the centre)");
            return bomb.AcceptCatch(best);
        }

        // A window opened just after the bomb went by: still a catch within the late grace (even over a held explosion).
        void TryLateCatch()
        {
            float since = Time.time - bomb.Tuning.catchLateGrace;
            Player best = null;
            float bestTime = since;
            foreach (var p in Player.All)
            {
                if (!IsEligible(p) || !SweptHere(p) || !p.Catcher.WindowOpen || p.PlayerId < 0 || p.PlayerId >= lastInReach.Length) continue;
                if (lastInReach[p.PlayerId] >= bestTime)
                {
                    best = p;
                    bestTime = lastInReach[p.PlayerId];
                }
            }
            if (best == null) return;

            PatataLog.Bomb($"Catch accepted {best} (late press, in reach {(Time.time - bestTime) * 1000f:F0} ms ago)");
            bomb.AcceptCatch(best);
        }

        /// <summary>
        /// Host only, the single decision for a remote receiver (#117 P3): "on my screen, flight <paramref name="flightId"/>
        /// was within reach of my catch centre <paramref name="center"/> (feet <paramref name="feet"/>) at server time
        /// <paramref name="time"/>, and my window was open". Every part of it is checked; refusing returns false.
        /// </summary>
        public bool TryResolveClaim(Player receiver, int flightId, double time, Vector3 center, Vector3 feet)
        {
            var t = bomb.Tuning;
            if (!Compensating || bomb.State != BombState.Thrown || !IsEligible(receiver)) return false;
            var flight = bomb.Body.Flight;
            if (flight.Id != flightId) return Refuse(receiver, $"another flight ({flightId}, now {flight.Id})");

            double now = SimulationClock.ServerNow;
            double at = HazardRewind.ClampClaim(time, now, t.catchLagCompensation);   // never in the future, never older than the cap
            if (at != time && time < now) SyncStats.ClaimsClamped++;
            if (at < flight.T0) return Refuse(receiver, "before the throw");
            if (bomb.ExplosionPending ? at > bomb.Body.EndedAt : !bomb.Body.Flying) return Refuse(receiver, "after the flight ended");
            // A back-dated flight's first moments were seen late by everyone but the thrower: the window is judged from
            // when the receiver could first see that point, with the same late grace (plus their jitter).
            double seen = flight.SeenNoEarlierThan(at);
            if (!receiver.Catcher.RemoteWindowCovers(seen, NetQuality.JitterSeconds(receiver.PlayerId))) return Refuse(receiver, "window closed then");

            float g = bomb.Body.Gravity;
            Vector3 x = flight.PositionAt(at, g);
            float d = ReachDistance(t, x, x, center, out _);
            float reach = ReachFor(receiver, flight.VelocityAt(at, g)) + t.catchClaimSlack;
            if (d > reach) return Refuse(receiver, $"{d:F2} m from the centre (reach {reach:F2})");

            // The centre must be where the receiver's own stamps put them then (their copy here is drawn late).
            var history = receiver.Net != null ? receiver.Net.History : null;
            Vector3 expected = history != null && history.TryFeetAt(at, StampExtrapolation, out Vector3 stamped) ? stamped : receiver.transform.position;
            float off = Vector3.Distance(feet, expected);
            if (off > t.catchClaimCentreTolerance) return Refuse(receiver, $"claimed feet {off:F2} m from their stamps");
            if ((center - feet).magnitude > t.catchCenterHeight + t.catchRadius + t.catchFacingBonus) return Refuse(receiver, "catch centre too far from the feet");

            SyncStats.ClaimsAccepted++;
            PatataLog.Bomb($"Catch accepted {receiver} (claimed, {(now - at) * 1000.0:F0} ms ago, {d:F2} m, rtt {RttMs(receiver)} ms)");
            return bomb.AcceptCatch(receiver);
        }

        bool Refuse(Player receiver, string why)
        {
            SyncStats.ClaimsRefused++;
            PatataLog.Bomb($"Catch claim refused {receiver}: {why}");
            if (receiver.Net != null && receiver.Net.IsSpawned) receiver.Net.SendClaimRefused();
            return false;
        }

        /// <summary>
        /// Seconds the host should wait before a lethal contact becomes an explosion, because a receiver the bomb just
        /// passed may still press catch (late grace) or, online, a remote receiver's claim may still be on its way.
        /// 0 = explode now.
        /// </summary>
        public float ExplosionHoldFor()
        {
            float until = 0f;

            float grace = bomb.Tuning.catchLateGrace;
            foreach (var p in Player.All)
            {
                if (!IsEligible(p) || !SweptHere(p) || p.PlayerId < 0 || p.PlayerId >= lastInReach.Length) continue;
                if (lastInReach[p.PlayerId] >= Time.time - grace) until = Mathf.Max(until, lastInReach[p.PlayerId] + grace);
            }
            float hold = Mathf.Max(0f, until - Time.time);

            if (Compensating)
                foreach (var p in Player.All)
                {
                    if (!IsEligible(p) || SweptHere(p)) continue;
                    hold = Mathf.Max(hold, RemoteHold(p));
                }
            return hold;
        }

        /// <summary>
        /// Did the recent flight pass near where <paramref name="receiver"/>'s stamps put them? Then their claim may be on its
        /// way: wait from that moment for as long as the measured lag allows (<see cref="LagBudget.LateMessageWait"/>).
        /// </summary>
        float RemoteHold(Player receiver)
        {
            var t = bomb.Tuning;
            var flight = bomb.Body.Flight;
            var history = receiver.Net != null ? receiver.Net.History : null;
            if (!flight.Valid || history == null) return 0f;

            float wait = LagBudget.LateMessageWait(receiver, t, t.catchLagCompensation);
            double now = SimulationClock.ServerNow;
            double end = bomb.Body.EndedAt;
            double start = System.Math.Max(flight.T0, now - wait);
            Vector3 centerOffset = receiver.CatchVolume.CatchCenter - receiver.transform.position;
            float g = bomb.Body.Gravity;
            float near = t.catchRadius + t.catchFacingBonus + t.catchClaimSlack + HoldScanExtra;
            // Their stamps trail the host by about a round trip: past the newest one, they are where it put them, give or
            // take how far they can run meanwhile (this only decides how long to wait, never a catch).
            var newest = history.Newest;
            float speed = Mathf.Min(newest.Velocity.magnitude, MaxRunSpeed);
            double reached = -1.0;
            for (double at = start; at <= end; at += HoldScanStep)
            {
                float slack = 0f;
                if (!history.TryFeetAt(at, StampExtrapolation, out Vector3 feet))
                {
                    if (history.Count == 0 || at < newest.Time) continue;
                    feet = newest.Position;
                    slack = speed * (float)(at - newest.Time);
                }
                Vector3 x = flight.PositionAt(at, g);
                if (ReachDistance(t, x, x, feet + centerOffset, out _) <= near + slack) reached = at;
            }
            if (reached < 0.0) return 0f;
            // They may press up to the late grace after they could see it pass them, and their claim leaves with that press.
            return Mathf.Max(0f, (float)(flight.SeenNoEarlierThan(reached) + t.catchLateGrace + wait - now));
        }

        bool IsEligible(Player receiver) =>
            receiver != null && receiver != bomb.LastThrower && !receiver.ControlLocked && receiver.CatchVolume != null;

        static int RttMs(Player p) => p.Net != null && p.Net.IsSpawned ? NetMode.RttMsFor(p.Net.OwnerClientId) : 0;
    }
}
