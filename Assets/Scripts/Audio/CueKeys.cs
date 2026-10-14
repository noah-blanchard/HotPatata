using System.Collections.Generic;

namespace HotPatata
{
    /// <summary>
    /// Every sound the code plays, as its folder under Assets/Audio (ARCHITECTURE §26). Drop sound files in the folder and
    /// they play; an empty or missing folder is silent (the bomb then uses its procedural placeholder). Never a magic string
    /// elsewhere: code asks for a key here, and the folder skeleton (HotPatata/Audio/Create Folder Skeleton) is built from it.
    /// </summary>
    public static class CueKeys
    {
        // ------------------------------------------------------------------ music (MusicDirector)

        public const string MusicMenu = "Music/Menu";
        public const string MusicLobby = "Music/Lobby";
        public const string MusicResults = "Music/Results";

        /// <summary>A course's track, played when its act has no folder of its own.</summary>
        public static string MusicCourse(string scene) => "Music/Courses/" + scene;
        public static string MusicAct(string scene, int act) => MusicCourse(scene) + "/" + Act(act);

        // ------------------------------------------------------------------ ambience (AmbienceDirector)

        /// <summary>The main menu and the lobby (the misty clearing).</summary>
        public const string AmbienceMenu = "Ambience/Menu";
        /// <summary>Plays everywhere in a course.</summary>
        public static string AmbienceCourse(string scene) => "Ambience/Courses/" + scene;
        /// <summary>Added on top while the team is in that act.</summary>
        public static string AmbienceAct(string scene, int act) => AmbienceCourse(scene) + "/" + Act(act);
        /// <summary>Replaces the environment's layer while the team is in that act.</summary>
        public static string AmbienceActReplace(string scene, int act) => AmbienceAct(scene, act) + "/Replace";
        /// <summary>The place the team is in (a course section, or an <see cref="AmbienceZone"/>).</summary>
        public static string AmbienceEnvironment(string environment) => "Ambience/Environments/" + environment;
        /// <summary>A fixed loop placed in the world by a builder (river, waterfall, campfire...).</summary>
        public static string AmbienceEmitter(string emitter) => "Ambience/Emitters/" + emitter;

        /// <summary>An ambience layer's loops (all layered, flat).</summary>
        public static string Beds(string layer) => layer + "/Beds";
        /// <summary>An ambience layer's one-shots (birds, cracks...), played at random spots around the listener.</summary>
        public static string Scatter(string layer) => layer + "/Scatter";

        public static string Act(int act) => "Act" + act;

        // ------------------------------------------------------------------ bomb (BombAudio, TransitPresentation)

        /// <summary>By fuse stage: calm, medium, urgent, critical.</summary>
        public static readonly string[] BombBeeps =
        {
            "SFX/Bomb/Beep/Calm", "SFX/Bomb/Beep/Medium", "SFX/Bomb/Beep/Urgent", "SFX/Bomb/Beep/Critical"
        };
        public const string BombThrow = "SFX/Bomb/Throw";
        public const string BombCatch = "SFX/Bomb/Catch";
        public const string BombExplosion = "SFX/Bomb/Explosion";
        /// <summary>A loop while the fuse burns (the bomb is held or flying).</summary>
        public const string BombFuseLoop = "SFX/Bomb/FuseLoop";
        /// <summary>Swallowed by a tube or a cannon.</summary>
        public const string BombTransitIn = "SFX/Bomb/TransitIn";
        /// <summary>The rising warning at the exit, just before the tube or cannon lets go.</summary>
        public const string BombTransitWarn = "SFX/Bomb/TransitWarn";

        // ------------------------------------------------------------------ players (PlayerSounds)

        public const string FootstepsDefault = "SFX/Player/Footsteps/Default";
        public static string Footsteps(string surface) => "SFX/Player/Footsteps/" + surface;
        public const string PlayerJump = "SFX/Player/Jump";
        public const string PlayerLandSoft = "SFX/Player/Land/Soft";
        public const string PlayerLandHard = "SFX/Player/Land/Hard";
        public const string PlayerSlideStart = "SFX/Player/Slide/Start";
        public const string PlayerSlideLoop = "SFX/Player/Slide/Loop";

