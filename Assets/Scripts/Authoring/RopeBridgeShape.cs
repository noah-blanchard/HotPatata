using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A rope bridge of an authored level (ARCHITECTURE §27): slats on foot ropes between two ends, hand ropes on posts, dipping at
    /// mid span. An end on a deck's edge is attached to it (it follows the deck, and opens its railing). Data only: the editor draws
    /// it with the timber kit.
    /// </summary>
    [SelectionBase]
    public sealed class RopeBridgeShape : LevelPiece
    {
        /// <summary>How far onto a deck an attached end reaches past its edge.</summary>
        public const float AttachLead = 0.15f;

        [SerializeField, Tooltip("The walking surface at each end, in the piece's space.")] Vector3 a = new Vector3(0f, 0f, 0f), b = new Vector3(0f, 0f, 10f);
        [SerializeField, Min(0.8f)] float width = 1.6f;
        [SerializeField, Tooltip("The dip at mid span (m); 0 = 6 % of the span, at most 1.2 m.")] float sag;
        [SerializeField, Tooltip("Colliders up to the hand ropes: no stepping off the side.")] bool sideWalls = true;
        [SerializeField] TimberWear wear;
        [SerializeField] TimberAttach startAttach, endAttach;

        public Vector3 A { get => a; set => a = value; }
        public Vector3 B { get => b; set => b = value; }
        public float Width { get => width; set => width = Mathf.Max(0.8f, value); }
        public float Sag { get => sag; set => sag = Mathf.Max(0f, value); }
        public bool SideWalls { get => sideWalls; set => sideWalls = value; }
        public TimberWear Wear { get => wear; set => wear = value; }
        public TimberAttach StartAttach { get => startAttach; set => startAttach = value; }
        public TimberAttach EndAttach { get => endAttach; set => endAttach = value; }

        /// <summary>The two ends in the world: on their decks' edges, on the ends they are joined to, else at their points.</summary>
        public (Vector3 a, Vector3 b) WorldEnds() => (End(true), End(false));

        Vector3 End(bool atStart)
        {
            var attach = atStart ? startAttach : endAttach;
            if (attach.TryPoint(out var point, out var outward)) return attach.OnDeck ? point - outward * AttachLead : point;
            return transform.TransformPoint(atStart ? a : b);
        }

        /// <summary>Where this bridge puts its start or end by itself: on its deck's edge, else at its point (never through a join).</summary>
        public Vector3 OwnEnd(bool atStart)
        {
            var attach = atStart ? startAttach : endAttach;
            if (attach.OnDeck) return attach.deck.EdgePoint(attach.along, out var outward) - outward * AttachLead;
            return transform.TransformPoint(atStart ? a : b);
        }

        /// <summary>The dip used: the set one, else 6 % of the span (1.2 m at most).</summary>
        public float SagFor(Vector3 wa, Vector3 wb) => sag > 0f ? sag : Mathf.Min(0.06f * Vector3.Distance(wa, wb), 1.2f);
    }
}
