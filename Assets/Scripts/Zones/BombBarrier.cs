using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A zone the flying bomb may not cross: crossing it is a lethal contact, like hitting a wall (PROJECT_SPEC §7.3).
    /// Players walk through. With a forbidden <see cref="FuseZone"/> on the same object it is a laser curtain: lethal to
    /// the bomb in every state, which is how a window between two curtains becomes mandatory (§13.13).
    /// </summary>
    [RequireComponent(typeof(Zone))]
    public class BombBarrier : MonoBehaviour, IBombZoneEffect
    {
        public void OnBombPassed(BombController bomb) => bomb.ReportZoneContact($"barrier={name}");
    }
}
