using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// How the pieces of an authored level meet (ARCHITECTURE §27): the ends of walkways and bridges attached to a deck's edge (they
    /// follow it, and open its railing there), and the snap that attaches an end dropped near an edge.
    /// </summary>
    public static class TimberLinks
    {
        /// <summary>An end dropped this close to a deck's edge (flat metres) attaches to it.</summary>
        public const float SnapDistance = 0.8f;
        /// <summary>... and this close to its height.</summary>
        public const float SnapHeight = 1.5f;
        /// <summary>A railing opening is this much wider than what passes through it.</summary>
        public const float OpeningMargin = 0.3f;

        /// <summary>One end of a walkway or a bridge on a deck: where round the edge, and how wide it is.</summary>
        public struct Link
        {
            public LevelPiece piece;
            public TimberDeckShape deck;
            public float along, width;
        }

        public static IEnumerable<LevelPiece> Pieces(Scene scene) =>
            scene.IsValid() && scene.isLoaded
                ? scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelPiece>(true))
                : Enumerable.Empty<LevelPiece>();

        /// <summary>Every attached end in the scene.</summary>
        public static IEnumerable<Link> Links(Scene scene)
        {
            foreach (var piece in Pieces(scene))
            {
                if (piece is TimberWalkShape walk)
                {
                    if (walk.Start.Attached) yield return new Link { piece = walk, deck = walk.Start.deck, along = walk.Start.along, width = walk.Width };
                    if (walk.End.Attached) yield return new Link { piece = walk, deck = walk.End.deck, along = walk.End.along, width = walk.Width };
                }
                else if (piece is RopeBridgeShape bridge)
                {
                    if (bridge.StartAttach.Attached) yield return new Link { piece = bridge, deck = bridge.StartAttach.deck, along = bridge.StartAttach.along, width = bridge.Width };
                    if (bridge.EndAttach.Attached) yield return new Link { piece = bridge, deck = bridge.EndAttach.deck, along = bridge.EndAttach.along, width = bridge.Width };
                }
            }
        }

        /// <summary>The railing openings of <paramref name="deck"/>: its own, and one where each attached end meets it.</summary>
        public static List<TimberKit.Opening> OpeningsOf(TimberDeckShape deck)
        {
            var list = new List<TimberKit.Opening>();
            foreach (var o in deck.Openings) list.Add(new TimberKit.Opening(deck.EdgePoint(o.along), o.width));
            foreach (var link in Links(deck.gameObject.scene).Where(l => l.deck == deck))
                list.Add(new TimberKit.Opening(deck.EdgePoint(link.along), link.width + OpeningMargin));
            return list;
        }

        /// <summary>
        /// The deck edge an end dropped at <paramref name="world"/> attaches to (the nearest within <see cref="SnapDistance"/> and
        /// <see cref="SnapHeight"/>), false when none is near.
        /// </summary>
        public static bool TrySnap(Scene scene, Vector3 world, LevelPiece self, out TimberAttach attach, out Vector3 edge)
        {
            attach = default;
            edge = world;
            float best = SnapDistance;
            foreach (var deck in Pieces(scene).OfType<TimberDeckShape>())
            {
                if (deck == self || deck.Outline.Count < 3) continue;
                float along = deck.NearestAlong(world, out float distance);
                if (distance > best) continue;
                var point = deck.EdgePoint(along);
                if (Mathf.Abs(point.y - world.y) > SnapHeight) continue;
                best = distance;
                attach = new TimberAttach { deck = deck, along = along };
                edge = point;
            }
            return attach.Attached;
        }
    }
}
