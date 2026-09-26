using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Follows <see cref="Target"/>'s CameraTarget using that player's aim yaw/pitch. Pulls in
    /// against environment geometry so it never clips through walls.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        const float ProbeRadius = 0.25f;
        const float MinDistance = 0.4f;

        [SerializeField] Player target;
        [SerializeField] GameTuning tuning;

        int collisionMask;

        public Player Target
        {
            get => target;
            set => target = value;
        }

        void Awake() => collisionMask = LayerMask.GetMask("Environment");

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 pivot = target.CameraTarget.position;
            Quaternion rot = target.Look.AimRotation;
            Vector3 back = rot * Vector3.back;

            float distance = tuning.cameraDistance;
            if (Physics.SphereCast(pivot, ProbeRadius, back, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(MinDistance, hit.distance);

            transform.SetPositionAndRotation(pivot + back * distance, rot);
        }
    }
}
