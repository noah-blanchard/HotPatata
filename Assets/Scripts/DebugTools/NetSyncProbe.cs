using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Development only (F7 toggles the panel). Measures the network time-frame error that docs/netcode-deterministic-plan.md
    /// is about, on every machine:
    ///  - riders: for each remote player whose owner stands on a moving carrier, the vertical gap between their drawn feet
    ///    and the carrier's top (negative = sunk into it), next to the gap their replicated world position alone would
    ///    give (what was drawn before riders were rebuilt on their carrier), as one <c>[Sync] rider</c> line every
    ///    couple of seconds;
    ///  - the counters of <see cref="SyncStats"/> (host hazard verdicts, client throw prediction vs the host's verdict).
    /// Observes only. Created automatically in the Editor and development builds.
    /// </summary>
    [DefaultExecutionOrder(2000)]   // after remote players have been placed for this frame
    public class NetSyncProbe : MonoBehaviour
    {
        const float ReportEvery = 2f;   // seconds between rider lines
        const float RayStart = 1.5f;    // metres above the feet: still finds the top of a slab the rider is sunk into
        const float RayLength = 3f;

        struct Gap
        {
            public int Carrier;
            public float Sum, Min, Max, WorldSum, WorldMin, WorldMax;
            public int Count;

            public void Add(int carrier, float gap, float world)
            {
                if (Count == 0 || carrier != Carrier)
                    this = new Gap { Carrier = carrier, Min = gap, Max = gap, WorldMin = world, WorldMax = world };
                Sum += gap;
                Min = Mathf.Min(Min, gap);
                Max = Mathf.Max(Max, gap);
                WorldSum += world;
                WorldMin = Mathf.Min(WorldMin, world);
                WorldMax = Mathf.Max(WorldMax, world);
                Count++;
            }
        }

        public static bool Visible;

        readonly Gap[] gaps = new Gap[Player.MaxSlots];
        readonly float[] lastGap = new float[Player.MaxSlots];
        readonly bool[] riding = new bool[Player.MaxSlots];
        float nextReport;
        int environmentMask;
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("NetSyncProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<NetSyncProbe>();
        }

        void Awake() => environmentMask = LayerMask.GetMask("Environment");

        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f7Key.wasPressedThisFrame) Visible = !Visible;
        }

        void LateUpdate()
        {
            if (!NetMode.IsNetworked) return;

            for (int s = 0; s < riding.Length; s++) riding[s] = false;
            foreach (var p in Player.All)
            {
                if (p == null || p.IsLocal || p.PlayerId < 0 || p.PlayerId >= Player.MaxSlots) continue;
                if (!TryMeasure(p, out int carrier, out float gap, out float world)) continue;
                riding[p.PlayerId] = true;
                lastGap[p.PlayerId] = gap;
                gaps[p.PlayerId].Add(carrier, gap, world);
            }

            if (Time.unscaledTime < nextReport) return;
            nextReport = Time.unscaledTime + ReportEvery;
            for (int s = 0; s < gaps.Length; s++)
            {
                var g = gaps[s];
                if (g.Count == 0) continue;
                PatataLog.Sync($"rider slot={s} carrier={g.Carrier} gap mean={g.Sum / g.Count * 100f:F1}cm " +
                               $"min={g.Min * 100f:F1}cm max={g.Max * 100f:F1}cm | replicated-only mean={g.WorldSum / g.Count * 100f:F1}cm " +
                               $"min={g.WorldMin * 100f:F1}cm max={g.WorldMax * 100f:F1}cm n={g.Count} rtt={NetMode.RttMs}ms");
                gaps[s] = default;
            }
        }

        /// <summary>
        /// The remote player's feet relative to the top of the moving carrier their owner stands on, as drawn here
        /// (<paramref name="gap"/>) and as their replicated world position alone would put them (<paramref name="world"/>).
        /// Only while the owner says it rides that carrier: a player merely under a lift is not a sync error.
        /// </summary>
        bool TryMeasure(Player p, out int carrier, out float gap, out float world)
        {
            carrier = 0;
            gap = world = 0f;
            if (p.Net == null || p.Net.Stamp.CarrierId == 0) return false;
            Vector3 feet = p.transform.position;
            if (!Physics.Raycast(feet + Vector3.up * RayStart, Vector3.down, out var hit, RayLength, environmentMask,
                    QueryTriggerInteraction.Ignore))
                return false;
            var c = hit.collider.GetComponentInParent<IPlatformCarrier>();
            if (c == null || c.CarrierId != p.Net.Stamp.CarrierId) return false;

            carrier = c.CarrierId;
            gap = feet.y - hit.point.y;
            world = p.Net.ReplicatedPosition.y - hit.point.y;
            return true;
        }

        static string RebuiltTag(int slot)
        {
            foreach (var p in Player.All)
                if (p != null && p.PlayerId == slot && p.Net != null && p.Net.RebuiltOnCarrier) return "  (rebuilt on carrier)";
            return "";
        }

        void OnGUI()
        {
            if (!Visible) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };

            var text = new System.Text.StringBuilder("NET SYNC (F7)\n");
            for (int s = 0; s < riding.Length; s++)
                if (riding[s]) text.Append($"slot {s} on carrier: gap {lastGap[s] * 100f:F1} cm{RebuiltTag(s)}\n");
            text.Append($"hazard hits (host): trigger {SyncStats.HazardTriggerHits}  rewound {SyncStats.HazardRewindHits}  static {SyncStats.StaticKills}\n");
            text.Append($"throw prediction: agree {SyncStats.ThrowAgreed}  disagree {SyncStats.ThrowDisagreed}   rtt {NetMode.RttMs} ms");

            var rect = new Rect(Screen.width - 430, 10, 420, 96);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, rect.height - 8), text.ToString(), style);
        }
    }
}
