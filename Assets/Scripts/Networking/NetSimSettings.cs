using System;

namespace HotPatata
{
    /// <summary>
    /// What the debug network simulator applies to every packet this machine sends (docs/TESTING.md §3, #117 P0). Applied by
    /// HotPatata.DebugTools (Editor and development builds only) through Unity's Network Simulator.
    /// </summary>
    [Serializable]
    public struct NetSimSettings
    {
        /// <summary>One-way delay, ms.</summary>
        public int DelayMs;
        /// <summary>Random extra delay, ± ms. Negative = the default (delay / 5).</summary>
        public int JitterMs;
        /// <summary>Share of packets dropped, %.</summary>
        public int LossPercent;
        /// <summary>Drop one packet in every N (0 = off): bursty loss on top of <see cref="LossPercent"/>.</summary>
        public int LossInterval;
        /// <summary>A lag spike of <see cref="SpikeMs"/> every <see cref="SpikeEverySeconds"/> (0 = none).</summary>
        public float SpikeEverySeconds;
        public int SpikeMs;

        public int EffectiveJitterMs => JitterMs >= 0 ? JitterMs : DelayMs / 5;

        public bool Any => DelayMs > 0 || JitterMs > 0 || LossPercent > 0 || LossInterval > 0 || SpikeEverySeconds > 0f;

        public override string ToString() =>
            $"{DelayMs} ms one-way ±{EffectiveJitterMs} ms, loss {LossPercent}%" +
            (LossInterval > 0 ? $" + 1/{LossInterval}" : "") +
            (SpikeEverySeconds > 0f ? $", {SpikeMs} ms spike every {SpikeEverySeconds:0.#} s" : "");

        /// <summary>
        /// Named conditions for <c>-patataNet</c> (the matrix of #117 §7), one way: half of the round trip each side.
        /// False for an unknown name.
        /// </summary>
        public static bool TryPreset(string name, out NetSimSettings settings)
        {
            settings = default;
            switch (name?.ToLowerInvariant())
            {
                case "lan": settings = Make(2, 1, 0); return true;
                case "good": settings = Make(50, 10, 0); return true;       // 100 ms RTT, wired
                case "wifi": settings = Make(60, 25, 2); return true;       // 120 ms RTT, 2 % loss
                case "relay": settings = Make(75, 15, 1); return true;      // 150 ms RTT through Relay
                case "bad": settings = Make(100, 30, 2); return true;       // the P2/P3 acceptance condition
                case "awful": settings = Make(150, 50, 5); return true;     // 300 ms RTT, 5 % loss
                case "spiky":
                    settings = Make(60, 25, 2);
                    settings.SpikeEverySeconds = 20f;
                    settings.SpikeMs = 500;
                    return true;
                default: return false;
            }
        }

        static NetSimSettings Make(int delay, int jitter, int loss) =>
            new NetSimSettings { DelayMs = delay, JitterMs = jitter, LossPercent = loss };
    }
}
