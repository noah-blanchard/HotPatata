using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A braced trestle tower of an authored level (ARCHITECTURE §27): four splayed legs from the ground up to the piece's height,
    /// girts and cross braces, legs that collide. It holds whatever stands on it (a deck, a winch, a bell). Data only: the editor
    /// draws it with the timber kit.
    /// </summary>
    [SelectionBase]
    public sealed class TrestleShape : LevelPiece
    {
        [SerializeField, Tooltip("Half the footprint of the legs' heads (x, z), metres.")] Vector2 half = new Vector2(1.2f, 1.2f);

        public Vector2 Half { get => half; set => half = new Vector2(Mathf.Max(0.4f, value.x), Mathf.Max(0.4f, value.y)); }
    }
}
