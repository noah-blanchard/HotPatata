using System.Collections.Generic;
using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Activates once ALL players are inside its trigger volume. After that, a section reset returns the
    /// team to this checkpoint's spawn slots and hands the bomb to <see cref="CarrierSlot"/>.
    /// </summary>
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] int id;
        [SerializeField] Collider trigger;
        [SerializeField, Tooltip("Spawn_01..Spawn_04. Slot N is where player N respawns.")] PlayerSpawn[] spawns;
        [SerializeField] Transform bombAnchor;
        [SerializeField, Range(0, 3), Tooltip("Which player slot holds the bomb after a reset to this checkpoint.")] int carrierSlot;
        [SerializeField, Min(0f), Tooltip("Hold time for the bomb from this checkpoint onward (0 = normal). Used for the faster final sprint.")]
        float holdFuseOverride;
        [SerializeField] Renderer padRenderer;
        [SerializeField] Color inactiveColor = new Color(0.55f, 0.6f, 0.55f);
        [SerializeField] Color activeColor = new Color(0.25f, 0.9f, 0.35f);

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        readonly HashSet<Player> inside = new HashSet<Player>();
        MaterialPropertyBlock block;

        public int Id => id;
        public int CarrierSlot => carrierSlot;
        public float HoldFuseOverride => holdFuseOverride;
        public Transform BombAnchor => bombAnchor;
        public bool Activated { get; private set; }

        void Awake() => Paint(inactiveColor);

        void Update()
        {
            // Remote clients just show what the host decided.
            var run = RunManager.Instance;
            if (NetMode.IsRemoteClient && run != null)
            {
                bool shouldBeActive = run.CurrentCheckpoint != null && run.CurrentCheckpoint.Id >= id;
                if (shouldBeActive != Activated)
                {
                    Activated = shouldBeActive;
                    Paint(shouldBeActive ? activeColor : inactiveColor);
                    PatataLog.Run($"(mirror) checkpoint {id} {(shouldBeActive ? "active" : "inactive")}");
                }
            }
        }

        void FixedUpdate()
        {
            var run = RunManager.Instance;
            if (!NetMode.IsAuthority || Activated || run == null || run.State != RunState.Playing) return;

            if (PlayerZone.Collect(trigger, inside) >= run.Players.Count)
            {
                Activated = true;
                Paint(activeColor);
                run.ActivateCheckpoint(this);
            }
        }

        /// <summary>Counts as reached without the team walking in (the run starts at or past it). Authority only.</summary>
        public void MarkReached()
        {
            Activated = true;
            Paint(activeColor);
        }

        /// <summary>Back to inactive (a new run).</summary>
        public void Rearm()
        {
            Activated = false;
            Paint(inactiveColor);
        }

        public PlayerSpawn FindSpawn(int slot)
        {
            foreach (var s in spawns)
                if (s != null && s.Slot == slot) return s;
            return null;
        }

        void Paint(Color c)
        {
            if (padRenderer == null) return;
            block ??= new MaterialPropertyBlock();
            padRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, c);
            padRenderer.SetPropertyBlock(block);
        }
    }
}
