using UnityEngine;

namespace Beep
{
    /// <summary>Prototype-only overlay: run/bomb state, carrier, fuse bar and the key bindings.</summary>
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] LocalPlayerSwitcher switcher;
        [SerializeField] bool visible = true;

        GUIStyle style;

        void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible || run == null) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };

            var bomb = run.Bomb;
            float fuse = bomb.Fuse.Duration > 0f ? bomb.Fuse.Remaining / bomb.Fuse.Duration : 0f;

            GUI.Box(new Rect(10, 10, 360, 146), GUIContent.none);
            GUI.Label(new Rect(18, 14, 290, 22), $"Run: {run.State}  resets: {run.ResetCount}  cp: {(run.CurrentCheckpoint != null ? run.CurrentCheckpoint.Id.ToString() : "-")}  {run.RunTime:F0}s", style);
            GUI.Label(new Rect(18, 34, 290, 22), $"Bomb: {bomb.State}   carrier: {(bomb.Carrier != null ? bomb.Carrier.DisplayName : "-")}", style);
            GUI.Label(new Rect(18, 54, 290, 22), $"Controlling: {(switcher != null && switcher.Focused != null ? switcher.Focused.DisplayName : "-")}", style);

            GUI.color = Color.Lerp(Color.red, Color.green, fuse);
            GUI.DrawTexture(new Rect(18, 80, 280 * Mathf.Clamp01(fuse), 8), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(18, 92, 350, 64), "WASD move  Space jump\nHold LMB charge, release to throw\nRMB catch (time it!)   Tab switch player   F1 hide", style);
        }
    }
}
