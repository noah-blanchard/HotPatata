using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Beep
{
    public enum InputSource
    {
        None,
        KeyboardMouse,
        Gamepad
    }

    /// <summary>
    /// Reads one player's input from a private clone of the BeepControls asset, restricted to the
    /// devices of the selected <see cref="InputSource"/>. Several players can therefore coexist in
    /// one scene, each fed by a different device (or by none).
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actionsAsset;
        [SerializeField] InputSource initialSource = InputSource.None;
        [SerializeField] int gamepadIndex;

        InputActionAsset actions;
        InputAction move, look, jump, throwAction;

        /// <summary>
        /// Programmatic input that completely replaces device input while assigned. Used by automated
        /// tests (and usable for scripted dummies); the game never sets it.
        /// </summary>
        public sealed class ScriptedInput
        {
            public Vector2 Move;
            /// <summary>Look rate in "stick" units (-1..1), like a gamepad stick.</summary>
            public Vector2 Look;
            bool jumpQueued, throwQueued;

            public void PressJump() => jumpQueued = true;
            public void PressThrow() => throwQueued = true;

            internal bool ConsumeJump() { bool v = jumpQueued; jumpQueued = false; return v; }
            internal bool ConsumeThrow() { bool v = throwQueued; throwQueued = false; return v; }
        }

        public ScriptedInput Scripted { get; set; }

        public InputSource Source { get; private set; }
        public bool Active => Scripted != null || Source != InputSource.None;

        public Vector2 Move => Scripted != null ? Scripted.Move : Source != InputSource.None ? move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => Scripted != null ? Scripted.Look : Source != InputSource.None ? look.ReadValue<Vector2>() : Vector2.zero;
        /// <summary>Mouse look is a per-frame delta; stick look is a rate. Callers scale them differently.</summary>
        public bool LookIsMouse => Scripted == null && Source == InputSource.KeyboardMouse;
        public bool JumpPressed => Scripted != null ? Scripted.ConsumeJump() : Source != InputSource.None && jump.WasPressedThisFrame();
        public bool ThrowPressed => Scripted != null ? Scripted.ConsumeThrow() : Source != InputSource.None && throwAction.WasPressedThisFrame();

        void Awake()
        {
            actions = Instantiate(actionsAsset);
            var map = actions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            jump = map.FindAction("Jump", true);
            throwAction = map.FindAction("Throw", true);
        }

        void OnEnable()
        {
            SetSource(initialSource, gamepadIndex);   // restrict devices first, so nothing resolves against all of them
            actions.Enable();
        }

        void OnDisable() => actions.Disable();

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
                    actions.devices = new ReadOnlyArray<InputDevice>(new InputDevice[] { Keyboard.current, Mouse.current });
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
