using UnityEngine;

namespace Beep
{
    /// <summary>
    /// The single place that decides whether a flying bomb is caught. First valid claim wins: the bomb leaves the
    /// Thrown state immediately, so a second receiver in the same physics step is rejected. Player scripts never
    /// decide catches.
    ///
    /// Forgiveness lives here, on the receiver's side, never in the flight (PROJECT_SPEC §9):
    ///  - the test is SWEPT: every physics step the bomb's path since the last step is checked against each
    ///    receiver's reach, so a fast bomb never slips between two samples;
    ///  - the reach is <see cref="GameTuning.catchRadius"/>, plus <see cref="GameTuning.catchFacingBonus"/> when the
    ///    bomb comes from in front of where the receiver looks (<see cref="ReachFor"/>), and a little less
    ///    vertically than sideways (<see cref="GameTuning.catchVerticalScale"/>);
    ///  - a press up to <see cref="GameTuning.catchLateGrace"/> after the bomb was in reach still catches, as long as
    ///    it has not exploded (a lethal contact right after it passed a receiver waits out that grace).
    ///
    /// Lag compensation (online): a remote player sees the bomb late, so by the time their catch reaches the
    /// host the bomb has already flown past them. Their machine therefore claims the catch it saw
    /// (<see cref="TryResolveCompensatedCatch"/>), and the host checks it against a short history of the flight,
    /// bounded by <see cref="GameTuning.catchLagCompensation"/>. For the same reason a lethal contact right after
    /// the bomb passed near a remote receiver is held for a moment (<see cref="ExplosionHoldFor"/>).
    /// </summary>
    [RequireComponent(typeof(BombController))]
    public class CatchResolver : MonoBehaviour
    {
        const float ReachSlack = 0.4f;               // metres: the client's interpolated path is not the host's exact path
        const float InterpolationAllowance = 0.1f;   // seconds a client renders the bomb behind the server
        const float HoldMargin = 0.05f;
        const float Never = -999f;

        readonly FlightHistory history = new FlightHistory();
        readonly float[] lastInReach = new float[Player.MaxSlots];   // host: when the swept path last came within reach
        BombController bomb;
        Vector3 lastSweep;
        bool sweeping;

        void Awake() => bomb = GetComponent<BombController>();

        void OnEnable() => bomb.BombThrown += OnThrown;
        void OnDisable() => bomb.BombThrown -= OnThrown;

        void OnThrown(Player thrower)
        {
            history.Clear();
            for (int i = 0; i < lastInReach.Length; i++) lastInReach[i] = Never;
            lastSweep = bomb.transform.position;
            sweeping = true;
        }

        bool Compensating => NetMode.IsNetworked && NetMode.IsAuthority && bomb.Tuning.catchLagCompensation > 0f;

        /// <summary>Lag-compensation reach: the most a receiver could reach, plus slack for the client's interpolation.</summary>
        float CompensatedReach => bomb.Tuning.catchRadius + bomb.Tuning.catchFacingBonus + ReachSlack;

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
        public static float ReachDistance(GameTuning t, Vector3 a, Vector3 b, Vector3 center, out Vector3 closest)
        {
            float stretch = 1f / Mathf.Max(0.05f, t.catchVerticalScale);
            Vector3 s = new Vector3(1f, stretch, 1f);
            Vector3 sa = Vector3.Scale(a - center, s), sb = Vector3.Scale(b - center, s);
            Vector3 ab = sb - sa;
            float k = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(-Vector3.Dot(sa, ab) / ab.sqrMagnitude) : 0f;
            closest = Vector3.Lerp(a, b, k);
            return (sa + ab * k).magnitude;
        }

        void FixedUpdate()
        {
            if (!NetMode.IsAuthority || bomb.State != BombState.Thrown) return;

            if (!bomb.Body.Frozen)
            {
                Sweep();
                if (bomb.State != BombState.Thrown) return;   // caught during the sweep

                if (Compensating)
                {
                    history.Begin(Time.time, bomb.transform.position);
                    foreach (var p in Player.All)
                        if (p != null && p.CatchVolume != null) history.AddCenter(p.PlayerId, p.CatchVolume.CatchCenter);
                }
            }

            TryLateCatch();
        }

