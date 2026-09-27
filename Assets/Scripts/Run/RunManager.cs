using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    public enum RunState
    {
        Initializing,
        WaitingForPlayers,
        Playing,
        Failing,
        Resetting,
        Completed
    }

    /// <summary>
    /// Owns the run: hands out the bomb, tracks the active checkpoint and the section clock, and turns
    /// any failure (bomb explosion, player fall) into a fast section reset
    /// (lock -> feedback -> reset level objects -> teleport -> reset bomb -> resume). Also decides completion.
    ///
    /// Only the authority (offline, or the host online) runs these rules. Remote clients read the replicated
    /// <see cref="NetworkRunState"/> through the same properties.
    /// </summary>
    [DefaultExecutionOrder(100)]   // after players and the bomb have started
    public class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        [SerializeField] GameTuning tuning;
        [SerializeField] BombController bomb;
        [SerializeField, Tooltip("Fallback spawns used until a checkpoint is activated.")] PlayerSpawn[] spawns;
        [SerializeField, Tooltip("Player slot that holds the bomb at the start.")] int startingCarrier;
        [SerializeField, Min(1), Tooltip("The section starts once this many players are present.")] int playersToStart = 2;
        [SerializeField] NetworkRunState netState;

        readonly List<IResettable> resettables = new List<IResettable>();
        readonly HashSet<Player> placed = new HashSet<Player>();
        Checkpoint[] checkpoints = Array.Empty<Checkpoint>();
        Checkpoint checkpoint;
        RunState state = RunState.Initializing;
        int resetCount;
        double sectionStart;
        double runStart;
        float runTime;
        float resetAtTime;

        bool Mirror => NetMode.IsRemoteClient && netState != null && netState.IsSpawned;

        public RunState State => Mirror ? netState.State : state;
        public int ResetCount => Mirror ? netState.ResetCount : resetCount;
        /// <summary>Seconds since the current section attempt began; the time base for level motion.</summary>
        public float SectionTime => (float)(NetMode.ServerTime - (Mirror ? netState.SectionStart : sectionStart));
        /// <summary>Total run time; frozen at completion.</summary>
        public float RunTime => Mirror ? netState.RunTime : runTime;
        public Checkpoint CurrentCheckpoint => Mirror ? CheckpointById(netState.CheckpointId) : checkpoint;
        public IReadOnlyList<Player> Players => Player.All;
        public BombController Bomb => bomb;

        public event Action<RunState> StateChanged;
        public event Action<Checkpoint> CheckpointActivated;
        public event Action<float> RunCompleted;

        void Awake() => Instance = this;

        void Start()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IResettable r) resettables.Add(r);
            // Cached once: remote clients resolve the replicated checkpoint id every frame.
            checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);

            bomb.BombExploded += OnBombExploded;
            if (NetMode.IsAuthority) Transition(RunState.WaitingForPlayers);
        }

        void OnDestroy()
        {
            if (bomb != null) bomb.BombExploded -= OnBombExploded;
            if (Instance == this) Instance = null;
        }

        RunState lastMirroredState = RunState.Initializing;

        void Update()
        {
            if (!NetMode.IsAuthority)
            {
                // Remote clients only mirror; log what the host tells us (handy when comparing logs).
                if (Mirror && State != lastMirroredState)
                {
                    lastMirroredState = State;
                    PatataLog.Run($"(mirror) run state = {State} resets={ResetCount} time={RunTime:F1}s");
                }
                return;
            }

            switch (state)
            {
                case RunState.WaitingForPlayers:
                    if (Players.Count >= playersToStart) StartSection();
                    break;

                case RunState.Playing:
                    runTime = (float)(NetMode.ServerTime - runStart);
                    PlaceLateJoiners();
                    if (CarrierLeft()) FailSection("CarrierLeft", "the bomb holder disconnected");
                    break;

                case RunState.Failing:
                    if (Time.time >= resetAtTime) ResetSection();
                    break;
            }

            if (netState != null && netState.IsSpawned)
                netState.Push(state, resetCount, sectionStart, runTime, checkpoint != null ? checkpoint.Id : -1);
        }

        void StartSection()
        {
            runStart = NetMode.ServerTime;
            sectionStart = runStart;
            PlacePlayers();
            bomb.BeginReset();
            bomb.EndReset(CarrierForReset());
            SetPlayersLocked(false);
            Transition(RunState.Playing);
            PatataLog.Run($"Run started with {Players.Count} players");
        }

        // ------------------------------------------------------------------ failure / reset

        void OnBombExploded(BombFailReason reason, string detail)
        {
            if (NetMode.IsAuthority) FailSection(reason.ToString(), detail);
        }

        /// <summary>Fails the current section for the whole team. Ignored unless the run is being played.</summary>
        public void FailSection(string reason, string detail)
        {
            if (!NetMode.IsAuthority || state != RunState.Playing) return;

            resetCount++;
            SetPlayersLocked(true);
            resetAtTime = Time.time + tuning.resetDelay;
            Transition(RunState.Failing);
            PatataLog.Run($"Section fail reason={reason} {detail} (reset #{resetCount})");
        }

        void ResetSection()
        {
            if (Players.Count == 0)
            {
                // Everybody left during the failure delay: wait for players instead of handing the bomb to nobody.
                bomb.BeginReset();
                Transition(RunState.WaitingForPlayers);
                return;
            }

            Transition(RunState.Resetting);
            PatataLog.Run("Reset start");

            foreach (var r in resettables) r.ResetState();   // falling platforms etc.
            sectionStart = NetMode.ServerTime;               // moving platforms / rotating bars restart their cycle
            bomb.BeginReset();                               // inert, ownerless, velocity cleared, fuse restored
            PlacePlayers();                                  // teleport + clear player velocity
            bomb.EndReset(CarrierForReset());
            SetPlayersLocked(false);

            Transition(RunState.Playing);
            PatataLog.Run("Reset end");
        }

        // ------------------------------------------------------------------ checkpoints / completion

        public void ActivateCheckpoint(Checkpoint cp)
        {
            if (!NetMode.IsAuthority || state != RunState.Playing) return;
            // Checkpoint ids increase along the course; walking back through an earlier one must not move the respawn back.
            if (checkpoint != null && cp.Id <= checkpoint.Id) return;

            checkpoint = cp;
            // Normalise the bomb for the new section: this checkpoint's hold time, and a fresh window for whoever has it.
            bomb.Fuse.SetDurationOverride(cp.HoldFuseOverride);
            PatataLog.Run($"Checkpoint {cp.Id} activated");
            CheckpointActivated?.Invoke(cp);
        }

        /// <summary>Called by FinishZone once every required player is inside. Fires once.</summary>
        public void Complete()
        {
            if (!NetMode.IsAuthority || state != RunState.Playing) return;

            runTime = (float)(NetMode.ServerTime - runStart);
            SetPlayersLocked(true);
            bomb.BeginReset();   // inert: nothing can explode after the finish
            Transition(RunState.Completed);
            PatataLog.Run($"Course complete time={runTime:F1}s resets={resetCount}");
            RunCompleted?.Invoke(runTime);
        }

        /// <summary>Starts the whole run again from the beginning (rematch). Authority only.</summary>
        public void Restart()
        {
            if (!NetMode.IsAuthority || (state != RunState.Completed && state != RunState.Playing) || Players.Count == 0) return;

            checkpoint = null;
            resetCount = 0;
            foreach (var cp in checkpoints) cp.Rearm();
            foreach (var r in resettables) r.ResetState();
            bomb.Fuse.SetDurationOverride(0f);
            placed.Clear();
            runStart = NetMode.ServerTime;
            sectionStart = runStart;
            runTime = 0f;
            PlacePlayers();
            bomb.BeginReset();
            bomb.EndReset(CarrierForReset());
            SetPlayersLocked(false);
            Transition(RunState.Playing);
            PatataLog.Run("Run restarted");
        }

        // ------------------------------------------------------------------ helpers

        Player CarrierForReset()
        {
            int slot = checkpoint != null ? checkpoint.CarrierSlot : startingCarrier;
            foreach (var p in Players)
                if (p.PlayerId == slot) return p;
            return Players[0];
        }

        bool CarrierLeft() =>
            (bomb.State == BombState.Held || bomb.State == BombState.CaughtGrace) && bomb.Carrier == null;

        void PlacePlayers()
        {
            foreach (var p in Players)
            {
                PlaceOne(p);
                placed.Add(p);
            }
        }

        /// <summary>A player who connects while the run is in progress starts at their slot's spawn.</summary>
        void PlaceLateJoiners()
        {
            foreach (var p in Players)
            {
                if (placed.Contains(p)) continue;
                PlaceOne(p);
                p.SetControlLocked(false);
                placed.Add(p);
            }
        }

        void PlaceOne(Player p)
        {
            var spawn = FindSpawn(p.PlayerId);
            if (spawn != null) p.TeleportTo(spawn.transform.position, spawn.transform.rotation);
        }

        PlayerSpawn FindSpawn(int slot)
        {
            if (checkpoint != null)
            {
                var s = checkpoint.FindSpawn(slot);
                if (s != null) return s;
            }
            foreach (var s in spawns)
                if (s.Slot == slot) return s;
            return null;
        }

        Checkpoint CheckpointById(int id)
        {
            if (id < 0) return null;
            foreach (var c in checkpoints)
                if (c != null && c.Id == id) return c;
            return null;
        }

        void SetPlayersLocked(bool locked)
        {
            foreach (var p in Players) p.SetControlLocked(locked);
        }

        void Transition(RunState next)
        {
            if (state == next) return;
            state = next;
            StateChanged?.Invoke(next);
        }
    }
}
