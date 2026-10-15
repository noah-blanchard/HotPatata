using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A free railing of an authored level (ARCHITECTURE §27): posts, a hand rail and a mid rail along its points (on the walking
    /// surface), colliding up to the hand rail. Data only: the editor draws it with the timber kit.
    /// </summary>
    [SelectionBase]
    public sealed class RailingShape : LevelPiece
    {
        [SerializeField, Tooltip("The walking surface under the rail, in the piece's space.")]
        List<Vector3> points = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 4f) };
        [SerializeField] bool closed;

        public List<Vector3> Points => points;
        public bool Closed { get => closed; set => closed = value; }

        public List<Vector3> WorldPoints()
        {
            var list = new List<Vector3>(points.Count + 1);
            foreach (var p in points) list.Add(transform.TransformPoint(p));
            if (closed && list.Count > 2) list.Add(list[0]);
            return list;
        }
    }
}