        /// <summary>Footstep surfaces the bank maps materials to (a new folder under Footsteps works as soon as a material maps to it).</summary>
        public static readonly string[] Surfaces =
        {
            "Default", "Grass", "Dirt", "Gravel", "Mud", "Leaves", "Rock", "Stone", "Wood", "Metal", "Concrete", "Water"
        };

        // ------------------------------------------------------------------ the run (CourseSounds)

        public const string CourseStart = "SFX/Course/Start";
        public const string CourseCheckpoint = "SFX/Course/Checkpoint";
        public const string CourseActChange = "SFX/Course/ActChange";
        public const string CourseReset = "SFX/Course/Reset";
        public const string CourseFinish = "SFX/Course/Finish";

        // ------------------------------------------------------------------ obstacles (ObstacleSounds)

        public const string ObstacleOn = "On";
        public const string ObstacleOff = "Off";
        public const string ObstacleLoop = "Loop";

        /// <summary>An obstacle system's sound, by its component type: SFX/Obstacles/PressurePlate/On, .../MovingPlatform/Loop...</summary>
        public static string Obstacle(string type, string what) => "SFX/Obstacles/" + type + "/" + what;

        // ------------------------------------------------------------------ menus (UiSounds)

        public const string UiHover = "SFX/UI/Hover";
        public const string UiClick = "SFX/UI/Click";
        public const string UiBack = "SFX/UI/Back";
        public const string UiSlider = "SFX/UI/Slider";
        public const string UiOpen = "SFX/UI/Open";
        public const string UiLobbyJoin = "SFX/UI/LobbyJoin";
        public const string UiLobbyLeave = "SFX/UI/LobbyLeave";

        // ------------------------------------------------------------------ the skeleton

        /// <summary>Environments the course builders name (Ambience/Environments/&lt;name&gt;).</summary>
        public static readonly string[] Environments =
        {
            "Forest", "River", "Cliffs", "Cave", "Meadow", "Summit", "Canopy", "HollowOak", "Jungle", "Temple", "Storm", "Factory"
        };

        /// <summary>The campfires' crackle.</summary>
        public const string EmitterFire = "Fire";

        /// <summary>Fixed emitters the builders place (Ambience/Emitters/&lt;name&gt;), named as NatureAudioFactory's kinds.</summary>
        public static readonly string[] Emitters = { EmitterFire, "River", "Waterfall", "Wind", "Birds", "Crickets" };

        /// <summary>Every key that does not depend on a course or an obstacle type.</summary>
        public static IEnumerable<string> Fixed()
        {
            yield return MusicMenu;
            yield return MusicLobby;
            yield return MusicResults;
            yield return Beds(AmbienceMenu);
            yield return Scatter(AmbienceMenu);
            foreach (var e in Environments)
            {
                yield return Beds(AmbienceEnvironment(e));
                yield return Scatter(AmbienceEnvironment(e));
            }
            foreach (var e in Emitters) yield return AmbienceEmitter(e);
            foreach (var b in BombBeeps) yield return b;
            yield return BombThrow;
            yield return BombCatch;
            yield return BombExplosion;
            yield return BombFuseLoop;
            yield return BombTransitIn;
            yield return BombTransitWarn;
            foreach (var s in Surfaces) yield return Footsteps(s);
            yield return PlayerJump;
            yield return PlayerLandSoft;
            yield return PlayerLandHard;
            yield return PlayerSlideStart;
            yield return PlayerSlideLoop;
            yield return CourseStart;
            yield return CourseCheckpoint;
            yield return CourseActChange;
            yield return CourseReset;
            yield return CourseFinish;
            yield return UiHover;
            yield return UiClick;
            yield return UiBack;
            yield return UiSlider;
            yield return UiOpen;
            yield return UiLobbyJoin;
            yield return UiLobbyLeave;
        }

        /// <summary>A course's folders: its music, its ambience, and one of each per act.</summary>
        public static IEnumerable<string> ForCourse(string scene, int acts)
        {
            yield return MusicCourse(scene);
            yield return Beds(AmbienceCourse(scene));
            yield return Scatter(AmbienceCourse(scene));
            for (int act = 1; act <= acts; act++)
            {
                yield return MusicAct(scene, act);
                yield return Beds(AmbienceAct(scene, act));
                yield return Scatter(AmbienceAct(scene, act));
            }
        }
    }
}
