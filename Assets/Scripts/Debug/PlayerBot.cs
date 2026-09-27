using UnityEngine;

namespace Beep
{
    /// <summary>
    /// A simple teammate bot for automated multiplayer runs: when it holds the bomb it aims at the other player
    /// and passes it after a short hold; when a bomb is flying toward it, it presses catch as it gets close.
    /// It plays through the same input path (and therefore the same network rules) as a human.
    /// Enabled with the -beepBot command-line flag.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerBot : MonoBehaviour
    {
        public static bool Enabled;

        [SerializeField, Min(0f)] float holdBeforeThrow = 0.8f;
        [SerializeField, Min(0.5f)] float catchWhenWithin = 3.0f;

        Player player;
        float heldSince = -1f;

        void Awake() => player = GetComponent<Player>();

        void Update()
        {
            var bomb = BombController.Instance;
            var input = player.Input.Scripted;
            if (bomb == null || input == null || player.ControlLocked) return;

            Player other = null;
            foreach (var p in Player.All)
                if (p != player) { other = p; break; }
            if (other == null) return;

            // Holding it: aim at the teammate and pass.
            if (bomb.Carrier == player && bomb.State == BombState.Held)
            {
                if (heldSince < 0f) heldSince = Time.time;
                if (Time.time - heldSince >= holdBeforeThrow)
                {
                    AimAt(other.CatchVolume.CatchCenter);
                    input.PressThrow();
                    heldSince = -1f;
                }
            }
            else
            {
                heldSince = -1f;
            }

            // Incoming: catch as it arrives.
            if (bomb.State == BombState.Thrown && bomb.LastThrower != player &&
                Vector3.Distance(bomb.transform.position, player.CatchVolume.CatchCenter) < catchWhenWithin)
            {
                input.PressCatch();
            }
        }

        void AimAt(Vector3 target)
        {
            Vector3 d = target - player.CameraTarget.position;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            player.Look.SetAim(yaw, pitch);
        }
    }
}
