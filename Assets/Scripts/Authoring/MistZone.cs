using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A volume of an authored level where the mist thickens (ARCHITECTURE §27): the mist floor is raised to the box's top inside it
    /// (blended over <see cref="Blend"/> at its sides), so the mist pools in a hollow, a gorge or round a clearing. Read when the
    /// editor bakes the level's mist (on save). Presentation only.
    /// </summary>
    public sealed class MistZone : MonoBehaviour
    {
        [SerializeField, Tooltip("The box (x, y, z, metres, in its own axes): the mist fills it up to its top.")] Vector3 size = new Vector3(30f, 6f, 30f);
        [SerializeField, Min(0f), Tooltip("How wide its sides blend into the mist around (m).")] float blend = 6f;

        public Vector3 Size { get => size; set => size = Vector3.Max(value, Vector3.one); }
        public float Blend { get => blend; set => blend = Mathf.Max(0f, value); }

        /// <summary>The mist floor this zone asks for at (x, z), and how much it weighs there (0 outside, 1 well inside).</summary>
        public float Weight(float x, float z, out float floor)
        {
            var local = transform.InverseTransformPoint(new Vector3(x, transform.position.y, z));
            float outside = Mathf.Max(Mathf.Abs(local.x) - size.x * 0.5f, Mathf.Abs(local.z) - size.z * 0.5f);
            floor = transform.position.y + size.y * 0.5f;
            if (outside > 0f) return blend <= 0f ? 0f : Mathf.Clamp01(1f - outside / blend);
            return 1f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.75f, 0.85f, 0.95f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
