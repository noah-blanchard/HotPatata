using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// Development only (#14): a sample UI Toolkit screen to check the shared theme, the <see cref="ScreenStack"/> and
    /// navigation by mouse, keyboard and gamepad. F6 or gamepad Select opens it in any scene; Back (Esc / B) or Close
    /// shuts it; "Open another" stacks a second one to check that focus comes back.
    /// </summary>
    public class UISampleScreen : UIScreen
    {
        readonly int depth;
        int presses;

        public UISampleScreen(int depth = 1) => this.depth = depth;

        public Label Counter { get; private set; }

        protected override VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("hp-overlay");
            var panel = new VisualElement();
            panel.AddToClassList("hp-panel");
            root.Add(panel);

            var title = new Label(depth == 1 ? "UI sample" : $"UI sample ({depth})");
            title.AddToClassList("hp-title");
            panel.Add(title);
            var hint = new Label("Mouse, arrows / WASD or stick / d-pad to move, Enter or A to press, Esc or B to go back.");
            hint.AddToClassList("hp-hint");
            panel.Add(hint);

            var press = new Button(() => Counter.text = $"Pressed {++presses}") { text = "Press me" };
            press.AddToClassList("hp-button");
            press.AddToClassList(FirstFocusClass);
            panel.Add(press);
            Counter = new Label("Pressed 0");
            Counter.AddToClassList("hp-hint");
            panel.Add(Counter);

            var toggle = new Toggle("A toggle");
            toggle.AddToClassList("hp-toggle");
            panel.Add(toggle);
            var slider = new Slider("A slider", 0f, 1f) { value = 0.5f };
            slider.AddToClassList("hp-slider");
            panel.Add(slider);

            var another = new Button(() => Stack.Push(new UISampleScreen(depth + 1))) { text = "Open another" };
            another.AddToClassList("hp-button");
            panel.Add(another);
            var close = new Button(() => Stack.Pop()) { text = "Close" };
            close.AddToClassList("hp-button");
            close.AddToClassList("hp-button--secondary");
            panel.Add(close);
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
