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
    /// Owns the local run: gives the bomb to the starting carrier, and turns any bomb explosion
    /// into a fast section reset (lock -> explosion feedback -> teleport -> reset bomb -> resume).
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        [SerializeField] GameTuning tuning;
        [SerializeField] BombController bomb;
        [SerializeField] Player[] players;
        [SerializeField] PlayerSpawn[] spawns;
        [SerializeField, Tooltip("Index into Players of who holds the bomb at the start of a section.")]
        int startingCarrier;

        float resetAtTime;

        public RunState State { get; private set; } = RunState.Initializing;
        public int ResetCount { get; private set; }
        public IReadOnlyList<Player> Players => players;
        public BombController Bomb => bomb;

        public event Action<RunState> StateChanged;

        void Start()
        {
            foreach (var p in players) p.Bind(bomb, players);
            bomb.BombExploded += OnBombExploded;
            StartSection();
        }

        void OnDestroy()
        {
            if (bomb != null) bomb.BombExploded -= OnBombExploded;
        }

        void Update()
        {
            if (State == RunState.Failing && Time.time >= resetAtTime) ResetSection();
        }

        void StartSection()
        {
            PlacePlayers();
            bomb.BeginReset();
            bomb.EndReset(players[startingCarrier]);
            SetPlayersLocked(false);
            Transition(RunState.Playing);
        }

        void OnBombExploded(BombFailReason reason, string detail)
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

            bomb.BeginReset();                       // inert, ownerless, velocity cleared, fuse restored
            PlacePlayers();                          // teleport + clear player velocity
            bomb.EndReset(players[startingCarrier]);
            SetPlayersLocked(false);

            Transition(RunState.Playing);
            BeepLog.Run("Reset end");
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
