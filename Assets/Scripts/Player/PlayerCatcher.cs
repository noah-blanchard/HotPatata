using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Catching is a timed action, never automatic. Pressing catch opens a short window
    /// (<see cref="GameTuning.catchWindowDuration"/>); a bomb only counts as caught if it reaches this
    /// player's catch volume while the window is open. After the window closes there is a cooldown
    /// (<see cref="GameTuning.catchCooldown"/>) so the button cannot be mashed. The bomb's
    /// <see cref="CatchResolver"/> reads <see cref="WindowOpen"/>; this class never touches the bomb's state.
    ///
    /// Online (#117 P3): a remote player's press travels with its server time (<see cref="NetworkPlayer.RequestCatch"/>), so
    /// the host knows when their window really opened (<see cref="RemoteWindowCovers"/>). Their own machine draws the same
    /// flight as the host (a pure function of server time); when it reaches the catch sphere while the window is open, it
    /// claims the catch with the time and the centre it saw, and shows the catch at once (spec §9.3). The host decides.
    /// The host relays every window that opens, so other screens can show a teammate's catch as it happens
    /// (<see cref="ShownWindowOpen"/>, presentation). "Too late / too early" hints come from the owner's own view.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerCatcher : MonoBehaviour
    {
        // Catch diagnostics only (they explain a miss, they never decide one).
        const float HintDuration = 1.6f;
        const float ReachSlack = 0.25f;           // metres beyond the reach that still count as "in reach now"
        const float LateMax = 0.8f;               // seconds since the bomb was in reach that still read as "too late"
        const float EarlyCheckDistance = 12f;     // metres: farther than this, a closed window is not "too early"
        const double Unset = double.NegativeInfinity;
        const double ClaimChordSeconds = 0.02;    // the claim sweep's chord, as long as one host physics step

        Player player;
        BombController bomb;
        NetworkBomb netBomb;
        float windowEnd = -1f;
        float cooldownEnd = -1f;
        float lastReachTime = -10f;
        bool windowWasOpen;
        bool claimSent;
        string hint;
        float hintUntil;

        // remote client: the flight as this machine draws it
        int seenFlight;
        double seenUntil;
        float seenInReachAt = -10f;
        double seenAt;
        Vector3 seenCenter, seenFeet;

        // host: when this remote player's window opened (their press time, server time); everyone: the last relayed window
        double remoteWindowStart = Unset;
        double shownWindowStart = Unset;

        /// <summary>Why the last catch attempt failed ("too late", "too early"), for a moment; null otherwise.</summary>
        public string Hint => Time.time < hintUntil ? hint : null;

        public bool WindowOpen => Time.time < windowEnd;
        public bool OnCooldown => !WindowOpen && Time.time < cooldownEnd;

        /// <summary>1 when the window has just opened, 0 when it closes.</summary>
        public float WindowRemaining01 => WindowOpen ? (windowEnd - Time.time) / player.Tuning.catchWindowDuration : 0f;
        /// <summary>1 right after the window closes, 0 when catch is available again.</summary>
        public float CooldownRemaining01 => OnCooldown ? (cooldownEnd - Time.time) / Mathf.Max(0.0001f, player.Tuning.catchCooldown) : 0f;

        void Awake() => player = GetComponent<Player>();

        public void Bind(BombController bombController)
        {
            Unbind();
            bomb = bombController;
            netBomb = bomb != null ? bomb.GetComponent<NetworkBomb>() : null;
            bomb.BombCaught += OnBombCaught;
        }

        void OnDestroy() => Unbind();

        void Unbind()
        {
            if (bomb != null) bomb.BombCaught -= OnBombCaught;
        }

        // A successful catch spends the window and frees the button.
        void OnBombCaught(Player receiver)
        {
            if (receiver == player) Clear();
        }

        void Update()
        {
            if (!player.IsLocal) return;

            CheckMissedEarly();   // the owner explains its own misses, from what it sees

            // The local window is instant feedback (and the whole story offline); online the host knows when it opened.
            bool pressed = player.Input.CatchPressed;   // always consume the input
            if (pressed && TryOpenWindow() && player.Net != null && player.Net.IsSpawned)
            {
                double now = SimulationClock.ServerNow;
                if (NetMode.IsRemoteClient) player.Net.RequestCatch(now);
                else player.Net.BroadcastWindowOpened(now);
            }

            if (NetMode.IsRemoteClient) ClaimIfInReach();
        }

        // What this machine draws is the host's flight at this machine's time: claim the catch we saw, with when and where.
        // Same swept reach and late grace as the host's CatchResolver, applied to the arc itself.
        void ClaimIfInReach()
        {
            var flight = bomb != null ? bomb.Body.Flight : default;
            if (bomb == null || bomb.State != BombState.Thrown || bomb.LastThrower == player || !flight.Valid || player.CatchVolume == null)
            {
                seenFlight = 0;
                return;
            }

            double now = SimulationClock.ServerNow;
            if (netBomb != null && netBomb.DrawnImpactAt < now) now = netBomb.DrawnImpactAt;   // it stopped on the world here
            // A new flight is swept from its start: a back-dated one may already have passed us before it reached this machine.
            double from = seenFlight == flight.Id ? seenUntil : flight.T0;
            if (seenFlight != flight.Id)
            {
                seenFlight = flight.Id;
                seenInReachAt = -10f;
                claimSent = claimSent && WindowOpen;   // a new flight: one claim per window still
            }
            // Short chords, like the host's sweep: one long chord would cut inside the arc.
            float g = bomb.Body.Gravity;
            Vector3 center = player.CatchVolume.CatchCenter;
            while (now > from)
            {
                double to = System.Math.Min(now, from + ClaimChordSeconds);
                Vector3 a = flight.PositionAt(from, g), b = flight.PositionAt(to, g);
                float d = CatchResolver.ReachDistance(player.Tuning, a, b, center, out _, out float along);
                if (d <= CatchResolver.ReachFor(player, b - a))
                {
                    seenInReachAt = Time.time;
                    lastReachTime = Time.time;
                    seenAt = from + (to - from) * along;
                    seenCenter = center;
                    seenFeet = transform.position;
                }
                from = to;
            }
            seenUntil = System.Math.Max(seenUntil, now);

            if (claimSent || !WindowOpen || Time.time - seenInReachAt > player.Tuning.catchLateGrace) return;
            claimSent = true;
            player.Net.ClaimCatch(flight.Id, seenAt, seenCenter, seenFeet);
            bomb.ShowPredictedCatch(player);
        }

        /// <summary>Opens a catch window if allowed (the local press).</summary>
        public bool TryOpenWindow()
        {
            if (bomb == null || player.ControlLocked) return false;
            if (bomb.Carrier == player) return false;    // you cannot catch what you are already holding
            if (WindowOpen || Time.time < cooldownEnd) return false;

            DiagnoseLatePress();

            var t = player.Tuning;
            windowEnd = Time.time + t.catchWindowDuration;
            cooldownEnd = windowEnd + t.catchCooldown;
            claimSent = false;
            PatataLog.Bomb($"Catch window open {player} ({t.catchWindowDuration:F2}s)");
            return true;
        }

        /// <summary>
        /// Host only: a remote player pressed catch at server time <paramref name="pressedAt"/> (clamped to the lag cap,
        /// never in the future). Their window covers [press, press + window]; a press inside the cooldown is ignored.
        /// </summary>
        public bool OpenRemoteWindow(double pressedAt)
        {
            var t = player.Tuning;
            if (bomb == null || player.ControlLocked || bomb.Carrier == player) return false;
            double now = SimulationClock.ServerNow;
            double start = HazardRewind.ClampClaim(pressedAt, now, t.catchLagCompensation);
            if (remoteWindowStart != Unset && start < remoteWindowStart + t.catchWindowDuration + t.catchCooldown - t.catchLateGrace) return false;
            remoteWindowStart = start;
            shownWindowStart = start;
            PatataLog.Bomb($"Catch window open {player} (pressed {(now - start) * 1000.0:F0} ms ago)");
            return true;
        }

        /// <summary>
        /// Host: was this remote player's window open at server time <paramref name="time"/>? A press up to the late grace
        /// (+ <paramref name="extraLate"/>, their measured jitter) after it still counts.
        /// </summary>
        public bool RemoteWindowCovers(double time, float extraLate = 0f)
        {
            var t = player.Tuning;
            return remoteWindowStart != Unset && time >= remoteWindowStart - t.catchLateGrace - extraLate && time <= remoteWindowStart + t.catchWindowDuration;
        }

        /// <summary>Everyone: the host relayed that this player's window opened at server time <paramref name="pressedAt"/>.</summary>
        public void MirrorWindow(double pressedAt) => shownWindowStart = pressedAt;

        /// <summary>Presentation: is this player's window open at server time <paramref name="time"/>, as far as this machine knows?</summary>
        public bool ShownWindowOpen(double time) =>
            player.IsLocal ? WindowOpen : shownWindowStart != Unset && time >= shownWindowStart && time <= shownWindowStart + player.Tuning.catchWindowDuration;

        public void Clear()
        {
            windowEnd = -1f;
            cooldownEnd = -1f;
            windowWasOpen = false;
            remoteWindowStart = Unset;
            shownWindowStart = Unset;
        }

        /// <summary>The bomb came within this player's reach (whether or not they were ready): the host's sweep, for its own players.</summary>
        public void NoteBombInReach() => lastReachTime = Time.time;

        /// <summary>Shows a hint on this player's screen for a moment.</summary>
        public void ReceiveHint(string text)
        {
            hint = text;
            hintUntil = Time.time + HintDuration;
            PatataLog.Bomb($"Catch hint for {player}: {text} (rtt {NetMode.RttMs} ms)");
        }

        // The press came after the bomb had already been in reach and left: too late.
        void DiagnoseLatePress()
        {
            if (bomb.State != BombState.Thrown || bomb.LastThrower == player) return;
            float since = Time.time - lastReachTime;
            Vector3 at = bomb.transform.position;
            bool inReachNow = Vector3.Distance(at, player.CatchVolume.CatchCenter) <= CatchResolver.ReachFor(player, bomb.Body.Velocity) + ReachSlack;
            // Within the late grace the press still catches (CatchResolver), so only later presses are "too late".
            if (!inReachNow && since > player.Tuning.catchLateGrace && since < LateMax) ReceiveHint($"Too late: it was in reach {since * 1000f:F0} ms ago");
        }

        // The window closed while the bomb was still coming: too early.
        void CheckMissedEarly()
        {
            bool open = WindowOpen;
            bool justClosed = windowWasOpen && !open;
            windowWasOpen = open;
            if (!justClosed || bomb == null || bomb.State != BombState.Thrown || bomb.LastThrower == player || player.CatchVolume == null) return;

            Vector3 toMe = player.CatchVolume.CatchCenter - bomb.transform.position;
            if (toMe.magnitude < EarlyCheckDistance && Vector3.Dot(bomb.Body.Velocity, toMe) > 0f) ReceiveHint("Too early: it was still on its way");
        }
    }
}
