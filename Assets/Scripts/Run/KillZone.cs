using UnityEngine;

namespace Beep
{
    /// <summary>
    /// A lethal volume (trigger). A bomb entering it explodes (handled by BombController), so a bomb can
    /// never fall out of the level and vanish. A player entering it fails the section for the whole team,
    /// which resets everyone to the checkpoint: deterministic, and never a soft-lock.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KillZone : MonoBehaviour
    {
        void Reset() => GetComponent<Collider>().isTrigger = true;

        void Awake() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<Player>();
            if (player != null && RunManager.Instance != null)
                RunManager.Instance.FailSection("PlayerFell", $"player={player} zone={name}");
        }
    }
}
