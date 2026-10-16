using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HotPatata
{
    /// <summary>The paints of the nature look a sign's glyph is drawn in.</summary>
    public enum TimberPaintColor { Amber, Blue, Green }

    /// <summary>
    /// A flat painted shape on a board (ARCHITECTURE §27.1): a glyph's part. Its outline is in the element's local x (right) and z (up);
    /// it stands a few millimetres proud along local y, out of the board. Drawn only, never solid.
    /// </summary>
    public sealed class TimberPaint : TimberElement
    {
        public const float Thickness = 0.025f;

        [SerializeField] List<Vector2> outline = new List<Vector2> { new Vector2(-0.2f, -0.2f), new Vector2(0.2f, -0.2f), new Vector2(0.2f, 0.2f), new Vector2(-0.2f, 0.2f) };
        [SerializeField] TimberPaintColor paint;

        public List<Vector2> Outline => outline;
        public TimberPaintColor Paint { get => paint; set => paint = value; }

        public override string ShapeKey()
        {
            var s = new StringBuilder("paint|");
            foreach (var p in outline)
                s.Append(Mathf.RoundToInt(p.x * 1000f).ToString(CultureInfo.InvariantCulture)).Append(',')
                 .Append(Mathf.RoundToInt(p.y * 1000f).ToString(CultureInfo.InvariantCulture)).Append(';');
            return s.ToString();
        }
    }
}
