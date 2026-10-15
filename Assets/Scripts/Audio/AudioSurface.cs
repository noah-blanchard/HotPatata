using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Says which footstep folder (SFX/Player/Footsteps/&lt;surface&gt;) a collider and its children sound like, when the material
    /// drawn on it does not say it already (the sound bank maps materials to surfaces, ARCHITECTURE §26). Presentation only.
    /// </summary>
    public sealed class AudioSurface : MonoBehaviour
    {
        [SerializeField, Tooltip("A footstep folder name: Grass, Wood, Rock, Metal... (see CueKeys.Surfaces).")]
        string surface = "Default";

        public string Surface => surface;

        /// <summary>Editor only (builders).</summary>
        public void Configure(string surface) => this.surface = surface;
    }
}
