using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// Online HUD (#117 P6): ping, jitter and loss in a corner while in a level, and a plain warning instead of silent
    /// refusals when the connection is the problem: "unstable connection" when a time the network made late hit a cap,
    /// a snapshot stream froze or the clock had to snap, and on the host "host frame drops" when its frames spike (the host
    /// is also the server, so everyone feels them). Layout: NetIndicator.uxml (<see cref="UIScreenCatalog.netIndicator"/>).
    /// Presentation only: it reads <see cref="NetQuality"/> and <see cref="SyncStats"/>, never decides anything.
    /// </summary>
    public class ConnectionIndicator : MonoBehaviour
    {
        const float RefreshSeconds = 0.25f;
        const float WarningSeconds = 4f;        // a trouble sign keeps the warning up this long
        const float JitterWarning = 0.08f;      // seconds
        const float LossWarning = 5f;           // %
        const int FrameSpikesWarning = 3;       // spikes within one warning period
        const int SortingOrder = ScreenStack.SortingOrder - 50;   // under the screens, over the world

        UIDocument document;
        VisualElement root, net;
        Label stats, warning;
        float nextRefresh;
        int lastTrouble, lastSpikes;
        float troubleUntil, spikesUntil;
        float spikesSince;
        int spikesAtSince;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isBatchMode) return;   // headless bots draw nothing
            var go = new GameObject("ConnectionIndicator");
            go.SetActive(false);   // set the panel before UIDocument enables
            DontDestroyOnLoad(go);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>(ScreenStack.PanelResource);
            doc.sortingOrder = SortingOrder;
            go.AddComponent<ConnectionIndicator>();
            go.SetActive(true);
        }

        void OnEnable()
        {
            document = GetComponent<UIDocument>();
            var catalog = Resources.Load<UIScreenCatalog>(ScreenStack.CatalogResource);
            if (document == null || catalog == null || catalog.netIndicator == null)
            {
                Debug.LogError("[UI] ConnectionIndicator: missing panel or UIScreenCatalog.netIndicator");
                enabled = false;
                return;
            }
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.Clear();
            catalog.netIndicator.CloneTree(root);
            net = Require<VisualElement>("net");
            stats = Require<Label>("net-stats");
            warning = Require<Label>("net-warning");
            net.pickingMode = PickingMode.Ignore;
            net.AddToClassList("hp-net--hidden");
        }

        T Require<T>(string name) where T : VisualElement
        {
            var e = root.Q<T>(name);
            if (e == null) Debug.LogError($"[UI] NetIndicator.uxml has no {typeof(T).Name} '{name}'");
            return e;
        }

        void Update()
        {
            if (net == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshSeconds;

            bool show = NetMode.IsNetworked && RunManager.Instance != null && Player.All.Count > 1;
            net.EnableInClassList("hp-net--hidden", !show);
            if (!show) return;

            float jitter = NetQuality.WorstJitterSeconds;
            float loss = WorstLoss(out int freezes);
            stats.text = $"PING {NetMode.RttMs} ms · JITTER {jitter * 1000f:F0} ms · LOSS {loss:F0}%";

            // A trouble sign: anything the network made late enough to change a verdict, freeze a player, or snap the clock.
            int trouble = SyncStats.ClaimsClamped + SyncStats.StampsClamped + SyncStats.ThrowsClamped + SyncStats.ClaimsRefused +
                          SyncStats.PredictedCatchesRefused + SyncStats.ClockSnaps + freezes;
            float now = Time.unscaledTime;
            if (trouble > lastTrouble) troubleUntil = now + WarningSeconds;
            lastTrouble = trouble;
            bool unstable = now < troubleUntil || jitter > JitterWarning || loss > LossWarning;

            bool hostDrops = false;
            if (NetMode.IsAuthority)
            {
                if (now - spikesSince > WarningSeconds)
                {
                    spikesSince = now;
                    spikesAtSince = NetQuality.FrameSpikes;
                }
                if (NetQuality.FrameSpikes - spikesAtSince >= FrameSpikesWarning && NetQuality.FrameSpikes > lastSpikes) spikesUntil = now + WarningSeconds;
                lastSpikes = NetQuality.FrameSpikes;
                hostDrops = now < spikesUntil;
            }

            warning.text = hostDrops ? "HOST FRAME DROPS" : "UNSTABLE CONNECTION";
            warning.EnableInClassList("hp-net__warning--hidden", !unstable && !hostDrops);
        }

        static float WorstLoss(out int freezes)
        {
            float worst = 0f;
            freezes = 0;
            for (int slot = 0; slot < Player.MaxSlots; slot++)
            {
                var s = NetQuality.StreamOf(slot);
                if (s == null) continue;
                worst = Mathf.Max(worst, s.LossPercent);
                freezes += s.Freezes;
            }
            return worst;
        }
    }
}
