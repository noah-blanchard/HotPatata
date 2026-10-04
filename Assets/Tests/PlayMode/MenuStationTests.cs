using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Cinemachine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HotPatata.Tests
{
    /// <summary>
    /// #79: the in-world menu. The generated backdrop holds the four stations, each board builds from its layout; only
    /// the shown station's board takes input; choosing flies the camera to the next station (a cut when the camera effects
    /// are off) and Back flies back; the settings screen over the scene pauses the board and gives its focus back; the
    /// lobby's players step onto the stage with their nameplates and the show resumes when they leave. Uses the real
    /// NetworkManager prefab (its <see cref="NetworkBootstrap"/> adds the <see cref="MenuView"/>), never starts a
    /// session, and sends navigation as UI Toolkit events (never simulated devices).
    /// </summary>
    public class MenuStationTests
    {
        const string BackdropPrefab = "Assets/Prefabs/Menu/MenuBackdrop.prefab";
        const string NetworkPrefab = "Assets/Prefabs/Network/NetworkManager.prefab";
        const string TuningPath = "Assets/ScriptableObjects/Tuning/GameTuning.asset";

        GameObject backdrop, network, cameraObject;
        MenuView view;
        MenuCameraRig rig;
        GameTuning tuning;
        string folder;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            folder = Path.Combine(Path.GetTempPath(), "HotPatataMenuStationTests");
            Directory.CreateDirectory(folder);
            Settings.Folder = folder;
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath);
            ScreenStack.Get();

            cameraObject = new GameObject("MenuCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<CinemachineBrain>();
            rig = cameraObject.AddComponent<MenuCameraRig>();
            rig.Configure(tuning);

            backdrop = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BackdropPrefab));
            network = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefab));
#else
            Assert.Ignore("needs the Editor's AssetDatabase");
