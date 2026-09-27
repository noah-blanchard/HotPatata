using UnityEngine;

namespace Beep
{
    /// <summary>
    /// Purely visual: body colour and the carrier indicator. Reads state, never changes it.
    /// The indicator is a bobbing diamond above the head, so "who has the bomb" never depends on
    /// colour alone.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class PlayerPresentation : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer bodyRenderer;
        [SerializeField] Transform carrierIndicator;
        [SerializeField] float bobHeight = 0.12f;
        [SerializeField] float bobSpeed = 4f;
        [SerializeField] float spinSpeed = 120f;

        Player player;
        MaterialPropertyBlock block;
        Vector3 indicatorBase;
        float indicatorBaseScale;
        float catchPulse;

        void Awake()
        {
            player = GetComponent<Player>();
            block = new MaterialPropertyBlock();
            if (carrierIndicator != null)
            {
                indicatorBase = carrierIndicator.localPosition;
                indicatorBaseScale = carrierIndicator.localScale.x;
                carrierIndicator.gameObject.SetActive(false);
            }
            ApplyColor();
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
                r.shadowCastingMode = mode;
            }
        }

        public void Bind(BombController bomb)
        {
            bomb.CarrierChanged += carrier => SetCarrier(carrier == player);
            bomb.BombCaught += receiver => { if (receiver == player) PulseCatch(); };
        }

        public void ApplyColor()
        {
            if (bodyRenderer == null) return;
            block ??= new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, player != null ? player.Color : Color.white);
            bodyRenderer.SetPropertyBlock(block);
        }

        public void SetCarrier(bool isCarrier)
        {
            if (carrierIndicator != null) carrierIndicator.gameObject.SetActive(isCarrier);
        }

        /// <summary>Quick pop on the indicator so a fresh catch is unmistakable.</summary>
        public void PulseCatch() => catchPulse = 1f;
    }
}
