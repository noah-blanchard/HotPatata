using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// What the network is really doing, measured on this machine (#117 P0): round trips, the jitter and gaps of each
    /// remote player's snapshot stream, the snapshots lost or recovered by redundancy, frame times and the link in use.
    /// It observes only, except that the host sizes its waits for late messages from the measured jitter
    /// (<see cref="JitterSeconds"/>), never from a guess. The F7 panel and the end-of-session <c>[NetReport]</c>
    /// (HotPatata.DebugTools) read it.
    /// </summary>
    public static class NetQuality
    {
        const int MinJitterSamples = 10;
        const double RttSampleEvery = 0.5;   // seconds
        /// <summary>Seconds without a snapshot from a remote player that count as a freeze of that player.</summary>
        public const double FreezeSeconds = 0.1;
        /// <summary>A frame longer than this is a spike (seconds).</summary>
        public const double FrameSpikeSeconds = 0.05;

        /// <summary>One remote player's snapshot stream, as this machine receives it.</summary>
        public sealed class Stream
        {
            public readonly RollingSamples Transit = new RollingSamples(128);   // arrival (server time here) - stamp time
            public readonly RollingSamples Gaps = new RollingSamples(128);      // seconds between packets
            public double LastArrival = -1.0;
            public int Highest = -1;
            public int Received, Recovered, Lost, Freezes, Packets;
            public double LongestGap;

            public float Jitter => Transit.Count < MinJitterSamples ? 0f : (float)(Transit.Percentile(0.95) - Transit.Percentile(0.5));
            public float LossPercent => Received + Lost == 0 ? 0f : 100f * Lost / (Received + Lost);
        }

        static readonly Stream[] streams = new Stream[Player.MaxSlots];
        static readonly Dictionary<ulong, RollingSamples> rtt = new Dictionary<ulong, RollingSamples>();
        static readonly RollingSamples frames = new RollingSamples(600);
        static double nextRttSample;

        /// <summary>Frames longer than <see cref="FrameSpikeSeconds"/> since the session began, and the longest one.</summary>
        public static int FrameSpikes { get; private set; }
        public static double WorstFrame { get; private set; }
        /// <summary>The transport link: protocol and the endpoint this machine talks to (a Relay server online).</summary>
        public static string Link { get; set; } = "-";
        public static IReadOnlyDictionary<ulong, RollingSamples> Rtt => rtt;
        public static RollingSamples Frames => frames;

        public static Stream StreamOf(int slot) =>
            slot >= 0 && slot < streams.Length ? streams[slot] ??= new Stream() : null;

        /// <summary>A session begins: forget the previous one.</summary>
        public static void Clear()
        {
            for (int i = 0; i < streams.Length; i++) streams[i] = null;
            rtt.Clear();
            frames.Clear();
            FrameSpikes = 0;
            WorstFrame = 0.0;
            Link = "-";
            SyncStats.Clear();
        }

        /// <summary>Once per frame (NetworkBootstrap): frame time, and every half second each connection's round trip.</summary>
        public static void SampleFrame()
        {
            double dt = Time.unscaledDeltaTime;
            if (!NetMode.IsNetworked) return;
            frames.Add(dt);
            if (dt > FrameSpikeSeconds) FrameSpikes++;
            if (dt > WorstFrame) WorstFrame = dt;

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextRttSample) return;
            nextRttSample = now + RttSampleEvery;
            var nm = NetworkManager.Singleton;
            if (nm.IsServer)
            {
                foreach (ulong id in nm.ConnectedClientsIds)
                    if (id != nm.LocalClientId) AddRtt(id);
            }
            else AddRtt(NetworkManager.ServerClientId);
        }

        static void AddRtt(ulong id)
        {
            int ms = NetMode.RttMsFor(id);
            if (ms <= 0) return;
            if (!rtt.TryGetValue(id, out var samples)) rtt[id] = samples = new RollingSamples(240);
            samples.Add(ms);
        }

        /// <summary>
        /// A packet of snapshots from <paramref name="slot"/> arrived. <paramref name="newest"/> is the newest sequence in it
        /// and <paramref name="newestTime"/> its stamp time. Call <see cref="NoteSnapshot"/> for each snapshot first.
        /// </summary>
        public static void NotePacket(int slot, double newestTime)
        {
            var s = StreamOf(slot);
            if (s == null) return;
            double arrival = Time.realtimeSinceStartupAsDouble;
            s.Packets++;
            s.Transit.Add(NetMode.ServerTime - newestTime);
            if (s.LastArrival >= 0.0)
            {
                double gap = arrival - s.LastArrival;
                s.Gaps.Add(gap);
                if (gap > s.LongestGap) s.LongestGap = gap;
                if (gap > FreezeSeconds) s.Freezes++;
            }
            s.LastArrival = arrival;
        }

        /// <summary>
        /// One snapshot of a packet, oldest first. True when it is new (never seen); <paramref name="redundant"/> = it came as
        /// a copy riding with a newer one. A sequence skipped for good counts as lost.
        /// </summary>
        public static bool NoteSnapshot(int slot, int sequence, bool redundant)
        {
            var s = StreamOf(slot);
            if (s == null) return true;
            if (sequence <= s.Highest) return false;
            if (s.Highest >= 0 && sequence > s.Highest + 1) s.Lost += sequence - s.Highest - 1;
            s.Highest = sequence;
            s.Received++;
            if (redundant) s.Recovered++;
            return true;
        }

        /// <summary>A remote player's sequence restarts (they respawned as a new object): accept from scratch.</summary>
        public static void RestartStream(int slot)
        {
            var s = StreamOf(slot);
            if (s != null) s.Highest = -1;
        }

        /// <summary>Jitter (p95 − median of the transit time) of <paramref name="slot"/>'s snapshots here, seconds; 0 when unknown.</summary>
        public static float JitterSeconds(int slot) => StreamOf(slot)?.Jitter ?? 0f;

        /// <summary>The worst <see cref="JitterSeconds"/> among the remote players this machine hears.</summary>
        public static float WorstJitterSeconds
        {
            get
            {
                float worst = 0f;
                foreach (var s in streams)
                    if (s != null && s.Jitter > worst) worst = s.Jitter;
                return worst;
            }
        }

        /// <summary>Median round trip to <paramref name="clientId"/> in ms (0 when unknown).</summary>
        public static int RttP50(ulong clientId) => rtt.TryGetValue(clientId, out var s) ? (int)s.Percentile(0.5) : 0;
        public static int RttP95(ulong clientId) => rtt.TryGetValue(clientId, out var s) ? (int)s.Percentile(0.95) : 0;
    }
}
