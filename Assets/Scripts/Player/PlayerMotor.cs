using System;
using UnityEngine;

namespace HotPatata
{
    public enum MoveState : byte
    {
        Ground,
        Air,
        Slide,
        Mantle
    }

    /// <summary>
    /// CharacterController-based first-person motor with an explicit state (<see cref="MoveState"/>):
    ///  - Ground: the run direction carves toward the stick instead of snapping; sprint (hold) raises the target speed;
    ///    speed above the target (after a slide, letting go of sprint) bleeds off gently, so momentum carries.
    ///  - Air: a jump keeps its momentum; moderate steering, no bunny-hop gain.
    ///  - Slide: crouch at speed; a boost that decays, faster downhill, steerable a little; a jump out keeps the speed.
    ///  - Mantle: moving into a chest-high ledge while airborne climbs it.
    /// Single jump with coyote time and jump buffer. Knows nothing about the bomb.
    ///
    /// Remote copies do not simulate: they apply the owner's replicated state (<see cref="PackedState"/>) so their
    /// capsule and catch height match the owner's.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Player))]
    public class PlayerMotor : MonoBehaviour
    {
        const float GroundStick = 2f;   // small downward push so isGrounded stays reliable
        const float Never = -999f;
        const float MantleRetryDelay = 0.3f;
        const float MantleWallRayHeight = 0.05f;   // just above the feet, below the lowest ledge a mantle is for

        Player player;
        CharacterController controller;
        int environmentMask, hazardLayer;

        Vector3 horizontalVelocity;
        float verticalVelocity;
        float lastGroundedTime = Never;
        float lastJumpPressTime = Never;
        float lastSlideBoostTime = Never;
        float lastMantleTime = Never;
        bool crouchWasHeld, wasGrounded;
        IPlatformCarrier ridingPlatform;   // carrier (moving platform, belt) we stood on during the last Move
        Vector3 groundNormal = Vector3.up;

        float standHeight;
        float centerOffset;   // controller centre minus half its height (the pivot is at the feet)

        Vector3 mantleStart, mantleEnd, mantleForward;
        float mantleProgress, mantleExitSpeed;

