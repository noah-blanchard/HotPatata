using System;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// A yes / no question (ARCHITECTURE §6.2; layout Assets/UI/Screens/Confirm.uxml, the texts from the caller). The
    /// safe answer (cancel) has the focus; Back cancels.
    /// </summary>
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

        /// <summary>The confirm button's text (the caller's answer, in capitals like every button).</summary>
        public Label ConfirmLabel { get; private set; }

        protected override VisualElement Build()
        {
            var root = FromTemplate(Templates.confirm);
            Require<Label>("title").text = title;
            Require<Label>("message").text = message;

            Cancel = Navigable(Require<Button>("cancel"));
            Cancel.clicked += () => Stack.Pop();
            Cancel.AddToClassList(FirstFocusClass);
            Confirm = Navigable(Require<Button>("confirm"));
            ConfirmLabel = Require<Label>("confirm-label");
            ConfirmLabel.text = confirmText.ToUpperInvariant();
            Confirm.clicked += () =>
            {
                Stack.Pop();
                onConfirm?.Invoke();
            };
            return root;
        }
    }
}
