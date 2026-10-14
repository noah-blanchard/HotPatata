using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A player's movement sounds (ARCHITECTURE §26, PROJECT_SPEC §11): footsteps every <see cref="GameTuning.stepLength"/>
    /// metres on the ground, by surface; a jump; a soft or hard landing; a slide's start and its loop, fading as it slows.
    /// Your own play flat and quieter (<see cref="GameTuning.ownMovementVolume"/>); teammates' play in 3D, so you hear where
    /// they are. Remote players are judged from their mirrored motion, as <see cref="PlayerAnimator"/> does. Added to every
    /// player by the <see cref="AudioDirector"/>. Presentation only: it reads the motor and never changes it.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public sealed class PlayerSounds : MonoBehaviour
    {
        const float ProbeUp = 0.3f, ProbeDown = 0.9f;     // ray from just above the feet down past the ground (metres)
        const float GroundCheckHeight = 0.12f, GroundCheckRadius = 0.22f;   // the remote ground test, as PlayerAnimator's
        const float TeleportDistance = 4f;                // further in one frame is a respawn, not a step
        const float JumpSpeed = 0.5f;                     // leaving the ground faster than this upward is a jump, not a step off
        const float SlideFadeOut = 0.15f;                 // seconds the slide loop takes to stop, never a click

        static readonly Dictionary<Collider, string> SurfaceCache = new Dictionary<Collider, string>();

        Player player;
        int groundMask;
        Vector3 lastPosition;
        bool started, wasGrounded, wasSliding;
        float stride, airTime, fallSpeed;
        LoopHandle slide;

        void Awake()
        {
            player = GetComponent<Player>();
            groundMask = LayerMask.GetMask("Environment");
        }

        void OnDisable() => slide.Stop();

        /// <summary>Surfaces are looked up once per collider; a new scene starts afresh.</summary>
        public static void ClearSurfaceCache() => SurfaceCache.Clear();

        void Update()
        {
            var director = AudioDirector.Instance;
            var motor = player.Motor;
            if (director == null || motor == null) return;
            var t = director.Tuning;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            var position = transform.position;
            if (!started || (position - lastPosition).sqrMagnitude > TeleportDistance * TeleportDistance)
            {
                started = true;
                lastPosition = position;
                wasGrounded = true;
                stride = 0f;
                return;
            }
            lastPosition = position;

            var velocity = player.Velocity;
            if (player.RemoteRiding) velocity -= player.CarrierVelocity;   // their own motion on a lift, not the lift's
            float horizontal = new Vector2(velocity.x, velocity.z).magnitude;
            bool grounded = player.IsLocal
                ? motor.Grounded
                : player.RemoteRiding || Physics.CheckSphere(position + Vector3.up * GroundCheckHeight, GroundCheckRadius,
                    groundMask, QueryTriggerInteraction.Ignore);
            bool sliding = motor.IsSliding;
            float volume = player.IsLocal ? t.ownMovementVolume : 1f;

            if (grounded && !wasGrounded)
            {
                if (airTime >= t.landingMinAirSeconds)
                    Play(director, fallSpeed >= t.hardLandingSpeed ? CueKeys.PlayerLandHard : CueKeys.PlayerLandSoft, volume);
                stride = 0f;
            }
            else if (!grounded && wasGrounded)
            {
                airTime = 0f;
                fallSpeed = 0f;
                if (velocity.y > JumpSpeed) Play(director, CueKeys.PlayerJump, volume);
            }

            if (!grounded)
            {
                airTime += dt;
                fallSpeed = Mathf.Max(fallSpeed, -velocity.y);
            }
            else if (!sliding)
            {
                stride += horizontal * dt;
                if (stride >= t.stepLength)
                {
                    stride -= t.stepLength;
                    if (stride > t.stepLength) stride = 0f;
                    Play(director, FootstepKey(director.Bank, position), volume);
                }
            }

            if (sliding && !wasSliding)
            {
                Play(director, CueKeys.PlayerSlideStart, volume);
                slide.Stop();
                slide = director.Loop(CueKeys.PlayerSlideLoop, player.IsLocal ? null : transform, volume);
            }
            else if (!sliding && wasSliding) slide.Stop(SlideFadeOut);
            if (sliding) slide.SetGain(Mathf.Clamp01(horizontal / t.slideLoopFullSpeed));

            wasGrounded = grounded;
            wasSliding = sliding;
        }

        void Play(AudioDirector director, string key, float volume)
        {
            // Your own sounds are flat (they come from you); a teammate's are placed at their feet.
            if (player.IsLocal) director.Play(key, null, volume);
            else director.Play(key, transform.position, volume);
        }

        string FootstepKey(SoundBank bank, Vector3 feet)
        {
            string surface = null;
            if (Physics.Raycast(feet + Vector3.up * ProbeUp, Vector3.down, out var hit, ProbeUp + ProbeDown, groundMask, QueryTriggerInteraction.Ignore))
                surface = SurfaceOf(hit.collider, bank);
            if (!string.IsNullOrEmpty(surface))
            {
                var key = CueKeys.Footsteps(surface);
                if (bank.Has(key)) return key;
            }
            return CueKeys.FootstepsDefault;
        }

        /// <summary>An <see cref="AudioSurface"/> on the collider wins; else the material drawn there, as the bank maps it.</summary>
        static string SurfaceOf(Collider collider, SoundBank bank)
        {
            if (SurfaceCache.TryGetValue(collider, out var cached)) return cached;
            string surface = null;
            var marker = collider.GetComponentInParent<AudioSurface>();
            if (marker != null) surface = marker.Surface;
            else
            {
                var renderer = collider.GetComponent<Renderer>();
                if (renderer == null) renderer = collider.GetComponentInChildren<Renderer>();
                if (renderer == null) renderer = collider.GetComponentInParent<Renderer>();
                if (renderer != null) surface = bank.SurfaceOf(renderer.sharedMaterial);
            }
            SurfaceCache[collider] = surface;
            return surface;
        }
    }
}
