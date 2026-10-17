using Unity.Netcode;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Replicates the bomb (#117 P2, ARCHITECTURE §13.3). The host runs the only real bomb (flight sweep, fuse, catch
    /// resolution) and publishes one snapshot of it per change; remote clients never simulate it, they mirror it:
    ///  - a flight travels ONCE, inside the snapshot, as a <see cref="FlightSegment"/> (state and arc are atomic); every
    ///    machine draws x(t) from its own clock, so the position is never streamed;
    ///  - the bomb stops on the surface where its arc meets the world on this screen (predicted impact) and explodes on the
    ///    host's verdict; a spectator's bomb leaves the thrower's drawn hand and eases onto the arc (<see cref="FlightCatchUp"/>);
    ///  - while held, the client attaches the bomb to the carrier's hand locally (no lag for the holder); other states
    ///    (in a tube, exploding, resetting) carry their position in the snapshot;
    ///  - a client that throws draws its own throw at once (<see cref="PredictLocalThrow"/>): the host starts the flight at the
    ///    same release time, so the drawing is the flight, with no correction and no freeze;
    ///  - a receiver's own catch is shown at once (<see cref="BombController.ShowPredictedCatch"/>), and a teammate's when
    ///    the arc enters their reach while their relayed window is open (<see cref="CatchPreview"/>), until the host decides;
    ///  - the fuse is a function of server time (<see cref="FuseClock"/>), sent only when its rate or level changes;
    ///  - state transitions raise the same events as on the host (sounds, pulse, carrier marker, UI).
    /// Nothing here lets a client change ownership: everything drawn ahead of the host is drawing only.
    /// </summary>
    [RequireComponent(typeof(BombController))]
    [DefaultExecutionOrder(1000)]   // draw after remote players have been placed for this frame
    public class NetworkBomb : NetworkBehaviour
    {
        const float RejectedAfter = 1.5f;         // still in the thrower's hand this long after the request: the host said no
        const float FuseResyncSeconds = 1f;       // the host re-sends the fuse clock this often (its own fuse is ticked by frames)

        public struct Snapshot : INetworkSerializable, System.IEquatable<Snapshot>
        {
            public int State;
            public int Carrier;       // player slot, -1 = nobody
            public int LastThrower;   // player slot, -1 = nobody
            public int FailReason;
            public int Receiver;      // player slot the host thinks the flight is heading for (logs; clients compute their own)
            public int Sequence;      // forces a change so identical states still replicate
            public FlightSegment Flight;   // while Thrown
            public Vector3 Position;       // where it is in the other states (a tube's hold point, the explosion)

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref State);
                serializer.SerializeValue(ref Carrier);
                serializer.SerializeValue(ref LastThrower);
                serializer.SerializeValue(ref FailReason);
                serializer.SerializeValue(ref Receiver);
                serializer.SerializeValue(ref Sequence);
                serializer.SerializeValue(ref Flight);
                serializer.SerializeValue(ref Position);
            }

            public bool Equals(Snapshot o) =>
                State == o.State && Carrier == o.Carrier && LastThrower == o.LastThrower && FailReason == o.FailReason &&
                Receiver == o.Receiver && Sequence == o.Sequence && Flight.Equals(o.Flight) && Position == o.Position;
        }

        /// <summary>
        /// The fuse as a function of server time: <see cref="Remaining"/> seconds left at <see cref="Time"/>, burning at
        /// <see cref="Rate"/> (0 while it does not burn). <see cref="DisplayRate"/> is the zone rate shown (hot / cold).
        /// </summary>
        public struct FuseClock : INetworkSerializable, System.IEquatable<FuseClock>
        {
            public double Time;
            public float Remaining;
            public float Rate;
            public float DisplayRate;
            public float Duration;

            public float RemainingAt(double now) =>
                Mathf.Clamp(Remaining - Rate * (float)System.Math.Max(0.0, now - Time), 0f, Mathf.Max(Duration, Remaining));

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Time);
                serializer.SerializeValue(ref Remaining);
                serializer.SerializeValue(ref Rate);
                serializer.SerializeValue(ref DisplayRate);
                serializer.SerializeValue(ref Duration);
            }

            public bool Equals(FuseClock o) =>
                Time == o.Time && Remaining == o.Remaining && Rate == o.Rate && DisplayRate == o.DisplayRate && Duration == o.Duration;
        }

        readonly NetworkVariable<Snapshot> snapshot = new NetworkVariable<Snapshot>();   // server-written
        readonly NetworkVariable<FuseClock> fuseClock = new NetworkVariable<FuseClock>(); // server-written

        BombController bomb;
        int sequence;
        bool dirty;
        float fuseSentAt = -10f;
        FuseClock lastFuse;

        // client drawing
        int drawnFlight;                 // the host flight id being drawn (0 = none)
        FlightSegment drawing;           // the arc drawn (the host's, or this machine's own throw before it arrives)
        bool drawingArc;
        Vector3 lastOnArc;
        double impactAt = double.PositiveInfinity;
        Vector3 impactPoint;
        FlightCatchUp catchUp;
        Player shownCatch;               // a teammate's catch shown ahead of the host
        float shownAt;
        Player lastPredictedCatch;
        float predictedCatchAt;
        int worldMask;

        // client-side throw prediction (drawing only)
        bool predicting;
        Player predictedThrower;
        float predictedStart;
        Player predictedReceiver;   // diagnostics: who the arc reaches first on this screen (null = the world, or nobody)
        bool predictedWorldHit;
        bool sawThrown;

        /// <summary>True while this machine draws its own throw ahead of the host's confirmation.</summary>
        public bool Predicting => predicting;

        /// <summary>Client: the server time at which the drawn arc met the world on this screen (+∞ while it flies).</summary>
        public double DrawnImpactAt => impactAt;

        public override void OnNetworkSpawn()
        {
            bomb = GetComponent<BombController>();
            worldMask = BombFlightSweep.WorldMask;

            if (IsServer)
            {
                bomb.StateChanged += OnStateChanged;
                bomb.CarrierChanged += OnCarrierChanged;
                dirty = true;
            }
            else
            {
                snapshot.OnValueChanged += OnSnapshotChanged;
                Apply(snapshot.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (bomb == null) return;
            bomb.StateChanged -= OnStateChanged;
            bomb.CarrierChanged -= OnCarrierChanged;
            snapshot.OnValueChanged -= OnSnapshotChanged;
        }

        void OnStateChanged(BombState from, BombState to) => dirty = true;
        void OnCarrierChanged(Player carrier) => dirty = true;
        void OnSnapshotChanged(Snapshot previous, Snapshot current) => Apply(current);

        // ------------------------------------------------------------------ host

        void Publish()
        {
            dirty = false;
            bool thrown = bomb.State == BombState.Thrown;
            snapshot.Value = new Snapshot
            {
                State = (int)bomb.State,
                Carrier = bomb.Carrier != null ? bomb.Carrier.PlayerId : -1,
                LastThrower = bomb.LastThrower != null ? bomb.LastThrower.PlayerId : -1,
                FailReason = (int)bomb.LastFailReason,
                Receiver = bomb.IntendedReceiver != null ? bomb.IntendedReceiver.PlayerId : -1,
                Sequence = ++sequence,
                Flight = thrown ? bomb.Body.Flight : default,
                Position = transform.position
            };
        }

        void PublishFuse()
        {
            var fuse = bomb.Fuse;
            bool burning = bomb.State == BombState.Held && !bomb.FuseHoldPending && float.IsFinite(fuse.Rate);
            var clock = new FuseClock
            {
                Time = SimulationClock.ServerNow,
                Remaining = fuse.Remaining,
                Rate = burning ? fuse.Rate : 0f,
                DisplayRate = float.IsFinite(fuse.Rate) ? fuse.Rate : 1f,
                Duration = fuse.Duration
            };
            // Only when the function changes (a refill, a rate), plus a slow resync of the host's frame-ticked fuse.
            bool changed = clock.Rate != lastFuse.Rate || clock.DisplayRate != lastFuse.DisplayRate || clock.Duration != lastFuse.Duration ||
                           Mathf.Abs(lastFuse.RemainingAt(clock.Time) - clock.Remaining) > 0.05f;
            if (!changed && Time.unscaledTime - fuseSentAt < FuseResyncSeconds) return;
            if (!changed && clock.Rate == 0f) return;   // not burning and nothing new: nothing to resync
            lastFuse = clock;
            fuseSentAt = Time.unscaledTime;
            fuseClock.Value = clock;
        }

        // ------------------------------------------------------------------ client

        void Apply(Snapshot s)
        {
            var state = (BombState)s.State;
            var thrower = FindPlayer(s.LastThrower);
            bool thrown = state == BombState.Thrown && s.Flight.Valid;
            if (thrown && s.Flight.Id != drawnFlight) BeginHostFlight(s.Flight);
            bomb.Body.Mirror(s.Flight, thrown);

            // The CATCH! marker: this machine's own reading of the arc against the players as it draws them.
            var receiver = thrown ? bomb.PickIntendedReceiver(thrower, s.Flight.Origin, s.Flight.Velocity) : null;
            bomb.ApplyMirror(state, FindPlayer(s.Carrier), thrower, (BombFailReason)s.FailReason, receiver);
            if (state != BombState.Thrown)
            {
                drawingArc = false;
                shownCatch = null;
            }
        }

        /// <summary>The host's flight arrived: keep drawing our own throw if it is the same arc, else ease onto it.</summary>
        void BeginHostFlight(FlightSegment flight)
        {
            drawnFlight = flight.Id;
            double now = SimulationClock.ServerNow;
            float g = bomb.Body.Gravity;
            Vector3 onArc = flight.PositionAt(now, g);
            if (predicting && drawingArc)
            {
                float pop = Vector3.Distance(transform.position, onArc);
                SyncStats.FlightPops.Add(pop);
                if (pop > 0.01f) PatataLog.Sync($"throw arc corrected by {pop * 100f:F0} cm (host started it {(flight.T0 - drawing.T0) * 1000.0:F0} ms apart)");
            }
            catchUp.Clear();
            catchUp.Begin(transform.position, onArc, FlightCatchUp.DurationFor(bomb.Tuning, FlightSeconds(flight)));
            drawing = flight;
            drawingArc = true;
            lastOnArc = flight.PositionAt(System.Math.Max(flight.T0, now - Time.deltaTime), g);
            impactAt = double.PositiveInfinity;
            shownCatch = null;
        }

        static Player FindPlayer(int slot)
        {
            if (slot < 0) return null;
            foreach (var p in Player.All)
                if (p.PlayerId == slot) return p;
            return null;
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                PublishFuse();
                return;
            }

            var clock = fuseClock.Value;
            bomb.Fuse.MirrorState(clock.RemainingAt(SimulationClock.ServerNow), clock.Duration);
            bomb.Fuse.SetRate(clock.DisplayRate);
            // The carrier may have spawned after the snapshot arrived: keep trying to resolve them.
            var s = snapshot.Value;
            if (s.Carrier >= 0 && bomb.Carrier == null) Apply(s);
        }

        /// <summary>
        /// Remote client that just asked the host to throw at server time <paramref name="releasedAt"/>: draw the throw now.
        /// The host starts the same flight at the same time, so this drawing is the flight. If the host never throws, the
        /// bomb goes back to the hand.
        /// </summary>
        public void PredictLocalThrow(Player thrower, double releasedAt, Vector3 origin, Vector3 velocity)
        {
            if (!IsSpawned || IsServer || bomb == null) return;

            predicting = true;
            sawThrown = false;
            predictedThrower = thrower;
            predictedStart = Time.time;
            transform.SetParent(null, true);
            drawing = new FlightSegment(releasedAt, origin, velocity, -1);
            drawingArc = true;
            lastOnArc = origin;
            impactAt = double.PositiveInfinity;
            catchUp.Clear();
            shownCatch = null;
            PredictOutcome(thrower, origin, velocity);
        }

        // Diagnostics (docs/netcode-deterministic-plan.md stage 0): who does the arc reach first on this screen?
        void PredictOutcome(Player thrower, Vector3 origin, Vector3 velocity)
        {
            const float Horizon = 3f, Step = 0.02f;
            predictedReceiver = null;
            predictedWorldHit = false;
            float g = bomb.Body.Gravity;
            float radius = bomb.Body.Radius;
            Vector3 previous = origin;
            for (float t = Step; t <= Horizon; t += Step)
            {
                Vector3 next = ThrowBallistics.PositionAt(origin, velocity, g, t);
                Vector3 delta = next - previous;
                if (Physics.SphereCast(previous, radius, delta.normalized, out _, delta.magnitude, worldMask, QueryTriggerInteraction.Ignore))
                {
                    predictedWorldHit = true;
                    return;
                }
                foreach (var p in Player.All)
                {
                    if (p == null || p == thrower || p.CatchVolume == null) continue;
                    if (CatchResolver.ReachDistance(p.Tuning, previous, next, p.CatchVolume.CatchCenter, out _) <= CatchResolver.ReachFor(p, delta))
                    {
                        predictedReceiver = p;
                        return;
                    }
                }
                previous = next;
            }
        }

        void UpdatePrediction()
        {
            var state = bomb.State;
            if (state == BombState.Thrown) sawThrown = true;

            bool outcome = state == BombState.CaughtGrace || state == BombState.Exploding || state == BombState.Resetting ||
                           state == BombState.InTransit || (sawThrown && state == BombState.Held);
            bool rejected = !sawThrown && state == BombState.Held && Time.time - predictedStart > RejectedAfter;
            bool lostIt = !sawThrown && bomb.Carrier != predictedThrower;
            if (outcome || rejected || lostIt)
            {
                predicting = false;
                if (!sawThrown) drawingArc = false;   // never thrown: back to the hand
                if (sawThrown) NotePredictionOutcome(state);
            }
        }

        void NotePredictionOutcome(BombState state)
        {
            string predicted = predictedReceiver != null ? $"receiver {predictedReceiver}" : predictedWorldHit ? "world hit" : "nothing";
            bool caught = state == BombState.CaughtGrace || state == BombState.Held;
            string host = caught ? $"caught by {(bomb.Carrier != null ? bomb.Carrier.ToString() : "?")}" : state.ToString();
            bool agreed = predictedReceiver != null ? caught && bomb.Carrier == predictedReceiver
                        : predictedWorldHit ? state == BombState.Exploding || state == BombState.Resetting
                        : !caught;
            if (agreed) SyncStats.ThrowAgreed++;
            else SyncStats.ThrowDisagreed++;
            PatataLog.Sync($"throw predicted={predicted} host={host} {(agreed ? "agree" : "DISAGREE")} rtt={NetMode.RttMs}ms");
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                if (dirty) Publish();
                return;
            }

            if (predicting) UpdatePrediction();
            var state = bomb.State;

            // Held or freshly caught: sit in the carrier's hand on this machine, whatever the network says.
            if ((state == BombState.Held || state == BombState.CaughtGrace) && bomb.Carrier != null && !(predicting && drawingArc))
            {
                transform.position = bomb.Carrier.HandAnchor.position;
                return;
            }

            if ((state == BombState.Thrown || predicting) && drawingArc)
            {
                DrawFlight();
                return;
            }

            if (state != BombState.Held && state != BombState.CaughtGrace) transform.position = snapshot.Value.Position;
        }

        /// <summary>The arc at this machine's time, stopped where it meets the world here, or a catch shown ahead of the host.</summary>
        void DrawFlight()
        {
            double now = SimulationClock.ServerNow;
            float g = bomb.Body.Gravity;
            var t = bomb.Tuning;

            // Our own catch, shown at once (PlayerCatcher claimed it): in our hands until the host confirms or refuses.
            var mine = bomb.PredictedCatch;
            if (mine != lastPredictedCatch)
            {
                if (mine == null && lastPredictedCatch != null) EaseBackOnto(now, g);
                lastPredictedCatch = mine;
                predictedCatchAt = Time.time;
            }
            if (mine != null)
            {
                if (Time.time - predictedCatchAt > NetMode.RttMs / 1000f + t.catchConfirmGrace) bomb.RetractPredictedCatch();
                else
                {
                    transform.position = mine.HandAnchor.position;
                    return;
                }
            }

            Vector3 onArc = now < impactAt ? drawing.PositionAt(now, g) : impactPoint;
            if (now < impactAt)
            {
                Vector3 chord = onArc - lastOnArc;
                float length = chord.magnitude;
                if (length > 1e-4f && Physics.SphereCast(lastOnArc, bomb.Body.Radius, chord / length, out var hit, length, worldMask, QueryTriggerInteraction.Ignore))
                {
                    // Predicted impact: it stops on the surface here; the host's verdict (the explosion) follows.
                    float k = hit.distance / length;
                    double from = now - Time.deltaTime;
                    impactAt = from + (now - from) * k;
                    impactPoint = lastOnArc + chord / length * hit.distance;
                    onArc = impactPoint;
                }

                // A teammate whose window is open, reached on this screen: shown caught until the host decides.
                if (shownCatch == null && drawnFlight == drawing.Id)
                {
                    shownCatch = CatchPreview.Find(bomb, lastOnArc, onArc, now);
                    shownAt = Time.time;
                }
                lastOnArc = onArc;
            }

            if (shownCatch != null)
            {
                if (Time.time - shownAt <= NetMode.RttMs / 1000f + t.catchConfirmGrace)
                {
                    transform.position = shownCatch.HandAnchor.position;
                    return;
                }
                shownCatch = null;   // not confirmed: back on the arc
                EaseBackOnto(now, g);
            }

            transform.position = catchUp.Apply(onArc);
        }

        void EaseBackOnto(double now, float g)
        {
            Vector3 onArc = now < impactAt ? drawing.PositionAt(now, g) : impactPoint;
            catchUp.Begin(transform.position, onArc, bomb.Tuning != null ? bomb.Tuning.flightCatchUpSeconds : 0f);
        }

        float FlightSeconds(FlightSegment flight)
        {
            const float Horizon = 3f, Step = 0.04f;
            float g = bomb.Body.Gravity;
            Vector3 previous = flight.Origin;
            for (float t = Step; t <= Horizon; t += Step)
            {
                Vector3 next = ThrowBallistics.PositionAt(flight.Origin, flight.Velocity, g, t);
                Vector3 chord = next - previous;
                if (Physics.SphereCast(previous, bomb.Body.Radius, chord.normalized, out _, chord.magnitude, worldMask, QueryTriggerInteraction.Ignore)) return t;
                previous = next;
            }
            return Horizon;
        }
    }
}
