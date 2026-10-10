using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// PatataTemple's day (ARCHITECTURE §25.4, M15), applied by <see cref="LookBuilder"/> through <see cref="NatureDayLook"/>: a humid
    /// jungle day in five moments over three acts. A misty golden dawn on the forecourt (act 1), a white, hard noon in the three wings
    /// (act 2), then the air grows heavy and a tropical storm closes in over the sanctuary (act 3): a low, dark, green-grey sky, a weak
    /// sun, a near haze. The mist pools in the gorge 40 m under the stone (its <see cref="MistField"/> follows the jungle floor): thick
    /// at dawn and in the storm, thin at noon. The storm's rain and lightning are presentation only (<see cref="TempleStorm"/>): no
    /// wind bends a flight (spec §17.2) and every flash honours <c>flashReduction</c>. Never set by hand: change this table and re-apply.
    /// </summary>
    public static class PatataTempleLook
    {
        const string PresetDir = "Assets/Settings/Look/TimeOfDay/Temple/";
        const string SkyMaterialPath = "Assets/Art/Materials/Sky_PatataTemple.mat";

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static readonly NatureDayLook.Moment[] Day =
        {
            new NatureDayLook.Moment { name = "1 Misty dawn", hdri = "kloofendal_misty_morning_puresky", yaw = 80f, minElevation = 6f, maxElevation = 12f,
                         sun = C(1f, 0.78f, 0.55f), intensity = 1.3f, shadow = 0.65f,
                         sky = C(0.46f, 0.52f, 0.5f), equator = C(0.52f, 0.54f, 0.44f), ground = C(0.18f, 0.2f, 0.14f),
                         fog = C(0.72f, 0.74f, 0.64f), fogStart = 70f, fogEnd = 420f, skyExposure = 0.9f, wind = 0.25f, exposure = 0.2f, temperature = 4f,
                         mist = C(0.78f, 0.8f, 0.7f), mistDensity = 0.08f, mistFalloff = 17f, mistGlow = 0.6f, shafts = 0.8f },
            new NatureDayLook.Moment { name = "2 Morning", hdri = "kloofendal_28d_misty_puresky", yaw = 95f, minElevation = 22f, maxElevation = 32f,
                         sun = C(1f, 0.9f, 0.74f), intensity = 1.55f, shadow = 0.72f,
                         sky = C(0.46f, 0.56f, 0.56f), equator = C(0.52f, 0.56f, 0.44f), ground = C(0.2f, 0.22f, 0.15f),
                         fog = C(0.66f, 0.72f, 0.66f), fogStart = 90f, fogEnd = 520f, skyExposure = 0.65f, wind = 0.3f, exposure = 0.1f, temperature = 2f,
                         mist = C(0.74f, 0.78f, 0.7f), mistDensity = 0.08f, mistFalloff = 14f, mistGlow = 0.35f, shafts = 0.55f },
            new NatureDayLook.Moment { name = "3 White noon", hdri = "kloofendal_48d_partly_cloudy_puresky", yaw = 140f, minElevation = 50f, maxElevation = 62f,
                         sun = C(1f, 0.98f, 0.93f), intensity = 1.85f, shadow = 0.8f,
                         sky = C(0.5f, 0.6f, 0.66f), equator = C(0.56f, 0.6f, 0.5f), ground = C(0.22f, 0.24f, 0.17f),
                         fog = C(0.74f, 0.8f, 0.82f), fogStart = 130f, fogEnd = 700f, skyExposure = 0.45f, wind = 0.3f, exposure = -0.05f, temperature = 1f,
                         mist = C(0.78f, 0.82f, 0.78f), mistDensity = 0.035f, mistFalloff = 12f, mistGlow = 0.15f, shafts = 0.2f },
            new NatureDayLook.Moment { name = "4 Heavy afternoon", hdri = "kloppenheim_06_puresky", yaw = 230f, minElevation = 14f, maxElevation = 22f,
                         sun = C(1f, 0.8f, 0.6f), intensity = 1.25f, shadow = 0.7f,
                         sky = C(0.4f, 0.44f, 0.46f), equator = C(0.5f, 0.48f, 0.4f), ground = C(0.18f, 0.18f, 0.14f),
                         fog = C(0.6f, 0.6f, 0.54f), fogStart = 80f, fogEnd = 460f, skyExposure = 0.8f, wind = 0.55f, exposure = 0.15f, temperature = 3f,
                         mist = C(0.64f, 0.66f, 0.6f), mistDensity = 0.09f, mistFalloff = 15f, mistGlow = 0.4f, shafts = 0.5f },
            new NatureDayLook.Moment { name = "5 Storm", hdri = "kloppenheim_01_puresky", yaw = 250f, minElevation = 4f, maxElevation = 9f,
                         sun = C(0.7f, 0.78f, 0.9f), intensity = 0.55f, shadow = 0.45f,
                         sky = C(0.24f, 0.3f, 0.32f), equator = C(0.26f, 0.3f, 0.28f), ground = C(0.1f, 0.12f, 0.1f),
                         fog = C(0.26f, 0.32f, 0.32f), fogStart = 40f, fogEnd = 260f, skyExposure = 1.2f, wind = 0.9f, exposure = 0.45f, temperature = -10f,
                         mist = C(0.3f, 0.36f, 0.36f), mistDensity = 0.08f, mistFalloff = 16f, mistGlow = 0.1f, shafts = 0f },
        };

        public static int MomentCount => Day.Length;

        public static float[] CheckpointTimes(int checkpoints) => NatureDayLook.CheckpointTimes(Day.Length, checkpoints);

        /// <summary>Writes the presets, the sky and the rig, and shows dawn in the open scene.</summary>
        public static void Apply(UnityEngine.SceneManagement.Scene scene) =>
            NatureDayLook.Apply(scene, new NatureDayLook.Day { moments = Day, presetDir = PresetDir, skyMaterialPath = SkyMaterialPath, skySaturation = 0.85f });
    }
}
