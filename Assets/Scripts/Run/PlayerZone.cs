using System.Collections.Generic;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Stateless "who is standing in this zone right now" query. It re-evaluates every call, so it stays
    /// correct across teleports and resets (no Enter/Exit bookkeeping to go stale).
    /// </summary>
    public static class PlayerZone
    {
        static readonly Collider[] Buffer = new Collider[32];

        public static int Collect(Collider zone, HashSet<Player> result)
        {
            result.Clear();
            Bounds b = zone.bounds;
            int n = Physics.OverlapBoxNonAlloc(b.center, b.extents, Buffer, Quaternion.identity,
                LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var p = Buffer[i].GetComponentInParent<Player>();
                if (p != null) result.Add(p);
            }
            return result.Count;
        }
    }
}
