using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// The menu's light (ARCHITECTURE §6.2 and §25.3), applied by <see cref="LookBuilder"/> through <see cref="NatureDayLook"/>: a misty
    /// blue-grey dawn in a forest clearing, one moment held for as long as the menu shows. The pale sun is low behind the show and shines
    /// towards the title camera, so the mist glows softly and the four players stand dark against it; a thick haze swallows the trees
    /// beyond thirty metres and a low mist lies on the grass, so the menu has depth and no horizon. Never set by hand: change this
    /// table and re-apply.
    /// </summary>
    public static class MenuForestLook
    {
        public const string SceneName = "Bootstrap";
        const string PresetDir = "Assets/Settings/Look/TimeOfDay/Menu/";
        const string SkyMaterialPath = "Assets/Art/Materials/Sky_Menu.mat";

        static Color C(float r, float g, float b) => new Color(r, g, b);

        // The same moment twice: NatureDayLook blends between moments; the menu holds one.
        static readonly NatureDayLook.Moment Dawn = new NatureDayLook.Moment
        {
            name = "Misty dawn", hdri = "qwantani_dawn_puresky", yaw = 196f, minElevation = 5f, maxElevation = 9f,
            sun = C(1f, 0.88f, 0.76f), intensity = 1.5f, shadow = 0.6f,
            sky = C(0.36f, 0.4f, 0.45f), equator = C(0.32f, 0.35f, 0.38f), ground = C(0.13f, 0.14f, 0.14f),
            fog = C(0.6f, 0.66f, 0.72f), fogStart = 10f, fogEnd = 80f, skyExposure = 0.4f, wind = 0.25f, exposure = 0f, temperature = -4f,
            mist = C(0.68f, 0.74f, 0.8f), mistDensity = 0.1f, mistFalloff = 3f, mistGlow = 0.5f, shafts = 0.8f
        };

        static readonly NatureDayLook.Moment Hold = WithName(Dawn, "Misty dawn (held)");

        /// <summary>The haze's colour: the menu camera clears to it, so nothing shows beyond the mist (no sky, no horizon).</summary>
        public static Color Haze => Dawn.fog;

        static NatureDayLook.Moment WithName(NatureDayLook.Moment m, string name)
        {
            m.name = name;
            return m;
        }

        /// <summary>Writes the presets, the sky and the rig, and shows the dawn in the open Bootstrap scene.</summary>
        public static void Apply(UnityEngine.SceneManagement.Scene scene) =>
            NatureDayLook.Apply(scene, new NatureDayLook.Day { moments = new[] { Dawn, Hold }, presetDir = PresetDir, skyMaterialPath = SkyMaterialPath, skySaturation = 0.8f });
    }
}
