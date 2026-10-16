using UnityEngine;

namespace HotPatata
{
    /// <summary>Which lane a marker names: the Sun lane (gold) or the Moon lane (blue), PROJECT_SPEC §15g.</summary>
    public enum Lane { Sun, Moon }

    /// <summary>How a lane marker stands: a post beside the path, or an arch over it.</summary>
    public enum LaneMarkerForm { Post, Arch }

    /// <summary>
    /// A lane marker of an authored level (ARCHITECTURE §27.2, PROJECT_SPEC §15g): where two lanes split, each lane's way is named by its
    /// glyph (a sun or a moon painted on a round board), its colour (ribbons) and a lantern. A post beside the path or an arch over it, its
    /// face along the piece's forward. Read in a glance, so a team splits without a word. Scenery only. Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class LaneMarkerShape : LevelPiece
    {
        [SerializeField] Lane lane = Lane.Sun;
        [SerializeField] LaneMarkerForm form = LaneMarkerForm.Post;
        [SerializeField, Min(1.6f), Tooltip("An arch's clear width (m).")] float span = 3.2f;
        [SerializeField, Min(2.4f), Tooltip("The post's or the arch's height (m).")] float height = 3f;
        [SerializeField, Tooltip("A lantern hangs from it.")] bool lantern = true;

        public Lane Lane { get => lane; set => lane = value; }
        public LaneMarkerForm Form { get => form; set => form = value; }
        public float Span { get => span; set => span = Mathf.Max(1.6f, value); }
        public float Height { get => height; set => height = Mathf.Max(2.4f, value); }
        public bool Lantern { get => lantern; set => lantern = value; }
    }
}
