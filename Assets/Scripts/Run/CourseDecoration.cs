using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Marks a collider-free decoration root on a generated course (presentation only, ARCHITECTURE §4): it never takes
    /// part in gameplay and keeps out of every <see cref="PassCorridor"/> (checked by the course tests).
    /// </summary>
    [DisallowMultipleComponent]
    public class CourseDecoration : MonoBehaviour
    {
    }
}
