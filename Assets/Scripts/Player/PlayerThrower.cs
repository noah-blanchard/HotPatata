using System.Collections.Generic;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Turns throw input into a launch velocity and asks the bomb to release. Hold the throw button to
    /// charge (a longer hold means a faster, farther throw), release to throw; a tap is the shortest pass.
    /// It never changes bomb ownership itself: <see cref="BombController.TryThrow"/> is the authority.
    ///
    /// Aim: a ray from the eyes along the look direction picks an aim point; the throw goes from the
    /// ThrowOrigin toward it, pitched up slightly to counter drop. A small, capped assist nudges the
    /// direction toward a receiver already close to where the player is aiming.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerThrower : MonoBehaviour
    {
        Player player;
        BombController bomb;
        IReadOnlyList<Player> allPlayers;
        int aimMask;
        float chargeStartTime = -1f;
        bool throwQueued;
        bool releaseOnThisMachine;
        float queuedReleaseTime;
        Vector3 queuedOrigin;
        Vector3 queuedPlayerPosition;
        Vector3 queuedVelocity;
        float bufferedCharge = -1f;   // a release during CaughtGrace, thrown as soon as the bomb is Held

        public bool Charging => chargeStartTime >= 0f;

        /// <summary>Who the throw would be bent toward if released now (drives the on-screen lock frame).</summary>
        public Player LockTarget { get; private set; }
        public float LockQuality { get; private set; }

        /// <summary>The throw you would make if you released now (local carrier only; Valid = false otherwise).</summary>
        public ThrowShot Preview { get; private set; }
        /// <summary>The most recent throw this player released (on the machine that computed it).</summary>
        public ThrowShot LastShot { get; private set; }

        /// <summary>True while this player is the bomb's carrier (drives the charge UI).</summary>
        public bool HoldsBomb => bomb != null && bomb.Carrier == player;

        /// <summary>0 = tap, 1 = fully charged.</summary>
        public float Charge01 => Charging ? Mathf.Clamp01((Time.time - chargeStartTime) / player.Tuning.throwChargeTime) : 0f;

        void Awake()
        {
            player = GetComponent<Player>();
            aimMask = LayerMask.GetMask("Environment", "Hazard", "Player");
        }

        public void Bind(BombController bombController, IReadOnlyList<Player> players)
        {
            bomb = bombController;
            allPlayers = players;
        }

        void Update()
        {
            if (throwQueued)
            {
                bool stillValid = bomb != null && !player.ControlLocked && bomb.Carrier == player &&
                                  bomb.State == BombState.Held;
                if (!stillValid)
                {
                    throwQueued = false;
                    player.Animator?.CancelThrowCharge();
                }
                else if (releaseOnThisMachine && Time.time >= queuedReleaseTime)
                {
                    throwQueued = false;
                    Vector3 releaseOrigin = queuedOrigin + (transform.position - queuedPlayerPosition);
                    bomb.TryThrow(player, releaseOrigin, queuedVelocity);
                }
                return;
            }

            // A host also advances the queued release of a remote-owned player above.
            if (!player.IsLocal) return;
            UpdateLockPreview();

            // Always consume both edges so nothing queues up while we cannot throw.
            bool pressed = player.Input.ThrowPressed;
            bool released = player.Input.ThrowReleased;

            bool mayThrow = bomb != null && !player.ControlLocked && bomb.Carrier == player
                            && (bomb.State == BombState.Held || bomb.State == BombState.CaughtGrace);
            if (!mayThrow)
            {
                if (Charging || throwQueued) CancelCharge();
                return;
            }

            if (pressed)
            {
                chargeStartTime = Time.time;
                player.Animator?.BeginThrowCharge();
                if (player.Net != null && player.Net.IsSpawned)
                {
                    if (NetMode.IsRemoteClient) player.Net.RequestThrowCharge();
                    else player.Net.BroadcastThrowCharge();
                }
            }

            if (released && Charging)
            {
                // Throws only leave from Held (spec §5); a release during the short catch grace is kept, not lost.
                bufferedCharge = Charge01;   // read before clearing chargeStartTime, which zeroes Charge01
                chargeStartTime = -1f;
            }

            if (bufferedCharge >= 0f && bomb.State == BombState.Held)
            {
                float charge = bufferedCharge;
                bufferedCharge = -1f;
                TryThrow(charge);
            }
        }

        public void CancelCharge()
        {
            chargeStartTime = -1f;
            throwQueued = false;
            player.Animator?.CancelThrowCharge();
            bufferedCharge = -1f;
        }

        void UpdateLockPreview()
        {
            LockTarget = null;
            LockQuality = 0f;
            Preview = default;
            if (bomb == null || bomb.Carrier != player || allPlayers == null) return;

            Vector3 origin = ThrowOriginNow();
            Preview = ComputeShot(origin, Charge01);
            Vector3 velocity = ComputeThrowVelocity(origin, 0f);   // direction is the same for every charge
            if (HomingTargeting.TryPick(player.Tuning, origin, velocity, player, allPlayers, out var target, out _, out float quality))
            {
                LockTarget = target;
                LockQuality = quality;
            }
        }

        public bool TryThrow(float charge01 = 0f)
        {
            if (throwQueued || bomb == null || bomb.State != BombState.Held || bomb.Carrier != player) return false;

            Vector3 origin = ThrowOriginNow();
            var shot = ComputeShot(origin, charge01);
            LastShot = shot;
            Vector3 velocity = shot.Velocity;

            player.Animator?.ReleaseThrow();
            throwQueued = true;
            releaseOnThisMachine = !NetMode.IsRemoteClient;
            queuedReleaseTime = Time.time + (player.Animator != null ? player.Animator.ThrowReleaseDelay : 0f);
            queuedOrigin = origin;
            queuedPlayerPosition = transform.position;
            queuedVelocity = velocity;

            if (NetMode.IsRemoteClient)
            {
                // The owner begins the wind-up immediately. The host mirrors it to the other
                // machines, waits for the hand-release frame, then performs the real throw.
                player.Net.RequestThrow(origin, velocity);
                return true;
            }

            if (player.Net != null && player.Net.IsSpawned) player.Net.BroadcastThrowRelease();
            return true;
        }

        /// <summary>Host only: validate first, then queue the authoritative release on the animation frame.</summary>
        public bool QueueNetworkThrow(Vector3 origin, Vector3 velocity)
        {
            if (throwQueued || bomb == null || bomb.State != BombState.Held || bomb.Carrier != player) return false;

            player.Animator?.ReleaseThrow();
            throwQueued = true;
            releaseOnThisMachine = true;
            queuedReleaseTime = Time.time + (player.Animator != null ? player.Animator.ThrowReleaseDelay : 0f);
            queuedOrigin = origin;
            queuedPlayerPosition = transform.position;
            queuedVelocity = velocity;
            player.Net.BroadcastThrowRelease();
            return true;
        }

        /// <summary>Throw origin from the current aim (the anchor transform only updates in LateUpdate).</summary>
        public Vector3 ThrowOriginNow() =>
            player.CameraTarget.position + player.Look.AimRotation * player.ThrowOrigin.localPosition;

        public static float SpeedFor(GameTuning t, float charge01) =>
            Mathf.Lerp(t.throwSpeedMin, t.throwSpeedMax, Mathf.Clamp01(charge01));

        /// <summary>Deterministic: the same aim, charge and tuning always give the same velocity.</summary>
        public Vector3 ComputeThrowVelocity(Vector3 origin, float charge01) => ComputeShot(origin, charge01).Velocity;

        /// <summary>The full throw for this aim and charge: raw velocity, assisted velocity and the assist's reasons.</summary>
        public ThrowShot ComputeShot(Vector3 origin, float charge01)
        {
            var t = player.Tuning;
            float speed = SpeedFor(t, charge01);
            var shot = new ThrowShot { Valid = true, Charge01 = charge01, Speed = speed, Origin = origin };

            // 1. Where is the player aiming?
            Vector3 pivot = player.CameraTarget.position;
            Vector3 forward = player.Look.AimRotation * Vector3.forward;
            shot.AimForward = forward;
            Vector3 aimPoint = Physics.Raycast(pivot, forward, out var hit, t.aimMaxDistance, aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : pivot + forward * t.aimMaxDistance;

            Vector3 dir = aimPoint - origin;
            dir = dir.sqrMagnitude < 1f ? forward : dir.normalized;

            // 2. Pitch up a little to counter drop.
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            if (right.sqrMagnitude > 1e-4f)
                dir = Quaternion.AngleAxis(-t.throwUpAngle, right.normalized) * dir;
            shot.RawVelocity = dir * speed;

            // 3. Optional small assist toward a nearby receiver.
            Vector3 assisted = ApplyAimAssist(origin, dir, speed, t, ref shot);
            shot.AssistCorrection = Vector3.Angle(dir, assisted);
            shot.Velocity = assisted * speed;
            return shot;
        }

        Vector3 ApplyAimAssist(Vector3 origin, Vector3 dir, float speed, GameTuning t, ref ThrowShot shot)
        {
            if (allPlayers == null || t.aimAssistStrength <= 0f) return dir;

            float g = ThrowBallistics.Gravity(t);
            Vector3? best = null;
            float bestAngle = t.aimAssistAngle;

            foreach (var other in allPlayers)
            {
                if (other == null || other == player || other.CatchVolume == null) continue;

                Vector3 to = other.CatchVolume.CatchCenter - origin;
                if (to.magnitude > t.aimAssistDistance) continue;
                if (!ThrowBallistics.TrySolveLowArc(to, speed, g, out Vector3 solved, out _)) continue;

                float angle = Vector3.Angle(dir, solved);
                if (angle <= bestAngle)
                {
                    bestAngle = angle;
                    best = solved;
                    shot.AssistTarget = other;
                    shot.AssistAngle = angle;
                }
            }

            return best.HasValue ? Vector3.Slerp(dir, best.Value, t.aimAssistStrength).normalized : dir;
        }
    }
}
