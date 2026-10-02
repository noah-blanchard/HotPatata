using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The mouth of a tube or the basket of a cannon (PROJECT_SPEC §6.3, §13.16): the one explicitly safe volume for a
    /// flying bomb. Instead of exploding, the bomb is captured into its <see cref="BombTransit"/>, which sends it out of
    /// the exit linked to this mouth. The volume reaches a little in front of the opening, so the sweep captures the
    /// bomb before it can touch the rim.
    /// </summary>
    [RequireComponent(typeof(Zone))]
    public class TransitMouth : MonoBehaviour, IBombZoneEffect
    {
        [SerializeField] BombTransit transit;
        [SerializeField, Min(0), Tooltip("Index of the exit (in the transit) this mouth leads to.")] int exit;

        public void OnBombPassed(BombController bomb)
        {
            if (transit != null) transit.Capture(bomb, exit);
        }
    }
}
