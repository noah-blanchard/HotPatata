using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The "feel" of moving in first person, computed for the local player only and applied through
    /// <see cref="PlayerLook"/> (eye pivot) and <see cref="FirstPersonCamera"/> (field of view):
    ///  - the field of view widens with speed (run, more at sprint, most in a slide) and narrows while charging a throw;
    ///  - the view leans into strafes and turns, and rolls into a slide with a light rumble;
    ///  - a foot-plant bob (a quick dip on every footfall) in step with the footstep sounds, a dip on hard landings;
    ///  - small punches when throwing and catching, and a shake when the bomb explodes nearby.
    /// It only reads movement state, never changes it, and everything scales with
    /// <see cref="GameTuning.viewEffectsStrength"/> so it can be dialled down or off.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerViewFeel : MonoBehaviour
    {
        /// <summary>Critically-ish damped spring: kicks decay into a smooth wobble.</summary>
        struct Spring
        {
            public float Value, Velocity;

            public void Step(float dt, float stiffness = 180f, float damping = 16f)
            {
                Velocity += (-stiffness * Value - damping * Velocity) * dt;
                Value += Velocity * dt;
            }

            /// <summary>Kick so the peak displacement is about <paramref name="amount"/>.</summary>
            public void Kick(float amount) => Velocity += amount * Mathf.Sqrt(180f);

            public void Clear() => Value = Velocity = 0f;
        }

        Player player;
        PlayerMotor motor;
        BombController bomb;
        Spring dip, fovImpulse, pitchKick;
        float fovSmoothed, rollSmoothed, bobAmount, phase, lastYaw, fallSpeed, slideAmount, shake, shakeSeed;
        bool wasGrounded = true;

        public float FovOffset { get; private set; }
        public float Roll { get; private set; }
        public float PitchKick { get; private set; }
        public Vector3 EyeOffset { get; private set; }
        /// <summary>Horizontal speed as a fraction of run speed (0..1). Also feeds wind and post effects.</summary>
        public float SpeedFraction { get; private set; }
        /// <summary>Unclamped: run = 1, sprint = 2, slides beyond (<see cref="PlayerMotor.MotionFraction"/>).</summary>
        public float MotionFraction { get; private set; }

        bool IsActive => FirstPersonCamera.Instance != null && FirstPersonCamera.Instance.Target == player && player.IsLocal;

        void Awake()
        {
            player = GetComponent<Player>();
            motor = GetComponent<PlayerMotor>();
        }

        /// <summary>Called by <see cref="Player"/> once the bomb exists.</summary>
        public void Bind(BombController bombController)
        {
            Unbind();
            bomb = bombController;
            bomb.BombThrown += OnBombThrown;
            bomb.BombCaught += OnBombCaught;
            bomb.BombExploded += OnBombExploded;
        }

        void OnDestroy() => Unbind();

        void Unbind()
        {
            if (bomb == null) return;
            bomb.BombThrown -= OnBombThrown;
            bomb.BombCaught -= OnBombCaught;
            bomb.BombExploded -= OnBombExploded;
        }

        void OnBombExploded(BombFailReason reason, string detail)
        {
            if (!IsActive) return;
            // Stronger the closer you are; nothing beyond ~16 m.
            float distance = Vector3.Distance(bomb.transform.position, player.CameraTarget.position);
            float k = Mathf.Clamp01(1f - distance / 16f);
            shake = Mathf.Max(shake, k * k * player.Tuning.explosionShake);
            shakeSeed = UnityEngine.Random.value * 100f;
        }

        /// <summary>FOV kick for a speed on the run = 1, sprint = 2 scale: run kick, blending to sprint, then to the max.</summary>
        static float FovKick(GameTuning t, float motion)
        {
            if (motion <= 1f) return motion * t.fovKickAtSpeed;
            if (motion <= 2f) return Mathf.Lerp(t.fovKickAtSpeed, t.fovKickAtSprint, motion - 1f);
            return Mathf.Lerp(t.fovKickAtSprint, t.fovKickMax, Mathf.Clamp01(motion - 2f));
        }

        void OnBombThrown(Player thrower)
        {
            if (thrower != player) return;
            pitchKick.Kick(1.2f);      // a small follow-through as the potato leaves the hand (kept small: it moves the crosshair)
            fovImpulse.Kick(3f);
        }

        void OnBombCaught(Player receiver)
        {
            if (receiver != player) return;
            fovImpulse.Kick(2.5f);     // a satisfying thump when it lands in the hand
            dip.Kick(-0.05f);
            pitchKick.Kick(1.2f);
        }

        void Update()
        {
            var t = player.Tuning;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 v = motor.Velocity;
            float speed = new Vector2(v.x, v.z).magnitude;
            MotionFraction = PlayerMotor.MotionFraction(t, speed);
            SpeedFraction = Mathf.Clamp01(MotionFraction);
            bool grounded = motor.Grounded;

            if (!IsActive)
            {
                // Not the camera's player (or a remote copy): stay neutral and keep state from drifting.
                dip.Clear(); fovImpulse.Clear(); pitchKick.Clear();
                fovSmoothed = rollSmoothed = bobAmount = slideAmount = shake = 0f;
                FovOffset = Roll = PitchKick = 0f;
                EyeOffset = Vector3.zero;
                wasGrounded = grounded;
                lastYaw = player.Look.Yaw;
                return;
            }

            float strength = Settings.ViewEffectsStrength(t);

            // ---- landing and take-off
            if (grounded && !wasGrounded)
            {
                float impact = Mathf.Clamp01(-fallSpeed / 18f);
                if (impact > 0.12f)
                {
                    dip.Kick(-impact * t.landingDip * 1.3f);
                    fovImpulse.Kick(-impact * 3f);
                }
            }
            else if (!grounded && wasGrounded && v.y > 1f)
            {
                dip.Kick(0.02f);
                fovImpulse.Kick(1.5f);
            }
            fallSpeed = grounded ? 0f : Mathf.Min(fallSpeed, v.y);
            wasGrounded = grounded;

            // ---- field of view: wider with speed, tighter while charging, punchy on events
            float charge = player.Thrower != null && player.Thrower.Charging ? player.Thrower.Charge01 : 0f;
            float fovTarget = FovKick(t, MotionFraction) - charge * t.chargeFovZoom;
            float rate = fovTarget > fovSmoothed ? 6f : 3.5f;   // opens fast, relaxes slowly: momentum
            fovSmoothed = Mathf.Lerp(fovSmoothed, fovTarget, 1f - Mathf.Exp(-rate * dt));

            // ---- roll: lean into strafing and turning
            Vector3 right = player.Look.YawRotation * Vector3.right;
            float strafe = Mathf.Clamp(Vector3.Dot(v, right) / Mathf.Max(0.01f, t.moveSpeed), -1.5f, 1.5f);
            float yawRate = Mathf.DeltaAngle(lastYaw, player.Look.Yaw) / dt;
            lastYaw = player.Look.Yaw;
            float rollTarget = -strafe * t.rollDegrees + Mathf.Clamp(-yawRate * 0.008f, -1.2f, 1.2f);
            // Sliding: roll into the slide (toward the side you steer, a little to the right by default).
            slideAmount = Mathf.MoveTowards(slideAmount, motor.IsSliding ? 1f : 0f, 6f * dt);
            float slideSide = strafe > 0.1f ? 1f : strafe < -0.1f ? -1f : 0.6f;
            rollTarget -= slideAmount * slideSide * t.slideRollDegrees;
            rollSmoothed = Mathf.Lerp(rollSmoothed, rollTarget, 1f - Mathf.Exp(-8f * dt));

            // ---- shake: a slide rumbles with speed, an explosion jolts and decays
            shake = Mathf.MoveTowards(shake, 0f, dt * 4f * Mathf.Max(0.5f, shake));
            float rumble = slideAmount * Mathf.Clamp01(MotionFraction - 1f) * 0.25f;
            float amplitude = shake + rumble;
            float shakeT = Time.time * 28f + shakeSeed;
            float shakePitch = (Mathf.PerlinNoise(shakeT, 0.3f) - 0.5f) * 2f * amplitude;
            float shakeRoll = (Mathf.PerlinNoise(0.7f, shakeT) - 0.5f) * 2f * amplitude;

            // ---- foot-plant bob (no bob while sliding)
            bool moving = grounded && speed > 0.5f && !motor.IsSliding;
            bobAmount = Mathf.Lerp(bobAmount, moving ? 1f : 0f, 1f - Mathf.Exp(-10f * dt));
            if (moving)
            {
                // Sprinting lengthens the stride, so the rhythm does not turn into a frantic patter.
                float stride = t.stepLength * Mathf.Lerp(1f, 1.3f, Mathf.Clamp01(MotionFraction - 1f));
                phase += speed / Mathf.Max(0.3f, stride) * Mathf.PI * dt;
            }
            float bobScale = bobAmount * Mathf.Lerp(0.4f, 1f, SpeedFraction);
            // A quick dip as each foot lands (phase = k*pi), easing back up between steps; a small lateral sway.
            float plant = 1f - Mathf.Abs(Mathf.Sin(phase));
            float bobY = -plant * plant * t.bobAmplitude * 1.4f * bobScale;
            float bobX = Mathf.Sin(phase) * t.bobAmplitude * 0.35f * bobScale;

            dip.Step(dt);
            fovImpulse.Step(dt);
            pitchKick.Step(dt);

            FovOffset = (fovSmoothed + fovImpulse.Value) * strength;
            Roll = (rollSmoothed + shakeRoll) * strength;
            PitchKick = (pitchKick.Value + shakePitch) * strength;
            EyeOffset = new Vector3(bobX, bobY + dip.Value, 0f) * strength;
        }
    }
}
