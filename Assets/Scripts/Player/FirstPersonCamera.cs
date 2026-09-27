using UnityEngine;

namespace Beep
{
    /// <summary>
    /// First-person camera: sits exactly at <see cref="Target"/>'s CameraTarget (the eyes) and looks
    /// where that player aims. The player it follows has their own body hidden from view, so the only
    /// thing they see of themselves is the bomb in their hand.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class FirstPersonCamera : MonoBehaviour
    {
        [SerializeField] Player target;
        [SerializeField] GameTuning tuning;

        Camera cam;

        public static FirstPersonCamera Instance { get; private set; }

        public Player Target
        {
            get => target;
            set
            {
                if (target != null) target.Presentation.SetLocalView(false);
                target = value;
                if (target != null) target.Presentation.SetLocalView(true);
            }
        }

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (target != null) target.Presentation.SetLocalView(true);
        }

        void LateUpdate()
        {
            if (target == null) return;

            cam.fieldOfView = tuning.fieldOfView;
            transform.SetPositionAndRotation(target.CameraTarget.position, target.Look.AimRotation);
        }
    }
}
