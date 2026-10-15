using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A walkway of an authored level (ARCHITECTURE §27): boards along a line through its points, steps where it climbs, stringers,
    /// supports and rails. An end on a deck's edge is attached to it (it follows the deck, and opens its railing). Data only: the
    /// editor draws it with the timber kit.
    /// </summary>
    [SelectionBase]
    public sealed class TimberWalkShape : LevelPiece
    {
        /// <summary>How far onto a deck an attached end reaches past its edge (the walk overlaps the deck there).</summary>
        public const float AttachLead = 0.3f;

        [SerializeField, Tooltip("The walking surface's centre line in the piece's space (a smooth line goes through them).")]
        List<Vector3> points = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 4f), new Vector3(0f, 1.5f, 8f), new Vector3(0f, 1.5f, 10f) };
        [SerializeField, Min(0.8f)] float width = 1.4f;
        [SerializeField] bool railLeft = true, railRight = true;
        [SerializeField, Tooltip("A boardwalk on sleepers on the ground rather than raised.")] bool onGround;
        [SerializeField] TimberSupport support;
        [SerializeField] TimberWear wear;
        [SerializeField] float maxPost = 4f;
        [SerializeField] TimberAttach start, end;

        public List<Vector3> Points => points;
        public float Width { get => width; set => width = Mathf.Max(0.8f, value); }
        public bool RailLeft { get => railLeft; set => railLeft = value; }
        public bool RailRight { get => railRight; set => railRight = value; }
        public bool OnGround { get => onGround; set => onGround = value; }
        public TimberSupport Support { get => support; set => support = value; }
        public TimberWear Wear { get => wear; set => wear = value; }
        public float MaxPost { get => maxPost; set => maxPost = value; }
        public TimberAttach Start { get => start; set => start = value; }
        public TimberAttach End { get => end; set => end = value; }

        /// <summary>
        /// The control points in the world: an end on a deck moved onto its edge (a short lead on the deck first), an end joined to another
        /// piece's end moved onto it.
        /// </summary>
        public List<Vector3> WorldControl()
        {
            var list = new List<Vector3>(points.Count + 2);
            foreach (var p in points) list.Add(transform.TransformPoint(p));
            if (list.Count < 2) return list;
            if (start.TryPoint(out var first, out var outward))
            {
                list[0] = first;
                if (start.OnDeck) list.Insert(0, first - outward * AttachLead);
            }
            if (end.TryPoint(out var last, out var outwardEnd))
            {
                list[list.Count - 1] = last;
                if (end.OnDeck) list.Add(last - outwardEnd * AttachLead);
            }
            return list;
        }

        /// <summary>Where this walkway puts its start or end by itself: on its deck's edge, else at its point (never through a join).</summary>
        public Vector3 OwnEnd(bool atStart)
        {
            var attach = atStart ? start : end;
            if (attach.OnDeck) return attach.deck.EdgePoint(attach.along);
            if (points.Count == 0) return transform.position;
            return transform.TransformPoint(atStart ? points[0] : points[points.Count - 1]);
        }
    }
}
