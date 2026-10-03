using System.Collections.Generic;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// One UI Toolkit screen on the <see cref="ScreenStack"/> (ARCHITECTURE §6.2): it builds its visual tree once (in
    /// code, or from a UXML template) and styles it with the shared theme's classes (<c>hp-*</c>, Assets/UI/Styles).
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

        protected abstract VisualElement Build();

        internal VisualElement Create()
        {
            if (Root != null) return Root;
            Root = Build();
            Root.AddToClassList("hp-screen");
            if (navigation.Count > 0) Root.RegisterCallback<NavigationMoveEvent>(OnNavigate);
            return Root;
        }

        /// <summary>
        /// Puts <paramref name="element"/> in the screen's up/down order (call in <see cref="Build"/>, top to bottom).
        /// Up / down (arrows, d-pad, stick) then walk this list, skipping disabled entries; left / right are left to the
        /// element (a setting row changes its value). Without it, focus moves in UI Toolkit's default order.
        /// </summary>
        protected T Navigable<T>(T element) where T : VisualElement
        {
            navigation.Add(element);
            return element;
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

        /// <summary>Builds the screen from a UXML template (the template's root is wrapped in a full-screen container).</summary>
        protected static VisualElement FromTemplate(VisualTreeAsset template)
        {
            var root = template.Instantiate();
            root.style.flexGrow = 1;
            return root;
        }
    }
}