        public bool Grounded { get; private set; }
        /// <summary>The carrier this player stood on during this frame's move (null when airborne or on plain ground). Owner only.</summary>
        public IPlatformCarrier RidingCarrier => Grounded && ridingPlatform is UnityEngine.Object c && c != null ? ridingPlatform : null;
        public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;
        public MoveState State { get; private set; } = MoveState.Air;
        /// <summary>The capsule is short: sliding, crouch-walking, or kept low by a ceiling.</summary>
        public bool Crouched { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsSliding => State == MoveState.Slide;
        /// <summary>0 standing .. 1 crouched, smoothed. Drives the eye and catch height on every machine.</summary>
        public float CrouchAmount { get; private set; }
        /// <summary>Body height as a fraction of standing height (1 standing, crouchHeight/standing when crouched), smoothed.</summary>
        public float HeightScale => Mathf.Lerp(1f, CrouchedScale, CrouchAmount);
        float CrouchedScale => standHeight > 0f ? Mathf.Clamp01(player.Tuning.crouchHeight / standHeight) : 1f;

        public event Action SlideStarted;
        public event Action Mantled;

        /// <summary>
        /// Speed on a scale where run speed is 1 and sprint speed is 2 (slides go beyond). Presentation uses it so
        /// sprinting and sliding read as faster than running instead of saturating at run speed.
        /// </summary>
        public static float MotionFraction(GameTuning t, float horizontalSpeed)
        {
            if (horizontalSpeed <= t.moveSpeed) return horizontalSpeed / Mathf.Max(0.01f, t.moveSpeed);
            return 1f + (horizontalSpeed - t.moveSpeed) / Mathf.Max(0.01f, t.sprintSpeed - t.moveSpeed);
        }

        /// <summary>State, crouch and sprint in one byte, replicated from the owner (see <see cref="NetworkPlayer"/>).</summary>
        public byte PackedState => (byte)((byte)State | (Crouched ? 4 : 0) | (IsSprinting ? 8 : 0));

        void Awake()
        {
            player = GetComponent<Player>();
            controller = GetComponent<CharacterController>();
            standHeight = controller.height;
            centerOffset = controller.center.y - standHeight * 0.5f;
            environmentMask = LayerMask.GetMask("Environment", "Hazard");
            hazardLayer = LayerMask.NameToLayer("Hazard");
        }

        void Update()
        {
            var t = player.Tuning;
            float dt = Time.deltaTime;

            if (!player.IsLocal)
            {
                // Remote copies are moved by their NetworkTransform; only mirror the posture.
                if (player.Net != null) ApplyPackedState(player.Net.RemoteMoveState);
                SmoothCrouch(t, dt);
                return;
            }

            bool locked = player.ControlLocked;
            var input = player.Input;

            // First person: the body always faces the way you are looking.
            transform.rotation = player.Look.YawRotation;

            Vector2 stick = locked ? Vector2.zero : Vector2.ClampMagnitude(input.Move, 1f);
            bool sprintHeld = !locked && input.SprintHeld;
            bool crouchHeld = !locked && input.CrouchHeld;
            bool crouchPressed = crouchHeld && !crouchWasHeld;
            crouchWasHeld = crouchHeld;
            if (!locked && input.JumpPressed) lastJumpPressTime = Time.time;

            if (State == MoveState.Mantle)
            {
                UpdateMantle(t, dt);
                SmoothCrouch(t, dt);
                return;
            }

            Vector3 wish = player.Look.YawRotation * new Vector3(stick.x, 0f, stick.y);
            float wishAmount = wish.magnitude;
            Vector3 wishDir = wishAmount > 1e-4f ? wish / wishAmount : Vector3.zero;
            float speed = horizontalVelocity.magnitude;

            // --- State from the last Move -------------------------------------------------------
            bool landed = Grounded && !wasGrounded;
            wasGrounded = Grounded;
            if (!Grounded && State != MoveState.Air) State = MoveState.Air;
            else if (Grounded && State == MoveState.Air) State = MoveState.Ground;

            // --- Slide: crouch at speed (also straight out of a landing with crouch held) ---------
            if (State == MoveState.Ground && speed >= t.slideMinEntrySpeed && (crouchPressed || (landed && crouchHeld)))
                StartSlide(t, ref speed);
            else if (State == MoveState.Slide && (!crouchHeld || speed < t.slideExitSpeed))
                State = MoveState.Ground;   // stand up (or crouch-walk if crouch is still held)

            // --- Posture: low while sliding or crouching, and while a ceiling is in the way -----
            bool wantCrouch = crouchHeld || State == MoveState.Slide;
            if (wantCrouch && !Crouched) SetCrouched(true);
            else if (!wantCrouch && Crouched && CanStand(t)) SetCrouched(false);

            IsSprinting = sprintHeld && !Crouched && State != MoveState.Slide && wishAmount > 0.1f &&
                          stick.y >= t.sprintForwardDot * stick.magnitude;

            // --- Horizontal -----------------------------------------------------------------------
            if (State == MoveState.Slide)
            {
                horizontalVelocity = SlideStep(t, dt, wishDir, wishAmount > 0.1f);
            }
            else
            {
                bool onGround = State == MoveState.Ground;
                float target = onGround && Crouched ? t.crouchSpeed : IsSprinting ? t.sprintSpeed : t.moveSpeed;
                horizontalVelocity = Steer(t, dt, horizontalVelocity, wishDir, wishAmount * target, onGround);
            }

            // --- Jump: buffer the press, allow it within the coyote window ---------------------
            bool buffered = Time.time - lastJumpPressTime <= t.jumpBuffer;
            bool coyote = Time.time - lastGroundedTime <= t.coyoteTime;
            if (buffered && coyote)
            {
                // A jump keeps all horizontal speed: a slide-jump carries the slide's momentum.
                verticalVelocity = Mathf.Sqrt(2f * t.gravity * t.jumpHeight);
                lastJumpPressTime = Never;
                lastGroundedTime = Never;   // consume coyote so we cannot double-jump
                Grounded = false;
                wasGrounded = false;
                State = MoveState.Air;
            }

            // --- Vertical -----------------------------------------------------------------------
            if (Grounded && verticalVelocity < 0f) verticalVelocity = -(GroundStick + DownhillSink());
            else verticalVelocity -= t.gravity * dt;

            // --- Mantle: moving into a chest-high ledge while airborne climbs it ----------------
            if (State == MoveState.Air && !locked && stick.y > 0.3f && TryStartMantle(t))
            {
                SmoothCrouch(t, dt);
                return;
            }

            // --- Move ---------------------------------------------------------------------------
            // Ride a carrier (moving platform, belt): add the distance it moved this frame (it updates before us).
            Vector3 carry = ridingPlatform is UnityEngine.Object c && c != null ? ridingPlatform.FrameDelta : Vector3.zero;
            ridingPlatform = null;   // OnControllerColliderHit sets it again if we are still on one
            var flags = controller.Move(Velocity * dt + carry);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

            Grounded = controller.isGrounded;
            if (Grounded) lastGroundedTime = Time.time;
            ProbeGround();
            SmoothCrouch(t, dt);
        }

        // ------------------------------------------------------------------ horizontal models

        /// <summary>
        /// Carving movement: the direction swings toward the stick at a speed-dependent turn rate while the speed
        /// settles toward the target. Sharp reversals brake through zero instead of making a wide U-turn.
        /// </summary>
        static Vector3 Steer(GameTuning t, float dt, Vector3 v, Vector3 wishDir, float targetSpeed, bool grounded)
        {
            float control = grounded ? 1f : t.airControl;

            if (targetSpeed < 0.01f)
            {
                // No input: brake hard on the ground, keep the jump's momentum in the air.
                return Vector3.MoveTowards(v, Vector3.zero, (grounded ? t.braking : t.airDrag) * dt);
            }

            float speed = v.magnitude;
            Vector3 wishVelocity = wishDir * targetSpeed;
            if (speed < 0.05f)
                return Vector3.MoveTowards(v, wishVelocity, StartRate(t, speed) * control * dt);

            Vector3 dir = v / speed;
            if (Vector3.Angle(dir, wishDir) > t.reverseAngle)
                return Vector3.MoveTowards(v, wishVelocity, t.braking * control * dt);

            float turnRate = grounded
                ? Mathf.Lerp(t.groundTurnRate, t.sprintTurnRate, Mathf.InverseLerp(t.moveSpeed * 0.5f, t.sprintSpeed, speed))
                : t.airTurnRate;
            dir = Vector3.RotateTowards(dir, wishDir, turnRate * Mathf.Deg2Rad * dt, 0f);

            float newSpeed;
            if (speed < targetSpeed)
            {
                float rate = speed < t.moveSpeed ? StartRate(t, speed) : t.sprintAcceleration;
                newSpeed = Mathf.MoveTowards(speed, targetSpeed, rate * control * dt);
            }
            else
            {
                float decel = grounded ? t.overspeedDeceleration : speed > t.sprintSpeed ? t.airOverspeedDrag : 0f;
                newSpeed = Mathf.MoveTowards(speed, targetSpeed, decel * dt);
            }
            return dir * newSpeed;
        }

        /// <summary>Progressive start: gentler off the mark, full strength once we are moving.</summary>
        static float StartRate(GameTuning t, float speed)
        {
            float build = Mathf.Clamp01(speed / Mathf.Max(0.01f, t.moveSpeed * 0.5f));
            return t.acceleration * Mathf.Lerp(t.startAccelerationMultiplier, 1f, build);
        }

        void StartSlide(GameTuning t, ref float speed)
        {
            State = MoveState.Slide;
            if (Time.time - lastSlideBoostTime >= t.slideBoostCooldown)
            {
                lastSlideBoostTime = Time.time;
                // The boost tops out at sprint speed + boost: chaining slides and jumps cannot stack speed.
                speed = Mathf.Max(speed, Mathf.Min(speed + t.slideBoost, t.sprintSpeed + t.slideBoost, t.slideMaxSpeed));
                horizontalVelocity = horizontalVelocity.normalized * speed;
            }
            SlideStarted?.Invoke();
        }

        Vector3 SlideStep(GameTuning t, float dt, Vector3 wishDir, bool steering)
        {
            float speed = horizontalVelocity.magnitude;
            Vector3 dir = speed > 1e-4f ? horizontalVelocity / speed : transform.forward;
            if (steering && Vector3.Angle(dir, wishDir) < 100f)
                dir = Vector3.RotateTowards(dir, wishDir, t.slideSteerRate * Mathf.Deg2Rad * dt, 0f);

            // Gravity along the slope: downhill speeds the slide up, uphill slows it down.
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
            Vector3 slope = new Vector3(downhill.x, 0f, downhill.z) * (t.gravity * t.slideSlopeAcceleration);
            Vector3 v = dir * speed + slope * dt;

            speed = Mathf.Clamp(v.magnitude - t.slideFriction * dt, 0f, t.slideMaxSpeed);
            return v.sqrMagnitude > 1e-6f ? v.normalized * speed : Vector3.zero;
        }

        /// <summary>Extra downward speed that keeps a fast run or slide glued to a downhill slope.</summary>
        float DownhillSink()
        {
            if (groundNormal.y > 0.999f || groundNormal.y < 0.1f) return 0f;
            var flatDown = new Vector3(groundNormal.x, 0f, groundNormal.z);   // points downhill
            float tan = flatDown.magnitude / groundNormal.y;
            return Mathf.Max(0f, Vector3.Dot(horizontalVelocity, flatDown.normalized)) * tan;
        }

        void ProbeGround()
        {
            groundNormal = Vector3.up;
            if (Grounded && Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.6f,
                    environmentMask, QueryTriggerInteraction.Ignore))
                groundNormal = hit.normal;
        }