        /// <summary>
        /// Checks the bomb's path since the last sweep against every eligible receiver. A receiver whose window is open
        /// catches it (the closest wins). Also called by the controller right before judging a lethal contact, so the
        /// last stretch of flight is never skipped.
        /// </summary>
        public void Sweep()
        {
            if (!sweeping || bomb.State != BombState.Thrown) return;

            Vector3 from = lastSweep, to = bomb.transform.position;
            lastSweep = to;

            Player best = null;
            float bestDistance = float.MaxValue;
            foreach (var p in Player.All)
            {
                if (!IsEligible(p)) continue;
                float d = ReachDistance(p.Tuning, from, to, p.CatchVolume.CatchCenter, out Vector3 closest);
                if (d > ReachFor(p, to - from)) continue;

                if (p.PlayerId >= 0 && p.PlayerId < lastInReach.Length) lastInReach[p.PlayerId] = Time.time;
                p.Catcher.NoteBombInReach();
                if (p.Catcher.WindowOpen && d < bestDistance)
                {
                    best = p;
                    bestDistance = d;
                }
            }

            if (best != null && !bomb.ExplosionPending)
            {
                BeepLog.Bomb($"Catch accepted {best} ({bestDistance:F2} m from the centre)");
                bomb.AcceptCatch(best);
            }
        }

        // A window opened just after the bomb went by: still a catch within the late grace (even over a held explosion).
        void TryLateCatch()
        {
            float since = Time.time - bomb.Tuning.catchLateGrace;
            Player best = null;
            float bestTime = since;
            foreach (var p in Player.All)
            {
                if (!IsEligible(p) || !p.Catcher.WindowOpen || p.PlayerId < 0 || p.PlayerId >= lastInReach.Length) continue;
                if (lastInReach[p.PlayerId] >= bestTime)
                {
                    best = p;
                    bestTime = lastInReach[p.PlayerId];
                }
            }
            if (best == null) return;

            BeepLog.Bomb($"Catch accepted {best} (late press, in reach {(Time.time - bestTime) * 1000f:F0} ms ago)");
            bomb.AcceptCatch(best);
        }

        /// <summary>
        /// Host only: a remote receiver reports that, on their screen, the bomb reached their catch sphere while their
        /// window was open. Accepted if the host's own record puts the bomb within reach of them recently enough.
        /// </summary>
        public bool TryResolveCompensatedCatch(Player receiver)
        {
            if (!Compensating || bomb.State != BombState.Thrown || !IsEligible(receiver)) return false;

            float window = bomb.Tuning.catchLagCompensation;
            float reached = history.LastReach(receiver.PlayerId, Time.time - window, CompensatedReach);
            if (reached < 0f)
            {
                BeepLog.Bomb($"Catch claim rejected {receiver} (not in reach in the last {window * 1000f:F0} ms)");
                return false;
            }

            BeepLog.Bomb($"Catch accepted {receiver} (lag-compensated, in reach {(Time.time - reached) * 1000f:F0} ms ago, rtt {RttMs(receiver)} ms)");
            return bomb.AcceptCatch(receiver);
        }

        /// <summary>
        /// Seconds the host should wait before a lethal contact becomes an explosion, because a receiver the bomb just
        /// passed may still press catch (late grace) or, online, a remote receiver's catch may still be on its way.
        /// 0 = explode now.
        /// </summary>
        public float ExplosionHoldFor()
        {
            Sweep();   // register the last stretch of flight before deciding
            float until = 0f;

            float grace = bomb.Tuning.catchLateGrace;
            foreach (var p in Player.All)
            {
                if (!IsEligible(p) || p.PlayerId < 0 || p.PlayerId >= lastInReach.Length) continue;
                if (lastInReach[p.PlayerId] >= Time.time - grace) until = Mathf.Max(until, lastInReach[p.PlayerId] + grace);
            }

            if (Compensating)
            {
                float cap = bomb.Tuning.catchLagCompensation;
                foreach (var p in Player.All)
                {
                    if (!IsEligible(p) || p.IsLocal) continue;   // the host's own player has no lag to wait for
                    float reached = history.LastReach(p.PlayerId, Time.time - cap, CompensatedReach);
                    if (reached < 0f) continue;

                    float delay = Mathf.Min(cap, RttMs(p) / 1000f + InterpolationAllowance + HoldMargin);
                    until = Mathf.Max(until, reached + delay);
                }
            }
            return Mathf.Max(0f, until - Time.time);
        }

        bool IsEligible(Player receiver) =>
            receiver != null && receiver != bomb.LastThrower && !receiver.ControlLocked && receiver.CatchVolume != null;

        static int RttMs(Player p) => p.Net != null && p.Net.IsSpawned ? NetMode.RttMsFor(p.Net.OwnerClientId) : 0;
    }
}
