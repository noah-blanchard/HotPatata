using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A frozen wall's collision (ARCHITECTURE §27.7): an invisible wall along a line, <see cref="Thickness"/> thick, from
    /// <see cref="Bottom"/> to <see cref="Top"/> above its points (local, its foot). Deleting stones does not open it: drag its points,
    /// split it, or use Open here (the editor cuts it round the stones taken out). The editor lays its boxes from the data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MasonryWall : MonoBehaviour
    {
        [SerializeField] List<Vector3> points = new List<Vector3> { Vector3.zero, Vector3.forward * 2f };
        [SerializeField, Tooltip("Its bottom above its line (m, under it when negative).")] float bottom = -0.3f;
        [SerializeField, Tooltip("Its top above its line (m).")] float top = 2f;
        [SerializeField, Min(0.05f)] float thickness = 0.7f;
        [SerializeField, HideInInspector] string builtFrom;

        public List<Vector3> Points => points;
        public float Bottom { get => bottom; set => bottom = value; }
        public float Top { get => top; set => top = value; }
        public float Thickness { get => thickness; set => thickness = Mathf.Max(0.05f, value); }
        public string BuiltFrom { get => builtFrom; set => builtFrom = value; }

        public string ShapeKey()
        {
            var s = new StringBuilder("masonry|").Append(Mathf.RoundToInt(bottom * 1000f)).Append('|').Append(Mathf.RoundToInt(top * 1000f)).Append('|')
                .Append(Mathf.RoundToInt(thickness * 1000f)).Append('|');
            foreach (var p in points)
                s.Append(Mathf.RoundToInt(p.x * 1000f).ToString(CultureInfo.InvariantCulture)).Append(',')
                 .Append(Mathf.RoundToInt(p.y * 1000f).ToString(CultureInfo.InvariantCulture)).Append(',')
                 .Append(Mathf.RoundToInt(p.z * 1000f).ToString(CultureInfo.InvariantCulture)).Append(';');
            return s.ToString();
        }
    }
}