        // ------------------------------------------------------------------ posture

        void SetCrouched(bool crouched)
        {
            Crouched = crouched;
            ApplyCapsule();
        }

        void ApplyCapsule()
        {
            // The pivot stays at the feet: crouching lowers the top of the capsule, never lifts the feet.
            float height = Crouched ? Mathf.Min(standHeight, player.Tuning.crouchHeight) : standHeight;
            controller.height = height;
            controller.center = new Vector3(controller.center.x, height * 0.5f + centerOffset, controller.center.z);
        }

        bool CanStand(GameTuning t)
        {
            float r = controller.radius;
            Vector3 feet = transform.position;
            Vector3 low = feet + Vector3.up * (Mathf.Min(standHeight, t.crouchHeight) - r);
            Vector3 high = feet + Vector3.up * (standHeight - r);
            return !Physics.CheckCapsule(low, high, r * 0.95f, environmentMask, QueryTriggerInteraction.Ignore);
        }

        void SmoothCrouch(GameTuning t, float dt)
        {
            CrouchAmount = Mathf.MoveTowards(CrouchAmount, Crouched ? 1f : 0f, t.crouchTransitionRate * dt);
        }

        void ApplyPackedState(byte packed)
        {
            State = (MoveState)(packed & 3);
            IsSprinting = (packed & 8) != 0;
            bool crouched = (packed & 4) != 0;
            if (crouched != Crouched) SetCrouched(crouched);
        }

