using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// The game code as <see cref="SessionService.CodeLength"/> character dials (ARCHITECTURE §6.2), so every device can
    /// enter it: the row is one focusable element in the board's up / down walk; Enter / A starts editing, then up / down
    /// turn the current dial through <see cref="Alphabet"/>, left / right pick the dial, and Enter / A or Esc / B stop
    /// (Esc never leaves the station while editing). The keyboard types straight in (a letter or digit fills the current
    /// dial and moves on, Backspace clears back, Ctrl+V pastes a code); the mouse clicks a dial or its ▲ / ▼.
    /// </summary>
    [UxmlElement]
    public partial class CodeDials : VisualElement
    {
        /// <summary>The characters of a game code, in dial order (letters, then digits).</summary>
        public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        public const char Empty = '\0';

        public const string EditingClass = "hp-dials--editing";
        public const string CurrentClass = "hp-dial--current";

        readonly char[] cells = new char[SessionService.CodeLength];
        readonly Label[] glyphs = new Label[SessionService.CodeLength];
        readonly VisualElement[] dials = new VisualElement[SessionService.CodeLength];
        int cursor;
        bool editing;

        /// <summary>Raised when a dial changes, with the new <see cref="Value"/>.</summary>
        public event Action<string> Changed;

        /// <summary>The code: the filled dials in order (a full code has <see cref="SessionService.CodeLength"/> characters).</summary>
        public string Value
        {
            get
            {
                var chars = new System.Text.StringBuilder(cells.Length);
                foreach (char c in cells)
                    if (c != Empty) chars.Append(c);
                return chars.ToString();
            }
        }

        /// <summary>True while the dials take up / down / left / right (after Enter / A).</summary>
        public bool Editing => editing;

        /// <summary>The dial that up / down and typing change.</summary>
        public int Cursor => cursor;

        public CodeDials()
        {
            focusable = true;
            AddToClassList("hp-dials");
            for (int i = 0; i < cells.Length; i++)
            {
                int index = i;
                var dial = new VisualElement();
                dial.AddToClassList("hp-dial");
                var up = new Button(() => Click(index, 1)) { text = "▲", focusable = false };
                up.AddToClassList("hp-dial__arrow");
                var glyph = new Label();
                glyph.AddToClassList("hp-dial__glyph");
                glyph.RegisterCallback<PointerDownEvent>(_ => Click(index, 0));
                var down = new Button(() => Click(index, -1)) { text = "▼", focusable = false };
                down.AddToClassList("hp-dial__arrow");
                dial.Add(up);
                dial.Add(glyph);
                dial.Add(down);
                Add(dial);
                dials[i] = dial;
                glyphs[i] = glyph;
            }
            RegisterCallback<NavigationSubmitEvent>(OnSubmit);
            RegisterCallback<NavigationMoveEvent>(OnMove);
            RegisterCallback<NavigationCancelEvent>(OnCancel);
            RegisterCallback<KeyDownEvent>(OnKey);
            RegisterCallback<FocusOutEvent>(_ => SetEditing(false));
            Redraw();
        }

        /// <summary>Fills the dials from <paramref name="code"/> (normalised, cut to the code length), without notifying.</summary>
        public void SetValueWithoutNotify(string code)
        {
            string normalized = SessionService.NormalizeCode(code);
            for (int i = 0; i < cells.Length; i++)
                cells[i] = i < normalized.Length && Alphabet.IndexOf(normalized[i]) >= 0 ? normalized[i] : Empty;
            cursor = Mathf.Min(normalized.Length, cells.Length - 1);
            Redraw();
        }

        /// <summary>Turns the current dial by <paramref name="steps"/> (an empty dial starts at the first or last character).</summary>
        public void Spin(int steps)
        {
            int at = cells[cursor] == Empty ? (steps > 0 ? -1 : 0) : Alphabet.IndexOf(cells[cursor]);
            int n = Alphabet.Length;
            cells[cursor] = Alphabet[((at + steps) % n + n) % n];
            Notify();
        }

        /// <summary>Moves the current dial by <paramref name="steps"/>, staying on the row.</summary>
        public void MoveCursor(int steps)
        {
            cursor = Mathf.Clamp(cursor + steps, 0, cells.Length - 1);
            Redraw();
        }

        /// <summary>Types one character: a letter or digit fills the current dial and moves on; anything else is ignored.</summary>
        public bool Type(char c)
        {
            c = char.ToUpperInvariant(c);
            if (Alphabet.IndexOf(c) < 0) return false;
            cells[cursor] = c;
            if (cursor < cells.Length - 1) cursor++;
            Notify();
            return true;
        }

        /// <summary>Clears the current dial, or the one before it if it is already empty.</summary>
        public void Erase()
        {
            if (cells[cursor] == Empty && cursor > 0) cursor--;
            cells[cursor] = Empty;
            Notify();
        }

        public void SetEditing(bool on)
        {
            if (editing == on) return;
            editing = on;
            Redraw();
        }

        void Click(int index, int steps)
        {
            Focus();
            cursor = index;
            SetEditing(true);
            if (steps != 0) Spin(steps);
            else Redraw();
        }

        void OnSubmit(NavigationSubmitEvent e)
        {
            SetEditing(!editing);
            e.StopPropagation();
        }

        void OnMove(NavigationMoveEvent e)
        {
            // Typing: WASD also reach the UI as navigation; the key itself fills the dial (OnKey). Arrows still move on.
            if (LetterKeyHeld) { StopNavigation(e); return; }
            if (!editing) return;   // up / down: the board moves the focus
            switch (e.direction)
            {
                case NavigationMoveEvent.Direction.Up: Spin(1); break;
                case NavigationMoveEvent.Direction.Down: Spin(-1); break;
                case NavigationMoveEvent.Direction.Left: MoveCursor(-1); break;
                case NavigationMoveEvent.Direction.Right: MoveCursor(1); break;
                default: return;
            }
            StopNavigation(e);
        }

        void StopNavigation(NavigationMoveEvent e)
        {
            focusController?.IgnoreEvent(e);
            e.StopPropagation();
        }

        void OnCancel(NavigationCancelEvent e)
        {
            if (!editing) return;   // Back leaves the station
            SetEditing(false);
            e.StopPropagation();
        }

        void OnKey(KeyDownEvent e)
        {
            if ((e.ctrlKey || e.commandKey) && e.keyCode == KeyCode.V)
            {
                SetValueWithoutNotify(GUIUtility.systemCopyBuffer);
                Notify();
            }
            else if (e.keyCode == KeyCode.Backspace) Erase();
            else if (e.character == Empty || !Type(e.character)) return;
            SetEditing(true);
            e.StopPropagation();
        }

        static bool LetterKeyHeld
        {
            get
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                return keyboard != null && (keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed);
            }
        }

        void Notify()
        {
            Redraw();
            Changed?.Invoke(Value);
        }

        void Redraw()
        {
            EnableInClassList(EditingClass, editing);
            for (int i = 0; i < cells.Length; i++)
            {
                glyphs[i].text = cells[i] == Empty ? "–" : cells[i].ToString();
                dials[i].EnableInClassList(CurrentClass, i == cursor);
            }
        }
    }
}
