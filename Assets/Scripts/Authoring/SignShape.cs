using UnityEngine;

namespace HotPatata
{
    /// <summary>What is painted on a sign's board (the courses' clues; Sun and Moon name the two lanes, PROJECT_SPEC §15g).</summary>
    public enum SignGlyph { None, Owl, Leaf, Axe, Sun, Moon }

    /// <summary>
    /// A sign of an authored level (ARCHITECTURE §27.1): a board on one or two posts, its face along the piece's forward, a glyph
    /// painted on it, a little roof over it if asked. Data only.
    /// </summary>
    [SelectionBase]
    public sealed class SignShape : LevelPiece
    {
        [SerializeField, Range(1, 2)] int posts = 2;
        [SerializeField, Tooltip("The board's width and height (m).")] Vector2 board = new Vector2(1.7f, 1.2f);
        [SerializeField, Min(0.4f), Tooltip("The board's middle above the piece (m).")] float boardHeight = 1.75f;
        [SerializeField] SignGlyph glyph = SignGlyph.Owl;
        [SerializeField] bool roof = true;

        public int Posts { get => posts; set => posts = Mathf.Clamp(value, 1, 2); }
        public Vector2 Board { get => board; set => board = new Vector2(Mathf.Max(0.3f, value.x), Mathf.Max(0.2f, value.y)); }
        public float BoardHeight { get => boardHeight; set => boardHeight = Mathf.Max(0.4f, value); }
        public SignGlyph Glyph { get => glyph; set => glyph = value; }
        public bool Roof { get => roof; set => roof = value; }
    }
}
