using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Cartoon dust puffs at a player's feet, visible on every machine: a ring on hard landings, a spray while
    /// sliding, a puff on sprint footfalls. Reads movement (the owner's motor, or the replicated posture and
    /// observed velocity for remote copies) and never changes it. Emits into one world-space ParticleSystem.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerDustFx : MonoBehaviour
    {
        [SerializeField, Tooltip("World-space ParticleSystem with no emission of its own (puffs are emitted from here).")]
        ParticleSystem dust;
        [SerializeField] Color dustColor = new Color(1f, 0.95f, 0.85f, 1f);
        [SerializeField, Min(0.5f)] float sprintPuffEvery = 2.4f;   // metres

        Player player;
        int groundMask;
        bool wasGrounded = true;
        float fallSpeed, slideBudget, sprintDistance;
        Vector3 lastPosition;

        void Awake()
        {
            player = GetComponent<Player>();
            groundMask = LayerMask.GetMask("Environment", "Hazard");
            lastPosition = transform.position;
            if (dust != null)
            {
                var emission = dust.emission;
                emission.enabled = false;
            }
        }

        void Update()
        {
            if (dust == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var motor = player.Motor;
            Vector3 velocity = player.Velocity;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            bool grounded = player.IsLocal
                ? motor.Grounded
                : Physics.CheckSphere(transform.position + Vector3.up * 0.12f, 0.22f, groundMask, QueryTriggerInteraction.Ignore);

            if (grounded && !wasGrounded && fallSpeed < -6f)
                LandingRing(Mathf.Clamp01((-fallSpeed - 6f) / 12f));
            fallSpeed = grounded ? 0f : Mathf.Min(fallSpeed, velocity.y);
            wasGrounded = grounded;

            Vector3 moved = transform.position - lastPosition;
            lastPosition = transform.position;
            if (moved.sqrMagnitude > 25f) return;   // a teleport

            if (grounded && motor.IsSliding && flat.sqrMagnitude > 4f)
            {
                slideBudget += 28f * dt;
                while (slideBudget >= 1f)
                {
                    slideBudget -= 1f;
                    Vector3 back = -flat.normalized;
                    Puff(transform.position + back * 0.2f + Random.insideUnitSphere * 0.25f,
                        back * Random.Range(0.5f, 2f) + Vector3.up * Random.Range(0.8f, 2f) + Random.insideUnitSphere * 0.6f,
                        Random.Range(0.18f, 0.34f), Random.Range(0.35f, 0.6f));
                }
            }

            if (grounded && motor.IsSprinting)
            {
                sprintDistance += new Vector3(moved.x, 0f, moved.z).magnitude;
                if (sprintDistance >= sprintPuffEvery)
                {
                    sprintDistance = 0f;
                    for (int i = 0; i < 2; i++)
                        Puff(transform.position + Random.insideUnitSphere * 0.2f,
                            -flat.normalized * 0.8f + Vector3.up * 0.6f + Random.insideUnitSphere * 0.4f,
                            Random.Range(0.14f, 0.24f), Random.Range(0.3f, 0.45f));
                }
            }
        }

        void LandingRing(float impact)
        {
            int count = Mathf.RoundToInt(Mathf.Lerp(6f, 16f, impact));
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Puff(transform.position + outward * 0.3f + Vector3.up * 0.05f,
                    outward * Random.Range(2f, 3.5f) * Mathf.Lerp(0.7f, 1.3f, impact) + Vector3.up * Random.Range(0.3f, 0.9f),
                    Random.Range(0.2f, 0.38f) * Mathf.Lerp(0.8f, 1.3f, impact), Random.Range(0.35f, 0.55f));
            }
        }

        void Puff(Vector3 position, Vector3 velocity, float size, float lifetime)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = dustColor,
                rotation3D = Random.insideUnitSphere * 180f,
            };
            dust.Emit(p, 1);
        }
    }
}
