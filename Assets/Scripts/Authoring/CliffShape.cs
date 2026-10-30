using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>The rock a cliff is made of: grey granite with lichen, layered sandstone, or mossy rock.</summary>
    public enum CliffRock { Granite, Sandstone, Mossy, Limestone, Basalt, Earth }

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

        [Header("Detail (M21: every value at 0 draws the cliff as before)")]
        [SerializeField, Range(1, 6), Tooltip("Noise octaves on its faces: more is finer rock.")] int octaves = 1;
        [SerializeField, Range(1.5f, 3f), Tooltip("How much finer each octave is.")] float lacunarity = 2f;
        [SerializeField, Range(0f, 1f), Tooltip("Horizontal bands (sandstone, limestone).")] float strata;
        [SerializeField, Range(0f, 1f), Tooltip("Faceting: planes break the bulge like joints in real rock.")] float fracture;
        [SerializeField, Range(0f, 1.5f), Tooltip("An outward lip in its top third (m). Never over a walk line (Verify warns).")] float overhang;
        [Header("Into the ground")]
        [SerializeField, Range(0f, 1.5f), Tooltip("A band at its foot drawn in the ground's scree (m).")] float footBlend;
        [SerializeField, Range(0f, 1f), Tooltip("Scanned rocks set into its faces (decoration).")] float rockScatter;
        [SerializeField, Range(0f, 1f), Tooltip("Small rocks and gravel scattered at its foot (decoration).")] float scree;
        [SerializeField, Range(1f, 12f), Tooltip("How far from its foot the scree reaches (m).")] float screeWidth = 4f;
        [SerializeField, Range(0f, 4f), Tooltip("The ground rises to meet its foot over this many metres (a talus): no cut shows.")] float fuse;
        [SerializeField, Range(0f, 1f), Tooltip("Moss, mud and leaf patches round its foot (decoration).")] float decals;

        public List<Vector3> Outline => outline;
        public int Octaves { get => octaves; set => octaves = Mathf.Clamp(value, 1, 6); }
        public float Lacunarity { get => lacunarity; set => lacunarity = Mathf.Clamp(value, 1.5f, 3f); }
        public float Strata { get => strata; set => strata = Mathf.Clamp01(value); }
        public float Fracture { get => fracture; set => fracture = Mathf.Clamp01(value); }
        public float Overhang { get => overhang; set => overhang = Mathf.Clamp(value, 0f, 1.5f); }
        public float FootBlend { get => footBlend; set => footBlend = Mathf.Clamp(value, 0f, 1.5f); }
        public float RockScatter { get => rockScatter; set => rockScatter = Mathf.Clamp01(value); }
        public float Scree { get => scree; set => scree = Mathf.Clamp01(value); }
        public float ScreeWidth { get => screeWidth; set => screeWidth = Mathf.Clamp(value, 1f, 12f); }
        public float Fuse { get => fuse; set => fuse = Mathf.Clamp(value, 0f, 4f); }
        public float Decals { get => decals; set => decals = Mathf.Clamp01(value); }

        /// <summary>True when its faces are drawn finer than its collider (octaves, strata, fracture, an overhang, basalt's columns).</summary>
        public bool Detailed => octaves > 1 || strata > 0f || fracture > 0f || overhang > 0f || rock == CliffRock.Basalt;
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
