using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Throws a player standing on it straight up so they reach <see cref="launchHeight"/> metres. Used for the
    /// vertical-catch beat: the launched player is airborne and catches the bomb near the top of the jump.
    /// Only the machine that owns a player launches it (owners move their own players).
    /// </summary>
    public class LaunchPad : MonoBehaviour
    {
        [SerializeField] Collider trigger;
        [SerializeField, Min(0.5f), Tooltip("How high above the pad the launched player's feet rise.")] float launchHeight = 6f;
        [SerializeField, Min(0f)] float cooldown = 0.6f;

        readonly HashSet<Player> inside = new HashSet<Player>();
        readonly Dictionary<Player, float> lastLaunch = new Dictionary<Player, float>();

        void FixedUpdate()
        {
            PlayerZone.Collect(trigger, inside);
            foreach (var p in inside)
            {
                if (!p.IsLocal || p.ControlLocked || !p.Motor.Grounded) continue;
                if (lastLaunch.TryGetValue(p, out float t) && Time.time - t < cooldown) continue;

                lastLaunch[p] = Time.time;
                p.Motor.Launch(Mathf.Sqrt(2f * p.Tuning.gravity * launchHeight));
                PatataLog.Run($"{name} launched {p}");
            }
        }
    }
}
