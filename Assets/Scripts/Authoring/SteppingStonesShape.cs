using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Stepping stones of an authored level (M21, ARCHITECTURE §27.7): flat stones along a line, every <see cref="Spacing"/> metres (at
    /// most 2.5: a jump, never a lock), standing above the ground or the water under them. Each stone's top is solid; over water a miss
    /// is a fall into its kill zone. Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class SteppingStonesShape : LevelPiece
    {
        [SerializeField, Tooltip("The line the stones follow (piece space).")]
        List<Vector3> points = new List<Vector3> { Vector3.zero, new Vector3(0f, 0f, 8f) };
        [SerializeField, Range(0.8f, 2.5f), Tooltip("From one stone's middle to the next (m).")] float spacing = 1.5f;
        [SerializeField, Range(0.5f, 2f), Tooltip("A stone's width (m).")] float size = 0.9f;
        [SerializeField, Range(0f, 1.2f), Tooltip("Their tops above the line (m).")] float height = 0.25f;
        [SerializeField, Range(0f, 0.6f), Tooltip("How far each strays from the line (m).")] float jitter = 0.25f;

        public List<Vector3> Points => points;
        public float Spacing { get => spacing; set => spacing = Mathf.Clamp(value, 0.8f, 2.5f); }
        public float Size { get => size; set => size = Mathf.Clamp(value, 0.5f, 2f); }
        public float Height { get => height; set => height = Mathf.Clamp(value, 0f, 1.2f); }
        public float Jitter { get => jitter; set => jitter = Mathf.Clamp(value, 0f, 0.6f); }

        public List<Vector3> WorldPoints()
        {
            var list = new List<Vector3>(points.Count);
            foreach (var p in points) list.Add(transform.TransformPoint(p));
            return list;
        }
    }
}
