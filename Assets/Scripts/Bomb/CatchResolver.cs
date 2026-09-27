using UnityEngine;

namespace Beep
{
    /// <summary>
    /// The single place that decides whether contact with a catch volume is a valid catch.
    /// First valid claim wins: the bomb leaves the Thrown state immediately, so a second volume
    /// touched in the same physics step is rejected. Player scripts never decide catches.
    ///
    /// Lag compensation (online): a remote player sees the bomb late, so by the time their catch reaches the
    /// host the bomb has already flown past them. Their machine therefore claims the catch it saw
    /// (<see cref="TryResolveCompensatedCatch"/>), and the host checks it against a short history of the flight,
    /// bounded by <see cref="GameTuning.catchLagCompensation"/>. For the same reason a lethal contact right after
    /// the bomb passed near a remote receiver is held for a moment (<see cref="ExplosionHoldFor"/>), so the late
    /// catch can still win instead of the explosion.
    /// </summary>
    [RequireComponent(typeof(BombController))]
    public class CatchResolver : MonoBehaviour
    {
        const float ReachSlack = 0.4f;               // metres: the client's interpolated path is not the host's exact path
        const float InterpolationAllowance = 0.1f;   // seconds a client renders the bomb behind the server
        const float HoldMargin = 0.05f;

        readonly FlightHistory history = new FlightHistory();
        BombController bomb;

        void Awake() => bomb = GetComponent<BombController>();

        void OnEnable() => bomb.BombThrown += OnThrown;
        void OnDisable() => bomb.BombThrown -= OnThrown;

        void OnThrown(Player thrower) => history.Clear();

        bool Compensating => NetMode.IsNetworked && NetMode.IsAuthority && bomb.Tuning.catchLagCompensation > 0f;

        float Reach => bomb.Tuning.catchRadius + ReachSlack;

        void FixedUpdate()
        {
            if (!Compensating || bomb.State != BombState.Thrown || bomb.Body.Frozen) return;

            history.Begin(Time.time, bomb.transform.position);
            foreach (var p in Player.All)
                if (p != null && p.CatchVolume != null) history.AddCenter(p.PlayerId, p.CatchVolume.CatchCenter);
        }

        public bool TryResolveCatch(PlayerCatchVolume volume)
        {
            if (bomb.State != BombState.Thrown || bomb.ExplosionPending) return false;

            Player receiver = volume.Owner;
            if (receiver == null) return false;

            // A pass is hand -> flight -> ANOTHER player's catch.
            if (receiver == bomb.LastThrower) return false;

            // Catching is never automatic: the receiver must have pressed catch just before / as the bomb arrives.
            if (!receiver.Catcher.WindowOpen) return false;

            BeepLog.Bomb($"Catch accepted {receiver}");
            return bomb.AcceptCatch(receiver);
        }

        /// <summary>
        /// Host only: a remote receiver reports that, on their screen, the bomb reached their catch sphere while their
        /// window was open. Accepted if the host's own record puts the bomb within reach of them recently enough.
        /// </summary>
        public bool TryResolveCompensatedCatch(Player receiver)
        {
            if (!Compensating || bomb.State != BombState.Thrown || !IsEligible(receiver)) return false;

            float window = bomb.Tuning.catchLagCompensation;
            float reached = history.LastReach(receiver.PlayerId, Time.time - window, Reach);
            if (reached < 0f)
            {
                BeepLog.Bomb($"Catch claim rejected {receiver} (not in reach in the last {window * 1000f:F0} ms)");
                return false;
            }

            BeepLog.Bomb($"Catch accepted {receiver} (lag-compensated, in reach {(Time.time - reached) * 1000f:F0} ms ago, rtt {RttMs(receiver)} ms)");
            return bomb.AcceptCatch(receiver);
        }

        /// <summary>
        /// Seconds the host should wait before a lethal contact becomes an explosion, because a remote receiver the bomb
        /// just passed may still be sending their catch. 0 = explode now (offline, or nobody remote was in reach).
        /// </summary>
        public float ExplosionHoldFor()
        {
            if (!Compensating) return 0f;

            float cap = bomb.Tuning.catchLagCompensation;
            float until = 0f;
            foreach (var p in Player.All)
            {
                if (!IsEligible(p) || p.IsLocal) continue;   // the host's own player has no lag to wait for
                float reached = history.LastReach(p.PlayerId, Time.time - cap, Reach);
                if (reached < 0f) continue;

                float delay = Mathf.Min(cap, RttMs(p) / 1000f + InterpolationAllowance + HoldMargin);
                until = Mathf.Max(until, reached + delay);
            }
            return Mathf.Max(0f, until - Time.time);
        }

        bool IsEligible(Player receiver) =>
            receiver != null && receiver != bomb.LastThrower && !receiver.ControlLocked && receiver.CatchVolume != null;

        static int RttMs(Player p) => p.Net != null && p.Net.IsSpawned ? NetMode.RttMsFor(p.Net.OwnerClientId) : 0;
    }
}
