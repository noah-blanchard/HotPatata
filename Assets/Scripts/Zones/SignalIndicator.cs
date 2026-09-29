using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Presentation only: lights its renderers while a signal source is active (a gate after the pass, a plate while
    /// held, an arch once claimed), and pulses them during the last seconds of a timed gate so runners see it is about
    /// to close. Brightness and a size pulse carry the state, never colour alone (spec §19); the pulse is gentle, not a
    /// flash. Reads the source, never changes it.
    /// </summary>
    public class SignalIndicator : MonoBehaviour
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField, Tooltip("A component implementing ISignalSource.")] MonoBehaviour source;
        [SerializeField] Renderer[] lamps;
        [SerializeField, ColorUsage(false, true)] Color onEmission = new Color(1.2f, 0.9f, 0.2f);
        [SerializeField, Tooltip("Optional part that pops slightly while active (e.g. a plate's pad sinks).")] Transform pressed;
        [SerializeField] Vector3 pressedOffset = new Vector3(0f, -0.05f, 0f);
        [SerializeField, Min(0f)] float warnSeconds = 2f;

        MaterialPropertyBlock block;
        Vector3 pressedRest;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (pressed != null) pressedRest = pressed.localPosition;
        }

        void Update()
        {
            if (source is not ISignalSource signal) return;
            bool on = signal.Active;
            float glow = on ? 1f : 0f;
            if (on && source is BombGate gate && gate.RemainingSeconds < warnSeconds)
                glow = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Cos(Time.time * Mathf.PI * 4f));   // about to close

            foreach (var r in lamps)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColor, onEmission * glow);
                r.SetPropertyBlock(block);
            }
            if (pressed != null) pressed.localPosition = pressedRest + (on ? pressedOffset : Vector3.zero);
        }
    }
}
