using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Development only, offline only: solo pass practice. F4 turns every player you are NOT controlling into a
    /// <see cref="PlayerBot"/> that catches your passes and throws back; F5 cycles how they move while waiting
    /// (stand, strafe, jump, run across). Tab still switches which player you control.
    /// </summary>
    public class PassPartner : MonoBehaviour
    {
        public static bool Active { get; private set; }

        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && !NetMode.IsNetworked)
            {
                if (keyboard.f4Key.wasPressedThisFrame) Active = !Active;
                if (keyboard.f5Key.wasPressedThisFrame)
                    PlayerBot.Movement = (PlayerBot.Pattern)(((int)PlayerBot.Movement + 1) % 4);
            }
            if (NetMode.IsNetworked || PlayerBot.Enabled || LocalPlayerSwitcher.SuppressAutoFocus) return;   // real bots / tests own their players

            var focused = FirstPersonCamera.Instance != null ? FirstPersonCamera.Instance.Target : null;
            foreach (var p in Player.All)
            {
                if (p == null) continue;
                bool wantBot = Active && p != focused;
                var bot = p.GetComponent<PlayerBot>();
                if (wantBot && bot == null)
                {
                    p.Input.Scripted = new PlayerInputReader.ScriptedInput();
                    p.gameObject.AddComponent<PlayerBot>();
                }
                else if (!wantBot && bot != null)
                {
                    Destroy(bot);
                    p.Input.Scripted = null;
                }
            }
        }
    }
}
