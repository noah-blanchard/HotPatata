using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Map-testing cheats (#97 follow-up), toggled by the cheat panel (<see cref="DevPanel"/>, F8) or <c>-patataCheats</c>.
    /// In every build, release included (owner decision, 2026-10-17): a yellow banner says when one is on. Docs: docs/TESTING.md
    /// §2 (dev tools).
    ///  - <see cref="NoClip"/>: the local player flies through everything (<see cref="PlayerMotor"/>). Owner-side, anyone.
    ///  - <see cref="Invincible"/>: a lethal zone never fails the section: a static one (void, water) sends that player
    ///    back to their spawn, a moving one is ignored. A rule: it acts where the rules run (offline or on the host).
    ///  - <see cref="NoExplosions"/>: the bomb never explodes; what would have blown it up hands it back (to its carrier,
    ///    else its thrower) with a fresh fuse. A rule too: offline or on the host.
    /// Cheats change no gameplay rule for a normal run; they exist to walk, fly and test a course.
    /// </summary>
    public static class DevCheats
    {
        public static bool NoClip { get; set; }
        public static bool Invincible { get; set; }
        public static bool NoExplosions { get; set; }

        /// <summary>Nothing carries over from one play session to the next in the Editor (no domain reload).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() => NoClip = Invincible = NoExplosions = false;

        /// <summary>Noclip flight speed (m/s); sprint multiplies it by <see cref="NoClipBoost"/>.</summary>
        public const float NoClipSpeed = 12f;
        public const float NoClipBoost = 3f;

        /// <summary>Any cheat on (the HUD says so, and logs tag it).</summary>
        public static bool Any => NoClip || Invincible || NoExplosions;
    }
}
