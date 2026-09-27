using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beep
{
    /// <summary>
    /// Development only (F3 toggles). Draws the throw/catch model in the game view so it can be tuned by eye:
    /// raw aim, raw vs assisted launch, the assist cone, the selected receiver, the predicted arcs, every catch
    /// volume (green while its window is open), and the last flight with its closest approach to each player.
    /// A text panel gives the numbers. Lines are drawn with GL after the camera renders (works under URP).
    /// This is a tuning tool, not the player-facing UI: the game itself shows no trajectory.
    /// </summary>
    public class ThrowDebugOverlay : MonoBehaviour
    {
        public static bool Visible;

        const float TraceStep = 0.02f, TraceTime = 3f, BombRadius = 0.15f;

        static readonly Color RawAim = new Color(1f, 1f, 1f, 0.7f);
        static readonly Color RawArc = new Color(0.65f, 0.65f, 0.65f, 0.8f);
        static readonly Color AssistArc = new Color(0.3f, 1f, 0.4f, 1f);
        static readonly Color ConeColor = new Color(1f, 0.8f, 0.2f, 0.6f);
        static readonly Color TargetColor = new Color(1f, 0.55f, 0.1f, 1f);
        static readonly Color VolumeIdle = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color VolumeOpen = new Color(0.2f, 1f, 0.35f, 0.9f);
        static readonly Color FlightColor = new Color(0.2f, 0.85f, 1f, 1f);
        static readonly Color HitColor = new Color(0.2f, 1f, 0.35f, 1f);
        static readonly Color MissColor = new Color(1f, 0.25f, 0.2f, 1f);

        readonly List<Vector3> rawPoints = new List<Vector3>();
        readonly List<Vector3> assistPoints = new List<Vector3>();
        Material lineMaterial;
        GUIStyle style;
        int traceMask;

        void Awake() => traceMask = LayerMask.GetMask("Environment", "Hazard");

        void OnEnable() => RenderPipelineManager.endCameraRendering += OnEndCamera;
        void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCamera;

        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) Visible = !Visible;
        }

        static Player Viewed => FirstPersonCamera.Instance != null ? FirstPersonCamera.Instance.Target : null;

        // ------------------------------------------------------------------ world lines

        void OnEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (!Visible || FirstPersonCamera.Instance == null || cam != FirstPersonCamera.Instance.GetComponent<Camera>()) return;
            if (!EnsureMaterial()) return;

            lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            GL.Begin(GL.LINES);

            DrawCatchVolumes();
            DrawLastFlight();
            var player = Viewed;
            if (player != null && player.Thrower.Preview.Valid) DrawAim(player, player.Thrower.Preview);

            GL.End();
            GL.PopMatrix();
        }

        void DrawAim(Player player, ThrowShot shot)
        {
            var t = player.Tuning;
            float g = ThrowBallistics.Gravity(t);
            Vector3 eye = player.CameraTarget.position;

            Line(eye + shot.AimForward * 0.5f, eye + shot.AimForward * 25f, RawAim);

            ThrowBallistics.Trace(shot.Origin, shot.RawVelocity, g, TraceTime, TraceStep, traceMask, BombRadius, rawPoints, out _);
            Polyline(rawPoints, RawArc);
            if (shot.HasAssist)
            {
                ThrowBallistics.Trace(shot.Origin, shot.Velocity, g, TraceTime, TraceStep, traceMask, BombRadius, assistPoints, out _);
                Polyline(assistPoints, AssistArc);
                Box(shot.AssistTarget.CatchVolume.CatchCenter, 0.45f, TargetColor);
            }

            // Assist cone: a ring around the aim ray at the target's distance (or 10 m).
            float distance = shot.HasAssist ? Vector3.Distance(eye, shot.AssistTarget.CatchVolume.CatchCenter) : 10f;
            Ring(eye + shot.AimForward * distance, shot.AimForward, distance * Mathf.Tan(t.aimAssistAngle * Mathf.Deg2Rad), ConeColor);
        }

        void DrawCatchVolumes()
        {
            var bomb = BombController.Instance;
            foreach (var p in Player.All)
            {
                if (p == null || p.CatchVolume == null || (bomb != null && bomb.Carrier == p)) continue;
                var color = p.Catcher.WindowOpen ? VolumeOpen : VolumeIdle;
                WireSphere(p.CatchVolume.CatchCenter, p.Tuning.catchRadius, color);
            }
        }

        void DrawLastFlight()
        {
            var telemetry = ThrowTelemetry.Instance;
            if (telemetry == null || telemetry.Path.Count < 2) return;

            Polyline(telemetry.Path, FlightColor);
            foreach (var p in Player.All)
            {
                if (p == null || p.PlayerId < 0 || p.PlayerId >= telemetry.Closest.Length) continue;
                var c = telemetry.Closest[p.PlayerId];
                if (!c.Valid || c.Distance > 4f) continue;
                Line(c.BombPoint, c.Center, c.Distance <= p.Tuning.catchRadius ? HitColor : MissColor);
            }
        }

        // ------------------------------------------------------------------ text panel

        void OnGUI()
        {
            if (!Visible) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, normal = { textColor = Color.white } };

            var lines = new System.Text.StringBuilder("THROW DEBUG (F3)   ");
            lines.Append(NetMode.IsNetworked ? "\n" : $"partner bot (F4): {(PassPartner.Active ? "ON" : "off")}   moves (F5): {PlayerBot.Movement}\n");
            var player = Viewed;
            if (player != null && player.Thrower.Preview.Valid)
            {
                var s = player.Thrower.Preview;
                float horizontal = new Vector2(s.Velocity.x, s.Velocity.z).magnitude;
                lines.Append($"charge {s.Charge01:F2}   speed {s.Velocity.magnitude:F1} m/s   elev {Mathf.Atan2(s.Velocity.y, horizontal) * Mathf.Rad2Deg:F1}°\n");
                lines.Append(s.HasAssist
                    ? $"assist -> {s.AssistTarget}  off {s.AssistAngle:F1}°  turned {s.AssistCorrection:F1}°{(s.AssistYawOnly ? "  YAW ONLY (too weak)" : "")}\n"
                    : "assist: none\n");
            }
            else if (player != null)
            {
                lines.Append($"catch window {(player.Catcher.WindowOpen ? "OPEN" : player.Catcher.OnCooldown ? "cooldown" : "ready")}\n");
            }

            var telemetry = ThrowTelemetry.Instance;
            if (telemetry != null && telemetry.LastSummary != null) lines.Append("last: ").Append(telemetry.LastSummary);

            var rect = new Rect(Screen.width - 470, 10, 460, 130);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, rect.height - 8), lines.ToString(), style);
        }

        // ------------------------------------------------------------------ GL helpers

        bool EnsureMaterial()
        {
            if (lineMaterial != null) return true;
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return false;
            lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
            lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);   // see it through walls
            return true;
        }

        static void Line(Vector3 a, Vector3 b, Color c)
        {
            GL.Color(c);
            GL.Vertex(a);
            GL.Vertex(b);
        }

        static void Polyline(List<Vector3> points, Color c)
        {
            for (int i = 1; i < points.Count; i++) Line(points[i - 1], points[i], c);
        }

        static void Ring(Vector3 center, Vector3 normal, float radius, Color c, int segments = 32)
        {
            Vector3 u = Vector3.Cross(normal, Vector3.up);
            if (u.sqrMagnitude < 1e-4f) u = Vector3.Cross(normal, Vector3.right);
            u.Normalize();
            Vector3 v = Vector3.Cross(normal, u).normalized;
            Vector3 prev = center + u * radius;
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 next = center + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius;
                Line(prev, next, c);
                prev = next;
            }
        }

        static void WireSphere(Vector3 center, float radius, Color c)
        {
            Ring(center, Vector3.up, radius, c);
            Ring(center, Vector3.right, radius, c);
            Ring(center, Vector3.forward, radius, c);
        }

        static void Box(Vector3 center, float half, Color c)
        {
            for (int i = 0; i < 4; i++)
            {
                float x0 = (i & 1) == 0 ? -half : half, z0 = (i & 2) == 0 ? -half : half;
                Line(center + new Vector3(x0, -half, z0), center + new Vector3(x0, half, z0), c);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                float y = s * half;
                Line(center + new Vector3(-half, y, -half), center + new Vector3(half, y, -half), c);
                Line(center + new Vector3(half, y, -half), center + new Vector3(half, y, half), c);
                Line(center + new Vector3(half, y, half), center + new Vector3(-half, y, half), c);
                Line(center + new Vector3(-half, y, half), center + new Vector3(-half, y, -half), c);
            }
        }
    }
}
