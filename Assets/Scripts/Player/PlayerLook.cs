using UnityEngine;

namespace HotPatata
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

        /// <summary>
        /// The direction the camera actually shows: the aim plus the current view kick (throw/catch punches).
        /// Throws use this, so the crosshair never lies. Roll and bob do not change where the view points.
        /// </summary>
        public Quaternion ViewRotation
        {
            get
            {
                var feel = player.IsLocal ? player.Feel : null;
                return feel != null ? Quaternion.Euler(pitch + feel.PitchKick, yaw, 0f) : AimRotation;
            }
        }
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
            delta *= player.Input.LookIsMouse ? Settings.MouseSensitivity(t) : Settings.StickLookSpeed(t) * Time.deltaTime;
            if (Settings.InvertY) delta.y = -delta.y;

            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, t.pitchMin, t.pitchMax);
        }

        // The eye pivot carries the camera, the hand anchor and the throw origin, so the bomb stays
        // in view however you look.
        void LateUpdate()
        {
            float p = player.IsLocal ? pitch : player.Net.RemotePitch;   // remote players' pitch is replicated
            // The eye (and the hand and throw origin under it) sinks with a crouch or slide, on every machine.
            Vector3 eye = new Vector3(baseEyePosition.x, baseEyePosition.y * player.Motor.HeightScale, baseEyePosition.z);
            var feel = player.IsLocal ? player.Feel : null;
            if (feel != null)
            {
                // Roll, recoil and bob are applied to the eye pivot, so the held bomb moves with the view.
                player.CameraTarget.localRotation = Quaternion.Euler(p + feel.PitchKick, 0f, feel.Roll);
                player.CameraTarget.localPosition = eye + feel.EyeOffset;
            }
            else
            {
                player.CameraTarget.localRotation = Quaternion.Euler(p, 0f, 0f);
                player.CameraTarget.localPosition = eye;
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
