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
        Resetting,
        InTransit   // carried by a tube or cannon (PROJECT_SPEC §5): no fuse, no collision, not catchable
    }

    public enum BombFailReason
    {
        HoldFuseExpired,
        WorldContact,
        KillZone,
        ForbiddenZone   // carrier in a forbidden fuse zone, or a flying bomb through a laser curtain (PROJECT_SPEC §7.3)
    }

    /// <summary>
    /// Owns the bomb state machine, the current carrier and every transition
    /// (PROJECT_SPEC §5). All gameplay decisions about the bomb end up here, exactly once.
    ///
    ///   Resetting --EndReset--> Held --TryThrow--> Thrown --AcceptCatch--> CaughtGrace --grace--> Held
    ///   Thrown --EnterTransit--> InTransit --delay--> Thrown (tubes and cannons, nobody is the thrower)
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
        BombTransit transit;   // host: the tube or cannon carrying the bomb while InTransit
        int transitExit;
        int flightId;
        float fuseHoldUntil = -1f;   // host: a remote carrier's fuse ran out; a throw they made before it may still arrive (F4)
        double fuseExpiredAt;
        Player predictedCatch;       // remote client: a catch this screen already showed (P3), not yet confirmed

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
        /// <summary>Host only: the carrier's fuse ran out and the explosion waits for a throw already on its way (#117 F4).</summary>
        public bool FuseHoldPending => fuseHoldUntil >= 0f;
        /// <summary>Remote client: the catch this screen showed ahead of the host's confirmation (presentation), or null.</summary>
        public Player PredictedCatch => predictedCatch;

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
                    if (FuseHoldPending)
                    {
                        if (Time.time >= fuseHoldUntil) Explode(BombFailReason.HoldFuseExpired, $"carrier={Carrier} (no throw before the fuse ran out)");
                        break;
                    }
                    if (InForbiddenZone()) break;
                    if (Carrier != null && Carrier.ControlLocked) break;   // online the section starts a moment ahead: no burn before it
                    fuse.Tick(Time.deltaTime * fuse.Rate);
                    break;
                case BombState.Thrown:
                    fuse.SetRate(1f);
                    if (ExplosionPending && Time.time >= pendingExplosionAt) Explode(pendingReason, pendingDetail);
                    break;
                case BombState.CaughtGrace:
                    if (InForbiddenZone()) break;   // a catch made inside a forbidden zone explodes at once
                    if (Time.time >= graceEndTime) Transition(BombState.Held);
                    break;
                case BombState.InTransit:
                    fuse.SetRate(1f);
                    if (transit != null && SimulationClock.ServerNow >= transit.ReleaseAt) LeaveTransit();
                    break;
                default:
                    fuse.SetRate(1f);
                    break;
            }
        }

        /// <summary>
        /// Updates the fuse rate from the fuse zones the carrier stands in (PROJECT_SPEC §7.3) and explodes the bomb if
        /// one of them is forbidden. Returns true when it exploded.
        /// </summary>
        bool InForbiddenZone()
        {
            float rate = FuseZone.RateFor(Carrier, tuning);
            if (!float.IsPositiveInfinity(rate))
            {
                fuse.SetRate(rate);
                return false;
            }
            Explode(BombFailReason.ForbiddenZone, $"carrier={Carrier} in a forbidden zone");
            return true;
        }

        // ------------------------------------------------------------------ throw / catch

        /// <summary>A throw released now (offline, the host's own player).</summary>
        public bool TryThrow(Player thrower, Vector3 origin, Vector3 velocity) =>
            TryThrow(thrower, origin, velocity, SimulationClock.ServerNow, false);

        /// <summary>
        /// A throw released at server time <paramref name="launchTime"/> (a remote thrower's release, back-dated by the host
        /// within <see cref="GameTuning.throwBackdateCap"/>, #117 P2): the flight is that segment, swept from its start.
        /// <paramref name="remote"/>: the thrower is drawn late here, so the drawing catches up (presentation).
        /// </summary>
        public bool TryThrow(Player thrower, Vector3 origin, Vector3 velocity, double launchTime, bool remote)
        {
            if (State != BombState.Held || Carrier != thrower)
            {
                PatataLog.Bomb($"Throw rejected {thrower} (state={State}, carrier={Carrier})");
                return false;
            }
            if (FuseHoldPending)
            {
                // The fuse ran out on the host while this throw was on its way: it counts if it left before the fuse ran out.
                if (launchTime > fuseExpiredAt)
                {
                    PatataLog.Bomb($"Throw rejected {thrower} (released {(launchTime - fuseExpiredAt) * 1000.0:F0} ms after the fuse ran out)");
                    return false;
                }
                fuseHoldUntil = -1f;
                SyncStats.FuseHoldSaves++;
                PatataLog.Bomb($"Throw from {thrower} beat the fuse by {(fuseExpiredAt - launchTime) * 1000.0:F0} ms (held for it)");
            }

            LastThrower = thrower;
            IntendedReceiver = PickIntendedReceiver(thrower, origin, velocity);
            SetCarrier(null);
            bombPhysics.EnterThrown(new FlightSegment(launchTime, origin, velocity, ++flightId), remote);   // before the state: the snapshot carries both
            Transition(BombState.Thrown);
            PatataLog.Bomb($"Held -> Thrown carrier={thrower} speed={velocity.magnitude:F1}" +
                           (remote ? $" released {(SimulationClock.ServerNow - launchTime) * 1000.0:F0} ms ago" : ""));
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

        // ------------------------------------------------------------------ transit (tubes and cannons)

        /// <summary>
        /// A flying bomb entered a transit mouth (<see cref="TransitMouth"/>, found by the host's sweep): the level takes it
        /// (PROJECT_SPEC §5 <c>InTransit</c>). It waits, inert and fuse-less, at the exit's hold point until the transit's
        /// delay is over. Authority only.
        /// </summary>
        public bool EnterTransit(BombTransit into, int exit)
        {
            if (!NetMode.IsAuthority || State != BombState.Thrown || ExplosionPending || into == null) return false;

            transit = into;
            transitExit = exit;
            IntendedReceiver = null;
            bombPhysics.EnterInert();
            var hold = into.GetExit(exit).hold;
            if (hold != null) transform.position = hold.position;
            into.SetTransit(exit, NetMode.ServerTime + into.Delay);
            Transition(BombState.InTransit);
            PatataLog.Bomb($"Thrown -> InTransit {into.name} exit={exit} delay={into.Delay:F2}s");
            return true;
        }

        /// <summary>The transit's delay is over: the bomb leaves the exit on its fixed arc, Thrown again, with no thrower.</summary>
        void LeaveTransit()
        {
            var from = transit;
            int exit = transitExit;
            double releaseAt = from.ReleaseAt;   // a server time: the exit arc starts exactly then on every machine
            transit = null;
            from.SetTransit(-1, -1.0);

            Vector3 origin = from.GetExit(exit).muzzle.position;
            Vector3 velocity = from.LaunchVelocity(exit);
            LastThrower = null;   // anyone may catch the exit arc, including whoever threw it in
            IntendedReceiver = PickIntendedReceiver(null, origin, velocity);
            bombPhysics.EnterThrown(new FlightSegment(releaseAt, origin, velocity, ++flightId));
            Transition(BombState.Thrown);
            PatataLog.Bomb($"InTransit -> Thrown {from.name} exit={exit} speed={velocity.magnitude:F1}");
            BombThrown?.Invoke(null);
        }

        void ClearTransit()
        {
            if (transit != null) transit.SetTransit(-1, -1.0);
            transit = null;
        }

        /// <summary>
        /// The player the (pure ballistic) arc passes closest to, within <see cref="IntendedReceiverReach"/>, as THIS machine
        /// draws the players: every machine computes it from the flight segment (the CATCH! marker, #117 P2).
        /// </summary>
        public Player PickIntendedReceiver(Player thrower, Vector3 origin, Vector3 velocity)
        {
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
        /// A flying bomb crossed a zone it may not cross (<see cref="BombBarrier"/>, found by <see cref="BombZoneSweep"/>).
        /// Lethal like world contact, including the hold for a late catch. Authority only.
        /// </summary>
        public void ReportZoneContact(string detail)
        {
            if (!NetMode.IsAuthority || State != BombState.Thrown) return;
            LethalContact(BombFailReason.ForbiddenZone, detail);
        }

        /// <summary>
        /// The flight's sweep (<see cref="BombPhysics.AdvanceTo"/>) entered a kill zone. Catches are decided by
        /// <see cref="CatchResolver"/>'s swept reach test; every other trigger (catch volumes included) is neutral.
        /// </summary>
        public void ReportKillZone(KillZone zone)
        {
            if (!NetMode.IsAuthority || State != BombState.Thrown) return;
            LethalContact(BombFailReason.KillZone, $"object={zone.name}");
        }

        /// <summary>
        /// A flying bomb touched something lethal. It explodes now, unless a receiver it has just passed may still catch
        /// it (late-press grace, or online a remote receiver's catch still on its way): then it stops dead where it hit
        /// and waits a moment (<see cref="CatchResolver.ExplosionHoldFor"/>). Either the late catch wins or the explosion happens.
        /// </summary>
        void LethalContact(BombFailReason reason, string detail)
        {
            if (ExplosionPending) return;

            float hold = resolver.ExplosionHoldFor();   // the flight's sweep has already tested this chord for catches
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
            if (State != BombState.Held) return;
            // A remote carrier saw their fuse a moment late, and their throw takes a moment to come: wait for it (F4).
            float wait = NetMode.IsAuthority ? LagBudget.LateMessageWait(Carrier, tuning, tuning.fuseExpiryHoldCap) : 0f;
            if (wait > 0f)
            {
                fuseHoldUntil = Time.time + wait;
                fuseExpiredAt = SimulationClock.ServerNow;
                SyncStats.FuseHolds++;
                PatataLog.Bomb($"Fuse ran out for {Carrier}: explosion held {wait * 1000f:F0} ms for a throw on its way");
                return;
            }
            Explode(BombFailReason.HoldFuseExpired, $"carrier={Carrier}");
        }

        public void Explode(BombFailReason reason, string detail)
        {
            if (State == BombState.Exploding || State == BombState.Resetting) return;
            if (DevCheats.NoExplosions && NetMode.IsAuthority && HandBack(reason, detail)) return;

            var from = State;
            pendingExplosionAt = -1f;
            ClearTransit();
            LastFailReason = reason;
            IntendedReceiver = null;
            bombPhysics.EnterInert();
            Transition(BombState.Exploding);
            PatataLog.Bomb($"{from} -> Exploding reason={reason} {detail}");
            BombExploded?.Invoke(reason, detail);
        }

        /// <summary>
        /// Dev cheat (<see cref="DevCheats.NoExplosions"/>): instead of exploding, the bomb goes back to its carrier, else its
        /// thrower, else anyone, with a fresh fuse. False when there is nobody to hand it to.
        /// </summary>
        bool HandBack(BombFailReason reason, string detail)
        {
            var to = Carrier != null ? Carrier : LastThrower;
            if (to == null)
                foreach (var p in Player.All)
                    if (p != null) { to = p; break; }
            if (to == null) return false;
            PatataLog.Bomb($"{State} -> handed back to {to} (no explosions, dev) reason={reason} {detail}");
            BeginReset();
            EndReset(to);
            return true;
        }

        // ------------------------------------------------------------------ reset

        /// <summary>First half of a section reset: make the bomb inert, ownerless, with a full fuse.</summary>
        public void BeginReset()
        {
            bombPhysics.EnterInert();
            transform.SetParent(null, true);
            pendingExplosionAt = -1f;
            ClearTransit();
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
            var shown = predictedCatch;
            predictedCatch = null;
            Transition(newState);
            if (newState == BombState.Thrown) BombThrown?.Invoke(lastThrower);
            else if (newState == BombState.CaughtGrace)
            {
                if (shown == carrier) SyncStats.PredictedCatchesConfirmed++;   // already shown (sound, brackets): not twice
                else BombCaught?.Invoke(carrier);
            }
            else if (newState == BombState.Exploding) BombExploded?.Invoke(failReason, "remote");
            if (shown != null && shown != carrier) SyncStats.PredictedCatchesRefused++;
        }

        /// <summary>
        /// Remote client, presentation only (#117 P3, spec §9.3): this screen saw <paramref name="receiver"/> (the local player)
        /// catch the bomb and claimed it; show the catch now (the catch event drives the sound and the brackets). The host
        /// still decides: its confirmation is not shown twice, its refusal puts the bomb back on its arc.
        /// </summary>
        public void ShowPredictedCatch(Player receiver)
        {
            if (NetMode.IsAuthority || State != BombState.Thrown || predictedCatch != null) return;
            predictedCatch = receiver;
            PatataLog.Bomb($"(predicted) catch shown for {receiver}");
            BombCaught?.Invoke(receiver);
        }

        /// <summary>Remote client: the host did not confirm the catch this screen showed; it is no longer shown.</summary>
        public void RetractPredictedCatch()
        {
            if (predictedCatch == null) return;
            PatataLog.Bomb($"(predicted) catch for {predictedCatch} not confirmed: back on the arc");
            predictedCatch = null;
            SyncStats.PredictedCatchesRefused++;
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
            fuseHoldUntil = -1f;
            StateChanged?.Invoke(from, next);
        }
    }
}
