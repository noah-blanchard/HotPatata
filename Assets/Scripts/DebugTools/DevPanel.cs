using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HotPatata
{
    /// <summary>
    /// Development only: the map-testing tools (docs/TESTING.md §2). F8 opens the panel; while it is open, 1–7 use it:
    ///   1 noclip (fly, also F9 at any time)   2 invincible   3 no explosions   4 show kill zones, checkpoints, carriers
    ///   5 previous checkpoint   6 next checkpoint   7 give me the bomb
    /// The cheats themselves live in <see cref="DevCheats"/>. Noclip is the local player's own; the others are rules, so
    /// they act offline or on the host (a client sees them as host-only). A banner says when any cheat is on, panel closed
    /// or not, so a playtest is never run with one by mistake. Created automatically in the Editor and development builds.
    /// </summary>
    public class DevPanel : MonoBehaviour
    {
        const float RescanEvery = 1f;   // seconds between scans for zones to draw

        public static bool Visible;
        public static bool ShowZones;

        static readonly Color KillColor = new Color(1f, 0.2f, 0.2f, 0.9f);
        static readonly Color CheckpointColor = new Color(0.3f, 1f, 0.4f, 0.9f);
        static readonly Color CarrierColor = new Color(0.3f, 0.8f, 1f, 0.9f);

        readonly List<Collider> kills = new List<Collider>();
        readonly List<Collider> checkpoints = new List<Collider>();
        readonly List<Collider> carriers = new List<Collider>();
        float nextScan;
        Material lineMaterial;
        GUIStyle style, banner;
        string lastAction = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("DevPanel");
            DontDestroyOnLoad(go);
            go.AddComponent<DevPanel>();
        }

        void OnEnable() => RenderPipelineManager.endCameraRendering += DrawZones;

        void OnDisable() => RenderPipelineManager.endCameraRendering -= DrawZones;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f8Key.wasPressedThisFrame) Visible = !Visible;
            if (kb.f9Key.wasPressedThisFrame) ToggleNoClip();
            if (!Visible) return;

            if (kb.digit1Key.wasPressedThisFrame) ToggleNoClip();
            if (kb.digit2Key.wasPressedThisFrame && RulesHere("invincible")) Set("invincible", DevCheats.Invincible = !DevCheats.Invincible);
            if (kb.digit3Key.wasPressedThisFrame && RulesHere("no explosions")) Set("no explosions", DevCheats.NoExplosions = !DevCheats.NoExplosions);
            if (kb.digit4Key.wasPressedThisFrame) Set("show zones", ShowZones = !ShowZones);
            if (kb.digit5Key.wasPressedThisFrame && RulesHere("checkpoint warp")) Warp(-1);
            if (kb.digit6Key.wasPressedThisFrame && RulesHere("checkpoint warp")) Warp(+1);
            if (kb.digit7Key.wasPressedThisFrame && RulesHere("give bomb")) GiveBomb();
        }

        void ToggleNoClip() => Set("noclip", DevCheats.NoClip = !DevCheats.NoClip);

        void Set(string what, bool on)
        {
            lastAction = $"{what} {(on ? "ON" : "off")}";
            PatataLog.Run($"[Dev] {lastAction}");
        }

        /// <summary>Rule cheats act where the rules run: offline or on the host.</summary>
        bool RulesHere(string what)
        {
            if (NetMode.IsAuthority) return true;
            lastAction = $"{what}: host only";
            return false;
        }

        static Player LocalPlayer()
        {
            var target = FirstPersonCamera.Instance != null ? FirstPersonCamera.Instance.Target : null;
            if (target != null && target.IsLocal) return target;
            foreach (var p in Player.All)
                if (p != null && p.IsLocal) return p;
            return null;
        }

        /// <summary>Starts the run again at the previous or next checkpoint (the same path as -patataCheckpoint).</summary>
        void Warp(int step)
        {
            var run = RunManager.Instance;
            if (run == null) return;
            var ids = new List<int> { 0 };
            foreach (var cp in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                if (cp.Id > 0 && !ids.Contains(cp.Id)) ids.Add(cp.Id);
            ids.Sort();
            int current = run.CurrentCheckpoint != null ? run.CurrentCheckpoint.Id : 0;
            int index = ids.IndexOf(current);
            if (index < 0) index = 0;
            int target = ids[Mathf.Clamp(index + step, 0, ids.Count - 1)];
            RunOptions.StartCheckpoint = target;
            run.Restart();
            lastAction = target == 0 ? "warped to the start" : $"warped to checkpoint {target}";
            PatataLog.Run($"[Dev] {lastAction}");
        }

        void GiveBomb()
        {
            var bomb = BombController.Instance;
            var me = LocalPlayer();
            if (bomb == null || me == null) return;
            bomb.BeginReset();
            bomb.EndReset(me);
            lastAction = "bomb given";
            PatataLog.Run($"[Dev] bomb given to {me}");
        }

        // ------------------------------------------------------------------ zones

        void Rescan()
        {
            kills.Clear();
            checkpoints.Clear();
            carriers.Clear();
            foreach (var z in FindObjectsByType<KillZone>(FindObjectsSortMode.None))
                if (z.TryGetComponent(out Collider c)) kills.Add(c);
            foreach (var cp in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                foreach (var c in cp.GetComponentsInChildren<Collider>())
                    if (c.isTrigger) checkpoints.Add(c);
            foreach (var carrier in CarrierRegistry.All)
                if (carrier is Component mb)
                    foreach (var c in mb.GetComponentsInChildren<Collider>())
                        if (!c.isTrigger) carriers.Add(c);
        }

        void DrawZones(ScriptableRenderContext context, Camera cam)
        {
            if (!ShowZones || cam.cameraType != CameraType.Game) return;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + RescanEvery;
                Rescan();
            }
            if (lineMaterial == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");   // Unity's built-in line shader
                if (shader == null) return;
                lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);   // seen through walls
            }

            GL.PushMatrix();
            lineMaterial.SetPass(0);
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            GL.Begin(GL.LINES);
            DrawAll(kills, KillColor);
            DrawAll(checkpoints, CheckpointColor);
            DrawAll(carriers, CarrierColor);
            GL.End();
            GL.PopMatrix();
        }

        static void DrawAll(List<Collider> colliders, Color color)
        {
            GL.Color(color);
            foreach (var c in colliders)
                if (c != null && c.enabled && c.gameObject.activeInHierarchy) DrawBox(c);
        }

        static readonly Vector3[] Corners = new Vector3[8];

        /// <summary>A box collider as its oriented box, anything else as its world bounds.</summary>
        static void DrawBox(Collider c)
        {
            if (c is BoxCollider b)
            {
                Vector3 h = b.size * 0.5f;
                for (int i = 0; i < 8; i++)
                    Corners[i] = b.transform.TransformPoint(b.center + new Vector3((i & 1) != 0 ? h.x : -h.x, (i & 2) != 0 ? h.y : -h.y, (i & 4) != 0 ? h.z : -h.z));
            }
            else
            {
                Bounds bb = c.bounds;
                Vector3 h = bb.extents;
                for (int i = 0; i < 8; i++)
                    Corners[i] = bb.center + new Vector3((i & 1) != 0 ? h.x : -h.x, (i & 2) != 0 ? h.y : -h.y, (i & 4) != 0 ? h.z : -h.z);
            }
            for (int i = 0; i < 8; i++)
                for (int bit = 1; bit < 8; bit <<= 1)
                    if ((i & bit) == 0)
                    {
                        GL.Vertex(Corners[i]);
                        GL.Vertex(Corners[i | bit]);
                    }
        }

        // ------------------------------------------------------------------ panel

        void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            banner ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.85f, 0.2f) } };

            if (DevCheats.Any)
            {
                string on = (DevCheats.NoClip ? "NOCLIP  " : "") + (DevCheats.Invincible ? "INVINCIBLE  " : "") + (DevCheats.NoExplosions ? "NO EXPLOSIONS" : "");
                GUI.Label(new Rect(Screen.width * 0.5f - 250f, 6f, 500f, 24f), "DEV CHEATS: " + on.Trim(), banner);
            }
            if (!Visible) return;

            bool rules = NetMode.IsAuthority;
            string host = rules ? "" : "  (host only)";
            var run = RunManager.Instance;
            string cp = run != null && run.CurrentCheckpoint != null ? run.CurrentCheckpoint.Id.ToString() : "start";
            string text =
                "DEV TOOLS (F8)\n" +
                $"1  noclip (F9)          {(DevCheats.NoClip ? "ON" : "off")}   fly: look + WASD, Space up, Crouch down, Sprint x{DevCheats.NoClipBoost:F0}\n" +
                $"2  invincible           {(DevCheats.Invincible ? "ON" : "off")}{host}\n" +
                $"3  no explosions        {(DevCheats.NoExplosions ? "ON" : "off")}{host}\n" +
                $"4  show zones           {(ShowZones ? "ON" : "off")}   red kill, green checkpoint, blue carrier\n" +
                $"5 / 6  checkpoint  <  {cp}  >{host}\n" +
                $"7  give me the bomb{host}\n" +
                lastAction;

            var rect = new Rect(10f, Screen.height - 190f, 560f, 180f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, rect.height - 8f), text, style);
        }
    }
}
