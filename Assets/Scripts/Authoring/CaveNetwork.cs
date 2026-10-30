using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A cave network of an authored level (M21, ARCHITECTURE §27.6): its nodes (chambers: child caverns and shafts) and its links (child
    /// tunnels whose ends are attached to two nodes, <see cref="CaveAttach"/>: moving a chamber moves its tunnels' ends). It draws nothing
    /// itself: each child is an ordinary rock piece, and can be detached to stand on its own. Data only: the editor edits it.
    /// </summary>
    [SelectionBase]
    public sealed class CaveNetwork : LevelPiece
    {
        [SerializeField, Range(0f, 8f), Tooltip("A new link's flare: its last metres widen where it opens into a chamber.")] float flare = 4f;
        [SerializeField, Min(2f), Tooltip("A new link's clear width (m).")] float linkWidth = 5f;
        [SerializeField, Min(2.2f), Tooltip("A new link's clear height (m).")] float linkHeight = 4.5f;

        public float Flare { get => flare; set => flare = Mathf.Clamp(value, 0f, 8f); }
        public float LinkWidth { get => linkWidth; set => linkWidth = Mathf.Max(2f, value); }
        public float LinkHeight { get => linkHeight; set => linkHeight = Mathf.Max(2.2f, value); }

        /// <summary>The chambers (child caverns and shafts).</summary>
        public IEnumerable<RockShellShape> Nodes => GetComponentsInChildren<RockShellShape>(true).Where(s => s.Kind != RockShellKind.Tunnel && s.transform.parent == transform);

        /// <summary>The tunnels (child tunnels).</summary>
        public IEnumerable<RockShellShape> Links => GetComponentsInChildren<RockShellShape>(true).Where(s => s.Kind == RockShellKind.Tunnel && s.transform.parent == transform);
    }
}
