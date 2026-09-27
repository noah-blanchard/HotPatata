using Unity.Netcode;

namespace Beep
{
    /// <summary>Replicates the moment a falling platform was triggered; every machine derives the same animation from it.</summary>
    [UnityEngine.RequireComponent(typeof(FallingPlatform))]
    public class NetworkFallingPlatform : NetworkBehaviour
    {
        readonly NetworkVariable<double> triggerTime = new NetworkVariable<double>(-1.0);   // server-written

        FallingPlatform platform;

        public override void OnNetworkSpawn()
        {
            platform = GetComponent<FallingPlatform>();
            if (IsServer)
            {
                platform.TriggerTimeChanged += t => triggerTime.Value = t;
                triggerTime.Value = platform.TriggerTime;
            }
            else
            {
                triggerTime.OnValueChanged += (_, t) =>
                {
                    platform.SetTriggerTime(t, false);
                    BeepLog.Run($"(mirror) {name} trigger time = {t:F2}");
                };
                platform.SetTriggerTime(triggerTime.Value, false);
            }
        }
    }
}
