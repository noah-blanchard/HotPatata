using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Drifts the menu backdrop's clouds and far islands sideways, wrapping them around (ARCHITECTURE §6.2). Presentation
    /// only, on unscaled time (the menu is offline).
    /// </summary>
    public class MenuCloudDrift : MonoBehaviour
    {
        [SerializeField, Tooltip("Metres per second along +X.")] float speed = 1.2f;
        [SerializeField, Tooltip("Children wrap from +range to -range (m, around this object).")] float range = 180f;

        public void Configure(float metresPerSecond, float wrapRange) { speed = metresPerSecond; range = wrapRange; }

        void Update()
        {
            float step = speed * Time.unscaledDeltaTime;
            foreach (Transform child in transform)
            {
                var p = child.localPosition;
                // Each child drifts a little differently (by its height), so the sky has depth.
                p.x += step * (0.7f + 0.3f * Mathf.Repeat(Mathf.Abs(p.y) * 0.1f, 1f));
                if (p.x > range) p.x -= 2f * range;
                child.localPosition = p;
            }
        }
    }
}