        // ------------------------------------------------------------------ mantle

        bool TryStartMantle(GameTuning t)
        {
            if (Time.time - lastMantleTime < MantleRetryDelay) return false;

            Vector3 forward = player.Look.YawRotation * Vector3.forward;
            if (Vector3.Dot(horizontalVelocity, forward) < -0.5f) return false;   // moving away from the ledge

            float r = controller.radius;
            Vector3 feet = transform.position;

            // 1. A wall face right in front, low enough that it is a ledge and not the ground.
            Vector3 wallOrigin = feet + Vector3.up * MantleWallRayHeight;
            if (!Physics.Raycast(wallOrigin, forward, out var wall, r + t.mantleReach, environmentMask,
                    QueryTriggerInteraction.Ignore) || Mathf.Abs(wall.normal.y) > 0.35f)
                return false;

            // 2. Its top, within climbing height, flat, and not a hazard.
            float top = t.mantleMaxHeight + 0.05f;
            Vector3 downOrigin = wall.point + forward * 0.15f;
            downOrigin.y = feet.y + top;
            if (!Physics.Raycast(downOrigin, Vector3.down, out var ledge, top - MantleWallRayHeight, environmentMask,
                    QueryTriggerInteraction.Ignore) || ledge.normal.y < 0.7f || ledge.collider.gameObject.layer == hazardLayer)
                return false;
            float height = ledge.point.y - feet.y;
            if (height < t.mantleMinHeight || height > t.mantleMaxHeight) return false;

            // A jump that is still rising enough to clear the ledge does not need (and must not be cut short by) a mantle.
            float riseLeft = verticalVelocity > 0f ? verticalVelocity * verticalVelocity / (2f * Mathf.Max(0.01f, t.gravity)) : 0f;
            if (riseLeft > height + 0.1f) return false;

            // 3. Room to rise in place and to stand on top.
            float h = controller.height;
            Vector3 end = ledge.point + forward * (r * 0.6f) + Vector3.up * 0.02f;
            if (Blocked(feet + Vector3.up * height, h, r) || Blocked(end, h, r)) return false;

            State = MoveState.Mantle;
            mantleStart = feet;
            mantleEnd = end;
            mantleForward = forward;
            mantleProgress = 0f;
            mantleExitSpeed = Mathf.Max(Vector3.Dot(horizontalVelocity, forward), t.moveSpeed * 0.6f);
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            lastMantleTime = Time.time;
            Mantled?.Invoke();
            return true;
        }

