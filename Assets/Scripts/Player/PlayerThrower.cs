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

        public bool Charging => chargeStartTime >= 0f;

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
            // Always consume both edges so nothing queues up while we cannot throw.
            bool pressed = player.Input.ThrowPressed;
            bool released = player.Input.ThrowReleased;

            bool mayThrow = bomb != null && !player.ControlLocked && bomb.Carrier == player
                            && (bomb.State == BombState.Held || bomb.State == BombState.CaughtGrace);
            if (!mayThrow)
            {
                CancelCharge();
                return;
            }

            if (pressed) chargeStartTime = Time.time;

            if (released && Charging)
            {
                float charge = Charge01;
                CancelCharge();
                TryThrow(charge);
            }
        }

        public void CancelCharge() => chargeStartTime = -1f;

        public bool TryThrow(float charge01 = 0f)
        {
            if (bomb == null || bomb.State != BombState.Held || bomb.Carrier != player) return false;

            Vector3 origin = ThrowOriginNow();
            return bomb.TryThrow(player, origin, ComputeThrowVelocity(origin, charge01));
        }

        /// <summary>Throw origin from the current aim (the anchor transform only updates in LateUpdate).</summary>
        public Vector3 ThrowOriginNow() =>
            player.CameraTarget.position + player.Look.AimRotation * player.ThrowOrigin.localPosition;

        public static float SpeedFor(GameTuning t, float charge01) =>
            Mathf.Lerp(t.throwSpeedMin, t.throwSpeedMax, Mathf.Clamp01(charge01));

        /// <summary>Deterministic: the same aim, charge and tuning always give the same velocity.</summary>
        public Vector3 ComputeThrowVelocity(Vector3 origin, float charge01)
        {
            var t = player.Tuning;
            float speed = SpeedFor(t, charge01);

            // 1. Where is the player aiming?
            Vector3 pivot = player.CameraTarget.position;
            Vector3 forward = player.Look.AimRotation * Vector3.forward;
            Vector3 aimPoint = Physics.Raycast(pivot, forward, out var hit, t.aimMaxDistance, aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : pivot + forward * t.aimMaxDistance;

            Vector3 dir = aimPoint - origin;
            dir = dir.sqrMagnitude < 1f ? forward : dir.normalized;

            // 2. Pitch up a little to counter drop.
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            if (right.sqrMagnitude > 1e-4f)
                dir = Quaternion.AngleAxis(-t.throwUpAngle, right.normalized) * dir;

            // 3. Optional small assist toward a nearby receiver.
            dir = ApplyAimAssist(origin, dir, speed, t);

            return dir * speed;
        }

        Vector3 ApplyAimAssist(Vector3 origin, Vector3 dir, float speed, GameTuning t)
        {
            if (allPlayers == null || t.aimAssistStrength <= 0f) return dir;

            float g = -Physics.gravity.y * t.bombGravityScale;
            Vector3? best = null;
            float bestAngle = t.aimAssistAngle;

            foreach (var other in allPlayers)
            {
                if (other == player) continue;

                Vector3 to = other.CatchVolume.CatchCenter - origin;
                if (to.magnitude > t.aimAssistDistance) continue;
                if (!TrySolveBallistic(to, speed, g, out Vector3 solved)) continue;

                float angle = Vector3.Angle(dir, solved);
                if (angle <= bestAngle)
                {
                    bestAngle = angle;
                    best = solved;
                }
            }

            return best.HasValue ? Vector3.Slerp(dir, best.Value, t.aimAssistStrength).normalized : dir;
        }

        /// <summary>Low-arc launch direction that reaches <paramref name="to"/> at the given speed.</summary>
        static bool TrySolveBallistic(Vector3 to, float speed, float gravity, out Vector3 direction)
        {
            direction = default;
            var flat = new Vector3(to.x, 0f, to.z);
            float d = flat.magnitude;
            if (d < 0.5f || gravity <= 0f) return false;

            float v2 = speed * speed;
            float disc = v2 * v2 - gravity * (gravity * d * d + 2f * to.y * v2);
            if (disc < 0f) return false;

            float tanTheta = (v2 - Mathf.Sqrt(disc)) / (gravity * d);
            direction = (flat.normalized + Vector3.up * tanTheta).normalized;
            return true;
        }
    }
}
