using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The receiver's catch region: a trigger sphere on the PlayerCatch layer at upper-torso height,
    /// pushed slightly forward. It only describes the region; the bomb's CatchResolver decides.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class PlayerCatchVolume : MonoBehaviour
    {
        Player owner;
        SphereCollider sphere;

        public Player Owner => owner;
        public Vector3 CatchCenter => transform.TransformPoint(sphere.center);

        void Awake()
        {
            owner = GetComponentInParent<Player>();
            sphere = GetComponent<SphereCollider>();
            sphere.isTrigger = true;
            Apply();
        }

        // Cheap, and lets radius/bias be tuned live in the Inspector during Play Mode.
        void Update() => Apply();

        void Apply()
        {
            var t = owner != null ? owner.Tuning : null;
            if (t == null) return;
            sphere.radius = t.catchRadius;
            transform.localPosition = new Vector3(0f, t.catchCenterHeight, t.catchFrontBias);
        }
    }
}
