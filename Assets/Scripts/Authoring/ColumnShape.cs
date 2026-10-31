using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>A column's section.</summary>
    public enum ColumnKind { Round, Square, Drum }

    /// <summary>
    /// A stone column of an authored level (M21, ARCHITECTURE §27.7): a shaft on a base under a capital, or broken (its top fallen in
    /// drums at its foot), leaning if asked. What stands collides. Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class ColumnShape : LevelPiece
    {
        [SerializeField] ColumnKind kind = ColumnKind.Drum;
        [SerializeField, Min(0.5f)] float height = 4.5f;
        [SerializeField, Min(0.15f)] float radius = 0.4f;
        [SerializeField] bool capital = true;
        [SerializeField] bool basePlinth = true;
        [SerializeField, Range(0f, 1f), Tooltip("How much of its top is gone (fallen at its foot).")] float broken;
        [SerializeField, Range(0f, 12f), Tooltip("Its lean (degrees).")] float lean;
        [SerializeField] StoneWear wear = StoneWear.Aged;

        public ColumnKind Kind { get => kind; set => kind = value; }
        public float Height { get => height; set => height = Mathf.Max(0.5f, value); }
        public float Radius { get => radius; set => radius = Mathf.Max(0.15f, value); }
        public bool Capital { get => capital; set => capital = value; }
        public bool Base { get => basePlinth; set => basePlinth = value; }
        public float Broken { get => broken; set => broken = Mathf.Clamp01(value); }
        public float Lean { get => lean; set => lean = Mathf.Clamp(value, 0f, 12f); }
        public StoneWear Wear { get => wear; set => wear = value; }
    }
}
