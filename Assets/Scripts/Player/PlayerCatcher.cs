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
        }
    }
}
