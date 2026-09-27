using UnityEngine;

namespace Beep
{
    /// <summary>
    /// CharacterController-based third-person motor: fast accel/brake, moderate air control,
    /// single jump with coyote time and jump buffer. Knows nothing about the bomb.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Player))]
    public class PlayerMotor : MonoBehaviour
    {
        const float GroundStick = 2f;   // small downward push so isGrounded stays reliable
        const float Never = -999f;

        Player player;
        CharacterController controller;

        Vector3 horizontalVelocity;
        float verticalVelocity;
        float lastGroundedTime = Never;
        float lastJumpPressTime = Never;

        public bool Grounded { get; private set; }
        public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;

        void Awake()
        {
            player = GetComponent<Player>();
            controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            var t = player.Tuning;
            float dt = Time.deltaTime;
            bool locked = player.ControlLocked;
            var input = player.Input;

            // First person: the body always faces the way you are looking.
            transform.rotation = player.Look.YawRotation;

            // --- Horizontal: camera-relative wish direction -------------------------------------
            Vector2 stick = locked ? Vector2.zero : Vector2.ClampMagnitude(input.Move, 1f);
            Vector3 wish = player.Look.YawRotation * new Vector3(stick.x, 0f, stick.y);
            Vector3 target = wish * t.moveSpeed;

            float rate = wish.sqrMagnitude > 0.0001f ? t.acceleration : t.braking;
            if (!Grounded) rate *= t.airControl;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, rate * dt);

            // --- Jump: buffer the press, allow it within the coyote window ---------------------
            if (!locked && input.JumpPressed) lastJumpPressTime = Time.time;

            bool buffered = Time.time - lastJumpPressTime <= t.jumpBuffer;
            bool coyote = Time.time - lastGroundedTime <= t.coyoteTime;
            if (buffered && coyote)
            {
                verticalVelocity = Mathf.Sqrt(2f * t.gravity * t.jumpHeight);
                lastJumpPressTime = Never;
                lastGroundedTime = Never;   // consume coyote so we cannot double-jump
                Grounded = false;
            }

            // --- Vertical -----------------------------------------------------------------------
            if (Grounded && verticalVelocity < 0f) verticalVelocity = -GroundStick;
            else verticalVelocity -= t.gravity * dt;

            // --- Move ---------------------------------------------------------------------------
            var flags = controller.Move(Velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

            Grounded = controller.isGrounded;
            if (Grounded) lastGroundedTime = Time.time;
        }

        public void ResetVelocity()
        {
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            lastJumpPressTime = Never;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            // CharacterController overrides transform writes unless it is disabled around them.
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            ResetVelocity();
            Grounded = false;
        }
    }
}
