using System;
using System.Collections.Generic;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Stands until a player steps on it, shakes for <see cref="warningDelay"/>, then drops away.
    /// The whole animation is a pure function of ONE number, the (server) time at which it was triggered,
    /// so every machine shows the same thing: the host decides when it triggers and replicates that time
    /// (see <see cref="NetworkFallingPlatform"/>). Restored by the RunManager on every section reset.
    /// </summary>
    public class FallingPlatform : MonoBehaviour, IResettable
    {
        [SerializeField, Tooltip("The part that shakes and falls (Visual + Collision live under it).")]
        Transform body;
        [SerializeField, Tooltip("Trigger volume just above the surface; a player inside starts the collapse.")]
        Collider trigger;
        [SerializeField, Min(0f)] float warningDelay = 0.6f;
        [SerializeField, Min(0f)] float fallAcceleration = 25f;
        [SerializeField, Min(1f), Tooltip("Metres it falls before it is switched off.")] float fallDistance = 20f;
        [SerializeField, Min(0f)] float shakeAmount = 0.05f;

        readonly HashSet<Player> inside = new HashSet<Player>();
        Vector3 startPosition;
        double triggerTime = -1.0;

        /// <summary>Raised when the trigger time changes (the host replicates it).</summary>
        public event Action<double> TriggerTimeChanged;

        public double TriggerTime => triggerTime;
        double Elapsed => triggerTime < 0.0 ? -1.0 : NetMode.ServerTime - triggerTime;

        public bool IsIdle => triggerTime < 0.0;
        public bool HasFallen => Elapsed >= warningDelay;

        void Awake() => startPosition = body.localPosition;

        void FixedUpdate()
        {
            if (NetMode.IsAuthority && IsIdle && PlayerZone.Collect(trigger, inside) > 0)
            {
                SetTriggerTime(NetMode.ServerTime);
                BeepLog.Run($"{name} warning ({warningDelay:F2}s)");
            }
        }

        void Update() => ApplyPose();

        public void SetTriggerTime(double time, bool notify = true)
        {
            triggerTime = time;
            if (notify) TriggerTimeChanged?.Invoke(time);
        }

        void ApplyPose()
        {
            double e = Elapsed;
            if (e < 0.0)
            {
                body.gameObject.SetActive(true);
                body.localPosition = startPosition;
            }
            else if (e < warningDelay)
            {
                body.gameObject.SetActive(true);
                body.localPosition = startPosition + UnityEngine.Random.insideUnitSphere * shakeAmount;
            }
            else
            {
                float t = (float)(e - warningDelay);
                float drop = 0.5f * fallAcceleration * t * t;
                if (drop > fallDistance)
                {
                    body.gameObject.SetActive(false);
                }
                else
                {
                    body.gameObject.SetActive(true);
                    body.localPosition = startPosition + Vector3.down * drop;
                }
            }
        }

        public void ResetState()
        {
            SetTriggerTime(-1.0);
            ApplyPose();
        }
    }
}
