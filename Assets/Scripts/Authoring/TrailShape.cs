using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>The layer a trail paints along its way.</summary>
    public enum TrailPaint { Trail, Gravel, Mud, None }

    /// <summary>
    /// A trail of an authored level (ARCHITECTURE §27.4, docs/LEVEL_AUTHORING.md §27): a way on the ground along a line, its ground pulled to
    /// a profile no steeper than <see cref="MaxGrade"/> along it and flat across its width, banked into the ground each side, painted with
    /// its layer, and walked (its line joins the level's walk lines: the route runner walks it). Where the ground is steeper than its grade
    /// it cuts and fills; a cut too deep says so (put a stone stair there). Data only: the editor's height stack applies it.
    /// </summary>
    [SelectionBase]
    public sealed class TrailShape : LevelPiece
    {
        /// <summary>A cut or a fill deeper than this (m) is reported: the slope wants steps.</summary>
        public const float DeepCut = 2.5f;

        [SerializeField, Tooltip("The trail's line, in the piece's space (y ignored: it follows the ground).")]
        List<Vector3> points = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 12f), new Vector3(-1f, 0f, 24f), new Vector3(1f, 0f, 36f) };
        [SerializeField, Min(0.8f), Tooltip("The walking width (m).")] float width = 2.4f;
        [SerializeField, Min(0f), Tooltip("The bank each side, from the trail down or up to the ground (m).")] float bank = 3f;
        [SerializeField, Range(5f, 100f), Tooltip("The steepest grade along it (%). 30 % is about 17°.")] float maxGrade = 30f;
        [SerializeField, Range(0f, 1f), Tooltip("How strongly the ground is pulled to the trail's profile.")] float flatten = 1f;
        [SerializeField] TrailPaint paint = TrailPaint.Trail;
        [SerializeField, Range(0f, 1.5f), Tooltip("How much its edges wander (m).")] float edgeNoise = 0.4f;
        [SerializeField, Tooltip("The team walks it: its line joins the level's walk lines.")] bool walked = true;

        public List<Vector3> Points => points;
        public float Width { get => width; set => width = Mathf.Max(0.8f, value); }
        public float Bank { get => bank; set => bank = Mathf.Max(0f, value); }
        public float MaxGrade { get => maxGrade; set => maxGrade = Mathf.Clamp(value, 5f, 100f); }
        public float Flatten { get => flatten; set => flatten = Mathf.Clamp01(value); }
        public TrailPaint Paint { get => paint; set => paint = value; }
        public float EdgeNoise { get => edgeNoise; set => edgeNoise = Mathf.Clamp(value, 0f, 1.5f); }
        public bool Walked { get => walked; set => walked = value; }

        public List<Vector2> WorldPoints()
        {
            var list = new List<Vector2>(points.Count);
            foreach (var p in points)
            {
                var w = transform.TransformPoint(new Vector3(p.x, 0f, p.z));
                list.Add(new Vector2(w.x, w.z));
            }
            return list;
        }
    }
}
