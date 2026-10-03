using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>Small pieces the screens build from data (ARCHITECTURE §6.2): slot chips and the potato spinner.</summary>
    public static class UIParts
    {
        const float WobbleDegrees = 14f;   // the potato spinner rocks this far each way
        const float WobbleSpeed = 5f;      // radians per second
        const float WobbleLift = 6f;       // px it hops at each end
        const int WobbleTickMs = 16;
        const float LightChip = 0.35f;     // above this luminance a chip takes a navy shape (sky, white), else white (blue, plum)

        static readonly Color Ink = new Color32(0x24, 0x30, 0x5E, 0xFF);

        /// <summary>
        /// A slot chip: a disc in the slot's colour holding the slot's shape (spec §19: never colour alone), the shape in
        /// white or navy, whichever reads on that colour.
        /// </summary>
        public static VisualElement Chip(GameTuning tuning, int slot, bool small = false)
        {
            var color = PlayerIdentity.ColorFor(tuning, slot);
            var chip = new VisualElement();
            chip.AddToClassList("hp-chip");
            if (small) chip.AddToClassList("hp-chip--small");
            chip.style.backgroundColor = color;
            var shape = new VisualElement();
            shape.AddToClassList("hp-chip__shape");
            shape.AddToClassList("hp-chip__shape--" + PlayerIdentity.ShapeFor(tuning, slot).ToString().ToLowerInvariant());
            shape.style.unityBackgroundImageTintColor = Luminance(color) > LightChip ? Ink : Color.white;
            chip.Add(shape);
            return chip;
        }

        /// <summary>An empty seat's chip.</summary>
        public static VisualElement EmptyChip()
        {
            var chip = new VisualElement();
            chip.AddToClassList("hp-chip");
            chip.AddToClassList("hp-chip--empty");
            return chip;
        }

        /// <summary>A small badge: an icon and a word (HOST, YOU).</summary>
        public static VisualElement Badge(string text, string icon, string extraClass = null)
        {
            var badge = new VisualElement();
            badge.AddToClassList("hp-badge");
            if (extraClass != null) badge.AddToClassList(extraClass);
            var glyph = new VisualElement();
            glyph.AddToClassList("hp-icon");
            glyph.AddToClassList("hp-icon--" + icon);
            badge.Add(glyph);
            var label = new Label(text);
            label.AddToClassList("hp-badge__text");
            badge.Add(label);
            return badge;
        }

        /// <summary>Rocks the potato spinner (hp-potato) while it is in a panel: the "working" animation.</summary>
        public static void Wobble(VisualElement potato)
        {
            potato.schedule.Execute(() =>
            {
                float t = Time.unscaledTime * WobbleSpeed;
                potato.style.rotate = new Rotate(Mathf.Sin(t) * WobbleDegrees);
                potato.style.translate = new Translate(0, -Mathf.Abs(Mathf.Cos(t)) * WobbleLift);
            }).Every(WobbleTickMs);
        }

        /// <summary>Relative luminance (sRGB), to pick a readable glyph colour on a slot colour.</summary>
        public static float Luminance(Color c)
        {
            var linear = c.linear;
            return 0.2126f * linear.r + 0.7152f * linear.g + 0.0722f * linear.b;
        }
    }
}
