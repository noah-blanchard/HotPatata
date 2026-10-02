using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Purely visual: body colour and the carrier indicator. Reads state, never changes it.
    /// The indicator bobs above the carrier's head in the carrier yellow, shaped like the carrier's slot
    /// (<see cref="PlayerShapeMesh"/>), so neither "who has the bomb" nor "which player" depends on colour alone.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerPresentation : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("Every part of the character that shows the slot colour (the suit material uses the toon shader's Suit Tint).")]
        Renderer[] bodyRenderers;
        [SerializeField] Transform carrierIndicator;
        [SerializeField] float bobHeight = 0.12f;
        [SerializeField] float bobSpeed = 4f;
        [SerializeField] float spinSpeed = 120f;

        Player player;
        BombController bomb;
        MeshFilter indicatorMesh;
        MaterialPropertyBlock block;
        Vector3 indicatorBase;
        float indicatorBaseScale;
        float catchPulse;

        /// <summary>The mesh the carrier indicator shows (the slot's shape).</summary>
        public Mesh IndicatorMesh => indicatorMesh != null ? indicatorMesh.sharedMesh : null;

        void Awake()
        {
            player = GetComponent<Player>();
            block = new MaterialPropertyBlock();
            if (carrierIndicator != null)
            {
                indicatorBase = carrierIndicator.localPosition;
                indicatorBaseScale = carrierIndicator.localScale.x;
                indicatorMesh = carrierIndicator.GetComponent<MeshFilter>();
                carrierIndicator.gameObject.SetActive(false);
            }
            ApplyIdentity();
        }

        void Update()
        {
            if (carrierIndicator == null || !carrierIndicator.gameObject.activeSelf) return;

            catchPulse = Mathf.MoveTowards(catchPulse, 0f, Time.deltaTime * 3f);
            carrierIndicator.localPosition = indicatorBase + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
            carrierIndicator.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            carrierIndicator.localScale = Vector3.one * (indicatorBaseScale * (1f + catchPulse * 0.8f));
        }

        /// <summary>
        /// The player the camera follows must not see their own body (no model hands for now, only the bomb).
        /// Their shadow is kept so they still feel grounded.
        /// </summary>
        public void SetLocalView(bool isLocal)
        {
            var mode = isLocal ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<BombController>() != null) continue;   // never hide the held bomb
                if (r is ParticleSystemRenderer) continue;                         // your own dust stays visible
                r.shadowCastingMode = mode;
            }
        }

        public void Bind(BombController bombController)
        {
            Unbind();
            bomb = bombController;
            bomb.CarrierChanged += OnCarrierChanged;
            bomb.BombCaught += OnBombCaught;
            // The carrier may have been set before we bound (a player who joins mid-run).
            SetCarrier(bomb.Carrier == player);
        }

        void OnDestroy() => Unbind();

        void Unbind()
        {
            if (bomb == null) return;
            bomb.CarrierChanged -= OnCarrierChanged;
            bomb.BombCaught -= OnBombCaught;
        }

        void OnCarrierChanged(Player carrier) => SetCarrier(carrier == player);

        void OnBombCaught(Player receiver)
        {
            if (receiver == player) PulseCatch();
        }

        /// <summary>The slot's colour on the body and the slot's shape on the carrier indicator.</summary>
        public void ApplyIdentity()
        {
            if (indicatorMesh != null && player != null) indicatorMesh.sharedMesh = PlayerShapeMesh.For(player.Shape);
            if (bodyRenderers == null) return;
            block ??= new MaterialPropertyBlock();
            foreach (var body in bodyRenderers)
            {
                if (body == null) continue;
                body.GetPropertyBlock(block);
                block.SetColor(BaseColor, player != null ? player.Color : Color.white);
                body.SetPropertyBlock(block);
            }
        }

        public void SetCarrier(bool isCarrier)
        {
            if (carrierIndicator != null) carrierIndicator.gameObject.SetActive(isCarrier);
        }

        /// <summary>Quick pop on the indicator so a fresh catch is unmistakable.</summary>
        public void PulseCatch() => catchPulse = 1f;
    }
}
