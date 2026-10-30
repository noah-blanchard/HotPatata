using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>What a land shape does to the ground (ARCHITECTURE §27.4).</summary>
    public enum LandKind { Hill, Hollow, Ridge, Plateau }

    /// <summary>How a land shape rises from its edge.</summary>
    public enum LandProfile { Smooth, Steep, Terraced }

    /// <summary>
    /// A land shape of an authored level (ARCHITECTURE §27.4, docs/LEVEL_AUTHORING.md §27): a hill, a hollow, a ridge along a line or a
    /// plateau with banked sides, added to the terrain without painting it in: the editor's height stack recomposes the ground from the
    /// author's sculpt and the shapes, so a shape can be moved, edited or deleted and the sculpt round it stays. Data only.
    /// </summary>
    [SelectionBase]
    public sealed class LandShape : LevelPiece
    {
        [SerializeField] LandKind kind = LandKind.Hill;
        [SerializeField, Tooltip("The outline (closed; a ridge: its crest line), in the piece's space (y ignored).")]
        List<Vector3> points = new List<Vector3> { new Vector3(-12f, 0f, -10f), new Vector3(12f, 0f, -10f), new Vector3(14f, 0f, 8f), new Vector3(-10f, 0f, 12f) };
        [SerializeField, Tooltip("How high it rises (m; a hollow: how deep). A plateau: its top above the piece.")] float height = 8f;
        [SerializeField, Min(0.5f), Tooltip("How far beyond the outline it fades into the ground (m; a ridge: its half width).")] float falloff = 12f;
        [SerializeField] LandProfile profile = LandProfile.Smooth;
        [SerializeField, Range(0f, 6f), Tooltip("How much its outline wanders (m).")] float outlineNoise = 2f;
        [SerializeField, Range(0f, 3f), Tooltip("Bumps on its surface (m).")] float surfaceNoise = 0.6f;
        [SerializeField, Min(2f), Tooltip("The size of the noise's features (m).")] float noiseScale = 14f;

        public LandKind Kind { get => kind; set => kind = value; }
        public List<Vector3> Points => points;
        public float Height { get => height; set => height = value; }
        public float Falloff { get => falloff; set => falloff = Mathf.Max(0.5f, value); }
        public LandProfile Profile { get => profile; set => profile = value; }
        public float OutlineNoise { get => outlineNoise; set => outlineNoise = Mathf.Clamp(value, 0f, 6f); }
        public float SurfaceNoise { get => surfaceNoise; set => surfaceNoise = Mathf.Clamp(value, 0f, 3f); }
        public float NoiseScale { get => noiseScale; set => noiseScale = Mathf.Max(2f, value); }

        /// <summary>A ridge is a line; the others an outline.</summary>
        public bool Closed => kind != LandKind.Ridge;

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
