using UnityEngine;
using UnityEngine.InputSystem;

namespace Beep
{
    /// <summary>
    /// Local pass-sandbox test rig: a single keyboard/mouse drives ONE player at a time and the camera
    /// follows that player. Tab hands control to the next player, so one person can throw to the other
    /// player, switch, and throw back. Esc releases the mouse cursor. Not part of the shipped game.
    /// </summary>
    public class LocalPlayerSwitcher : MonoBehaviour
    {
        [SerializeField] Player[] players;
        [SerializeField] ThirdPersonCamera cam;
        [SerializeField] bool lockCursor = true;

        /// <summary>Automated tests set this so no real keyboard/mouse is attached to a player.</summary>
        public static bool SuppressAutoFocus;

        int focused = -1;

        public Player Focused => focused >= 0 ? players[focused] : null;

        void Start()
        {
            if (SuppressAutoFocus) return;
            Focus(0);
            SetCursor(lockCursor);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || SuppressAutoFocus) return;

            if (kb.tabKey.wasPressedThisFrame) Focus((focused + 1) % players.Length);
            if (kb.escapeKey.wasPressedThisFrame) SetCursor(Cursor.lockState != CursorLockMode.Locked);
        }

        public void Focus(int index)
        {
            if (players == null || players.Length == 0) return;
            focused = Mathf.Clamp(index, 0, players.Length - 1);

            for (int i = 0; i < players.Length; i++)
                players[i].Input.SetSource(i == focused ? InputSource.KeyboardMouse : InputSource.None);

            if (cam != null) cam.Target = players[focused];
        }

        static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
