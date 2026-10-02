using Unity.Netcode;

namespace HotPatata
{
    /// <summary>
    /// Replicates when an actuator last changed direction (time, progress then, direction); every machine derives the
    /// same motion from it and the server clock.
    /// </summary>
    [UnityEngine.RequireComponent(typeof(SignalActuator))]
    public class NetworkSignalActuator : NetworkBehaviour
    {
        public struct State : INetworkSerializable, System.IEquatable<State>
        {
            public double ChangeTime;
            public float FromProgress;
            public bool Opening;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref ChangeTime);
                serializer.SerializeValue(ref FromProgress);
                serializer.SerializeValue(ref Opening);
            }

            public bool Equals(State o) => ChangeTime == o.ChangeTime && FromProgress == o.FromProgress && Opening == o.Opening;
        }

        readonly NetworkVariable<State> state = new NetworkVariable<State>();   // server-written

        SignalActuator actuator;

        public override void OnNetworkSpawn()
        {
            actuator = GetComponent<SignalActuator>();
            if (IsServer)
            {
                actuator.StateChanged += OnChanged;
                OnChanged(actuator.ChangeTime, actuator.FromProgress, actuator.Opening);
            }
            else
            {
                state.OnValueChanged += OnMirror;
                Apply(state.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (actuator == null) return;
            actuator.StateChanged -= OnChanged;
            state.OnValueChanged -= OnMirror;
        }

        void OnChanged(double time, float progress, bool opening) =>
            state.Value = new State { ChangeTime = time, FromProgress = progress, Opening = opening };

        void OnMirror(State previous, State current)
        {
            Apply(current);
            PatataLog.Run($"(mirror) {name} {(current.Opening ? "opening" : "closing")}");
        }

        void Apply(State s) => actuator.SetState(s.ChangeTime, s.FromProgress, s.Opening, false);
    }
}
