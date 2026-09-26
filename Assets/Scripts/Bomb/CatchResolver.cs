using UnityEngine;

namespace Beep
{
    /// <summary>
    /// The single place that decides whether contact with a catch volume is a valid catch.
    /// First valid claim wins: the bomb leaves the Thrown state immediately, so a second volume
    /// touched in the same physics step is rejected. Player scripts never decide catches.
    /// </summary>
    [RequireComponent(typeof(BombController))]
    public class CatchResolver : MonoBehaviour
    {
        BombController bomb;

        void Awake() => bomb = GetComponent<BombController>();

        public bool TryResolveCatch(PlayerCatchVolume volume)
        {
            if (bomb.State != BombState.Thrown) return false;

            Player receiver = volume.Owner;
            if (receiver == null) return false;

            // A pass is hand -> flight -> ANOTHER player's catch.
            if (receiver == bomb.LastThrower)
            {
                BeepLog.Bomb($"Catch rejected {receiver} (thrower cannot catch their own throw)");
                return false;
            }

            BeepLog.Bomb($"Catch accepted {receiver}");
            return bomb.AcceptCatch(receiver);
        }
    }
}
