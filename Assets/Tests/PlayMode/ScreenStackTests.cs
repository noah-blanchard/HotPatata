using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HotPatata.Tests
{
    /// <summary>
    /// #14: the UI Toolkit foundation. One persistent themed panel with an EventSystem for keyboard/gamepad navigation;
    /// screens stack with only the top one shown; focus goes to a screen's first element and comes back on close; Back
    /// (the Cancel navigation event) closes the top screen unless it refuses; Submit presses the focused button; an open
    /// screen frees the cursor and closing it gives the lock back to gameplay. #76: every screen layout is in the
    /// catalog, a missing UXML element fails at once with the screen and the element named, and up / down follow the
    /// layout's order. Navigation is sent as UI Toolkit events, never through simulated devices.
    /// </summary>
    public class ScreenStackTests
    {
        class TestScreen : UIScreen
        {
            readonly bool canGoBack;
            public Button First, Second;
            public int Presses;

            public TestScreen(bool canGoBack = true) => this.canGoBack = canGoBack;

            public override bool CanGoBack => canGoBack;

            protected override VisualElement Build()
            {
                var root = new VisualElement();
                root.AddToClassList("hp-overlay");
                root.Add(new Label("Test"));
                First = new Button(() => Presses++) { text = "First" };
                First.AddToClassList("hp-button");
                Second = new Button { text = "Second" };
                Second.AddToClassList("hp-button");
                root.Add(First);
                root.Add(Second);
                return root;
            }
        }

        /// <summary>A screen whose layout lacks the element its code requires (a renamed or deleted UXML element).</summary>
        class BrokenScreen : UIScreen
        {
            readonly VisualTreeAsset template;

            public BrokenScreen(VisualTreeAsset template) => this.template = template;

            protected override VisualElement Build()
            {
                var root = FromTemplate(template);
                Require<Button>("missing-button");
                return root;
            }
        }

        /// <summary>Three buttons top to bottom, made navigable out of order: the walk must follow the tree.</summary>
        class OrderScreen : UIScreen
        {
            public Button A, B, C;

            protected override VisualElement Build()
            {
                var root = new VisualElement();
                A = new Button { text = "A" };
                B = new Button { text = "B" };
                C = new Button { text = "C" };
                root.Add(A);
                root.Add(B);
                root.Add(C);
                Navigable(B);
                Navigable(A);
                Navigable(C);
                return root;
            }
        }

        ScreenStack stack;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            CursorPolicy.SetGameplayLock(false);
            stack = ScreenStack.Get();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (stack != null) Object.Destroy(stack.gameObject);
            CursorPolicy.SetGameplayLock(false);
            yield return null;
        }

        static IEnumerator Frames(int n = 3)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        Focusable Focused => stack.Document.rootVisualElement.panel.focusController.focusedElement;

        static void Send(EventBase e, VisualElement target)
        {
            e.target = target;
            target.SendEvent(e);
        }

        [UnityTest]
        public IEnumerator Get_MakesOnePersistentThemedPanel_WithKeyboardAndGamepadNavigation()
        {
            Assert.AreSame(stack, ScreenStack.Get(), "one stack");
            Assert.IsNotNull(stack.Document.panelSettings, "Resources/" + ScreenStack.PanelResource);
            Assert.IsNotNull(stack.Document.panelSettings.themeStyleSheet, "the HotPatata theme");
            Assert.AreEqual(ScreenStack.SortingOrder, stack.Document.sortingOrder);

            var events = Object.FindAnyObjectByType<EventSystem>();
            Assert.IsNotNull(events, "an EventSystem feeds UI Toolkit navigation");
            var module = events.GetComponent<InputSystemUIInputModule>();
            Assert.IsNotNull(module, "the Input System UI module (no legacy input)");
            Assert.IsNotNull(module.move?.action, "move: arrows / WASD / stick / d-pad");
            Assert.IsNotNull(module.submit?.action, "submit: Enter / A");
            Assert.IsNotNull(module.cancel?.action, "cancel: Esc / B");
            Assert.IsFalse(ScreenStack.AnyOpen);
            yield break;
        }

        [UnityTest]
        public IEnumerator Push_ShowsOnlyTheTop_FocusesIt_AndPopGivesTheFocusBack()
        {
            var a = new TestScreen();
            stack.Push(a);
            yield return Frames();
            Assert.IsTrue(ScreenStack.AnyOpen);
            Assert.AreSame(a.First, Focused, "the first button takes focus");

            a.Second.Focus();
            var b = new TestScreen();
            stack.Push(b);
            yield return Frames();
            Assert.AreEqual(2, stack.Count);
            Assert.AreEqual(DisplayStyle.None, a.Root.resolvedStyle.display, "only the top screen shows");
            Assert.AreSame(b.First, Focused);

            stack.Pop();
            yield return Frames();
            Assert.AreSame(a, stack.Top);
            Assert.AreEqual(DisplayStyle.Flex, a.Root.resolvedStyle.display);
            Assert.AreSame(a.Second, Focused, "focus comes back where it was");

            stack.Clear();
            Assert.AreEqual(0, stack.Count);
            Assert.IsFalse(ScreenStack.AnyOpen);
        }

        [UnityTest]
        public IEnumerator Cancel_GoesBack_UnlessTheScreenRefuses()
        {
            var root = new TestScreen(canGoBack: false);
            var child = new TestScreen();
            stack.Push(root);
            stack.Push(child);
            yield return Frames();

            using (var e = NavigationCancelEvent.GetPooled()) Send(e, child.First);
            yield return Frames();
            Assert.AreSame(root, stack.Top, "Esc / B closes the top screen");

            using (var e = NavigationCancelEvent.GetPooled()) Send(e, root.First);
            yield return Frames();
            Assert.AreSame(root, stack.Top, "a root menu stays");
        }

        [UnityTest]
        public IEnumerator Navigation_MovesFocus_AndSubmitPressesTheFocusedButton()
        {
            var s = new TestScreen();
            stack.Push(s);
            yield return Frames();
            Assert.AreSame(s.First, Focused);

            using (var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next)) Send(e, s.First);
            yield return Frames();
            Assert.AreSame(s.Second, Focused, "down / next moves to the next button");

            s.First.Focus();
            using (var e = NavigationSubmitEvent.GetPooled()) Send(e, s.First);
            yield return Frames();
            Assert.AreEqual(1, s.Presses, "Enter / A presses the focused button");
        }

        [Test]
        public void Catalog_LoadsFromResources_WithEveryScreenLayout()
        {
            var catalog = stack.Catalog;
            Assert.IsNotNull(catalog, "Resources/" + ScreenStack.CatalogResource);
            foreach (var field in typeof(UIScreenCatalog).GetFields())
                if (field.FieldType == typeof(VisualTreeAsset))
                    Assert.IsNotNull(field.GetValue(catalog), $"{field.Name}: a UXML in Assets/UI/Screens");
        }

        [Test]
        public void AMissingElement_FailsAtOnce_NamingTheScreenAndTheElement()
        {
            var template = ScriptableObject.CreateInstance<VisualTreeAsset>();
            template.name = "Broken";
            var error = Assert.Throws<System.InvalidOperationException>(() => stack.Push(new BrokenScreen(template)));
            StringAssert.Contains(nameof(BrokenScreen), error.Message);
            StringAssert.Contains("Broken.uxml", error.Message);
            StringAssert.Contains("missing-button", error.Message);
            Assert.AreEqual(0, stack.Count, "the broken screen is not shown");
            Object.Destroy(template);
        }

        [UnityTest]
        public IEnumerator UpDown_FollowsTheLayoutOrder_NotTheOrderTheCodeListedThem()
        {
            var s = new OrderScreen();
            stack.Push(s);
            yield return Frames();
            s.A.Focus();
            using (var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Down)) Send(e, s.A);
            yield return Frames();
            Assert.AreSame(s.B, Focused, "down from the top button goes to the one under it");
            using (var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Down)) Send(e, s.B);
            yield return Frames();
            Assert.AreSame(s.C, Focused);
        }

        [UnityTest]
        public IEnumerator APushedScreen_PopsIn_AndItsButtonsCascade_ThenSettle()
        {
            var s = new OrderScreen();
            stack.Push(s);
            Assert.IsTrue(s.Root.ClassListContains(UIScreen.EnterClass), "the entrance starts hidden");
            Assert.IsTrue(s.B.ClassListContains(UIScreen.StaggerClass + "-2"), "the second button in the walk comes in second");
            yield return new WaitForSecondsRealtime(0.15f);   // the scheduler ticks on time, not frames
            Assert.IsFalse(s.Root.ClassListContains(UIScreen.EnterClass), "then the screen transitions in");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsFalse(s.A.ClassListContains(UIScreen.StaggerClass), "once in, hover and focus are instant again");
            Assert.AreSame(s.A, Focused, "the entrance never moves the focus");
        }

        [UnityTest]
        public IEnumerator AScreen_BuildsFromItsLayout_WithTheCallersTexts()
        {
            var confirm = new ConfirmScreen("Leave?", "Really.", "Go", null);
            stack.Push(confirm);
            yield return Frames();
            Assert.AreEqual("Leave?", confirm.Root.Q<Label>("title").text);
            Assert.AreEqual("GO", confirm.ConfirmLabel.text, "the caller's answer, in capitals like every button");
            Assert.AreSame(confirm.Cancel, Focused, "the safe answer has the focus");
            Assert.IsTrue(confirm.Root.ClassListContains("hp-screen"));
        }

        [UnityTest]
        public IEnumerator AnOpenScreen_FreesTheCursor_AndClosingItGivesTheLockBack()
        {
            CursorPolicy.SetGameplayLock(true);
            Assert.IsTrue(CursorPolicy.IsLocked, "a player looking around");

            stack.Push(new TestScreen());
            yield return null;
            Assert.IsFalse(CursorPolicy.IsLocked, "the screen needs the mouse");
            Assert.AreEqual(CursorLockMode.None, UnityEngine.Cursor.lockState);
            Assert.IsTrue(UnityEngine.Cursor.visible);

            stack.Clear();
            yield return null;
            Assert.IsTrue(CursorPolicy.IsLocked, "back to gameplay");
            Assert.IsTrue(CursorPolicy.GameplayWantsLock, "the screen never changed what gameplay asked for");
        }
    }
}
