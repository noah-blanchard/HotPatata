using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// The board of a menu station (ARCHITECTURE §6.2): a <see cref="UIScreen"/> built into the station's world-space
    /// document instead of the <see cref="ScreenStack"/>, with the same conventions (a UXML layout from the catalog,
    /// <see cref="UIScreen.Require{T}"/>, up / down walk). Its choices move the <see cref="MenuFlow"/> (the camera follows)
    /// or call the bootstrap's public methods; Back goes to the previous station. Boards that start session work show it
    /// in place ("Creating your game..."), their buttons off until it ends.
    /// </summary>
    public abstract class StationScreen : UIScreen
    {
        protected readonly NetworkBootstrap Bootstrap;
        protected readonly MenuFlow Flow;

        VisualElement actions, working;
        Label status;
        bool busy;

        protected StationScreen(NetworkBootstrap bootstrap, MenuFlow flow)
        {
            Bootstrap = bootstrap;
            Flow = flow;
        }

        /// <summary>Back (Esc / gamepad B) goes to the previous station.</summary>
        public override void OnBack() => Flow.Back();

        /// <summary>
        /// The layout's working block (a rocking potato and the bootstrap's status, named <c>working</c> and
        /// <c>status</c>) and the <c>actions</c> it replaces while the session flow is busy.
        /// </summary>
        protected void WorkingBlock()
        {
            actions = Require<VisualElement>("actions");
            working = Require<VisualElement>("working");
            status = Require<Label>("status");
            UIParts.Wobble(Require<VisualElement>("working-spinner"));
            Show(working, false);
        }

        /// <summary>Re-reads the bootstrap (every <see cref="MenuParts.RefreshMs"/>).</summary>
        protected virtual void Refresh()
        {
            if (working == null) return;
            bool now = Bootstrap.Phase == NetworkBootstrap.Mode.Working;
            status.text = string.IsNullOrEmpty(Bootstrap.Status) ? "Connecting..." : Bootstrap.Status;
            if (now == busy) return;
            busy = now;
            Show(working, busy);
            actions.SetEnabled(!busy);
            if (!busy && Root.enabledInHierarchy && Root.focusController?.focusedElement == null) FirstFocus()?.Focus();
        }

        protected void StartRefresh(VisualElement root)
        {
            root.schedule.Execute(Refresh).Every(MenuParts.RefreshMs);
            Refresh();
        }
    }

    /// <summary>The Title station: the HOT PATATA logo over the island and one prompt. The menu's root: Back does nothing.</summary>
    public class TitleStation : StationScreen
    {
        public TitleStation(NetworkBootstrap bootstrap, MenuFlow flow) : base(bootstrap, flow) { }

        public override bool CanGoBack => false;
        public override void OnBack() { }

        public Button Play { get; private set; }

        protected override VisualElement Build()
        {
            var root = FromTemplate(Templates.stationTitle);
            Play = Navigable(Require<Button>("play"));
            Play.clicked += () => Flow.Choose(MenuAction.Start);
            Play.AddToClassList(FirstFocusClass);
            return root;
        }
    }

    /// <summary>
    /// The Play station (parity with the old main menu): your name, Host Online (to the level choice), Join with a game
    /// code (<see cref="CodeDials"/>, a gamepad can enter it), Play Local (to the level choice), Settings (the settings
    /// screen over the scene), and the LAN / testing connection behind a toggle. Errors from the session flow show here.
    /// </summary>
    public class PlayStation : StationScreen
    {
        Label error;

        public PlayStation(NetworkBootstrap bootstrap, MenuFlow flow) : base(bootstrap, flow) { }

        public TextField NameField { get; private set; }
        public CodeDials Code { get; private set; }
        public Label Error => error;

        protected override VisualElement Build()
        {
            var root = FromTemplate(Templates.stationPlay);

            NameField = Navigable(MenuParts.Field(Require<TextField>("name-field"), PlayerNames.Local, PlayerNames.MaxLength));
            NameField.RegisterValueChangedCallback(e => PlayerNames.Local = e.newValue);   // saved here; the host cleans it up when shared

            var host = Navigable(Require<Button>("host-online"));
            host.clicked += () => Flow.Choose(MenuAction.HostOnline);
            host.AddToClassList(FirstFocusClass);
            Code = Navigable(Require<CodeDials>("code-dials"));
            Navigable(Require<Button>("join-code")).clicked += () => _ = Bootstrap.JoinCodeAsync(Code.Value);
            Navigable(Require<Button>("play-local")).clicked += () => Flow.Choose(MenuAction.PlayLocal);
            Navigable(Require<Button>("settings")).clicked += () => ScreenStack.Get().Push(new SettingsScreen(Bootstrap.Tuning));

            var direct = Require<VisualElement>("direct-group");
            var toggle = Navigable(Require<ToggleRow>("direct-toggle"));
            toggle.SetValueWithoutNotify(false);
            var ip = Navigable(MenuParts.Field(Require<TextField>("direct-address"), Bootstrap.DirectAddress, MenuParts.AddressMaxLength));
            ip.RegisterValueChangedCallback(e => Bootstrap.DirectAddress = e.newValue.Trim());
            Navigable(Require<Button>("join-ip")).clicked += () => Bootstrap.StartClientDirect(Bootstrap.DirectAddress);
            Navigable(Require<Button>("host-direct")).clicked += () => Flow.Choose(MenuAction.HostLan);
            Show(direct, false);
            toggle.Changed += on => Show(direct, on);

            error = Require<Label>("error");
            WorkingBlock();
            StartRefresh(root);
            return root;
        }

        protected override void Refresh()
        {
            base.Refresh();
            string message = Bootstrap.LastMessage;
            error.text = message ?? "";
            Show(error, !string.IsNullOrEmpty(message));
        }
    }

    /// <summary>
    /// The Level station: the level and spawn point, then the button that does what brought you here (Host Online,
    /// Host on LAN, Play local, or Done for the host coming from the lobby). Back returns where you came from.
    /// </summary>
    public class LevelStation : StationScreen
    {
        ChoiceRow level, spawn;
        Label confirmLabel, confirmSub;

        public LevelStation(NetworkBootstrap bootstrap, MenuFlow flow) : base(bootstrap, flow) { }

        public Button Confirm { get; private set; }

        protected override VisualElement Build()
        {
            var root = FromTemplate(Templates.stationLevel);
            level = Navigable(Require<ChoiceRow>("level"));
            spawn = Navigable(Require<ChoiceRow>("spawn"));
            MenuParts.LevelChoices(level, spawn, Bootstrap);
            Confirm = Navigable(Require<Button>("confirm"));
            Confirm.clicked += OnConfirm;
            Confirm.AddToClassList(FirstFocusClass);
            confirmLabel = Require<Label>("confirm-label");
            confirmSub = Require<Label>("confirm-sub");
            Navigable(Require<Button>("back")).clicked += () => Flow.Back();
            WorkingBlock();
            StartRefresh(root);
            ShowPurpose();
            return root;
        }

        public override void OnShow()
        {
            MenuParts.SyncLevelChoices(level, spawn, Bootstrap);
            ShowPurpose();
        }

        void ShowPurpose()
        {
            (string title, string sub) = Flow.Purpose switch
            {
                LevelPurpose.HostOnline => ("HOST ONLINE", "get a game code"),
                LevelPurpose.HostLan => ("HOST ON LAN", "friends join your address"),
                LevelPurpose.Local => ("PLAY LOCAL", "2 players, one keyboard"),
                _ => ("DONE", "back to the lobby")
            };
            confirmLabel.text = title;
            confirmSub.text = sub;
        }

        void OnConfirm()
        {
            switch (Flow.Purpose)
            {
                case LevelPurpose.HostOnline: _ = Bootstrap.HostOnlineAsync(); break;
                case LevelPurpose.HostLan: Bootstrap.StartHostDirect(); break;
                case LevelPurpose.Local: Bootstrap.PlayLocal(); break;
                default: Flow.Back(); break;
            }
        }
    }

    /// <summary>
    /// The Lobby station (parity with the old lobby screen): the game code (Copy code), a card per player (slot chip,
    /// name, HOST / YOU) and per free seat, then for the host the chosen level (Change level, to the Level station) and
    /// Start, for the others a waiting line, and Leave. Back asks before leaving. The players also step onto the stage
    /// in front of it (<see cref="MenuLobbyStage"/>).
    /// </summary>
    public class LobbyStation : StationScreen
    {
        Label code, playersTitle, levelName;
        VisualElement playerList, hostControls, waiting;
        Button start, leave;
        string listed;

        public LobbyStation(NetworkBootstrap bootstrap, MenuFlow flow) : base(bootstrap, flow) { }

        protected override VisualElement Build()
        {
            var root = FromTemplate(Templates.stationLobby);

            code = Require<Label>("code");
            Navigable(Require<Button>("copy-code")).clicked += () => GUIUtility.systemCopyBuffer = Bootstrap.LobbyCode ?? "";
            playersTitle = Require<Label>("players-title");
            playerList = Require<VisualElement>("player-list");
            playerList.Clear();   // anything the layout shows there is a preview

            hostControls = Require<VisualElement>("host-controls");
            waiting = Require<VisualElement>("waiting");
            UIParts.Wobble(Require<VisualElement>("waiting-spinner"));
            levelName = Require<Label>("level-name");
            Navigable(Require<Button>("change-level")).clicked += () => Flow.Choose(MenuAction.ChangeLevel);
            start = Navigable(Require<Button>("start"));
            start.clicked += Bootstrap.StartLevel;
            leave = Navigable(Require<Button>("leave"));
            leave.clicked += AskLeave;

            StartRefresh(root);
            return root;
        }

        /// <summary>The host lands on Start, the others on Leave.</summary>
        public override Focusable FirstFocus()
        {
            Refresh();   // the host's controls show before they take focus
            return Bootstrap.IsLobbyHost ? start : leave;
        }

        public override void OnBack() => AskLeave();

        void AskLeave() =>
            ScreenStack.Get().Push(new ConfirmScreen("Leave the lobby?", "You leave this game and go back to the menu.", "Leave", () => _ = Bootstrap.LeaveAsync(null)));

        /// <summary>
        /// The code, the host's controls and the player cards, rebuilt only when the list changed (join, leave, name):
        /// each player's slot chip, name and HOST / YOU badges, then an empty card per free seat.
        /// </summary>
        protected override void Refresh()
        {
            code.text = Bootstrap.LobbyCode ?? "";
            bool host = Bootstrap.IsLobbyHost;
            Show(hostControls, host);
            Show(waiting, !host);
            levelName.text = MenuParts.LevelSummary(Bootstrap);

            var players = Bootstrap.LobbyPlayers();
            string signature = string.Join("|", players.Select(p => p.Label + p.IsHost + p.IsYou)) + "/" + Bootstrap.LobbyMaxPlayers;
            if (signature == listed) return;
            listed = signature;
            playersTitle.text = $"{players.Count} / {Bootstrap.LobbyMaxPlayers}";
            playerList.Clear();
            foreach (var p in players)
            {
                var card = new VisualElement();
                card.AddToClassList("hp-slot");
                card.Add(UIParts.Chip(Bootstrap.Tuning, p.Slot));
                var name = new Label(p.Name);
                name.AddToClassList("hp-slot__name");
                card.Add(name);
                if (p.IsHost) card.Add(UIParts.Badge("HOST", "crown"));
                if (p.IsYou) card.Add(UIParts.Badge("YOU", "star", "hp-badge--you"));
                playerList.Add(card);
            }
            for (int i = players.Count; i < Bootstrap.LobbyMaxPlayers; i++)
            {
                var seat = new VisualElement();
                seat.AddToClassList("hp-slot");
                seat.AddToClassList("hp-slot--empty");
                seat.Add(UIParts.EmptyChip());
                var empty = new Label("Waiting for a player...");
                empty.AddToClassList("hp-slot__name");
                seat.Add(empty);
                playerList.Add(seat);
            }
        }
    }
}
