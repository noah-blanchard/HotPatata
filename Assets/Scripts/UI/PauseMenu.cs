using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// The pause menu (#16, ARCHITECTURE §6.2): Resume, Settings, Leave to menu. Opened and closed by the Pause action
    /// (Esc, gamepad Start; <see cref="PlayerPause"/>), closed by Back too. While it is open the local player's gameplay
    /// input reads neutral (<see cref="PlayerInputReader.Blocked"/>) and the cursor is free. Offline the game freezes
    /// (<c>Time.timeScale</c> 0, settings screen included); online nothing pauses for anyone, so a carrier who opens it
    /// keeps the bomb while its fuse burns, as the rules say.
    /// </summary>
    public class PauseScreen : UIScreen
    {
        readonly GameTuning tuning;

        public PauseScreen(GameTuning tuning) => this.tuning = tuning;

        public Button Resume { get; private set; }
        public Button SettingsButton { get; private set; }
        public Button LeaveButton { get; private set; }

        protected override VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("hp-overlay");
            var panel = new VisualElement();
            panel.AddToClassList("hp-panel");
            root.Add(panel);
            var title = new Label(NetMode.IsNetworked ? "Menu" : "Paused");
            title.AddToClassList("hp-title");
            panel.Add(title);
            var hint = new Label(NetMode.IsNetworked
                ? "The game keeps running for everyone. If you hold the bomb, its fuse still burns."
                : "The game is paused.");
            hint.AddToClassList("hp-hint");
            panel.Add(hint);

            Resume = Navigable(new Button(() => Stack.Pop()) { text = "Resume" });
            Resume.AddToClassList("hp-button");
            Resume.AddToClassList(FirstFocusClass);
            panel.Add(Resume);
            SettingsButton = Navigable(new Button(() => Stack.Push(new SettingsScreen(tuning))) { text = "Settings" });
            SettingsButton.AddToClassList("hp-button");
            panel.Add(SettingsButton);
            LeaveButton = Navigable(new Button(() => Stack.Push(new ConfirmScreen("Leave the run?",
                NetMode.IsNetworked ? "You leave the game; the others carry on without you." : "Your progress in this run is lost.",
                "Leave to menu", PauseMenu.LeaveToMenu))) { text = "Leave to menu" });
            LeaveButton.AddToClassList("hp-button");
            LeaveButton.AddToClassList("hp-button--secondary");
            panel.Add(LeaveButton);
            return root;
        }
    }

    /// <summary>Opens and closes the <see cref="PauseScreen"/>, freezes the offline game while it is open, and leaves the run.</summary>
    public static class PauseMenu
    {
        static ScreenStack watched;

        public static bool IsOpen => ScreenStack.Existing != null && ScreenStack.Existing.Has<PauseScreen>();

        /// <summary>The game is frozen: offline only, while the pause menu (or a screen above it) is open.</summary>
        public static bool Frozen => IsOpen && !NetMode.IsNetworked;

        /// <summary>
        /// The Pause action: opens the menu when no screen is open, closes it when it is on top. Any other screen on top
        /// (settings, a question) is left to its own Back. Ignored on a frame where a screen already opened or closed, so
        /// Esc (which is also the UI's Back) never closes and reopens the menu at once.
        /// </summary>
        public static void Toggle(GameTuning tuning)
        {
            var stack = ScreenStack.Get();
            if (stack.LastChangeFrame == Time.frameCount) return;
            if (stack.Count == 0) Open(tuning);
            else if (stack.Top is PauseScreen) stack.Pop();
        }

        public static PauseScreen Open(GameTuning tuning)
        {
            var stack = ScreenStack.Get();
            Watch(stack);
            var screen = new PauseScreen(tuning);
            stack.Push(screen);
            return screen;
        }

        /// <summary>Leaves the run: through the session flow when there is one (never a raw NetworkManager shutdown), else back to the entry scene.</summary>
        public static void LeaveToMenu()
        {
            ScreenStack.Existing?.Clear();
            Time.timeScale = 1f;
            if (NetworkBootstrap.Instance != null) _ = NetworkBootstrap.Instance.LeaveAsync(null);
            else SceneManager.LoadScene(0);   // a course played directly in the Editor: Bootstrap is the first scene of the build
        }

        static void Watch(ScreenStack stack)
        {
            if (watched == stack) return;
            if (watched != null) watched.Changed -= ApplyFreeze;
            watched = stack;
            stack.Changed += ApplyFreeze;
        }

        static void ApplyFreeze() => Time.timeScale = Frozen ? 0f : 1f;
    }
}
