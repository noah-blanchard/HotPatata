using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Timber or rope along a line (ARCHITECTURE §27.1): a rope, a curved rim board, a stringer, a hand rail, a log, an iron band round
    /// a trunk. Its points are local to the element. A round profile is a rope or a log (a radius); a square one is a sawn section
    /// (width across, kept level, height up). Either end may hang on another object (<see cref="StartAnchor"/>,
    /// <see cref="EndAnchor"/>): it follows it when that moves. A path of two points dips by <see cref="Sag"/> in the middle.
    /// </summary>
    public sealed class TimberPath : TimberElement
    {
        public enum Profile { Round, Square }

        /// <summary>Where the line runs on a square section: on its top face, through its middle, or along its bottom.</summary>
        public enum Seat { Top, Center, Bottom }

        [SerializeField] List<Vector3> points = new List<Vector3> { Vector3.zero, Vector3.forward * 2f };
        [SerializeField] Profile profile = Profile.Round;
        [SerializeField, Tooltip("A round profile's radius (m).")] float radius = 0.03f;
        [SerializeField, Tooltip("A square profile's width across (m).")] float width = 0.1f;
        [SerializeField, Tooltip("A square profile's height (m).")] float height = 0.1f;
        [SerializeField] Seat seat = Seat.Center;
        [SerializeField, Tooltip("Ends closed (a log, a cut beam).")] bool caps;
        [SerializeField, Tooltip("The path closes on itself (a band round a trunk).")] bool closed;
        [SerializeField, Tooltip("How far a two-point path dips at its middle (m): a hanging rope.")] float sag;
        [SerializeField, Tooltip("The start hangs on this object (it follows it).")] Transform startAnchor;
        [SerializeField, HideInInspector] Vector3 startOffset;
        [SerializeField, Tooltip("The end hangs on this object (it follows it).")] Transform endAnchor;
        [SerializeField, HideInInspector] Vector3 endOffset;

        public List<Vector3> Points => points;
        public Profile Shape { get => profile; set => profile = value; }
        public float Radius { get => radius; set => radius = Mathf.Max(0.005f, value); }
        public float Width { get => width; set => width = Mathf.Max(0.005f, value); }
        public float Height { get => height; set => height = Mathf.Max(0.005f, value); }
        public Seat Anchor { get => seat; set => seat = value; }
        public bool Caps { get => caps; set => caps = value; }
        public bool Closed { get => closed; set => closed = value; }
        public float Sag { get => sag; set => sag = Mathf.Max(0f, value); }
        public Transform StartAnchor => startAnchor;
        public Transform EndAnchor => endAnchor;

        /// <summary>Hangs the start (or the end) on <paramref name="anchor"/> where it is now, or frees it (null).</summary>
        public void Attach(bool start, Transform anchor)
        {
            if (points.Count < 2) return;
            var world = transform.TransformPoint(start ? points[0] : points[points.Count - 1]);
            var offset = anchor != null ? anchor.InverseTransformPoint(world) : Vector3.zero;
            if (start) { startAnchor = anchor; startOffset = offset; }
            else { endAnchor = anchor; endOffset = offset; }
        }

        /// <summary>Moves the ends onto their anchors (the inner points follow, spread along the path); true if anything moved.</summary>
        public bool FollowAnchors()
        {
            if (points.Count < 2) return false;
            var startDelta = startAnchor != null ? transform.InverseTransformPoint(startAnchor.TransformPoint(startOffset)) - points[0] : Vector3.zero;
            var endDelta = endAnchor != null ? transform.InverseTransformPoint(endAnchor.TransformPoint(endOffset)) - points[points.Count - 1] : Vector3.zero;
            if (startDelta.sqrMagnitude < 1e-8f && endDelta.sqrMagnitude < 1e-8f) return false;
            // each point moves by the ends' moves, weighted by where it lies along the path
            float total = 0f;
            var along = new float[points.Count];
            for (int i = 1; i < points.Count; i++) along[i] = total += Vector3.Distance(points[i], points[i - 1]);
            for (int i = 0; i < points.Count; i++)
            {
                float t = total < 1e-5f ? i / (float)(points.Count - 1) : along[i] / total;
                points[i] += Vector3.Lerp(startDelta, endDelta, t);
            }
            return true;
        }

        public override string ShapeKey()
        {
            var s = new StringBuilder("path|");
            s.Append(profile).Append('|').Append(seat).Append('|').Append(caps).Append(closed).Append('|');
            s.Append(Mm(radius)).Append(',').Append(Mm(width)).Append(',').Append(Mm(height)).Append(',').Append(Mm(sag)).Append('|').Append(Grain).Append('|');
            foreach (var p in points) s.Append(Mm(p.x)).Append(',').Append(Mm(p.y)).Append(',').Append(Mm(p.z)).Append(';');
            return s.ToString();
        }

        static string Mm(float v) => Mathf.RoundToInt(v * 1000f).ToString(CultureInfo.InvariantCulture);
    }
}