#endif
            yield return Frames(4);
            view = network.GetComponent<MenuView>();
            Assert.IsNotNull(view, "the bootstrap adds the menu view outside batch mode");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (ScreenStack.Existing != null) Object.Destroy(ScreenStack.Existing.gameObject);
            Object.Destroy(backdrop);
            Object.Destroy(network);
            Object.Destroy(cameraObject);
            yield return null;
            Settings.ResetToDefaults();
            Settings.Folder = null;
        }

        static IEnumerator Frames(int n = 3)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Seconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static void Send(EventBase e, VisualElement target)
        {
            e.target = target;
            target.SendEvent(e);
        }

        static MenuStation Station(MenuStationId id) => MenuStation.Find(id);

        static Focusable FocusedOn(MenuStation s) => s.Board.rootVisualElement.panel?.focusController.focusedElement;

        void SetViewEffects(float strength)
        {
            var data = Settings.Editable(tuning);
            data.viewEffectsStrength = strength;
            Settings.Preview(data, tuning);
        }

        [UnityTest]
        public IEnumerator Backdrop_HasFourStations_EachBoardBuildsFromItsLayout()
        {
            var ids = MenuStation.All.Select(s => s.Id).OrderBy(i => i).ToArray();
            CollectionAssert.AreEqual(new[] { MenuStationId.Title, MenuStationId.Play, MenuStationId.Level, MenuStationId.Lobby }, ids);
            foreach (var s in MenuStation.All)
            {
                Assert.IsNotNull(s.Spot, s.Id + ": a camera spot");
                Assert.IsNotNull(s.Board.panelSettings, s.Id + ": a world-space board");
                Assert.AreEqual(PanelRenderMode.WorldSpace, s.Board.panelSettings.renderMode, s.Id.ToString());
                Assert.IsNotNull(s.Screen?.Root, s.Id + ": its board was built");
                Assert.IsInstanceOf<StationScreen>(s.Screen);
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator OnlyTheShownBoard_TakesInput_AndHasFocus()
        {
            Assert.AreSame(Station(MenuStationId.Title), view.Active, "the menu opens on Title");
            yield return Frames();
            foreach (var s in MenuStation.All)
            {
                bool active = s == view.Active;
                Assert.AreEqual(active, s.Screen.Root.enabledInHierarchy, s.Id + " takes input only when shown");
                Assert.AreEqual(!active, s.Screen.Root.ClassListContains(MenuStation.IdleClass), s.Id.ToString());
                Assert.AreEqual(active ? LayerMask.NameToLayer("UI") : LayerMask.NameToLayer("Ignore Raycast"), s.Board.gameObject.layer,
                                s.Id + ": only the shown board catches the pointer");
            }
            var play = ((TitleStation)view.Active.Screen).Play;
            Assert.AreSame(play, FocusedOn(view.Active), "keyboard / gamepad focus on the prompt");
        }

        [UnityTest]
        public IEnumerator Submit_FliesToTheNextStation_AndBackFliesBack()
        {
            yield return Frames();
            var title = Station(MenuStationId.Title);
            var prompt = ((TitleStation)title.Screen).Play;
            using (var e = NavigationSubmitEvent.GetPooled()) Send(e, prompt);
            yield return Frames();

            var play = Station(MenuStationId.Play);
            Assert.AreSame(play, view.Active);
            Assert.AreSame(play, rig.Current);
            Assert.AreEqual(10, (int)play.Spot.Priority, "the Play spot is live");
            yield return Seconds(tuning.menuTravelSeconds + 0.3f);
            Assert.That(Vector3.Distance(cameraObject.transform.position, play.Spot.transform.position), Is.LessThan(0.05f), "the camera arrived");
            Assert.IsInstanceOf<Button>(FocusedOn(play), "the Play board has the focus");

            using (var e = NavigationCancelEvent.GetPooled()) Send(e, (VisualElement)FocusedOn(play));
            yield return Frames();
            Assert.AreSame(title, view.Active, "Back flies back to Title");
        }

        [UnityTest]
        public IEnumerator WithoutCameraEffects_TheMoveIsACut()
        {
            SetViewEffects(0f);
            view.Flow.Choose(MenuAction.Start);
            yield return Frames(3);
            var play = Station(MenuStationId.Play);
            Assert.IsFalse(rig.IsTravelling, "no swoop");
            Assert.That(Vector3.Distance(cameraObject.transform.position, play.Spot.transform.position), Is.LessThan(0.05f), "already there");
        }

        [UnityTest]
        public IEnumerator WithCameraEffects_TheMoveGlides()
        {
            SetViewEffects(1f);
            yield return Seconds(0.3f);   // past the instant opening shot
            view.Flow.Choose(MenuAction.Start);
            yield return Frames(3);
            Assert.IsTrue(rig.IsTravelling, "an eased flight");
        }

        [UnityTest]
        public IEnumerator TheLevelBoard_SaysWhatItWillDo()
        {
            view.Flow.Choose(MenuAction.Start);
            view.Flow.Choose(MenuAction.PlayLocal);
            yield return Frames();
            var level = Station(MenuStationId.Level);
            Assert.AreSame(level, view.Active);
            Assert.AreEqual("PLAY LOCAL", level.Screen.Root.Q<Label>("confirm-label").text);
            Assert.AreSame(((LevelStation)level.Screen).Confirm, FocusedOn(level));

            using (var e = NavigationCancelEvent.GetPooled()) Send(e, ((LevelStation)level.Screen).Confirm);
            yield return Frames();
            Assert.AreSame(Station(MenuStationId.Play), view.Active);
            view.Flow.Choose(MenuAction.HostOnline);
            yield return Frames();
            Assert.AreEqual("HOST ONLINE", level.Screen.Root.Q<Label>("confirm-label").text);
        }

        [UnityTest]
        public IEnumerator SettingsOverTheScene_PausesTheBoard_ThenGivesItsFocusBack()
        {
            view.Flow.Choose(MenuAction.Start);
            yield return Frames();
            var play = Station(MenuStationId.Play);
            var settings = play.Screen.Root.Q<Button>("settings");
            settings.Focus();
            using (var e = NavigationSubmitEvent.GetPooled()) Send(e, settings);
            yield return Frames();

            Assert.IsTrue(ScreenStack.Get().Has<SettingsScreen>(), "the settings screen opens over the scene");
            Assert.IsFalse(play.Screen.Root.enabledInHierarchy, "the board waits");

            ScreenStack.Get().Pop();
            yield return Frames();
            Assert.IsTrue(play.Screen.Root.enabledInHierarchy);
            Assert.AreSame(settings, FocusedOn(play), "focus comes back where it was");
        }

        [UnityTest]
        public IEnumerator TheCodeDials_TakeAGamepadCode()
        {
            view.Flow.Choose(MenuAction.Start);
            yield return Frames();
            var dials = ((PlayStation)Station(MenuStationId.Play).Screen).Code;
            dials.Focus();
            using (var e = NavigationSubmitEvent.GetPooled()) Send(e, dials);
            Assert.IsTrue(dials.Editing, "A starts editing");
            using (var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Up)) Send(e, dials);
            using (var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Right)) Send(e, dials);
            using (var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Up)) Send(e, dials);
            using (var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Up)) Send(e, dials);
            Assert.AreEqual("AB", dials.Value);

            using (var e = NavigationCancelEvent.GetPooled()) Send(e, dials);
            yield return Frames();
            Assert.IsFalse(dials.Editing, "B stops editing");
            Assert.AreSame(Station(MenuStationId.Play), view.Active, "and does not leave the station");
        }

        [UnityTest]
        public IEnumerator TheLobbyStage_ShowsEachGuestWithANameplate_ThenTheShowResumes()
        {
            view.enabled = false;   // the test plays the lobby list
            var stage = backdrop.GetComponentInChildren<MenuLobbyStage>();
            var show = backdrop.GetComponentInChildren<MenuHotPotato>();
            var plates = stage.GetComponentsInChildren<UIDocument>();
            Assert.AreEqual(4, plates.Length, "a nameplate per slot");

            stage.SetGuests(new[] { new MenuLobbyStage.Guest(0, "Noah"), new MenuLobbyStage.Guest(2, "Bea") });
            Assert.IsTrue(show.Paused, "the potato show pauses");
            yield return Seconds(tuning.menuStepSeconds + 0.2f);

            var spots = stage.transform.Cast<Transform>().ToArray();
            Assert.That(Vector3.Distance(show.Players[0].position, spots[0].position), Is.LessThan(0.01f), "slot 1 on its spot");
            Assert.That(Vector3.Distance(show.Players[2].position, spots[2].position), Is.LessThan(0.01f), "slot 3 on its spot");
            Assert.That(Vector3.Distance(show.Players[1].position, spots[1].position), Is.GreaterThan(0.5f), "a free seat stays in the show");
            Assert.AreEqual(DisplayStyle.Flex, plates[0].rootVisualElement.resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, plates[1].rootVisualElement.resolvedStyle.display);
            Assert.AreEqual("Noah", plates[0].rootVisualElement.Q<Label>("name").text);
            Assert.IsNotNull(plates[0].rootVisualElement.Q(className: "hp-chip"), "the slot chip: colour and shape (spec §19)");

            stage.SetGuests(new MenuLobbyStage.Guest[0]);
            yield return Seconds(tuning.menuStepSeconds + 0.2f);
            Assert.IsFalse(stage.IsOpen);
            Assert.IsFalse(show.Paused, "everyone is back, the show goes on");
            Assert.AreEqual(DisplayStyle.None, plates[0].rootVisualElement.resolvedStyle.display);
        }
    }
}
