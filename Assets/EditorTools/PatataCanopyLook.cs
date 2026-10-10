using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// PatataCanopy's day (ARCHITECTURE §25.3, M13.7), applied by <see cref="LookBuilder"/> through <see cref="NatureDayLook"/>: the
    /// same five moments and skies as PatataWilds, cooler and softer for a quiet forest. A green-teal haze, a softer sun, a paler
    /// sky. A height mist pools on the forest floor 40 m under the decks (its <see cref="MistField"/> follows the ground): thick at
    /// dawn, thin at noon, back at sunset and dusk; at deck height it barely shows, so passes and teammates stay crisp. The light
    /// shafts through the crowns are strongest with a low sun. The sun carries a leaf cookie (<see cref="PatataCanopyBuilder"/>).
    /// Never set by hand: change this table and re-apply.
    /// </summary>
    public static class PatataCanopyLook
    {
        const string PresetDir = "Assets/Settings/Look/TimeOfDay/Canopy/";
        const string SkyMaterialPath = "Assets/Art/Materials/Sky_PatataCanopy.mat";

        static Color C(float r, float g, float b) => new Color(r, g, b);

        // The day, act by act (the skies and the sun's bounds of PatataWilds, so the elevations still follow the photos).
        static readonly NatureDayLook.Moment[] Day =
        {
            new NatureDayLook.Moment { name = "1 Dawn", hdri = "qwantani_dawn_puresky", yaw = 75f, minElevation = 7f, maxElevation = 14f,
                         sun = C(1f, 0.8f, 0.62f), intensity = 1.35f, shadow = 0.7f,
                         sky = C(0.42f, 0.5f, 0.56f), equator = C(0.5f, 0.54f, 0.48f), ground = C(0.18f, 0.2f, 0.16f),
                         fog = C(0.66f, 0.72f, 0.7f), fogStart = 30f, fogEnd = 190f, skyExposure = 0.85f, wind = 0.3f, exposure = 0.2f, temperature = -2f,
                         mist = C(0.7f, 0.78f, 0.76f), mistDensity = 0.11f, mistFalloff = 16f, mistGlow = 0.5f, shafts = 0.8f },
            new NatureDayLook.Moment { name = "2 Noon", hdri = "qwantani_noon_puresky", yaw = 20f, minElevation = 48f, maxElevation = 62f,
                         sun = C(1f, 0.97f, 0.92f), intensity = 1.7f, shadow = 0.75f,
                         sky = C(0.46f, 0.58f, 0.66f), equator = C(0.5f, 0.56f, 0.5f), ground = C(0.2f, 0.23f, 0.18f),
                         fog = C(0.58f, 0.7f, 0.78f), fogStart = 40f, fogEnd = 300f, skyExposure = 0.5f, wind = 0.35f, exposure = 0.05f, temperature = -8f,
                         mist = C(0.66f, 0.76f, 0.74f), mistDensity = 0.04f, mistFalloff = 12f, mistGlow = 0.15f, shafts = 0.25f },
            new NatureDayLook.Moment { name = "3 Late afternoon", hdri = "qwantani_late_afternoon_puresky", yaw = 230f, minElevation = 20f, maxElevation = 30f,
                         sun = C(1f, 0.86f, 0.68f), intensity = 1.6f, shadow = 0.78f,
                         sky = C(0.44f, 0.5f, 0.58f), equator = C(0.56f, 0.52f, 0.44f), ground = C(0.2f, 0.19f, 0.16f),
                         fog = C(0.7f, 0.72f, 0.62f), fogStart = 35f, fogEnd = 250f, skyExposure = 0.7f, wind = 0.4f, exposure = 0.1f, temperature = -2f,
                         mist = C(0.74f, 0.76f, 0.66f), mistDensity = 0.07f, mistFalloff = 13f, mistGlow = 0.45f, shafts = 0.9f },
            new NatureDayLook.Moment { name = "4 Sunset", hdri = "qwantani_sunset_puresky", yaw = 250f, minElevation = 7f, maxElevation = 11f,
                         sun = C(1f, 0.66f, 0.45f), intensity = 1.3f, shadow = 0.75f,
                         sky = C(0.36f, 0.38f, 0.5f), equator = C(0.58f, 0.44f, 0.38f), ground = C(0.16f, 0.14f, 0.14f),
                         fog = C(0.72f, 0.58f, 0.5f), fogStart = 30f, fogEnd = 220f, skyExposure = 0.85f, wind = 0.5f, exposure = 0.15f, temperature = 4f,
                         mist = C(0.72f, 0.62f, 0.58f), mistDensity = 0.085f, mistFalloff = 14f, mistGlow = 0.55f, shafts = 0.6f },
            new NatureDayLook.Moment { name = "5 Dusk", hdri = "qwantani_dusk_2_puresky", yaw = 262f, minElevation = 2f, maxElevation = 5f,
                         sun = C(0.6f, 0.66f, 0.95f), intensity = 0.45f, shadow = 0.5f,
                         sky = C(0.22f, 0.28f, 0.38f), equator = C(0.24f, 0.26f, 0.3f), ground = C(0.1f, 0.11f, 0.12f),
                         fog = C(0.18f, 0.23f, 0.33f), fogStart = 30f, fogEnd = 180f, skyExposure = 1.5f, wind = 0.55f, exposure = 0.5f, temperature = -18f,
                         mist = C(0.24f, 0.3f, 0.36f), mistDensity = 0.1f, mistFalloff = 15f, mistGlow = 0.15f, shafts = 0f },
        };

        public static int MomentCount => Day.Length;

        public static float[] CheckpointTimes(int checkpoints) => NatureDayLook.CheckpointTimes(Day.Length, checkpoints);

        /// <summary>Writes the presets, the sky and the rig, and shows dawn in the open scene; then the sun's leaf cookie.</summary>
        public static void Apply(UnityEngine.SceneManagement.Scene scene)
        {
            NatureDayLook.Apply(scene, new NatureDayLook.Day { moments = Day, presetDir = PresetDir, skyMaterialPath = SkyMaterialPath, skySaturation = 0.75f });
            PatataCanopyBuilder.ApplyDapple(scene);
        }
    }
}
