using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A marked volume of the level (PROJECT_SPEC §7.3, §13.13-§13.17). The volume only answers geometry questions;
    /// what the zone DOES is composed from effect components on the same object (<see cref="FuseZone"/>,
    /// <see cref="BombBarrier"/>, <c>BombGate</c>, <c>TransitMouth</c>...), so one zone can combine
    /// several effects (a laser curtain is a forbidden fuse zone plus a barrier for the flying bomb).
    ///
    /// Players are found with <see cref="PlayerZone.Collect"/>. A flying bomb is found by the host's sweep of its
    /// flight (<see cref="BombZoneSweep"/>), never by trigger events: a fast bomb can cross a thin volume between
    /// two physics steps.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Zone : MonoBehaviour
    {
        static readonly List<Zone> all = new List<Zone>();

        [SerializeField] BoxCollider volume;

        IBombZoneEffect[] bombEffects;

        /// <summary>Every enabled zone in the loaded scenes.</summary>
        public static IReadOnlyList<Zone> All => all;

        public BoxCollider Volume => volume;
        public IReadOnlyList<IBombZoneEffect> BombEffects => bombEffects ??= GetComponents<IBombZoneEffect>();

        void Reset() => volume = GetComponent<BoxCollider>();

        void Awake()
        {
            if (volume == null) volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;
            bombEffects = GetComponents<IBombZoneEffect>();
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public int CollectPlayers(HashSet<Player> result) => PlayerZone.Collect(volume, result);

        /// <summary>
        /// Does the segment a→b (world space), swept by a sphere of <paramref name="radius"/>, touch this volume?
        /// <paramref name="enter"/> is where along the segment (0..1) it first does.
        /// </summary>
        public bool SegmentTouches(Vector3 a, Vector3 b, float radius, out float enter)
        {
            var t = volume.transform;
            Vector3 scale = t.lossyScale;
            Vector3 half = Vector3.Scale(volume.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            // Work in an unscaled local frame so the sphere radius stays a radius.
            var frame = Matrix4x4.TRS(t.TransformPoint(volume.center), t.rotation, Vector3.one).inverse;
            return SegmentHitsBox(frame.MultiplyPoint3x4(a), frame.MultiplyPoint3x4(b), half + Vector3.one * radius, out enter);
        }

        /// <summary>
        /// Slab test of the segment a→b against the axis-aligned box of half-size <paramref name="half"/> centred on
        /// the origin. Pure maths (EditMode tested). A segment starting inside counts, with <paramref name="enter"/> = 0.
        /// </summary>
        public static bool SegmentHitsBox(Vector3 a, Vector3 b, Vector3 half, out float enter)
        {
            float tMin = 0f, tMax = 1f;
            Vector3 d = b - a;
            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Abs(d[i]) < 1e-6f)
                {
                    if (a[i] < -half[i] || a[i] > half[i])
                    {
                        enter = 0f;
                        return false;
                    }
                    continue;
                }
                float t1 = (-half[i] - a[i]) / d[i];
                float t2 = (half[i] - a[i]) / d[i];
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
                if (tMin > tMax)
                {
                    enter = 0f;
                    return false;
                }
            }
            enter = tMin;
            return true;
        }
    }
}
