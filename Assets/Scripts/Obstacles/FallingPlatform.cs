using System.Collections.Generic;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Stands until a player steps on it, shakes for <see cref="warningDelay"/>, then drops away.
    /// Restored by the RunManager on every section reset. Forces the group to commit rather than wait.
    /// </summary>
    public class FallingPlatform : MonoBehaviour, IResettable
    {
        enum State { Idle, Warning, Falling, Fallen }

        [SerializeField, Tooltip("The part that shakes and falls (Visual + Collision live under it).")]
        Transform body;
        [SerializeField, Tooltip("Trigger volume just above the surface; a player inside starts the collapse.")]
        Collider trigger;
        [SerializeField, Min(0f)] float warningDelay = 0.6f;
        [SerializeField, Min(0f)] float fallAcceleration = 25f;
        [SerializeField, Min(1f), Tooltip("Metres it falls before it is switched off.")] float fallDistance = 20f;
        [SerializeField, Min(0f)] float shakeAmount = 0.05f;

        readonly HashSet<Player> inside = new HashSet<Player>();
        State state = State.Idle;
        Vector3 startPosition;
        float timer, fallSpeed;

        public bool IsIdle => state == State.Idle;
        public bool HasFallen => state == State.Falling || state == State.Fallen;

        void Awake() => startPosition = body.localPosition;

        void FixedUpdate()
        {
            if (NetMode.IsAuthority && state == State.Idle && PlayerZone.Collect(trigger, inside) > 0)
            {
                state = State.Warning;
                timer = warningDelay;
                BeepLog.Run($"{name} warning ({warningDelay:F2}s)");
            }
        }

        void Update()
        {
            switch (state)
            {
                case State.Warning:
                    timer -= Time.deltaTime;
                    body.localPosition = startPosition + Random.insideUnitSphere * shakeAmount;
                    if (timer <= 0f)
                    {
                        body.localPosition = startPosition;
                        fallSpeed = 0f;
                        state = State.Falling;
                    }
                    break;

                case State.Falling:
                    fallSpeed += fallAcceleration * Time.deltaTime;
                    body.position += Vector3.down * (fallSpeed * Time.deltaTime);
                    if (startPosition.y - body.localPosition.y > fallDistance)
                    {
                        body.gameObject.SetActive(false);
                        state = State.Fallen;
                    }
                    break;
            }
        }

        public void ResetState()
        {
            state = State.Idle;
            timer = 0f;
            fallSpeed = 0f;
            body.gameObject.SetActive(true);
            body.localPosition = startPosition;
        }
    }
}
