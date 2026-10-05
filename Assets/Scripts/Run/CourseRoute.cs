using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Level-design metadata (PROJECT_SPEC §15b): the intended route of a generated course as a polyline in world space,
    /// from the start through every turn, climb and drop to the finish, with the waypoints that are out in the open. The
    /// course tests measure its shape (turns, climb, descent, extent) and check that indoor waypoints have a ceiling.
    /// Gameplay never reads it.
    /// </summary>
    public class CourseRoute : MonoBehaviour
    {
        [SerializeField] Vector3[] points = System.Array.Empty<Vector3>();
        [SerializeField] bool[] outdoors = System.Array.Empty<bool>();

        public IReadOnlyList<Vector3> Points => points;

        public bool Outdoors(int index) => index >= 0 && index < outdoors.Length && outdoors[index];

        /// <summary>Editor builders.</summary>
        public void Configure(Vector3[] routePoints, bool[] routeOutdoors)
        {
            points = routePoints;
            outdoors = routeOutdoors;
        }
    }
}
