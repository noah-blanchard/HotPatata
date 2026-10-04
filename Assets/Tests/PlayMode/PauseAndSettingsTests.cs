using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HotPatata.Tests
{
    /// <summary>
    /// #16 pause menu and #17 settings screen, offline in PassSandbox. The pause menu freezes the offline game, takes the
    /// local player's gameplay input and frees the cursor; a carrier keeps the bomb and loses a charge instead of
    /// throwing on resume; Esc / Start never closes and reopens it on one frame; Leave asks first. The settings screen
    /// previews every change, writes the file once when it closes, resets to the tuning defaults and never writes the
    /// GameTuning asset. Navigation is sent as UI Toolkit events (never simulated devices); the settings file goes to a
    /// temporary folder, never the player's.
    /// </summary>
    public class PauseAndSettingsTests : SandboxTestBase
    {
        string folder;

        [UnitySetUp]
        public IEnumerator UseATemporarySettingsFolder()
        {
            folder = Path.Combine(Path.GetTempPath(), "HotPatataSettingsTests");
            Directory.CreateDirectory(folder);
            Settings.Folder = folder;
            yield break;
        }

        [UnityTearDown]
        public IEnumerator CloseEverything()
        {
            if (ScreenStack.Existing != null) Object.Destroy(ScreenStack.Existing.gameObject);
            yield return null;
            Settings.ResetToDefaults();   // deletes the temporary file, back to the tuning values
            Settings.Folder = null;
            Time.timeScale = 1f;
            CursorPolicy.SetGameplayLock(false);
        }

        static IEnumerator Frames(int n = 3)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static void Send(EventBase e, VisualElement target)
        {
            e.target = target;
            target.SendEvent(e);
        }

        static void Move(VisualElement target, NavigationMoveEvent.Direction direction)
        {
            using (var e = NavigationMoveEvent.GetPooled(direction)) Send(e, target);
        }

        static void Click(Button button)
        {
            using (var e = NavigationSubmitEvent.GetPooled()) Send(e, button);
        }

        static Focusable Focused(UIScreen screen) => screen.Root.focusController?.focusedElement;

        [UnityTest]
        public IEnumerator Pause_FreezesTheOfflineGame_TakesTheInput_FreesTheCursor_AndResumes()
        {
            CursorPolicy.SetGameplayLock(true);
            var pause = PauseMenu.Open(tuning);
            yield return Frames();
            Assert.IsTrue(PauseMenu.IsOpen);
            Assert.AreEqual(0f, Time.timeScale, "offline, the game is frozen");
            Assert.IsTrue(p1.Input.Blocked, "the local player's gameplay input is taken");
            Drive.Move = Vector2.one;
            Drive.PressJump();
            Assert.AreEqual(Vector2.zero, p1.Input.Move);
            Assert.IsFalse(p1.Input.JumpPressed, "a press while paused is dropped, not kept for later");
            Assert.IsFalse(CursorPolicy.IsLocked, "the cursor is free on the menu");
            Assert.AreSame(pause.Resume, Focused(pause), "Resume has the focus");

            Click(pause.Resume);
            yield return Frames();
            Assert.IsFalse(PauseMenu.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(p1.Input.Blocked);
            Assert.AreEqual(Vector2.one, p1.Input.Move);
            Assert.IsTrue(CursorPolicy.IsLocked, "back to gameplay");
            Drive.Move = Vector2.zero;
        }

        [UnityTest]
        public IEnumerator ACarrierWhoPauses_KeepsTheBomb_AndTheChargeIsCancelled()
        {
            Give(p1);
            Drive.SetThrowHeld(true);
            yield return Frames();
            Assert.IsTrue(p1.Thrower.Charging, "charging a throw");

            PauseMenu.Open(tuning);
            yield return Frames();
            Assert.IsFalse(p1.Thrower.Charging, "a menu cancels the charge");
            Drive.SetThrowHeld(false);   // released while paused
            yield return Frames();

            ScreenStack.Existing.Back();
            yield return Frames(10);
            Assert.AreSame(p1, bomb.Carrier, "the carrier still holds the bomb");
            Assert.AreEqual(BombState.Held, bomb.State, "closing the menu never throws by itself");
        }

        [UnityTest]
        public IEnumerator PauseKey_NeverClosesAndReopensOnOneFrame_AndLeavesOtherScreensToTheirBack()
        {
            var stack = ScreenStack.Get();
            PauseMenu.Toggle(tuning);
            PauseMenu.Toggle(tuning);   // the same key seen twice on one frame (UI Back and gameplay Pause)
            Assert.IsTrue(PauseMenu.IsOpen, "opened, not closed again on the same frame");
            yield return Frames();

            var settings = new SettingsScreen(tuning);
            stack.Push(settings);
            yield return Frames();
            PauseMenu.Toggle(tuning);
            Assert.AreSame(settings, stack.Top, "Pause leaves the settings screen to its own Back");

            stack.Back();
            yield return Frames();
            Assert.IsInstanceOf<PauseScreen>(stack.Top, "Back from the settings returns to the pause menu");
            PauseMenu.Toggle(tuning);
            Assert.IsFalse(PauseMenu.IsOpen, "Pause closes the menu when it is on top");
        }

        [UnityTest]
        public IEnumerator Leave_AsksFirst_AndCancelKeepsYouInTheRun()
        {
            var pause = PauseMenu.Open(tuning);
            yield return Frames();
            Click(pause.LeaveButton);
            yield return Frames();
            var confirm = ScreenStack.Existing.Top as ConfirmScreen;
            Assert.IsNotNull(confirm, "a question before leaving");
            Assert.AreSame(confirm.Cancel, Focused(confirm), "the safe answer has the focus");

            Click(confirm.Cancel);
            yield return Frames();
            Assert.AreSame(pause, ScreenStack.Existing.Top);
            Assert.AreEqual(0f, Time.timeScale, "still paused");
        }

        [UnityTest]
        public IEnumerator Settings_PreviewEachChange_SaveOnClose_ResetToDefaults_AndNeverWriteTheTuning()
        {
            float tuningEffects = tuning.viewEffectsStrength;
            float tuningFov = tuning.fieldOfView;
            var pause = PauseMenu.Open(tuning);
            yield return Frames();
            Click(pause.SettingsButton);
            yield return Frames();
            var screen = ScreenStack.Existing.Top as SettingsScreen;
            Assert.IsNotNull(screen, "Settings is reachable from the pause menu");
            Assert.AreEqual(0f, Time.timeScale, "still frozen under the settings");

            var rows = screen.Root.Query<SettingRow>().ToList();
            var effects = (SliderRow)rows[0];
            Assert.AreSame(effects, Focused(screen), "the first row has the focus");

            // Left / right on a row change its value; down moves to the next row.
            Move(effects, NavigationMoveEvent.Direction.Left);
            yield return null;
            Assert.AreEqual(Mathf.Max(0f, tuningEffects - effects.StepSize), Settings.ViewEffectsStrength(tuning), 1e-4f, "applied at once");
            Assert.IsFalse(File.Exists(Path.Combine(folder, "settings.json")), "not written while the screen is open");
            Move(effects, NavigationMoveEvent.Direction.Down);
            yield return Frames();
            Assert.AreSame(rows[1], Focused(screen), "down moves to the next row");

            var fov = rows.Find(r => r.Q<Label>().text == "Field of view") as SliderRow;
            Assert.IsNotNull(fov);
            Move(fov, NavigationMoveEvent.Direction.Right);
            yield return null;
            Assert.AreEqual(tuningFov + 1f, Settings.FieldOfView(tuning), 1e-4f);

            ScreenStack.Existing.Back();
            yield return Frames();
            Assert.IsInstanceOf<PauseScreen>(ScreenStack.Existing.Top);
            string file = Path.Combine(folder, "settings.json");
            Assert.IsTrue(File.Exists(file), "written once, when the screen closed");
            var saved = Settings.Parse(File.ReadAllText(file), tuning);
            Assert.AreEqual(tuningFov + 1f, saved.fieldOfView, 1e-4f);

            Assert.AreEqual(tuningEffects, tuning.viewEffectsStrength, "the shared tuning asset is never written");
            Assert.AreEqual(tuningFov, tuning.fieldOfView);

            // Reset to defaults: back to the tuning values at once.
            Click(pause.SettingsButton);
            yield return Frames();
            screen = (SettingsScreen)ScreenStack.Existing.Top;
            var reset = screen.Root.Q<Button>("reset");
            Click(reset);
            yield return null;
            Assert.AreEqual(tuningEffects, Settings.ViewEffectsStrength(tuning), 1e-4f);
            Assert.AreEqual(tuningFov, Settings.FieldOfView(tuning), 1e-4f);
        }
    }
}
