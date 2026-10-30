using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>A river (water over a kill zone) or a ravine (dry).</summary>
    public enum RiverKind { River, Ravine }

    /// <summary>A river bed's cross section.</summary>
    public enum RiverProfile { U, V }

    /// <summary>
    /// A river bed or a ravine of an authored level (ARCHITECTURE §27.4, docs/LEVEL_AUTHORING.md §27): a bed carved along a line, falling
    /// <see cref="Slope"/> metres every 100 m and never rising, its banks fading into the ground. A river also carries a water ribbon
    /// <see cref="WaterDrop"/> under its banks, with kill zones under it all along (falling in fails the section, AGENTS: water over a kill
    /// zone); the water's flow is drawn only, it moves nothing (spec §17.2: no currents). Data only.
    /// </summary>
    [SelectionBase]
    public sealed class RiverShape : LevelPiece
    {
        [SerializeField] RiverKind kind = RiverKind.River;
        [SerializeField, Tooltip("The bed's line, upstream first, in the piece's space (y ignored).")]
        List<Vector3> points = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(6f, 0f, 20f), new Vector3(0f, 0f, 40f), new Vector3(8f, 0f, 60f) };
        [SerializeField, Min(1f), Tooltip("The bed's width at its top (m).")] float width = 8f;
        [SerializeField, Min(0.3f), Tooltip("How deep the bed is under the banks (m).")] float depth = 3f;
        [SerializeField, Min(0f), Tooltip("The banks each side, from the bed's edge to the ground (m).")] float bank = 4f;
        [SerializeField] RiverProfile profile = RiverProfile.U;
        [SerializeField, Range(0f, 3f), Tooltip("How much the banks wander (m).")] float bankNoise = 1f;
        [SerializeField, Min(0.1f), Tooltip("How far under the banks the water's surface lies (m).")] float waterDrop = 1.2f;
        [SerializeField, Range(0f, 20f), Tooltip("How far the bed falls every 100 m (m).")] float slope = 2f;

        public RiverKind Kind { get => kind; set => kind = value; }
        public List<Vector3> Points => points;
        public float Width { get => width; set => width = Mathf.Max(1f, value); }
        public float Depth { get => depth; set => depth = Mathf.Max(0.3f, value); }
        public float Bank { get => bank; set => bank = Mathf.Max(0f, value); }
        public RiverProfile Profile { get => profile; set => profile = value; }
        public float BankNoise { get => bankNoise; set => bankNoise = Mathf.Clamp(value, 0f, 3f); }
        public float WaterDrop { get => waterDrop; set => waterDrop = Mathf.Max(0.1f, value); }
        public float Slope { get => slope; set => slope = Mathf.Clamp(value, 0f, 20f); }

        /// <summary>A river holds water (and its kill zones); a ravine is dry.</summary>
        public bool Water => kind == RiverKind.River;

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
