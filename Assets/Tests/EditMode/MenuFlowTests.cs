using NUnit.Framework;
using Unity.Cinemachine;
using Mode = HotPatata.NetworkBootstrap.Mode;

namespace HotPatata.Tests
{
    /// <summary>
    /// #79: the in-world menu's navigation. The player's choices move between the stations (Title, Play, Level, Lobby)
    /// and Back goes the same way back; the session flow wins (an open lobby shows the Lobby, a failed or left session
    /// lands on Play with its error, never on Title); the camera flies with an eased blend, or cuts when the camera
    /// effects are off (spec §19).
    /// </summary>
    public class MenuFlowTests
    {
        [Test]
        public void StartsOnTitle_ThenPlay_AndBackReturns()
        {
            var flow = new MenuFlow();
            Assert.AreEqual(MenuStationId.Title, flow.Station);
            Assert.IsFalse(flow.Back(), "Title is the root");

            flow.Choose(MenuAction.Start);
            Assert.AreEqual(MenuStationId.Play, flow.Station);
            Assert.IsTrue(flow.Back());
            Assert.AreEqual(MenuStationId.Title, flow.Station);
        }

        [TestCase(MenuAction.HostOnline, LevelPurpose.HostOnline)]
        [TestCase(MenuAction.HostLan, LevelPurpose.HostLan)]
        [TestCase(MenuAction.PlayLocal, LevelPurpose.Local)]
        public void Play_OpensTheLevelChoice_ForItsPurpose_AndBackReturnsToPlay(MenuAction action, LevelPurpose purpose)
        {
            var flow = new MenuFlow(startAtTitle: false);
            flow.Choose(action);
            Assert.AreEqual(MenuStationId.Level, flow.Station);
            Assert.AreEqual(purpose, flow.Purpose);
            Assert.IsTrue(flow.Back());
            Assert.AreEqual(MenuStationId.Play, flow.Station);
        }

        [Test]
        public void ChoicesOfAnotherStation_AreIgnored()
        {
            var flow = new MenuFlow();
            flow.Choose(MenuAction.HostOnline);
            flow.Choose(MenuAction.ChangeLevel);
            Assert.AreEqual(MenuStationId.Title, flow.Station);
        }

        [Test]
        public void AnOpenLobby_ShowsTheLobby_AndTheHostsLevelChoiceComesBackToIt()
        {
            var flow = new MenuFlow(startAtTitle: false);
            flow.Choose(MenuAction.HostOnline);
            flow.Sync(Mode.Working, false);
            Assert.AreEqual(MenuStationId.Level, flow.Station, "the board that started the work shows it");
            flow.Sync(Mode.Lobby, true);
            Assert.AreEqual(MenuStationId.Lobby, flow.Station);
            Assert.IsFalse(flow.Back(), "leaving the lobby is asked on its board");

            flow.Choose(MenuAction.ChangeLevel);
            Assert.AreEqual(MenuStationId.Level, flow.Station);
            Assert.AreEqual(LevelPurpose.LobbyEdit, flow.Purpose);
            flow.Sync(Mode.Lobby, true);
            Assert.AreEqual(MenuStationId.Level, flow.Station, "the lobby leaves the host on the level choice");
            flow.Back();
            Assert.AreEqual(MenuStationId.Lobby, flow.Station);
        }

        [Test]
        public void AFailedSession_LandsOnPlay()
        {
            var flow = new MenuFlow(startAtTitle: false);
            flow.Choose(MenuAction.HostOnline);
            flow.Sync(Mode.Working, false);
            flow.Sync(Mode.Menu, false);   // NetworkBootstrap.Fail
            Assert.AreEqual(MenuStationId.Play, flow.Station, "the error shows on Play");
        }

        [Test]
        public void LeavingTheLobby_OrComingBackFromAGame_LandsOnPlay_NotTitle()
        {
            var flow = new MenuFlow();
            flow.Sync(Mode.Lobby, true);
            flow.Sync(Mode.Menu, false);
            Assert.AreEqual(MenuStationId.Play, flow.Station);

            var local = new MenuFlow();
            local.Choose(MenuAction.Start);
            local.Choose(MenuAction.PlayLocal);
            local.Sync(Mode.InGame, false);
            Assert.AreEqual(MenuStationId.Level, local.Station, "in game the menu stays where it was");
            local.Sync(Mode.Menu, false);
            Assert.AreEqual(MenuStationId.Play, local.Station);
        }

        [Test]
        public void TheMenuPhase_KeepsThePlayersPlace()
        {
            var flow = new MenuFlow();
            flow.Sync(Mode.Menu, false);
            Assert.AreEqual(MenuStationId.Title, flow.Station);
            flow.Choose(MenuAction.Start);
            flow.Choose(MenuAction.PlayLocal);
            flow.Sync(Mode.Menu, false);
            Assert.AreEqual(MenuStationId.Level, flow.Station);
        }

        [Test]
        public void Moved_IsRaisedOncePerChange()
        {
            var flow = new MenuFlow();
            int moves = 0;
            flow.Moved += () => moves++;
            flow.Choose(MenuAction.Start);
            flow.Choose(MenuAction.Start);
            flow.Sync(Mode.Menu, false);
            Assert.AreEqual(1, moves);
        }

        [Test]
        public void Travel_IsAnEasedBlend_OrACutWithoutCameraEffects()
        {
            var eased = MenuCameraRig.BlendFor(1f, 1.1f);
            Assert.AreEqual(CinemachineBlendDefinition.Styles.EaseInOut, eased.Style);
            Assert.AreEqual(1.1f, eased.Time, 1e-5f);

            Assert.AreEqual(CinemachineBlendDefinition.Styles.EaseInOut, MenuCameraRig.BlendFor(0.25f, 1.1f).Style, "reduced effects still glide");
            Assert.AreEqual(CinemachineBlendDefinition.Styles.Cut, MenuCameraRig.BlendFor(0f, 1.1f).Style, "0 = no swoop");
            Assert.AreEqual(CinemachineBlendDefinition.Styles.Cut, MenuCameraRig.BlendFor(1f, 0f).Style, "an instant move");
        }
    }
}
