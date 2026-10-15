using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// PatataRanger (PROJECT_SPEC §15f, M17). The route runner: a player is driven with scripted input (the real motor, no noclip, no
    /// invincibility) along every walkway, stair and bridge the builder laid; it must reach the end of each without stalling or falling.
    /// A gate on the way is opened the way the course opens it: the partner stands on its plate. The bomb is kept from exploding while
    /// it runs (a traversal test); the timings it logs feed the trials' windows.
    /// </summary>
    public class PatataRangerTests : SandboxTestBase
    {
        protected override string SceneName => "PatataRanger";

        const float Reach = 0.9f, Stall = 4f, Fall = 2.6f;

        [UnityTest]
        public IEnumerator RouteRunner_WalksEveryWalkwayStairAndBridge()
        {
            DevCheats.NoExplosions = true;
            try
            {
                var walks = Object.FindFirstObjectByType<RouteWalks>();
                Assert.IsNotNull(walks, "the course keeps its walked lines");
                Assert.Greater(walks.Lines.Count, 10, "every walkway, stair and bridge is listed");
                var failures = new List<string>();
                var report = new StringBuilder();
                foreach (var line in walks.Lines) yield return Walk(line, failures, report);
                Debug.Log("[RangerRouteRunner] timings\n" + report);
                Assert.IsEmpty(failures, string.Join("\n", failures));
            }
            finally { DevCheats.NoExplosions = false; }
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        IEnumerator Walk(RouteWalks.Line line, List<string> failures, StringBuilder report)
        {
            var pts = line.points;
            if (pts.Length < 2) yield break;
            OpenGatesOn(pts);
            var start = pts[0];
            var dir = pts[1] - pts[0];
            dir.y = 0f;
            p1.TeleportTo(start + Vector3.up * 0.25f, Quaternion.LookRotation(dir.normalized));
            p1.Look.SetAim(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
            float landBy = Time.realtimeSinceStartup + 2f;
            while (!p1.Motor.Grounded && Time.realtimeSinceStartup < landBy) yield return null;
            if (!p1.Motor.Grounded)
            {
                failures.Add($"{line.name}: never landed at its start {start}");
                yield break;
            }
            int target = 1;
            float t0 = Time.time, lastProgress = Time.time;
            float length = 0f;
            for (int i = 1; i < pts.Length; i++) length += Vector3.Distance(pts[i - 1], pts[i]);
            while (true)
            {
                var pos = p1.transform.position;
                int before = target;
                while (target < pts.Length - 1 && Flat(pts[target] - pos) < Reach) target++;
                if (target != before) lastProgress = Time.time;
                if (target == pts.Length - 1 && Flat(pts[target] - pos) < Reach) break;
                var aim = pts[Mathf.Min(target + 2, pts.Length - 1)] - pos;
                aim.y = 0f;
                if (aim.sqrMagnitude > 1e-4f) p1.Look.SetAim(Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg, 0f);
                Drive.Move = new Vector2(0f, 1f);
                if (p1.Motor.Grounded && SweeperComing(pos)) Drive.PressJump();
                float floor = Mathf.Min(pts[target].y, pts[Mathf.Max(0, target - 1)].y);
                if (pos.y < floor - Fall)
                {
                    failures.Add($"{line.name}: fell at ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}), point {target}/{pts.Length}");
                    break;
                }
                if (Time.time - lastProgress > Stall)
                {
                    failures.Add($"{line.name}: stuck at ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}), point {target}/{pts.Length}");
                    break;
                }
                yield return null;
            }
            Drive.Move = Vector2.zero;
            report.AppendLine($"{line.name}: {Time.time - t0:F1} s for {length:F0} m");
            yield return null;
        }

        /// <summary>A sweeping bar (a lethal rotator) about to reach the walker's feet: jump it, as a player would.</summary>
        static bool SweeperComing(Vector3 pos)
        {
            foreach (var c in Physics.OverlapSphere(pos + Vector3.up * 0.5f, 1.3f, ~0, QueryTriggerInteraction.Collide))
                if (c.GetComponent<KillZone>() != null && c.GetComponentInParent<RotatingObstacle>() != null) return true;
            return false;
        }

        /// <summary>Puts player 2 on the plate of every actuator standing across the line (a gate is opened from its plate).</summary>
        void OpenGatesOn(Vector3[] pts)
        {
            var actuators = new HashSet<SignalActuator>();
            foreach (var q in pts)
                foreach (var c in Physics.OverlapSphere(q + Vector3.up * 1f, 0.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var a = c.GetComponentInParent<SignalActuator>();
                    if (a != null) actuators.Add(a);
                }
            foreach (var a in actuators)
                if (a.Source is PressurePlate plate)
                    p2.TeleportTo(plate.transform.position + Vector3.up * 0.2f, Quaternion.identity);
        }
    }
}
