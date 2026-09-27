using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Presentation-only bridge between the player simulation and the mannequin Animator.
    /// It also keeps the shared HandAnchor on the first-person camera for the viewed player,
    /// and between the animated hands for every third-person player.
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
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;
        [SerializeField] float speedDampTime = 0.1f;
        [SerializeField] float handForwardOffset = 0.05f;

        Player player;
        BombController boundBomb;
        Vector3 lastPosition;
        Vector3 cameraHandPosition;
        Quaternion cameraHandRotation;
        int groundMask;
        float throwGuardUntil;
        ThrowPhase throwPhase;

        void Awake()
        {
            player = GetComponent<Player>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null) animator.applyRootMotion = false;

            cameraHandPosition = player.HandAnchor.localPosition;
            cameraHandRotation = player.HandAnchor.localRotation;
            lastPosition = transform.position;
            groundMask = LayerMask.GetMask("Environment", "Hazard");
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

            float moveSpeed = Mathf.Max(0.01f, player.Tuning.moveSpeed);
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            bool grounded = player.IsLocal
                ? player.Motor.Grounded
                : Physics.CheckSphere(transform.position + Vector3.up * 0.12f, 0.22f,
                    groundMask, QueryTriggerInteraction.Ignore);

            animator.SetFloat(Speed, Mathf.Clamp01(horizontalSpeed / moveSpeed), speedDampTime, dt);
            animator.SetFloat(VerticalSpeed, velocity.y);
            animator.SetBool(Grounded, grounded);
            UpdateThrowPose();
        }

        void LateUpdate()
        {
            if (player == null || player.HandAnchor == null) return;

            bool firstPersonView = FirstPersonCamera.Instance != null &&
                                   FirstPersonCamera.Instance.Target == player;
            if (firstPersonView || leftHand == null || rightHand == null)
            {
                player.HandAnchor.localPosition = cameraHandPosition;
                player.HandAnchor.localRotation = cameraHandRotation;
                return;
            }

            Vector3 midpoint = (leftHand.position + rightHand.position) * 0.5f;
            player.HandAnchor.SetPositionAndRotation(
                midpoint + transform.forward * handForwardOffset,
                Quaternion.LookRotation(transform.forward, transform.up));
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
