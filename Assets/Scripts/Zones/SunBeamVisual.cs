using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (PROJECT_SPEC §13.23): draws a <see cref="SunBeam"/>'s light from the slit to the first body in it, and
    /// lights the stone eye while the light reaches it. The cue is a bright gold shaft and a lit or dark eye, kept in every look;
    /// it never flashes. Reads the beam, never changes it.
    /// </summary>
    public class SunBeamVisual : MonoBehaviour
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] SunBeam beam;
        [SerializeField, Tooltip("The shaft: a unit-long mesh along +Z from the slit; its Z scale is set to the length the light reaches.")] Transform shaft;
        [SerializeField, Min(0.1f)] float length = 12f;
        [SerializeField] Renderer[] eyeLamps = new Renderer[0];
        [SerializeField, ColorUsage(false, true)] Color eyeEmission = new Color(2f, 1.5f, 0.5f);
        [SerializeField, Min(0f), Tooltip("Seconds for the shaft to grow back (it is cut at once).")] float regrowSeconds = 0.15f;

        MaterialPropertyBlock block;
        float shown = 1f;

        void Update()
        {
            if (beam == null) return;
            float target = beam.Reach;
            shown = target < shown || regrowSeconds <= 0f ? target : Mathf.MoveTowards(shown, target, Time.deltaTime / regrowSeconds);
            if (shaft != null)
            {
                var s = shaft.localScale;
                shaft.localScale = new Vector3(s.x, s.y, Mathf.Max(0.01f, shown * length));
            }
            block ??= new MaterialPropertyBlock();
            float glow = beam.Cut ? 0f : 1f;
            foreach (var r in eyeLamps)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColor, eyeEmission * glow);
                r.SetPropertyBlock(block);
            }
        }
    }
}
