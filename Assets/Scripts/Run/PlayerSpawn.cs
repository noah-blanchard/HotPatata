using UnityEngine;

namespace HotPatata
{
    /// <summary>Marks where a player slot (0-3) starts / respawns. Drawn as a gizmo in the Scene view.</summary>
    public class PlayerSpawn : MonoBehaviour
    {
        [SerializeField, Range(0, 3)] int slot;
        [SerializeField] Color gizmoColor = Color.cyan;

        public int Slot => slot;

        void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Vector3 p = transform.position;
            Gizmos.DrawWireSphere(p + Vector3.up * 0.1f, 0.35f);
            Gizmos.DrawLine(p, p + Vector3.up * 1.8f);
            Gizmos.DrawRay(p + Vector3.up * 0.9f, transform.forward * 0.9f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(p + Vector3.up * 2f, "Spawn " + (slot + 1));
#endif
        }
    }
}
