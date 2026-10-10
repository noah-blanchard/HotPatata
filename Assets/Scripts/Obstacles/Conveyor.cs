using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// A conveyor belt: a static surface that carries whoever stands on it along <see cref="direction"/> (local
    /// space) at <see cref="speed"/>. No state and no clock, so it is identical on every machine and needs no
    /// reset; each owner carries its own player. The belt's stripes scroll with it (cosmetic, `_PatternScroll`).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class Conveyor : MonoBehaviour, IPlatformCarrier
    {
        static readonly int PatternScrollId = Shader.PropertyToID("_PatternScroll");

        [SerializeField, Tooltip("Carry direction in local space (flattened, normalised).")] Vector3 direction = Vector3.forward;
        [SerializeField, Tooltip("Metres per second (negative reverses).")] float speed = 3f;
        [SerializeField, Tooltip("Renderers whose stripes scroll with the belt.")] Renderer[] beltRenderers;

        MaterialPropertyBlock block;

        /// <summary>World-space belt velocity (m/s).</summary>
        public Vector3 Velocity
        {
            get
            {
                Vector3 d = transform.TransformDirection(direction);
                d.y = 0f;
                return d.sqrMagnitude > 0.0001f ? d.normalized * speed : Vector3.zero;
            }
        }

        public Vector3 FrameDelta => Velocity * Time.deltaTime;

        // The belt itself never moves: a rider's world position already matches on every machine (no relative sync).
        public int CarrierId => 0;
        public bool Moves => false;
        public Vector3 AnchorPosition => transform.position;

        void OnEnable() => ApplyScroll();

        void OnValidate() => ApplyScroll();

        void ApplyScroll()
        {
            if (beltRenderers == null) return;
            block ??= new MaterialPropertyBlock();
            foreach (var r in beltRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetVector(PatternScrollId, Velocity);
                r.SetPropertyBlock(block);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Vector3 v = Velocity;
            if (v.sqrMagnitude < 0.0001f) return;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.6f, v.normalized * 2f);
        }
    }
}
