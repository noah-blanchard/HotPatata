using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// The lobby's players in the scene (ARCHITECTURE §6.2): while the Lobby station shows, the potato show pauses and
    /// the backdrop's mannequin <c>i</c> (already in slot <c>i</c>'s colour) hops to stage spot <c>i</c> for each joined
    /// player, waves, and gets a nameplate over its head: the slot chip (colour and shape) and the name, spec §19, never
    /// colour alone. Free seats keep their mannequin idle in the show. Leaving the lobby sends everyone back and the
    /// show goes on. Presentation only, on unscaled time; fed by <see cref="MenuView"/> from the bootstrap's lobby list.
    /// Placed by <c>MenuBackdropBuilder</c>.
    /// </summary>
    public class MenuLobbyStage : MonoBehaviour
    {
        /// <summary>One joined player: the slot (colour, shape, mannequin) and the name.</summary>
        public readonly struct Guest
        {
            public readonly int Slot;
            public readonly string Name;

            public Guest(int slot, string name)
            {
                Slot = slot;
                Name = name;
            }
        }

        [SerializeField] GameTuning tuning;
        [SerializeField, Tooltip("The potato show whose mannequins step onto the stage.")] MenuHotPotato show;
        [SerializeField, Tooltip("Stage spot per slot; its rotation is where the player faces.")] Transform[] spots;
        [SerializeField, Tooltip("Nameplate per slot above each spot (world-space UI documents).")] UIDocument[] nameplates;

        struct Step
        {
            public Vector3 From, To;
            public Quaternion FromRotation, ToRotation;
            public float Start;
            public bool Moving;
        }

        Vector3[] home;
        Quaternion[] homeRotation;
        Step[] steps;
        bool[] onStage;
        Label[] names;
        bool open;

        /// <summary>True while players are shown on the stage (from the first guest until everyone is back in the show).</summary>
        public bool IsOpen => open;

        public void Configure(GameTuning gameTuning, MenuHotPotato potatoShow, Transform[] stageSpots, UIDocument[] plates)
        {
            tuning = gameTuning;
            show = potatoShow;
            spots = stageSpots;
            nameplates = plates;
        }

        int Count => show != null && show.Players != null ? Mathf.Min(show.Players.Length, spots != null ? spots.Length : 0) : 0;

        void Awake()
        {
            int n = Count;
            home = new Vector3[n];
            homeRotation = new Quaternion[n];
            steps = new Step[n];
            onStage = new bool[n];
            names = new Label[n];
            for (int i = 0; i < n; i++)
            {
                home[i] = show.Players[i].position;
                homeRotation[i] = show.Players[i].rotation;
            }
        }

        void Start()
        {
            if (Application.isBatchMode)
            {
                enabled = false;
                return;
            }
            for (int i = 0; i < Count; i++)
            {
                BuildNameplate(i);
                ShowIdentity(i, false);
            }
        }

        /// <summary>The nameplate's layout (Nameplate.uxml): the slot chip and the name.</summary>
        void BuildNameplate(int i)
        {
            var doc = nameplates != null && i < nameplates.Length ? nameplates[i] : null;
            var template = ScreenStack.Get().Catalog != null ? ScreenStack.Get().Catalog.nameplate : null;
            if (doc == null || doc.rootVisualElement == null || template == null) return;
            var root = doc.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            var plate = template.Instantiate();
            plate.style.flexGrow = 1;
            plate.pickingMode = PickingMode.Ignore;
            root.Add(plate);
            plate.Q("chip")?.Add(UIParts.Chip(tuning, i));
            names[i] = plate.Q<Label>("name");
        }

        /// <summary>
        /// The joined players (empty when nobody is, or the lobby closed): their mannequins hop onto the stage, the others
        /// hop back to the show. The show resumes once everyone is back.
        /// </summary>
        public void SetGuests(IReadOnlyList<Guest> guests)
        {
            if (!enabled || steps == null) return;
            var wanted = new bool[Count];
            if (guests != null)
            {
                foreach (var g in guests)
                {
                    if (g.Slot < 0 || g.Slot >= Count) continue;
                    wanted[g.Slot] = true;
                    if (names[g.Slot] != null) names[g.Slot].text = g.Name;
                }
            }
            bool any = false;
            for (int i = 0; i < Count; i++)
            {
                any |= wanted[i];
                if (wanted[i] != onStage[i]) Move(i, wanted[i]);
            }
            if (any && !open)
            {
                open = true;
                show.SetPaused(true);
            }
        }

        void Move(int i, bool toStage)
        {
            onStage[i] = toStage;
            var player = show.Players[i];
            steps[i] = new Step
            {
                From = player.position,
                FromRotation = player.rotation,
                To = toStage ? spots[i].position : home[i],
                ToRotation = toStage ? spots[i].rotation : homeRotation[i],
                Start = Time.unscaledTime,
                Moving = true
            };
            ShowIdentity(i, false);
            show.Animate(i, MenuHotPotato.Hop);
        }

        void Update()
        {
            if (!open) return;
            float seconds = tuning != null ? tuning.menuStepSeconds : 0f;
            bool anyOnStage = false, anyMoving = false;
            for (int i = 0; i < Count; i++)
            {
                anyOnStage |= onStage[i];
                if (!steps[i].Moving) continue;
                float t = seconds > 0f ? Mathf.Clamp01((Time.unscaledTime - steps[i].Start) / seconds) : 1f;
                float eased = Mathf.SmoothStep(0f, 1f, t);
                var player = show.Players[i];
                player.SetPositionAndRotation(Vector3.Lerp(steps[i].From, steps[i].To, eased),
                                              Quaternion.Slerp(steps[i].FromRotation, steps[i].ToRotation, eased));
                if (t < 1f)
                {
                    anyMoving = true;
                    continue;
                }
                steps[i].Moving = false;
                if (onStage[i])
                {
                    ShowIdentity(i, true);
                    show.Animate(i, MenuHotPotato.Wave);
                }
            }
            if (!anyOnStage && !anyMoving)
            {
                open = false;
                show.SetPaused(false);
            }
        }

        void ShowIdentity(int i, bool on)
        {
            var plate = nameplates != null && i < nameplates.Length ? nameplates[i] : null;
            if (plate != null && plate.rootVisualElement != null)
                plate.rootVisualElement.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
