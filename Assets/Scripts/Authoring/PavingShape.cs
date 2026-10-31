using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>What a paving is laid in.</summary>
    public enum PavingKind { Flagstones, Cobbles, Slabs }

    /// <summary>
    /// A paved floor of an authored level (M21, ARCHITECTURE §27.7) over its outline: flagstones, cobbles or slabs, at the piece's height,
    /// or raised up to 3 m on a retaining edge of stone (a terrace). Walked as a flat surface. Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class PavingShape : LevelPiece
    {
        [SerializeField, Tooltip("Its outline (piece space, on the flat).")]
        List<Vector3> outline = new List<Vector3> { new Vector3(-4f, 0f, -4f), new Vector3(4f, 0f, -4f), new Vector3(4f, 0f, 4f), new Vector3(-4f, 0f, 4f) };
        [SerializeField] PavingKind kind = PavingKind.Flagstones;
        [SerializeField, Range(0f, 3f), Tooltip("Raised this high on a retaining edge (0: on the ground).")] float raised;
        [SerializeField, Tooltip("A kerb of edge stones round it.")] bool edgeStones = true;
        [SerializeField] StoneWear wear = StoneWear.Aged;

        public List<Vector3> Outline => outline;
        public PavingKind Kind { get => kind; set => kind = value; }
        public float Raised { get => raised; set => raised = Mathf.Clamp(value, 0f, 3f); }
        public bool EdgeStones { get => edgeStones; set => edgeStones = value; }
        public StoneWear Wear { get => wear; set => wear = value; }

        public List<Vector2> WorldOutline()
        {
            var list = new List<Vector2>(outline.Count);
            foreach (var p in outline) { var w = transform.TransformPoint(p); list.Add(new Vector2(w.x, w.z)); }
            return list;
        }
    }
}
