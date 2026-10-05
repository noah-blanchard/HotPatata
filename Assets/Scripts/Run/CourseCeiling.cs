using UnityEngine;

namespace HotPatata
{
    /// <summary>Marks structural ceilings for enclosed-course clearance checks (ARCHITECTURE §4).
    /// Gates and moving hazards have separate pass-window checks; gameplay still uses Environment colliders.</summary>
    public sealed class CourseCeiling : MonoBehaviour { }
}
