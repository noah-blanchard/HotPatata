using UnityEngine;

namespace Beep
{
    /// <summary>Minimal centre-screen aim marker (a plus sign) so throws can be aimed.</summary>
    public class AimReticle : MonoBehaviour
    {
        [SerializeField] Color color = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] float length = 14f;
        [SerializeField] float thickness = 2f;

        void OnGUI()
        {
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(cx - length * 0.5f, cy - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - length * 0.5f, thickness, length), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
