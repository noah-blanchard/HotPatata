using Unity.Netcode;

namespace HotPatata
{
    /// <summary>
    /// Replicates which exit of a tube or cannon the bomb will leave from, and when (for the exit's warning light and
    /// tone on every machine). The bomb's own state and position come from <see cref="NetworkBomb"/>.
    /// </summary>
    [UnityEngine.RequireComponent(typeof(BombTransit))]
    public class NetworkBombTransit : NetworkBehaviour
    {
        public struct State : INetworkSerializable, System.IEquatable<State>
        {
            public int Exit;
            public double ReleaseAt;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Exit);
                serializer.SerializeValue(ref ReleaseAt);
            }

            public bool Equals(State o) => Exit == o.Exit && ReleaseAt == o.ReleaseAt;
        }

        readonly NetworkVariable<State> state = new NetworkVariable<State>(new State { Exit = -1, ReleaseAt = -1.0 });   // server-written

        BombTransit transit;

        public override void OnNetworkSpawn()
        {
            transit = GetComponent<BombTransit>();
            if (IsServer)
            {
                transit.TransitChanged += OnChanged;
                OnChanged(transit.ActiveExit, transit.ReleaseAt);
            }
            else
            {
                state.OnValueChanged += OnMirror;
                transit.SetTransit(state.Value.Exit, state.Value.ReleaseAt, false);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (transit == null) return;
            transit.TransitChanged -= OnChanged;
            state.OnValueChanged -= OnMirror;
        }

        void OnChanged(int exit, double releaseAt) => state.Value = new State { Exit = exit, ReleaseAt = releaseAt };

        void OnMirror(State previous, State current) => transit.SetTransit(current.Exit, current.ReleaseAt, false);
    }
}
