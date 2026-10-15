using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>A gap left in a deck's railing: how far round the outline (0..1) and how wide (m).</summary>
    [Serializable]
    public struct TimberOpening
    {
        [Range(0f, 1f)] public float along;
        [Min(0.6f)] public float width;
    }

    /// <summary>
    /// A deck of an authored level (ARCHITECTURE §27): boards on joists and bearers inside an outline, its walking surface at the
    /// piece's height (tilted by <see cref="Slope"/>), held by posts, a tower, braces to the trunk it hugs or hangers. Walkways and
    /// bridges that end on its edge open its railing there. Data only: the editor draws it with the timber kit.
    /// </summary>
    [SelectionBase]
    public sealed class TimberDeckShape : LevelPiece
    {
        [SerializeField, Tooltip("The outline in the piece's space (x, z; y is ignored): the deck's surface is at the piece's height.")]
        List<Vector3> outline = new List<Vector3>
        {
            new Vector3(-3f, 0f, -2f), new Vector3(3f, 0f, -2f), new Vector3(3f, 0f, 2f), new Vector3(-3f, 0f, 2f),
        };
        [SerializeField, Tooltip("Rise per metre along world x and z (0, 0 = level).")] Vector2 slope;
        [SerializeField, Tooltip("The boards' direction, degrees from the piece's forward.")] float grain;
        [SerializeField] bool rails = true;
        [SerializeField, Tooltip("Gaps in the railing besides the ones the walkways and bridges ending here open.")]
        List<TimberOpening> openings = new List<TimberOpening>();
        [SerializeField, Tooltip("The giant tree the deck hugs (cut round its trunk, braced to it).")] MonoBehaviour trunk;
        [SerializeField] TimberSupport support;
        [SerializeField] TimberWear wear;
        [SerializeField, Tooltip("Posts taller than this are not built (a tower, braces or hangers hold the deck instead).")] float maxPost = 4f;

        public List<Vector3> Outline => outline;
        public Vector2 Slope { get => slope; set => slope = value; }
        public float Grain { get => grain; set => grain = value; }
        public bool Rails { get => rails; set => rails = value; }
        public List<TimberOpening> Openings => openings;
        public ITrunkSource Trunk => trunk as ITrunkSource;
        public MonoBehaviour TrunkComponent { get => trunk; set => trunk = value is ITrunkSource ? value : null; }
        public TimberSupport Support { get => support; set => support = value; }
        public TimberWear Wear { get => wear; set => wear = value; }
        public float MaxPost { get => maxPost; set => maxPost = value; }

        /// <summary>The surface's height at world (x, z).</summary>
        public float TopAt(Vector2 xz)
        {
            var o = transform.position;
            return o.y + slope.x * (xz.x - o.x) + slope.y * (xz.y - o.z);
        }

        /// <summary>The outline in world (x, z).</summary>
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

        /// <summary>The world point on the edge <paramref name="along"/> (0..1) of the way round, on the surface.</summary>
        public Vector3 EdgePoint(float along) => EdgePoint(along, out _);

        /// <summary>The edge point and the outward direction there (world, flat).</summary>
        public Vector3 EdgePoint(float along, out Vector3 outward)
        {
            var ring = WorldOutline();
            outward = Vector3.forward;
            if (ring.Count < 2) return transform.position;
            bool ccw = SignedArea(ring) > 0f;
            float total = Perimeter(ring);
            float d = Mathf.Repeat(along, 1f) * total;
            for (int i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                float l = Vector2.Distance(a, b);
                if (d <= l || i == ring.Count - 1)
                {
                    var q = Vector2.Lerp(a, b, l < 1e-5f ? 0f : Mathf.Clamp01(d / l));
                    var e = (b - a).normalized;
                    var n = ccw ? new Vector2(e.y, -e.x) : new Vector2(-e.y, e.x);
                    outward = new Vector3(n.x, 0f, n.y);
                    return new Vector3(q.x, TopAt(q), q.y);
                }
                d -= l;
            }
            return transform.position;
        }

        /// <summary>How far round the outline (0..1) the edge point nearest <paramref name="world"/> is, and its flat distance.</summary>
        public float NearestAlong(Vector3 world, out float distance)
        {
            var ring = WorldOutline();
            var p = new Vector2(world.x, world.z);
            float total = Perimeter(ring), run = 0f, best = 0f;
            distance = float.MaxValue;
            for (int i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                var ab = b - a;
                float l = ab.magnitude;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float dist = Vector2.Distance(p, a + ab * t);
                if (dist < distance)
                {
                    distance = dist;
                    best = total < 1e-5f ? 0f : (run + t * l) / total;
                }
                run += l;
            }
            return best;
        }

        public static float Perimeter(IList<Vector2> p)
        {
            float l = 0f;
            for (int i = 0; i < p.Count; i++) l += Vector2.Distance(p[i], p[(i + 1) % p.Count]);
            return l;
        }

        public static float SignedArea(IList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                var q = p[i];
                var r = p[(i + 1) % p.Count];
                a += q.x * r.y - r.x * q.y;
            }
            return a * 0.5f;
        }
    }
}
