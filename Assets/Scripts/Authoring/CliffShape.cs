using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>The rock a cliff is made of: grey granite with lichen, layered sandstone, or mossy rock.</summary>
    public enum CliffRock { Granite, Sandstone, Mossy }

    /// <summary>A ledge round a cliff: at a height above its foot, the face steps back by an inset (a shelf one walks on).</summary>
    [Serializable]
    public struct CliffLedge
    {
        [Tooltip("The shelf's height above the cliff's foot (m).")] public float height;
        [Tooltip("How far the face steps back there (m).")] public float inset;
    }

    /// <summary>
    /// A cliff of an authored level (ARCHITECTURE §27.2): a block of rock over a closed outline (piece space, x and z), from the piece's
    /// height (its foot, sunk a little into the ground) up <see cref="Height"/>, its top walkable, its faces rough, ledges stepping it back
    /// on the way up, its top edge ragged (<see cref="Crest"/>). A rock spine between two lanes, a wall round a hollow, a face a team climbs by its ledges. A face of 3.4 m or more
    /// between two shelves is not climbed (spec §13.20): its contract still declares the Climb lock. Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class CliffShape : LevelPiece
    {
        /// <summary>A face at least this tall is out of a jump and a mantle's reach (PROJECT_SPEC §13.20; the courses used 5 m).</summary>
        public const float LockedFace = 3.4f;

        [SerializeField, Tooltip("The cliff's outline at its foot (piece space; closed).")]
        List<Vector3> outline = new List<Vector3> { new Vector3(-6f, 0f, -2f), new Vector3(6f, 0f, -2f), new Vector3(6f, 0f, 2f), new Vector3(-6f, 0f, 2f) };
        [SerializeField, Min(0.5f), Tooltip("Its top above the piece (m).")] float height = 6f;
        [SerializeField, Tooltip("Shelves on the way up, from the foot.")] List<CliffLedge> ledges = new List<CliffLedge>();
        [SerializeField] CliffRock rock = CliffRock.Granite;
        [SerializeField, Range(0f, 2f), Tooltip("How rough its faces are (m): they bulge out up to this much.")] float roughness = 0.9f;
        [SerializeField, Range(0f, 0.3f), Tooltip("How far its faces lean back for each metre up (a face is never quite plumb).")] float batter = 0.08f;
        [SerializeField, Min(0f), Tooltip("How far it reaches under the piece (m): into the ground, so no gap shows at its foot.")] float sink = 2f;
        [SerializeField, Range(0f, 12f), Tooltip("How much its top edge rises and falls above Height (m): a ragged skyline. Only ever higher, so a lock " +
                                                   "holds; keep 0 for a top that is walked on.")] float crest;

        public List<Vector3> Outline => outline;
        public float Height { get => height; set => height = Mathf.Max(0.5f, value); }
        public List<CliffLedge> Ledges => ledges;
        public CliffRock Rock { get => rock; set => rock = value; }
        public float Roughness { get => roughness; set => roughness = Mathf.Clamp(value, 0f, 2f); }
        public float Batter { get => batter; set => batter = Mathf.Clamp(value, 0f, 0.3f); }
        public float Sink { get => sink; set => sink = Mathf.Max(0f, value); }
        public float Crest { get => crest; set => crest = Mathf.Clamp(value, 0f, 12f); }

        /// <summary>The outline in the world (x, z).</summary>
        public List<Vector2> WorldOutline()
        {
            var list = new List<Vector2>(outline.Count);
            foreach (var p in outline)
            {
                var w = transform.TransformPoint(new Vector3(p.x, 0f, p.z));
                list.Add(new Vector2(w.x, w.z));
            }
            return list;
        }

        /// <summary>The faces from the foot up (each one's height): from the foot to the first shelf, shelf to shelf, the last to the top.</summary>
        public List<float> Faces()
        {
            var steps = new List<float>();
            foreach (var l in ledges) if (l.height > 0f && l.height < height) steps.Add(l.height);
            steps.Sort();
            var faces = new List<float>();
            float from = 0f;
            foreach (var s in steps) { faces.Add(s - from); from = s; }
            faces.Add(height - from);
            return faces;
        }
    }
}
