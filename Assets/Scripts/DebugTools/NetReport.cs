using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Development only. Writes one <c>[NetReport]</c> block to the log when a session ends (leave, disconnect, quit), from
    /// <see cref="NetQuality"/> and <see cref="SyncStats"/> (#117 P0): enough to explain a "disaster moment" after the fact.
    /// The F7 panel (<see cref="NetSyncProbe"/>) shows the same numbers live. How to read it: docs/TESTING.md §3.
    /// Observes only. Created automatically in the Editor and development builds.
    /// </summary>
    public class NetReport : MonoBehaviour
    {
        bool wasNetworked;
        bool wasHost;
        float startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("NetReport");
            DontDestroyOnLoad(go);
            go.AddComponent<NetReport>();
        }

        void Update()
        {
            bool networked = NetMode.IsNetworked;
            if (networked && !wasNetworked)
            {
                startedAt = Time.unscaledTime;
                wasHost = NetworkManager.Singleton.IsServer;
            }
            else if (!networked && wasNetworked) Write();
            wasNetworked = networked;
        }

        void OnApplicationQuit()
        {
            if (wasNetworked) Write();
            wasNetworked = false;
        }

        void Write()
        {
            Debug.Log(Build(wasHost, Time.unscaledTime - startedAt));
        }

        /// <summary>The report as text (also shown by the F7 panel, without the header).</summary>
        public static string Build(bool host, float seconds)
        {
            var b = new StringBuilder();
            b.Append($"[NetReport] {(host ? "host" : "client")} session {seconds:F0} s, link {NetQuality.Link}\n");
            AppendBody(b);
            return b.ToString();
        }

        public static void AppendBody(StringBuilder b)
        {
            foreach (var pair in NetQuality.Rtt)
                b.Append($"  rtt to {pair.Key}: p50 {pair.Value.Percentile(0.5):F0} ms  p95 {pair.Value.Percentile(0.95):F0} ms  max {pair.Value.Max:F0} ms\n");

            for (int slot = 0; slot < Player.MaxSlots; slot++)
            {
                var s = NetQuality.StreamOf(slot);
                if (s == null || s.Packets == 0) continue;
                b.Append($"  snapshots from slot {slot}: {s.Packets} packets, jitter {s.Jitter * 1000f:F0} ms, " +
                         $"gap p95 {s.Gaps.Percentile(0.95) * 1000.0:F0} ms longest {s.LongestGap * 1000.0:F0} ms, " +
                         $"freezes>{NetQuality.FreezeSeconds * 1000.0:F0}ms {s.Freezes}, lost {s.Lost} ({s.LossPercent:F1}%), recovered {s.Recovered}\n");
            }

            var f = NetQuality.Frames;
            if (f.Count > 0)
                b.Append($"  frames: p50 {f.Percentile(0.5) * 1000.0:F1} ms  p99 {f.Percentile(0.99) * 1000.0:F1} ms  worst {NetQuality.WorstFrame * 1000.0:F0} ms  " +
                         $"spikes>{NetQuality.FrameSpikeSeconds * 1000.0:F0}ms {NetQuality.FrameSpikes}\n");

            b.Append($"  clock: NGO hard resets {SyncStats.ClockJumps}, level clock snaps {SyncStats.ClockSnaps}\n");
            b.Append($"  caps hit (host): hazard stamps {SyncStats.StampsClamped}, catch claims {SyncStats.ClaimsClamped}, throws {SyncStats.ThrowsClamped}\n");
            b.Append($"  catches: claims accepted {SyncStats.ClaimsAccepted} refused {SyncStats.ClaimsRefused} | " +
                     $"shown at once then confirmed {SyncStats.PredictedCatchesConfirmed} refused {SyncStats.PredictedCatchesRefused}\n");
            b.Append($"  throws (client): prediction agree {SyncStats.ThrowAgreed} disagree {SyncStats.ThrowDisagreed}");
            var pops = SyncStats.FlightPops;
            if (pops.Count > 0) b.Append($" | arc pop p50 {pops.Percentile(0.5) * 100.0:F0} cm p95 {pops.Percentile(0.95) * 100.0:F0} cm max {pops.Max * 100.0:F0} cm");
            b.Append('\n');
            b.Append($"  fuse (host): expiry holds {SyncStats.FuseHolds}, saved by a late throw {SyncStats.FuseHoldSaves}\n");
            b.Append($"  hazards (host): trigger {SyncStats.HazardTriggerHits}  rewound {SyncStats.HazardRewindHits}  static {SyncStats.StaticKills}");
        }
    }
}
