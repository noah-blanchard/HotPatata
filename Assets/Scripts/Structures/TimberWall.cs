using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// An invisible wall along a line (ARCHITECTURE §27.1): what keeps a body and the bomb from passing a railing or the side of a rope
    /// bridge. Its points (local) are the walking surface at its foot; it stands <see cref="Height"/> above them. The editor lays its
    /// boxes (children on Environment) from the data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimberWall : MonoBehaviour
    {
        [SerializeField] List<Vector3> points = new List<Vector3> { Vector3.zero, Vector3.forward * 2f };
        [SerializeField, Tooltip("How high the wall stands above its line (m).")] float height = 1.07f;
        [SerializeField, HideInInspector] string builtFrom;

        public List<Vector3> Points => points;
        public float Height { get => height; set => height = Mathf.Max(0.1f, value); }
        public string BuiltFrom { get => builtFrom; set => builtFrom = value; }

        public string ShapeKey()
        {
            var s = new StringBuilder("wall|").Append(Mathf.RoundToInt(height * 1000f)).Append('|');
            foreach (var p in points)
                s.Append(Mathf.RoundToInt(p.x * 1000f).ToString(CultureInfo.InvariantCulture)).Append(',')
                 .Append(Mathf.RoundToInt(p.y * 1000f).ToString(CultureInfo.InvariantCulture)).Append(',')
                 .Append(Mathf.RoundToInt(p.z * 1000f).ToString(CultureInfo.InvariantCulture)).Append(';');
            return s.ToString();
        }
    }
}
