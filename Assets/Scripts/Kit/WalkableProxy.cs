using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Marks the collider of a course's static organic ground (ARCHITECTURE §25.5): a closed, smoothed <see cref="MeshCollider"/>
    /// generated with the piece it stands for (a curved deck, a stair or boardwalk, a sagging bridge, the terrain), on the
    /// <c>Environment</c> layer, never moving. It is the one exception to the box-only rule: obstacles, movers and everything
    /// a system owns keep box colliders (OBSTACLES §4.5). The drawn piece stays within a few centimetres of it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshCollider))]
    public sealed class WalkableProxy : MonoBehaviour
    {
        public enum Kind { Deck, Walkway, Bridge, Terrain, Solid }

        [SerializeField] Kind kind;

        /// <summary>How far the drawn surface may stray from the proxy (metres): stair treads stand half a rise off their ramp.</summary>
        [SerializeField] float tolerance = 0.08f;

        public Kind ProxyKind => kind;
        public float Tolerance => tolerance;

        public void Configure(Kind proxyKind, float drawnTolerance)
        {
            kind = proxyKind;
            tolerance = drawnTolerance;
        }
    }
}
