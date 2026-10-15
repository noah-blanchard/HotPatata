using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// Menu sounds (ARCHITECTURE §26, #48), flat on the UI group: hover and focus, click and submit, a value changed (sliders,
    /// toggles, choices), a screen opened, back. <see cref="UIScreen"/> hooks every navigable element through
    /// <see cref="Attach"/> and the <see cref="ScreenStack"/> calls <see cref="Opened"/> and <see cref="Back"/>, so a new
    /// screen sounds with no extra code. The component also plays a player joining or leaving the lobby.
    /// </summary>
    public sealed class UiSounds : MonoBehaviour
    {
        AudioDirector director;
        int lobbyPlayers = -1;

        public void Init(AudioDirector owner) => director = owner;

        void Update()
        {
            var boot = NetworkBootstrap.Instance;
            if (boot == null || boot.Phase != NetworkBootstrap.Mode.Lobby)
            {
                lobbyPlayers = -1;
                return;
            }
            int count = boot.LobbyPlayerCount;
            if (lobbyPlayers >= 0 && count != lobbyPlayers) director.Play(count > lobbyPlayers ? CueKeys.UiLobbyJoin : CueKeys.UiLobbyLeave);
            lobbyPlayers = count;
        }

        /// <summary>Gives <paramref name="element"/> its hover, click and change sounds (UIScreen.Navigable).</summary>
        public static void Attach(VisualElement element)
        {
            element.RegisterCallback<PointerEnterEvent>(_ => Play(CueKeys.UiHover));
            element.RegisterCallback<FocusInEvent>(_ => Play(CueKeys.UiHover));
            element.RegisterCallback<ClickEvent>(_ => Play(CueKeys.UiClick), TrickleDown.TrickleDown);
            element.RegisterCallback<NavigationSubmitEvent>(_ => Play(CueKeys.UiClick), TrickleDown.TrickleDown);
            element.RegisterCallback<ChangeEvent<float>>(_ => Play(CueKeys.UiSlider));
            element.RegisterCallback<ChangeEvent<int>>(_ => Play(CueKeys.UiSlider));
            element.RegisterCallback<ChangeEvent<bool>>(_ => Play(CueKeys.UiSlider));
            element.RegisterCallback<ChangeEvent<string>>(e =>
            {
                if (!(e.target is TextField)) Play(CueKeys.UiSlider);   // a choice changed; typing stays silent
            });
        }

        public static void Opened() => Play(CueKeys.UiOpen);
        public static void Back() => Play(CueKeys.UiBack);

        static void Play(string key) => Sfx.Play(key);
    }
}
