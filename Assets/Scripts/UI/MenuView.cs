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
        public const int AddressMaxLength = 64;

        /// <summary>A text field of the layout: its value, its length limit, and typed (not delayed) changes.</summary>
        public static TextField Field(TextField field, string value, int maxLength)
        {
            field.maxLength = maxLength;
            field.isDelayed = false;
            field.SetValueWithoutNotify(value ?? "");
            return field;
        }

        /// <summary>The level and spawn-point choices (main menu and the host's lobby); the spawn list follows the level.</summary>
        public static void LevelChoices(ChoiceRow level, ChoiceRow spawn, NetworkBootstrap bootstrap)
        {
            void ShowSpawn() => spawn.style.display = bootstrap.SceneCheckpointCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            level.SetOptions(bootstrap.GameplayScenes.ToList(), bootstrap.SceneIndex);
            spawn.SetOptions(NetworkBootstrap.SpawnLabels(bootstrap.SceneCheckpointCount), RunOptions.StartCheckpoint);
            level.Changed += i =>
            {
                bootstrap.SceneIndex = i;
                spawn.SetOptions(NetworkBootstrap.SpawnLabels(bootstrap.SceneCheckpointCount), RunOptions.StartCheckpoint);
                ShowSpawn();
            };
            spawn.Changed += i => RunOptions.StartCheckpoint = i;
            ShowSpawn();
        }
    }

    /// <summary>
    /// The main menu (parity with the old IMGUI one; layout Assets/UI/Screens/MainMenu.uxml): your name, level and spawn
    /// point, Host Online (get a game code), Join with code, Play Local, Settings, and the direct connection (LAN /
    /// testing) behind a toggle. Errors from the session flow show under it. It is the root: Back does nothing.
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
            var root = FromTemplate(Templates.mainMenu);

            NameField = Navigable(MenuParts.Field(Require<TextField>("name-field"), PlayerNames.Local, PlayerNames.MaxLength));
            NameField.RegisterValueChangedCallback(e => PlayerNames.Local = e.newValue);   // saved here; the host cleans it up when shared

            var level = Navigable(Require<ChoiceRow>("level"));
            var spawn = Navigable(Require<ChoiceRow>("spawn"));
            MenuParts.LevelChoices(level, spawn, bootstrap);

            var host = Navigable(Require<Button>("host-online"));
            host.clicked += () => _ = bootstrap.HostOnlineAsync();
            host.AddToClassList(FirstFocusClass);
            CodeField = Navigable(MenuParts.Field(Require<TextField>("code-field"), "", SessionService.CodeLength));
            CodeField.RegisterValueChangedCallback(e =>
            {
                string normalized = SessionService.NormalizeCode(e.newValue);
                if (normalized != e.newValue) CodeField.SetValueWithoutNotify(normalized);
            });
            Navigable(Require<Button>("join-code")).clicked += () => _ = bootstrap.JoinCodeAsync(CodeField.value);
            Navigable(Require<Button>("play-local")).clicked += bootstrap.PlayLocal;
            Navigable(Require<Button>("settings")).clicked += () => Stack.Push(new SettingsScreen(bootstrap.Tuning));

            var direct = Require<VisualElement>("direct-group");
            var toggle = Navigable(Require<ToggleRow>("direct-toggle"));
            toggle.SetValueWithoutNotify(false);
            var ip = Navigable(MenuParts.Field(Require<TextField>("direct-address"), bootstrap.DirectAddress, MenuParts.AddressMaxLength));
            ip.RegisterValueChangedCallback(e => bootstrap.DirectAddress = e.newValue.Trim());
            Navigable(Require<Button>("join-ip")).clicked += () => bootstrap.StartClientDirect(bootstrap.DirectAddress);
            Navigable(Require<Button>("host-direct")).clicked += bootstrap.StartHostDirect;
            Show(direct, false);
            toggle.Changed += on => Show(direct, on);

            error = Require<Label>("error");
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
            var root = FromTemplate(Templates.working);
            status = Require<Label>("status");
            root.schedule.Execute(Refresh).Every(MenuParts.RefreshMs);
            Refresh();
            return root;
        }

        void Refresh() => status.text = string.IsNullOrEmpty(bootstrap.Status) ? "Connecting..." : bootstrap.Status;
    }

    /// <summary>
    /// The online lobby (parity with the old IMGUI one; layout Assets/UI/Screens/Lobby.uxml): the game code (Copy code),
    /// the player list with each slot's colour and shape and the host / you tags, then for the host the level, spawn
    /// point and Start, for the others a waiting line, and Leave. Back asks before leaving.
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
            var root = FromTemplate(Templates.lobby);

            Require<Label>("code").text = bootstrap.LobbyCode ?? "";
            Navigable(Require<Button>("copy-code")).clicked += () => GUIUtility.systemCopyBuffer = bootstrap.LobbyCode ?? "";

            playersTitle = Require<Label>("players-title");
            playerList = Require<VisualElement>("player-list");
            playerList.Clear();   // anything the layout shows there is a preview

            // The host picks the level and starts; the others wait.
            bool host = bootstrap.IsLobbyHost;
            Show(Require<VisualElement>("host-controls"), host);
            Show(Require<Label>("waiting"), !host);
            if (host)
            {
                var level = Navigable(Require<ChoiceRow>("level"));
                var spawn = Navigable(Require<ChoiceRow>("spawn"));
                MenuParts.LevelChoices(level, spawn, bootstrap);
                var start = Navigable(Require<Button>("start"));
                start.clicked += bootstrap.StartLevel;
                start.AddToClassList(FirstFocusClass);
            }
            Navigable(Require<Button>("leave")).clicked += () => _ = bootstrap.LeaveAsync(null);

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
