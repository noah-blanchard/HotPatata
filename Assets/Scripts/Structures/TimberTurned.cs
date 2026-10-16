using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A turned piece (ARCHITECTURE §27.1): a solid of revolution round the element's local up, its profile (radius, height) from the
    /// bottom up. A pulley wheel, a winch drum, a peg.
    /// </summary>
    public sealed class TimberTurned : TimberElement
    {
        [SerializeField] List<Vector2> profile = new List<Vector2> { new Vector2(0.3f, -0.1f), new Vector2(0.3f, 0.1f) };
        [SerializeField, Range(6, 32)] int sides = 14;

        public List<Vector2> Profile => profile;
        public int Sides { get => sides; set => sides = Mathf.Clamp(value, 6, 32); }

        public override string ShapeKey()
        {
            var s = new StringBuilder("turned|").Append(sides).Append('|').Append(Grain).Append('|');
            foreach (var p in profile)
                s.Append(Mathf.RoundToInt(p.x * 1000f).ToString(CultureInfo.InvariantCulture)).Append(',')
                 .Append(Mathf.RoundToInt(p.y * 1000f).ToString(CultureInfo.InvariantCulture)).Append(';');
            return s.ToString();
        }
    }
}
