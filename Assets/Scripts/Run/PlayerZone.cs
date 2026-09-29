using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Stateless "who is standing in this zone right now" query. It re-evaluates every call, so it stays
    /// correct across teleports and resets (no Enter/Exit bookkeeping to go stale). A box collider is tested as the
    /// oriented box it is (a zone laid on a slope); any other collider by its bounds.
    /// </summary>
    public static class PlayerZone
    {
        static readonly Collider[] Buffer = new Collider[32];
        static int playerMask;

        public static int Collect(Collider zone, HashSet<Player> result)
        {
            result.Clear();
            if (playerMask == 0) playerMask = LayerMask.GetMask("Player");
            Vector3 center, extents;
            Quaternion rotation;
            if (zone is BoxCollider box)
            {
                var t = box.transform;
                Vector3 s = t.lossyScale;
                center = t.TransformPoint(box.center);
                extents = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
                rotation = t.rotation;
            }
            else
            {
                Bounds b = zone.bounds;
                center = b.center;
                extents = b.extents;
                rotation = Quaternion.identity;
            }
            int n = Physics.OverlapBoxNonAlloc(center, extents, Buffer, rotation, playerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var p = Buffer[i].GetComponentInParent<Player>();
                if (p != null) result.Add(p);
            }
            return result.Count;
        }
    }
}
