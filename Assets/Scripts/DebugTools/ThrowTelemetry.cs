using System.Collections.Generic;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Development only. Records every flight (path, closest approach to each player, catch window state) and writes
    /// one <c>[Throw]</c> line per throw, so pass feel can be tuned from numbers instead of impressions.
    /// Observes the bomb, never changes it. Created automatically in the Editor and development builds.
    /// </summary>
    public class ThrowTelemetry : MonoBehaviour
    {
        public struct Approach
        {
            public bool Valid;
            public float Distance;      // closest approach to the catch centre, in reach space (compare with ReachFor)
            public float Time;          // seconds after release
            public Vector3 BombPoint;   // where the bomb was then
            public Vector3 Center;      // where the catch centre was then
            public Vector3 Travel;      // the bomb's step there (its direction decides the facing bonus)
            public bool WindowOpen;     // was their catch window open at that moment
        }

        public static ThrowTelemetry Instance { get; private set; }

        /// <summary>The current (or last) flight, for the debug overlay.</summary>
        public readonly List<Vector3> Path = new List<Vector3>();
        public readonly Approach[] Closest = new Approach[Player.MaxSlots];
        public Player Thrower { get; private set; }
        public ThrowShot Shot { get; private set; }
        public string LastSummary { get; private set; }

        BombController bomb;
        float releaseTime;
        Vector3 lastPosition;
        bool flying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("ThrowDebug");
            DontDestroyOnLoad(go);
            go.AddComponent<ThrowTelemetry>();
            go.AddComponent<ThrowDebugOverlay>();
            go.AddComponent<PassPartner>();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            Unbind();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (BombController.Instance != bomb) Bind(BombController.Instance);
        }

        void Bind(BombController next)
        {
            Unbind();
            bomb = next;
            if (bomb == null) return;
            bomb.BombThrown += OnThrown;
            bomb.BombCaught += OnCaught;
            bomb.StateChanged += OnStateChanged;
        }

        void Unbind()
        {
            if (bomb == null) return;
            bomb.BombThrown -= OnThrown;
            bomb.BombCaught -= OnCaught;
            bomb.StateChanged -= OnStateChanged;
            bomb = null;
        }

        void OnThrown(Player thrower)
        {
            Thrower = thrower;
            Shot = thrower != null && thrower.Thrower.LastShot.Valid ? thrower.Thrower.LastShot : default;
            releaseTime = Time.time;
            lastPosition = bomb.transform.position;
            Path.Clear();
            Path.Add(lastPosition);
            for (int i = 0; i < Closest.Length; i++) Closest[i] = default;
            flying = true;
        }

        void FixedUpdate()
        {
            if (!flying || bomb == null || bomb.State != BombState.Thrown) return;

            Vector3 position = bomb.transform.position;
            if ((position - lastPosition).sqrMagnitude < 1e-6f) return;
            Path.Add(position);

            foreach (var p in Player.All)
            {
                if (p == null || p == Thrower || p.CatchVolume == null || p.PlayerId < 0 || p.PlayerId >= Closest.Length) continue;
                Vector3 center = p.CatchVolume.CatchCenter;
                float d = CatchResolver.ReachDistance(p.Tuning, lastPosition, position, center, out Vector3 point);
                ref var c = ref Closest[p.PlayerId];
                if (!c.Valid || d < c.Distance)
                {
                    c.Valid = true;
                    c.Distance = d;
                    c.Time = Time.time - releaseTime;
                    c.BombPoint = point;
                    c.Travel = position - lastPosition;
                    c.Center = center;
                    c.WindowOpen = p.Catcher.WindowOpen;
                }
            }
            lastPosition = position;
        }

        void OnCaught(Player receiver)
        {
            if (!flying) return;
            flying = false;
            Report($"CAUGHT by {receiver}", receiver);
        }

        void OnStateChanged(BombState from, BombState to)
        {
            if (!flying || from != BombState.Thrown || to == BombState.CaughtGrace) return;
            flying = false;
            Report($"MISSED ({bomb.LastFailReason})", NearestPlayer());
        }

        Player NearestPlayer()
        {
            Player best = null;
            float bestDistance = float.MaxValue;
            foreach (var p in Player.All)
            {
                if (p == null || p == Thrower || p.PlayerId < 0 || p.PlayerId >= Closest.Length || !Closest[p.PlayerId].Valid) continue;
                if (Closest[p.PlayerId].Distance < bestDistance)
                {
                    bestDistance = Closest[p.PlayerId].Distance;
                    best = p;
                }
            }
            return best;
        }

        void Report(string outcome, Player receiver)
        {
            float flight = Time.time - releaseTime;
            var s = Shot;
            string shot = s.Valid
                ? $"charge {s.Charge01:F2} speed {s.Velocity.magnitude:F1} m/s elev {Elevation(s.Velocity):F1}° " +
                  (s.HasAssist ? $"assist {s.AssistTarget} off {s.AssistAngle:F1}° turned {s.AssistCorrection:F1}°{(s.AssistYawOnly ? " yaw-only" : "")}" : "no assist")
                : "shot n/a (remote thrower)";

            string approach = "";
            if (receiver != null && receiver.PlayerId >= 0 && receiver.PlayerId < Closest.Length && Closest[receiver.PlayerId].Valid)
            {
                var c = Closest[receiver.PlayerId];
                approach = $" | closest to {receiver} {c.Distance:F2} m at {c.Time:F2}s (reach {CatchResolver.ReachFor(receiver, c.Travel):F2}, window {(c.WindowOpen ? "open" : "closed")})";
            }

            LastSummary = $"{Thrower} -> {outcome} after {flight:F2}s | {shot}{approach}";
            BeepLog.Throw(LastSummary);
        }

        static float Elevation(Vector3 v)
        {
            float horizontal = new Vector2(v.x, v.z).magnitude;
            return Mathf.Atan2(v.y, horizontal) * Mathf.Rad2Deg;
        }
    }
}
