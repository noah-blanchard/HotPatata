using UnityEngine;
using UnityEngine.Events;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (docs/OBSTACLES.md §4): plays any visual from an obstacle's state (<see cref="IObstacleState"/>) without
    /// code. Every frame it writes the state's progress and "on" flag into an Animator's parameters and raises events when the
    /// system switches on or off, on every machine, from state that is already replicated. It never changes the state, so a
    /// variant can swap in any model and animate it (a cannon's recoil, a door's lamps, a crumbling plank) on top of the
    /// unchanged logic.
    /// </summary>
    public class ObstacleVisualDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("The system to read (a MovingPlatform, SignalActuator, FallingPlatform, RotatingObstacle, BombTransit, " +
                                 "BombGate, PressurePlate or Checkpoint). Empty: the first one on this object or its parents.")]
        MonoBehaviour source;
        [SerializeField, Tooltip("Optional: an Animator to drive.")] Animator animator;
        [SerializeField, Tooltip("Float parameter set to the progress (empty: none).")] string progressParameter = "Progress";
        [SerializeField, Tooltip("Bool parameter set to the on flag (empty: none).")] string activeParameter = "Active";
        [Tooltip("Raised when the system switches on (not on the first frame).")] public UnityEvent onActivated = new UnityEvent();
        [Tooltip("Raised when the system switches off (not on the first frame).")] public UnityEvent onDeactivated = new UnityEvent();
        [Tooltip("Raised every frame with the progress (0..1).")] public UnityEvent<float> onProgress = new UnityEvent<float>();

        IObstacleState state;
        int progressHash, activeHash;
        bool hasProgress, hasActive, started, wasActive;

        public IObstacleState State => state;

        void OnValidate()
        {
            if (source != null && source is not IObstacleState)
            {
                Debug.LogWarning($"{name}: the source must be an obstacle system (IObstacleState)", this);
                source = null;
            }
        }

        void Awake()
        {
            state = source as IObstacleState ?? FindState(transform);
            if (animator != null)
            {
                foreach (var p in animator.parameters)
                {
                    if (p.name == progressParameter && p.type == AnimatorControllerParameterType.Float) hasProgress = true;
                    if (p.name == activeParameter && p.type == AnimatorControllerParameterType.Bool) hasActive = true;
                }
                progressHash = Animator.StringToHash(progressParameter);
                activeHash = Animator.StringToHash(activeParameter);
            }
        }

        static IObstacleState FindState(Transform t)
        {
            for (; t != null; t = t.parent)
                foreach (var c in t.GetComponents<MonoBehaviour>())
                    if (c is IObstacleState s && c is not ObstacleVisualDriver) return s;
            return null;
        }

        void LateUpdate()
        {
            if (state == null) return;
            float progress = state.Progress;
            bool active = state.Active;
            if (animator != null && animator.isActiveAndEnabled)
            {
                if (hasProgress) animator.SetFloat(progressHash, progress);
                if (hasActive) animator.SetBool(activeHash, active);
            }
            onProgress.Invoke(progress);
            if (started && active != wasActive) (active ? onActivated : onDeactivated).Invoke();
            wasActive = active;
            started = true;
        }
    }
}
