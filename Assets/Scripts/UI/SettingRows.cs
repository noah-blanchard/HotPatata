using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// The base of a settings row (ARCHITECTURE §6.2): one focusable line (<c>hp-row</c>) with a label on the left, so a
    /// gamepad walks rows with up / down (<see cref="UIScreen.Navigable{T}"/>) and changes the focused row with left /
    /// right. The inner field is not focusable, it only takes the mouse. The rows are UXML elements (UI Builder library,
    /// Project &gt; HotPatata): the layout gives the label, the screen's code gives the range, choices and value.
    /// </summary>
    [UxmlElement]   // abstract: not in the library, but its label attribute is inherited by the rows
    public abstract partial class SettingRow : VisualElement
    {
        readonly Label title;

        /// <summary>The text on the left (the <c>label</c> attribute in UXML).</summary>
        [UxmlAttribute("label")]
        public string LabelText
        {
            get => title.text;
            set => title.text = value;
        }

        protected SettingRow()
        {
            focusable = true;
            AddToClassList("hp-row");
            title = new Label();
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
    [UxmlElement]
    public partial class SliderRow : SettingRow
    {
        readonly Slider slider;
        readonly Label valueLabel;
        Func<float, string> format = Percent;

        public float StepSize { get; private set; } = 0.05f;
        public float Value => slider.value;
        public event Action<float> Changed;

        public SliderRow()
        {
            slider = new Slider(0f, 1f) { focusable = false, fill = true };
            slider.AddToClassList("hp-row__field");
            slider.RegisterValueChangedCallback(e =>
            {
                valueLabel.text = format(e.newValue);
                Changed?.Invoke(e.newValue);
            });
            Add(slider);
            valueLabel = new Label(format(slider.value));
            valueLabel.AddToClassList("hp-row__value");
            Add(valueLabel);
        }

        public SliderRow(string label, float min, float max, float value, float step, Func<float, string> format) : this()
        {
            LabelText = label;
            Setup(min, max, value, step, format);
        }

        /// <summary>The range, the value (without notifying), the left / right step and how the value reads.</summary>
        public void Setup(float min, float max, float value, float step, Func<float, string> format)
        {
            this.format = format ?? Percent;
            StepSize = step;
            slider.lowValue = min;
            slider.highValue = max;
            SetValueWithoutNotify(value);
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
    [UxmlElement]
    public partial class ToggleRow : SettingRow
    {
        readonly Toggle toggle;

        public bool Value => toggle.value;
        public event Action<bool> Changed;

        public ToggleRow()
        {
            toggle = new Toggle { focusable = false };
            toggle.AddToClassList("hp-row__toggle");
            toggle.RegisterValueChangedCallback(e => Changed?.Invoke(e.newValue));
            Add(toggle);
        }

        public ToggleRow(string label, bool value) : this()
        {
            LabelText = label;
            SetValueWithoutNotify(value);
        }

        protected override void Step(int direction) => toggle.value = !toggle.value;

        public void SetValueWithoutNotify(bool value) => toggle.SetValueWithoutNotify(value);
    }

    /// <summary>One of a list: ‹ value › arrows for the mouse, left / right (or Enter / A, forward) cycle it.</summary>
    [UxmlElement]
    public partial class ChoiceRow : SettingRow
    {
        readonly List<string> options = new List<string>();
        readonly Label valueLabel;

        public int Index { get; private set; }
        public event Action<int> Changed;

        public ChoiceRow()
        {
            var box = new VisualElement();
            box.AddToClassList("hp-row__field");
            box.AddToClassList("hp-choice");
            var previous = new Button(() => Step(-1)) { text = "‹", focusable = false };
            previous.AddToClassList("hp-choice__arrow");
            valueLabel = new Label();
            valueLabel.AddToClassList("hp-choice__value");
            var next = new Button(() => Step(1)) { text = "›", focusable = false };
            next.AddToClassList("hp-choice__arrow");
            box.Add(previous);
            box.Add(valueLabel);
            box.Add(next);
            Add(box);
        }

        public ChoiceRow(string label, IList<string> choices, int index) : this()
        {
            LabelText = label;
            SetOptions(choices, index);
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
