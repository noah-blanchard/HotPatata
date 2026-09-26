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

        public InputSource Source { get; private set; }
        public bool Active => Source != InputSource.None;

        public Vector2 Move => Active ? move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => Active ? look.ReadValue<Vector2>() : Vector2.zero;
        /// <summary>Mouse look is a per-frame delta; stick look is a rate. Callers scale them differently.</summary>
        public bool LookIsMouse => Source == InputSource.KeyboardMouse;
        public bool JumpPressed => Active && jump.WasPressedThisFrame();
        public bool ThrowPressed => Active && throwAction.WasPressedThisFrame();

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
            actions.Enable();
            SetSource(initialSource, gamepadIndex);
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
