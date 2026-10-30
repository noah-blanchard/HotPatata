using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A cave's mouth on a hillside (M21, ARCHITECTURE §27.6): the parts made together when it was placed (its tunnel, the lintel of rock
    /// over it with its scree and talus, the cave's ambience, two lanterns), each a child the author edits as usual. Deleting the mouth
    /// deletes them. Data only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaveMouth : MonoBehaviour
    {
        [SerializeField, Tooltip("The tunnel it opens (a child).")] RockShellShape tunnel;
        [SerializeField, Tooltip("The rock over its top (a child).")] CliffShape lintel;
        [SerializeField, Tooltip("The cave's ambience over its first metres (a child).")] AmbienceZone ambience;
        [SerializeField, Tooltip("The lanterns inside (children).")] List<GameObject> lanterns = new List<GameObject>();

        public RockShellShape Tunnel => tunnel;
        public CliffShape Lintel => lintel;
        public AmbienceZone Ambience => ambience;
        public List<GameObject> Lanterns => lanterns;

        /// <summary>Editor: the parts just made.</summary>
        public void Set(RockShellShape tunnel, CliffShape lintel, AmbienceZone ambience)
        {
            this.tunnel = tunnel;
            this.lintel = lintel;
            this.ambience = ambience;
        }
    }
}
