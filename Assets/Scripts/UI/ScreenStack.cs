using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// The UI Toolkit screen router (ARCHITECTURE §6.2): one persistent panel (Resources/<see cref="PanelResource"/>, the
    /// shared theme) holding a stack of <see cref="UIScreen"/>s. Only the top screen shows and takes input. Back (the UI
    /// Cancel action: Esc, gamepad B) goes to the top screen; focus moves to a screen's first element when it appears
    /// and comes back where it was when the screen above closes. Keyboard and gamepad navigation come through an
    /// <see cref="EventSystem"/> with the Input System UI module (created if the scene has none). While a screen that
    /// needs the mouse is on top, <see cref="CursorPolicy"/> shows the cursor. Created on first use, kept across scenes.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class ScreenStack : MonoBehaviour
    {
        public const string PanelResource = "HotPatataPanel";
        public const int SortingOrder = 100;   // above any world/HUD panel

        static ScreenStack instance;

        readonly List<UIScreen> screens = new List<UIScreen>();
        readonly Dictionary<UIScreen, Focusable> focusBelow = new Dictionary<UIScreen, Focusable>();
        UIDocument document;
        VisualElement layer;

        /// <summary>Raised after a push, pop or clear.</summary>
        public event Action Changed;

        /// <summary>The stack if it exists (never creates it).</summary>
        public static ScreenStack Existing => instance;

        /// <summary>True while any screen is open: gameplay keys such as Esc must leave it alone.</summary>
        public static bool AnyOpen => instance != null && instance.screens.Count > 0;

        public int Count => screens.Count;
        public UIScreen Top => screens.Count > 0 ? screens[screens.Count - 1] : null;
        public UIDocument Document => document;

        /// <summary>The stack, created (with its panel and, if needed, an EventSystem) on first use.</summary>
        public static ScreenStack Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("UIRoot");
            go.SetActive(false);   // set the panel before UIDocument enables
            DontDestroyOnLoad(go);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>(PanelResource);
            if (doc.panelSettings == null) Debug.LogError($"[UI] missing Resources/{PanelResource} (Assets/UI/Resources)");
            doc.sortingOrder = SortingOrder;
            instance = go.AddComponent<ScreenStack>();
            go.SetActive(true);
            return instance;
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            document = GetComponent<UIDocument>();
            EnsureEventSystem(transform);
        }

        void OnEnable() => EnsureLayer();

        /// <summary>
        /// The layer the screens live in, set up once the document has its root (UIDocument may enable after this
        /// component, and rebuilds its root when re-enabled).
        /// </summary>
        VisualElement EnsureLayer()
        {
            var root = document != null ? document.rootVisualElement : null;
            if (root == null) return null;
            if (layer != null && layer.parent == root) return layer;
            root.pickingMode = PickingMode.Ignore;   // an empty stack never eats clicks
            layer = new VisualElement { name = "hp-stack", pickingMode = PickingMode.Ignore };
            layer.AddToClassList("hp-stack");
            root.Add(layer);
            root.RegisterCallback<NavigationCancelEvent>(OnCancel);
            foreach (var s in screens) layer.Add(s.Root);
            Refresh();
            return layer;
        }

        void OnDisable()
        {
            document?.rootVisualElement?.UnregisterCallback<NavigationCancelEvent>(OnCancel);
        }

        void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            CursorPolicy.SetScreenWantsCursor(false);
        }

        /// <summary>Shows <paramref name="screen"/> on top of the others.</summary>
        public void Push(UIScreen screen)
        {
            if (screen == null || screens.Contains(screen)) return;
            var below = Top;
            if (below != null) focusBelow[screen] = Focused();
            screen.Stack = this;
            var host = EnsureLayer();
            if (host == null)
            {
                Debug.LogError("[UI] the screen stack has no panel yet");
                return;
            }
            host.Add(screen.Create());
            screens.Add(screen);
            if (below != null) below.OnHide();
            screen.OnShow();
            Refresh();
            FocusLater(screen.FirstFocus());
            Changed?.Invoke();
        }

        /// <summary>Closes the top screen; the one below shows again with its focus back.</summary>
        public void Pop()
        {
            var top = Top;
            if (top == null) return;
            screens.RemoveAt(screens.Count - 1);
            top.OnHide();
            top.Root.RemoveFromHierarchy();
            var below = Top;
            focusBelow.TryGetValue(top, out var restore);
            focusBelow.Remove(top);
            if (below != null) below.OnShow();
            Refresh();
            if (below != null) FocusLater(restore ?? below.FirstFocus());
            Changed?.Invoke();
        }

        /// <summary>Closes every screen (leaving a game, loading a level).</summary>
        public void Clear()
        {
            while (screens.Count > 0) Pop();
        }

        /// <summary>Back, as if Cancel was pressed: the top screen decides (<see cref="UIScreen.OnBack"/>).</summary>
        public void Back() => Top?.OnBack();

        void OnCancel(NavigationCancelEvent e)
        {
            if (Top == null) return;
            Back();
            e.StopPropagation();
        }

        /// <summary>Only the top screen is shown; the cursor follows the top screen's needs.</summary>
        void Refresh()
        {
            for (int i = 0; i < screens.Count; i++)
                screens[i].Root.style.display = i == screens.Count - 1 ? DisplayStyle.Flex : DisplayStyle.None;
            CursorPolicy.SetScreenWantsCursor(Top != null && Top.ReleasesCursor);
        }

        Focusable Focused() => document.rootVisualElement.panel?.focusController.focusedElement;

        /// <summary>Focus once the screen has been laid out (hidden or unlaid elements cannot take focus).</summary>
        static void FocusLater(Focusable target)
        {
            if (target is VisualElement ve) ve.schedule.Execute(() => target.Focus());
            else target?.Focus();
        }

        /// <summary>UI Toolkit reads keyboard/gamepad navigation through the EventSystem; one is made if the scene has none.</summary>
        static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();   // default UI actions: arrows / WASD / stick / d-pad, Enter / A, Esc / B
        }
    }
}
