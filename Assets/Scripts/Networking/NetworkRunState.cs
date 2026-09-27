using Unity.Netcode;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Replicates the run: state, reset count, the section clock start, run time and the active checkpoint.
    /// The host's RunManager pushes; remote RunManagers read. Because the section clock start is a server
    /// time, moving platforms and rotating bars are in the same place on every machine.
    /// </summary>
    public class NetworkRunState : NetworkBehaviour
    {
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
    }
}
