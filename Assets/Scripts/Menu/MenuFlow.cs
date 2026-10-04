namespace HotPatata
{
    /// <summary>The stations of the in-world menu (ARCHITECTURE §6.2), each a board with a camera spot on the island.</summary>
    public enum MenuStationId { Title, Play, Level, Lobby }

    /// <summary>What the Level station's confirm button does: it is reached from different places.</summary>
    public enum LevelPurpose { HostOnline, HostLan, Local, LobbyEdit }

    /// <summary>A choice on a station board that moves the menu to another station.</summary>
    public enum MenuAction
    {
        /// <summary>Title: "press to play".</summary>
        Start,
        /// <summary>Play: Host Online, pick the level first.</summary>
        HostOnline,
        /// <summary>Play: Host on LAN, pick the level first.</summary>
        HostLan,
        /// <summary>Play: Play Local, pick the level first.</summary>
        PlayLocal,
        /// <summary>Lobby (host): change the level, then come back.</summary>
        ChangeLevel
    }

    /// <summary>
    /// Where the in-world menu is (ARCHITECTURE §6.2): the station the camera shows, and why the Level station was
    /// opened. Plain state, no Unity objects: <see cref="MenuView"/> feeds it the player's choices and the bootstrap's
    /// phase, then flies the camera to <see cref="Station"/>. The session flow wins over the player's place: an open
    /// lobby shows the Lobby station, a failed or left session lands on Play (with its error) and never on Title.
    /// </summary>
    public class MenuFlow
    {
        NetworkBootstrap.Mode lastPhase = NetworkBootstrap.Mode.Menu;

        /// <summary>Starts on Title (first launch) or on Play (back from a game, or after an error).</summary>
        public MenuFlow(bool startAtTitle = true) => Station = startAtTitle ? MenuStationId.Title : MenuStationId.Play;

        public MenuStationId Station { get; private set; }
        public LevelPurpose Purpose { get; private set; } = LevelPurpose.HostOnline;

        /// <summary>Raised when <see cref="Station"/> changes.</summary>
        public event System.Action Moved;

        /// <summary>A board's choice. Choices that do not belong to the current station are ignored.</summary>
        public void Choose(MenuAction action)
        {
            switch (action)
            {
                case MenuAction.Start when Station == MenuStationId.Title: MoveTo(MenuStationId.Play); break;
                case MenuAction.HostOnline when Station == MenuStationId.Play: OpenLevel(LevelPurpose.HostOnline); break;
                case MenuAction.HostLan when Station == MenuStationId.Play: OpenLevel(LevelPurpose.HostLan); break;
                case MenuAction.PlayLocal when Station == MenuStationId.Play: OpenLevel(LevelPurpose.Local); break;
                case MenuAction.ChangeLevel when Station == MenuStationId.Lobby: OpenLevel(LevelPurpose.LobbyEdit); break;
            }
        }

        /// <summary>
        /// Back (Esc / gamepad B, or a board's Back button): the station before this one. False when there is none here
        /// (Title is the root; leaving the Lobby is a session action the board asks about first).
        /// </summary>
        public bool Back()
        {
            switch (Station)
            {
                case MenuStationId.Play: MoveTo(MenuStationId.Title); return true;
                case MenuStationId.Level: MoveTo(Purpose == LevelPurpose.LobbyEdit ? MenuStationId.Lobby : MenuStationId.Play); return true;
                default: return false;
            }
        }

        /// <summary>
        /// Follows the session flow: an open lobby shows the Lobby (or the host's level choice), and back in the Menu phase
        /// after a session (failed, left, or a game ended) the menu shows Play. Working and InGame keep the station, so
        /// the board that started the work shows it.
        /// </summary>
        public void Sync(NetworkBootstrap.Mode phase, bool hasLobby)
        {
            var previous = lastPhase;
            lastPhase = phase;
            switch (phase)
            {
                case NetworkBootstrap.Mode.Lobby when hasLobby:
                    if (Station != MenuStationId.Lobby && !(Station == MenuStationId.Level && Purpose == LevelPurpose.LobbyEdit))
                        MoveTo(MenuStationId.Lobby);
                    break;
                case NetworkBootstrap.Mode.Menu when previous != NetworkBootstrap.Mode.Menu:
                    MoveTo(MenuStationId.Play);
                    break;
            }
        }

        void OpenLevel(LevelPurpose purpose)
        {
            Purpose = purpose;
            MoveTo(MenuStationId.Level);
        }

        void MoveTo(MenuStationId station)
        {
            if (station == Station) return;
            Station = station;
            Moved?.Invoke();
        }
    }
}
