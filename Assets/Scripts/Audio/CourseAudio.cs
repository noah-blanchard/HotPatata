using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A course's audio map (ARCHITECTURE §26), written by its builder: for each checkpoint (the section the team runs from it),
    /// the act it is in and the environment it sounds like. Music and ambience change act by act; the ambience environment
    /// changes section by section (an <see cref="AmbienceZone"/> can still override it locally). Data only.
    /// A scene without one is a single act with no environment: its course folders still play.
    /// </summary>
    public sealed class CourseAudio : MonoBehaviour
    {
        public static CourseAudio Current { get; private set; }

        [SerializeField, Tooltip("Act of the section run from each checkpoint id (0 = the start). Empty = act 1 everywhere.")]
        int[] acts = new int[0];
        [SerializeField, Tooltip("Environment (Ambience/Environments/<name>) of the section run from each checkpoint id. Empty = the default.")]
        string[] environments = new string[0];
        [SerializeField, Tooltip("Environment where the list says nothing.")] string defaultEnvironment = "";

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        /// <summary>The act (1, 2...) the team is in once it has reached <paramref name="checkpointId"/>.</summary>
        public int ActAt(int checkpointId) => acts.Length == 0 ? 1 : Mathf.Max(1, acts[Mathf.Clamp(checkpointId, 0, acts.Length - 1)]);

        /// <summary>The environment of the section run from <paramref name="checkpointId"/> ("" = none).</summary>
        public string EnvironmentAt(int checkpointId)
        {
            if (environments.Length == 0) return defaultEnvironment;
            var e = environments[Mathf.Clamp(checkpointId, 0, environments.Length - 1)];
            return string.IsNullOrEmpty(e) ? defaultEnvironment : e;
        }

        /// <summary>The act the team is in now: 1 outside a course or before its run has a checkpoint.</summary>
        public static int CurrentAct => Current != null ? Current.ActAt(CurrentCheckpointId) : 1;

        /// <summary>The checkpoint the run restarts from (host-decided, mirrored on clients), 0 at the start.</summary>
        public static int CurrentCheckpointId
        {
            get
            {
                var run = RunManager.Instance;
                var cp = run != null ? run.CurrentCheckpoint : null;
                return cp != null ? cp.Id : 0;
            }
        }

        /// <summary>Editor only (course builders).</summary>
        public void Configure(int[] acts, string[] environments, string defaultEnvironment)
        {
            this.acts = acts ?? new int[0];
            this.environments = environments ?? new string[0];
            this.defaultEnvironment = defaultEnvironment ?? "";
        }
    }
}
