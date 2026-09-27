using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Catching is a timed action, never automatic. Pressing catch opens a short window
    /// (<see cref="GameTuning.catchWindowDuration"/>); a bomb only counts as caught if it reaches this
    /// player's catch volume while the window is open. After the window closes there is a cooldown
    /// (<see cref="GameTuning.catchCooldown"/>) so the button cannot be mashed. The bomb's
    /// <see cref="CatchResolver"/> reads <see cref="WindowOpen"/>; this class never touches the bomb.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerCatcher : MonoBehaviour
    {
        Player player;
        BombController bomb;
        float windowEnd = -1f;
        float cooldownEnd = -1f;
        float lastReachTime = -10f;
        bool windowWasOpen;
        string hint;
        float hintUntil;

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
            bomb = bombController;
            bomb.BombCaught += receiver =>
            {
                if (receiver == player) Clear();   // a successful catch spends the window and frees the button
            };
        }

        void Update()
        {
            if (NetMode.IsAuthority) CheckMissedEarly();   // the host judges timing, for every player

            if (!player.IsLocal) return;

            bool pressed = player.Input.CatchPressed;   // always consume the input
            if (!pressed) return;

            // The local window is instant feedback (and the whole story offline); the host runs its own
            // window for this player and is the one that decides whether a bomb is caught.
            if (TryOpenWindow() && NetMode.IsRemoteClient) player.Net.RequestCatch();
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
            BeepLog.Bomb($"Catch window open {player} ({t.catchWindowDuration:F2}s)");
            return true;
        }

        public void Clear()
        {
            windowEnd = -1f;
            cooldownEnd = -1f;
            windowWasOpen = false;
        }

        /// <summary>The bomb touched this player's catch volume (whether or not they were ready). Host only.</summary>
        public void NoteBombInReach() => lastReachTime = Time.time;

        /// <summary>A hint decided on the host, delivered to this player's own machine.</summary>
        public void ReceiveHint(string text)
        {
            hint = text;
            hintUntil = Time.time + 1.6f;
        }

        void SayHint(string text)
        {
            BeepLog.Bomb($"Catch hint for {player}: {text} (rtt {NetMode.RttMs} ms)");
            if (player.Net != null && player.Net.IsSpawned && !player.Net.IsOwner) player.Net.SendHint(text);
            else ReceiveHint(text);
        }

        // The press arrived after the bomb had already been in reach and left: too late.
        void DiagnoseLatePress()
        {
            if (bomb.State != BombState.Thrown || bomb.LastThrower == player) return;
            float since = Time.time - lastReachTime;
            bool inReachNow = Vector3.Distance(bomb.transform.position, player.CatchVolume.CatchCenter) <= player.Tuning.catchRadius + 0.25f;
            if (!inReachNow && since > 0.03f && since < 0.8f) SayHint($"Too late: it was in reach {since * 1000f:F0} ms ago");
        }

        // The window closed while the bomb was still coming: too early.
        void CheckMissedEarly()
        {
            bool open = WindowOpen;
            bool justClosed = windowWasOpen && !open;
            windowWasOpen = open;
            if (!justClosed || bomb == null || bomb.State != BombState.Thrown || bomb.LastThrower == player) return;

            Vector3 toMe = player.CatchVolume.CatchCenter - bomb.transform.position;
            if (toMe.magnitude < 12f && Vector3.Dot(bomb.Body.Velocity, toMe) > 0f) SayHint("Too early: it was still on its way");
        }
    }
}
