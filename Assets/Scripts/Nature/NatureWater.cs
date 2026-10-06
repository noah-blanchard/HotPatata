using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Marks a PatataWilds water surface (ARCHITECTURE §25.3). Water is lethal: its builder always lays a <see cref="KillZone"/>
    /// just under it (checked by <c>PatataWildsTests</c>). Presentation only: the surface itself has no collider.
    /// </summary>
    [DisallowMultipleComponent]
    public class NatureWater : MonoBehaviour
    {
    }
}
