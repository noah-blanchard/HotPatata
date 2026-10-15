using System.Collections;
using System.Linq;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// HotPatata/Level/Run Route Runner in Play (ARCHITECTURE §27): once both players stand on the ground, player 1 is driven along every
    /// line of the level's <see cref="RouteWalks"/> (the bomb kept from exploding), and the result is logged: each walk's time, then
    /// what fell or stalled. Player 1 is handed back afterwards. Editor and development builds only.
    /// </summary>
    public sealed class RouteRunSession : MonoBehaviour
    {
        IEnumerator Start()
        {
            Player p1 = null, p2 = null;
            float giveUp = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < giveUp)
            {
                var players = FindObjectsByType<Player>(FindObjectsSortMode.None).OrderBy(p => p.name).ToArray();
                if (players.Length > 0 && players.All(p => p.Motor.Grounded))
                {
                    p1 = players[0];
                    p2 = players.Length > 1 ? players[1] : null;
                    break;
                }
                yield return null;
            }
            var walks = FindFirstObjectByType<RouteWalks>();
            if (p1 == null || walks == null || walks.Lines.Count == 0)
            {
                Debug.LogWarning("[RouteRunner] nothing to walk: " + (p1 == null ? "no player landed" : "no walk lines (save the level first)"));
                Destroy(gameObject);
                yield break;
            }
            var drive = new PlayerInputReader.ScriptedInput();
            p1.Input.Scripted = drive;
            DevCheats.NoExplosions = true;
            var runner = new RouteRunner(p1, p2, drive);
            try
            {
                yield return runner.WalkAll(walks);
            }
            finally
            {
                DevCheats.NoExplosions = false;
                p1.Input.Scripted = null;
            }
            string timings = "[RouteRunner] timings\n" + runner.Report;
            if (runner.Failures.Count == 0) Debug.Log($"[RouteRunner] every line walked ({walks.Lines.Count}).\n{timings}");
            else Debug.LogWarning($"[RouteRunner] {runner.Failures.Count} of {walks.Lines.Count} line(s) failed:\n- " + string.Join("\n- ", runner.Failures) + "\n" + timings);
            Destroy(gameObject);
        }
    }
}
