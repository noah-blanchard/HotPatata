using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The in-world menu of <see cref="NetworkBootstrap"/> (#79, ARCHITECTURE §6.2). The island is the menu: each
    /// <see cref="MenuStation"/> (Title, Play, Level, Lobby) has a board and a camera spot, the <see cref="MenuFlow"/> says
    /// which one shows, and <see cref="MenuCameraRig"/> flies there. This view builds the boards
    /// (<see cref="StationScreen"/>s), makes only the shown one take input and focus, keeps them out of the way while a
    /// screen of the <see cref="ScreenStack"/> (Settings, a dialog) is open over the scene, and puts the lobby's players
    /// on the stage (<see cref="MenuLobbyStage"/>). It holds no game logic: the boards call the bootstrap's public
    /// methods. Added by <see cref="NetworkBootstrap"/> outside batch mode, so headless bot runs have no menu.
    /// </summary>
    public class MenuView : MonoBehaviour
    {
        const float LobbyPollSeconds = 0.2f;

        readonly MenuFlow flow = new MenuFlow();
        readonly List<MenuLobbyStage.Guest> guests = new List<MenuLobbyStage.Guest>();

        NetworkBootstrap bootstrap;
        NetworkBootstrap.Mode shownPhase;
        bool shownLobby, shown, blocked;
        MenuStation active;
        MenuCameraRig rig;
        MenuLobbyStage stage;
        float nextPoll;

        public MenuFlow Flow => flow;

        /// <summary>The station that shows and takes input (null outside the menu scene).</summary>
        public MenuStation Active => active;

        void Awake() => bootstrap = GetComponent<NetworkBootstrap>();

        void Update()
        {
            var phase = bootstrap.Phase;
            bool lobby = bootstrap.HasLobby;
            if (!shown || phase != shownPhase || lobby != shownLobby)
            {
                shown = true;
                shownPhase = phase;
                shownLobby = lobby;
                ScreenStack.Existing?.Clear();   // a new phase closes what was over it (a pause menu left from the level, a dialog)
            }
            flow.Sync(phase, lobby);

            if (MenuStation.All.Count == 0)
            {
                active = null;   // not in the menu scene
                return;
            }
            foreach (var station in MenuStation.All)
                if (station.Screen == null) station.Show(CreateScreen(station.Id));

            bool arrived = false;
            var target = MenuStation.Find(flow.Station);
            if (target != null && target != active)
            {
                if (rig == null) rig = FindAnyObjectByType<MenuCameraRig>();
                if (rig != null) rig.Travel(target, instant: active == null);
                foreach (var station in MenuStation.All) station.SetActive(station == target);
                active = target;
                arrived = true;
            }
            if (active == null) return;

            // Settings or a dialog over the scene, or a level loading: the board waits.
            bool block = ScreenStack.AnyOpen || phase == NetworkBootstrap.Mode.InGame;
            if (arrived)
            {
                blocked = block;
                active.SetInteractive(!block);
                active.FocusFirst();
            }
            else if (block != blocked)
            {
                blocked = block;
                active.SetInteractive(!block);
            }

            if (Time.unscaledTime >= nextPoll)
            {
                nextPoll = Time.unscaledTime + LobbyPollSeconds;
                FeedStage(phase == NetworkBootstrap.Mode.Lobby && lobby);
            }
        }

        UIScreen CreateScreen(MenuStationId id) => id switch
        {
            MenuStationId.Title => new TitleStation(bootstrap, flow),
            MenuStationId.Play => new PlayStation(bootstrap, flow),
            MenuStationId.Level => new LevelStation(bootstrap, flow),
            _ => new LobbyStation(bootstrap, flow)
        };

        /// <summary>The lobby's players step onto the stage; out of the lobby, everyone goes back to the show.</summary>
        void FeedStage(bool inLobby)
        {
            if (stage == null) stage = FindAnyObjectByType<MenuLobbyStage>();
            if (stage == null) return;
            guests.Clear();
            if (inLobby)
                foreach (var p in bootstrap.LobbyPlayers())
                    guests.Add(new MenuLobbyStage.Guest(p.Slot, p.Name));
            stage.SetGuests(guests);
        }
    }
}
