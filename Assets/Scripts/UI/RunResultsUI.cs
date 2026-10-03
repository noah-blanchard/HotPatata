using UnityEngine;
using UnityEngine.InputSystem;

namespace HotPatata
{
    /// <summary>
    /// Shows the <see cref="ResultsScreen"/> (#23) while the run is complete, from the replicated run state, so every
    /// player sees it; closes it when the run starts again. The host (or an offline player) can also rematch with R.
    /// Placed in each course scene; it draws nothing itself.
    /// </summary>
    public class RunResultsUI : MonoBehaviour
    {
        ResultsScreen shown;

        void Update()
        {
            var run = RunManager.Instance;
            bool completed = run != null && run.State == RunState.Completed;
            if (completed && shown == null)
            {
                shown = new ResultsScreen(run);
                ScreenStack.Get().Push(shown);
            }
            else if (!completed && shown != null)
            {
                var stack = ScreenStack.Existing;
                if (stack != null && stack.Top == shown) stack.Pop();
                shown = null;
            }

            if (completed && NetMode.IsAuthority && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) run.Restart();
        }

        void OnDestroy()
        {
            var stack = ScreenStack.Existing;
            if (shown != null && stack != null && stack.Top == shown) stack.Pop();
        }
    }
}
