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
        int sightMask;   // what blocks the assist's line of sight to a receiver
        const float RequestTimeout = 1f;   // seconds a remote client waits for the host to act on its throw request

        float chargeStartTime = -1f;
        float requestPendingUntil = -1f;   // remote client: a throw request is on its way to the host
        float bufferedCharge = -1f;   // a release during CaughtGrace, thrown as soon as the bomb is Held

        public bool Charging => chargeStartTime >= 0f;

        /// <summary>The throw you would make if you released now (local carrier only; Valid = false otherwise).
        /// Its assist target drives the on-screen lock brackets.</summary>
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
            sightMask = LayerMask.GetMask("Environment", "Hazard");
        }

        public void Bind(BombController bombController, IReadOnlyList<Player> players)
        {
            bomb = bombController;
            allPlayers = players;
        }

        bool RequestPending => requestPendingUntil >= 0f && Time.time < requestPendingUntil &&
                               bomb != null && bomb.Carrier == player && bomb.State == BombState.Held;

        void Update()
        {
            if (!player.IsLocal) return;
            if (!RequestPending) requestPendingUntil = -1f;
            UpdateLockPreview();

            // Always consume both edges so nothing queues up while we cannot throw.
            bool pressed = player.Input.ThrowPressed;
            bool released = player.Input.ThrowReleased;

            bool mayThrow = bomb != null && !player.ControlLocked && bomb.Carrier == player
                            && (bomb.State == BombState.Held || bomb.State == BombState.CaughtGrace);
            if (!mayThrow)
            {
                if (Charging || bufferedCharge >= 0f) CancelCharge();
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
            requestPendingUntil = -1f;
            player.Animator?.CancelThrowCharge();
            bufferedCharge = -1f;
        }

        void UpdateLockPreview()
        {
            Preview = default;
            if (bomb == null || bomb.Carrier != player || allPlayers == null) return;
            Preview = ComputeShot(ThrowOriginNow(), Charge01);
        }

        /// <summary>Throws now (offline / host), or asks the host to (remote client). The bomb leaves this frame.</summary>
        public bool TryThrow(float charge01 = 0f)
        {
            if (RequestPending || bomb == null || bomb.State != BombState.Held || bomb.Carrier != player) return false;

            Vector3 origin = ThrowOriginNow();
            var shot = ComputeShot(origin, charge01);
            LastShot = shot;
            player.Animator?.ReleaseThrow();   // follow-through only

            if (NetMode.IsRemoteClient)
            {
                requestPendingUntil = Time.time + RequestTimeout;
                player.Net.RequestThrow(origin, shot.Velocity);
                return true;
            }

            if (!bomb.TryThrow(player, origin, shot.Velocity)) return false;
            if (player.Net != null && player.Net.IsSpawned) player.Net.BroadcastThrowRelease();
            return true;
        }

        /// <summary>Host only: a remote owner's validated throw request, released immediately.</summary>
        public bool ThrowFromRequest(Vector3 origin, Vector3 velocity)
        {
            if (bomb == null || bomb.State != BombState.Held || bomb.Carrier != player) return false;

            player.Animator?.ReleaseThrow();
            if (!bomb.TryThrow(player, origin, velocity)) return false;
            player.Net.BroadcastThrowRelease();
            return true;
        }

        /// <summary>Throw origin from the current aim (the anchor transform only updates in LateUpdate).</summary>
        public Vector3 ThrowOriginNow() =>
            player.CameraTarget.position + player.Look.ViewRotation * player.ThrowOrigin.localPosition;

        /// <summary>
        /// The share of the thrower's own motion a throw along <paramref name="direction"/> keeps: horizontal, along the
        /// aim, never backwards, never sideways, never vertical (so strafing and jumping do not bend your aim).
        /// </summary>
        public static Vector3 InheritedVelocity(GameTuning t, Vector3 playerVelocity, Vector3 direction)
        {
            var flatDir = new Vector3(direction.x, 0f, direction.z);
            if (t.throwInheritForward <= 0f || flatDir.sqrMagnitude < 1e-4f) return Vector3.zero;
            flatDir.Normalize();
            float along = Vector3.Dot(new Vector3(playerVelocity.x, 0f, playerVelocity.z), flatDir);
            return along > 0f ? flatDir * (along * t.throwInheritForward) : Vector3.zero;
        }

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
            Vector3 forward = player.Look.ViewRotation * Vector3.forward;   // what the crosshair shows
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
            // 3. Keep a share of the run speed along the aim (never sideways or vertical).
            Vector3 inherited = InheritedVelocity(t, player.Velocity, dir);
            shot.RawVelocity = dir * speed + inherited;

            // 4. Small release-time assist toward a receiver near the aim: direction only, never speed.
            var assist = AimAssist.Apply(t, pivot, forward, origin, dir, speed, inherited, player, allPlayers, sightMask);
            shot.AssistTarget = assist.Target;
            shot.AssistAngle = assist.Angle;
            shot.AssistStrength = assist.Strength;
            shot.AssistCorrection = assist.Correction;
            shot.AssistYawOnly = assist.Target != null && !assist.Reachable;
            shot.Velocity = assist.Direction * speed + inherited;
            return shot;
        }
    }
}
