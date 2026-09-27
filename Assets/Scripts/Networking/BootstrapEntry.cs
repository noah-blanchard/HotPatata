using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Lives in the Bootstrap scene. Makes sure the persistent network object (NetworkManager + transport +
    /// NetworkBootstrap) exists exactly once, however many times this scene is loaded (e.g. after leaving a game).
    /// </summary>
    public class BootstrapEntry : MonoBehaviour
    {
        [SerializeField] GameObject networkPrefab;

        void Awake()
        {
            if (NetworkBootstrap.Instance == null) Instantiate(networkPrefab);
        }
    }
}
