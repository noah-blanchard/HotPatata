using System.Collections.Generic;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// The course is complete only when ALL required players are inside at the same time.
    /// One player alone can never finish a multi-player run, and completion fires exactly once.
    /// </summary>
    public class FinishZone : MonoBehaviour
    {
        [SerializeField] Collider trigger;

        readonly HashSet<Player> inside = new HashSet<Player>();

        void FixedUpdate()
        {
            var run = RunManager.Instance;
            if (!NetMode.IsAuthority || run == null || run.State != RunState.Playing) return;

            if (PlayerZone.Collect(trigger, inside) >= run.Players.Count)
                run.Complete();
        }
    }
}
