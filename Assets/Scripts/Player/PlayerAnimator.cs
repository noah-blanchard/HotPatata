using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Presentation-only bridge between the player simulation and the character's Animator (the slot's
    /// <see cref="PlayerCharacter"/>, bound by <see cref="PlayerPresentation"/>). It also keeps the shared HandAnchor on
    /// the first-person camera for the viewed player, and in the right palm (<see cref="PlayerCharacter.HandSocket"/>)
    /// for every third-person player, so the potato rides the hand and goes back with the arm on a held throw.
    /// Speed runs 0 (idle) to 1 (run) to 2 (sprint); slides and crouch-walks are procedural poses on the model root
    /// (no slide clip), driven by the replicated motor state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Player))]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int Grounded = Animator.StringToHash("Grounded");
        static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        static readonly int Throw = Animator.StringToHash("Throw");
        static readonly int ThrowSpeed = Animator.StringToHash("ThrowSpeed");
        const int ThrowLayer = 1;
        const float FollowThroughSpeed = 1.6f;   // the arm catches up with a throw that has already left

        enum ThrowPhase { Idle, Charging, Holding, Releasing }

        [SerializeField] Animator animator;
        [SerializeField, Tooltip("The right palm (the character's HandSocket): the potato sits here in third person.")] Transform handSocket;
        [SerializeField, Tooltip("The held potato from the palm, in the player's frame (right, up, forward, m): out of the hip, a little ahead.")]
        Vector3 heldOffset = new Vector3(0.05f, 0f, 0.07f);
        [SerializeField, Tooltip("The held potato's yaw (degrees): its long side points forward, along the arm.")] float heldYaw = 90f;
        [SerializeField] float speedDampTime = 0.1f;
        [Header("Procedural poses")]
        [Tooltip("Model root that is leaned and squashed (defaults to the Animator's transform).")]
        [SerializeField] Transform poseRoot;
        [SerializeField] float slideLeanDegrees = 62f;
        [SerializeField] Vector3 slideOffset = new Vector3(0f, 0.05f, 0.55f);
        [SerializeField] float crouchLeanDegrees = 18f;
        [SerializeField] float crouchSquash = 0.7f;
        [SerializeField] float poseBlendRate = 10f;

        Player player;
        BombController boundBomb;
        Vector3 lastPosition;
        Vector3 cameraHandPosition;
        Quaternion cameraHandRotation;
        int groundMask;
        float throwGuardUntil;
        ThrowPhase throwPhase;
        Vector3 poseBasePosition, poseBaseScale;
        Quaternion poseBaseRotation;
        float slidePose, crouchPose;

        void Awake()
        {
            player = GetComponent<Player>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null) animator.applyRootMotion = false;
            if (poseRoot == null && animator != null) poseRoot = animator.transform;
            CapturePoseBase();

            cameraHandPosition = player.HandAnchor.localPosition;
            cameraHandRotation = player.HandAnchor.localRotation;
            lastPosition = transform.position;
            groundMask = LayerMask.GetMask("Environment", "Hazard");
        }

        /// <summary>Drives <paramref name="character"/> (the slot's character, swapped in by <see cref="PlayerPresentation"/>).</summary>
        public void Bind(PlayerCharacter character)
        {
            if (character == null || character.Animator == animator && character.HandSocket == handSocket) return;
            animator = character.Animator;
            handSocket = character.HandSocket;
            if (animator != null) animator.applyRootMotion = false;
            poseRoot = animator != null ? animator.transform : null;
            CapturePoseBase();
            throwPhase = ThrowPhase.Idle;
        }

        void CapturePoseBase()
        {
            if (poseRoot == null) return;
            poseBasePosition = poseRoot.localPosition;
            poseBaseRotation = poseRoot.localRotation;
            poseBaseScale = poseRoot.localScale;
            slidePose = crouchPose = 0f;
        }

        void OnEnable()
        {
            lastPosition = transform.position;
            TryBindBomb();
        }

        void OnDisable()
        {
            if (boundBomb != null) boundBomb.BombThrown -= OnBombThrown;
            boundBomb = null;
        }

        void Update()
        {
            TryBindBomb();
            if (animator == null) return;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 velocity = player.IsLocal
                ? player.Motor.Velocity
                : (transform.position - lastPosition) / dt;
            lastPosition = transform.position;

            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            var motor = player.Motor;
            bool grounded = player.IsLocal
                ? player.Motor.Grounded
                : Physics.CheckSphere(transform.position + Vector3.up * 0.12f, 0.22f,
                    groundMask, QueryTriggerInteraction.Ignore);

            // Sliding holds a still pose (the legs do not run); otherwise run = 1, sprint = 2.
            float speedParam = motor.IsSliding ? 0f : Mathf.Min(PlayerMotor.MotionFraction(player.Tuning, horizontalSpeed), 2f);
            animator.SetFloat(Speed, speedParam, speedDampTime, dt);
            animator.SetFloat(VerticalSpeed, velocity.y);
            animator.SetBool(Grounded, grounded || motor.IsSliding);
            UpdateThrowPose();
            UpdateBodyPose(motor, dt);
        }

        void UpdateBodyPose(PlayerMotor motor, float dt)
        {
            if (poseRoot == null) return;
            float step = poseBlendRate * dt;
            slidePose = Mathf.MoveTowards(slidePose, motor.IsSliding ? 1f : 0f, step);
            crouchPose = Mathf.MoveTowards(crouchPose, motor.Crouched && !motor.IsSliding ? 1f : 0f, step);

            // Slide: lean back, feet first. Crouch-walk: lean in and squash (cartoon, no crouch clip).
            float pitch = -slideLeanDegrees * slidePose + crouchLeanDegrees * crouchPose;
            poseRoot.localRotation = poseBaseRotation * Quaternion.Euler(pitch, 0f, 0f);
            poseRoot.localPosition = poseBasePosition + slideOffset * slidePose;
            var scale = poseBaseScale;
            scale.y *= Mathf.Lerp(1f, crouchSquash, crouchPose);
            poseRoot.localScale = scale;
        }

        void LateUpdate()
        {
            if (player == null || player.HandAnchor == null) return;

            bool firstPersonView = FirstPersonCamera.Instance != null &&
                                   FirstPersonCamera.Instance.Target == player;
            if (firstPersonView || handSocket == null)
            {
                player.HandAnchor.localPosition = cameraHandPosition;
                player.HandAnchor.localRotation = cameraHandRotation;
                return;
            }

            // In the palm, upright: the potato follows the hand without spinning with the wrist.
            var held = handSocket.position + transform.right * heldOffset.x + transform.up * heldOffset.y + transform.forward * heldOffset.z;
            player.HandAnchor.SetPositionAndRotation(held, Quaternion.LookRotation(transform.forward, transform.up) * Quaternion.Euler(0f, heldYaw, 0f));
        }

        void TryBindBomb()
        {
            var bomb = BombController.Instance;
            if (bomb == null || bomb == boundBomb) return;

            if (boundBomb != null) boundBomb.BombThrown -= OnBombThrown;
            boundBomb = bomb;
            boundBomb.BombThrown += OnBombThrown;
        }

        void OnBombThrown(Player thrower)
        {
            // Fallback for throws started directly by gameplay/debug code. Normal player throws
            // already began their animation before the authoritative release.
            if (thrower == player && throwPhase == ThrowPhase.Idle)
            {
                BeginThrowCharge();
                ReleaseThrow();
            }
        }

        public void BeginThrowCharge()
        {
            if (animator == null || throwPhase != ThrowPhase.Idle || Time.time < throwGuardUntil) return;

            animator.SetFloat(ThrowSpeed, 1f);
            animator.ResetTrigger(Throw);
            animator.SetTrigger(Throw);
            throwPhase = ThrowPhase.Charging;
            throwGuardUntil = Time.time + player.Tuning.throwAnimationReplayGuard;
        }

        /// <summary>The potato has already left the hands: play the rest of the throw quickly as a follow-through.</summary>
        public void ReleaseThrow()
        {
            if (animator == null) return;
            if (throwPhase == ThrowPhase.Idle) BeginThrowCharge();
            animator.SetFloat(ThrowSpeed, FollowThroughSpeed);
            throwPhase = ThrowPhase.Releasing;
        }

        public void CancelThrowCharge()
        {
            if (animator == null || throwPhase == ThrowPhase.Idle) return;
            animator.SetFloat(ThrowSpeed, 1f);
            animator.CrossFade("Empty", 0.05f, ThrowLayer);
            throwPhase = ThrowPhase.Idle;
        }

        void UpdateThrowPose()
        {
            if (animator.layerCount <= ThrowLayer || throwPhase == ThrowPhase.Idle) return;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(ThrowLayer);
            if (throwPhase == ThrowPhase.Charging && state.IsName("Throw") &&
                state.normalizedTime >= player.Tuning.throwAnimationHoldNormalized)
            {
                animator.SetFloat(ThrowSpeed, 0f);
                throwPhase = ThrowPhase.Holding;
            }
            else if (throwPhase == ThrowPhase.Releasing && !animator.IsInTransition(ThrowLayer) && state.IsName("Empty"))
            {
                throwPhase = ThrowPhase.Idle;
            }
        }
    }
}
