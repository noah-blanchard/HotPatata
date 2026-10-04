using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Centre-screen feedback for the local player, drawn with IMGUI (prototype UI):
    ///  - crosshair;
    ///  - while holding the bomb: a charge bar (fills as you hold the throw button) with the launch speed
    ///    and an estimated level range;
    ///  - while not holding it: catch brackets. Faint = ready, big green = catch window open,
    ///    small red with a shrinking bar = cooldown. State also changes size, not just colour.
    /// </summary>
    public class AimReticle : MonoBehaviour
    {
        [SerializeField] FirstPersonCamera cam;
        [SerializeField] Color crosshairColor = new Color(1f, 1f, 1f, 0.9f);

        GUIStyle label;

        void OnGUI()
        {
            var player = cam != null ? cam.Target : null;
            if (player == null || ScreenStack.BlocksGameplay) return;   // a menu on top: no aim feedback over it

            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            DrawCrosshair(cx, cy);

            if (player.Thrower.HoldsBomb)
            {
                DrawChargeBar(player, cx, cy);
                DrawLockFrame(player);
            }
            else
            {
                DrawCatchIndicator(player.Catcher, cx, cy);
                DrawIncoming(player);
            }

            string hint = player.Catcher.Hint;
            if (hint != null)
            {
                label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 14 };
                label.normal.textColor = new Color(1f, 0.85f, 0.3f);
                GUI.Label(new Rect(cx - 200, cy + 62, 400, 24), hint, label);
            }
        }

        Camera ViewCamera => cam != null ? cam.GetComponent<Camera>() : null;

        /// <summary>
        /// Corner brackets around the receiver the aim assist would help if you released now. Fainter = weaker help;
        /// red = your current charge is too weak to reach them (the assist will not add range).
        /// </summary>
        void DrawLockFrame(Player player)
        {
            var shot = player.Thrower.Preview;
            var target = shot.AssistTarget;
            var view = ViewCamera;
            if (target == null || view == null) return;

            Vector3 s = view.WorldToScreenPoint(target.CatchVolume.CatchCenter);
            if (s.z <= 0f) return;

            float q = Mathf.Clamp01(shot.AssistStrength / Mathf.Max(0.01f, player.Tuning.assistStrength));
            var c = shot.AssistYawOnly
                ? new Color(1f, 0.25f, 0.2f, 0.9f)
                : Color.Lerp(new Color(1f, 1f, 1f, 0.35f), new Color(1f, 0.6f, 0.1f, 1f), q);
            float size = Mathf.Clamp(900f / s.z, 30f, 90f);
            Brackets(s.x, Screen.height - s.y, size, size * 0.35f, 3f, c);
        }

        /// <summary>A pulsing marker on the bomb while it is coming for you, so you know when to press catch.</summary>
        void DrawIncoming(Player player)
        {
            var bomb = BombController.Instance;
            var view = ViewCamera;
            if (bomb == null || view == null || bomb.State != BombState.Thrown || bomb.IntendedReceiver != player) return;

            Vector3 s = view.WorldToScreenPoint(bomb.transform.position);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
            var c = new Color(1f, 0.25f + 0.3f * pulse, 0.1f, 1f);
            label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 14 };
            label.normal.textColor = c;

            if (s.z > 0f)
            {
                float size = 34f + 14f * pulse;
                float x = Mathf.Clamp(s.x, 30f, Screen.width - 30f), y = Mathf.Clamp(Screen.height - s.y, 30f, Screen.height - 30f);
                Brackets(x, y, size, size * 0.4f, 3f, c);
                GUI.Label(new Rect(x - 80, y + size + 4f, 160, 22), "CATCH!", label);
            }
            else
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 120, Screen.height - 90f, 240, 24), "INCOMING - BEHIND YOU!", label);
            }
        }

        void DrawCrosshair(float cx, float cy)
        {
            const float length = 14f, thickness = 2f;
            Fill(new Rect(cx - length * 0.5f, cy - thickness * 0.5f, length, thickness), crosshairColor);
            Fill(new Rect(cx - thickness * 0.5f, cy - length * 0.5f, thickness, length), crosshairColor);
        }

        void DrawChargeBar(Player player, float cx, float cy)
        {
            var t = player.Tuning;
            float charge = player.Thrower.Charge01;
            const float w = 180f, h = 12f;
            float x = cx - w * 0.5f, y = cy + 58f;

            Fill(new Rect(x - 2, y - 2, w + 4, h + 4), new Color(0f, 0f, 0f, 0.6f));
            Color fill = Color.Lerp(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.25f, 0.1f), charge);
            Fill(new Rect(x, y, w * charge, h), fill);
            for (int i = 1; i < 4; i++)   // quarter marks
                Fill(new Rect(x + w * i * 0.25f - 1, y, 2, h), new Color(0f, 0f, 0f, 0.55f));

            float speed = PlayerThrower.SpeedFor(t, charge);
            float g = -Physics.gravity.y * t.bombGravityScale;
            float range = speed * speed * Mathf.Sin(2f * t.throwUpAngle * Mathf.Deg2Rad) / g;   // level-ground estimate

            label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 14 };
            label.normal.textColor = Color.white;
            string text = player.Thrower.Charging ? $"{speed:F0} m/s   ~{range:F0} m" : "hold to charge";
            GUI.Label(new Rect(cx - 120, y + h + 2, 240, 22), text, label);
        }

        void DrawCatchIndicator(PlayerCatcher catcher, float cx, float cy)
        {
            if (catcher.WindowOpen)
            {
                Brackets(cx, cy, 34f, 14f, 4f, new Color(0.2f, 1f, 0.35f, 1f));
            }
            else if (catcher.OnCooldown)
            {
                Brackets(cx, cy, 20f, 7f, 2f, new Color(1f, 0.3f, 0.25f, 0.85f));
                float w = 40f * catcher.CooldownRemaining01;
                Fill(new Rect(cx - w * 0.5f, cy + 30f, w, 3f), new Color(1f, 0.3f, 0.25f, 0.85f));
            }
            else
            {
                Brackets(cx, cy, 20f, 7f, 2f, new Color(1f, 1f, 1f, 0.3f));
            }
        }

        static void Brackets(float cx, float cy, float half, float len, float th, Color c)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    float px = cx + sx * half, py = cy + sy * half;
                    Fill(new Rect(sx < 0 ? px : px - len, sy < 0 ? py : py - th, len, th), c);   // horizontal arm
                    Fill(new Rect(sx < 0 ? px : px - th, sy < 0 ? py : py - len, th, len), c);   // vertical arm
                }
            }
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
