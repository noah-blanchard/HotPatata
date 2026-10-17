using System;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// The bomb's motion, and the source of gameplay-critical contact reports (#117 P2, ARCHITECTURE §8.3, §13.3).
    /// Held: kinematic, collider off, parented to the hand (cannot hurt its carrier). Thrown: a <see cref="FlightSegment"/>,
    /// a pure function of server time that nothing steers after release (the maths lives in <see cref="ThrowBallistics"/>).
    /// No machine integrates it and PhysX never decides it: the rigidbody stays kinematic and the collider off.
    ///
    /// On the authority, every physics step sweeps the arc from where the last step stopped to now, a short chord at a
    /// time (<see cref="AdvanceTo"/>): zones first (<see cref="BombZoneSweep"/>: transit mouths, barriers), then catches
    /// (<see cref="CatchResolver.SweepChord"/>), kill zones, and the world (<see cref="BombFlightSweep.CastWorld"/>,
    /// movers posed at the chord's time). A back-dated throw is fast-forwarded the same way, in one step.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(BombController))]
    public class BombPhysics : MonoBehaviour
    {
        const double MaxChordSeconds = 0.02;   // one physics step: a chord is short enough to be a straight line

        [SerializeField] GameTuning tuning;

        Rigidbody body;
        SphereCollider sphere;
        BombController controller;
        CatchResolver resolver;
        BombZoneSweep zones;
        FlightSegment flight;
        bool flying;
        bool mirrored;
        double sweptUntil;
        double endedAt;
        int worldMask;
        FlightCatchUp catchUp;

        /// <summary>The current flight (valid while <see cref="Flying"/>, and after it as a record of the last one).</summary>
        public FlightSegment Flight => flight;
        /// <summary>Moving along a flight segment (Thrown, not stopped on a contact).</summary>
        public bool Flying => flying;
        public Vector3 Velocity => flying || mirrored ? flight.VelocityAt(SimulationClock.ServerNow, Gravity) : Vector3.zero;
        /// <summary>Not moving (held, inert, or stopped on a lethal contact that is waiting for a late catch).</summary>
        public bool Frozen => !flying;
        public float Gravity => ThrowBallistics.Gravity(tuning);
        public float Radius => sphere != null ? sphere.radius * Mathf.Abs(transform.lossyScale.x) : 0.15f;
        /// <summary>Authority: the server time up to which the flight has been swept.</summary>
        public double SweptUntil => sweptUntil;
        /// <summary>Authority: the server time of the contact that stopped the last flight (valid while a lethal contact is held).</summary>
        public double EndedAt => endedAt;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            sphere = GetComponent<SphereCollider>();
            controller = GetComponent<BombController>();
            resolver = GetComponent<CatchResolver>();
            zones = GetComponent<BombZoneSweep>();
            body.useGravity = false;
            worldMask = BombFlightSweep.WorldMask;
            EnterInert();
        }

        public void SetTuning(GameTuning value) => tuning = value;

        /// <summary>Attach to a hand anchor, with all motion cleared.</summary>
        public void EnterHeld(Transform anchor)
        {
            EnterInert();
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Detach onto <paramref name="segment"/>. A flight that began in the past (a back-dated remote throw, a transit exit)
        /// is swept from its start on the next step. <paramref name="catchUp"/>: draw it leaving from where the bomb is drawn
        /// now and converge onto the arc (a remote thrower's hand is drawn late; presentation only).
        /// </summary>
        public void EnterThrown(FlightSegment segment, bool catchUp = false)
        {
            Vector3 drawn = transform.position;
            transform.SetParent(null, true);
            flight = segment;
            flying = true;
            sweptUntil = segment.T0;
            shownCatch = null;
            this.catchUp.Clear();
            Vector3 now = flight.PositionAt(SimulationClock.ServerNow, Gravity);
            if (catchUp) this.catchUp.Begin(drawn, now, FlightCatchUp.DurationFor(tuning, EstimatedFlightSeconds()));
            transform.position = this.catchUp.Apply(now);
            body.position = transform.position;
            stoppedAt = lastOnArc = now;
        }

        /// <summary>
        /// Remote clients: the flight the host replicated (NetworkBomb draws it), so <see cref="Velocity"/> and
        /// <see cref="Flight"/> read the same arc everywhere. Never swept here.
        /// </summary>
        public void Mirror(FlightSegment segment, bool active)
        {
            flight = segment;
            mirrored = active && segment.Valid;
        }

        /// <summary>Stop dead where it is (explosion / reset / a held contact). Keeps the current parent.</summary>
        public void EnterInert()
        {
            flying = false;
            mirrored = false;
            catchUp.Clear();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            sphere.enabled = false;
        }

        /// <summary>Seconds until the arc meets the world as it is drawn now (3 s at most): the length of the flight to draw.</summary>
        public float EstimatedFlightSeconds()
        {
            const float Horizon = 3f, Step = 0.04f;
            float g = Gravity;
            Vector3 previous = flight.Origin;
            for (float t = Step; t <= Horizon; t += Step)
            {
                Vector3 next = ThrowBallistics.PositionAt(flight.Origin, flight.Velocity, g, t);
                Vector3 chord = next - previous;
                if (Physics.SphereCast(previous, Radius, chord.normalized, out _, chord.magnitude, worldMask, QueryTriggerInteraction.Ignore)) return t;
                previous = next;
            }
            return Horizon;
        }

        void Update()
        {
            // Draw the flight on the authority (remote clients draw it in NetworkBomb from the same segment).
            if (!NetMode.IsAuthority) return;
            if (controller.State != BombState.Thrown)
            {
                shownCatch = null;
                return;
            }

            double now = SimulationClock.ServerNow;
            Vector3 onArc = flying ? flight.PositionAt(now, Gravity) : stoppedAt;
            if (NetMode.IsNetworked)
            {
                // A remote teammate whose window is open, reached as drawn here: shown caught until their claim arrives (P3).
                if (shownCatch == null && flying)
                {
                    shownCatch = CatchPreview.Find(controller, lastOnArc, onArc, now);
                    shownAt = Time.time;
                }
                if (shownCatch != null)
                {
                    if (Time.time - shownAt <= LagBudget.LateMessageWait(shownCatch, tuning, tuning.catchLagCompensation) + tuning.catchConfirmGrace)
                    {
                        lastOnArc = onArc;
                        transform.position = shownCatch.HandAnchor.position;
                        return;
                    }
                    shownCatch = null;   // no claim came: back on the arc
                    catchUp.Begin(transform.position, onArc, tuning.flightCatchUpSeconds);
                }
            }
            lastOnArc = onArc;
            transform.position = flying ? catchUp.Apply(onArc) : stoppedAt;
        }

        void FixedUpdate()
        {
            if (!NetMode.IsAuthority || !flying || controller.State != BombState.Thrown || controller.ExplosionPending) return;
            AdvanceTo(SimulationClock.ServerNow);
        }

        bool StillFlying => flying && controller.State == BombState.Thrown && !controller.ExplosionPending;

        Vector3 chordFrom, chordTo;
        double chordStart, chordEnd;
        Vector3 stoppedAt, lastOnArc;
        Player shownCatch;   // presentation: a remote teammate's catch shown ahead of their claim
        float shownAt;

        /// <summary>
        /// Authority, during <see cref="AdvanceTo"/>: a contact <paramref name="fraction"/> (0..1) along the chord being swept.
        /// The bomb is placed there and <see cref="EndedAt"/> is its time, for whatever the contact decides.
        /// </summary>
        public void MarkChordContact(float fraction)
        {
            transform.position = stoppedAt = Vector3.Lerp(chordFrom, chordTo, fraction);
            endedAt = chordStart + (chordEnd - chordStart) * fraction;
        }

        /// <summary>Authority: sweeps the flight up to server time <paramref name="now"/> and reports how it ends, if it does.</summary>
        public void AdvanceTo(double now)
        {
            float g = Gravity;
            float radius = Radius;
            while (sweptUntil < now && StillFlying)
            {
                double ta = sweptUntil, tb = Math.Min(now, ta + MaxChordSeconds);
                Vector3 a = flight.PositionAt(ta, g), b = flight.PositionAt(tb, g);

                bool wall = BombFlightSweep.CastWorld(a, b, ta, tb, now, radius, worldMask, out var hit);
                chordFrom = a;
                chordTo = wall ? hit.Point : b;
                chordStart = ta;
                chordEnd = wall ? ta + (tb - ta) * hit.Fraction : tb;

                if (zones != null) zones.Sweep(chordFrom, chordTo, radius);   // transit mouths take the bomb before the rim, barriers explode it
                if (!StillFlying) return;

                if (resolver != null && resolver.SweepChord(chordFrom, chordTo)) return;   // caught

                var killZone = BombFlightSweep.FirstKillZone(chordFrom, chordTo, radius, out float k);
                if (killZone != null)
                {
                    MarkChordContact(k);
                    controller.ReportKillZone(killZone);
                    if (!StillFlying) return;
                }

                if (wall)
                {
                    MarkChordContact(1f);   // a held contact waits here, on the surface
                    controller.ReportWorldContact(hit.Collider);
                    if (!StillFlying) return;
                }
                sweptUntil = tb;
            }
        }
    }

    /// <summary>
    /// Presentation only: a flight drawn leaving from where the bomb was drawn, converging onto its arc (#117 P2). A spectator
    /// draws a remote thrower a moment late, so the arc's present point is ahead of their hand: instead of popping, the
    /// bomb eases onto the arc over <see cref="GameTuning.flightCatchUpSeconds"/> or the first
    /// <see cref="GameTuning.flightCatchUpFraction"/> of the flight, whichever is shorter.
    /// </summary>
    public struct FlightCatchUp
    {
        Vector3 offset;
        float start;
        float duration;

        public void Begin(Vector3 drawn, Vector3 onArc, float seconds)
        {
            offset = drawn - onArc;
            start = Time.time;
            duration = seconds;
        }

        public void Clear()
        {
            offset = Vector3.zero;
            duration = 0f;
        }

        public Vector3 Apply(Vector3 onArc)
        {
            if (duration <= 0f) return onArc;
            float k = Mathf.Clamp01((Time.time - start) / duration);
            if (k >= 1f) duration = 0f;
            return onArc + offset * (1f - Mathf.SmoothStep(0f, 1f, k));
        }

        public static float DurationFor(GameTuning t, float flightSeconds) =>
            t == null ? 0f : Mathf.Min(t.flightCatchUpSeconds, t.flightCatchUpFraction * Mathf.Max(0f, flightSeconds));
    }
}
