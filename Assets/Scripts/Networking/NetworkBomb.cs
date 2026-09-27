using Unity.Netcode;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Replicates the bomb. The host runs the only real bomb (physics, fuse, catch resolution) and publishes
    /// a snapshot of its state; remote clients never simulate it, they mirror it:
    ///  - position while flying comes from the server-authoritative NetworkTransform;
    ///  - while held, the client attaches the bomb to the carrier's hand locally (no lag for the holder);
    ///  - state transitions raise the same events as on the host (sounds, pulse, carrier marker, UI).
    /// Nothing here lets a client change ownership.
    /// </summary>
    [RequireComponent(typeof(BombController))]
    public class NetworkBomb : NetworkBehaviour
    {
        public struct Snapshot : INetworkSerializable, System.IEquatable<Snapshot>
        {
            public int State;
            public int Carrier;       // player slot, -1 = nobody
            public int LastThrower;   // player slot, -1 = nobody
            public int FailReason;
            public int Sequence;      // forces a change so identical states still replicate

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref State);
                serializer.SerializeValue(ref Carrier);
                serializer.SerializeValue(ref LastThrower);
                serializer.SerializeValue(ref FailReason);
                serializer.SerializeValue(ref Sequence);
            }

            public bool Equals(Snapshot o) =>
                State == o.State && Carrier == o.Carrier && LastThrower == o.LastThrower && FailReason == o.FailReason && Sequence == o.Sequence;
        }

        readonly NetworkVariable<Snapshot> snapshot = new NetworkVariable<Snapshot>();   // server-written
        readonly NetworkVariable<float> fuseConsumed = new NetworkVariable<float>();     // server-written

        BombController bomb;
        int sequence;
        float lastSentFuse = -1f;

        public override void OnNetworkSpawn()
        {
            bomb = GetComponent<BombController>();

            if (IsServer)
            {
                bomb.StateChanged += (a, b) => Publish();
                bomb.CarrierChanged += c => Publish();
                Publish();
            }
            else
            {
                snapshot.OnValueChanged += (_, s) => Apply(s);
                Apply(snapshot.Value);
            }
        }

        void Publish()
        {
            snapshot.Value = new Snapshot
            {
                State = (int)bomb.State,
                Carrier = bomb.Carrier != null ? bomb.Carrier.PlayerId : -1,
                LastThrower = bomb.LastThrower != null ? bomb.LastThrower.PlayerId : -1,
                FailReason = (int)bomb.LastFailReason,
                Sequence = ++sequence
            };
        }

        void Apply(Snapshot s)
        {
            bomb.ApplyMirror((BombState)s.State, FindPlayer(s.Carrier), FindPlayer(s.LastThrower), (BombFailReason)s.FailReason);
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
            }
            else
            {
                bomb.Fuse.MirrorConsumed(fuseConsumed.Value);
                // The carrier may have spawned after the snapshot arrived: keep trying to resolve them.
                var s = snapshot.Value;
                if (s.Carrier >= 0 && bomb.Carrier == null) Apply(s);
            }
        }

        void LateUpdate()
        {
            if (!IsSpawned || IsServer) return;

            // Held or freshly caught: sit in the carrier's hand on this machine, whatever the network says.
            if ((bomb.State == BombState.Held || bomb.State == BombState.CaughtGrace) && bomb.Carrier != null)
                transform.position = bomb.Carrier.HandAnchor.position;
        }
    }
}
