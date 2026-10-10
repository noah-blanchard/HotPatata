using UnityEngine;

namespace HotPatata
{
    /// <summary>
    /// Presentation only (PROJECT_SPEC §13.21, §13.22): shows what a plate still needs. On a heavy plate one footprint lamp
    /// lights per counted player, so "one more" reads at a glance; on an hourglass plate a ring of lamps empties as the sand
    /// runs out after release. Brightness carries the state, never colour alone (§19). Reads the plate, never changes it.
    /// </summary>
    public class PlateGauge : MonoBehaviour
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] PressurePlate plate;
        [SerializeField, Tooltip("Heavy plate: one lamp per body it needs.")] Renderer[] bodyLamps = new Renderer[0];
        [SerializeField, Tooltip("Hourglass plate: the ring of sand, lit while held, emptying after release.")] Renderer[] sandLamps = new Renderer[0];
        [SerializeField, ColorUsage(false, true)] Color onEmission = new Color(1.2f, 0.9f, 0.2f);

        MaterialPropertyBlock block;

        void Update()
        {
            if (plate == null) return;
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < bodyLamps.Length; i++) Light(bodyLamps[i], BodyLampOn(i, plate.CountedBodies, plate.Active) ? 1f : 0f);

            float sand = ((IObstacleState)plate).Progress;
            for (int i = 0; i < sandLamps.Length; i++) Light(sandLamps[i], SandLamp(i, sandLamps.Length, sand));
        }

        void Light(Renderer r, float glow)
        {
            if (r == null) return;
            r.GetPropertyBlock(block);
            block.SetColor(EmissionColor, onEmission * glow);
            r.SetPropertyBlock(block);
        }

        /// <summary>Is footprint <paramref name="index"/> lit? One per counted body, all of them while active. Pure (EditMode tested).</summary>
        public static bool BodyLampOn(int index, int counted, bool active) => active || index < counted;

        /// <summary>How bright sand lamp <paramref name="index"/> of <paramref name="count"/> is with <paramref name="sand"/> (0..1) left. Pure.</summary>
        public static float SandLamp(int index, int count, float sand) => Mathf.Clamp01(sand * count - index);
    }
}
