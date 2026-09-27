using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Owns the player's aim yaw/pitch. The camera follows it; movement is relative to yaw; throws
    /// are aimed with it. Not replicated - purely local.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerLook : MonoBehaviour
    {
        Player player;
        float yaw;
        float pitch;
        Vector3 baseEyePosition;

        public float Yaw => yaw;
        public float Pitch => pitch;
        public Quaternion AimRotation => Quaternion.Euler(pitch, yaw, 0f);
        public Quaternion YawRotation => Quaternion.Euler(0f, yaw, 0f);

        void Awake()
        {
            player = GetComponent<Player>();
            yaw = transform.eulerAngles.y;
            baseEyePosition = player.CameraTarget.localPosition;
        }

        void Update()
        {
            if (!player.IsLocal || player.ControlLocked || !player.Input.Active) return;

            var t = player.Tuning;
            Vector2 delta = player.Input.Look;
            delta *= player.Input.LookIsMouse ? t.mouseSensitivity : t.stickLookSpeed * Time.deltaTime;

            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, t.pitchMin, t.pitchMax);
        }

        // The eye pivot carries the camera, the hand anchor and the throw origin, so the bomb stays
        // in view however you look.
        void LateUpdate()
        {
            float p = player.IsLocal ? pitch : player.Net.RemotePitch;   // remote players' pitch is replicated
            var feel = player.IsLocal ? player.Feel : null;
            if (feel != null)
            {
                // Roll, recoil and bob are applied to the eye pivot, so the held bomb moves with the view.
                player.CameraTarget.localRotation = Quaternion.Euler(p + feel.PitchKick, 0f, feel.Roll);
                player.CameraTarget.localPosition = baseEyePosition + feel.EyeOffset;
            }
            else
            {
                player.CameraTarget.localRotation = Quaternion.Euler(p, 0f, 0f);
            }
        }

        public void SetYaw(float degrees) => yaw = degrees;

        public void SetAim(float yawDegrees, float pitchDegrees)
        {
            yaw = yawDegrees;
            pitch = pitchDegrees;
        }
    }
}
