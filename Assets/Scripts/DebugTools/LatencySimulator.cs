using System;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Debug only (Editor and development builds): delays, jitters and drops every packet this machine sends, and adds
    /// periodic lag spikes, using Unity's Network Simulator from the Multiplayer Tools package. Triggered by
    /// <see cref="NetworkBootstrap.SimulateNetwork"/> (the -patataLatency / -patataJitter / -patataLoss / -patataSpike /
    /// -patataNet flags, docs/TESTING.md §3). Lives in its own assembly so release builds never reference the tools package.
    /// </summary>
    static class LatencySimulator
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => NetworkBootstrap.SimulationRequested += Apply;

        static void Apply(GameObject networkObject, NetSimSettings settings)
        {
            var simulator = networkObject.GetComponent<NetworkSimulator>() ?? networkObject.AddComponent<NetworkSimulator>();
            simulator.ConnectionPreset = new NetworkSimulatorPreset
            {
                Name = "HotPatata debug network",
                PacketDelayMs = Mathf.Max(0, settings.DelayMs),
                PacketJitterMs = Mathf.Max(0, settings.EffectiveJitterMs),
                PacketLossPercent = Mathf.Clamp(settings.LossPercent, 0, 100),
                PacketLossInterval = Mathf.Max(0, settings.LossInterval)
            };
            var spikes = networkObject.GetComponent<LagSpikes>();
            if (settings.SpikeEverySeconds > 0f && settings.SpikeMs > 0)
            {
                spikes ??= networkObject.AddComponent<LagSpikes>();
                spikes.Configure(simulator, settings.SpikeEverySeconds, settings.SpikeMs);
            }
            else if (spikes != null) UnityEngine.Object.Destroy(spikes);
            PatataLog.Run($"Simulating {settings}");
        }

        /// <summary>A lag spike every few seconds (#117 §7: the 500 ms spike of the validation matrix).</summary>
        sealed class LagSpikes : MonoBehaviour
        {
            NetworkSimulator simulator;
            float every;
            int ms;
            float next;

            public void Configure(NetworkSimulator sim, float everySeconds, int spikeMs)
            {
                simulator = sim;
                every = everySeconds;
                ms = spikeMs;
                next = Time.unscaledTime + every;
            }

            void Update()
            {
                if (simulator == null || Time.unscaledTime < next) return;
                next = Time.unscaledTime + every;
                if (!NetMode.IsNetworked) return;
                simulator.TriggerLagSpike(TimeSpan.FromMilliseconds(ms));
                PatataLog.Sync($"lag spike {ms} ms (simulated)");
            }
        }
    }
}
