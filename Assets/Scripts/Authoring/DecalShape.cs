using UnityEngine;

namespace HotPatata
{
    /// <summary>A decal's surface: moss, mud, dead leaves, or wet ground.</summary>
    public enum DecalKind { Moss, Mud, Leaves, Wet }

    /// <summary>
    /// A decal of an authored level (ARCHITECTURE §27.5): a patch of moss, mud, leaves or wet ground laid on what is under it (the
    /// ground, a rock face), its edges broken along the surface's own relief. Decoration: no collider, no shadow. Data only: the editor
    /// conforms it to the surface along the piece's down.
    /// </summary>
    [SelectionBase]
    public sealed class DecalShape : LevelPiece
    {
        [SerializeField] DecalKind kind = DecalKind.Moss;
        [SerializeField, Range(0.3f, 8f), Tooltip("Its width (m).")] float size = 2.5f;
        [SerializeField, Range(0, 3), Tooltip("Which of the four patches of its kind.")] int variant;

        public DecalKind Kind { get => kind; set => kind = value; }
        public float Size { get => size; set => size = Mathf.Clamp(value, 0.3f, 8f); }
        public int Variant { get => variant; set => variant = Mathf.Clamp(value, 0, 3); }
    }
}
