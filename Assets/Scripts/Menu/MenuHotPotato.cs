using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The menu backdrop's little show (ARCHITECTURE §6.2): four mannequins in the slot colours pass the potato around
    /// in arcs while its wick sparks faster and faster. When the fuse runs out it goes off in the game's own cartoon
    /// explosion (<see cref="ExplosionFx"/>, its flash always reduced, more if the player asked), the unlucky holder
    /// flinches, the others cheer, and a new potato pops in. Presentation only: no physics, no rules, nothing networked.
    /// It is a loop of looks, built by <c>MenuBackdropBuilder</c>, on unscaled time (the menu is offline and local).
    /// </summary>
    public class MenuHotPotato : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        const string Idle = "Idle_A", IdleLook = "Idle_B", Throw = "Throw", Cheer = "Cheering", Wave = "Waving", Flinch = "Hit_A", Hop = "Jump_Full_Short";
        const float Blend = 0.15f;

        [SerializeField] GameTuning tuning;
        [SerializeField, Tooltip("The players' pivots (yaw only), each with its mannequin's Animator below it.")] Transform[] players;
        [SerializeField, Tooltip("The potato's visual (model, wick, sparks).")] Transform potato;
        [SerializeField] ParticleSystem sparks;
        [SerializeField] TrailRenderer trail;
        [SerializeField] ParticleSystem flightPuffs;
        [SerializeField] ExplosionFx explosionPrefab;

        [Header("Pacing")]
        [SerializeField] Vector2 holdSeconds = new Vector2(0.45f, 1.1f);
        [SerializeField] Vector2 flightSeconds = new Vector2(0.75f, 1.15f);
        [SerializeField] Vector2 arcHeight = new Vector2(1.4f, 3.2f);
        [SerializeField] Vector2 fuseSeconds = new Vector2(9f, 15f);
        [SerializeField, Tooltip("Seconds into the Throw clip when the potato leaves the hand.")] float releaseAt = 0.45f;
        [SerializeField, Tooltip("Seconds of calm after an explosion.")] float afterBoom = 2.6f;
        [SerializeField, Tooltip("Chance per pass that a bystander cheers, waves or hops.")] float bystanderChance = 0.35f;

        [Header("Looks")]
        [SerializeField] float handHeight = 1.1f;
        [SerializeField] float handForward = 0.42f;
        [SerializeField, Tooltip("Degrees per second the players turn to face the action.")] float turnSpeed = 420f;
        [SerializeField, Tooltip("Menu explosions always reduce the flash at least this much (it plays while you read the menu).")]
        float menuFlashFloor = 0.6f;
        [SerializeField] float sparkRateFloor = 12f;

        Animator[] animators;
        float[] busyUntil, squash;
        Vector3 potatoScale;
        Transform holderPivot;   // the potato sits at this player's hands; null while it flies
        Transform lookTarget;    // what the bystanders face
        Transform aimAt;         // who the holder will throw to
        ExplosionFx explosion;
        float fuseStart, fuseEnd;
        bool exploding;

        public void Configure(GameTuning gameTuning, Transform[] pivots, Transform potatoVisual, ParticleSystem wickSparks,
                              TrailRenderer flightTrail, ParticleSystem puffs, ExplosionFx boom)
        {
            tuning = gameTuning;
            players = pivots;
            potato = potatoVisual;
            sparks = wickSparks;
            trail = flightTrail;
            flightPuffs = puffs;
            explosionPrefab = boom;
        }

        void Start()
        {
            if (Application.isBatchMode || players == null || players.Length < 2 || potato == null)
            {
                enabled = false;
                return;
            }
            animators = new Animator[players.Length];
            busyUntil = new float[players.Length];
            squash = new float[players.Length];
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < players.Length; i++)
            {
                animators[i] = players[i].GetComponentInChildren<Animator>();
                if (animators[i] != null) animators[i].Play(Idle, 0, Random.value);
                // The slot colour on the suit, as in game (PlayerPresentation).
                foreach (var r in players[i].GetComponentsInChildren<Renderer>())
                {
                    r.GetPropertyBlock(block);
                    block.SetColor(BaseColor, PlayerIdentity.ColorFor(tuning, i));
                    r.SetPropertyBlock(block);
                }
            }
            potatoScale = potato.localScale;
            if (explosionPrefab != null)
            {
                explosion = Instantiate(explosionPrefab, transform);
                explosion.gameObject.SetActive(false);
            }
            SetFlying(false);
            StartCoroutine(Show());
        }

        IEnumerator Show()
        {
            int holder = Random.Range(0, players.Length);
            while (true)
            {
                // A new potato pops into someone's hands.
                yield return PopIn(holder);
                fuseStart = Time.unscaledTime;
                fuseEnd = fuseStart + Random.Range(fuseSeconds.x, fuseSeconds.y);

                while (true)
                {
                    int target = PickTarget(holder);
                    holderPivot = players[holder];
                    aimAt = players[target];
                    lookTarget = potato;
                    float hold = Random.Range(holdSeconds.x, holdSeconds.y);
                    yield return Wait(hold);
                    if (Time.unscaledTime >= fuseEnd) break;

                    // Wind up, release, fly.
                    Play(holder, Throw);
                    yield return Wait(releaseAt);
                    holderPivot = null;
                    yield return Fly(players[holder], players[target]);
                    holder = target;
                    holderPivot = players[holder];
                    squash[holder] = 1f;
                    Bystanders(holder);
                    if (Time.unscaledTime >= fuseEnd) break;
                }

                yield return Boom(holder);
                holder = Random.Range(0, players.Length);
            }
        }

        IEnumerator PopIn(int holder)
        {
            holderPivot = players[holder];
            lookTarget = potato;
            potato.gameObject.SetActive(true);
            exploding = false;
            const float duration = 0.45f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                potato.localScale = potatoScale * EaseOutBack(t / duration);
                yield return null;
            }
            potato.localScale = potatoScale;
        }

        IEnumerator Fly(Transform from, Transform to)
        {
            SetFlying(true);
            Vector3 start = Hand(from), spin = Random.onUnitSphere;
            float duration = Random.Range(flightSeconds.x, flightSeconds.y);
            float height = Random.Range(arcHeight.x, arcHeight.y);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float u = t / duration;
                Vector3 end = Hand(to);   // the catcher may still be turning
                potato.position = Vector3.Lerp(start, end, u) + Vector3.up * (4f * height * u * (1f - u));
                potato.Rotate(spin, 540f * Time.unscaledDeltaTime, Space.World);
                yield return null;
            }
            SetFlying(false);
        }

        IEnumerator Boom(int holder)
        {
            exploding = true;
            if (explosion != null)
            {
                float reduction = Mathf.Max(menuFlashFloor, tuning != null ? Settings.FlashReduction(tuning) : 1f);
                explosion.Play(potato.position, reduction);
            }
            potato.gameObject.SetActive(false);
            Play(holder, Flinch);
            lookTarget = players[holder];
            yield return Wait(0.35f);
            for (int i = 0; i < players.Length; i++)
                if (i != holder) Play(i, Random.value < 0.7f ? Cheer : Hop);
            yield return Wait(afterBoom);
        }

        int PickTarget(int holder)
        {
            int target = Random.Range(0, players.Length - 1);
            return target >= holder ? target + 1 : target;
        }

        void Bystanders(int holder)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (i == holder || Random.value > bystanderChance / (players.Length - 1)) continue;
                float roll = Random.value;
                Play(i, roll < 0.4f ? Cheer : roll < 0.7f ? Wave : roll < 0.85f ? Hop : IdleLook);
            }
        }

        void Update()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;

            // The potato rides in its holder's hands, wobbling harder as the fuse burns down.
            float burnt = fuseEnd > fuseStart ? Mathf.Clamp01((now - fuseStart) / (fuseEnd - fuseStart)) : 0f;
            if (holderPivot != null && !exploding)
            {
                float wobble = Mathf.Lerp(4f, 22f, burnt * burnt);
                potato.position = Hand(holderPivot) + Vector3.up * (Mathf.Sin(now * 9f) * 0.03f);
                potato.rotation = Quaternion.LookRotation(holderPivot.forward) * Quaternion.Euler(0f, 0f, Mathf.Sin(now * (8f + 18f * burnt)) * wobble);
            }
            if (sparks != null)
            {
                var emission = sparks.emission;
                emission.rateOverTime = SparkRate(burnt);
            }

            for (int i = 0; i < players.Length; i++)
            {
                // Everyone faces the action: the holder faces the player they will throw to, the others the potato.
                Vector3 target = holderPivot == players[i] && aimAt != null ? aimAt.position
                    : lookTarget != null ? lookTarget.position : transform.position;
                Vector3 flat = target - players[i].position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f)
                    players[i].rotation = Quaternion.RotateTowards(players[i].rotation, Quaternion.LookRotation(flat), turnSpeed * dt);

                // A catch squashes the catcher for a beat; a one-shot clip returns to idle when it ends.
                squash[i] = Mathf.MoveTowards(squash[i], 0f, dt * 4f);
                float s = 1f + Mathf.Sin(squash[i] * Mathf.PI) * 0.08f;
                players[i].localScale = new Vector3(s, 2f - s, s);
                if (busyUntil[i] > 0f && now >= busyUntil[i])
                {
                    busyUntil[i] = 0f;
                    if (animators[i] != null) animators[i].CrossFadeInFixedTime(Idle, Blend);
                }
            }
        }

        float SparkRate(float burnt)
        {
            var rates = tuning != null ? tuning.fuseSparkRates : null;
            if (rates == null || rates.Length == 0) return Mathf.Lerp(sparkRateFloor, 60f, burnt);
            int stage = Mathf.Clamp((int)(burnt * rates.Length), 0, rates.Length - 1);
            return Mathf.Max(sparkRateFloor, rates[stage]);
        }

        void Play(int i, string clip)
        {
            var animator = animators[i];
            if (animator == null) return;
            animator.CrossFadeInFixedTime(clip, Blend);
            busyUntil[i] = Time.unscaledTime + ClipLength(animator, clip) - Blend;
        }

        readonly Dictionary<string, float> lengths = new Dictionary<string, float>();

        float ClipLength(Animator animator, string clip)
        {
            if (lengths.TryGetValue(clip, out float length)) return length;
            length = 1f;
            foreach (var c in animator.runtimeAnimatorController.animationClips)
                if (c.name == clip) { length = c.length; break; }
            lengths[clip] = length;
            return length;
        }

        void SetFlying(bool on)
        {
            if (trail != null) trail.emitting = on;
            if (flightPuffs != null)
            {
                if (on) flightPuffs.Play(true);
                else flightPuffs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        Vector3 Hand(Transform pivot) => pivot.position + Vector3.up * handHeight + pivot.forward * handForward;

        static IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            x = Mathf.Clamp01(x);
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
