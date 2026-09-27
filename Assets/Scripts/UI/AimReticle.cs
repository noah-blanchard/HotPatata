using UnityEngine;

namespace Beep
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
            if (player == null) return;

            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            DrawCrosshair(cx, cy);

            if (player.Thrower.HoldsBomb) DrawChargeBar(player, cx, cy);
            else DrawCatchIndicator(player.Catcher, cx, cy);
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
