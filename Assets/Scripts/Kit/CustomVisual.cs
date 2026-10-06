using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Marks a hand-made visual root on an obstacle prefab or a variant (docs/OBSTACLES.md §4): renderers only, never a collider.
    /// The builders' look passes (<c>KitSkin</c> reskins, <c>IndustrialRestyle</c>, <c>NatureRestyle</c>) leave everything under it
    /// alone, so a variant keeps its own models and materials in every course. Presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    public class CustomVisual : MonoBehaviour
    {
        /// <summary>True when <paramref name="t"/> is (under) a hand-made visual.</summary>
        public static bool Covers(Component t) => t != null && t.GetComponentInParent<CustomVisual>(true) != null;
    }
}
