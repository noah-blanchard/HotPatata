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
        float pitch = 12f;

        public float Yaw => yaw;
        public float Pitch => pitch;
        public Quaternion AimRotation => Quaternion.Euler(pitch, yaw, 0f);
        public Quaternion YawRotation => Quaternion.Euler(0f, yaw, 0f);

        void Awake()
        {
            player = GetComponent<Player>();
            yaw = transform.eulerAngles.y;
        }

        void Update()
        {
            if (player.ControlLocked || !player.Input.Active) return;

            var t = player.Tuning;
            Vector2 delta = player.Input.Look;
            delta *= player.Input.LookIsMouse ? t.mouseSensitivity : t.stickLookSpeed * Time.deltaTime;

            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, t.pitchMin, t.pitchMax);
        }

        public void SetYaw(float degrees) => yaw = degrees;
    }
}
