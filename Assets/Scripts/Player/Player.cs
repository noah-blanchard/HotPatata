using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Root component of the Player prefab. Holds identity, anchors and references to the
    /// player's sub-components. Contains no bomb rules; the bomb system talks to players through
    /// this type (carrier identity, hand anchor, catch volume).
    /// </summary>
    [DisallowMultipleComponent]
    public class Player : MonoBehaviour
    {
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
        public PlayerPresentation Presentation { get; private set; }

        /// <summary>True while the run system has taken control away (reset lockout).</summary>
        public bool ControlLocked { get; private set; }

        void Awake()
        {
            Input = GetComponent<PlayerInputReader>();
            Motor = GetComponent<PlayerMotor>();
            Look = GetComponent<PlayerLook>();
            Thrower = GetComponent<PlayerThrower>();
            Presentation = GetComponent<PlayerPresentation>();
        }

        public void Configure(int id, string name, Color playerColor)
        {
            playerId = id;
            displayName = name;
            color = playerColor;
            if (Presentation != null) Presentation.ApplyColor();
        }

        public void SetControlLocked(bool locked)
        {
            ControlLocked = locked;
            if (locked) Motor.ResetVelocity();
        }

        /// <summary>Moves the player instantly and clears all motion. Safe for CharacterController.</summary>
        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            Motor.Teleport(position, rotation);
            Look.SetYaw(rotation.eulerAngles.y);
        }

        public override string ToString() => displayName;
    }
}
