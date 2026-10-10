using System;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// The game's look (ARCHITECTURE §25): a cinematic golden hour. A low warm sun with long soft shadows, a cool sky
    /// filling the shadows, a warm haze, a sky with a glowing sun side, and a grade with ACES, more contrast, teal
    /// shadows and warm highlights, bloom only on what really shines. Applied to every scene from here (menu
    /// HotPatata/Look/Apply Look To All Scenes), never by hand: lights, sky, fog and the look volume are found by
    /// component, never by name. No chromatic aberration, no lens distortion (AGENTS), no vignette in game (the speed
    /// effects own it); flashes keep honouring flashReduction elsewhere.
    /// </summary>
    public static class LookBuilder
    {
        public const string ProfilePath = "Assets/Settings/Look/HotPatata_Look.asset";
        const string SkyMaterialPath = "Assets/Art/Materials/Sky_HotPatata.mat";
        const string RenderPipelinePath = "Assets/Settings/PC_RPAsset.asset";

        // ------------------------------------------------------------------ golden hour

        /// <summary>The sun's height above the horizon (degrees): low, for long shadows and warm light.</summary>
        public const float SunElevation = 22f;
        public static readonly Color SunColor = new Color(1f, 0.78f, 0.56f);
        public const float SunIntensity = 1.9f;
        public const float SunShadowStrength = 0.92f;

        static readonly Color AmbientSky = new Color(0.42f, 0.5f, 0.72f);
        static readonly Color AmbientEquator = new Color(0.7f, 0.56f, 0.48f);
        static readonly Color AmbientGround = new Color(0.26f, 0.22f, 0.24f);

        /// <summary>
        /// The potato must read in any light (spec §19, ARCHITECTURE §25): a stronger sky fill and a warm rim on its
        /// material, so it never turns into a dark blob in the long golden-hour shadows. Its colour is unchanged.
        /// </summary>
        const string PotatoMaterialPath = "Assets/Art/Materials/Bomb_Potato.mat";
        const float PotatoFill = 0.95f, PotatoRim = 0.55f;

        static readonly Color SkyTop = new Color(0.2f, 0.34f, 0.62f);
        public static readonly Color Horizon = new Color(0.98f, 0.72f, 0.5f);
        static readonly Color BelowHorizon = new Color(0.52f, 0.44f, 0.5f);
        static readonly Color SkySun = new Color(1f, 0.82f, 0.58f);
        static readonly Color Clouds = new Color(1f, 0.88f, 0.8f);
        const float FogStart = 55f, FogEnd = 300f;

        /// <summary>
        /// The sun's yaw per scene (degrees, the direction its light travels): from behind and to the side of the main
        /// run, so players never run straight into it and shapes get a long raking light.
        /// </summary>
        static readonly (string scene, float yaw)[] Scenes =
        {
            ("Assets/Scenes/Bootstrap.unity", 55f),
            ("Assets/Scenes/PassSandbox.unity", 35f),
            ("Assets/Scenes/IndustrialPlant.unity", 55f),
            ("Assets/Scenes/PatataWilds.unity", 75f),  // its sun then moves with the day (PatataWildsLook, ARCHITECTURE §25.3)
            ("Assets/Scenes/PatataCanopy.unity", 75f)  // its own, cooler day (PatataCanopyLook)
        };

        public static Quaternion SunRotation(float yaw) => Quaternion.Euler(SunElevation, yaw, 0f);

        public static float SunYaw(string scenePath) => Scenes.FirstOrDefault(s => s.scene == scenePath).yaw;

        [MenuItem("HotPatata/Look/Apply Look To All Scenes")]
        public static void ApplyToAllScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildProfile();
            ConfigurePipeline();
            ConfigurePotato();
            foreach (var (path, _) in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;   // a course not built yet
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ApplyToScene(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[LookBuilder] golden hour applied to every scene");
        }

        /// <summary>The open scene's sun (every directional light), ambient, fog, sky and look volume.</summary>
        public static void ApplyToScene(UnityEngine.SceneManagement.Scene scene)
        {
            float yaw = SunYaw(scene.path);
            foreach (var light in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).Where(l => l.type == LightType.Directional))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(light)) continue;   // a prefab's sun is its builder's (MenuBackdropBuilder)
                ConfigureSun(light, yaw);
            }
            ApplyRenderSettings(scene.name, yaw);
            if (IsInterior(scene.name))
            {
                // No sun at all: a directional light's shadows are camera-relative (cascades, a shadow distance), so under a roof
                // the floor would light up as you approach. The lamps (point lights, no shadows) are the only light, the same
                // everywhere whatever the player's position.
                foreach (var sun in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).Where(l => l.type == LightType.Directional))
                {
                    sun.intensity = 0f;
                    sun.shadows = LightShadows.None;
                    EditorUtility.SetDirty(sun);
                }
                // A closed, dark plant (ARCHITECTURE §25.2): a low neutral fill (no sky indoors) and a dark warm haze; the lamps
                // carry the scene.
                // The fill stands in for the light the lamps bounce off walls and floors (no GI here): bright enough that no
                // floor goes black between two lamps, neutral so the lamps keep their colour.
                RenderSettings.ambientSkyColor = new Color(0.58f, 0.56f, 0.54f);
                RenderSettings.ambientEquatorColor = new Color(0.52f, 0.49f, 0.46f);
                RenderSettings.ambientGroundColor = new Color(0.4f, 0.37f, 0.34f);
                RenderSettings.fogColor = new Color(0.2f, 0.18f, 0.16f);
                RenderSettings.fogStartDistance = 25f;
                RenderSettings.fogEndDistance = 180f;
                PlantExposure(scene);
            }
            // the nature courses: a day from dawn to dusk, HDRI skies, one preset per act (sun, fill, haze, mist, grade), blended at
            // run time; each course has its own day (NatureDayLook)
            if (scene.name == PatataWildsLook.SceneName) PatataWildsLook.Apply(scene);
            if (scene.name == PatataCanopyBuilder.SceneName) PatataCanopyLook.Apply(scene);

            var volume = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Volume>(true)).FirstOrDefault(v => v.isGlobal && v.priority <= 0f);
            if (volume == null)
            {
                var go = new GameObject("LookVolume");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
            }
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            EditorUtility.SetDirty(volume);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>The closed industrial scenes, lit by lamps only: the plant, and the menu's factory hall (Bootstrap).</summary>
        public static bool IsInterior(string sceneName) => sceneName == "IndustrialPlant" || sceneName == "Bootstrap";

        public const string PlantProfilePath = "Assets/Settings/Look/HotPatata_Look_Plant.asset";
        public const float PlantExposure_EV = 0.45f;

        /// <summary>
        /// The closed plant is lit by lamps only, so it gets more exposure than the golden-hour courses: a second global volume
        /// (priority 1) that only overrides post exposure on top of the shared look.
        /// </summary>
        static void PlantExposure(UnityEngine.SceneManagement.Scene scene)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PlantProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PlantProfilePath);
            }
            var adjust = Get<ColorAdjustments>(profile);
            adjust.postExposure.Override(0.25f + PlantExposure_EV);   // the shared look's +0.25, plus the plant's own
            EditorUtility.SetDirty(profile);
            var volume = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Volume>(true)).FirstOrDefault(v => v.isGlobal && v.priority >= 1f);
            if (volume == null)
            {
                var go = new GameObject("PlantExposureVolume");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;
            }
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
        }

        public static void ConfigureSun(Light sun, float yaw)
        {
            sun.transform.rotation = SunRotation(yaw);
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = SunShadowStrength;
            EditorUtility.SetDirty(sun);
            EditorUtility.SetDirty(sun.transform);
        }

        /// <summary>Steady practical lights for the factory (ARCHITECTURE §25); no flashes or baked dependencies.</summary>
        public static void FactoryLamp(Transform parent, Vector3 position, bool furnace = false)
        {
            var go = new GameObject(furnace ? "Furnace glow" : "Warm practical");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = furnace ? new Color(1f, 0.38f, 0.12f) : new Color(1f, 0.77f, 0.48f);
            light.intensity = furnace ? 8f : 5f;
            light.range = 20f;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
        }

        /// <summary>A steady practical light of any colour (the industrial plant's lamps and furnaces); no shadows, no baking.</summary>
        public static Light PracticalLamp(Transform parent, Vector3 position, Color color, float intensity, float range, string name = "Practical")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            return light;
        }

        /// <summary>Ambient, fog and the scene's own sky (a copy of the shared sky, its sun where this scene's sun is).</summary>
        public static void ApplyRenderSettings(string sceneName, float yaw)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Horizon;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            RenderSettings.skybox = SkyFor(sceneName, yaw);
        }

        static Material SkyFor(string sceneName, float yaw)
        {
            var shared = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath) ?? throw new InvalidOperationException("missing " + SkyMaterialPath);
            string path = SkyMaterialPath.Replace(".mat", "_" + sceneName + ".mat");
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(shared);
                AssetDatabase.CreateAsset(sky, path);
            }
            sky.SetColor("_TopColor", SkyTop);
            sky.SetColor("_HorizonColor", Horizon);
            sky.SetColor("_BottomColor", BelowHorizon);
            sky.SetFloat("_GradientPower", 1.6f);
            sky.SetVector("_SunDirection", -(SunRotation(yaw) * Vector3.forward));   // towards the sun
            sky.SetColor("_SunColor", SkySun);
            sky.SetFloat("_SunSize", 0.03f);
            sky.SetFloat("_SunGlow", 1.3f);
            sky.SetColor("_CloudColor", Clouds);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        // ------------------------------------------------------------------ grade

        /// <summary>The look volume's profile: ACES, contrast, split toning, bloom on highlights only, a warm balance.</summary>
        public static VolumeProfile BuildProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath) ?? throw new InvalidOperationException("missing " + ProfilePath);

            var tonemapping = Get<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var adjust = Get<ColorAdjustments>(profile);
            adjust.postExposure.Override(0.25f);
            adjust.contrast.Override(18f);
            adjust.saturation.Override(6f);

            var balance = Get<WhiteBalance>(profile);
            balance.temperature.Override(8f);
            balance.tint.Override(2f);

            var split = Get<SplitToning>(profile);
            split.shadows.Override(new Color(0.36f, 0.5f, 0.58f));   // teal shadows
            split.highlights.Override(new Color(0.72f, 0.56f, 0.4f));   // warm highlights
            split.balance.Override(-10f);

            var smh = Get<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(new Vector4(0.96f, 0.99f, 1.06f, -0.04f));
            smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1f, 0.95f, 0.02f));

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.86f, 0.7f));

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            EditorUtility.SetDirty(component);
            return component;
        }

        static void ConfigurePotato()
        {
            var potato = AssetDatabase.LoadAssetAtPath<Material>(PotatoMaterialPath);
            if (potato == null) return;
            potato.SetFloat("_AmbientStrength", PotatoFill);
            potato.SetFloat("_RimStrength", PotatoRim);
            potato.SetColor("_RimColor", new Color(1f, 0.8f, 0.55f));
            EditorUtility.SetDirty(potato);
        }

        /// <summary>Longer shadows for the low sun: more distance, the same resolution and cascades.</summary>
        static void ConfigurePipeline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RenderPipelinePath);
            if (asset == null) return;
            asset.shadowDistance = 75f;
            EditorUtility.SetDirty(asset);
        }
    }
}
