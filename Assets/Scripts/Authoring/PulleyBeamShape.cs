using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A pulley beam of an authored level (ARCHITECTURE §27.1): a beam out along the piece's forward, braced below, a pulley wheel at its
    /// tip and a rope hanging from it. It stands on its own post at the piece, or (no post) leans on what the piece is set against: a
    /// trunk, a wall. Data only.
    /// </summary>
    [SelectionBase]
    public sealed class PulleyBeamShape : LevelPiece
    {
        [SerializeField, Min(1f), Tooltip("How far the beam reaches out (m).")] float reach = 3.5f;
        [SerializeField, Min(0.5f), Tooltip("The beam's height above the piece (m).")] float height = 5f;
        [SerializeField, Tooltip("Its own post from the piece up; off: it is bolted to a trunk or a wall at the piece.")] bool post = true;
        [SerializeField, Min(0f), Tooltip("How far the rope hangs from the wheel (m).")] float ropeDrop = 1.2f;

        public float Reach { get => reach; set => reach = Mathf.Max(1f, value); }
        public float Height { get => height; set => height = Mathf.Max(0.5f, value); }
        public bool Post { get => post; set => post = value; }
        public float RopeDrop { get => ropeDrop; set => ropeDrop = Mathf.Max(0f, value); }
    }
}
