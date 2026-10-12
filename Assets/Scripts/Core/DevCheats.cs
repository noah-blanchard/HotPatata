using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Map-testing cheats (#97 follow-up), toggled by the dev panel (<c>DevPanel</c>, F8, <c>HotPatata.DebugTools</c>) or
    /// <c>-patataCheats</c>. Editor and development builds only: in a release build every flag reads false and cannot be set,
    /// so no cheat code path can run. Docs: docs/TESTING.md §2 (dev tools).
    ///  - <see cref="NoClip"/>: the local player flies through everything (<see cref="PlayerMotor"/>). Owner-side, anyone.
    ///  - <see cref="Invincible"/>: a lethal zone never fails the section: a static one (void, water) sends that player
    ///    back to their spawn, a moving one is ignored. A rule: it acts where the rules run (offline or on the host).
    ///  - <see cref="NoExplosions"/>: the bomb never explodes; what would have blown it up hands it back (to its carrier,
    ///    else its thrower) with a fresh fuse. A rule too: offline or on the host.
    /// Cheats change no gameplay rule for a normal run; they exist to walk, fly and test a course.
    /// </summary>
    public static class DevCheats
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public const bool Available = true;

        public static bool NoClip { get; set; }
        public static bool Invincible { get; set; }
        public static bool NoExplosions { get; set; }

        /// <summary>Nothing carries over from one play session to the next in the Editor (no domain reload).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() => NoClip = Invincible = NoExplosions = false;
#else
        public const bool Available = false;

        public static bool NoClip { get => false; set { } }
        public static bool Invincible { get => false; set { } }
        public static bool NoExplosions { get => false; set { } }
#endif

        /// <summary>Noclip flight speed (m/s); sprint multiplies it by <see cref="NoClipBoost"/>.</summary>
        public const float NoClipSpeed = 12f;
        public const float NoClipBoost = 3f;

        /// <summary>Any cheat on (the HUD says so, and logs tag it).</summary>
        public static bool Any => NoClip || Invincible || NoExplosions;
    }
}
