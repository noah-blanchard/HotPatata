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
    ///   owner --stamp (time, position, carrier id + offset), once per tick--> everyone: a rider on a moving carrier is
    ///   rebuilt against the carrier as each machine draws it (<see cref="RiderReconstruction"/>,
    ///   docs/netcode-deterministic-plan.md §2.3), and the host judges moving hazards at the time the owner saw them (§2.4)
    /// Offline (not spawned) this component does nothing and Player behaves exactly as before.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class NetworkPlayer : NetworkBehaviour
    {
        const float ThrowSpeedTolerance = 1.05f;   // never trust a client for more than the design maximum
        const float MaxReleaseDistance = 3f;       // metres between the claimed release point and the thrower's eyes
        const float PitchSendThreshold = 0.25f;    // degrees

        /// <summary>
        /// The owner's state at one instant, sent once per network tick: the <see cref="SimulationClock.ServerNow"/> at which
        /// it was produced (the time the owner saw the level at), its feet, and the moving carrier it stands on
        /// (<see cref="IPlatformCarrier.CarrierId"/>, 0 = none) with its offset from the carrier's anchor.
        /// </summary>
        public struct PlayerStamp : INetworkSerializable, System.IEquatable<PlayerStamp>
        {
            public double Time;
            public Vector3 Position;
            public int CarrierId;
            public Vector3 Offset;

            public bool Valid => Time > 0.0;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Time);
                serializer.SerializeValue(ref Position);
                serializer.SerializeValue(ref CarrierId);
                serializer.SerializeValue(ref Offset);
            }

            public bool Equals(PlayerStamp o) => Time == o.Time && Position == o.Position && CarrierId == o.CarrierId && Offset == o.Offset;
        }

        readonly NetworkVariable<int> slot = new NetworkVariable<int>(-1);   // server-written
        readonly NetworkVariable<bool> locked = new NetworkVariable<bool>(false);   // server-written
        readonly NetworkVariable<FixedString64Bytes> displayName = new NetworkVariable<FixedString64Bytes>();   // server-written, sanitised
        readonly NetworkVariable<float> pitch = new NetworkVariable<float>(0f,
            NetworkVariableBase.DefaultReadPerm, NetworkVariableWritePermission.Owner);   // owner-written
        readonly NetworkVariable<byte> moveState = new NetworkVariable<byte>((byte)MoveState.Ground,
            NetworkVariableBase.DefaultReadPerm, NetworkVariableWritePermission.Owner);   // owner-written: PlayerMotor.PackedState
        readonly NetworkVariable<PlayerStamp> stamp = new NetworkVariable<PlayerStamp>(default,
            NetworkVariableBase.DefaultReadPerm, NetworkVariableWritePermission.Owner);   // owner-written, once per tick
        int lastStampTick = int.MinValue;

        readonly RiderReconstruction rider = new RiderReconstruction();
        Vector3 lastWritten, lastReplicated;
        bool wroteLast;

        Player player;
        Unity.Netcode.Components.NetworkTransform netTransform;

        /// <summary>Set by the spawner on the server before the object is spawned.</summary>
        public int InitialSlot { get; set; } = -1;

        public bool Locked => locked.Value;
        public float RemotePitch => pitch.Value;
        /// <summary>The owner's <see cref="PlayerMotor.PackedState"/> (slide/crouch/sprint), so every copy has the same posture.</summary>
        public byte RemoteMoveState => moveState.Value;
        /// <summary>The owner's latest state: time, feet, carrier and offset (replicated).</summary>
        public PlayerStamp Stamp => stamp.Value;
        /// <summary>This copy is drawn (partly) relative to a moving carrier.</summary>
        public bool RebuiltOnCarrier => rider.Active;
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

            if (IsServer && !IsOwner) stamp.OnValueChanged += OnStampOnHost;   // moving hazards are judged at the owner's time

            if (IsOwner)
            {
                BecomeLocal();
                SubmitNameRpc(PlayerNames.Shared);
            }
            PatataLog.Run($"Player spawned slot={slot.Value} owner={OwnerClientId} local={IsOwner}");
        }

        public override void OnNetworkDespawn()
        {
            stamp.OnValueChanged -= OnStampOnHost;
            PatataLog.Run($"Player despawned slot={slot.Value}");
        }

        void OnStampOnHost(PlayerStamp previous, PlayerStamp current) => HazardRewind.Judge(player, previous, current);

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
            if (Mathf.Abs(pitch.Value - player.Look.Pitch) > PitchSendThreshold) pitch.Value = player.Look.Pitch;
            byte state = player.Motor.PackedState;
            if (moveState.Value != state) moveState.Value = state;
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
            if (tick == lastStampTick && carrierId == stamp.Value.CarrierId) return;

            lastStampTick = tick;
            stamp.Value = new PlayerStamp
            {
                Time = SimulationClock.ServerNow,
                Position = transform.position,
                CarrierId = carrierId,
                // In the carrier's frame, so a rider on a turning pivot stays where it stands (identity for the others).
                Offset = onCarrier ? Quaternion.Inverse(carrier.AnchorRotation) * (transform.position - carrier.AnchorPosition) : Vector3.zero
            };
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

            var state = stamp.Value;
            var t = player.Tuning;
            float dt = Time.deltaTime;
            rider.Retarget(state.CarrierId, state.Offset, dt, t.riderOffsetSmoothing);
            if (!rider.Active) return;

            var carrier = CarrierRegistry.Get(rider.CarrierId);
            if (carrier == null)
            {
                rider.Clear();   // a carrier this machine does not have (should not happen: same scene everywhere)
                return;
            }

            bool riding = state.CarrierId != 0 && state.CarrierId == rider.CarrierId;
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
            stamp.Value = new PlayerStamp { Time = SimulationClock.ServerNow, Position = position };   // a teleport always leaves the carrier (plan rule 7)
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

        public void RequestThrow(Vector3 origin, Vector3 velocity) => RequestThrowRpc(origin, velocity);

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
        void RequestThrowRpc(Vector3 origin, Vector3 velocity)
        {
            var bomb = BombController.Instance;
            if (bomb == null || bomb.State != BombState.Held || bomb.Carrier != player) return;
            if (!IsFinite(origin) || !IsFinite(velocity)) return;   // NaN/Infinity would poison the host's physics

            var t = player.Tuning;
            float max = (t.throwSpeedMax + t.throwInheritForward * t.throwInheritMaxSpeed) * ThrowSpeedTolerance;   // charge + kept run speed
            if (velocity.magnitude > max) velocity = velocity.normalized * max;

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
