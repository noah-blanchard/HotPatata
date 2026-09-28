using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HotPatata
{
    /// <summary>
    /// End-of-course screen: completion, the team's names, run time and reset count, plus rematch. Shown to everyone from the
    /// replicated run state; only the host (or an offline player) can restart, with R.
    /// </summary>
    public class RunResultsUI : MonoBehaviour
    {
        GUIStyle title, body;

        void Update()
        {
            var run = RunManager.Instance;
            if (run == null || run.State != RunState.Completed || !NetMode.IsAuthority) return;
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) run.Restart();
        }

        void OnGUI()
        {
            var run = RunManager.Instance;
            if (run == null || run.State != RunState.Completed) return;

            title ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.85f, 0.2f) } };
            body ??= new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

            float w = 460f, h = 264f;
            var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(box, GUIContent.none);

            string team = string.Join(",  ", Player.All.OrderBy(p => p.PlayerId).Select(p => p.DisplayName));
            int minutes = (int)(run.RunTime / 60f);
            float seconds = run.RunTime - minutes * 60f;
            GUI.Label(new Rect(box.x, box.y + 16, w, 50), "COURSE COMPLETE!", title);
            GUI.Label(new Rect(box.x, box.y + 78, w, 32), team, body);
            GUI.Label(new Rect(box.x, box.y + 112, w, 32), $"Time  {minutes}:{seconds:00.0}", body);
            GUI.Label(new Rect(box.x, box.y + 146, w, 32), $"Explosions / resets  {run.ResetCount}", body);
            GUI.Label(new Rect(box.x, box.y + 202, w, 32),
                NetMode.IsAuthority ? "Press R to play again" : "Waiting for the host to restart...", body);
        }
    }
}
