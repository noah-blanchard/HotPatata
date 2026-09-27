using System;
using System.Collections.Generic;
using UnityEngine;

namespace Beep
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
    /// Owns the local run: hands out the bomb, tracks the active checkpoint and the section clock, and turns
    /// any failure (bomb explosion, player fall) into a fast section reset
    /// (lock -> feedback -> reset level objects -> teleport -> reset bomb -> resume).
    /// Also decides course completion.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        [SerializeField] GameTuning tuning;
        [SerializeField] BombController bomb;
        [SerializeField] Player[] players;
        [SerializeField, Tooltip("Fallback spawns used until a checkpoint is activated.")] PlayerSpawn[] spawns;
        [SerializeField, Tooltip("Index into Players of who holds the bomb at the start.")] int startingCarrier;

        readonly List<IResettable> resettables = new List<IResettable>();
        float resetAtTime;
        float sectionStartTime;
        float runStartTime;

        public RunState State { get; private set; } = RunState.Initializing;
        public int ResetCount { get; private set; }
        public IReadOnlyList<Player> Players => players;
        public BombController Bomb => bomb;
        public Checkpoint CurrentCheckpoint { get; private set; }
        /// <summary>Seconds since the current section attempt began; the time base for level motion.</summary>
        public float SectionTime => Time.time - sectionStartTime;
        /// <summary>Total run time; frozen at completion.</summary>
        public float RunTime { get; private set; }

        public event Action<RunState> StateChanged;
        public event Action<Checkpoint> CheckpointActivated;
        public event Action<float> RunCompleted;

        void Awake() => Instance = this;

        void Start()
        {
            foreach (var p in players) p.Bind(bomb, players);
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IResettable r) resettables.Add(r);

            bomb.BombExploded += OnBombExploded;
            runStartTime = Time.time;
            StartSection();
        }

        void OnDestroy()
        {
            if (bomb != null) bomb.BombExploded -= OnBombExploded;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (State == RunState.Playing) RunTime = Time.time - runStartTime;
            if (State == RunState.Failing && Time.time >= resetAtTime) ResetSection();
        }

        void StartSection()
        {
            sectionStartTime = Time.time;
            PlacePlayers();
            bomb.BeginReset();
            bomb.EndReset(CarrierForReset());
            SetPlayersLocked(false);
            Transition(RunState.Playing);
        }

        // ------------------------------------------------------------------ failure / reset

        void OnBombExploded(BombFailReason reason, string detail) => FailSection(reason.ToString(), detail);

        /// <summary>Fails the current section for the whole team. Ignored unless the run is being played.</summary>
        public void FailSection(string reason, string detail)
        {
            if (State != RunState.Playing) return;

            ResetCount++;
            SetPlayersLocked(true);
            resetAtTime = Time.time + tuning.resetDelay;
            Transition(RunState.Failing);
            BeepLog.Run($"Section fail reason={reason} {detail} (reset #{ResetCount})");
        }

        void ResetSection()
        {
            Transition(RunState.Resetting);
            BeepLog.Run("Reset start");

            foreach (var r in resettables) r.ResetState();   // falling platforms etc.
            sectionStartTime = Time.time;                    // moving platforms / rotating bars restart their cycle
            bomb.BeginReset();                               // inert, ownerless, velocity cleared, fuse restored
            PlacePlayers();                                  // teleport + clear player velocity
            bomb.EndReset(CarrierForReset());
            SetPlayersLocked(false);

            Transition(RunState.Playing);
            BeepLog.Run("Reset end");
        }

        // ------------------------------------------------------------------ checkpoints / completion

        public void ActivateCheckpoint(Checkpoint checkpoint)
        {
            if (State != RunState.Playing) return;

            CurrentCheckpoint = checkpoint;
            // Normalise the bomb for the new section: a fresh hold window for whoever has it.
            if (bomb.State == BombState.Held || bomb.State == BombState.CaughtGrace) bomb.Fuse.Refresh();
            BeepLog.Run($"Checkpoint {checkpoint.Id} activated");
            CheckpointActivated?.Invoke(checkpoint);
        }

        /// <summary>Called by FinishZone once every required player is inside. Fires once.</summary>
        public void Complete()
        {
            if (State != RunState.Playing) return;

            RunTime = Time.time - runStartTime;
            SetPlayersLocked(true);
            bomb.BeginReset();   // inert: nothing can explode after the finish
            Transition(RunState.Completed);
            BeepLog.Run($"Course complete time={RunTime:F1}s resets={ResetCount}");
            RunCompleted?.Invoke(RunTime);
        }

        // ------------------------------------------------------------------ helpers

        Player CarrierForReset()
        {
            int slot = CurrentCheckpoint != null ? CurrentCheckpoint.CarrierSlot : startingCarrier;
            foreach (var p in players)
                if (p.PlayerId == slot) return p;
            return players[0];
        }

        void PlacePlayers()
        {
            foreach (var p in players)
            {
                var spawn = FindSpawn(p.PlayerId);
                if (spawn != null) p.TeleportTo(spawn.transform.position, spawn.transform.rotation);
            }
        }

        PlayerSpawn FindSpawn(int slot)
        {
            if (CurrentCheckpoint != null)
            {
                var s = CurrentCheckpoint.FindSpawn(slot);
                if (s != null) return s;
            }
            foreach (var s in spawns)
                if (s.Slot == slot) return s;
            return null;
        }

        void SetPlayersLocked(bool locked)
        {
            foreach (var p in players) p.SetControlLocked(locked);
        }

        void Transition(RunState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
