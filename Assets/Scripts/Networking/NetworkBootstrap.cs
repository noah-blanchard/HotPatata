using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Beep
{
    /// <summary>
    /// Minimal session startup (M3.2): a utilitarian menu to Host, Join by IP, or Play Local, plus disconnect
    /// handling. Lives on the NetworkManager object, which persists across scenes, so there is exactly one
    /// of them. Direct IP connection for now; Relay / join codes arrive with the lobby milestone.
    ///
    /// Command line (for automated runs and second instances):
    ///   -beepHost | -beepJoin &lt;ip&gt; | -beepLocal     start immediately
    ///   -beepBot                                       the local player is played by a bot
    ///   -beepQuit &lt;seconds&gt;                            exit after this long
    ///   -beepLatency &lt;ms&gt;                              (debug) delay outgoing packets
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class NetworkBootstrap : MonoBehaviour
    {
        public static NetworkBootstrap Instance { get; private set; }

        [SerializeField] string menuScene = "Bootstrap";
        [SerializeField] string gameplayScene = "PassSandbox";
        [SerializeField] ushort port = 7777;

        NetworkManager nm;
        UnityTransport transport;
        string ip = "127.0.0.1";
        string message = "";
        bool playingLocal;

        public bool InSession => (nm != null && nm.IsListening) || playingLocal;

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
            nm.OnClientConnectedCallback += id => BeepLog.Run($"Client {id} connected (local={id == nm.LocalClientId})");

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-beepBot": PlayerBot.Enabled = true; break;
                    case "-beepLatency" when i + 1 < args.Length && int.TryParse(args[i + 1], out int ms): SimulateLatency(ms); break;
                    case "-beepQuit" when i + 1 < args.Length && float.TryParse(args[i + 1], out float s): Invoke(nameof(Quit), s); break;
                }
            }
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-beepHost") { StartHost(); break; }
                if (args[i] == "-beepJoin" && i + 1 < args.Length) { StartClient(args[i + 1]); break; }
                if (args[i] == "-beepLocal") { PlayLocal(); break; }
            }
        }

        void OnDestroy()
        {
            if (Instance != this || nm == null) return;
            nm.OnServerStarted -= OnServerStarted;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        // ------------------------------------------------------------------ actions

        public void PlayLocal()
        {
            playingLocal = true;
            message = "";
            SceneManager.LoadScene(gameplayScene);
        }

        public void StartHost()
        {
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");   // listen on every interface
            message = "";
            if (!nm.StartHost()) message = "Could not start the host (is the port in use?)";
        }

        public void StartClient(string address)
        {
            ip = address;
            transport.SetConnectionData(address, port);
            message = $"Connecting to {address}:{port} ...";
            if (!nm.StartClient()) message = "Could not start the client";
        }

        public void Leave()
        {
            if (nm.IsListening) nm.Shutdown();
            playingLocal = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(menuScene);
        }

        void Quit() => Application.Quit();

        /// <summary>Debug only: delay every packet this side sends by <paramref name="milliseconds"/> (Editor / development builds).</summary>
        public void SimulateLatency(int milliseconds)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            transport.SetDebugSimulatorParameters(milliseconds, milliseconds / 5, 0);
            BeepLog.Run($"Simulating {milliseconds} ms one-way latency");
#endif
        }

        // ------------------------------------------------------------------ callbacks

        void OnServerStarted()
        {
            if (nm.IsServer) nm.SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single);
        }

        void OnClientDisconnected(ulong clientId)
        {
            // On a client this fires for its own connection ending (host left, kicked, network lost).
            // On the host it fires for every client that leaves; the host itself just carries on.
            BeepLog.Run($"Client {clientId} disconnected");
            if (nm.IsServer && clientId != nm.LocalClientId) return;

            string reason = string.IsNullOrEmpty(nm.DisconnectReason) ? "connection closed" : nm.DisconnectReason;
            Leave();
            message = "Disconnected: " + reason;
        }

        // ------------------------------------------------------------------ menu

        void Update()
        {
            if (InSession && Keys.LeavePressed) Leave();
        }

        void OnGUI()
        {
            if (InSession)
            {
                if (GUI.Button(new Rect(Screen.width - 130, 10, 120, 26), "Leave (F10)")) Leave();
                return;
            }

            const float w = 320f;
            var box = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.25f, w, 250f);
            GUI.Box(box, "BEEP!");
            GUILayout.BeginArea(new Rect(box.x + 16, box.y + 32, w - 32, 210f));

            if (GUILayout.Button("Play Local (2 players, one keyboard)", GUILayout.Height(30))) PlayLocal();
            GUILayout.Space(8);
            if (GUILayout.Button("Host", GUILayout.Height(30))) StartHost();
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            ip = GUILayout.TextField(ip, GUILayout.Width(170), GUILayout.Height(30));
            if (GUILayout.Button("Join", GUILayout.Height(30))) StartClient(ip);
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message);

            GUILayout.EndArea();
        }

        static class Keys
        {
            public static bool LeavePressed =>
                UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame;
        }
    }
}
