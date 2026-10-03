using System.Linq;
using UnityEngine.UIElements;

namespace HotPatata
{
    /// <summary>
    /// The end-of-course screen (#23, ARCHITECTURE §6.2; layout Assets/UI/Screens/Results.uxml): COURSE COMPLETE!, the
    /// team (slot chips and names), the run time and the explosions / resets. The host (or an offline player) gets
    /// Rematch (<see cref="RunManager.Restart"/>, R does the same) and Leave; the others wait for the host and can leave.
    /// Pause only opens on an empty stack, so leaving is offered here. Back does nothing: the run is over.
    /// </summary>
    public class ResultsScreen : UIScreen
    {
        readonly RunManager run;

        public ResultsScreen(RunManager run) => this.run = run;

        public override bool CanGoBack => false;

        public Button Rematch { get; private set; }
        public Label Time { get; private set; }

        protected override VisualElement Build()
        {
            var root = FromTemplate(Templates.results);

            var team = Require<VisualElement>("team");
            team.Clear();   // anything the layout shows there is a preview
            foreach (var p in Player.All.OrderBy(p => p.PlayerId))
            {
                var pill = new VisualElement();
                pill.AddToClassList("hp-pill");
                pill.Add(UIParts.Chip(p.Tuning, p.PlayerId, small: true));
                var name = new Label(p.DisplayName);
                name.AddToClassList("hp-pill__name");
                pill.Add(name);
                team.Add(pill);
            }

            Time = Require<Label>("time");
            Time.text = FormatTime(run.RunTime);
            Require<Label>("resets").text = run.ResetCount.ToString();

            bool host = NetMode.IsAuthority;
            Show(Require<VisualElement>("host-actions"), host);
            Show(Require<VisualElement>("client-actions"), !host);
            if (host)
            {
                Rematch = Navigable(Require<Button>("rematch"));
                Rematch.clicked += run.Restart;
                Rematch.AddToClassList(FirstFocusClass);
                Navigable(Require<Button>("leave")).clicked += Leave;
            }
            else
            {
                var leave = Navigable(Require<Button>("client-leave"));
                leave.clicked += Leave;
                leave.AddToClassList(FirstFocusClass);
                UIParts.Wobble(Require<VisualElement>("waiting-spinner"));
            }
            return root;
        }

        void Leave() => Stack.Push(new ConfirmScreen("Leave the game?",
            NetMode.IsNetworked ? "You leave the game; the others stay." : "You go back to the menu.", "Leave to menu", PauseMenu.LeaveToMenu));

        /// <summary>m:ss.s, as the old results screen showed it.</summary>
        public static string FormatTime(float seconds)
        {
            int minutes = (int)(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes}:{rest:00.0}";
        }
    }
}
