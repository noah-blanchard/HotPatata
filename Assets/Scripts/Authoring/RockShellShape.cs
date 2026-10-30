using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>What a rock shell is: a tunnel along a line, a cavern domed over an outline, or a shaft (straight walls up from an outline).</summary>
    public enum RockShellKind { Tunnel, Cavern, Shaft }

    /// <summary>A tunnel's cross section at a point (M21): its shape.</summary>
    public enum CaveProfile { Round, Rift, Gallery, Arch, Fissure }

    /// <summary>A tunnel's section at one of its points (M21, ARCHITECTURE §27.6): sections are blended between the points.</summary>
    [Serializable]
    public struct CaveSection
    {
        [Min(1.5f), Tooltip("Clear width at the floor (m).")] public float width;
        [Min(2f), Tooltip("Clear height under the crown (m).")] public float height;
        public CaveProfile profile;
        [Range(-25f, 25f), Tooltip("How far it leans (degrees).")] public float lean;
        [Range(-3f, 3f), Tooltip("The floor raised (or lowered) here (m): a step or a ramp.")] public float floorOffset;

        public CaveSection(float width, float height, CaveProfile profile, float lean = 0f, float floorOffset = 0f)
        {
            this.width = width;
            this.height = height;
            this.profile = profile;
            this.lean = lean;
            this.floorOffset = floorOffset;
        }
    }

    /// <summary>
    /// Where a tunnel's end meets a chamber of a cave network (M21): the chamber, the direction from its middle (degrees round, 0 = +Z) and
    /// the height above its floor. The end follows the chamber.
    /// </summary>
    [Serializable]
    public struct CaveAttach
    {
        public RockShellShape node;
        [Range(0f, 360f)] public float angle;
        [Min(0f)] public float height;

        public bool Attached => node != null && node.Kind != RockShellKind.Tunnel;
    }

    /// <summary>A bulge of a cavern's outline outward (M21): where round its outline (0..1), how wide and deep.</summary>
    [Serializable]
    public struct CaveAlcove
    {
        [Range(0f, 1f)] public float along;
        [Min(1f)] public float width;
        [Min(0.5f)] public float depth;
    }

    /// <summary>A rock column from a cavern's floor to its roof (M21): where (piece space x, z), how thick, how narrow at its waist (0..1).</summary>
    [Serializable]
    public struct CavePillar
    {
        public Vector2 at;
        [Min(0.4f)] public float radius;
        [Range(0.3f, 1f)] public float waist;
    }

    /// <summary>A shelf of rock in a cavern or along a shaft's wall (M21), walked on: its outline (piece space x, z), its top's height and its thickness.</summary>
    [Serializable]
    public struct CaveFloor
    {
        public List<Vector2> outline;
        [Min(0.5f)] public float height;
        [Min(0.2f)] public float thickness;
    }

    /// <summary>A shelf round part of a shaft's wall (M21): at a height, reaching in by an inset, between two places round the outline (0..1).</summary>
    [Serializable]
    public struct ShaftLedge
    {
        [Min(0.5f)] public float height;
        [Min(0.5f)] public float inset;
        [Range(0f, 1f)] public float from, to;
    }

    /// <summary>What dresses a cave (M21, ARCHITECTURE §27.6): how many of each, never in its walking corridor. All 0: a bare cave (as before M21).</summary>
    [Serializable]
    public struct CaveDressing
    {
        [Range(0f, 1f)] public float stalactites, stalagmites, columns, puddles, props;
        [Min(0f), Tooltip("A lantern on the walls along the walk every this many metres (0 = none).")] public float lanternSpacing;

        public bool Any => stalactites > 0f || stalagmites > 0f || columns > 0f || puddles > 0f || props > 0f || lanternSpacing > 0f;
    }

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
        [Header("Tunnel (M21)")]
        [SerializeField, Tooltip("Tunnel: a section at each point, blended between them (empty: Width and Height all along).")]
        List<CaveSection> sections = new List<CaveSection>();
        [SerializeField, Range(0f, 8f), Tooltip("Tunnel: its last metres widen to 1.4 times where it opens into a chamber (an eroded mouth, not a drilled one).")]
        float flare;
        [SerializeField, Tooltip("Tunnel: its start meets a chamber of a network.")] CaveAttach startAttach;
        [SerializeField, Tooltip("Tunnel: its end meets a chamber of a network.")] CaveAttach endAttach;
        [Header("Cavern (M21)")]
        [SerializeField, Tooltip("Cavern: the crown's offset from the outline's middle (piece space x, z): an asymmetric vault.")] Vector2 apex;
        [SerializeField, Min(0f), Tooltip("Cavern: the crown's height (0: Height). Above it, Height is the walls' height at the outline.")] float apexHeight;
        [SerializeField] List<CaveAlcove> alcoves = new List<CaveAlcove>();
        [SerializeField] List<CavePillar> pillars = new List<CavePillar>();
        [SerializeField, Tooltip("Cavern: shelves of rock (a mezzanine), walked on.")] List<CaveFloor> floors = new List<CaveFloor>();
        [Header("Shaft (M21)")]
        [SerializeField, Tooltip("Shaft: no roof (a skylight the full size).")] bool openTop = true;
        [SerializeField, Tooltip("Shaft: no floor at its foot.")] bool openBottom;
        [SerializeField] List<ShaftLedge> ledges = new List<ShaftLedge>();
        [Header("Dressing (M21)")]
        [SerializeField] CaveDressing dressing;

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
        public List<CaveSection> Sections => sections;
        public float Flare { get => flare; set => flare = Mathf.Clamp(value, 0f, 8f); }
        public CaveAttach StartAttach { get => startAttach; set => startAttach = value; }
        public CaveAttach EndAttach { get => endAttach; set => endAttach = value; }
        public Vector2 Apex { get => apex; set => apex = value; }
        public float ApexHeight { get => apexHeight; set => apexHeight = Mathf.Max(0f, value); }
        public List<CaveAlcove> Alcoves => alcoves;
        public List<CavePillar> Pillars => pillars;
        public List<CaveFloor> Floors => floors;
        public bool OpenTop { get => openTop; set => openTop = value; }
        public bool OpenBottom { get => openBottom; set => openBottom = value; }
        public List<ShaftLedge> Ledges => ledges;
        public CaveDressing Dressing { get => dressing; set => dressing = value; }

        /// <summary>A cavern with M21's shape (an apex, alcoves): its dome is drawn by the v2 builder.</summary>
        public bool ShapedCavern => kind == RockShellKind.Cavern && (apexHeight > 0f || apex != Vector2.zero || alcoves.Count > 0);

        /// <summary>
        /// The points in the world. A tunnel's end attached to a chamber is at the chamber's wall in its direction (a metre inside it, so the
        /// tunnel opens through the wall), at its height above the chamber's floor.
        /// </summary>
        public List<Vector3> WorldPoints()
        {
            var list = new List<Vector3>(points.Count);
            foreach (var p in points) list.Add(transform.TransformPoint(p));
            if (kind != RockShellKind.Tunnel || list.Count < 2) return list;
            if (startAttach.Attached) list[0] = AttachPoint(startAttach);
            if (endAttach.Attached) list[list.Count - 1] = AttachPoint(endAttach);
            return list;
        }

        /// <summary>Where an attached end meets its chamber: its wall in the attach's direction from its middle, a metre inside.</summary>
        public static Vector3 AttachPoint(CaveAttach attach)
        {
            var node = attach.node;
            var outline = node.points;
            var t = node.transform;
            var center = Vector2.zero;
            foreach (var p in outline) center += new Vector2(p.x, p.z);
            center /= Mathf.Max(1, outline.Count);
            var dir = new Vector2(Mathf.Sin(attach.angle * Mathf.Deg2Rad), Mathf.Cos(attach.angle * Mathf.Deg2Rad));
            // the ray from the middle out to the outline (piece space)
            float best = float.MaxValue;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = new Vector2(outline[i].x, outline[i].z), e = new Vector2(outline[(i + 1) % outline.Count].x, outline[(i + 1) % outline.Count].z) - a;
                float den = dir.x * e.y - dir.y * e.x;
                if (Mathf.Abs(den) < 1e-8f) continue;
                var ap = a - center;
                float tt = (ap.x * e.y - ap.y * e.x) / den, u = (ap.x * dir.y - ap.y * dir.x) / den;
                if (tt > 0f && u >= 0f && u <= 1f) best = Mathf.Min(best, tt);
            }
            if (best == float.MaxValue) best = 1f;
            var local = center + dir * Mathf.Max(0.5f, best - 1f);
            return t.TransformPoint(new Vector3(local.x, attach.height, local.y));
        }
    }
}
