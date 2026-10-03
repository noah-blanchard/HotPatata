using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The local player's Pause action (Esc, gamepad Start) opens and closes the pause menu (#16, <see cref="PauseMenu"/>).
    /// Owner-only, like the rest of the player's input.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerPause : MonoBehaviour
    {
        Player player;

        void Awake() => player = GetComponent<Player>();

        void Update()
        {
            if (player.IsLocal && player.Input != null && player.Input.PausePressed) PauseMenu.Toggle(player.Tuning);
        }
    }
}
