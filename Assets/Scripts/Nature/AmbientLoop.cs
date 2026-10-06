using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (ARCHITECTURE §25.3): starts a world ambience loop (river, falls, birds, wind) at a random point, so
    /// neighbouring sources playing the same clip never sound in step. Never a movement sound (AGENTS).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbientLoop : MonoBehaviour
    {
        void Start()
        {
            var source = GetComponent<AudioSource>();
            if (source.clip == null) return;
            source.time = Random.Range(0f, source.clip.length);
            if (!source.isPlaying) source.Play();
        }
    }
}
