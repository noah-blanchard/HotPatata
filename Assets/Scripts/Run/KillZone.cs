using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Marker for a lethal volume (trigger). A bomb entering it explodes, so a bomb can never
    /// fall out of the level and disappear. Player behaviour is added with the Milestone 2 prefab kit.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KillZone : MonoBehaviour
    {
        void Reset() => GetComponent<Collider>().isTrigger = true;

        void Awake() => GetComponent<Collider>().isTrigger = true;
    }
}
