using System;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Marks the root of a piece's generated geometry (<see cref="LevelPiece.Output"/>, ARCHITECTURE §27): rebuilt from the data, never
    /// edited. It remembers what it was built from (a signature of the piece's inputs: the editor rebuilds only when it changes), the
    /// line a walker treads on it (the level's <see cref="RouteWalks"/> are gathered from these) and the points nothing holds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelPieceOutput : MonoBehaviour
    {
        [SerializeField, HideInInspector] string signature;
        [SerializeField, HideInInspector] Vector3[] walkLine = Array.Empty<Vector3>();
        [SerializeField, HideInInspector] int unheld;

        public string Signature => signature;
        /// <summary>The walking surface's centre line (world), empty when the piece is not walked along (a deck, a rail).</summary>
        public Vector3[] WalkLine => walkLine;
        /// <summary>Hangers that found nothing to tie to: the piece is not held there.</summary>
        public int Unheld => unheld;

        /// <summary>Editor: what the geometry was built from.</summary>
        public void Configure(string builtFrom, Vector3[] line, int unheldPoints)
        {
            signature = builtFrom;
            walkLine = line ?? Array.Empty<Vector3>();
            unheld = unheldPoints;
        }
    }
}
