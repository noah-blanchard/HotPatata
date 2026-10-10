using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata
{
    /// <summary>
    /// Session startup and the lobby (Milestone 4). Lives on the NetworkManager object, which persists across
    /// scenes, so there is exactly one of them. This is the logic only: the menu and the lobby are drawn by
    /// <see cref="MenuView"/> (UI Toolkit, #15), which reads the state below and calls the public methods. In batch mode
    /// (headless bots) there is no view; the command line drives everything.
    ///
    ///   Menu  --Host Online--> Lobby (code + player list + Start, host only)
    ///         --Join code----> Lobby (waits for the host)          --host Start--> everybody loads the level
    ///         --Play Local---> local two-player rig
    ///         --Direct IP----> (LAN / testing) loads the level immediately
    ///
    /// Online games go through Unity Multiplayer Services (Relay), see <see cref="SessionService"/>.
    ///
    /// Command line (automated runs and second instances):
    ///   -patataHost | -patataJoin &lt;ip&gt; | -patataLocal      direct IP / local
    ///   -patataHostOnline | -patataJoinCode &lt;code&gt;         online session
    ///   -patataAutoStart &lt;n&gt;                             host starts the level once n players are in the lobby
    ///   -patataCheckpoint &lt;id&gt;                          start the run at that checkpoint (host / local)
    ///   -patataName &lt;name&gt;                             player name for this process (instead of the saved one)
    ///   -patataBot   -patataBotMove &lt;Stand|Strafe|Jump|RunAcross|Ride&gt;
    ///   -patataScene &lt;name&gt;   -patataQuit &lt;s&gt;   -patataLatency &lt;ms&gt;
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class NetworkBootstrap : MonoBehaviour
    {
        /// <summary>Menu: the main menu. Working: creating or joining a game. Lobby: in an online lobby. InGame: in a level.</summary>
        public enum Mode { Menu, Working, Lobby, InGame }

        public static NetworkBootstrap Instance { get; private set; }

        [SerializeField] string menuScene = "Bootstrap";
        [SerializeField] string[] gameplayScenes = { "PatataWilds", "IndustrialPlant", "PassSandbox" };
        [SerializeField, Tooltip("Highest checkpoint id of each gameplay scene (same order), offered as a start point.")]
        int[] sceneCheckpoints = { 9, 9, 7, 1 };
        [SerializeField] ushort port = 7777;
        [SerializeField, Tooltip("Player colours and shapes for the lobby list, and the defaults of the settings screen.")] GameTuning tuning;

        NetworkManager nm;
        UnityTransport transport;
        readonly SessionService sessions = new SessionService();

        Mode mode = Mode.Menu;
        int sceneIndex;
        string ip = "127.0.0.1";
        string message = "";
        string status = "";
        bool directMode;       // LAN / testing: the level loads as soon as the host starts
        bool leaving;
        int autoStartPlayers;

        string GameplayScene => gameplayScenes[Mathf.Clamp(sceneIndex, 0, gameplayScenes.Length - 1)];
        int SceneCheckpoints => sceneIndex >= 0 && sceneIndex < sceneCheckpoints.Length ? sceneCheckpoints[sceneIndex] : 0;

        public bool InSession => mode == Mode.InGame;
        public Mode Phase => mode;
        /// <summary>What a Working phase is doing ("Creating your game...").</summary>
        public string Status => status;
        public GameTuning Tuning => tuning;
        public IReadOnlyList<string> GameplayScenes => gameplayScenes;

        /// <summary>The level the menu / host lobby has selected; the spawn point is clamped to its checkpoints.</summary>
        public int SceneIndex
        {
            get => sceneIndex;
            set
            {
                sceneIndex = Mathf.Clamp(value, 0, gameplayScenes.Length - 1);
                RunOptions.StartCheckpoint = Mathf.Clamp(RunOptions.StartCheckpoint, 0, SceneCheckpoints);
            }
        }

        /// <summary>Highest checkpoint of the selected level (its spawn choices are Start and CP1..CPn).</summary>
        public int SceneCheckpointCount => SceneCheckpoints;

        /// <summary>The address for a direct (LAN / testing) connection.</summary>
        public string DirectAddress
        {
            get => ip;
            set => ip = value;
        }
        public string SessionCode => sessions.Current?.Code;
        public int LobbyPlayerCount => sessions.Current == null ? 0
            : nm.IsServer ? Mathf.Min(sessions.Current.PlayerCount, nm.ConnectedClientsIds.Count) : sessions.Current.PlayerCount;
        public string LastMessage => message;

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            nm = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
        }

        void Start()
        {
            if (Instance != this) return;
            if (!Application.isBatchMode) gameObject.AddComponent<MenuView>();   // headless bots need no UI

            nm.OnServerStarted += OnServerStarted;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            nm.OnClientConnectedCallback += id => PatataLog.Run($"Client {id} connected (local={id == nm.LocalClientId})");
            sessions.Ended += reason => { if (!leaving) _ = LeaveAsync(reason); };

            ParseCommandLine();
        }

        void OnDestroy()
        {
            if (Instance != this || nm == null) return;
            nm.OnServerStarted -= OnServerStarted;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        void ParseCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-patataBot": PlayerBot.Enabled = true; break;
                    case "-patataBotMove" when i + 1 < args.Length && Enum.TryParse(args[i + 1], true, out PlayerBot.Pattern pattern):
                        PlayerBot.Movement = pattern;
                        break;
                    case "-patataScene" when i + 1 < args.Length: sceneIndex = Mathf.Max(0, Array.IndexOf(gameplayScenes, args[i + 1])); break;
                    case "-patataLatency" when i + 1 < args.Length && int.TryParse(args[i + 1], out int ms): SimulateLatency(ms); break;
                    case "-patataAutoStart" when i + 1 < args.Length && int.TryParse(args[i + 1], out int n): autoStartPlayers = n; break;
                    case "-patataName" when i + 1 < args.Length: PlayerNames.OverrideForThisProcess(args[i + 1]); break;
                    case "-patataCheckpoint" when i + 1 < args.Length && int.TryParse(args[i + 1], out int cp): RunOptions.StartCheckpoint = Mathf.Max(0, cp); break;
                    case "-patataLeaveAfter" when i + 1 < args.Length && float.TryParse(args[i + 1], out float leaveIn): Invoke(nameof(AutoLeave), leaveIn); break;
                    case "-patataQuit" when i + 1 < args.Length && float.TryParse(args[i + 1], out float s): Invoke(nameof(Quit), s); break;
                }
            }
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-patataHost") { StartHostDirect(); break; }
                if (args[i] == "-patataJoin" && i + 1 < args.Length) { StartClientDirect(args[i + 1]); break; }
                if (args[i] == "-patataHostOnline") { _ = HostOnlineAsync(); break; }
                if (args[i] == "-patataJoinCode" && i + 1 < args.Length) { _ = JoinCodeAsync(args[i + 1]); break; }
                if (args[i] == "-patataLocal") { PlayLocal(); break; }
            }
        }

        void Quit() => Application.Quit();

        void AutoLeave() => _ = LeaveAsync("left on request");   // automation: the same path as pressing Leave

        void Update()
        {
            if (mode == Mode.InGame && Keys.LeavePressed) _ = LeaveAsync(null);

            // Lobby -> in game as soon as the level scene is the active one (host Start, or a client following the host).
            if (mode == Mode.Lobby && nm.IsListening && SceneManager.GetActiveScene().name != menuScene)
            {
                mode = Mode.InGame;
                status = "";
            }

            // Automation: the host starts once enough players are in the lobby.
            if (mode == Mode.Lobby && autoStartPlayers > 0 && nm.IsServer && sessions.Current != null
                && sessions.Current.PlayerCount >= autoStartPlayers && nm.ConnectedClientsIds.Count >= autoStartPlayers)
            {
                autoStartPlayers = 0;
                StartLevel();
            }
        }

        // ------------------------------------------------------------------ local and direct IP

        public void PlayLocal()
        {
            mode = Mode.InGame;
            message = "";
            SceneManager.LoadScene(GameplayScene);
        }

        public void StartHostDirect()
        {
            directMode = true;
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");   // listen on every interface
            message = "";
            mode = Mode.Working;
            if (!nm.StartHost()) Fail("Could not start the host (is the port in use?)");
            else mode = Mode.InGame;
        }

        public void StartClientDirect(string address)
        {
            directMode = true;
            ip = address;
            transport.SetConnectionData(address, port);
            message = "";
            mode = Mode.Working;
            status = $"Connecting to {address}:{port} ...";
            if (!nm.StartClient()) Fail("Could not start the client");
            else mode = Mode.InGame;
        }

        void OnServerStarted()
        {
            // Direct hosts load the level straight away. Online hosts wait in the lobby until they press Start.
            if (nm.IsServer && directMode) nm.SceneManager.LoadScene(GameplayScene, LoadSceneMode.Single);
        }

        // ------------------------------------------------------------------ online sessions

        public async System.Threading.Tasks.Task HostOnlineAsync()
        {
            if (mode == Mode.Working) return;
            directMode = false;
            message = "";
            status = "Creating your game...";
            mode = Mode.Working;
            try
            {
                var session = await sessions.HostAsync(PlayerNames.Shared);
                mode = Mode.Lobby;
                status = "";
                PatataLog.Run($"[Session] lobby open, code {session.Code}");
            }
            catch (Exception e)
            {
                Fail(SessionService.Describe(e));
                PatataLog.Run($"[Session] host failed: {message}");
                await sessions.LeaveAsync();
            }
        }

        public async System.Threading.Tasks.Task JoinCodeAsync(string rawCode)
        {
            if (mode == Mode.Working) return;
            directMode = false;
            message = "";

            string code = SessionService.NormalizeCode(rawCode);
            if (!SessionService.IsPlausibleCode(code))
            {
                Fail($"A game code has {SessionService.CodeLength} letters or digits.");
                PatataLog.Run($"[Session] join failed: {message}");
                return;
            }

            status = $"Joining {code}...";
            mode = Mode.Working;
            try
            {
                var session = await sessions.JoinAsync(code, PlayerNames.Shared);
                mode = Mode.Lobby;
                status = "";
                PatataLog.Run($"[Session] joined lobby {session.Code}");
            }
            catch (Exception e)
            {
                Fail(SessionService.Describe(e));
                PatataLog.Run($"[Session] join failed: {message}");
                await sessions.LeaveAsync();
            }
        }

        /// <summary>Host only: everybody in the lobby loads the level together.</summary>
        public void StartLevel()
        {
            if (mode != Mode.Lobby || !nm.IsServer) return;
            PatataLog.Run($"[Session] starting {GameplayScene} with {nm.ConnectedClientsIds.Count} connected");
            nm.SceneManager.LoadScene(GameplayScene, LoadSceneMode.Single);
        }

        public async System.Threading.Tasks.Task LeaveAsync(string reason)
        {
            if (leaving) return;
            leaving = true;
            try
            {
                if (sessions.InSession) await sessions.LeaveAsync();          // the SDK also stops the network
                else if (nm.IsListening) nm.Shutdown();

                directMode = false;
                autoStartPlayers = 0;
                CursorPolicy.SetGameplayLock(false);
                mode = Mode.Menu;
                message = reason ?? "";
                PatataLog.Run($"[Session] left{(string.IsNullOrEmpty(reason) ? "" : ": " + reason)}");
                if (SceneManager.GetActiveScene().name != menuScene) SceneManager.LoadScene(menuScene);
            }
            finally
            {
                leaving = false;
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            PatataLog.Run($"Client {clientId} disconnected");
            // On a client this fires for its own connection ending (host left, kicked, network lost).
            // On the host it fires for every client that leaves; the host itself just carries on.
            if (nm.IsServer && clientId != nm.LocalClientId) return;
            if (leaving || mode == Mode.Menu) return;

            string reason = string.IsNullOrEmpty(nm.DisconnectReason) ? "The connection to the host was lost." : nm.DisconnectReason;
            _ = LeaveAsync(reason);
        }

        void Fail(string text)
        {
            message = text;
            status = "";
            mode = Mode.Menu;
        }

        /// <summary>Raised when latency simulation is requested; handled by HotPatata.DebugTools (Editor / development builds only).</summary>
        public static event Action<GameObject, int> LatencyRequested;

        /// <summary>Debug only: delay every packet this side sends by <paramref name="milliseconds"/>.</summary>
        public void SimulateLatency(int milliseconds) => LatencyRequested?.Invoke(gameObject, milliseconds);

        // ------------------------------------------------------------------ state for the menu view (MenuView, ARCHITECTURE §6.2)

        /// <summary>
        /// One line of the lobby list: the slot (its colour and shape through <see cref="PlayerIdentity"/>), the name,
        /// the same as rich text (glyph + name), and the host / you tags.
        /// </summary>
        public readonly struct LobbyPlayer
        {
            public readonly string Label, Name;
            public readonly int Slot;
            public readonly bool IsHost, IsYou;

            public LobbyPlayer(string label, string name, int slot, bool isHost, bool isYou)
            {
                Label = label;
                Name = name;
                Slot = slot;
                IsHost = isHost;
                IsYou = isYou;
            }
        }

        public bool HasLobby => sessions.Current != null;
        public string LobbyCode => sessions.Current?.Code;
        public bool IsLobbyHost => sessions.Current != null && sessions.Current.IsHost;
        public int LobbyMaxPlayers => sessions.Current?.MaxPlayers ?? 0;

        /// <summary>The lobby list in slot order (level slots are handed out in connection order, so usually the slot each player gets).</summary>
        public List<LobbyPlayer> LobbyPlayers()
        {
            var list = new List<LobbyPlayer>();
            var session = sessions.Current;
            if (session == null) return list;
            // The service keeps a crashed player listed for a while; the host knows who is really connected.
            var players = session.Players;
            int shown = session.IsHost && nm.IsServer ? Mathf.Min(players.Count, nm.ConnectedClientsIds.Count) : players.Count;
            for (int i = 0; i < shown; i++)
            {
                string name = SessionService.NameOf(players[i], i);
                list.Add(new LobbyPlayer(PlayerIdentity.RichLabel(tuning, i, name), name, i,
                                         players[i].Id == session.Host, players[i].Id == session.CurrentPlayer.Id));
            }
            return list;
        }

        /// <summary>The spawn choices of the selected level: Start, then CP1..CPn (<see cref="RunOptions.StartCheckpoint"/>).</summary>
        public static List<string> SpawnLabels(int checkpoints)
        {
            var labels = new List<string> { "Start" };
            for (int i = 1; i <= checkpoints; i++) labels.Add("CP" + i);
            return labels;
        }

        static class Keys
        {
            public static bool LeavePressed =>
                UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame;
        }
    }
}
