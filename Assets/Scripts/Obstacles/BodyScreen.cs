using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A screen of brambles, a net or a web (PROJECT_SPEC §13.18): it stops every player's body but never the bomb, the
    /// mirror image of a laser curtain. Pure physics, no rule runs here: its colliders sit on the <c>BodyScreen</c> layer,
    /// which collides with <c>Player</c> only, so the bomb, the aim ray and the catch reach pass through, and the motor's
    /// ledge test (Environment / Hazard) never mantles onto it. It is how an opening becomes "bomb only".
    /// Switched off and on as a whole by a <see cref="SignalSwitch"/> that targets it.
    /// </summary>
    public class BodyScreen : MonoBehaviour, IObstacleState
    {
        public const string LayerName = "BodyScreen";

        float IObstacleState.Progress => isActiveAndEnabled ? 1f : 0f;
        bool IObstacleState.Active => isActiveAndEnabled;
    }
}
