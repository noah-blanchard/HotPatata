using Unity.Netcode;

namespace HotPatata
{
    /// <summary>Replicates the moment the bomb last flew through a gate; every machine derives the gate's state from it.</summary>
    [UnityEngine.RequireComponent(typeof(BombGate))]
    public class NetworkBombGate : NetworkBehaviour
    {
        readonly NetworkVariable<double> passedAt = new NetworkVariable<double>(-1.0);   // server-written

        BombGate gate;

        public override void OnNetworkSpawn()
        {
            gate = GetComponent<BombGate>();
            if (IsServer)
            {
                gate.PassedAtChanged += OnPassed;
                passedAt.Value = gate.PassedAt;
            }
            else
            {
                passedAt.OnValueChanged += OnMirror;
                gate.SetPassedAt(passedAt.Value, false);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (gate == null) return;
            gate.PassedAtChanged -= OnPassed;
            passedAt.OnValueChanged -= OnMirror;
        }

        void OnPassed(double t) => passedAt.Value = t;

        void OnMirror(double previous, double t)
        {
            gate.SetPassedAt(t, false);
            PatataLog.Run($"(mirror) {name} passed at {t:F2}");
        }
    }
}
