using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Debug only (Editor and development builds): delays every packet this machine sends, using Unity's Network
    /// Simulator from the Multiplayer Tools package. Triggered by <see cref="NetworkBootstrap.SimulateLatency"/>
    /// (the -beepLatency flag). Lives in its own assembly so release builds never reference the tools package.
    /// </summary>
    static class LatencySimulator
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => NetworkBootstrap.LatencyRequested += Apply;

        static void Apply(GameObject networkObject, int milliseconds)
        {
            var simulator = networkObject.GetComponent<NetworkSimulator>() ?? networkObject.AddComponent<NetworkSimulator>();
            simulator.ConnectionPreset = new NetworkSimulatorPreset
            {
                Name = "Beep debug latency",
                PacketDelayMs = milliseconds,
                PacketJitterMs = milliseconds / 5,
                PacketLossPercent = 0
            };
            BeepLog.Run($"Simulating {milliseconds} ms one-way latency (+/-{milliseconds / 5} ms jitter)");
        }
    }
}
