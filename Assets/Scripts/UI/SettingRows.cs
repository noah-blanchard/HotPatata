using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// The base of a settings row (ARCHITECTURE §6.2): one focusable line (<c>hp-row</c>) with a label on the left, so a
    /// gamepad walks rows with up / down (<see cref="UIScreen.Navigable{T}"/>) and changes the focused row with left /
    /// right. The inner field is not focusable, it only takes the mouse.
    /// </summary>
    public abstract class SettingRow : VisualElement
    {
        protected SettingRow(string label)
        {
            focusable = true;
            AddToClassList("hp-row");
            var title = new Label(label);
            title.AddToClassList("hp-row__label");
            Add(title);
            RegisterCallback<NavigationMoveEvent>(OnMove);
            RegisterCallback<NavigationSubmitEvent>(e =>
            {
                Submit();
                e.StopPropagation();
            });
        }

        /// <summary>Left (-1) or right (+1) while the row is focused.</summary>
        protected abstract void Step(int direction);

        /// <summary>Enter / A on the row.</summary>
        protected virtual void Submit() => Step(1);

        void OnMove(NavigationMoveEvent e)
        {
            int direction = e.direction == NavigationMoveEvent.Direction.Left ? -1 : e.direction == NavigationMoveEvent.Direction.Right ? 1 : 0;
            if (direction == 0) return;   // up / down: the screen moves the focus
            Step(direction);
            focusController?.IgnoreEvent(e);
            e.StopPropagation();
        }
    }

    /// <summary>A value in a range: a slider for the mouse, <see cref="StepSize"/> per left / right, the value shown on the right.</summary>
    public class SliderRow : SettingRow
    {
        readonly Slider slider;
        readonly Label valueLabel;
        readonly Func<float, string> format;

        public float StepSize { get; }
        public float Value => slider.value;
        public event Action<float> Changed;

        public SliderRow(string label, float min, float max, float value, float step, Func<float, string> format) : base(label)
        {
            this.format = format;
            StepSize = step;
            slider = new Slider(min, max) { focusable = false };
            slider.AddToClassList("hp-row__field");
            slider.SetValueWithoutNotify(value);
            slider.RegisterValueChangedCallback(e =>
            {
                valueLabel.text = format(e.newValue);
                Changed?.Invoke(e.newValue);
            });
            Add(slider);
            valueLabel = new Label(format(value));
            valueLabel.AddToClassList("hp-row__value");
            Add(valueLabel);
        }

        protected override void Step(int direction) =>
            slider.value = Mathf.Clamp(slider.value + direction * StepSize, slider.lowValue, slider.highValue);

        public void SetValueWithoutNotify(float value)
        {
            slider.SetValueWithoutNotify(value);
            valueLabel.text = format(slider.value);
        }

        /// <summary>0..1 shown as a percentage.</summary>
        public static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";
    }

    /// <summary>On / off: a toggle for the mouse, Enter / A or left / right flips it.</summary>
    public class ToggleRow : SettingRow
    {
        readonly Toggle toggle;

        public bool Value => toggle.value;
        public event Action<bool> Changed;

        public ToggleRow(string label, bool value) : base(label)
        {
            toggle = new Toggle { focusable = false };
            toggle.AddToClassList("hp-row__toggle");
            toggle.SetValueWithoutNotify(value);
            toggle.RegisterValueChangedCallback(e => Changed?.Invoke(e.newValue));
            Add(toggle);
        }

        protected override void Step(int direction) => toggle.value = !toggle.value;

        public void SetValueWithoutNotify(bool value) => toggle.SetValueWithoutNotify(value);
    }

    /// <summary>One of a list: ‹ value › arrows for the mouse, left / right (or Enter / A, forward) cycle it.</summary>
    public class ChoiceRow : SettingRow
    {
        readonly List<string> options;
        readonly Label valueLabel;

        public int Index { get; private set; }
        public event Action<int> Changed;

        public ChoiceRow(string label, IList<string> choices, int index) : base(label)
        {
            options = new List<string>(choices);
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1));
            var box = new VisualElement();
            box.AddToClassList("hp-row__field");
            box.AddToClassList("hp-choice");
            var previous = new Button(() => Step(-1)) { text = "‹", focusable = false };
            previous.AddToClassList("hp-choice__arrow");
            valueLabel = new Label(options.Count > 0 ? options[Index] : "");
            valueLabel.AddToClassList("hp-choice__value");
            var next = new Button(() => Step(1)) { text = "›", focusable = false };
            next.AddToClassList("hp-choice__arrow");
            box.Add(previous);
            box.Add(valueLabel);
            box.Add(next);
            Add(box);
        }

        protected override void Step(int direction)
        {
            if (options.Count == 0) return;
            Index = (Index + direction + options.Count) % options.Count;
            valueLabel.text = options[Index];
            Changed?.Invoke(Index);
        }

        /// <summary>Replaces the choices (e.g. the spawn points of another level) and selects <paramref name="index"/>, without notifying.</summary>
        public void SetOptions(IList<string> choices, int index)
        {
            options.Clear();
            options.AddRange(choices);
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1));
            valueLabel.text = options.Count > 0 ? options[Index] : "";
        }

        public void SetIndexWithoutNotify(int index)
        {
            if (options.Count == 0) return;
            Index = Mathf.Clamp(index, 0, options.Count - 1);
            valueLabel.text = options[Index];
        }
    }
}
