using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The one place that sets the mouse cursor (ARCHITECTURE §6.2). Gameplay asks for a locked cursor (a local player
    /// is looking around); an open UI screen that needs the mouse wins over it. Nothing else writes
    /// <see cref="Cursor.lockState"/>, so the gameplay rig and the menus never fight.
    /// </summary>
    public static class CursorPolicy
    {
        /// <summary>Gameplay wants the cursor locked (a local player is in control).</summary>
        public static bool GameplayWantsLock { get; private set; }

        /// <summary>A UI screen that needs the mouse is open.</summary>
        public static bool ScreenWantsCursor { get; private set; }

        public static bool IsLocked => Resolve(GameplayWantsLock, ScreenWantsCursor);

        public static void SetGameplayLock(bool locked)
        {
            GameplayWantsLock = locked;
            Apply();
        }

        public static void SetScreenWantsCursor(bool wants)
        {
            ScreenWantsCursor = wants;
            Apply();
        }

        /// <summary>The rule: locked only while gameplay wants it and no screen needs the mouse.</summary>
        public static bool Resolve(bool gameplayWantsLock, bool screenWantsCursor) => gameplayWantsLock && !screenWantsCursor;

        static void Apply()
        {
            bool locked = IsLocked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
