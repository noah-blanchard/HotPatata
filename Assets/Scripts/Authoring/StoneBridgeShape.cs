using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A stone bridge of an authored level (M21, ARCHITECTURE §27.7) from one end to the other (piece space, its deck's ends): a deck
    /// walked over arches on piers, parapets on both sides (they collide), its piers solid. Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class StoneBridgeShape : LevelPiece
    {
        [SerializeField] Vector3 start = new Vector3(0f, 0f, -8f);
        [SerializeField] Vector3 end = new Vector3(0f, 0f, 8f);
        [SerializeField, Range(1, 6)] int arches = 2;
        [SerializeField, Min(1.2f)] float deckWidth = 3f;
        [SerializeField, Range(0f, 1.4f), Tooltip("Its parapets' height above the deck (0: none).")] float parapet = 0.9f;
        [SerializeField, Range(0f, 2f), Tooltip("How much its deck humps up in the middle (m).")] float hump = 0.5f;
        [SerializeField] StoneWear wear = StoneWear.Aged;

        public Vector3 Start { get => start; set => start = value; }
        public Vector3 End { get => end; set => end = value; }
        public int Arches { get => arches; set => arches = Mathf.Clamp(value, 1, 6); }
        public float DeckWidth { get => deckWidth; set => deckWidth = Mathf.Max(1.2f, value); }
        public float Parapet { get => parapet; set => parapet = Mathf.Clamp(value, 0f, 1.4f); }
        public float Hump { get => hump; set => hump = Mathf.Clamp(value, 0f, 2f); }
        public StoneWear Wear { get => wear; set => wear = value; }

        public (Vector3 a, Vector3 b) WorldEnds() => (transform.TransformPoint(start), transform.TransformPoint(end));
    }
}
