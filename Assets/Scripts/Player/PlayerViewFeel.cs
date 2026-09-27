using System;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// The "feel" of moving in first person, computed for the local player only and applied through
    /// <see cref="PlayerLook"/> (eye pivot) and <see cref="FirstPersonCamera"/> (field of view):
    ///  - the field of view widens with speed and narrows while charging a throw;
    ///  - the view leans into strafes and turns;
    ///  - a walking bob that follows footsteps, a dip on hard landings, a lift on jumping;
    ///  - small punches when throwing and catching.
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
        float fovSmoothed, rollSmoothed, bobAmount, phase, lastYaw, fallSpeed;
        bool wasGrounded = true;
        int lastStep;

        public float FovOffset { get; private set; }
        public float Roll { get; private set; }
        public float PitchKick { get; private set; }
        public Vector3 EyeOffset { get; private set; }
        /// <summary>Horizontal speed as a fraction of run speed (0..1). Also feeds wind and post effects.</summary>
        public float SpeedFraction { get; private set; }

        /// <summary>Raised on each footfall with the current speed fraction.</summary>
        public event Action<float> Stepped;
        /// <summary>Raised on landing with an impact strength (0..1).</summary>
        public event Action<float> Landed;

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
        }

        void OnDestroy() => Unbind();

        void Unbind()
        {
            if (bomb == null) return;
            bomb.BombThrown -= OnBombThrown;
            bomb.BombCaught -= OnBombCaught;
        }

        void OnBombThrown(Player thrower)
        {
            if (thrower != player) return;
            pitchKick.Kick(2.5f);      // a little follow-through as the potato leaves the hand
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
            SpeedFraction = Mathf.Clamp01(speed / Mathf.Max(0.01f, t.moveSpeed));
            bool grounded = motor.Grounded;

            if (!IsActive)
            {
                // Not the camera's player (or a remote copy): stay neutral and keep state from drifting.
                dip.Clear(); fovImpulse.Clear(); pitchKick.Clear();
                fovSmoothed = rollSmoothed = bobAmount = 0f;
                FovOffset = Roll = PitchKick = 0f;
                EyeOffset = Vector3.zero;
                wasGrounded = grounded;
                lastYaw = player.Look.Yaw;
                return;
            }

            float strength = t.viewEffectsStrength;

            // ---- landing and take-off
            if (grounded && !wasGrounded)
            {
                float impact = Mathf.Clamp01(-fallSpeed / 18f);
                if (impact > 0.12f)
                {
                    dip.Kick(-impact * t.landingDip);
                    fovImpulse.Kick(-impact * 3f);
                    Landed?.Invoke(impact);
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
            float fovTarget = SpeedFraction * t.fovKickAtSpeed - charge * t.chargeFovZoom;
            float rate = fovTarget > fovSmoothed ? 6f : 3.5f;   // opens fast, relaxes slowly: momentum
            fovSmoothed = Mathf.Lerp(fovSmoothed, fovTarget, 1f - Mathf.Exp(-rate * dt));

            // ---- roll: lean into strafing and turning
            Vector3 right = player.Look.YawRotation * Vector3.right;
            float strafe = Vector3.Dot(v, right) / Mathf.Max(0.01f, t.moveSpeed);
            float yawRate = Mathf.DeltaAngle(lastYaw, player.Look.Yaw) / dt;
            lastYaw = player.Look.Yaw;
            float rollTarget = -strafe * t.rollDegrees + Mathf.Clamp(-yawRate * 0.008f, -1.2f, 1.2f);
            rollSmoothed = Mathf.Lerp(rollSmoothed, rollTarget, 1f - Mathf.Exp(-8f * dt));

            // ---- walking bob, in step with the footfalls
            bool moving = grounded && speed > 0.5f;
            bobAmount = Mathf.Lerp(bobAmount, moving ? 1f : 0f, 1f - Mathf.Exp(-10f * dt));
            if (moving)
            {
                phase += speed / Mathf.Max(0.3f, t.stepLength) * Mathf.PI * dt;
                int step = Mathf.FloorToInt(phase / Mathf.PI);
                if (step != lastStep)
                {
                    lastStep = step;
                    Stepped?.Invoke(SpeedFraction);
                }
            }
            float bobScale = bobAmount * Mathf.Lerp(0.4f, 1f, SpeedFraction);
            float bobX = Mathf.Sin(phase) * t.bobAmplitude * 0.6f * bobScale;
            float bobY = -Mathf.Abs(Mathf.Sin(phase)) * t.bobAmplitude * bobScale;

            dip.Step(dt);
            fovImpulse.Step(dt);
            pitchKick.Step(dt);

            FovOffset = (fovSmoothed + fovImpulse.Value) * strength;
            Roll = rollSmoothed * strength;
            PitchKick = pitchKick.Value * strength;
            EyeOffset = new Vector3(bobX, bobY + dip.Value, 0f) * strength;
        }
    }
}
