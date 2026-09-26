using System;
using UnityEngine;

namespace Beep
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
    /// </summary>
    [RequireComponent(typeof(BombFuse), typeof(BombPhysics), typeof(CatchResolver))]
    public class BombController : MonoBehaviour
    {
        [SerializeField] GameTuning tuning;

        [Header("Debug (read-only at runtime)")]
        [SerializeField] BombState debugState = BombState.Resetting;
        [SerializeField] string debugCarrier = "-";
        [SerializeField] float debugFuseRemaining;

        BombFuse fuse;
        BombPhysics bombPhysics;
        CatchResolver resolver;
        float graceEndTime;

        public BombState State { get; private set; } = BombState.Resetting;
        public Player Carrier { get; private set; }
        /// <summary>Who released the bomb most recently; cannot catch that same flight.</summary>
        public Player LastThrower { get; private set; }

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
            fuse = GetComponent<BombFuse>();
            bombPhysics = GetComponent<BombPhysics>();
            resolver = GetComponent<CatchResolver>();
        }

        void OnEnable() => fuse.Expired += OnFuseExpired;
        void OnDisable() => fuse.Expired -= OnFuseExpired;

        void Update()
        {
            switch (State)
            {
                case BombState.Held:
                    fuse.Tick(Time.deltaTime);
                    break;
                case BombState.CaughtGrace:
                    if (Time.time >= graceEndTime) Transition(BombState.Held);
                    break;
            }

            debugFuseRemaining = fuse.Remaining;
        }

        // ------------------------------------------------------------------ throw / catch

        public bool TryThrow(Player thrower, Vector3 origin, Vector3 velocity)
        {
            if (State != BombState.Held || Carrier != thrower)
            {
                BeepLog.Bomb($"Throw rejected {thrower} (state={State}, carrier={Carrier})");
                return false;
            }

            LastThrower = thrower;
            SetCarrier(null);
            Transition(BombState.Thrown);
            bombPhysics.EnterThrown(origin, velocity);
            BeepLog.Bomb($"Held -> Thrown carrier={thrower} speed={velocity.magnitude:F1}");
            BombThrown?.Invoke(thrower);
            return true;
        }

        /// <summary>Called by <see cref="CatchResolver"/> once it has validated a catch.</summary>
        public bool AcceptCatch(Player receiver)
        {
            if (State != BombState.Thrown) return false;

            SetCarrier(receiver);
            bombPhysics.EnterHeld(receiver.HandAnchor);
            fuse.Refresh();
            graceEndTime = Time.time + tuning.caughtGraceDuration;
            Transition(BombState.CaughtGrace);
            BeepLog.Bomb($"Thrown -> CaughtGrace carrier={receiver}");
            BombCaught?.Invoke(receiver);
            return true;
        }

        // ------------------------------------------------------------------ failure

        /// <summary>Lethal contact reported by <see cref="BombPhysics"/>. Only counts while Thrown.</summary>
        public void ReportWorldContact(Collider other)
        {
            if (State != BombState.Thrown) return;
            Explode(BombFailReason.WorldContact,
                $"object={other.name} layer={LayerMask.LayerToName(other.gameObject.layer)}");
        }

        /// <summary>Trigger contact from <see cref="BombPhysics"/>: catch volumes and kill zones only.</summary>
        public void ReportTriggerContact(Collider other)
        {
            if (State != BombState.Thrown) return;

            if (other.TryGetComponent(out PlayerCatchVolume volume))
                resolver.TryResolveCatch(volume);
            else if (other.TryGetComponent(out KillZone _))
                Explode(BombFailReason.KillZone, $"object={other.name}");
            // Any other trigger is intentionally neutral: it must never count as lethal world contact.
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
            bombPhysics.EnterInert();
            Transition(BombState.Exploding);
            BeepLog.Bomb($"{from} -> Exploding reason={reason} {detail}");
            BombExploded?.Invoke(reason, detail);
        }

        // ------------------------------------------------------------------ reset

        /// <summary>First half of a section reset: make the bomb inert, ownerless, with a full fuse.</summary>
        public void BeginReset()
        {
            bombPhysics.EnterInert();
            transform.SetParent(null, true);
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
            BeepLog.Bomb($"Resetting -> Held carrier={carrier}");
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
