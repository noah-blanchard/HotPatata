using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// One UI Toolkit screen on the <see cref="ScreenStack"/> (ARCHITECTURE §6.2): its layout is a UXML file in
    /// Assets/UI/Screens (<see cref="FromTemplate"/>), styled only with the shared theme's classes (<c>hp-*</c>,
    /// Assets/UI/Styles); the code finds the named elements once (<see cref="Require{T}"/>) and wires behaviour.
    /// </summary>
    public abstract class UIScreen
    {
        /// <summary>USS class put on the element that takes focus when the screen is shown (else the first focusable one).</summary>
        public const string FirstFocusClass = "hp-first-focus";

        public VisualElement Root { get; private set; }
        public ScreenStack Stack { get; internal set; }

        /// <summary>Shows the mouse cursor while this screen is on top (menus do; a HUD would not).</summary>
        public virtual bool ReleasesCursor => true;

        /// <summary>Back (Esc / gamepad B) closes this screen. A root menu with nowhere to go back to says no.</summary>
        public virtual bool CanGoBack => true;

        /// <summary>While this screen is on top the local player's gameplay input is ignored (menus do; a HUD would not).</summary>
        public virtual bool BlocksGameplay => true;

        readonly List<VisualElement> navigation = new List<VisualElement>();
        VisualElement tree;
        string templateName;

        /// <summary>Builds the screen: <see cref="FromTemplate"/> its layout, <see cref="Require{T}"/> the named elements, wire behaviour.</summary>
        protected abstract VisualElement Build();

        internal VisualElement Create()
        {
            if (Root != null) return Root;
            Root = Build();
            Root.AddToClassList("hp-screen");
            SortNavigationByTree();
            if (navigation.Count > 0) Root.RegisterCallback<NavigationMoveEvent>(OnNavigate);
            return Root;
        }

        /// <summary>The screens' layouts (Resources/HotPatataScreens).</summary>
        protected UIScreenCatalog Templates => (Stack != null ? Stack : ScreenStack.Get()).Catalog;

        /// <summary>
        /// Puts <paramref name="element"/> in the screen's up/down walk (call in <see cref="Build"/>). The walk follows
        /// the elements' order in the tree, so reordering them in UI Builder reorders it too. Up / down (arrows, d-pad,
        /// stick) walk this list, skipping disabled or hidden entries; left / right are left to the element (a setting row
        /// changes its value). Without it, focus moves in UI Toolkit's default order.
        /// </summary>
        protected T Navigable<T>(T element) where T : VisualElement
        {
            navigation.Add(element);
            return element;
        }

        // Tree order (depth first); an element outside the tree keeps its call order, after the others.
        void SortNavigationByTree()
        {
            if (navigation.Count < 2) return;
            var order = new Dictionary<VisualElement, int>();
            IndexTree(Root, order);
            var sorted = navigation
                .Select((element, call) => (element, call))
                .OrderBy(p => order.TryGetValue(p.element, out int index) ? index : int.MaxValue)
                .ThenBy(p => p.call)
                .Select(p => p.element)
                .ToList();
            navigation.Clear();
            navigation.AddRange(sorted);
        }

        static void IndexTree(VisualElement e, Dictionary<VisualElement, int> order)
        {
            order[e] = order.Count;
            for (int i = 0; i < e.hierarchy.childCount; i++) IndexTree(e.hierarchy[i], order);
        }

        static bool KeyboardHeld => UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.anyKey.isPressed;

        void OnNavigate(NavigationMoveEvent e)
        {
            int step = e.direction == NavigationMoveEvent.Direction.Up ? -1 : e.direction == NavigationMoveEvent.Direction.Down ? 1 : 0;
            if (step == 0) return;
            // Typing in a text field: the keyboard's arrows and WASD belong to the text (Tab still moves on); a gamepad navigates.
            if (KeyboardHeld && (e.target as VisualElement)?.GetFirstOfType<TextField>() != null) return;
            int from = navigation.FindIndex(n => n.focusController != null && n.focusController.focusedElement == n);
            for (int i = 1; i <= navigation.Count; i++)
            {
                var next = navigation[((from < 0 ? (step > 0 ? -1 : 0) : from) + step * i + navigation.Count * 2) % navigation.Count];
                if (!next.enabledInHierarchy || !next.canGrabFocus || next.resolvedStyle.display == DisplayStyle.None) continue;
                next.Focus();
                break;
            }
            Root.focusController?.IgnoreEvent(e);
            e.StopPropagation();
        }

        public virtual void OnShow() { }
        public virtual void OnHide() { }

        /// <summary>Back was pressed while this screen was on top.</summary>
        public virtual void OnBack()
        {
            if (CanGoBack) Stack.Pop();
        }

        /// <summary>The element focused when the screen appears.</summary>
        public virtual Focusable FirstFocus()
        {
            var marked = Root.Q(className: FirstFocusClass);
            if (marked != null && marked.focusable) return marked;
            return FindFocusable(Root);
        }

        static Focusable FindFocusable(VisualElement e)
        {
            if (e.focusable && e.canGrabFocus && e.resolvedStyle.display != DisplayStyle.None) return e;
            foreach (var child in e.Children())
            {
                var f = FindFocusable(child);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>
        /// Builds the screen from its UXML layout (Assets/UI/Screens, the template's root wrapped in a full-screen
        /// container). <see cref="Require{T}"/> then finds the named elements in it.
        /// </summary>
        protected VisualElement FromTemplate(VisualTreeAsset template)
        {
            if (template == null)
                throw new InvalidOperationException($"[UI] {GetType().Name}: no UXML template (assign it in Assets/UI/Resources/{ScreenStack.CatalogResource})");
            var root = template.Instantiate();
            root.style.flexGrow = 1;
            tree = root;
            templateName = template.name;
            return root;
        }

        /// <summary>
        /// The element of this screen's layout named <paramref name="name"/> (kebab-case, set in UXML). A renamed or
        /// deleted element fails at once, naming the screen and the element, so a broken UXML never half-works.
        /// </summary>
        protected T Require<T>(string name) where T : VisualElement
        {
            if (tree == null) throw new InvalidOperationException($"[UI] {GetType().Name}: Require(\"{name}\") before FromTemplate");
            return tree.Q<T>(name) ?? throw new InvalidOperationException(
                $"[UI] {GetType().Name} ({templateName}.uxml): no {typeof(T).Name} named \"{name}\"");
        }

        /// <summary>Shows or hides an element (behaviour that the layout leaves to the code, e.g. the host-only controls).</summary>
        protected static void Show(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
