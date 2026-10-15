using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The route runner (docs/TESTING.md, ARCHITECTURE §27): a player driven with scripted input (the real motor, no noclip, no
    /// invincibility) along every line of a level's <see cref="RouteWalks"/> (each walkway, stair and bridge); it must reach the end of
    /// each without stalling or falling. A gate on the way is opened the way the level opens it: the partner stands on its plate; a
    /// sweeping bar is jumped. Used by the course tests (PatataRanger, the authored levels) and by HotPatata/Level/Run Route Runner.
    /// Editor and development builds only.
    /// </summary>
    public sealed class RouteRunner
    {
        public const float Reach = 0.9f, Stall = 4f, Fall = 2.6f;

        readonly Player walker, partner;
        readonly PlayerInputReader.ScriptedInput drive;

        /// <summary>What went wrong, one line per walk that failed.</summary>
        public readonly List<string> Failures = new List<string>();
        /// <summary>How long each walk took.</summary>
        public readonly StringBuilder Report = new StringBuilder();

        public RouteRunner(Player walker, Player partner, PlayerInputReader.ScriptedInput drive)
        {
            this.walker = walker;
            this.partner = partner;
            this.drive = drive;
        }

        /// <summary>Walks every line (a coroutine).</summary>
        public IEnumerator WalkAll(RouteWalks walks)
        {
            foreach (var line in walks.Lines) yield return Walk(line);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        /// <summary>Walks one line (a coroutine): a failure is added when the walker falls or stalls.</summary>
        public IEnumerator Walk(RouteWalks.Line line)
        {
            var pts = line.points;
            if (pts.Length < 2) yield break;
            OpenGatesOn(pts);
            var start = pts[0];
            var dir = pts[1] - pts[0];
            dir.y = 0f;
            walker.TeleportTo(start + Vector3.up * 0.25f, Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward));
            walker.Look.SetAim(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
            float landBy = Time.realtimeSinceStartup + 2f;
            while (!walker.Motor.Grounded && Time.realtimeSinceStartup < landBy) yield return null;
            if (!walker.Motor.Grounded)
            {
                Failures.Add($"{line.name}: never landed at its start {start}");
                yield break;
            }
            int target = 1;
            float t0 = Time.time, lastProgress = Time.time;
            float length = 0f;
            for (int i = 1; i < pts.Length; i++) length += Vector3.Distance(pts[i - 1], pts[i]);
            while (true)
            {
                var pos = walker.transform.position;
                int before = target;
                while (target < pts.Length - 1 && Flat(pts[target] - pos) < Reach) target++;
                if (target != before) lastProgress = Time.time;
                if (target == pts.Length - 1 && Flat(pts[target] - pos) < Reach) break;
                var aim = pts[Mathf.Min(target + 2, pts.Length - 1)] - pos;
                aim.y = 0f;
                if (aim.sqrMagnitude > 1e-4f) walker.Look.SetAim(Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg, 0f);
                drive.Move = new Vector2(0f, 1f);
                if (walker.Motor.Grounded && SweeperComing(pos)) drive.PressJump();
                float floor = Mathf.Min(pts[target].y, pts[Mathf.Max(0, target - 1)].y);
                if (pos.y < floor - Fall)
                {
                    Failures.Add($"{line.name}: fell at ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}), point {target}/{pts.Length}");
                    break;
                }
                if (Time.time - lastProgress > Stall)
                {
                    Failures.Add($"{line.name}: stuck at ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}), point {target}/{pts.Length}");
                    break;
                }
                yield return null;
            }
            drive.Move = Vector2.zero;
            Report.AppendLine($"{line.name}: {Time.time - t0:F1} s for {length:F0} m");
            yield return null;
        }

        /// <summary>A sweeping bar (a lethal rotator) about to reach the walker's feet: jump it, as a player would.</summary>
        static bool SweeperComing(Vector3 pos)
        {
            foreach (var c in Physics.OverlapSphere(pos + Vector3.up * 0.5f, 1.3f, ~0, QueryTriggerInteraction.Collide))
                if (c.GetComponent<KillZone>() != null && c.GetComponentInParent<RotatingObstacle>() != null) return true;
            return false;
        }

        /// <summary>Puts the partner on the plate of every actuator standing across the line (a gate is opened from its plate).</summary>
        void OpenGatesOn(Vector3[] pts)
        {
            if (partner == null) return;
            var actuators = new HashSet<SignalActuator>();
            foreach (var q in pts)
                foreach (var c in Physics.OverlapSphere(q + Vector3.up * 1f, 0.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var a = c.GetComponentInParent<SignalActuator>();
                    if (a != null) actuators.Add(a);
                }
            foreach (var a in actuators)
                if (a.Source is PressurePlate plate)
                    partner.TeleportTo(plate.transform.position + Vector3.up * 0.2f, Quaternion.identity);
        }
    }
}
