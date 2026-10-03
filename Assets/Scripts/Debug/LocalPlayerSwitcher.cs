using UnityEngine;
using UnityEngine.InputSystem;

namespace HotPatata
{
    /// <summary>
    /// Offline pass-sandbox test rig: a single keyboard/mouse drives ONE player at a time and the camera
    /// follows that player. Tab hands control to the next player, so one person can throw to the other
    /// player, switch, and throw back. It asks <see cref="CursorPolicy"/> for a locked cursor; Esc opens the pause menu
    /// (<see cref="PauseMenu"/>), which frees the cursor while it is open. Not part of the shipped game.
    /// Online, each machine controls only its own player, so this only locks the cursor.
    /// </summary>
    [DefaultExecutionOrder(50)]   // after the spawner has built the local rig
    public class LocalPlayerSwitcher : MonoBehaviour
    {
        [SerializeField] FirstPersonCamera cam;
        [SerializeField] bool lockCursor = true;

        /// <summary>Automated tests set this so no real keyboard/mouse is attached to a player.</summary>
        public static bool SuppressAutoFocus;

        int focused = -1;

        /// <summary>The player the camera (and, offline, the keyboard) currently belongs to.</summary>
        public Player Focused => cam != null ? cam.Target : null;

        void Start()
        {
            if (SuppressAutoFocus || NetMode.IsNetworked) return;
            Focus(0);
            CursorPolicy.SetGameplayLock(lockCursor);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || SuppressAutoFocus) return;

            if (!NetMode.IsNetworked && kb.tabKey.wasPressedThisFrame && Player.All.Count > 0)
                Focus((focused + 1) % Player.All.Count);
        }

        public void Focus(int index)
        {
            var players = Player.All;
            if (players.Count == 0) return;
            focused = Mathf.Clamp(index, 0, players.Count - 1);

            for (int i = 0; i < players.Count; i++)
                players[i].Input.SetSource(i == focused ? InputSource.KeyboardMouse : InputSource.None);

            if (cam != null) cam.Target = players[focused];
        }
    }
}
