using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// An actuator that switches things on and off instead of moving them (PROJECT_SPEC §13.19): driven by ONE
    /// <see cref="ISignalSource"/> like any <see cref="SignalActuator"/> (same replication, reset and travel), it turns its
    /// <see cref="targets"/> on or off as a whole when its progress crosses half way. A target is any object: a laser
    /// curtain (a plate that cuts the beams), a bramble screen (<see cref="BodyScreen"/>, a ring that parts the hedge), a fuse
    /// zone. Switching the object off removes its <see cref="Zone"/>, <see cref="FuseZone"/> and colliders from play, so no
    /// rule needs to know about the switch. Every machine derives the same state from the replicated progress.
    /// The base motion fields (platform, waypoints) stay empty.
    /// </summary>
    public class SignalSwitch : SignalActuator
    {
        [SerializeField, Tooltip("What the switch turns on and off as a whole (its zone, colliders and visuals together). Never this object or a parent of it.")]
        GameObject[] targets = new GameObject[0];
        [SerializeField, Tooltip("Off: the targets stand while the source is idle and go while it is active (a plate that cuts a curtain). " +
                                 "On: the reverse (a ring that raises a screen for a while).")]
        bool activeWhenOpen;

        public IReadOnlyList<GameObject> Targets => targets;
        public bool ActiveWhenOpen => activeWhenOpen;

        /// <summary>Are the targets on at <paramref name="progress"/> (0 closed .. 1 open)? Pure (EditMode tested).</summary>
        public static bool TargetsOn(float progress, bool activeWhenOpen) => (progress >= 0.5f) == activeWhenOpen;

        void Awake() => ApplyProgress(0f);

        protected override void ApplyProgress(float progress)
        {
            bool on = TargetsOn(progress, activeWhenOpen);
            foreach (var target in targets)
                if (target != null && target.activeSelf != on) target.SetActive(on);
        }
    }
}
