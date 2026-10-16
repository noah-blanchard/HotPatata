using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>What a rock shell is: a tunnel along a line, or a cavern domed over an outline.</summary>
    public enum RockShellKind { Tunnel, Cavern }

    /// <summary>A hole in a rock shell's roof (a crack the sun comes through): where it is (piece space, x and z) and how wide.</summary>
    [Serializable]
    public struct RockSkylight
    {
        [Tooltip("The hole's middle in the piece's space (x, z).")] public Vector2 at;
        [Min(0.5f)] public float radius;
    }

    /// <summary>
    /// A cave of an authored level (ARCHITECTURE §27.2): a rock shell, either a <b>tunnel</b> along a line through its points (the floor's
    /// centre line) or a <b>cavern</b> domed over a closed outline. Its walls and roof are solid rock (players stop at them, a thrown bomb
    /// explodes on them, and its roof is a ceiling: no lob passes it, docs/OBSTACLES.md §5). The floor is the terrain under it, or its own
    /// rock floor. Holes in the roof let the sun in (a <see cref="SunBeam"/> fits one). Data only: the editor draws it.
    /// </summary>
    [SelectionBase]
    public sealed class RockShellShape : LevelPiece
    {
        [SerializeField] RockShellKind kind = RockShellKind.Tunnel;
        [SerializeField, Tooltip("Tunnel: the floor's centre line. Cavern: its outline on the floor (closed). Piece space.")]
        List<Vector3> points = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 10f), new Vector3(2f, 0f, 20f) };
        [SerializeField, Min(2f), Tooltip("A tunnel's clear width at the floor (m).")] float width = 8f;
        [SerializeField, Min(2.5f), Tooltip("The clear height inside: a tunnel's under its crown, a cavern's under its dome (m).")] float height = 6f;
        [SerializeField, Range(0.5f, 6f), Tooltip("How thick the rock is (m).")] float thickness = 2f;
        [SerializeField, Range(0f, 2.5f), Tooltip("How rough the rock is: it bulges out up to this much (m); the clear space inside stays.")] float roughness = 1.2f;
        [SerializeField, Tooltip("Its own rock floor at the line's height (else the terrain is the floor).")] bool floor;
        [SerializeField, Tooltip("Tunnel: close its start (a dead end) instead of leaving a mouth.")] bool closeStart;
        [SerializeField, Tooltip("Tunnel: close its end (a dead end) instead of leaving a mouth.")] bool closeEnd;
        [SerializeField, Tooltip("Holes in the roof (piece space).")] List<RockSkylight> skylights = new List<RockSkylight>();

        public RockShellKind Kind { get => kind; set => kind = value; }
        public List<Vector3> Points => points;
        public float Width { get => width; set => width = Mathf.Max(2f, value); }
        public float Height { get => height; set => height = Mathf.Max(2.5f, value); }
        public float Thickness { get => thickness; set => thickness = Mathf.Clamp(value, 0.5f, 6f); }
        public float Roughness { get => roughness; set => roughness = Mathf.Clamp(value, 0f, 2.5f); }
        public bool Floor { get => floor; set => floor = value; }
        public bool CloseStart { get => closeStart; set => closeStart = value; }
        public bool CloseEnd { get => closeEnd; set => closeEnd = value; }
        public List<RockSkylight> Skylights => skylights;

        /// <summary>The points in the world.</summary>
        public List<Vector3> WorldPoints()
        {
            var list = new List<Vector3>(points.Count);
            foreach (var p in points) list.Add(transform.TransformPoint(p));
            return list;
        }
    }
}
