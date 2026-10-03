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
    /// Offline (not spawned) this component does nothing and Player behaves exactly as before.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class NetworkPlayer : NetworkBehaviour
    {
        const float ThrowSpeedTolerance = 1.05f;   // never trust a client for more than the design maximum
        const float MaxReleaseDistance = 3f;       // metres between the claimed release point and the thrower's eyes
        const float PitchSendThreshold = 0.25f;    // degrees

        readonly NetworkVariable<int> slot = new NetworkVariable<int>(-1);   // server-written
        readonly NetworkVariable<bool> locked = new NetworkVariable<bool>(false);   // server-written
        readonly NetworkVariable<FixedString64Bytes> displayName = new NetworkVariable<FixedString64Bytes>();   // server-written, sanitised
        readonly NetworkVariable<float> pitch = new NetworkVariable<float>(0f,
            NetworkVariableBase.DefaultReadPerm, NetworkVariableWritePermission.Owner);   // owner-written
        readonly NetworkVariable<byte> moveState = new NetworkVariable<byte>((byte)MoveState.Ground,
            NetworkVariableBase.DefaultReadPerm, NetworkVariableWritePermission.Owner);   // owner-written: PlayerMotor.PackedState

        Player player;
        Unity.Netcode.Components.NetworkTransform netTransform;

        /// <summary>Set by the spawner on the server before the object is spawned.</summary>
        public int InitialSlot { get; set; } = -1;

        public bool Locked => locked.Value;
        public float RemotePitch => pitch.Value;
        /// <summary>The owner's <see cref="PlayerMotor.PackedState"/> (slide/crouch/sprint), so every copy has the same posture.</summary>
        public byte RemoteMoveState => moveState.Value;

        public override void OnNetworkSpawn()
        {
            player = GetComponent<Player>();
            netTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (IsServer) slot.Value = InitialSlot;

            slot.OnValueChanged += (_, s) => ApplySlot(s);
            locked.OnValueChanged += (_, l) => OnLockedChanged(l);
            displayName.OnValueChanged += (_, n) => player.SetDisplayName(n.ToString());
            ApplySlot(slot.Value);

            if (IsOwner)
            {
                BecomeLocal();
                SubmitNameRpc(PlayerNames.Shared);
            }
            PatataLog.Run($"Player spawned slot={slot.Value} owner={OwnerClientId} local={IsOwner}");
        }

        public override void OnNetworkDespawn() => PatataLog.Run($"Player despawned slot={slot.Value}");

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
            if (!IsSpawned || !IsOwner) return;
            if (Mathf.Abs(pitch.Value - player.Look.Pitch) > PitchSendThreshold) pitch.Value = player.Look.Pitch;
            byte state = player.Motor.PackedState;
            if (moveState.Value != state) moveState.Value = state;
        }

        // ------------------------------------------------------------------ host -> players

        /// <summary>
        /// Owner only: tell everyone this move is a teleport. Without it, other machines interpolate the player
        /// across the whole level, which would sweep them through triggers (falling platforms, checkpoints).
        /// </summary>
        public void SyncTeleport(Vector3 position, Quaternion rotation)
        {
            if (IsOwner && netTransform != null) netTransform.Teleport(position, rotation, transform.localScale);
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
