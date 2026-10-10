using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// PatataWilds' day (ARCHITECTURE §25.3), applied by <see cref="LookBuilder"/> through <see cref="NatureDayLook"/>: five
    /// warm golden-hour moments (dawn, noon, late afternoon, sunset, dusk) and a morning mist low in the gorges and over the river,
    /// thin at noon, back at dusk. Never set by hand: change this table and re-apply.
    /// </summary>
    public static class PatataWildsLook
    {
        public const string SceneName = "PatataWilds";
        const string PresetDir = "Assets/Settings/Look/TimeOfDay/";
        const string SkyMaterialPath = "Assets/Art/Materials/Sky_PatataWilds.mat";

        static Color C(float r, float g, float b) => new Color(r, g, b);

        // The day, act by act. Yaw: the direction the sunlight travels; the elevation follows the photographed sun within the bounds.
        static readonly NatureDayLook.Moment[] Day =
        {
            new NatureDayLook.Moment { name = "1 Dawn", hdri = "qwantani_dawn_puresky", yaw = 75f, minElevation = 7f, maxElevation = 14f,
                         sun = C(1f, 0.68f, 0.44f), intensity = 1.6f, shadow = 0.82f,
                         sky = C(0.4f, 0.46f, 0.62f), equator = C(0.68f, 0.56f, 0.5f), ground = C(0.22f, 0.2f, 0.19f),
                         fog = C(0.82f, 0.68f, 0.6f), fogStart = 22f, fogEnd = 230f, skyExposure = 1f, wind = 0.25f, exposure = 0.2f, temperature = 10f,
                         mist = C(0.86f, 0.76f, 0.7f), mistDensity = 0.03f, mistFalloff = 4f, mistGlow = 0.45f, shafts = 0f },
            new NatureDayLook.Moment { name = "2 Noon", hdri = "qwantani_noon_puresky", yaw = 20f, minElevation = 48f, maxElevation = 62f,
                         sun = C(1f, 0.95f, 0.88f), intensity = 2.1f, shadow = 0.85f,
                         sky = C(0.48f, 0.6f, 0.82f), equator = C(0.6f, 0.6f, 0.58f), ground = C(0.27f, 0.25f, 0.21f),
                         fog = C(0.7f, 0.78f, 0.88f), fogStart = 70f, fogEnd = 460f, skyExposure = 0.55f, wind = 0.35f, exposure = 0f, temperature = -3f,
                         mist = C(0.78f, 0.84f, 0.9f), mistDensity = 0.002f, mistFalloff = 3f, mistGlow = 0.1f, shafts = 0f },
            new NatureDayLook.Moment { name = "3 Late afternoon", hdri = "qwantani_late_afternoon_puresky", yaw = 230f, minElevation = 20f, maxElevation = 30f,
                         sun = C(1f, 0.8f, 0.58f), intensity = 2f, shadow = 0.9f,
                         sky = C(0.42f, 0.5f, 0.72f), equator = C(0.7f, 0.56f, 0.48f), ground = C(0.26f, 0.22f, 0.22f),
                         fog = C(0.92f, 0.74f, 0.56f), fogStart = 60f, fogEnd = 380f, skyExposure = 0.8f, wind = 0.45f, exposure = 0.05f, temperature = 9f,
                         mist = C(0.9f, 0.78f, 0.62f), mistDensity = 0.006f, mistFalloff = 3f, mistGlow = 0.3f, shafts = 0f },
            new NatureDayLook.Moment { name = "4 Sunset", hdri = "qwantani_sunset_puresky", yaw = 250f, minElevation = 7f, maxElevation = 11f,
                         sun = C(1f, 0.56f, 0.32f), intensity = 1.6f, shadow = 0.85f,
                         sky = C(0.36f, 0.36f, 0.55f), equator = C(0.74f, 0.46f, 0.38f), ground = C(0.2f, 0.16f, 0.17f),
                         fog = C(0.92f, 0.56f, 0.4f), fogStart = 45f, fogEnd = 330f, skyExposure = 1f, wind = 0.55f, exposure = 0.12f, temperature = 13f,
                         mist = C(0.88f, 0.62f, 0.5f), mistDensity = 0.014f, mistFalloff = 4f, mistGlow = 0.5f, shafts = 0f },
            new NatureDayLook.Moment { name = "5 Dusk", hdri = "qwantani_dusk_2_puresky", yaw = 262f, minElevation = 2f, maxElevation = 5f,
                         sun = C(0.62f, 0.62f, 0.95f), intensity = 0.55f, shadow = 0.55f,
                         sky = C(0.26f, 0.3f, 0.46f), equator = C(0.32f, 0.28f, 0.36f), ground = C(0.12f, 0.11f, 0.14f),
                         fog = C(0.28f, 0.29f, 0.42f), fogStart = 35f, fogEnd = 260f, skyExposure = 1.8f, wind = 0.7f, exposure = 0.5f, temperature = -16f,
                         mist = C(0.34f, 0.35f, 0.48f), mistDensity = 0.022f, mistFalloff = 4f, mistGlow = 0.2f, shafts = 0f },
        };

        public static int MomentCount => Day.Length;

        /// <summary>The time of day at each checkpoint id: dawn at the start, dusk at the last checkpoint, evenly in between.</summary>
        public static float[] CheckpointTimes(int checkpoints) => NatureDayLook.CheckpointTimes(Day.Length, checkpoints);

        /// <summary>Writes the presets, the sky and the rig, and shows dawn in the open scene.</summary>
        public static void Apply(UnityEngine.SceneManagement.Scene scene) =>
            NatureDayLook.Apply(scene, new NatureDayLook.Day { moments = Day, presetDir = PresetDir, skyMaterialPath = SkyMaterialPath, skySaturation = 1f });
    }
}
