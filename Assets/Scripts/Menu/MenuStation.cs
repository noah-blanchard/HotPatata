using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// One station of the in-world menu (ARCHITECTURE §6.2): a board (a world-space UI Toolkit document) and the camera
    /// spot that frames it. <see cref="MenuView"/> puts a station screen on the board and <see cref="MenuCameraRig"/>
    /// flies the camera here. Only the active station's board shows its panel and takes input: an idle board fades its
    /// panel out (the Title logo stays, it is scenery), is disabled so focus never wanders onto it, and leaves the
    /// pointer's raycast layer so it never catches a click meant for the board behind it. Placed by <c>MenuBackdropBuilder</c>.
    /// </summary>
    public class MenuStation : MonoBehaviour
    {
        /// <summary>On an idle board's root: its panel fades out, and its elements cannot take focus or clicks.</summary>
        public const string IdleClass = "hp-station--idle";

        static readonly List<MenuStation> all = new List<MenuStation>();
        static int IdleLayer => LayerMask.NameToLayer("Ignore Raycast");

        [SerializeField] MenuStationId id;
        [SerializeField, Tooltip("The camera spot that frames this station.")] CinemachineCamera spot;
        [SerializeField, Tooltip("The board: a world-space UI document (Resources/HotPatataWorldPanel).")] UIDocument board;

        UIScreen screen;
        int boardLayer = -1;    // the pointer's layer (the boards' interaction layer), set back when the board is active
        VisualElement lastFocused, resume;   // the board's last focused element, and the one to give back after a pause
        bool active;

        public MenuStationId Id => id;
        public CinemachineCamera Spot => spot;
        public UIDocument Board => board;
        public UIScreen Screen => screen;
        public bool IsActive => active;

        /// <summary>The stations in the loaded scenes.</summary>
        public static IReadOnlyList<MenuStation> All => all;

        public static MenuStation Find(MenuStationId stationId)
        {
            foreach (var s in all)
                if (s.id == stationId) return s;
            return null;
        }

        public void Configure(MenuStationId stationId, CinemachineCamera cameraSpot, UIDocument document)
        {
            id = stationId;
            spot = cameraSpot;
            board = document;
        }

        void OnEnable()
        {
            all.Add(this);
            if (board != null && boardLayer < 0) boardLayer = board.gameObject.layer;
        }

        void OnDisable()
        {
            all.Remove(this);
            screen = null;   // the document rebuilds its root when it enables again
        }

        /// <summary>Builds <paramref name="stationScreen"/> into the board, replacing what it showed.</summary>
        public void Show(UIScreen stationScreen)
        {
            var root = board != null ? board.rootVisualElement : null;
            if (root == null) return;
            root.Clear();
            screen = stationScreen;
            if (screen == null) return;
            var view = screen.Create();
            root.Add(view);
            // Back (Esc / gamepad B) goes to the shown board's screen; the focused element knows it was last.
            view.RegisterCallback<NavigationCancelEvent>(OnCancel);
            view.RegisterCallback<FocusInEvent>(e => lastFocused = e.target as VisualElement, TrickleDown.TrickleDown);
            Apply();
        }

        void OnCancel(NavigationCancelEvent e)
        {
            if (!active || screen == null) return;
            screen.OnBack();
            e.StopPropagation();
        }

        /// <summary>Makes this the board that takes input (or one of the idle ones).</summary>
        public void SetActive(bool on)
        {
            if (active == on && screen != null) return;
            active = on;
            Apply();
            if (screen == null) return;
            if (on)
            {
                screen.OnShow();
                screen.PlayEntrance();
            }
            else screen.OnHide();
        }

        /// <summary>
        /// Lets the active board take input or not (a screen of the <see cref="ScreenStack"/> is open over the scene). When
        /// it takes input again, the focus comes back where it was (else to the first element).
        /// </summary>
        public void SetInteractive(bool on)
        {
            var root = screen?.Root;
            if (root == null) return;
            if (!on) resume = lastFocused;
            root.SetEnabled(on && active);
            if (!on || !active) return;
            var back = resume;
            resume = null;
            if (back != null && root.Contains(back)) root.schedule.Execute(() => back.Focus());
            else FocusFirst();
        }

        void Apply()
        {
            if (board != null && boardLayer >= 0) board.gameObject.layer = active ? boardLayer : IdleLayer;
            if (screen?.Root == null) return;
            screen.Root.SetEnabled(active);
            screen.Root.EnableInClassList(IdleClass, !active);
        }

        /// <summary>Puts the keyboard / gamepad focus on the board's first element, once it has been laid out.</summary>
        public void FocusFirst()
        {
            var root = screen?.Root;
            if (root == null) return;
            root.schedule.Execute(() =>
            {
                if (active && root.enabledInHierarchy) screen.FirstFocus()?.Focus();
            });
        }
    }
}
