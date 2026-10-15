using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The lines a generated course is walked along (each walkway, stair and bridge its builder laid, on its walking surface), kept with
    /// the course for the route runner (the course tests drive a player along each, PatataRangerTests) and for tools. No gameplay reads it.
    /// </summary>
    public class RouteWalks : MonoBehaviour
    {
        [Serializable]
        public class Line
        {
            public string name;
            public Vector3[] points = Array.Empty<Vector3>();
        }

        [SerializeField] List<Line> lines = new List<Line>();

        public IReadOnlyList<Line> Lines => lines;

        /// <summary>Editor builders.</summary>
        public void Configure(List<Line> walkLines) => lines = walkLines;
    }
}
