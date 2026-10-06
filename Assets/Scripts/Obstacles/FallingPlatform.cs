using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Stands until a player steps on it, shakes for <see cref="warningDelay"/>, then drops away.
    /// The whole animation is a pure function of ONE number, the (server) time at which it was triggered,
    /// so every machine shows the same thing: the host decides when it triggers and replicates that time
    /// (see <see cref="NetworkFallingPlatform"/>). Restored by the RunManager on every section reset.
    /// Generic (docs/OBSTACLES.md §4): the falling part and the trigger are references, so any visual under the body falls;
    /// subclasses may override <see cref="ApplyPose(double)"/> (crumble instead of drop), still from the elapsed time only.
    /// </summary>
    public class FallingPlatform : MonoBehaviour, IResettable, IObstacleState
    {
        [SerializeField, Tooltip("The part that shakes and falls (its visual and colliders live under it). Never this object itself.")]
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
        /// <summary>Seconds since the platform was triggered, or -1 while it stands.</summary>
        protected double Elapsed => triggerTime < 0.0 ? -1.0 : NetMode.ServerTime - triggerTime;
        public Transform Body => body;
        public Collider Trigger => trigger;
        public float WarningDelay => warningDelay;
        protected Vector3 StartPosition => startPosition;

        /// <summary>0 while it stands, then up to 1 at the end of the warning shake; 1 once it falls.</summary>
        float IObstacleState.Progress => IsIdle ? 0f : Mathf.Clamp01((float)(Elapsed / Mathf.Max(0.01f, warningDelay)));
        bool IObstacleState.Active => !IsIdle;

        public bool IsIdle => triggerTime < 0.0;
        public bool HasFallen => Elapsed >= warningDelay;

        void Awake() => startPosition = body.localPosition;

        void FixedUpdate()
        {
            if (NetMode.IsAuthority && IsIdle && PlayerZone.Collect(trigger, inside) > 0)
            {
                SetTriggerTime(NetMode.ServerTime);
                PatataLog.Run($"{name} warning ({warningDelay:F2}s)");
            }
        }

        void Update() => ApplyPose();

        void ApplyPose()
        {
            if (body != null) ApplyPose(Elapsed);
        }

        public void SetTriggerTime(double time, bool notify = true)
        {
            triggerTime = time;
            if (notify) TriggerTimeChanged?.Invoke(time);
        }

        /// <summary>Poses the body <paramref name="e"/> seconds after the trigger (negative: standing).</summary>
        protected virtual void ApplyPose(double e)
        {
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
