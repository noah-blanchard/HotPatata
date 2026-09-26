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

        public void ApplyColor()
        {
            if (bodyRenderer == null) return;
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
