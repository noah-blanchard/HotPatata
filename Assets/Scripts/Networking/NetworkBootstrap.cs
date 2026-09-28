using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata
{
    /// <summary>
    /// Session startup and the lobby (Milestone 4). Lives on the NetworkManager object, which persists across
    /// scenes, so there is exactly one of them.
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
    ///   -patataBot   -patataScene &lt;name&gt;   -patataQuit &lt;s&gt;   -patataLatency &lt;ms&gt;
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class NetworkBootstrap : MonoBehaviour
    {
        enum Mode { Menu, Working, Lobby, InGame }

        public static NetworkBootstrap Instance { get; private set; }

        [SerializeField] string menuScene = "Bootstrap";
        [SerializeField] string[] gameplayScenes = { "PrototypeCourse", "PassSandbox" };
        [SerializeField, Tooltip("Highest checkpoint id of each gameplay scene (same order), offered as a start point.")]
        int[] sceneCheckpoints = { 7, 1 };
        [SerializeField] ushort port = 7777;

        NetworkManager nm;
        UnityTransport transport;
        readonly SessionService sessions = new SessionService();

        Mode mode = Mode.Menu;
        int sceneIndex;
        string ip = "127.0.0.1";
        string codeInput = "";
        string message = "";
        string status = "";
        bool showDirect;
        bool directMode;       // LAN / testing: the level loads as soon as the host starts
        bool leaving;
        int autoStartPlayers;

        string GameplayScene => gameplayScenes[Mathf.Clamp(sceneIndex, 0, gameplayScenes.Length - 1)];
        int SceneCheckpoints => sceneIndex >= 0 && sceneIndex < sceneCheckpoints.Length ? sceneCheckpoints[sceneIndex] : 0;

        public bool InSession => mode == Mode.InGame;
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
                    case "-patataScene" when i + 1 < args.Length: sceneIndex = Mathf.Max(0, Array.IndexOf(gameplayScenes, args[i + 1])); break;
                    case "-patataLatency" when i + 1 < args.Length && int.TryParse(args[i + 1], out int ms): SimulateLatency(ms); break;
                    case "-patataAutoStart" when i + 1 < args.Length && int.TryParse(args[i + 1], out int n): autoStartPlayers = n; break;
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
                var session = await sessions.HostAsync();
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
                var session = await sessions.JoinAsync(code);
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
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
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

        // ------------------------------------------------------------------ UI (utilitarian on purpose)

        GUIStyle big, error;

        void OnGUI()
        {
            big ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            error ??= new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(1f, 0.45f, 0.4f) } };

            switch (mode)
            {
                case Mode.InGame:
                    if (GUI.Button(new Rect(Screen.width - 130, 10, 120, 26), "Leave (F10)")) _ = LeaveAsync(null);
                    break;
                case Mode.Menu: DrawMenu(); break;
                case Mode.Working: DrawPanel("HotPatata", () => GUILayout.Label(status)); break;
                case Mode.Lobby: DrawLobby(); break;
            }
        }

        void DrawPanel(string title, Action content, float height = 320f)
        {
            const float w = 360f;
            var box = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.18f, w, height);
            GUI.Box(box, title);
            GUILayout.BeginArea(new Rect(box.x + 16, box.y + 30, w - 32, height - 40));
            content();
            GUILayout.EndArea();
        }

        void DrawMenu()
        {
            DrawPanel("HotPatata", () =>
            {
                GUILayout.Label("Level");
                sceneIndex = GUILayout.SelectionGrid(sceneIndex, gameplayScenes, gameplayScenes.Length, GUILayout.Height(26));
                DrawStartCheckpoint(26f);
                GUILayout.Space(8);

                if (GUILayout.Button("Host Online  (get a game code)", GUILayout.Height(32))) _ = HostOnlineAsync();
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                codeInput = SessionService.NormalizeCode(GUILayout.TextField(codeInput, SessionService.CodeLength, GUILayout.Width(150), GUILayout.Height(32)));
                if (GUILayout.Button("Join with code", GUILayout.Height(32))) _ = JoinCodeAsync(codeInput);
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
                if (GUILayout.Button("Play Local  (2 players, one keyboard)", GUILayout.Height(28))) PlayLocal();

                GUILayout.Space(6);
                showDirect = GUILayout.Toggle(showDirect, "Direct connection (LAN / testing)");
                if (showDirect)
                {
                    GUILayout.BeginHorizontal();
                    ip = GUILayout.TextField(ip, GUILayout.Width(150), GUILayout.Height(24));
                    if (GUILayout.Button("Join IP")) StartClientDirect(ip);
                    if (GUILayout.Button("Host")) StartHostDirect();
                    GUILayout.EndHorizontal();
                }

                if (!string.IsNullOrEmpty(message)) GUILayout.Label(message, error);
            }, showDirect ? 400f : 370f);
        }

        void DrawLobby()
        {
            var session = sessions.Current;
            if (session == null)
            {
                DrawPanel("HotPatata", () => GUILayout.Label("Connecting..."));
                return;
            }

            DrawPanel("Lobby", () =>
            {
                GUILayout.Label("Game code", GUILayout.Height(18));
                GUILayout.Label(session.Code, big, GUILayout.Height(46));
                if (GUILayout.Button("Copy code")) GUIUtility.systemCopyBuffer = session.Code;
                GUILayout.Space(8);

                // The service keeps a crashed player listed for a while; the host knows who is really connected.
                var players = session.Players;
                int shown = session.IsHost && nm.IsServer ? Mathf.Min(players.Count, nm.ConnectedClientsIds.Count) : players.Count;
                GUILayout.Label($"Players ({shown}/{session.MaxPlayers})");
                for (int i = 0; i < shown; i++)
                {
                    string tag = players[i].Id == session.Host ? "  (host)" : "";
                    string you = players[i].Id == session.CurrentPlayer.Id ? "  (you)" : "";
                    GUILayout.Label($"   Player {i + 1}{tag}{you}");
                }
                GUILayout.Space(8);

                if (session.IsHost)
                {
                    GUILayout.Label("Level");
                    sceneIndex = GUILayout.SelectionGrid(sceneIndex, gameplayScenes, gameplayScenes.Length, GUILayout.Height(24));
                    DrawStartCheckpoint(24f);
                    if (GUILayout.Button("Start", GUILayout.Height(32))) StartLevel();
                }
                else
                {
                    GUILayout.Label("Waiting for the host to start...");
                }

                if (GUILayout.Button("Leave", GUILayout.Height(26))) _ = LeaveAsync(null);
            }, session.IsHost ? 450f : 400f);
        }

        /// <summary>Where the run starts: the beginning or one of the level's checkpoints (<see cref="RunOptions"/>).</summary>
        void DrawStartCheckpoint(float height)
        {
            int count = SceneCheckpoints;
            RunOptions.StartCheckpoint = Mathf.Clamp(RunOptions.StartCheckpoint, 0, count);
            if (count == 0) return;

            var labels = new string[count + 1];
            labels[0] = "Start";
            for (int i = 1; i <= count; i++) labels[i] = "CP" + i;
            GUILayout.Label("Spawn at");
            RunOptions.StartCheckpoint = GUILayout.SelectionGrid(RunOptions.StartCheckpoint, labels, labels.Length, GUILayout.Height(height));
        }

        static class Keys
        {
            public static bool LeavePressed =>
                UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame;
        }
    }
}
