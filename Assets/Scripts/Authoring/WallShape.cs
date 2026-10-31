using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>What a wall is made of (M21, ARCHITECTURE §27.7): it follows from its <see cref="WallStyle"/>.</summary>
    public enum WallFamily { Stone, Timber, Fence, Mixed }

    /// <summary>How a wall is built (M21). Appended only: the serialised values hold.</summary>
    public enum WallStyle
    {
        DryStone, Mortared, Ruined, Retaining,
        Palisade, Plank, StackedLogs, Wattle,
        RailFence, Pickets, RopeOnPosts, Hedge,
        StoneBaseTimberTop, HalfTimbered, StoneTimberCap,
    }

    /// <summary>What tops a wall.</summary>
    public enum WallCap { None, Coping, Timber, Points }

    /// <summary>How weathered a wall's stone is.</summary>
    public enum StoneWear { New, Aged, Mossy }

    /// <summary>The outline of an opening in a wall.</summary>
    public enum OpeningShape { Rect, RoundArch, PointedArch, Lintel, Breach }

    /// <summary>What frames an opening: nothing, stone voussoirs (or a lintel stone), or timber jambs and a lintel.</summary>
    public enum OpeningFrame { None, Stone, Timber }

    /// <summary>
    /// Who goes through an opening (M21, spec §13.20): everyone (Free), the bomb only (a body screen in it), runners only (a curtain in
    /// it), or a kit door (an actuator, wired to its source). Every passage but Free places the kit in the level's Kit group.
    /// </summary>
    public enum OpeningPassage { Free, BombOnly, RunnersOnly, KitDoor }

    /// <summary>A gap broken down from a ruined wall's top: where along it (m), how wide and how deep from its top (m).</summary>
    [Serializable]
    public struct WallBreach
    {
        [Min(0f)] public float along;
        [Min(0.5f)] public float width;
        [Min(0.2f)] public float depth;
    }

    /// <summary>
    /// An opening in a wall (M21, ARCHITECTURE §27.7): where along the wall's line (m, its middle), its size and sill, its shape and
    /// frame, and its passage. A passage other than Free places its kit (the look chosen) in the level's Kit group and keeps the link
    /// (<see cref="kit"/>): a rebuild of the wall moves and resizes it, never makes it again, so its wiring holds.
    /// </summary>
    [Serializable]
    public struct WallOpening
    {
        [Min(0f), Tooltip("Its middle, metres along the wall's line.")] public float along;
        [Min(0.4f)] public float width;
        [Min(0.4f)] public float height;
        [Min(0f), Tooltip("Its bottom above the wall's foot (a window).")] public float sill;
        public OpeningShape shape;
        public OpeningFrame frame;
        public OpeningPassage passage;
        [Tooltip("The kit's look: a path of a kit prefab (empty: the passage's own).")] public string look;
        [Tooltip("The kit placed in it (in the level's Kit group).")] public GameObject kit;

        public WallOpening(float along, float width, float height, OpeningPassage passage = OpeningPassage.Free, OpeningShape shape = OpeningShape.Rect, float sill = 0f)
        {
            this.along = along;
            this.width = width;
            this.height = height;
            this.sill = sill;
            this.shape = shape;
            frame = OpeningFrame.None;
            this.passage = passage;
            look = "";
            kit = null;
        }

        /// <summary>A player fits through it (spec: a window a player fits through is not bomb-only).</summary>
        public bool PlayerFits => width >= 0.8f && height >= 1.2f;
    }

    /// <summary>
    /// A wall of an authored level (M21, ARCHITECTURE §27.7): stone (dry-stone, mortared, ruined, retaining), timber (palisade, plank,
    /// stacked logs, wattle), a fence (rails, pickets, rope on posts, a hedge) or mixed, along a line through its points (closed if
    /// asked), with openings that place the kit. It collides as a box per straight run, from its foot to its locked height (its lowest
    /// top): under 1.4 m a player mantles it, under 3.4 m climbs it, from 3.4 m it is a lock. Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class WallShape : LevelPiece
    {
        [SerializeField, Tooltip("The wall's line at its foot (piece space).")]
        List<Vector3> points = new List<Vector3> { new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f) };
        [SerializeField, Tooltip("Joins the last point to the first (an enclosure).")] bool closed;
        [SerializeField] WallStyle style = WallStyle.DryStone;
        [SerializeField, Min(0.3f), Tooltip("Its top above its foot (m).")] float height = 2f;
        [SerializeField, Min(0.1f), Tooltip("Its thickness (m).")] float thickness = 0.7f;
        [SerializeField, Range(0f, 3f), Tooltip("Ruined: how far its top falls below Height at its lowest (its locked height is its lowest top).")] float crest;
        [SerializeField, Tooltip("Ruined: gaps broken down from its top.")] List<WallBreach> breaches = new List<WallBreach>();
        [SerializeField] WallCap cap = WallCap.None;
        [SerializeField, Min(0.8f), Tooltip("Fences and palisades: from one post to the next (m).")] float postSpacing = 2.4f;
        [SerializeField] StoneWear wear = StoneWear.Aged;
        [SerializeField, Tooltip("Retaining: the ground is held on its right (along the line) rather than its left.")] bool retainRight;
        [SerializeField, Min(0f), Tooltip("Retaining: how far the terrace behind it reaches (m).")] float terrace = 8f;
        [SerializeField] List<WallOpening> openings = new List<WallOpening>();

        public List<Vector3> Points => points;
        public bool Closed { get => closed; set => closed = value; }
        public WallStyle Style { get => style; set => style = value; }
        public float Height { get => height; set => height = Mathf.Max(0.3f, value); }
        public float Thickness { get => thickness; set => thickness = Mathf.Max(0.1f, value); }
        public float Crest { get => crest; set => crest = Mathf.Clamp(value, 0f, 3f); }
        public List<WallBreach> Breaches => breaches;
        public WallCap Cap { get => cap; set => cap = value; }
        public float PostSpacing { get => postSpacing; set => postSpacing = Mathf.Max(0.8f, value); }
        public StoneWear Wear { get => wear; set => wear = value; }
        public bool RetainRight { get => retainRight; set => retainRight = value; }
        public float Terrace { get => terrace; set => terrace = Mathf.Max(0f, value); }
        public List<WallOpening> Openings => openings;

        public WallFamily Family => FamilyOf(style);

        public static WallFamily FamilyOf(WallStyle s) => s switch
        {
            WallStyle.Palisade or WallStyle.Plank or WallStyle.StackedLogs or WallStyle.Wattle => WallFamily.Timber,
            WallStyle.RailFence or WallStyle.Pickets or WallStyle.RopeOnPosts or WallStyle.Hedge => WallFamily.Fence,
            WallStyle.StoneBaseTimberTop or WallStyle.HalfTimbered or WallStyle.StoneTimberCap => WallFamily.Mixed,
            _ => WallFamily.Stone,
        };

        /// <summary>The line in the world (closed: the first point again at its end).</summary>
        public List<Vector3> WorldLine()
        {
            var list = new List<Vector3>(points.Count + 1);
            foreach (var p in points) list.Add(transform.TransformPoint(p));
            if (closed && list.Count > 2) list.Add(list[0]);
            return list;
        }

        /// <summary>The wall's length along its line (m).</summary>
        public float Length
        {
            get
            {
                var line = WorldLine();
                float l = 0f;
                for (int i = 1; i < line.Count; i++) l += Vector3.Distance(line[i - 1], line[i]);
                return l;
            }
        }

        /// <summary>A fence's height when none is set: a rail at hip height.</summary>
        public const float FenceHeight = 1.1f;
        /// <summary>Below this a player mantles a wall; below <see cref="LockHeight"/> climbs it (a jump and a mantle); from it, a lock.</summary>
        public const float MantleHeight = 1.4f, LockHeight = 3.4f;
    }
}
