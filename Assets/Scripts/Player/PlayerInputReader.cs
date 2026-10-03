using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace HotPatata
{
    public enum InputSource
    {
        None,
        KeyboardMouse,
        Gamepad
    }

    /// <summary>
    /// Reads one player's input from a private clone of the HotPatataControls asset, restricted to the
    /// devices of the selected <see cref="InputSource"/>. Several players can therefore coexist in
    /// one scene, each fed by a different device (or by none). The clone carries the player's saved key
    /// bindings (<see cref="InputRebinding"/>) and picks up new ones when the settings are saved.
    /// While a menu is on top (<see cref="ScreenStack.BlocksGameplay"/>) every gameplay value reads as neutral (no move,
    /// no look, no presses), scripted input included; only Pause still reads, so Start can close the pause menu.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actionsAsset;
        [SerializeField] InputSource initialSource = InputSource.None;
        [SerializeField] int gamepadIndex;

        InputActionAsset actions;
        InputAction move, look, jump, throwAction, catchAction, sprint, crouch, pause;

        /// <summary>
        /// Programmatic input that completely replaces device input while assigned. Used by automated
        /// tests (and usable for scripted dummies); the game never sets it.
        /// </summary>
        public sealed class ScriptedInput
        {
            public Vector2 Move;
            /// <summary>Look rate in "stick" units (-1..1), like a gamepad stick.</summary>
            public Vector2 Look;
            /// <summary>Held buttons: set true to hold, false to release.</summary>
            public bool Sprint, Crouch;
            public bool ThrowHeld { get; private set; }
            bool jumpQueued, throwPressQueued, throwReleaseQueued, catchQueued;

            public void PressJump() => jumpQueued = true;
            public void PressCatch() => catchQueued = true;

            /// <summary>Hold or release the throw button (queues the matching press/release edge).</summary>
            public void SetThrowHeld(bool held)
            {
                if (held && !ThrowHeld) throwPressQueued = true;
                if (!held && ThrowHeld) throwReleaseQueued = true;
                ThrowHeld = held;
            }

            /// <summary>A tap: press and release in the same frame (an uncharged throw).</summary>
            public void PressThrow()
            {
                SetThrowHeld(true);
                SetThrowHeld(false);
            }

            internal bool ConsumeJump() { bool v = jumpQueued; jumpQueued = false; return v; }
            internal bool ConsumeThrowPress() { bool v = throwPressQueued; throwPressQueued = false; return v; }
            internal bool ConsumeThrowRelease() { bool v = throwReleaseQueued; throwReleaseQueued = false; return v; }
            internal bool ConsumeCatch() { bool v = catchQueued; catchQueued = false; return v; }
        }

        public ScriptedInput Scripted { get; set; }

        public InputSource Source { get; private set; }
        public bool Active => Scripted != null || Source != InputSource.None;

        /// <summary>A menu has the local player's gameplay input (the pause menu, settings): every gameplay value reads neutral.</summary>
        public bool Blocked => ScreenStack.BlocksGameplay;

        // Edges are always consumed (so nothing fires late when the menu closes), then dropped while blocked.
        public Vector2 Move => Blocked ? Vector2.zero : Scripted != null ? Scripted.Move : Source != InputSource.None ? move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => Blocked ? Vector2.zero : Scripted != null ? Scripted.Look : Source != InputSource.None ? look.ReadValue<Vector2>() : Vector2.zero;
        /// <summary>Mouse look is a per-frame delta; stick look is a rate. Callers scale them differently.</summary>
        public bool LookIsMouse => Scripted == null && Source == InputSource.KeyboardMouse;
        public bool JumpPressed => (Scripted != null ? Scripted.ConsumeJump() : Source != InputSource.None && jump.WasPressedThisFrame()) && !Blocked;
        public bool ThrowPressed => (Scripted != null ? Scripted.ConsumeThrowPress() : Source != InputSource.None && throwAction.WasPressedThisFrame()) && !Blocked;
        public bool ThrowReleased => (Scripted != null ? Scripted.ConsumeThrowRelease() : Source != InputSource.None && throwAction.WasReleasedThisFrame()) && !Blocked;
        public bool CatchPressed => (Scripted != null ? Scripted.ConsumeCatch() : Source != InputSource.None && catchAction.WasPressedThisFrame()) && !Blocked;
        public bool SprintHeld => !Blocked && (Scripted != null ? Scripted.Sprint : Source != InputSource.None && sprint.IsPressed());
        public bool CrouchHeld => !Blocked && (Scripted != null ? Scripted.Crouch : Source != InputSource.None && crouch.IsPressed());

        /// <summary>Esc / gamepad Start (rebindable): open or close the pause menu. Device input only; read even while blocked.</summary>
        public bool PausePressed => Scripted == null && Source != InputSource.None && pause.WasPressedThisFrame();

        void Awake()
        {
            actions = Instantiate(actionsAsset);
            InputRebinding.ApplySaved(actions);
            var map = actions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            jump = map.FindAction("Jump", true);
            throwAction = map.FindAction("Throw", true);
            catchAction = map.FindAction("Catch", true);
            sprint = map.FindAction("Sprint", true);
            crouch = map.FindAction("Crouch", true);
            pause = map.FindAction("Pause", true);
        }

        void OnEnable()
        {
            SetSource(initialSource, gamepadIndex);   // restrict devices first, so nothing resolves against all of them
            actions.Enable();
            Settings.Changed += OnSettingsChanged;
        }

        void OnDisable()
        {
            Settings.Changed -= OnSettingsChanged;
            actions.Disable();
        }

        void OnSettingsChanged() => InputRebinding.ApplySaved(actions);

        void OnDestroy()
        {
            if (actions != null) Destroy(actions);
        }

        public void SetSource(InputSource source, int padIndex = 0)
        {
            if (source == InputSource.Gamepad && padIndex >= Gamepad.all.Count)
                source = InputSource.None;

            Source = source;
            switch (source)
            {
                case InputSource.KeyboardMouse:
                    // Either device can be missing (no keyboard detected, headless run); never hand the actions a null.
                    var devices = new System.Collections.Generic.List<InputDevice>(2);
                    if (Keyboard.current != null) devices.Add(Keyboard.current);
                    if (Mouse.current != null) devices.Add(Mouse.current);
                    actions.devices = new ReadOnlyArray<InputDevice>(devices.ToArray());
                    break;
                case InputSource.Gamepad:
                    actions.devices = new ReadOnlyArray<InputDevice>(new InputDevice[] { Gamepad.all[padIndex] });
                    break;
                default:
                    actions.devices = new ReadOnlyArray<InputDevice>(new InputDevice[0]);
                    break;
            }
        }
    }
}
