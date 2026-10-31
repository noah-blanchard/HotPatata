using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>A tower's plan.</summary>
    public enum TowerPlan { Round, Square }

    /// <summary>What tops a tower.</summary>
    public enum TowerTop { Parapet, Crenellations, ConeRoof, PyramidRoof, Ruined }

    /// <summary>
    /// A tower of an authored level (M21, ARCHITECTURE §27.7): walls round a round or square plan (stone or timber, drawn as a wall
    /// is), floors inside (walked, each with a hatch), doors (wall openings, the same passages as a wall's: the kit is placed), and its
    /// top: a parapet, crenellations, a roof (a ceiling) or a ruined crest. The climb inside is the author's (a stair, a walkway, a lift).
    /// Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class TowerShape : LevelPiece, IWallSpec
    {
        [SerializeField] TowerPlan plan = TowerPlan.Round;
        [SerializeField, Min(1.5f), Tooltip("Its outer radius (round) or half its side (square), m.")] float radius = 4f;
        [SerializeField, Min(2f)] float height = 10f;
        [SerializeField] WallStyle wallStyle = WallStyle.Mortared;
        [SerializeField, Min(0.2f)] float thickness = 0.8f;
        [SerializeField, Tooltip("Its floors' heights above its foot (m).")] List<float> floors = new List<float> { 4f, 8f };
        [SerializeField, Min(0.8f), Tooltip("A floor's hatch (m square).")] float hatch = 1.4f;
        [SerializeField] TowerTop top = TowerTop.Crenellations;
        [SerializeField, Range(0f, 4f), Tooltip("Ruined: how far its top falls.")] float crest = 2f;
        [SerializeField] StoneWear wear = StoneWear.Aged;
        [SerializeField, Tooltip("Its doors and windows (metres round its wall from its south point, as a wall's openings).")]
        List<WallOpening> openings = new List<WallOpening>();

        public TowerPlan Plan { get => plan; set => plan = value; }
        public float Radius { get => radius; set => radius = Mathf.Max(1.5f, value); }
        public float Height { get => height; set => height = Mathf.Max(2f, value); }
        public WallStyle WallStyle { get => wallStyle; set => wallStyle = value; }
        public float Thickness { get => thickness; set => thickness = Mathf.Max(0.2f, value); }
        public List<float> Floors => floors;
        public float Hatch { get => hatch; set => hatch = Mathf.Max(0.8f, value); }
        public TowerTop Top { get => top; set => top = value; }
        public float TopCrest { get => crest; set => crest = Mathf.Clamp(value, 0f, 4f); }
        public StoneWear Wear { get => wear; set => wear = value; }
        public List<WallOpening> Openings => openings;

        // drawn as a closed wall round its plan
        public Component Owner => this;
        public WallStyle Style => top == TowerTop.Ruined && WallShape.FamilyOf(wallStyle) == WallFamily.Stone ? WallStyle.Ruined : wallStyle;
        public WallFamily Family => WallShape.FamilyOf(wallStyle);
        public float Crest => top == TowerTop.Ruined ? crest : 0f;
        readonly List<WallBreach> noBreaches = new List<WallBreach>();
        public List<WallBreach> Breaches => noBreaches;
        public WallCap Cap => WallCap.None;
        public float PostSpacing => 2.4f;

        /// <summary>The wall's line: its plan's middle line (half the thickness in from the outside), closed, from its south point.</summary>
        public List<Vector3> WorldLine()
        {
            var list = new List<Vector3>();
            float r = radius - thickness * 0.5f;
            if (plan == TowerPlan.Square)
                foreach (var c in new[] { new Vector2(0f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f), new Vector2(0f, -1f) })
                    list.Add(transform.TransformPoint(new Vector3(c.x * r, 0f, c.y * r)));
            else
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * r / 1.6f), 10, 40);
                for (int i = 0; i <= n; i++)
                {
                    float a = -Mathf.PI * 0.5f + i * 2f * Mathf.PI / n;
                    list.Add(transform.TransformPoint(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r)));
                }
            }
            return list;
        }

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
    }
}
