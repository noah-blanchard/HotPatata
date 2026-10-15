using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// A geometric walk check of PatataRanger, run at the end of every build (the motor's own run is the route runner's job, in the
    /// tests): the shared <see cref="WalkProbe"/> along each walkway and rope bridge the builder laid.
    /// </summary>
    public static partial class PatataRangerBuilder
    {
        static readonly List<(string name, List<Vector3> path, float width)> Walks = new List<(string, List<Vector3>, float)>();

        /// <summary>The problems found (empty: every walk is joined and clear).</summary>
        static List<string> ProbeWalks() => WalkProbe.Run(Walks.Select(w => (w.name, (IList<Vector3>)w.path))).Select(p => p.message).ToList();
    }
}
