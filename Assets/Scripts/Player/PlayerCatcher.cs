using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Catching is a timed action, never automatic. Pressing catch opens a short window
    /// (<see cref="GameTuning.catchWindowDuration"/>); a bomb only counts as caught if it reaches this
    /// player's catch volume while the window is open. After the window closes there is a cooldown
    /// (<see cref="GameTuning.catchCooldown"/>) so the button cannot be mashed. The bomb's
    /// <see cref="CatchResolver"/> reads <see cref="WindowOpen"/>; this class never touches the bomb.
    ///
    /// Online, a remote player's own machine also watches its local window: when the (late-rendered) bomb reaches
    /// the catch sphere while it is open, it sends one claim per window and the host's CatchResolver validates it.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerCatcher : MonoBehaviour
    {
        // Catch diagnostics only (they explain a miss, they never decide one).
        const float HintDuration = 1.6f;
        const float ReachSlack = 0.25f;           // metres beyond the reach that still count as "in reach now"
        const float LateMax = 0.8f;               // seconds since the bomb was in reach that still read as "too late"
        const float EarlyCheckDistance = 12f;     // metres: farther than this, a closed window is not "too early"

        Player player;
        BombController bomb;
        float windowEnd = -1f;
        float cooldownEnd = -1f;
        float lastReachTime = -10f;
        bool windowWasOpen;
        bool claimSent;
        Vector3 lastSeen;              // remote client: where the bomb was drawn last frame
        bool seenValid;
        float seenInReachAt = -10f;
        string hint;
        float hintUntil;
        string deferredHint;   // host: a miss explanation held back while a lag-compensated catch may still arrive
        float deferredHintAt;

        /// <summary>Why the last catch attempt failed ("too late", "too early"), for a moment; null otherwise.</summary>
        public string Hint => Time.time < hintUntil ? hint : null;

        public bool WindowOpen => Time.time < windowEnd;
        public bool OnCooldown => !WindowOpen && Time.time < cooldownEnd;

        /// <summary>1 when the window has just opened, 0 when it closes.</summary>
        public float WindowRemaining01 => WindowOpen ? (windowEnd - Time.time) / player.Tuning.catchWindowDuration : 0f;
        /// <summary>1 right after the window closes, 0 when catch is available again.</summary>
        public float CooldownRemaining01 => OnCooldown ? (cooldownEnd - Time.time) / Mathf.Max(0.0001f, player.Tuning.catchCooldown) : 0f;

        void Awake() => player = GetComponent<Player>();

        public void Bind(BombController bombController)
        {
            Unbind();
            bomb = bombController;
            bomb.BombCaught += OnBombCaught;
        }

        void OnDestroy() => Unbind();

        void Unbind()
        {
            if (bomb != null) bomb.BombCaught -= OnBombCaught;
        }

        // A successful catch spends the window and frees the button.
        void OnBombCaught(Player receiver)
        {
            if (receiver == player) Clear();
        }

        void Update()
        {
            if (NetMode.IsAuthority)
            {
                CheckMissedEarly();   // the host judges timing, for every player
                FlushDeferredHint();
            }

            if (!player.IsLocal) return;

            // The local window is instant feedback (and the whole story offline); the host runs its own
            // window for this player and is the one that decides whether a bomb is caught.
            bool pressed = player.Input.CatchPressed;   // always consume the input
            if (pressed && TryOpenWindow() && NetMode.IsRemoteClient) player.Net.RequestCatch();

            if (NetMode.IsRemoteClient) ClaimIfInReach();
        }

        // What this machine sees is the bomb as it was a moment ago on the host: claim the catch we saw.
        // Same swept reach and late grace as the host's CatchResolver, applied to the flight as this machine renders it.
        void ClaimIfInReach()
        {
            if (bomb == null || bomb.State != BombState.Thrown || bomb.LastThrower == player)
            {
                seenValid = false;
                return;
            }

            Vector3 seen = bomb.transform.position;
            Vector3 from = seenValid ? lastSeen : seen;
            lastSeen = seen;
            seenValid = true;

            float d = CatchResolver.ReachDistance(player.Tuning, from, seen, player.CatchVolume.CatchCenter, out Vector3 closest);
            if (d <= CatchResolver.ReachFor(player, seen - from)) seenInReachAt = Time.time;

            if (claimSent || !WindowOpen || Time.time - seenInReachAt > player.Tuning.catchLateGrace) return;
            claimSent = true;
            player.Net.ClaimCatch();
        }

        /// <summary>Opens a catch window if allowed. Also called on the host when a remote player asks.</summary>
        public bool TryOpenWindow()
        {
            if (bomb == null || player.ControlLocked) return false;
            if (bomb.Carrier == player) return false;    // you cannot catch what you are already holding
            if (WindowOpen || Time.time < cooldownEnd) return false;

            DiagnoseLatePress();

            var t = player.Tuning;
            windowEnd = Time.time + t.catchWindowDuration;
            cooldownEnd = windowEnd + t.catchCooldown;
            claimSent = false;
            PatataLog.Bomb($"Catch window open {player} ({t.catchWindowDuration:F2}s)");
            return true;
        }

        public void Clear()
        {
            windowEnd = -1f;
            cooldownEnd = -1f;
            windowWasOpen = false;
            deferredHint = null;
        }

        /// <summary>The bomb touched this player's catch volume (whether or not they were ready). Host only.</summary>
        public void NoteBombInReach() => lastReachTime = Time.time;

        /// <summary>A hint decided on the host, delivered to this player's own machine.</summary>
        public void ReceiveHint(string text)
        {
            hint = text;
            hintUntil = Time.time + HintDuration;
        }

        bool IsRemotelyOwned => player.Net != null && player.Net.IsSpawned && !player.Net.IsOwner;

        void SayHint(string text)
        {
            float delay = player.Tuning.catchLagCompensation;
            if (IsRemotelyOwned && delay > 0f)
            {
                // Their lag-compensated catch for this flight may still arrive: explain the miss only if it does not.
                deferredHint = text;
                deferredHintAt = Time.time + delay;
                return;
            }
            DeliverHint(text);
        }

        void FlushDeferredHint()
        {
            if (deferredHint == null || Time.time < deferredHintAt) return;
            string text = deferredHint;
            deferredHint = null;
            DeliverHint(text);
        }

        void DeliverHint(string text)
        {
            PatataLog.Bomb($"Catch hint for {player}: {text} (rtt {NetMode.RttMs} ms)");
            if (IsRemotelyOwned) player.Net.SendHint(text);
            else ReceiveHint(text);
        }

        // The press arrived after the bomb had already been in reach and left: too late.
        void DiagnoseLatePress()
        {
            if (bomb.State != BombState.Thrown || bomb.LastThrower == player) return;
            float since = Time.time - lastReachTime;
            Vector3 at = bomb.transform.position;
            bool inReachNow = Vector3.Distance(at, player.CatchVolume.CatchCenter) <= CatchResolver.ReachFor(player, bomb.Body.Velocity) + ReachSlack;
            // Within the late grace the press still catches (CatchResolver), so only later presses are "too late".
            if (!inReachNow && since > player.Tuning.catchLateGrace && since < LateMax) SayHint($"Too late: it was in reach {since * 1000f:F0} ms ago");
        }

        // The window closed while the bomb was still coming: too early.
        void CheckMissedEarly()
        {
            bool open = WindowOpen;
            bool justClosed = windowWasOpen && !open;
            windowWasOpen = open;
            if (!justClosed || bomb == null || bomb.State != BombState.Thrown || bomb.LastThrower == player) return;

            Vector3 toMe = player.CatchVolume.CatchCenter - bomb.transform.position;
            if (toMe.magnitude < EarlyCheckDistance && Vector3.Dot(bomb.Body.Velocity, toMe) > 0f) SayHint("Too early: it was still on its way");
        }
    }
}
