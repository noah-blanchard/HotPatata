using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// PatataRanger's morning (ARCHITECTURE §25.5, M17), applied by <see cref="LookBuilder"/> through <see cref="NatureDayLook"/>: the light
    /// tells the climb. A blue dawn in the camp, the ravine's cold mist, the first sun touching the old oak, a bright morning in the
    /// walkways and the winch house, then clear sun over the crowns with the mist left below (the lookout). The mist pools on the
    /// valley floor (its <see cref="MistField"/> follows the ground): thick at dawn, thinner as the team climbs out of it. Never set by
    /// hand: change this table and re-apply.
    /// </summary>
    public static class PatataRangerLook
    {
        /// <summary>PatataRanger's scene (an authored level since M18.4c: its look is applied by its name).</summary>
        public const string SceneName = "PatataRanger";

        const string PresetDir = "Assets/Settings/Look/TimeOfDay/Ranger/";
        const string SkyMaterialPath = "Assets/Art/Materials/Sky_PatataRanger.mat";

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static readonly NatureDayLook.Moment[] Day =
        {
            new NatureDayLook.Moment { name = "1 Blue dawn", hdri = "kloofendal_misty_morning_puresky", yaw = 70f, minElevation = 4f, maxElevation = 9f,
                         sun = C(1f, 0.74f, 0.52f), intensity = 1.05f, shadow = 0.6f,
                         sky = C(0.42f, 0.5f, 0.6f), equator = C(0.44f, 0.48f, 0.5f), ground = C(0.16f, 0.17f, 0.16f),
                         fog = C(0.58f, 0.64f, 0.7f), fogStart = 18f, fogEnd = 170f, skyExposure = 0.95f, wind = 0.15f, exposure = 0.3f, temperature = -6f,
                         mist = C(0.66f, 0.72f, 0.78f), mistDensity = 0.055f, mistFalloff = 12f, mistGlow = 0.55f, shafts = 0.7f },
            new NatureDayLook.Moment { name = "2 Ravine mist", hdri = "kloofendal_28d_misty_puresky", yaw = 78f, minElevation = 8f, maxElevation = 14f,
                         sun = C(1f, 0.82f, 0.62f), intensity = 1.25f, shadow = 0.66f,
                         sky = C(0.44f, 0.52f, 0.58f), equator = C(0.48f, 0.52f, 0.5f), ground = C(0.17f, 0.18f, 0.15f),
                         fog = C(0.62f, 0.68f, 0.7f), fogStart = 20f, fogEnd = 190f, skyExposure = 0.85f, wind = 0.2f, exposure = 0.2f, temperature = -2f,
                         mist = C(0.7f, 0.75f, 0.78f), mistDensity = 0.065f, mistFalloff = 13f, mistGlow = 0.5f, shafts = 0.8f },
            new NatureDayLook.Moment { name = "3 First sun", hdri = "qwantani_dawn_puresky", yaw = 88f, minElevation = 14f, maxElevation = 22f,
                         sun = C(1f, 0.86f, 0.66f), intensity = 1.5f, shadow = 0.72f,
                         sky = C(0.46f, 0.55f, 0.6f), equator = C(0.52f, 0.54f, 0.46f), ground = C(0.19f, 0.19f, 0.15f),
                         fog = C(0.66f, 0.7f, 0.68f), fogStart = 26f, fogEnd = 230f, skyExposure = 0.7f, wind = 0.25f, exposure = 0.1f, temperature = 3f,
                         mist = C(0.74f, 0.77f, 0.76f), mistDensity = 0.1f, mistFalloff = 16f, mistGlow = 0.45f, shafts = 0.75f },
            new NatureDayLook.Moment { name = "4 Bright morning", hdri = "kloofendal_48d_partly_cloudy_puresky", yaw = 105f, minElevation = 26f, maxElevation = 36f,
                         sun = C(1f, 0.93f, 0.8f), intensity = 1.7f, shadow = 0.76f,
                         sky = C(0.48f, 0.58f, 0.66f), equator = C(0.54f, 0.58f, 0.5f), ground = C(0.2f, 0.21f, 0.16f),
                         fog = C(0.7f, 0.76f, 0.78f), fogStart = 32f, fogEnd = 280f, skyExposure = 0.55f, wind = 0.3f, exposure = 0f, temperature = 2f,
                         mist = C(0.78f, 0.81f, 0.8f), mistDensity = 0.085f, mistFalloff = 20f, mistGlow = 0.3f, shafts = 0.45f },
            new NatureDayLook.Moment { name = "5 Clear over the crowns", hdri = "qwantani_noon_puresky", yaw = 120f, minElevation = 38f, maxElevation = 48f,
                         sun = C(1f, 0.97f, 0.9f), intensity = 1.85f, shadow = 0.8f,
                         sky = C(0.5f, 0.6f, 0.7f), equator = C(0.56f, 0.6f, 0.54f), ground = C(0.21f, 0.22f, 0.17f),
                         fog = C(0.74f, 0.8f, 0.84f), fogStart = 42f, fogEnd = 340f, skyExposure = 0.45f, wind = 0.35f, exposure = -0.05f, temperature = 1f,
                         mist = C(0.82f, 0.85f, 0.86f), mistDensity = 0.1f, mistFalloff = 22f, mistGlow = 0.2f, shafts = 0.25f },
        };

        public static int MomentCount => Day.Length;

        public static float[] CheckpointTimes(int checkpoints) => NatureDayLook.CheckpointTimes(Day.Length, checkpoints);

        /// <summary>Writes the presets, the sky and the rig, and shows dawn in the open scene.</summary>
        public static void Apply(UnityEngine.SceneManagement.Scene scene) =>
            NatureDayLook.Apply(scene, new NatureDayLook.Day { moments = Day, presetDir = PresetDir, skyMaterialPath = SkyMaterialPath, skySaturation = 0.9f });
    }
}
