using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Marks an authored level's safety net (ARCHITECTURE §27): the kill zone under the whole level, kept under the terrain's lowest
    /// point and wider than it by the editor on every save. Nothing falls out of the world.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelSafetyNet : MonoBehaviour
    {
    }
}
