using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Editor
{
    /// <summary>
    /// Where a piece of an authored level finds the ground, the trunks and the anchors (ARCHITECTURE §27): the timber kit's
    /// <see cref="TimberKit.Site"/> read from the scene instead of a builder's functions. The ground is the highest collider on
    /// Environment below the piece that is not timber itself (a post stands on the terrain or a rock, never on another deck); the
    /// trunks and anchors come from the scene's <see cref="ITrunkSource"/> (the giant trees).
    /// </summary>
    public static class SceneSite
    {
        /// <summary>How far down a post looks for the ground.</summary>
        public const float Reach = 400f;

        /// <summary>
        /// The site of a piece in <paramref name="scene"/>: trunks at height <paramref name="y"/>, the ground looked for from
        /// <paramref name="from"/> down.
        /// </summary>
        public static TimberKit.Site At(Scene scene, float y, float from, float maxPost)
        {
            var trunks = new List<TimberKit.Trunk>();
            var anchors = new List<Vector3>();
            foreach (var source in TrunkSources(scene))
            {
                if (source.TryTrunkAt(y, out var center, out float radius)) trunks.Add(new TimberKit.Trunk(center, radius));
                anchors.AddRange(source.Anchors);
            }
            return new TimberKit.Site { ground = p => GroundAt(p, from), trunks = trunks, anchors = anchors, maxPost = maxPost };
        }

        public static IEnumerable<ITrunkSource> TrunkSources(Scene scene) =>
            scene.IsValid() && scene.isLoaded
                ? scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).OfType<ITrunkSource>()
                : Enumerable.Empty<ITrunkSource>();

        /// <summary>The ground's height under (x, z) below <paramref name="from"/>, or NaN where there is none (the void).</summary>
        public static float GroundAt(Vector2 p, float from)
        {
            float best = float.NaN;
            var hits = Physics.RaycastAll(new Vector3(p.x, from, p.y), Vector3.down, Reach, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<LevelPieceOutput>() != null) continue;   // timber never stands on timber
                if (float.IsNaN(best) || hit.point.y > best) best = hit.point.y;
            }
            return best;
        }
    }
}
