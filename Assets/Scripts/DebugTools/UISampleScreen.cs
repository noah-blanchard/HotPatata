using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// Development only (#14): a sample UI Toolkit screen to check the shared theme, the <see cref="ScreenStack"/> and
    /// navigation by mouse, keyboard and gamepad. F6 or gamepad Select opens it in any scene; Back (Esc / B) or Close
    /// shuts it; "Open another" stacks a second one to check that focus comes back. Layout:
    /// Assets/UI/Screens/UISample.uxml (open it in UI Builder to check the theme there).
    /// </summary>
    public class UISampleScreen : UIScreen
    {
        readonly int depth;
        int presses;

        public UISampleScreen(int depth = 1) => this.depth = depth;

        public Label Counter { get; private set; }

        protected override VisualElement Build()
        {
            var root = FromTemplate(Templates.uiSample);
            if (depth > 1) Require<Label>("title").text = $"UI sample ({depth})";

            var press = Require<Button>("press");
            press.clicked += () => Counter.text = $"Pressed {++presses}";
            press.AddToClassList(FirstFocusClass);
            Counter = Require<Label>("counter");
            Counter.text = "Pressed 0";
            Require<Button>("another").clicked += () => Stack.Push(new UISampleScreen(depth + 1));
            Require<Button>("close").clicked += () => Stack.Pop();
            return root;
        }
    }

    /// <summary>Development only: F6 / gamepad Select toggles the <see cref="UISampleScreen"/> in any scene.</summary>
    public class UISampleHotkey : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("UISampleHotkey");
            DontDestroyOnLoad(go);
            go.AddComponent<UISampleHotkey>();
        }

        void Update()
        {
            bool pressed = (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
                           || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
            if (!pressed) return;
            var stack = ScreenStack.Get();
            if (stack.Top is UISampleScreen)
                while (stack.Top is UISampleScreen) stack.Pop();
            else stack.Push(new UISampleScreen());
        }
    }
}
