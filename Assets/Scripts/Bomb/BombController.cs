using System;
using UnityEngine;

namespace HotPatata
{
    public enum BombState
    {
        Held,
        Thrown,
        CaughtGrace,
        Exploding,
        Resetting
    }

    public enum BombFailReason
    {
        HoldFuseExpired,
        WorldContact,
        KillZone
    }

    /// <summary>
    /// Owns the bomb state machine, the current carrier and every transition
    /// (PROJECT_SPEC §5). All gameplay decisions about the bomb end up here, exactly once.
    ///
    ///   Resetting --EndReset--> Held --TryThrow--> Thrown --AcceptCatch--> CaughtGrace --grace--> Held
    ///   Held / Thrown --Explode--> Exploding --BeginReset--> Resetting
    /// Online, a lethal contact while Thrown may be held for a fraction of a second (still Thrown, frozen in place)
    /// so a remote receiver's lag-compensated catch can win; see <see cref="CatchResolver"/>.
    /// </summary>
    [RequireComponent(typeof(BombFuse), typeof(BombPhysics), typeof(CatchResolver))]
    public class BombController : MonoBehaviour
    {
        const float IntendedReceiverReach = 2.5f;     // metres: an arc passing farther than this from everyone is not a pass
        const float IntendedReceiverHorizon = 2.5f;   // seconds of flight considered

        [SerializeField] GameTuning tuning;

        [Header("Debug (read-only at runtime)")]
        [SerializeField] BombState debugState = BombState.Resetting;
        [SerializeField] string debugCarrier = "-";
        [SerializeField] float debugFuseRemaining;

        BombFuse fuse;
        BombPhysics bombPhysics;
        CatchResolver resolver;
        float graceEndTime;
        float pendingExplosionAt = -1f;   // a lethal contact held for a late (lag-compensated) catch
        BombFailReason pendingReason;
        string pendingDetail;

        public static BombController Instance { get; private set; }

        public BombState State { get; private set; } = BombState.Resetting;
        public Player Carrier { get; private set; }
        /// <summary>Who released the bomb most recently; cannot catch that same flight.</summary>
        public Player LastThrower { get; private set; }
        /// <summary>
        /// Who a flying bomb is heading for: the player its arc passes closest to, if close enough to be a pass.
        /// Feedback only (the receiver's "CATCH!" marker); nothing steers the bomb.
        /// </summary>
        public Player IntendedReceiver { get; private set; }
        /// <summary>Why the bomb last exploded (valid while Exploding).</summary>
        public BombFailReason LastFailReason { get; private set; }
        /// <summary>Host only: the bomb has hit something lethal and will explode unless a late catch arrives first.</summary>
        public bool ExplosionPending => pendingExplosionAt >= 0f;

        public GameTuning Tuning => tuning;
        public BombFuse Fuse => fuse;
        public BombPhysics Body => bombPhysics;
        public CatchResolver Resolver => resolver;

        public event Action<BombState, BombState> StateChanged;
        public event Action<Player> CarrierChanged;          // null while nobody holds it
        public event Action<Player> BombThrown;              // thrower
        public event Action<Player> BombCaught;              // receiver
        public event Action<BombFailReason, string> BombExploded;

        void Awake()
        {
            Instance = this;
            fuse = GetComponent<BombFuse>();
            bombPhysics = GetComponent<BombPhysics>();
            resolver = GetComponent<CatchResolver>();
        }

        void OnEnable() => fuse.Expired += OnFuseExpired;
        void OnDisable() => fuse.Expired -= OnFuseExpired;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            debugFuseRemaining = fuse.Remaining;
            if (!NetMode.IsAuthority) return;   // remote clients only mirror; the host runs the rules

            switch (State)
            {
                case BombState.Held:
                    fuse.Tick(Time.deltaTime);
                    break;
                case BombState.Thrown:
                    if (ExplosionPending && Time.time >= pendingExplosionAt) Explode(pendingReason, pendingDetail);
                    break;
                case BombState.CaughtGrace:
                    if (Time.time >= graceEndTime) Transition(BombState.Held);
                    break;
            }
        }

        // ------------------------------------------------------------------ throw / catch

        public bool TryThrow(Player thrower, Vector3 origin, Vector3 velocity)
        {
            if (State != BombState.Held || Carrier != thrower)
            {
                PatataLog.Bomb($"Throw rejected {thrower} (state={State}, carrier={Carrier})");
                return false;
            }

            LastThrower = thrower;
            IntendedReceiver = PickIntendedReceiver(thrower, origin, velocity);
            SetCarrier(null);
            Transition(BombState.Thrown);
            bombPhysics.EnterThrown(origin, velocity);
            PatataLog.Bomb($"Held -> Thrown carrier={thrower} speed={velocity.magnitude:F1}");
            BombThrown?.Invoke(thrower);
            return true;
        }

        /// <summary>Called by <see cref="CatchResolver"/> once it has validated a catch.</summary>
        public bool AcceptCatch(Player receiver)
        {
            if (State != BombState.Thrown) return false;

            if (ExplosionPending) PatataLog.Bomb($"Held lethal contact cancelled by a late catch ({pendingReason})");
            pendingExplosionAt = -1f;
            IntendedReceiver = null;
            SetCarrier(receiver);
            bombPhysics.EnterHeld(receiver.HandAnchor);
            fuse.Refresh();
            graceEndTime = Time.time + tuning.caughtGraceDuration;
            Transition(BombState.CaughtGrace);
            PatataLog.Bomb($"Thrown -> CaughtGrace carrier={receiver}");
            BombCaught?.Invoke(receiver);
            return true;
        }

