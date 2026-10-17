using Unity.Netcode;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Replicates the run: state, reset count, the section clock start, run time and the active checkpoint.
    /// The host's RunManager pushes; remote RunManagers read. Because the section clock start is a server
    /// time, moving platforms and rotating bars are in the same place on every machine.
    ///
    /// A section (re)start also travels as ONE reliable <see cref="ResetEvent"/> (#117 P4): the new section start, every
    /// player's spawn, and the server time at which everyone unlocks, applied in one block on every machine. NGO does not
    /// order an RPC against a variable, so a client never respawns against the old platform phase.
    /// </summary>
    public class NetworkRunState : NetworkBehaviour
    {
        public struct Spawn : INetworkSerializable
        {
            public int Slot;
            public Vector3 Position;
            public float Yaw;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Slot);
                serializer.SerializeValue(ref Position);
                serializer.SerializeValue(ref Yaw);
            }
        }

        /// <summary>A section (re)start: when it starts (= when everyone unlocks), and where each player respawns.</summary>
        public struct ResetEvent : INetworkSerializable
        {
            public int Resets;
            public double SectionStart;
            public double UnlockAt;
            public int Count;
            public Spawn S0, S1, S2, S3;

            public Spawn this[int i] => i == 0 ? S0 : i == 1 ? S1 : i == 2 ? S2 : S3;

            public void Add(Spawn s)
            {
                switch (Count)
                {
                    case 0: S0 = s; break;
                    case 1: S1 = s; break;
                    case 2: S2 = s; break;
                    case 3: S3 = s; break;
                    default: return;
                }
                Count++;
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Resets);
                serializer.SerializeValue(ref SectionStart);
                serializer.SerializeValue(ref UnlockAt);
                serializer.SerializeValue(ref Count);
                if (Count > 0) serializer.SerializeValue(ref S0);
                if (Count > 1) serializer.SerializeValue(ref S1);
                if (Count > 2) serializer.SerializeValue(ref S2);
                if (Count > 3) serializer.SerializeValue(ref S3);
            }
        }

        readonly NetworkVariable<int> state = new NetworkVariable<int>();
        readonly NetworkVariable<int> resetCount = new NetworkVariable<int>();
        readonly NetworkVariable<double> sectionStart = new NetworkVariable<double>();
        readonly NetworkVariable<float> runTime = new NetworkVariable<float>();
        readonly NetworkVariable<int> checkpointId = new NetworkVariable<int>(-1);

        float lastRunTimeSent = -1f;
        int lastStateSent = -1;

        public RunState State => (RunState)state.Value;
        public int ResetCount => resetCount.Value;
        public double SectionStart => sectionStart.Value;
        public float RunTime => runTime.Value;
        public int CheckpointId => checkpointId.Value;

        /// <summary>Host only.</summary>
        public void Push(RunState s, int resets, double sectionStartServerTime, float time, int checkpoint)
        {
            if (!IsServer) return;
            state.Value = (int)s;
            resetCount.Value = resets;
            sectionStart.Value = sectionStartServerTime;
            checkpointId.Value = checkpoint;
            bool stateChanged = (int)s != lastStateSent;
            lastStateSent = (int)s;
            if (stateChanged || Mathf.Abs(time - lastRunTimeSent) >= 0.25f)
            {
                lastRunTimeSent = time;
                runTime.Value = time;
            }
        }

        /// <summary>Host only: every client applies this section (re)start in one block.</summary>
        public void SendReset(ResetEvent e)
        {
            if (IsServer) ResetRpc(e);
        }

        [Rpc(SendTo.NotServer)]
        void ResetRpc(ResetEvent e)
        {
            if (RunManager.Instance != null) RunManager.Instance.ApplyResetEvent(e);
        }
    }
}
