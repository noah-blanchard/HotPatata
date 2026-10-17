using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (#117 P3): on a screen that is neither the thrower's nor the receiver's own, the bomb would fly
    /// through a teammate who caught it on their screen, then come back to their hands a round trip later. Instead, when the
    /// drawn arc enters the reach of a teammate whose catch window is open (relayed by the host,
    /// <see cref="PlayerCatcher.ShownWindowOpen"/>), the drawing puts the bomb in their hands at once and waits for the
    /// host's verdict. It decides nothing: <see cref="CatchResolver"/> does.
    /// </summary>
    public static class CatchPreview
    {
        /// <summary>A teammate drawn here (not this machine's own player, not the thrower) the chord a→b reaches while their window is open.</summary>
        public static Player Find(BombController bomb, Vector3 a, Vector3 b, double now)
        {
            if (!NetMode.IsNetworked || bomb == null) return null;
            foreach (var p in Player.All)
            {
                if (p == null || p.IsLocal || p == bomb.LastThrower || p.CatchVolume == null || p.ControlLocked) continue;
                if (!p.Catcher.ShownWindowOpen(now)) continue;
                if (CatchResolver.ReachDistance(p.Tuning, a, b, p.CatchVolume.CatchCenter, out _) <= CatchResolver.ReachFor(p, b - a)) return p;
            }
            return null;
        }
    }
}
