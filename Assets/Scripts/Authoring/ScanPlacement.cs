using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A scanned rock or root set into a rock face by the level tools (ARCHITECTURE §27.5): the face point it was set at and the face's
    /// outward normal, so the editor's checks can measure how far it sticks out. Decoration: gameplay never reads it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScanPlacement : MonoBehaviour
    {
        [SerializeField] Vector3 facePoint, faceNormal = Vector3.up;

        public Vector3 FacePoint => facePoint;
        public Vector3 FaceNormal => faceNormal;

        /// <summary>Editor: where on the face it was set.</summary>
        public void Configure(Vector3 point, Vector3 normal)
        {
            facePoint = point;
            faceNormal = normal.normalized;
        }
    }
}
