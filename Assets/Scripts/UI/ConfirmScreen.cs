using System;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>A yes / no question (ARCHITECTURE §6.2). The safe answer (cancel) has the focus; Back cancels.</summary>
    public class ConfirmScreen : UIScreen
    {
        readonly string title, message, confirmText;
        readonly Action onConfirm;

        public ConfirmScreen(string title, string message, string confirmText, Action onConfirm)
        {
            this.title = title;
            this.message = message;
            this.confirmText = confirmText;
            this.onConfirm = onConfirm;
        }

        public Button Cancel { get; private set; }
        public Button Confirm { get; private set; }

        protected override VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("hp-overlay");
            var panel = new VisualElement();
            panel.AddToClassList("hp-panel");
            root.Add(panel);
            var heading = new Label(title);
            heading.AddToClassList("hp-title");
            panel.Add(heading);
            var text = new Label(message);
            text.AddToClassList("hp-hint");
            panel.Add(text);

            Cancel = Navigable(new Button(() => Stack.Pop()) { text = "Cancel" });
            Cancel.AddToClassList("hp-button");
            Cancel.AddToClassList(FirstFocusClass);
            panel.Add(Cancel);
            Confirm = Navigable(new Button(() =>
            {
                Stack.Pop();
                onConfirm?.Invoke();
            }) { text = confirmText });
            Confirm.AddToClassList("hp-button");
            Confirm.AddToClassList("hp-button--danger");
            panel.Add(Confirm);
            return root;
        }
    }
}