        /// <summary>The player the (pure ballistic) arc passes closest to, within <see cref="IntendedReceiverReach"/>.</summary>
        Player PickIntendedReceiver(Player thrower, Vector3 origin, Vector3 velocity)
        {
            if (!NetMode.IsAuthority) return null;

            float g = ThrowBallistics.Gravity(tuning);
            Player best = null;
            float bestDistance = IntendedReceiverReach;
            foreach (var p in Player.All)
            {
                if (p == null || p == thrower || p.CatchVolume == null) continue;
                float d = ThrowBallistics.ClosestApproach(origin, velocity, g, p.CatchVolume.CatchCenter, IntendedReceiverHorizon, out _, 0.02f);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ failure

        /// <summary>Lethal contact reported by <see cref="BombPhysics"/>. Only counts while Thrown, on the authority.</summary>
        public void ReportWorldContact(Collider other)
        {
            if (!NetMode.IsAuthority || State != BombState.Thrown) return;
            LethalContact(BombFailReason.WorldContact,
                $"object={other.name} layer={LayerMask.LayerToName(other.gameObject.layer)}");
        }

        /// <summary>
        /// Trigger contact from <see cref="BombPhysics"/>: only kill zones matter. Catches are decided by
        /// <see cref="CatchResolver"/>'s swept reach test, not by trigger events; every other trigger (catch volumes
        /// included) is intentionally neutral and must never count as lethal world contact.
        /// </summary>
        public void ReportTriggerContact(Collider other)
        {
            if (!NetMode.IsAuthority || State != BombState.Thrown) return;
            if (other.TryGetComponent(out KillZone _)) LethalContact(BombFailReason.KillZone, $"object={other.name}");
        }

        /// <summary>
        /// A flying bomb touched something lethal. It explodes now, unless a receiver it has just passed may still catch
        /// it (late-press grace, or online a remote receiver's catch still on its way): then it stops dead where it hit
        /// and waits a moment (<see cref="CatchResolver.ExplosionHoldFor"/>). Either the late catch wins or the explosion happens.
        /// </summary>
        void LethalContact(BombFailReason reason, string detail)
        {
            if (ExplosionPending) return;

            float hold = resolver.ExplosionHoldFor();   // sweeps the last stretch of flight first: it may catch the bomb
            if (State != BombState.Thrown) return;
            if (hold <= 0f)
            {
                Explode(reason, detail);
                return;
            }

            bombPhysics.EnterInert();
            pendingReason = reason;
            pendingDetail = detail;
            pendingExplosionAt = Time.time + hold;
            PatataLog.Bomb($"Lethal contact held {hold * 1000f:F0} ms for a late catch ({reason} {detail})");
        }

        void OnFuseExpired()
        {
            if (State == BombState.Held)
                Explode(BombFailReason.HoldFuseExpired, $"carrier={Carrier}");
        }

        public void Explode(BombFailReason reason, string detail)
        {
            if (State == BombState.Exploding || State == BombState.Resetting) return;

            var from = State;
            pendingExplosionAt = -1f;
            LastFailReason = reason;
            IntendedReceiver = null;
            bombPhysics.EnterInert();
            Transition(BombState.Exploding);
            PatataLog.Bomb($"{from} -> Exploding reason={reason} {detail}");
            BombExploded?.Invoke(reason, detail);
        }

        // ------------------------------------------------------------------ reset

        /// <summary>First half of a section reset: make the bomb inert, ownerless, with a full fuse.</summary>
        public void BeginReset()
        {
            bombPhysics.EnterInert();
            transform.SetParent(null, true);
            pendingExplosionAt = -1f;
            IntendedReceiver = null;
            LastThrower = null;
            SetCarrier(null);
            fuse.Refresh();
            Transition(BombState.Resetting);
        }

        /// <summary>Second half: hand the bomb to <paramref name="carrier"/> and go live.</summary>
        public void EndReset(Player carrier)
        {
            SetCarrier(carrier);
            bombPhysics.EnterHeld(carrier.HandAnchor);
            fuse.Refresh();
            Transition(BombState.Held);
            PatataLog.Bomb($"Resetting -> Held carrier={carrier}");
        }

        // ------------------------------------------------------------------ remote mirroring

        /// <summary>
        /// Remote clients only: adopt the host's state and raise the same events the host raised, so audio,
        /// visuals and UI react identically everywhere. Never called on the authority.
        /// </summary>
        public void ApplyMirror(BombState newState, Player carrier, Player lastThrower, BombFailReason failReason, Player receiver)
        {
            var old = State;
            IntendedReceiver = receiver;
            LastThrower = lastThrower;
            LastFailReason = failReason;
            SetCarrier(carrier);
            if (newState == old) return;

            PatataLog.Bomb($"(mirror) {old} -> {newState} carrier={carrier} lastThrower={lastThrower}");
            Transition(newState);
            if (newState == BombState.Thrown) BombThrown?.Invoke(lastThrower);
            else if (newState == BombState.CaughtGrace) BombCaught?.Invoke(carrier);
            else if (newState == BombState.Exploding) BombExploded?.Invoke(failReason, "remote");
        }

        // ------------------------------------------------------------------ internals

        void SetCarrier(Player carrier)
        {
            if (Carrier == carrier) return;
            Carrier = carrier;
            debugCarrier = carrier != null ? carrier.DisplayName : "-";
            CarrierChanged?.Invoke(carrier);
        }

        void Transition(BombState next)
        {
            if (State == next) return;
            var from = State;
            State = next;
            debugState = next;
            StateChanged?.Invoke(from, next);
        }
    }
}
