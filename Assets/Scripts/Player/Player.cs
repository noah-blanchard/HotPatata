using System.Collections.Generic;
using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Root component of the Player prefab. Holds identity, anchors and references to the
    /// player's sub-components. Contains no bomb rules; the bomb system talks to players through
    /// this type (carrier identity, hand anchor, catch volume).
    ///
    /// Works offline and online. Online, <see cref="IsLocal"/> is true only on the owning client (which
    /// drives movement/aim/input); remote copies are driven by replicated state.
    /// </summary>
    [DisallowMultipleComponent]
    public class Player : MonoBehaviour
    {
        /// <summary>Every live player (offline: the local rig; online: all connected players).</summary>
        public static readonly List<Player> All = new List<Player>();

        [Header("Identity")]
        [SerializeField] int playerId;
        [SerializeField] string displayName = "Player";
        [SerializeField] Color color = Color.white;

        [Header("Config")]
        [SerializeField] GameTuning tuning;

        [Header("Anchors")]
        [SerializeField] Transform handAnchor;
        [SerializeField] Transform throwOrigin;
        [SerializeField] Transform nameplateAnchor;
        [SerializeField] Transform cameraTarget;
        [SerializeField] PlayerCatchVolume catchVolume;

        bool localLocked;

        public int PlayerId => playerId;
        public string DisplayName => displayName;
        public Color Color => color;
        public GameTuning Tuning => tuning;

        public Transform HandAnchor => handAnchor;
        public Transform ThrowOrigin => throwOrigin;
        public Transform NameplateAnchor => nameplateAnchor;
        public Transform CameraTarget => cameraTarget;
        public PlayerCatchVolume CatchVolume => catchVolume;

        public PlayerInputReader Input { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerLook Look { get; private set; }
        public PlayerThrower Thrower { get; private set; }
        public PlayerCatcher Catcher { get; private set; }
        public PlayerPresentation Presentation { get; private set; }
        public NetworkPlayer Net { get; private set; }

        bool NetSpawned => Net != null && Net.IsSpawned;

        /// <summary>False only for the copy of a player that another machine controls.</summary>
        public bool IsLocal => !NetSpawned || Net.IsOwner;

        /// <summary>True while the run system has taken control away (reset lockout, finish).</summary>
        public bool ControlLocked => NetSpawned ? Net.Locked : localLocked;

        void Awake()
        {
            Input = GetComponent<PlayerInputReader>();
            Motor = GetComponent<PlayerMotor>();
            Look = GetComponent<PlayerLook>();
            Thrower = GetComponent<PlayerThrower>();
            Catcher = GetComponent<PlayerCatcher>();
            Presentation = GetComponent<PlayerPresentation>();
            Net = GetComponent<NetworkPlayer>();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            // Every machine mirrors the bomb, so every machine binds to it.
            var bomb = BombController.Instance;
            if (bomb == null) return;
            Thrower.Bind(bomb, All);
            Catcher.Bind(bomb);
            Presentation.Bind(bomb);
        }

        public void Configure(int id, string name, Color playerColor)
        {
            playerId = id;
            displayName = name;
            color = playerColor;
            if (Presentation != null) Presentation.ApplyColor();
        }

        /// <summary>Authority only. Online this is replicated to the owning client.</summary>
        public void SetControlLocked(bool locked)
        {
            if (NetSpawned)
            {
                Net.SetLocked(locked);
                return;
            }

            localLocked = locked;
            if (locked)
            {
                Motor.ResetVelocity();
                Thrower.CancelCharge();
                Catcher.Clear();
            }
        }

        /// <summary>
        /// Moves the player instantly and clears all motion. Online, a player owned by another machine is
        /// moved by asking its owner to teleport itself (owners own their own transform).
        /// </summary>
        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (NetSpawned && !Net.IsOwner) Net.TeleportOwner(position, rotation);
            else TeleportLocal(position, rotation);
        }

        public void TeleportLocal(Vector3 position, Quaternion rotation)
        {
            BeepLog.Run($"{this} teleported to {position:F1}");
            Motor.Teleport(position, rotation);
            Look.SetYaw(rotation.eulerAngles.y);
            Thrower.CancelCharge();
            Catcher.Clear();
        }

        public override string ToString() => displayName;
    }
}
