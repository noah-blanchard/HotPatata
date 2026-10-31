using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A stone plinth of an authored level (M21, ARCHITECTURE §27.7): a block of dressed stone, its top walked, with up to three steps
    /// round it. Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class PlinthShape : LevelPiece
    {
        [SerializeField, Min(0.4f)] float width = 3f;
        [SerializeField, Min(0.4f)] float depth = 3f;
        [SerializeField, Min(0.2f)] float height = 0.9f;
        [SerializeField, Range(0, 3), Tooltip("Steps round it, each 0.4 m out.")] int steps = 2;
        [SerializeField] StoneWear wear = StoneWear.New;

        public float Width { get => width; set => width = Mathf.Max(0.4f, value); }
        public float Depth { get => depth; set => depth = Mathf.Max(0.4f, value); }
        public float Height { get => height; set => height = Mathf.Max(0.2f, value); }
        public int Steps { get => steps; set => steps = Mathf.Clamp(value, 0, 3); }
        public StoneWear Wear { get => wear; set => wear = value; }
    }
}
