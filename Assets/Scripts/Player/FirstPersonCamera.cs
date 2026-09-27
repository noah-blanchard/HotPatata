using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// First-person camera: sits exactly at <see cref="Target"/>'s CameraTarget (the eyes) and looks
    /// where that player aims. The player it follows has their own body hidden from view, so the only
    /// thing they see of themselves is the bomb in their hand.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(200)]   // after PlayerLook has placed the eye pivot
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

            float fovOffset = target.Feel != null ? target.Feel.FovOffset : 0f;
            cam.fieldOfView = tuning.fieldOfView + fovOffset;
            transform.SetPositionAndRotation(target.CameraTarget.position, target.CameraTarget.rotation);
        }
    }
}
