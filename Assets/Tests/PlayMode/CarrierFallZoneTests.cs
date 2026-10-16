using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// No kill zone lies across the travel of a lift, a bridge or a door a player can stand on (#97). The pulleys of PatataCanopy and
    /// the well of PatataTemple (archived, docs/MAP_ARCHIVE.md) climbed through the next section's fall zone, which lay beside them past the turn: whoever rode them
    /// "fell" a few decimetres up and the team reset. A carrier that retracts into the void (its lowest pose inside a kill zone, a
    /// rising bridge) is never stood on there, so it may cross one.
    /// </summary>
    public class CarrierFallZoneTests
    {
        const float StandingHeight = 2.2f;   // a player standing on the deck, head and catch volume included

        static IEnumerator Check(string scene)
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + scene + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Inconclusive("Editor only.");
#endif
            yield return null;
            var zones = Object.FindObjectsByType<KillZone>(FindObjectsSortMode.None);
            var errors = new List<string>();
            int carriers = 0;
            foreach (var a in Object.FindObjectsByType<SignalActuator>(FindObjectsSortMode.None))
            {
                var deck = a.Platform != null ? a.Platform.GetComponentsInChildren<BoxCollider>().FirstOrDefault(b => !b.isTrigger) : null;
                if (deck == null || a.WaypointClosed == null || a.WaypointOpen == null) continue;
                carriers++;
                var closed = deck.bounds;
                closed.center += a.WaypointClosed.position - a.Platform.position;
                var open = new Bounds(closed.center + (a.WaypointOpen.position - a.WaypointClosed.position), closed.size);
                var lowest = open.center.y < closed.center.y ? open : closed;
                if (zones.Any(z => z.GetComponentInParent<SignalActuator>() != a && z.Zone.bounds.Contains(lowest.center))) continue;   // retracts into the void
                var swept = closed;
                swept.Encapsulate(open);
                swept.max += Vector3.up * StandingHeight;
                foreach (var z in zones)
                    if (z.GetComponentInParent<SignalActuator>() != a && z.Zone.bounds.Intersects(swept))
                        errors.Add($"{a.transform.parent?.name}/{a.name} (y {swept.min.y:F1} to {swept.max.y:F1}) crosses {z.transform.parent?.name}/{z.name} " +
                                   $"(y {z.Zone.bounds.min.y:F1} to {z.Zone.bounds.max.y:F1})");
            }
            Assert.Greater(carriers, 0, scene + " has carriers to check");
            Assert.IsEmpty(errors, scene + ": a rider would enter a kill zone");
        }

        [UnityTest] public IEnumerator PassSandbox_NoKillZoneAcrossACarrier() => Check("PassSandbox");
    }
}
