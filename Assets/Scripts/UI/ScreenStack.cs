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
    /// shared theme) holding a stack of <see cref="UIScreen"/>s, whose layouts come from the <see cref="Catalog"/>. Only the top screen shows and takes input. Back (the UI
    /// Cancel action: Esc, gamepad B) goes to the top screen; focus moves to a screen's first element when it appears
    /// and comes back where it was when the screen above closes. Keyboard and gamepad navigation come through an
    /// <see cref="EventSystem"/> with the Input System UI module (created if the scene has none). While a screen that
    /// needs the mouse is on top, <see cref="CursorPolicy"/> shows the cursor. Created on first use, kept across scenes.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class ScreenStack : MonoBehaviour
    {
        public const string PanelResource = "HotPatataPanel";
        public const string CatalogResource = "HotPatataScreens";
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

        /// <summary>The top screen takes the local player's gameplay input (<see cref="UIScreen.BlocksGameplay"/>).</summary>
        public static bool BlocksGameplay => instance != null && instance.Top != null && instance.Top.BlocksGameplay;

        /// <summary>
        /// The frame of the last push or pop. One key can mean both "back" (the UI Cancel action) and "pause" (gameplay):
        /// whoever sees it second on that frame leaves it alone, so Esc never closes and reopens a menu at once.
        /// </summary>
        public int LastChangeFrame { get; private set; } = -1;

        public int Count => screens.Count;
        public UIScreen Top => screens.Count > 0 ? screens[screens.Count - 1] : null;

        /// <summary>Is a screen of this kind open anywhere in the stack?</summary>
        public bool Has<T>() where T : UIScreen => screens.Exists(s => s is T);
        public UIDocument Document => document;

        /// <summary>The screens' UXML layouts (Resources/<see cref="CatalogResource"/>), loaded on first use.</summary>
        public UIScreenCatalog Catalog
        {
            get
            {
                if (catalog != null) return catalog;
                catalog = Resources.Load<UIScreenCatalog>(CatalogResource);
                if (catalog == null) Debug.LogError($"[UI] missing Resources/{CatalogResource} (Assets/UI/Resources)");
                return catalog;
            }
        }

        UIScreenCatalog catalog;

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
            Clear();   // closing screens undo what they did (the pause menu's freeze, the settings' unsaved edits)
            instance = null;
            CursorPolicy.SetScreenWantsCursor(false);
        }

        /// <summary>Shows <paramref name="screen"/> on top of the others.</summary>
        public void Push(UIScreen screen)
        {
            if (screen == null || screens.Contains(screen)) return;
            var below = Top;
            screen.Stack = this;
            var host = EnsureLayer();
            if (host == null)
            {
                Debug.LogError("[UI] the screen stack has no panel yet");
                return;
            }
            var root = screen.Create();   // first: a broken layout throws here and leaves the stack as it was
            if (below != null) focusBelow[screen] = Focused();
            host.Add(root);
            screens.Add(screen);
            if (below != null) below.OnHide();
            screen.OnShow();
            LastChangeFrame = Time.frameCount;
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
            LastChangeFrame = Time.frameCount;
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
