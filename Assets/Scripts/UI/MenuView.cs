using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// The UI Toolkit view of <see cref="NetworkBootstrap"/> (#15, ARCHITECTURE §6.2): it shows the screen that matches
    /// the bootstrap's phase on the <see cref="ScreenStack"/> (Menu: <see cref="MainMenuScreen"/>, Working:
    /// <see cref="WorkingScreen"/>, Lobby: <see cref="LobbyScreen"/>, InGame: none, the level owns the screen) and the
    /// screens call the bootstrap's public methods. It holds no game logic. Added by <see cref="NetworkBootstrap"/>
    /// outside batch mode, so headless bot runs have no UI.
    /// </summary>
    public class MenuView : MonoBehaviour
    {
        NetworkBootstrap bootstrap;
        NetworkBootstrap.Mode shownPhase;
        bool shownLobby, shown;

        void Awake() => bootstrap = GetComponent<NetworkBootstrap>();

        void Update()
        {
            var phase = bootstrap.Phase;
            bool lobby = bootstrap.HasLobby;
            if (shown && phase == shownPhase && lobby == shownLobby) return;
            shown = true;
            shownPhase = phase;
            shownLobby = lobby;

            var stack = ScreenStack.Get();
            stack.Clear();   // a new phase replaces the menu screens (and any pause menu left from the level)
            switch (phase)
            {
                case NetworkBootstrap.Mode.Menu: stack.Push(new MainMenuScreen(bootstrap)); break;
                case NetworkBootstrap.Mode.Working: stack.Push(new WorkingScreen(bootstrap)); break;
                case NetworkBootstrap.Mode.Lobby: stack.Push(lobby ? new LobbyScreen(bootstrap) : (UIScreen)new WorkingScreen(bootstrap)); break;
            }
        }
    }

    /// <summary>Shared pieces of the menu screens.</summary>
    static class MenuParts
    {
        public const int RefreshMs = 200;   // how often a screen re-reads the bootstrap's state (messages, lobby list)

        public static VisualElement Panel(VisualElement root, string title)
        {
            root.AddToClassList("hp-overlay");
            root.AddToClassList("hp-overlay--menu");
            var panel = new VisualElement();
            panel.AddToClassList("hp-panel");
            panel.AddToClassList("hp-panel--menu");
            root.Add(panel);
            var heading = new Label(title);
            heading.AddToClassList("hp-title");
            panel.Add(heading);
            return panel;
        }

        public static Button Button(VisualElement parent, string text, System.Action onClick, string extraClass = null)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("hp-button");
            if (extraClass != null) button.AddToClassList(extraClass);
            parent.Add(button);
            return button;
        }

        public static TextField Field(VisualElement parent, string label, string value, int maxLength)
        {
            var field = new TextField(label) { maxLength = maxLength, isDelayed = false };
            field.SetValueWithoutNotify(value ?? "");
            field.AddToClassList("hp-field");
            parent.Add(field);
            return field;
        }

        public static Label Hint(VisualElement parent, string text, string extraClass = null)
        {
            var label = new Label(text);
            label.AddToClassList("hp-hint");
            if (extraClass != null) label.AddToClassList(extraClass);
            parent.Add(label);
            return label;
        }

        /// <summary>The level and spawn-point choices (main menu and the host's lobby); the spawn list follows the level.</summary>
        public static void LevelChoices(VisualElement parent, NetworkBootstrap bootstrap, out ChoiceRow level, out ChoiceRow spawn)
        {
            var spawnRow = new ChoiceRow("Spawn at", NetworkBootstrap.SpawnLabels(bootstrap.SceneCheckpointCount), RunOptions.StartCheckpoint);
            var levelRow = new ChoiceRow("Level", bootstrap.GameplayScenes.ToList(), bootstrap.SceneIndex);
            levelRow.Changed += i =>
            {
                bootstrap.SceneIndex = i;
                spawnRow.SetOptions(NetworkBootstrap.SpawnLabels(bootstrap.SceneCheckpointCount), RunOptions.StartCheckpoint);
                spawnRow.style.display = bootstrap.SceneCheckpointCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            };
            spawnRow.Changed += i => RunOptions.StartCheckpoint = i;
            spawnRow.style.display = bootstrap.SceneCheckpointCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            parent.Add(levelRow);
            parent.Add(spawnRow);
            level = levelRow;
            spawn = spawnRow;
        }
    }

    /// <summary>
    /// The main menu (parity with the old IMGUI one): your name, level and spawn point, Host Online (get a game code),
    /// Join with code, Play Local, Settings, and the direct connection (LAN / testing) behind a toggle. Errors from the
    /// session flow show under it. It is the root: Back does nothing.
    /// </summary>
    public class MainMenuScreen : UIScreen
    {
        readonly NetworkBootstrap bootstrap;
        Label error;

        public MainMenuScreen(NetworkBootstrap bootstrap) => this.bootstrap = bootstrap;

        public override bool CanGoBack => false;

        public TextField NameField { get; private set; }
        public TextField CodeField { get; private set; }
        public Label Error => error;

        protected override VisualElement Build()
        {
            var root = new VisualElement();
            var panel = MenuParts.Panel(root, "HotPatata");

            NameField = Navigable(MenuParts.Field(panel, "Your name", PlayerNames.Local, PlayerNames.MaxLength));
            NameField.RegisterValueChangedCallback(e => PlayerNames.Local = e.newValue);   // saved here; the host cleans it up when shared

            MenuParts.LevelChoices(panel, bootstrap, out var level, out var spawn);
            Navigable(level);
            Navigable(spawn);

            var host = Navigable(MenuParts.Button(panel, "Host Online  (get a game code)", () => _ = bootstrap.HostOnlineAsync()));
            host.AddToClassList(FirstFocusClass);
            CodeField = Navigable(MenuParts.Field(panel, "Game code", "", SessionService.CodeLength));
            CodeField.RegisterValueChangedCallback(e =>
            {
                string normalized = SessionService.NormalizeCode(e.newValue);
                if (normalized != e.newValue) CodeField.SetValueWithoutNotify(normalized);
            });
            Navigable(MenuParts.Button(panel, "Join with code", () => _ = bootstrap.JoinCodeAsync(CodeField.value)));
            Navigable(MenuParts.Button(panel, "Play Local  (2 players, one keyboard)", bootstrap.PlayLocal));
            Navigable(MenuParts.Button(panel, "Settings", () => Stack.Push(new SettingsScreen(bootstrap.Tuning)), "hp-button--secondary"));

            var direct = new VisualElement();
            direct.AddToClassList("hp-group");
            var toggle = Navigable(new ToggleRow("Direct connection (LAN / testing)", false));
            panel.Add(toggle);
            panel.Add(direct);
            var ip = Navigable(MenuParts.Field(direct, "Address", bootstrap.DirectAddress, 64));
            ip.RegisterValueChangedCallback(e => bootstrap.DirectAddress = e.newValue.Trim());
            Navigable(MenuParts.Button(direct, "Join IP", () => bootstrap.StartClientDirect(bootstrap.DirectAddress), "hp-button--secondary"));
            Navigable(MenuParts.Button(direct, "Host (direct)", bootstrap.StartHostDirect, "hp-button--secondary"));
            direct.style.display = DisplayStyle.None;
            toggle.Changed += on => direct.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

            error = MenuParts.Hint(panel, "", "hp-error");
            root.schedule.Execute(Refresh).Every(MenuParts.RefreshMs);
            Refresh();
            return root;
        }

        void Refresh()
        {
            string message = bootstrap.LastMessage;
            error.text = message ?? "";
            error.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }

    /// <summary>"Creating your game...", "Joining ABC123...", "Connecting...": the session flow is busy. Nothing to press.</summary>
    public class WorkingScreen : UIScreen
    {
        readonly NetworkBootstrap bootstrap;
        Label status;

        public WorkingScreen(NetworkBootstrap bootstrap) => this.bootstrap = bootstrap;

        public override bool CanGoBack => false;

        protected override VisualElement Build()
        {
            var root = new VisualElement();
            var panel = MenuParts.Panel(root, "HotPatata");
            status = MenuParts.Hint(panel, "");
            root.schedule.Execute(Refresh).Every(MenuParts.RefreshMs);
            Refresh();
            return root;
        }

        void Refresh() => status.text = string.IsNullOrEmpty(bootstrap.Status) ? "Connecting..." : bootstrap.Status;
    }

    /// <summary>
    /// The online lobby (parity with the old IMGUI one): the game code (Copy code), the player list with each slot's
    /// colour and shape and the host / you tags, then for the host the level, spawn point and Start, for the others a
    /// waiting line, and Leave. Back asks before leaving.
    /// </summary>
    public class LobbyScreen : UIScreen
    {
        readonly NetworkBootstrap bootstrap;
        Label playersTitle;
        VisualElement playerList;
        string listed;

        public LobbyScreen(NetworkBootstrap bootstrap) => this.bootstrap = bootstrap;

        protected override VisualElement Build()
        {
            var root = new VisualElement();
            var panel = MenuParts.Panel(root, "Lobby");

            MenuParts.Hint(panel, "Game code");
            var code = new Label(bootstrap.LobbyCode ?? "");
            code.AddToClassList("hp-code");
            panel.Add(code);
            Navigable(MenuParts.Button(panel, "Copy code", () => GUIUtility.systemCopyBuffer = bootstrap.LobbyCode ?? "", "hp-button--secondary"));

            playersTitle = new Label();
            playersTitle.AddToClassList("hp-section");
            panel.Add(playersTitle);
            playerList = new VisualElement();
            playerList.AddToClassList("hp-group");
            panel.Add(playerList);

            if (bootstrap.IsLobbyHost)
            {
                MenuParts.LevelChoices(panel, bootstrap, out var level, out var spawn);
                Navigable(level);
                Navigable(spawn);
                var start = Navigable(MenuParts.Button(panel, "Start", bootstrap.StartLevel));
                start.AddToClassList(FirstFocusClass);
            }
            else
            {
                MenuParts.Hint(panel, "Waiting for the host to start...");
            }
            Navigable(MenuParts.Button(panel, "Leave", () => _ = bootstrap.LeaveAsync(null), "hp-button--secondary"));

            root.schedule.Execute(Refresh).Every(MenuParts.RefreshMs);
            Refresh();
            return root;
        }

        public override void OnBack() =>
            Stack.Push(new ConfirmScreen("Leave the lobby?", "You leave this game and go back to the menu.", "Leave", () => _ = bootstrap.LeaveAsync(null)));

        /// <summary>The player list, rebuilt only when it changed (join, leave, name).</summary>
        void Refresh()
        {
            var players = bootstrap.LobbyPlayers();
            string signature = string.Join("|", players.Select(p => p.Label + p.IsHost + p.IsYou));
            if (signature == listed) return;
            listed = signature;
            playersTitle.text = $"Players ({players.Count}/{bootstrap.LobbyMaxPlayers})";
            playerList.Clear();
            foreach (var p in players)
            {
                string tags = (p.IsHost ? "  (host)" : "") + (p.IsYou ? "  (you)" : "");
                var line = new Label(p.Label + tags) { enableRichText = true };
                line.AddToClassList("hp-player");
                playerList.Add(line);
            }
        }
    }
}
