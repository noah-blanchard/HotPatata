using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A shaft of sunlight from a slit to a stone eye (PROJECT_SPEC §13.23): a long, thin <see cref="Zone"/> along its local +Z,
    /// from the slit (-Z face) to the eye (+Z face). As a <see cref="ISignalSource"/> it is active while it is CUT: at least one
    /// player's body stands anywhere in it. A body cuts it from any point of its length (from a ledge, from a passing
    /// shuttle), so the one who cuts it may be far from what it opens. The bomb never cuts it. With
    /// <see cref="activeWhileWhole"/> it is the other way round: active while the light reaches the eye (a bridge of light
    /// that whoever crosses its line takes down).
    /// Stateless like a plate: the host's answer drives the actuator (replicated); a client's answer only draws the beam.
    /// </summary>
    [RequireComponent(typeof(Zone))]
    public class SunBeam : MonoBehaviour, ISignalSource, IObstacleState
    {
        readonly HashSet<Player> inside = new HashSet<Player>();
        Zone zone;

        [SerializeField, Tooltip("Off: active while a body cuts the beam. On: active while the light reaches the eye.")]
        bool activeWhileWhole;

        public bool Active => Cut != activeWhileWhole;
        /// <summary>A body stands in the beam.</summary>
        public bool Cut { get; private set; }
        public bool ActiveWhileWhole => activeWhileWhole;
        /// <summary>How far the light gets from the slit (0..1 of the beam's length): 1 when it reaches the eye.</summary>
        public float Reach { get; private set; } = 1f;

        /// <summary>The light reaching the eye (0 cut .. 1 whole): what a visual of the eye reads.</summary>
        float IObstacleState.Progress => Cut ? 0f : 1f;

        void Awake() => zone = GetComponent<Zone>();

        void FixedUpdate()
        {
            zone.CollectPlayers(inside);
            Cut = inside.Count > 0;
            Reach = Cut ? FirstCut() : 1f;
        }

        /// <summary>The fraction of the length where the body nearest the slit cuts the light.</summary>
        float FirstCut()
        {
            var box = zone.Volume;
            float half = box.size.z * 0.5f;
            float nearest = 1f;
            foreach (var p in inside)
            {
                if (p == null) continue;
                float z = box.transform.InverseTransformPoint(p.transform.position).z - box.center.z;
                nearest = Mathf.Min(nearest, CutFraction(z, half));
            }
            return nearest;
        }

        /// <summary>Where along the beam (0 slit .. 1 eye) a body at local depth <paramref name="z"/> cuts it. Pure (EditMode tested).</summary>
        public static float CutFraction(float z, float halfLength) =>
            halfLength <= 0f ? 0f : Mathf.Clamp01((z + halfLength) / (2f * halfLength));
    }
}
