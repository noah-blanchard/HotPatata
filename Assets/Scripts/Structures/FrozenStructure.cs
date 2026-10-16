using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The root of a frozen structure (ARCHITECTURE §27.1): a piece of timber turned into its elements (<see cref="TimberElement"/>),
    /// each moved by hand from then on. It keeps the walking surface it lays from its walkable elements (slabs and runs: a smooth,
    /// closed collider, so a walker never catches a board's edge) and the line a walker treads on it (the level's walk lines, the route
    /// runner and the walk probe read it). The editor (HotPatata.Editor.WalkSurfaceBuilder) rebuilds both when an element moves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrozenStructure : MonoBehaviour
    {
        /// <summary>A run's surface: how wide it is swept across its points and how deep its slab is.</summary>
        [Serializable]
        public struct RunSurface
        {
            public int run;
            public float width;
            public float depth;
        }

        [SerializeField, Tooltip("What the walking surface is to the level's checks.")] WalkableProxy.Kind surfaceKind = WalkableProxy.Kind.Deck;
        [SerializeField, Tooltip("How far the drawn boards may stray from the walking surface (m).")] float tolerance = 0.08f;
        [SerializeField, Tooltip("How deep a slab under a walkable board is (m).")] float slabDepth = 0.4f;
        [SerializeField] List<RunSurface> runs = new List<RunSurface>();
        [SerializeField, HideInInspector] GameObject surface;
        [SerializeField, HideInInspector] Vector3[] walkLine = Array.Empty<Vector3>();
        [SerializeField, HideInInspector] string surfaceFrom;

        public WalkableProxy.Kind SurfaceKind { get => surfaceKind; set => surfaceKind = value; }
        public float Tolerance { get => tolerance; set => tolerance = Mathf.Max(0.01f, value); }
        public float SlabDepth { get => slabDepth; set => slabDepth = Mathf.Max(0.05f, value); }
        public List<RunSurface> Runs => runs;

        /// <summary>The generated walking surface (rebuilt from the elements, never edited).</summary>
        public GameObject Surface => surface;

        /// <summary>The line a walker treads along the structure's runs (world), empty when it has none (a deck).</summary>
        public Vector3[] WalkLine => walkLine ?? Array.Empty<Vector3>();

        /// <summary>Editor: what the surface was last built from.</summary>
        public string SurfaceFrom => surfaceFrom;

        public RunSurface RunInfo(int run)
        {
            foreach (var r in runs) if (r.run == run) return r;
            return new RunSurface { run = run, width = 0f, depth = slabDepth };
        }

        /// <summary>Editor: the surface just generated, the walk line and what they were built from.</summary>
        public void SetSurface(GameObject generated, Vector3[] line, string builtFrom)
        {
            surface = generated;
            walkLine = line ?? Array.Empty<Vector3>();
            surfaceFrom = builtFrom;
        }
    }
}
