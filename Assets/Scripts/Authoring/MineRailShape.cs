using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// An old mine track of an authored level (ARCHITECTURE §27.2): sleepers and two iron rails along a line through its points, lying on
    /// the ground under them. Decoration: walked over, never in the way (no collider). A mine cart prop sits on it. Freezes into elements.
    /// Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class MineRailShape : LevelPiece
    {
        [SerializeField, Tooltip("The track's centre line (piece space).")]
        List<Vector3> points = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 12f) };
        [SerializeField, Range(0.5f, 1.4f), Tooltip("Between the rails (m).")] float gauge = 0.75f;
        [SerializeField, Range(0f, 1f), Tooltip("Sleepers missing or broken (an abandoned track).")] float decay = 0.2f;

        public List<Vector3> Points => points;
        public float Gauge { get => gauge; set => gauge = Mathf.Clamp(value, 0.5f, 1.4f); }
        public float Decay { get => decay; set => decay = Mathf.Clamp01(value); }

        public List<Vector3> WorldPoints()
        {
            var list = new List<Vector3>(points.Count);
            foreach (var p in points) list.Add(transform.TransformPoint(p));
            return list;
        }
    }
}
