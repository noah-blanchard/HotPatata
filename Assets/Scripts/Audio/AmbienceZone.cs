using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A box where the ambience sounds like another environment (a cave inside a forest section, the falls' gorge), placed by
    /// a course builder (ARCHITECTURE §26). While the listener is inside, its environment replaces the section's; the highest
    /// priority wins where boxes overlap. Presentation only: a bounds test against the listener, no physics, no trigger.
    /// </summary>
    public sealed class AmbienceZone : MonoBehaviour
    {
        static readonly List<AmbienceZone> All = new List<AmbienceZone>();

        [SerializeField, Tooltip("Ambience/Environments/<name> plays inside.")] string environment = "";
        [SerializeField, Tooltip("Size of the box, in its own axes (metres).")] Vector3 size = new Vector3(20f, 10f, 20f);
        [SerializeField, Tooltip("Higher wins where zones overlap.")] int priority;

        public string Environment => environment;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool Contains(Vector3 point)
        {
            var local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f && Mathf.Abs(local.z) <= size.z * 0.5f;
        }

        /// <summary>The environment of the highest-priority zone holding <paramref name="point"/>, or null.</summary>
        public static string At(Vector3 point)
        {
            AmbienceZone best = null;
            foreach (var zone in All)
                if (zone.Contains(point) && (best == null || zone.priority > best.priority)) best = zone;
            return best != null && !string.IsNullOrEmpty(best.environment) ? best.environment : null;
        }

        /// <summary>Editor only (course builders).</summary>
        public void Configure(string environment, Vector3 size, int priority)
        {
            this.environment = environment;
            this.size = size;
            this.priority = priority;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
