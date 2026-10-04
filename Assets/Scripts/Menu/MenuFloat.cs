using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Menu backdrop decoration (ARCHITECTURE §6.2): bobs, spins or sways one object so the menu scene feels alive.
    /// Presentation only, placed by <c>MenuBackdropBuilder</c>. The menu is offline and local, so it runs on unscaled
    /// time rather than <see cref="SectionClock"/>.
    /// </summary>
    public class MenuFloat : MonoBehaviour
    {
        [SerializeField, Tooltip("Up and down travel (m).")] float bobHeight = 0.2f;
        [SerializeField, Tooltip("Seconds per bob.")] float bobPeriod = 3f;
        [SerializeField, Tooltip("Turn around the local up axis (degrees per second).")] float spinSpeed;
        [SerializeField, Tooltip("Rock around the local forward axis (degrees each way), e.g. a flag in the wind.")] float swayDegrees;
        [SerializeField, Tooltip("Seconds per sway.")] float swayPeriod = 2.5f;
        [SerializeField, Tooltip("Offset (s) so neighbours do not move in step.")] float phase;

        Vector3 basePosition;
        Quaternion baseRotation;

        public void Configure(float bob, float bobSeconds, float spin, float sway, float swaySeconds, float phaseSeconds)
        {
            bobHeight = bob;
            bobPeriod = bobSeconds;
            spinSpeed = spin;
            swayDegrees = sway;
            swayPeriod = swaySeconds;
            phase = phaseSeconds;
        }

        void Awake()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
        }

        void Update()
        {
            float t = Time.unscaledTime + phase;
            float bob = bobPeriod > 0f ? Mathf.Sin(t * 2f * Mathf.PI / bobPeriod) * bobHeight : 0f;
            float sway = swayPeriod > 0f ? Mathf.Sin(t * 2f * Mathf.PI / swayPeriod) * swayDegrees : 0f;
            transform.localPosition = basePosition + Vector3.up * bob;
            transform.localRotation = baseRotation * Quaternion.Euler(0f, t * spinSpeed, sway);
        }
    }
}
