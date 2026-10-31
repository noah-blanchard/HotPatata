using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>What an arch is.</summary>
    public enum ArchKind { StoneRound, StonePointed, StoneLintel, LogLintel, LogPortal }

    /// <summary>
    /// A free-standing arch or portal of an authored level (M21, ARCHITECTURE §27.7): two piers and what spans them (a round or pointed
    /// stone arch, a lintel of stone or log, a log portal with braces). Its piers and soffit collide; a body walks through under it; its
    /// top is found as ground (timber stands on it). Crest breaks it like a ruin. Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class ArchShape : LevelPiece
    {
        [SerializeField] ArchKind kind = ArchKind.StoneRound;
        [SerializeField, Min(0.8f), Tooltip("The clear span between the piers (m).")] float span = 3f;
        [SerializeField, Min(0f), Tooltip("The arch's rise above its piers' tops (m).")] float rise = 1.5f;
        [SerializeField, Min(0.3f), Tooltip("How deep it is through (m).")] float depth = 1f;
        [SerializeField, Min(0.5f), Tooltip("The piers' height to the springing (m).")] float pierHeight = 2.6f;
        [SerializeField, Min(0.3f)] float pierWidth = 0.8f;
        [SerializeField] bool keystone = true;
        [SerializeField, Range(0f, 1f), Tooltip("A ruin: how much of one side has fallen.")] float crest;
        [SerializeField] StoneWear wear = StoneWear.Aged;

        public ArchKind Kind { get => kind; set => kind = value; }
        public float Span { get => span; set => span = Mathf.Max(0.8f, value); }
        public float Rise { get => rise; set => rise = Mathf.Max(0f, value); }
        public float Depth { get => depth; set => depth = Mathf.Max(0.3f, value); }
        public float PierHeight { get => pierHeight; set => pierHeight = Mathf.Max(0.5f, value); }
        public float PierWidth { get => pierWidth; set => pierWidth = Mathf.Max(0.3f, value); }
        public bool Keystone { get => keystone; set => keystone = value; }
        public float Crest { get => crest; set => crest = Mathf.Clamp01(value); }
        public StoneWear Wear { get => wear; set => wear = value; }
    }
}
