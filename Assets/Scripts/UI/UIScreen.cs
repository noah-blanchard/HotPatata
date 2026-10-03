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

        protected abstract VisualElement Build();

        internal VisualElement Create()
        {
            if (Root != null) return Root;
            Root = Build();
            Root.AddToClassList("hp-screen");
            return Root;
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
