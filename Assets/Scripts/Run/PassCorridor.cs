using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Level-design metadata for an enclosed course (PROJECT_SPEC §15b, ARCHITECTURE §4): one intended pass, from this
    /// object's position (the release point) to <see cref="To"/> (the catch point). A course builder declares it; the course
    /// tests check that its intended arc clears the ceiling by <see cref="CeilingMargin"/>, that nothing solid or decorative
    /// sits on it, and that the opening it goes through is wide enough. Gameplay never reads it.
    /// </summary>
    public class PassCorridor : MonoBehaviour
    {
        /// <summary>Room left between the intended arc and the ceiling (PROJECT_SPEC §20).</summary>
        public const float CeilingMargin = 1.5f;
        /// <summary>How far decoration keeps from the intended arc.</summary>
        public const float DecorationClearance = 0.5f;
        /// <summary>An opening a pass goes through is at least the catch radius plus this, on its smaller side (PROJECT_SPEC §15b).</summary>
        public const float OpeningMargin = 0.5f;
        /// <summary>Radius swept along the arc that no wall may touch: the bomb with a margin.</summary>
        public const float SweepRadius = 0.35f;

        public enum ArcKind
        {
            Normal,   // the softest throw that reaches: the highest arc a player will intend
            Low,      // a full-power throw, where a low ceiling is the question (§13.5)
            Fixed     // a tube or cannon exit arc taking flightTime (§13.16)
        }

        [SerializeField, Tooltip("The catch point, in this object's space (so it turns with its section).")]
        Vector3 to;
        [SerializeField] ArcKind arc;
        [SerializeField, Min(0f), Tooltip("Fixed arcs: seconds from the release point to the catch point.")]
        float flightTime;
        [SerializeField, Tooltip("It crosses a moving obstacle (hoop, windmill blades, a piston): only the ceiling is checked.")]
        bool timed;
        [SerializeField, Min(0f), Tooltip("Smaller side of the window or doorway it goes through (0 = none).")]
        float opening;

        public Vector3 From => transform.position;
        public Vector3 To => transform.TransformPoint(to);
        public ArcKind Arc => arc;
        public bool Timed => timed;
        public float Opening => opening;

        /// <summary>Editor builders: sets the corridor while its section still stands at the origin.</summary>
        public void Configure(Vector3 worldTo, ArcKind kind, float fixedFlightTime, bool crossesMover, float openingSize)
        {
            to = transform.InverseTransformPoint(worldTo);
            arc = kind;
            flightTime = fixedFlightTime;
            timed = crossesMover;
            opening = openingSize;
        }

        /// <summary>
        /// The intended arc as points every <paramref name="step"/> seconds, from <see cref="From"/> to <see cref="To"/>. False
        /// when no throw within the tuning reaches: nothing here adds range.
        /// </summary>
        public bool TrySample(GameTuning tuning, List<Vector3> points, float step = 0.02f)
        {
            points.Clear();
            float g = ThrowBallistics.Gravity(tuning);
            Vector3 from = From, delta = To - from;
            Vector3 velocity;
            float time;
            if (arc == ArcKind.Fixed)
            {
                time = Mathf.Max(0.05f, flightTime);
                velocity = delta / time + 0.5f * g * time * Vector3.up;
            }
            else if (!TrySolve(tuning, delta, g, out velocity, out time)) return false;

            for (float t = 0f; t < time; t += step) points.Add(ThrowBallistics.PositionAt(from, velocity, g, t));
            points.Add(To);
            return true;
        }

        bool TrySolve(GameTuning tuning, Vector3 delta, float g, out Vector3 velocity, out float time)
        {
            velocity = default;
            time = 0f;
            float min = arc == ArcKind.Low ? tuning.throwSpeedMax : tuning.throwSpeedMin;
            for (float speed = min; speed <= tuning.throwSpeedMax + 1e-3f; speed += 0.5f)
            {
                if (!ThrowBallistics.TrySolveLowArc(delta, speed, g, out var direction, out time)) continue;
                velocity = direction * speed;
                return true;
            }
            return false;
        }
    }
}
