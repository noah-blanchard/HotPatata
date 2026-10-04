using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The menu camera's slow drift (ARCHITECTURE §6.2): a gentle orbit around <see cref="pivot"/> with a little bob, so
    /// the backdrop never looks frozen. Presentation only, on unscaled time (the menu is offline). Kept small and slow: no
    /// shake, and it scales with the camera effects setting (<see cref="Settings.ViewEffectsStrength"/>).
    /// </summary>
    public class MenuCameraDrift : MonoBehaviour
    {
        [SerializeField] GameTuning tuning;
        [SerializeField, Tooltip("The point the camera orbits and looks at.")] Vector3 pivot;
        [SerializeField, Tooltip("Orbit swing (degrees each way).")] float yawDegrees = 6f;
        [SerializeField, Tooltip("Seconds per orbit swing.")] float yawPeriod = 26f;
        [SerializeField, Tooltip("Height bob (m).")] float bobHeight = 0.35f;
        [SerializeField, Tooltip("Seconds per bob.")] float bobPeriod = 9f;

        Vector3 offset;

        public void Configure(GameTuning gameTuning, Vector3 lookAt) { tuning = gameTuning; pivot = lookAt; }

        void Awake() => offset = transform.position - pivot;

        void LateUpdate()
        {
            float strength = tuning != null ? Settings.ViewEffectsStrength(tuning) : 1f;
            float t = Time.unscaledTime;
            float yaw = Mathf.Sin(t * 2f * Mathf.PI / yawPeriod) * yawDegrees * strength;
            float bob = Mathf.Sin(t * 2f * Mathf.PI / bobPeriod) * bobHeight * strength;
            transform.position = pivot + Quaternion.Euler(0f, yaw, 0f) * offset + Vector3.up * bob;
            transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
        }
    }
}
