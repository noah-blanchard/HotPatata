using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The run's moments as sounds (ARCHITECTURE §26, #48): the start, a checkpoint reached (an act change instead when it opens
    /// a new act), a section reset, the finish. It watches the run's state and checkpoint, which the host decides and clients
    /// mirror, so every machine hears them; the run's own events fire on the host only. What it sees first (a late join, a
    /// practice start) is never played. Presentation only.
    /// </summary>
    public sealed class CourseSounds : MonoBehaviour
    {
        AudioDirector director;
        RunManager run;
        RunState lastState;
        int lastCheckpoint;

        public void Init(AudioDirector owner) => director = owner;

        void Update()
        {
            var current = RunManager.Instance;
            if (current != run)
            {
                run = current;
                if (run == null) return;
                lastState = run.State;
                lastCheckpoint = CourseAudio.CurrentCheckpointId;
                return;
            }
            if (run == null) return;

            var state = run.State;
            if (state != lastState)
            {
                if (state == RunState.Playing && (lastState == RunState.Initializing || lastState == RunState.WaitingForPlayers))
                    director.Play(CueKeys.CourseStart);
                else if (state == RunState.Resetting) director.Play(CueKeys.CourseReset);
                else if (state == RunState.Completed) director.Play(CueKeys.CourseFinish);
                lastState = state;
            }

            int checkpoint = CourseAudio.CurrentCheckpointId;
            if (checkpoint != lastCheckpoint)
            {
                // Forward only: a new run (back to the start) is silent.
                if (checkpoint > lastCheckpoint && state != RunState.Completed)
                {
                    var map = CourseAudio.Current;
                    bool newAct = map != null && map.ActAt(checkpoint) != map.ActAt(lastCheckpoint);
                    if (!(newAct && director.Play(CueKeys.CourseActChange))) director.Play(CueKeys.CourseCheckpoint);
                }
                lastCheckpoint = checkpoint;
            }
        }
    }
}
