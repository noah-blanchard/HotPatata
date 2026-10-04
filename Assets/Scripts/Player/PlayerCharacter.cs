using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The look of a player (ARCHITECTURE §25): one Mixamo character (Humanoid rig, shared clips through the
    /// <c>PlayerCharacter</c> controller), the right-hand socket the potato sits in, and its renderers. Each slot shows one
    /// (<see cref="PlayerIdentity.CharacterFor"/>); <see cref="PlayerPresentation"/> swaps it in and
    /// <see cref="PlayerAnimator"/> drives it. Presentation only. Built by <c>PlayerCharacterBuilder</c>, never by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCharacter : MonoBehaviour
    {
        [SerializeField, Tooltip("Stable name of this character (the prefab's), to know whether a swap is needed.")] string id;
        [SerializeField] Animator animator;
        [SerializeField, Tooltip("In the right palm: the held potato sits here (third person) and the menu show passes from it.")]
        Transform handSocket;
        [SerializeField] Renderer[] renderers;

        public string Id => id;
        public Animator Animator => animator;
        public Transform HandSocket => handSocket;
        public Renderer[] Renderers => renderers;

        public void Configure(string characterId, Animator characterAnimator, Transform socket, Renderer[] parts)
        {
            id = characterId;
            animator = characterAnimator;
            handSocket = socket;
            renderers = parts;
        }
    }
}
