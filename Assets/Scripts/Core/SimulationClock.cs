using System;
using Unity.Netcode;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The game's one notion of "now" for everything time-driven (docs/netcode-deterministic-plan.md §2.1): moving
    /// platforms, rotating bars, actuators, falling platforms, gates and transit readouts. It wraps NGO's network time;
    /// it is not a second time sync.
    ///  - <see cref="ServerNow"/> is sampled once per frame online, so every object drawn in a frame uses the same instant.
    ///  - On a client it is monotonic. When NGO corrects its estimate of the server time, the error is slewed in at no
    ///    more than <see cref="GameTuning.clockMaxSlew"/> instead of making the level (and every rider on it) jump back.
    ///    Only a desync larger than <see cref="GameTuning.clockSnapSeconds"/> snaps.
    ///  - On the host and offline it is exactly the raw time (offline it still freezes with the pause menu).
    ///  - Ticks: <see cref="ServerTick"/> and <see cref="SectionTick"/> count NGO network ticks online, physics steps
    ///    offline; a player's state is stamped with the time it was produced (<see cref="NetworkPlayer"/>).
    /// <see cref="SectionClock.Now"/> is a façade over <see cref="SectionTime"/>.
    /// </summary>
    public static class SimulationClock
    {
        static GameTuning tuning;
        static int sampledFrame = -1;
        static double now;
        static bool online;

        /// <summary>Gives the clock its slew limits (RunManager does it). Without them a client follows the raw time.</summary>
        public static void Configure(GameTuning t) => tuning = t;

        /// <summary>Server time as this machine draws the level this frame (seconds).</summary>
        public static double ServerNow
        {
            get
            {
                if (!NetMode.IsNetworked || !Application.isPlaying)
                {
                    online = false;
                    return NetMode.ServerTime;   // offline / edit mode: the local clock, untouched
                }
                Sample();
                return now;
            }
        }

        /// <summary>Server time at which the current section attempt began (replicated by the host).</summary>
        public static double SectionStart => RunManager.Instance != null ? RunManager.Instance.SectionStart : 0.0;

        /// <summary>Seconds since the current section attempt began; the time base for level motion.</summary>
        public static float SectionTime => (float)(ServerNow - SectionStart);   // the subtraction stays in double

        /// <summary>Ticks per second: NGO's network tick rate online, the physics rate offline.</summary>
        public static int TickRate =>
            NetMode.IsNetworked ? (int)NetworkManager.Singleton.NetworkConfig.TickRate : Mathf.Max(1, Mathf.RoundToInt(1f / Time.fixedDeltaTime));

        public static double TickDuration => 1.0 / TickRate;

        /// <summary>Whole ticks of server time.</summary>
        public static int ServerTick => (int)Math.Floor(ServerNow * TickRate);

        /// <summary>Whole ticks since the section attempt began.</summary>
        public static int SectionTick => (int)Math.Floor((ServerNow - SectionStart) * TickRate);

        /// <summary>How far this frame is between two ticks (0..1).</summary>
        public static float TickAlpha
        {
            get
            {
                double ticks = ServerNow * TickRate;
                return (float)(ticks - Math.Floor(ticks));
            }
        }

        static void Sample()
        {
            if (Time.frameCount == sampledFrame && online) return;
            double raw = NetMode.ServerTime;
            if (!online || NetMode.IsAuthority || tuning == null)
                now = raw;   // first sample, the host (its time is the server time) or unconfigured: no smoothing
            else
                now = Step(now, raw, Time.unscaledDeltaTime, tuning.clockMaxSlew, tuning.clockSnapSeconds);
            online = true;
            sampledFrame = Time.frameCount;
        }

        /// <summary>
        /// One frame of the client clock: advance by <paramref name="dt"/>, then correct the error against the raw network
        /// time by at most <paramref name="maxSlew"/> × dt, so the level never runs backwards nor jumps. An error larger
        /// than <paramref name="snapSeconds"/> is a real desync: snap. Pure (EditMode tested).
        /// </summary>
        public static double Step(double last, double raw, double dt, float maxSlew, float snapSeconds)
        {
            double predicted = last + Math.Max(0.0, dt);
            double error = raw - predicted;
            if (Math.Abs(error) > snapSeconds) return raw;
            double limit = Math.Max(0.0, maxSlew) * Math.Max(0.0, dt);
            return predicted + Math.Clamp(error, -limit, limit);
        }
    }
}
