using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>How worn a piece of timber looks (ARCHITECTURE §27): its share of aged boards. Never rotten: the rotten look is the falling planks' (spec §19).</summary>
    public enum TimberWear { New, Aged }

    /// <summary>What holds a piece of timber up (ARCHITECTURE §27): the kit's own choice, or one forced.</summary>
    public enum TimberSupport { Auto, Posts, Tower, Braces, Hangers, None }

    /// <summary>
    /// A piece of an authored level (ARCHITECTURE §27): the data an editor tool draws its geometry from (points, sizes, choices),
    /// and nothing else. The geometry is generated under <see cref="Output"/> by the editor (HotPatata.Editor.LevelPieceBuilder)
    /// each time the data changes, and is never edited by hand. Gameplay never reads a piece.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class LevelPiece : MonoBehaviour
    {
        [SerializeField, HideInInspector] string pieceId;
        [SerializeField, HideInInspector] GameObject output;
        [SerializeField, Tooltip("Changes the random cuts, board lengths and aged boards; the shape stays.")] int seed;

        /// <summary>The piece's stable id: its generated meshes live in a folder of that name.</summary>
        public string PieceId => pieceId;
        public int Seed => seed;
        /// <summary>The generated geometry (rebuilt from the data, never edited).</summary>
        public GameObject Output => output;

        /// <summary>Editor: gives the piece an id (a new one when <paramref name="renew"/>, e.g. a duplicate).</summary>
        public void EnsureId(bool renew = false)
        {
            if (renew || string.IsNullOrEmpty(pieceId)) pieceId = Guid.NewGuid().ToString("N");
        }

        /// <summary>Editor: the geometry just generated.</summary>
        public void SetOutput(GameObject generated) => output = generated;

        /// <summary>A point of the piece's local space in the world.</summary>
        public Vector3 World(Vector3 local) => transform.TransformPoint(local);

        /// <summary>A world point in the piece's local space.</summary>
        public Vector3 Local(Vector3 world) => transform.InverseTransformPoint(world);
    }

    /// <summary>
    /// Something timber can lean on and hang from (ARCHITECTURE §27): a giant tree's trunk at a height, and the limbs above it a
    /// rope can be tied to.
    /// </summary>
    public interface ITrunkSource
    {
        /// <summary>The trunk's axis (world) and radius at height <paramref name="y"/>, false above or below it.</summary>
        bool TryTrunkAt(float y, out Vector3 center, out float radius);

        /// <summary>World points a hanger can be tied to.</summary>
        IEnumerable<Vector3> Anchors { get; }
    }

    /// <summary>
    /// Where an end of a walkway or a bridge goes: onto a deck's edge (the deck, and how far round its outline, 0..1), or onto an end of
    /// another walkway or bridge (joined end to end). Free otherwise.
    /// </summary>
    [Serializable]
    public struct TimberAttach
    {
        public TimberDeckShape deck;
        [Range(0f, 1f)] public float along;
        [Tooltip("The walkway or bridge whose end this end is joined to (when not on a deck).")] public LevelPiece joined;
        [Tooltip("Joined to its start (else its end).")] public bool joinedStart;

        public bool Attached => OnDeck || Joined;
        public bool OnDeck => deck != null;
        // a removed piece is null to Unity even while C# still holds it
        public bool Joined => deck == null && joined != null && (joined is TimberWalkShape || joined is RopeBridgeShape);

        /// <summary>
        /// Where this end goes (and, on a deck, the edge's outward direction); false when free. A joined end goes where the other piece
        /// puts its own end (on its deck or at its point), never through that piece's joins: two ends joined to each other never loop.
        /// </summary>
        public bool TryPoint(out Vector3 point, out Vector3 outward)
        {
            outward = Vector3.zero;
            if (deck != null)
            {
                point = deck.EdgePoint(along, out outward);
                return true;
            }
            switch (Joined ? joined : null)
            {
                case TimberWalkShape walk:
                    point = walk.OwnEnd(joinedStart);
                    return true;
                case RopeBridgeShape bridge:
                    point = bridge.OwnEnd(joinedStart);
                    return true;
            }
            point = default;
            return false;
        }

        /// <summary>"on Deck", "joined to Bridge" or "free".</summary>
        public string Describe => deck != null ? "on " + deck.name : Joined ? "joined to " + joined.name : "free";
    }
}