        bool Blocked(Vector3 feet, float height, float r) =>
            Physics.CheckCapsule(feet + Vector3.up * (r + 0.05f), feet + Vector3.up * (height - r), r * 0.95f,
                environmentMask, QueryTriggerInteraction.Ignore);

        void UpdateMantle(GameTuning t, float dt)
        {
            mantleProgress = Mathf.Clamp01(mantleProgress + dt / Mathf.Max(0.01f, t.mantleDuration));
            float k = mantleProgress;
            // Rise first (ease out), then pull forward over the edge.
            float rise = 1f - (1f - Mathf.Clamp01(k / 0.7f)) * (1f - Mathf.Clamp01(k / 0.7f));
            float over = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.35f) / 0.65f));
            var desired = new Vector3(
                Mathf.Lerp(mantleStart.x, mantleEnd.x, over),
                Mathf.Lerp(mantleStart.y, mantleEnd.y, rise),
                Mathf.Lerp(mantleStart.z, mantleEnd.z, over));
            controller.Move(desired - transform.position);

            if (k >= 1f)
            {
                State = MoveState.Air;   // the next Move finds the ledge and lands
                horizontalVelocity = mantleForward * mantleExitSpeed;
                verticalVelocity = 0f;
                Grounded = false;
                wasGrounded = false;
            }
        }

        // ------------------------------------------------------------------ external

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.5f && hit.collider.GetComponentInParent<IPlatformCarrier>() is IPlatformCarrier carrier)
                ridingPlatform = carrier;
        }

        /// <summary>Throws the player upward (launch pads). Overrides any current vertical motion.</summary>
        public void Launch(float upSpeed)
        {
            verticalVelocity = upSpeed;
            Grounded = false;
            wasGrounded = false;
            State = MoveState.Air;
            lastGroundedTime = Never;
            lastJumpPressTime = Never;
        }

        public void ResetVelocity()
        {
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            lastJumpPressTime = Never;
            if (State == MoveState.Mantle || State == MoveState.Slide) State = Grounded ? MoveState.Ground : MoveState.Air;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            // CharacterController overrides transform writes unless it is disabled around them.
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            ResetVelocity();
            ridingPlatform = null;
            Grounded = false;
            wasGrounded = false;
            State = MoveState.Air;
            if (Crouched) SetCrouched(false);
            CrouchAmount = 0f;
            IsSprinting = false;
        }
    }
}
