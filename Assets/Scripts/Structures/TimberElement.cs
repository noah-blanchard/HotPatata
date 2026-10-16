using UnityEngine;

namespace HotPatata
{
    /// <summary>What a timber element is (ARCHITECTURE §27.1): the editor tools sort, check and snap by it.</summary>
    public enum TimberElementKind { Plank, Post, Beam, Brace, Rail, Trim, Rope, Iron, Log }

    /// <summary>The wood (or iron, or rope) an element is drawn in: the timber kit's material slots, in their order.</summary>
    public enum TimberWood { Plank, PlankAged, Timber, Iron, Rope }

    /// <summary>
    /// How an element takes part in its structure's walking surface (ARCHITECTURE §27.1): not at all, as a slab of its own (a deck
    /// board), or as a point of a run (a walkway's boards and treads, a bridge's slats: the surface is swept smoothly through them).
    /// </summary>
    public enum TimberWalk { None, Slab, Run }

    /// <summary>
    /// One piece of timber an author can move by hand (ARCHITECTURE §27.1): a board, a post, a beam, a rope, an iron band. It holds its
    /// shape's data only; the editor (HotPatata.Editor.TimberElementBuilder) draws its mesh from the shared mesh catalogue and gives
    /// it a collider when it is <see cref="Solid"/>. Gameplay never reads an element: it is level geometry, like the pieces it was frozen from.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class TimberElement : MonoBehaviour
    {
        /// <summary>A grain variant shifts the texture so neighbouring boards differ (the catalogue keeps this many per shape).</summary>
        public const int GrainVariants = 4;

        [SerializeField] TimberElementKind kind;
        [SerializeField] TimberWood wood = TimberWood.Timber;
        [SerializeField, Range(0, GrainVariants - 1), Tooltip("Shifts the wood's texture so neighbouring boards differ.")] int grain;
        [SerializeField, Tooltip("A body and the bomb stop at it (a collider on Environment); else it is drawn only.")] bool solid;
        [SerializeField, Tooltip("Part of the walking surface of the structure it is in: a slab of its own, or a point of a run.")] TimberWalk walk;
        [SerializeField, Tooltip("Which run of the structure this element is a point of.")] int run;
        [SerializeField, Tooltip("Where along its run this element lies (elements of one order make one point).")] int order;
        [SerializeField, HideInInspector] string builtFrom;

        public TimberElementKind Kind { get => kind; set => kind = value; }
        public TimberWood Wood { get => wood; set => wood = value; }
        public int Grain { get => grain; set => grain = Mathf.Clamp(value, 0, GrainVariants - 1); }

        /// <summary>
        /// The grain the element is drawn with: its own for boards (side by side, they must differ), the first for the rest (a post, a
        /// rope or a beam alone shows no repeat), so the catalogue shares their meshes.
        /// </summary>
        public int DrawnGrain => wood == TimberWood.Plank || wood == TimberWood.PlankAged ? ((grain % GrainVariants) + GrainVariants) % GrainVariants : 0;
        public bool Solid { get => solid; set => solid = value; }
        public TimberWalk Walk { get => walk; set => walk = value; }
        public int Run { get => run; set => run = value; }
        public int Order { get => order; set => order = value; }

        /// <summary>Editor: what the element's mesh and collider were last drawn from.</summary>
        public string BuiltFrom { get => builtFrom; set => builtFrom = value; }

        /// <summary>The element's shape data as text (no place: the element's transform carries that).</summary>
        public abstract string ShapeKey();

        /// <summary>The structure this element belongs to (its nearest <see cref="FrozenStructure"/> above it), or null when it stands alone.</summary>
        public FrozenStructure Structure => GetComponentInParent<FrozenStructure>(true);
    }
}
