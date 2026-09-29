using Unity.Netcode;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Replicates the bomb. The host runs the only real bomb (physics, fuse, catch resolution) and publishes
    /// a snapshot of its state; remote clients never simulate it, they mirror it:
    ///  - position while flying comes from the server-authoritative NetworkTransform;
    ///  - while held, the client attaches the bomb to the carrier's hand locally (no lag for the holder);
    ///  - a client that throws draws its own throw at once (<see cref="PredictLocalThrow"/>): the flight is a plain
    ///    ballistic arc, so the local drawing is exact; it stops where it would reach a receiver or hit the world and
    ///    waits there for the host's verdict (caught / exploded), which then takes over;
    ///  - state transitions raise the same events as on the host (sounds, pulse, carrier marker, UI).
    /// Nothing here lets a client change ownership: the prediction is drawing only.
    /// </summary>
    [RequireComponent(typeof(BombController))]
    [DefaultExecutionOrder(1000)]   // draw after the NetworkTransform has written the replicated position
    public class NetworkBomb : NetworkBehaviour
    {
        const float PredictionHorizon = 3f;       // seconds of flight drawn at most
        const float PredictionStep = 0.02f;
        const float RejectedAfter = 1.5f;         // still in the thrower's hand this long after the request: the host said no
        const float BombRadius = 0.15f;
        public struct Snapshot : INetworkSerializable, System.IEquatable<Snapshot>
        {
            public int State;
            public int Carrier;       // player slot, -1 = nobody
            public int LastThrower;   // player slot, -1 = nobody
            public int FailReason;
            public int Receiver;      // player slot the flight is heading for (feedback only), -1 = none
            public int Sequence;      // forces a change so identical states still replicate

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref State);
                serializer.SerializeValue(ref Carrier);
                serializer.SerializeValue(ref LastThrower);
                serializer.SerializeValue(ref FailReason);
                serializer.SerializeValue(ref Receiver);
                serializer.SerializeValue(ref Sequence);
            }

            public bool Equals(Snapshot o) =>
                State == o.State && Carrier == o.Carrier && LastThrower == o.LastThrower && FailReason == o.FailReason && Receiver == o.Receiver && Sequence == o.Sequence;
        }

        readonly NetworkVariable<Snapshot> snapshot = new NetworkVariable<Snapshot>();   // server-written
        readonly NetworkVariable<float> fuseConsumed = new NetworkVariable<float>();     // server-written
        readonly NetworkVariable<float> fuseRate = new NetworkVariable<float>(1f);       // server-written (fuse zones)

        BombController bomb;
        int sequence;
        float lastSentFuse = -1f;

        // client-side throw prediction (drawing only)
        bool predicting;
        Player predictedThrower;
        Vector3 predictedOrigin, predictedVelocity;
        float predictedStart, predictedStop;
        bool sawThrown;
        int environmentMask;

        public override void OnNetworkSpawn()
        {
            bomb = GetComponent<BombController>();
            environmentMask = LayerMask.GetMask("Environment", "Hazard");

            if (IsServer)
            {
                bomb.StateChanged += OnStateChanged;
                bomb.CarrierChanged += OnCarrierChanged;
                Publish();
            }
            else
            {
                snapshot.OnValueChanged += OnSnapshotChanged;
                Apply(snapshot.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (bomb == null) return;
            bomb.StateChanged -= OnStateChanged;
            bomb.CarrierChanged -= OnCarrierChanged;
            snapshot.OnValueChanged -= OnSnapshotChanged;
        }

        void OnStateChanged(BombState from, BombState to) => Publish();
        void OnCarrierChanged(Player carrier) => Publish();
        void OnSnapshotChanged(Snapshot previous, Snapshot current) => Apply(current);

        void Publish()
        {
            snapshot.Value = new Snapshot
            {
                State = (int)bomb.State,
                Carrier = bomb.Carrier != null ? bomb.Carrier.PlayerId : -1,
                LastThrower = bomb.LastThrower != null ? bomb.LastThrower.PlayerId : -1,
                FailReason = (int)bomb.LastFailReason,
                Receiver = bomb.IntendedReceiver != null ? bomb.IntendedReceiver.PlayerId : -1,
                Sequence = ++sequence
            };
        }

        void Apply(Snapshot s)
        {
            bomb.ApplyMirror((BombState)s.State, FindPlayer(s.Carrier), FindPlayer(s.LastThrower), (BombFailReason)s.FailReason, FindPlayer(s.Receiver));
        }

        static Player FindPlayer(int slot)
        {
            if (slot < 0) return null;
            foreach (var p in Player.All)
                if (p.PlayerId == slot) return p;
            return null;
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                float c = bomb.Fuse.Consumed01;
                if (Mathf.Abs(c - lastSentFuse) > 0.004f || c == 0f)
                {
                    lastSentFuse = c;
                    fuseConsumed.Value = c;
                }
                if (fuseRate.Value != bomb.Fuse.Rate) fuseRate.Value = bomb.Fuse.Rate;
            }
            else
            {
                bomb.Fuse.MirrorConsumed(fuseConsumed.Value);
                bomb.Fuse.SetRate(fuseRate.Value);
                // The carrier may have spawned after the snapshot arrived: keep trying to resolve them.
                var s = snapshot.Value;
                if (s.Carrier >= 0 && bomb.Carrier == null) Apply(s);
            }
        }

        /// <summary>
        /// Remote client that just asked the host to throw: draw the throw now instead of a round trip later. The arc is
        /// drawn until it would reach a receiver (the closest reach along the path) or hit the world, then held there until
        /// the host's outcome arrives. If the host never throws, the bomb goes back to the hand.
        /// </summary>
        public void PredictLocalThrow(Player thrower, Vector3 origin, Vector3 velocity)
        {
            if (!IsSpawned || IsServer || bomb == null) return;

            predicting = true;
            sawThrown = false;
            predictedThrower = thrower;
            predictedOrigin = origin;
            predictedVelocity = velocity;
            predictedStart = Time.time;

            // Where the drawing stops: first reach of another player, or the first thing it would hit.
            float g = ThrowBallistics.Gravity(bomb.Tuning);
            predictedStop = PredictionHorizon;
            Vector3 previous = origin;
            for (float t = PredictionStep; t <= PredictionHorizon; t += PredictionStep)
            {
                Vector3 next = ThrowBallistics.PositionAt(origin, velocity, g, t);
                Vector3 delta = next - previous;
                if (Physics.SphereCast(previous, BombRadius, delta.normalized, out var hit, delta.magnitude, environmentMask, QueryTriggerInteraction.Ignore))
                {
                    predictedStop = t - PredictionStep + PredictionStep * (hit.distance / Mathf.Max(1e-4f, delta.magnitude));
                    break;
                }
                bool reached = false;
                foreach (var p in Player.All)
                {
                    if (p == null || p == thrower || p.CatchVolume == null) continue;
                    float d = CatchResolver.ReachDistance(p.Tuning, previous, next, p.CatchVolume.CatchCenter, out _);
                    if (d <= CatchResolver.ReachFor(p, delta)) reached = true;
                }
                if (reached)
                {
                    predictedStop = t;
                    break;
                }
                previous = next;
            }
        }

        /// <summary>True while this machine draws its own throw ahead of the host's confirmation.</summary>
        public bool Predicting => predicting;

        void UpdatePrediction()
        {
            var state = bomb.State;
            if (state == BombState.Thrown) sawThrown = true;

            bool outcome = state == BombState.CaughtGrace || state == BombState.Exploding || state == BombState.Resetting ||
                           state == BombState.InTransit || (sawThrown && state == BombState.Held);
            bool rejected = !sawThrown && state == BombState.Held && Time.time - predictedStart > RejectedAfter;
            bool lostIt = !sawThrown && bomb.Carrier != predictedThrower;
            if (outcome || rejected || lostIt || Time.time - predictedStart > PredictionHorizon + RejectedAfter)
            {
                predicting = false;
                return;
            }

            float t = Mathf.Min(Time.time - predictedStart, predictedStop);
            transform.position = ThrowBallistics.PositionAt(predictedOrigin, predictedVelocity, ThrowBallistics.Gravity(bomb.Tuning), t);
        }

        void LateUpdate()
        {
            if (!IsSpawned || IsServer) return;

            if (predicting)
            {
                UpdatePrediction();
                if (predicting) return;
            }

            // Held or freshly caught: sit in the carrier's hand on this machine, whatever the network says.
            if ((bomb.State == BombState.Held || bomb.State == BombState.CaughtGrace) && bomb.Carrier != null)
                transform.position = bomb.Carrier.HandAnchor.position;
        }
    }
}
