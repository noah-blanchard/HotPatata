using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>How a stone stair is made.</summary>
    public enum StoneStairKind { CutInRock, Flagstone, Built }

    /// <summary>
    /// A stone stair of an authored level (M21, ARCHITECTURE §27.7): steps along a line from its first point (the foot) to its last (the
    /// top), cut in the rock, of flagstones, or built between side walls. It is walked as a smooth ramp (a walking proxy, as a walkway's
    /// flights): the steps are drawn only. Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class StoneStairShape : LevelPiece
    {
        [SerializeField, Tooltip("The walk's centre line at the steps' noses (piece space): foot to top.")]
        List<Vector3> points = new List<Vector3> { Vector3.zero, new Vector3(0f, 2.4f, 4.4f) };
        [SerializeField, Min(0.8f)] float width = 2f;
        [SerializeField, Range(0.15f, 0.22f), Tooltip("A step's rise (m).")] float rise = 0.18f;
        [SerializeField, Range(0.28f, 0.4f), Tooltip("A step's going (m).")] float going = 0.33f;
        [SerializeField] StoneStairKind kind = StoneStairKind.Flagstone;
        [SerializeField, Min(0f), Tooltip("Built: its side walls' height above the steps (0: none).")] float sideWalls = 0.9f;
        [SerializeField] StoneWear wear = StoneWear.Aged;

        public List<Vector3> Points => points;
        public float Width { get => width; set => width = Mathf.Max(0.8f, value); }
        public float Rise { get => rise; set => rise = Mathf.Clamp(value, 0.15f, 0.22f); }
        public float Going { get => going; set => going = Mathf.Clamp(value, 0.28f, 0.4f); }
        public StoneStairKind Kind { get => kind; set => kind = value; }
        public float SideWalls { get => sideWalls; set => sideWalls = Mathf.Max(0f, value); }
        public StoneWear Wear { get => wear; set => wear = value; }

        public List<Vector3> WorldPoints()
        {
            var list = new List<Vector3>(points.Count);
            foreach (var p in points) list.Add(transform.TransformPoint(p));
            return list;
        }
    }
}
