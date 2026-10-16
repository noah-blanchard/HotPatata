using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A gantry of an authored level (ARCHITECTURE §27.1): two pairs of braced legs, a head frame between them, a pulley wheel at its
    /// head and a little roof if asked. Over a gate that rises into it, at the top of a lift. Its legs collide. Data only.
    /// </summary>
    [SelectionBase]
    public sealed class GantryShape : LevelPiece
    {
        [SerializeField, Min(1f), Tooltip("Between the two leg pairs (x), metres.")] float span = 4.7f;
        [SerializeField, Min(0.6f), Tooltip("Between the legs of a pair (z), metres.")] float depth = 1.5f;
        [SerializeField, Min(2f)] float height = 9.6f;
        [SerializeField, Tooltip("A pulley wheel at the head, its rope down the middle.")] bool pulley = true;
        [SerializeField, Tooltip("How far the pulley's rope hangs (m).")] float ropeDrop = 4.7f;
        [SerializeField] bool roof = true;

        public float Span { get => span; set => span = Mathf.Max(1f, value); }
        public float Depth { get => depth; set => depth = Mathf.Max(0.6f, value); }
        public float Height { get => height; set => height = Mathf.Max(2f, value); }
        public bool Pulley { get => pulley; set => pulley = value; }
        public float RopeDrop { get => ropeDrop; set => ropeDrop = Mathf.Max(0f, value); }
        public bool Roof { get => roof; set => roof = value; }
    }
}
