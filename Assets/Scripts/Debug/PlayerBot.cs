using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A simple teammate bot for automated multiplayer runs and solo pass practice: when it holds the bomb it aims at
    /// the next player (solving the arc for a sensible charge, with a little human error) and passes it; when a bomb
    /// is flying toward it, it presses catch as it gets close. While not holding the bomb it can move in a
    /// <see cref="Pattern"/> so passes to a moving or jumping receiver can be practised alone.
    /// It plays through the same input path (and therefore the same network rules) as a human.
    /// Enabled with the -patataBot command-line flag, or on the idle player in PassSandbox with F4 (dev builds);
    /// -patataBotMove &lt;pattern&gt; picks the pattern. <see cref="Pattern.Ride"/> is a network diagnostic
    /// (docs/netcode-deterministic-plan.md stage 0): the bot hops onto the nearest moving platform and stays on it.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerBot : MonoBehaviour
    {
        public enum Pattern { Stand, Strafe, Jump, RunAcross, Ride }

        const float BoardAfterReset = 1f;   // seconds: let the host's respawn teleport land before boarding again

        public static bool Enabled;
        /// <summary>How bots move while waiting for a pass (solo practice).</summary>
        public static Pattern Movement = Pattern.Stand;

        [SerializeField, Min(0f)] float holdBeforeThrow = 0.8f;
        [SerializeField, Min(0.5f)] float catchWhenWithin = 3.0f;
        [Tooltip("Random aim error per throw, in degrees (0 = perfect).")]
        [SerializeField, Min(0f)] float aimErrorDegrees = 0.75f;
        [Tooltip("Charge used for passes; raised automatically if the receiver would be out of range.")]
        [SerializeField, Range(0f, 1f)] float preferredCharge = 0.5f;

        Player player;
        float heldSince = -1f;
        float chargeUntil = -1f;
        float patternClock;
        int boardedForReset = -1;
        int seenResetCount = -1;
        float resetSeenAt;

        void Awake() => player = GetComponent<Player>();

        void Update()
        {
            var bomb = BombController.Instance;
            var input = player.Input.Scripted;
            if (bomb == null || input == null || player.ControlLocked) return;

            Player other = NextTeammate();
            if (other == null) return;

            bool holding = bomb.Carrier == player && bomb.State == BombState.Held;
            Move(input, holding || bomb.Carrier == player);

            // Holding it: aim at the teammate, charge, release.
            if (holding)
            {
                if (heldSince < 0f) heldSince = Time.time;
                if (chargeUntil < 0f && Time.time - heldSince >= holdBeforeThrow)
                {
                    float charge = AimAt(other);
                    input.SetThrowHeld(true);
                    chargeUntil = Time.time + charge * player.Tuning.throwChargeTime;
                }
                if (chargeUntil >= 0f && Time.time >= chargeUntil)
                {
                    input.SetThrowHeld(false);
                    chargeUntil = -1f;
                    heldSince = -1f;
                }
            }
            else
            {
                heldSince = -1f;
                if (chargeUntil >= 0f) input.SetThrowHeld(false);
                chargeUntil = -1f;
            }

            // Incoming: catch as it arrives.
            if (bomb.State == BombState.Thrown && bomb.LastThrower != player &&
                Vector3.Distance(bomb.transform.position, player.CatchVolume.CatchCenter) < catchWhenWithin)
            {
                input.PressCatch();
            }
        }

        void Move(PlayerInputReader.ScriptedInput input, bool carrying)
        {
            patternClock += Time.deltaTime;
            input.Move = Vector2.zero;
            if (Movement == Pattern.Ride)
            {
                Board();
                return;   // stand still and let the platform carry us
            }
            if (carrying) return;   // stand still to throw

            switch (Movement)
            {
                case Pattern.Strafe:
                    input.Move = new Vector2(Mathf.Repeat(patternClock, 2.4f) < 1.2f ? 1f : -1f, 0f);
                    break;
                case Pattern.Jump:
                    if (player.Motor.Grounded && Mathf.Repeat(patternClock, 1.1f) < Time.deltaTime) input.PressJump();
                    break;
                case Pattern.RunAcross:
                    input.Move = new Vector2(Mathf.Repeat(patternClock, 4f) < 2f ? 1f : -1f, 0f);
                    break;
            }
        }

        /// <summary>
        /// <see cref="Pattern.Ride"/>: once per section attempt, teleport onto the top of the nearest moving carrier that is
        /// not lethal (no crusher). Debug only; it goes through <see cref="Player.TeleportTo"/> like any teleport.
        /// </summary>
        void Board()
        {
            var run = RunManager.Instance;
            if (run == null || run.State != RunState.Playing) return;
            if (run.ResetCount != seenResetCount)
            {
                seenResetCount = run.ResetCount;
                resetSeenAt = Time.time;
            }
            if (boardedForReset == seenResetCount || Time.time - resetSeenAt < BoardAfterReset) return;
            if (!TryFindRide(out Vector3 top)) return;

            boardedForReset = seenResetCount;
            player.TeleportTo(top, transform.rotation);
        }

        bool TryFindRide(out Vector3 top)
        {
            top = default;
            float best = float.MaxValue;
            foreach (var mp in FindObjectsByType<MovingPlatform>(FindObjectsSortMode.None))
                Consider(mp.Platform, ref best, ref top);
            foreach (var sa in FindObjectsByType<SignalActuator>(FindObjectsSortMode.None))
                Consider(sa.Platform, ref best, ref top);
            return best < float.MaxValue;
        }

        void Consider(Transform part, ref float best, ref Vector3 top)
        {
            if (part == null || part.GetComponentInChildren<KillZone>(true) != null) return;
            bool any = false;
            Bounds bounds = default;
            foreach (var c in part.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (any) bounds.Encapsulate(c.bounds);
                else bounds = c.bounds;
                any = true;
            }
            if (!any) return;
            float d = (bounds.center - transform.position).sqrMagnitude;
            if (d >= best) return;
            best = d;
            top = new Vector3(bounds.center.x, bounds.max.y + 0.05f, bounds.center.z);
        }

        /// <summary>The player in the next slot (wrapping), so with 3+ players passes also go client to client.</summary>
        Player NextTeammate()
        {
            Player next = null, lowest = null;
            foreach (var p in Player.All)
            {
                if (p == null || p == player) continue;
                if (lowest == null || p.PlayerId < lowest.PlayerId) lowest = p;
                if (p.PlayerId > player.PlayerId && (next == null || p.PlayerId < next.PlayerId)) next = p;
            }
            return next != null ? next : lowest;
        }

        /// <summary>Points the view so the raw throw arc lands on the receiver; returns the charge to use.</summary>
        float AimAt(Player receiver)
        {
            var t = player.Tuning;
            Vector3 origin = player.Thrower.ThrowOriginNow();
            Vector3 to = receiver.CatchVolume.CatchCenter - origin;
            float g = ThrowBallistics.Gravity(t);

            float charge = preferredCharge;
            Vector3 dir;
            while (!ThrowBallistics.TrySolveLowArc(to, PlayerThrower.SpeedFor(t, charge), g, out dir, out _) && charge < 1f)
                charge = Mathf.Min(1f, charge + 0.1f);
            if (dir == Vector3.zero) dir = to.normalized;

            // The throw leaves throwUpAngle above the aim, so look that much lower than the solved direction.
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float elevation = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg - t.throwUpAngle;
            Vector2 error = Random.insideUnitCircle * aimErrorDegrees;
            player.Look.SetAim(yaw + error.x, -(elevation + error.y));
            return charge;
        }
    }
}
