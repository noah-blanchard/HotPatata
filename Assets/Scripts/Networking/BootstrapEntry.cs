using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Lives in the Bootstrap scene. Makes sure the persistent network object (NetworkManager + transport +
    /// NetworkBootstrap) exists exactly once, however many times this scene is loaded (e.g. after leaving a game).
    /// It is also the game's entry, so it loads the player's saved <see cref="Settings"/> the first time.
    /// </summary>
    public class BootstrapEntry : MonoBehaviour
    {
        [SerializeField] GameObject networkPrefab;
        [SerializeField, Tooltip("Defaults for settings the player never changed.")] GameTuning tuning;

        void Awake()
        {
            Settings.LoadOnce(tuning);
            if (NetworkBootstrap.Instance == null) Instantiate(networkPrefab);
        }
    }
}
