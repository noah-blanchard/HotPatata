using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A prop of the library (ARCHITECTURE §27, Assets/Prefabs/Props): a CC0 model at its real size. Collider-free by default (decoration);
    /// <see cref="Solid"/> gives it a box (on Environment) round what it draws: a prop that looks walkable or in the way must be.
    /// The editor makes and removes the box; gameplay never reads this.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PropSolid : MonoBehaviour
    {
        [SerializeField, Tooltip("A box round the prop that players and the bomb collide with (else it is decoration).")] bool solid;
        [SerializeField, HideInInspector] BoxCollider box;

        public bool Solid => solid;
        public BoxCollider Box => box;

        /// <summary>Editor: the prop's solidity and its box.</summary>
        public void Configure(bool isSolid, BoxCollider collider)
        {
            solid = isSolid;
            box = collider;
        }
    }
}
