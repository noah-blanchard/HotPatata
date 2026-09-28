using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The shape paired with each player slot, so identity never rests on colour alone (spec §19). Shapes and
    /// colours per slot live in <see cref="GameTuning.playerColors"/> / <see cref="GameTuning.playerShapes"/>.
    /// </summary>
    public enum PlayerShape
    {
        Circle,
        Triangle,
        Square,
        Diamond
    }

    public static class PlayerIdentity
    {
        /// <summary>A text glyph for the shape (Arial and the default UI fonts have all four).</summary>
        public static string Glyph(PlayerShape shape)
        {
            switch (shape)
            {
                case PlayerShape.Triangle: return "▲";   // ▲
                case PlayerShape.Square: return "■";     // ■
                case PlayerShape.Diamond: return "◆";    // ◆
                default: return "●";                     // ●
            }
        }

        public static Color ColorFor(GameTuning tuning, int slot) =>
            tuning != null && tuning.playerColors != null && tuning.playerColors.Length > 0
                ? tuning.playerColors[Mathf.Abs(slot) % tuning.playerColors.Length]
                : Color.white;

        public static PlayerShape ShapeFor(GameTuning tuning, int slot) =>
            tuning != null && tuning.playerShapes != null && tuning.playerShapes.Length > 0
                ? tuning.playerShapes[Mathf.Abs(slot) % tuning.playerShapes.Length]
                : (PlayerShape)(Mathf.Abs(slot) % 4);

        /// <summary>IMGUI rich text: the slot's shape in the slot's colour, then the name (the style needs richText).</summary>
        public static string RichLabel(GameTuning tuning, int slot, string name) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(ColorFor(tuning, slot))}>{Glyph(ShapeFor(tuning, slot))}</color> {name}";
    }
}
