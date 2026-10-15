using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// A geometric walk check (ARCHITECTURE §27; the motor's own run is the route runner's job): along each walked line, something to
    /// step onto past both ends within a step's height, no ledge ahead the controller cannot step up, and room for the body (the
    /// player's capsule, 1.8 m by 0.35 m) all the way, nothing but the walk's own rails and the trials beside it. Used by PatataRanger's
    /// build and by Verify Level.
    /// </summary>
    public static class WalkProbe
    {
        public const float BodyRadius = 0.35f, BodyHeight = 1.8f, StepHeight = 0.35f;

        /// <summary>One problem on a walk: what, where, and the collider at fault (if any).</summary>
        public struct Problem
        {
            public string message;
            public Vector3 at;
            public Collider blocker;
        }

        /// <summary>The problems found along <paramref name="walks"/> (name and walking surface each): empty when every walk is joined and clear.</summary>
        public static List<Problem> Run(IEnumerable<(string name, IList<Vector3> path)> walks)
        {
            var problems = new List<Problem>();
            foreach (var (name, path) in walks)
            {
                var pts = Curves.Resample(path, 0.5f);
                if (pts.Count < 2) continue;
                for (int e = 0; e < 2; e++)
                {
                    var end = e == 0 ? pts[0] : pts[pts.Count - 1];
                    var next = e == 0 ? pts[1] : pts[pts.Count - 2];
                    var dir = end - next;
                    dir.y = 0f;
                    dir.Normalize();
                    var probe = end + dir * 0.9f;
                    string which = e == 0 ? "start" : "end";
                    if (!SurfaceNear(probe, out float y))
                        problems.Add(new Problem { message = $"{name}: nothing to step onto past its {which} ({probe.x:F1}, {probe.y:F1}, {probe.z:F1})", at = probe });
                    else if (Mathf.Abs(y - end.y) > StepHeight)
                        problems.Add(new Problem { message = $"{name}: a {y - end.y:+0.00;-0.00} m step past its {which} ({probe.x:F1}, {probe.z:F1})", at = probe });
                }
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    // a ledge just ahead of the feet that the controller cannot step up (another piece's edge standing proud of the walk)
                    var q = pts[i];
                    var ahead = pts[i + 1] - q;
                    ahead.y = 0f;
                    var probe = q + ahead.normalized * 0.45f;
                    float expect = Mathf.Lerp(q.y, pts[i + 1].y, 0.9f);
                    foreach (var hit in Physics.RaycastAll(probe + Vector3.up * 1.2f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                        if (hit.normal.y > 0.6f && hit.point.y - expect > StepHeight - 0.03f && hit.point.y - expect < 1.2f
                            && hit.collider.GetComponentInParent<RotatingObstacle>() == null && hit.collider.GetComponentInParent<FallingPlatform>() == null)
                        {
                            problems.Add(new Problem { message = $"{name}: a {hit.point.y - expect:F2} m ledge ({hit.collider.transform.parent?.name}) ahead at ({q.x:F1}, {q.y:F1}, {q.z:F1})", at = q, blocker = hit.collider });
                            break;
                        }
                }
                foreach (var q in pts)
                {
                    var bottom = q + Vector3.up * (BodyRadius + 0.4f);
                    var top = q + Vector3.up * (BodyHeight - BodyRadius);
                    foreach (var c in Physics.OverlapCapsule(bottom, top, BodyRadius + 0.08f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (c.GetComponent<WalkableProxy>() != null || c is TerrainCollider) continue;
                        if (c.GetComponentInParent<SignalActuator>() != null || c.gameObject.layer == CourseKit.Layer("BodyScreen")) continue;
                        if (c.GetComponentInParent<RotatingObstacle>() != null || c.GetComponentInParent<FallingPlatform>() != null) continue;   // the trials themselves
                        if (c.gameObject.layer == CourseKit.Layer("Player") || c.gameObject.layer == CourseKit.Layer("Bomb")) continue;
                        problems.Add(new Problem { message = $"{name}: {c.name} ({c.transform.parent?.name}) is in the way at ({q.x:F1}, {q.y:F1}, {q.z:F1})", at = q, blocker = c });
                        break;
                    }
                }
            }
            return problems.GroupBy(p => p.message).Select(g => g.First()).ToList();
        }

        /// <summary>A walkable surface under <paramref name="p"/> (up to a step above it, down to 0.8 m below), its height.</summary>
        public static bool SurfaceNear(Vector3 p, out float y)
        {
            y = 0f;
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * (StepHeight + 0.4f), Vector3.down, StepHeight + 1.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.layer == CourseKit.Layer("Player") || hit.collider.gameObject.layer == CourseKit.Layer("Bomb")) continue;
                if (hit.normal.y < 0.6f) continue;
                float d = Mathf.Abs(hit.point.y - p.y);
                if (d < best) { best = d; y = hit.point.y; }
            }
            return best < float.MaxValue;
        }
    }
}
