using Unity.Netcode;

namespace HotPatata
{
    /// <summary>
    /// Replicates when an hourglass plate stops (PROJECT_SPEC §13.22): open-ended while held, the release time plus its
    /// memory once let go. Clients stop deciding it themselves and only read the host's value, so the sand and the actuator
    /// agree on every machine. Plain and heavy plates need no replication (their actuator is replicated).
    /// </summary>
    [UnityEngine.RequireComponent(typeof(PressurePlate))]
    public class NetworkPressurePlate : NetworkBehaviour
    {
        readonly NetworkVariable<double> activeUntil = new NetworkVariable<double>(double.NegativeInfinity);   // server-written

        PressurePlate plate;

        public override void OnNetworkSpawn()
        {
            plate = GetComponent<PressurePlate>();
            if (IsServer)
            {
                plate.ActiveUntilChanged += OnChanged;
                activeUntil.Value = plate.ActiveUntil;
            }
            else
            {
                activeUntil.OnValueChanged += OnMirror;
                plate.Mirror(activeUntil.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (plate == null) return;
            plate.ActiveUntilChanged -= OnChanged;
            activeUntil.OnValueChanged -= OnMirror;
        }

        void OnChanged(double until) => activeUntil.Value = until;

        void OnMirror(double previous, double until) => plate.Mirror(until);
    }
}
