using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Network face of a Player. The owning client drives movement and aim (its NetworkTransform is
    /// owner-authoritative); everything that decides the game (throw acceptance, catch windows, locking,
    /// teleports on reset) is requested from, or pushed by, the host:
    ///   client --RequestThrow/RequestCatch/ClaimCatch--> host validates and applies
    ///   host --Locked / TeleportOwner--> owning client
    ///   owner --SubmitName--> host sanitises it --displayName--> everyone (late joiners included)
    ///   owner --stamp (time, feet, velocity, aim, posture, carrier id + offset), once per tick, UNRELIABLE with the two
    ///   before it (#117 P1)--> everyone: a rider on a moving carrier is rebuilt against the carrier as each machine draws it
    ///   (<see cref="RiderReconstruction"/>, docs/netcode-deterministic-plan.md §2.3), and the host judges moving hazards
    ///   at the time the owner saw them (§2.4) and time-stamped claims against the owner's own history (<see cref="History"/>)
    /// Offline (not spawned) this component does nothing and Player behaves exactly as before.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class NetworkPlayer : NetworkBehaviour
    {
        const float ThrowSpeedTolerance = 1.05f;   // never trust a client for more than the design maximum
        const float MaxReleaseDistance = 3f;       // metres between the claimed release point and the thrower's eyes

        /// <summary>
        /// The owner's state at one instant, sent once per network tick (#117 P1: one snapshot holds everything, so posture,
        /// aim and position never arrive a tick apart): its sequence, the <see cref="SimulationClock.ServerNow"/> at which it
        /// was produced (the time the owner saw the level at), its feet and velocity, its aim, its posture
        /// (<see cref="PlayerMotor.PackedState"/>), and the moving carrier it stands on (<see cref="IPlatformCarrier.CarrierId"/>,
        /// 0 = none) with its offset from the carrier's anchor.
        /// </summary>
        public struct PlayerStamp : INetworkSerializable, System.IEquatable<PlayerStamp>
        {
            public int Sequence;
            public double Time;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Pitch;
            public byte State;
            public int CarrierId;
            public Vector3 Offset;
            /// <summary>Bumped by every teleport: viewers snap on a new epoch, never on a distance (#97).</summary>
            public byte Epoch;

            public bool Valid => Time > 0.0;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Sequence);
                serializer.SerializeValue(ref Time);
                serializer.SerializeValue(ref Position);
                serializer.SerializeValue(ref Velocity);
                serializer.SerializeValue(ref Pitch);
                serializer.SerializeValue(ref State);
                serializer.SerializeValue(ref CarrierId);
                serializer.SerializeValue(ref Offset);
                serializer.SerializeValue(ref Epoch);
            }

            public bool Equals(PlayerStamp o) =>
                Sequence == o.Sequence && Time == o.Time && Position == o.Position && CarrierId == o.CarrierId && Offset == o.Offset && Epoch == o.Epoch;
        }

        /// <summary>
        /// The last <see cref="Redundancy"/> stamps, oldest first, sent unreliably every tick (#117 P1): a lost packet is
        /// simply skipped, never waited for, and the next one carries the stamps it lost (two losses in a row cost nothing).
        /// </summary>
        public struct StampPacket : INetworkSerializable
        {
            public byte Count;
            public PlayerStamp A, B, C;

            public PlayerStamp this[int i] => i == 0 ? A : i == 1 ? B : C;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Count);
                if (Count > 0) serializer.SerializeValue(ref A);
                if (Count > 1) serializer.SerializeValue(ref B);
                if (Count > 2) serializer.SerializeValue(ref C);
            }
        }

        public const int Redundancy = 3;

        readonly NetworkVariable<int> slot = new NetworkVariable<int>(-1);   // server-written
        readonly NetworkVariable<bool> locked = new NetworkVariable<bool>(false);   // server-written
        readonly NetworkVariable<FixedString64Bytes> displayName = new NetworkVariable<FixedString64Bytes>();   // server-written, sanitised
        int lastStampTick = int.MinValue;

        // owner: the stamps in flight (oldest first), and the next sequence
        readonly PlayerStamp[] sent = new PlayerStamp[Redundancy];
        int sentCount;
        int sequence;
        // viewer: the newest stamp received and a short history of them (the host validates time-stamped claims against it)
        PlayerStamp latest;
        readonly StampHistory history = new StampHistory();

        readonly RiderReconstruction rider = new RiderReconstruction();
        Vector3 lastWritten, lastReplicated;
        bool wroteLast;
        byte epoch;              // owner: teleports so far
        byte seenEpoch;          // viewer: the epoch the reconstruction belongs to
        float carrierLostAt = -1f;   // viewer: when the owner stopped reporting the carrier we rebuild on

        Player player;
        Unity.Netcode.Components.NetworkTransform netTransform;

        /// <summary>Set by the spawner on the server before the object is spawned.</summary>
        public int InitialSlot { get; set; } = -1;

        public bool Locked => locked.Value;
        public float RemotePitch => latest.Pitch;
        /// <summary>The owner's <see cref="PlayerMotor.PackedState"/> (slide/crouch/sprint), so every copy has the same posture.</summary>
        public byte RemoteMoveState => latest.State;
        /// <summary>The owner's latest state: time, feet, velocity, aim, posture, carrier and offset (replicated).</summary>
        public PlayerStamp Stamp => IsOwner && sentCount > 0 ? sent[sentCount - 1] : latest;
        /// <summary>Remote copy: the owner's recent stamps, oldest first.</summary>
        public StampHistory History => history;
        /// <summary>This copy is drawn (partly) relative to a moving carrier.</summary>
        public bool RebuiltOnCarrier => rider.Active;
        /// <summary>The carrier this copy is rebuilt on (0 = none), also while blending out (diagnostics).</summary>
        public int RebuiltCarrierId => rider.CarrierId;
        /// <summary>Remote copy: the moving carrier its owner stands on, as this machine has it (null when none).</summary>
        public IPlatformCarrier RemoteCarrier => CarrierRegistry.Get(latest.CarrierId);
        /// <summary>Remote copy: the world position NGO replicated this frame, before any rebuilding on a carrier (diagnostics).</summary>
        public Vector3 ReplicatedPosition => lastReplicated;

        public override void OnNetworkSpawn()
        {
            player = GetComponent<Player>();
            netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (IsServer) slot.Value = InitialSlot;

            slot.OnValueChanged += (_, s) => ApplySlot(s);
            locked.OnValueChanged += (_, l) => OnLockedChanged(l);
            displayName.OnValueChanged += (_, n) => player.SetDisplayName(n.ToString());
            ApplySlot(slot.Value);

            if (!IsOwner)
            {
                latest = default;
                history.Clear();
                if (slot.Value >= 0) NetQuality.RestartStream(slot.Value);
            }

            if (IsOwner)
            {
                BecomeLocal();
                SubmitNameRpc(PlayerNames.Shared);
            }
            PatataLog.Run($"Player spawned slot={slot.Value} owner={OwnerClientId} local={IsOwner}");
        }

        public override void OnNetworkDespawn()
        {
            PatataLog.Run($"Player despawned slot={slot.Value}");
        }

        void ApplySlot(int s)
        {
            if (s >= 0) player.ConfigureSlot(s);
            player.SetDisplayName(displayName.Value.ToString());   // ConfigureSlot resets the name to "Player N"
        }

        void BecomeLocal()
        {
            if (PlayerBot.Enabled)
            {
                player.Input.Scripted = new PlayerInputReader.ScriptedInput();
                player.gameObject.AddComponent<PlayerBot>();
            }
            else
            {
                player.Input.SetSource(InputSource.KeyboardMouse);
                CursorPolicy.SetGameplayLock(true);
            }

            if (FirstPersonCamera.Instance != null) FirstPersonCamera.Instance.Target = player;
        }

        void OnLockedChanged(bool isLocked)
        {
            if (!isLocked || !IsOwner) return;
            player.Motor.ResetVelocity();
            player.Thrower.CancelCharge();
            player.Catcher.Clear();
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (!IsOwner)
            {
                PlaceRemoteRider();
                return;
            }
            PublishStamp();
        }

        /// <summary>
        /// Owner, once per network tick (after this frame's move): when we are, where, and on which moving carrier. A
        /// boarding or leaving is sent at once instead of waiting for the next tick.
        /// </summary>
        void PublishStamp()
        {
            var carrier = player.Motor.RidingCarrier;
            bool onCarrier = carrier != null && carrier.Moves && carrier.CarrierId != 0;
            int carrierId = onCarrier ? carrier.CarrierId : 0;
            int tick = SimulationClock.ServerTick;
            if (tick == lastStampTick && carrierId == Stamp.CarrierId) return;

            lastStampTick = tick;
            Send(new PlayerStamp
            {
                Time = SimulationClock.ServerNow,
                Position = transform.position,
                Velocity = player.Motor.Velocity,
                Pitch = player.Look.Pitch,
                State = player.Motor.PackedState,
                CarrierId = carrierId,
                // In the carrier's frame, so a rider on a turning pivot stays where it stands (identity for the others).
                Offset = onCarrier ? Quaternion.Inverse(carrier.AnchorRotation) * (transform.position - carrier.AnchorPosition) : Vector3.zero,
                Epoch = epoch
            });
        }

        /// <summary>Owner: numbers the stamp and sends it with the ones before it (<see cref="Redundancy"/>).</summary>
        void Send(PlayerStamp stamp)
        {
            stamp.Sequence = ++sequence;
            if (sentCount == sent.Length)
            {
                for (int i = 1; i < sent.Length; i++) sent[i - 1] = sent[i];
                sentCount--;
            }
            sent[sentCount++] = stamp;
            var packet = new StampPacket { Count = (byte)sentCount, A = sent[0] };
            if (sentCount > 1) packet.B = sent[1];
            if (sentCount > 2) packet.C = sent[2];
            StampRpc(packet);
        }

        [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        void StampRpc(StampPacket packet)
        {
            if (IsOwner || player == null) return;
            int s = slot.Value;
            PlayerStamp newest = default;
            for (int i = 0; i < packet.Count && i < Redundancy; i++)
            {
                var stamp = packet[i];
                bool redundant = i < packet.Count - 1;
                NetQuality.NoteSnapshot(s, stamp.Sequence, redundant);
                if (stamp.Sequence <= latest.Sequence) continue;   // already seen (or older): a copy riding with a newer one
                var previous = latest;
                latest = stamp;
                history.Add(stamp);
                // Moving hazards are judged at the time the owner saw them, once per stamp, in order (docs/netcode-deterministic-plan.md §2.4).
                if (IsServer) HazardRewind.Judge(player, previous, stamp);
                newest = stamp;
            }
            if (newest.Valid) NetQuality.NotePacket(s, newest.Time);
        }

        /// <summary>
        /// Remote copy, after NGO has applied the replicated world position (PreLateUpdate) and before the held bomb is
        /// put in the hand (NetworkBomb, order 1000). On a moving carrier, draw the player at the owner's offset from the
        /// carrier as this machine draws it. The host's copy is placed the same way, so its rules (plates, checkpoints,
        /// catch centres, kill zones) see the rider on the platform, not inside it.
        /// </summary>
        void PlaceRemoteRider()
        {
            Vector3 world = transform.position;
            // NGO writes the replicated position every frame; if it did not, this is still our own last write.
            if (wroteLast && world == lastWritten) world = lastReplicated;
            lastReplicated = world;
            wroteLast = false;

            var state = latest;
            var t = player.Tuning;
            float dt = Time.deltaTime;
            if (state.Epoch != seenEpoch)
            {
                seenEpoch = state.Epoch;   // a teleport: snap to the replicated position, never sweep across the level
                rider.Clear();
                carrierLostAt = -1f;
            }
            rider.Retarget(state.CarrierId, state.Offset, dt, t.riderOffsetSmoothing);
            if (!rider.Active) return;

            var carrier = CarrierRegistry.Get(rider.CarrierId);
            if (carrier == null)
            {
                rider.Clear();   // a carrier this machine does not have (should not happen: same scene everywhere)
                return;
            }

            // A carrier the owner stops reporting is kept a moment (a single stray tick never drops the rider under a lift).
            bool reported = state.CarrierId != 0 && state.CarrierId == rider.CarrierId;
            if (reported || state.CarrierId != 0) carrierLostAt = -1f;
            else if (carrierLostAt < 0f) carrierLostAt = Time.time;
            bool riding = reported || (state.CarrierId == 0 && Time.time - carrierLostAt < t.riderLeaveGraceSeconds);
            Vector3 position = rider.Blend(world, carrier.AnchorPosition, carrier.AnchorRotation, riding, dt, t.riderBlendSeconds, t.riderSnapDistance);
            transform.position = position;
            lastWritten = position;
            wroteLast = true;
        }

        // ------------------------------------------------------------------ host -> players

        /// <summary>
        /// Owner only: tell everyone this move is a teleport. Without it, other machines interpolate the player
        /// across the whole level, which would sweep them through triggers (falling platforms, checkpoints).
        /// </summary>
        public void SyncTeleport(Vector3 position, Quaternion rotation)
        {
            if (!IsOwner) return;
            epoch++;
            // A teleport always leaves the carrier (plan rule 7); sent at once, its epoch rides in every later stamp too.
            Send(new PlayerStamp { Time = SimulationClock.ServerNow, Position = position, Pitch = player.Look.Pitch, State = player.Motor.PackedState, Epoch = epoch });
            lastStampTick = SimulationClock.ServerTick;
            if (netTransform != null) netTransform.Teleport(position, rotation, transform.localScale);
        }

        /// <summary>Host only: tell this player why their catch failed.</summary>
        public void SendHint(string text) => HintRpc(text);

        [Rpc(SendTo.Owner)]
        void HintRpc(string text) => player.Catcher.ReceiveHint(text);

        /// <summary>Host only: lock or unlock this player's controls.</summary>
        public void SetLocked(bool value)
        {
            if (IsServer) locked.Value = value;
        }

        /// <summary>Host only: move a player that is owned by a remote client (it owns its own transform).</summary>
        public void TeleportOwner(Vector3 position, Quaternion rotation) => TeleportOwnerRpc(position, rotation);

        [Rpc(SendTo.Owner)]
        void TeleportOwnerRpc(Vector3 position, Quaternion rotation) => player.TeleportLocal(position, rotation);

        // ------------------------------------------------------------------ players -> host

        /// <summary>
        /// Owner: ask the host to throw. On a moving carrier, <paramref name="carrierId"/> and <paramref name="offset"/> (the
        /// release point in the carrier's frame) let the host replay the release on its own copy of the carrier, at its own
        /// time, and <paramref name="velocity"/> is the throw's own part: the host adds the carrier's velocity (#97).
        /// </summary>
        public void RequestThrow(Vector3 origin, Vector3 velocity, int carrierId, Vector3 offset) => RequestThrowRpc(origin, velocity, carrierId, offset);

        public void RequestThrowCharge() => RequestThrowChargeRpc();

        /// <summary>Server: show the charge pose on every client.</summary>
        public void BroadcastThrowCharge()
        {
            if (IsServer) ThrowChargeRpc();
        }

        [Rpc(SendTo.NotServer)]
        void ThrowChargeRpc() => player.Animator?.BeginThrowCharge();

        /// <summary>Server: resume THROW on every client at release.</summary>
        public void BroadcastThrowRelease()
        {
            if (IsServer) ThrowReleaseRpc();
        }

        [Rpc(SendTo.NotServer)]
        void ThrowReleaseRpc() => player.Animator?.ReleaseThrow();

        public void RequestCatch() => RequestCatchRpc();

        /// <summary>Owner only: "on my screen the bomb reached my catch sphere while my window was open". The host decides.</summary>
        public void ClaimCatch() => ClaimCatchRpc();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void RequestThrowChargeRpc()
        {
            var bomb = BombController.Instance;
            if (bomb == null || bomb.Carrier != player ||
                (bomb.State != BombState.Held && bomb.State != BombState.CaughtGrace)) return;

            player.Animator?.BeginThrowCharge();
            BroadcastThrowCharge();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void RequestThrowRpc(Vector3 origin, Vector3 velocity, int carrierId, Vector3 offset)
        {
            var bomb = BombController.Instance;
            if (bomb == null || bomb.State != BombState.Held || bomb.Carrier != player) return;
            if (!IsFinite(origin) || !IsFinite(velocity) || !IsFinite(offset)) return;   // NaN/Infinity would poison the host's physics

            var t = player.Tuning;
            float max = (t.throwSpeedMax + t.throwInheritForward * t.throwInheritMaxSpeed) * ThrowSpeedTolerance;   // charge + kept run speed
            if (velocity.magnitude > max) velocity = velocity.normalized * max;

            // Thrown from a moving carrier: released from that carrier as THIS machine has it now, with its velocity here
            // (the client's lift was drawn a latency earlier; a rising deck would be under, or above, its world point).
            var carrier = CarrierRegistry.Get(carrierId);
            if (carrier != null && carrier.Moves)
            {
                origin = carrier.AnchorPosition + carrier.AnchorRotation * offset;
                velocity += CarrierMotion.VelocityAt(carrier, origin);
            }

            Vector3 eye = player.CameraTarget.position;
            if ((origin - eye).sqrMagnitude > MaxReleaseDistance * MaxReleaseDistance) return;   // the release point must be near the thrower

            player.Thrower.ThrowFromRequest(origin, velocity);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void SubmitNameRpc(string raw)
        {
            string name = PlayerNames.Sanitize(raw, slot.Value);   // at most 16 UTF-16 units: always fits 61 UTF-8 bytes
            displayName.Value = new FixedString64Bytes(name);
            PatataLog.Run($"Player slot={slot.Value} owner={OwnerClientId} is named '{name}'");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void RequestCatchRpc() => player.Catcher.TryOpenWindow();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void ClaimCatchRpc()
        {
            var bomb = BombController.Instance;
            if (bomb != null) bomb.Resolver.TryResolveCompensatedCatch(player);
        }

        static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
