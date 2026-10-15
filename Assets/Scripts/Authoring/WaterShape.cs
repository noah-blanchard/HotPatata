using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Water of an authored level (ARCHITECTURE §27): a surface at the piece's height, turned with it, and its kill zone from just under
    /// the surface down to its depth (a player who falls in, or a thrown bomb that touches it, fails the section; PatataWilds' rule:
    /// every water surface has a kill zone under it). Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class WaterShape : LevelPiece
    {
        [SerializeField, Tooltip("Width (x) and length (z), metres. The water flows along z.")] Vector2 size = new Vector2(20f, 10f);
        [SerializeField, Min(0.8f), Tooltip("How deep its kill zone reaches under the surface (m).")] float depth = 3f;

        public Vector2 Size { get => size; set => size = new Vector2(Mathf.Max(1f, value.x), Mathf.Max(1f, value.y)); }
        public float Depth { get => depth; set => depth = Mathf.Max(0.8f, value); }
    }
}
