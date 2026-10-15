using System;
using System.Collections.Generic;

namespace HotPatata
{
    /// <summary>
    /// Pure rules (unit tested): which music folders may play now, best first (ARCHITECTURE §26). The first one holding a
    /// track plays; none means silence.
    ///   main menu: Menu. Lobby: Lobby, else Menu. A level loading: silence.
    ///   a course: its act's folder, else the course's. Run complete: Results, else the course's music goes on.
    /// </summary>
    public static class MusicContext
    {
        public enum Place
        {
            /// <summary>The menu scene, no lobby.</summary>
            Menu,
            /// <summary>The menu scene, in an online lobby.</summary>
            Lobby,
            /// <summary>Between the lobby and a ready level (the scene loading, the run initialising).</summary>
            Loading,
            /// <summary>A course with its run started.</summary>
            Course,
            /// <summary>A course whose run is complete (the results).</summary>
            Results,
        }

        /// <summary>Where the game is, from what can be read on any machine.</summary>
        public static Place Resolve(NetworkBootstrap.Mode? phase, bool courseLoaded, RunState? run)
        {
            if (!courseLoaded)
            {
                if (phase == NetworkBootstrap.Mode.InGame) return Place.Loading;
                return phase == NetworkBootstrap.Mode.Lobby ? Place.Lobby : Place.Menu;
            }
            if (run == null || run == RunState.Initializing) return Place.Loading;
            return run == RunState.Completed ? Place.Results : Place.Course;
        }

        /// <summary>The folders that may play at <paramref name="place"/>, best first.</summary>
        public static IReadOnlyList<string> Candidates(Place place, string scene, int act)
        {
            switch (place)
            {
                case Place.Menu: return new[] { CueKeys.MusicMenu };
                case Place.Lobby: return new[] { CueKeys.MusicLobby, CueKeys.MusicMenu };
                case Place.Course: return new[] { CueKeys.MusicAct(scene, act), CueKeys.MusicCourse(scene) };
                case Place.Results: return new[] { CueKeys.MusicResults, CueKeys.MusicAct(scene, act), CueKeys.MusicCourse(scene) };
                default: return Array.Empty<string>();
            }
        }
    }
}
