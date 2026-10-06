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
    /// PatataWilds' light (ARCHITECTURE §25.3), applied by <see cref="LookBuilder"/> after the shared golden-hour settings: five
    /// moments of one day (dawn, noon, late afternoon, sunset, dusk), each a Poly Haven sky photographed at one place
    /// (Qwantani, so the horizon stays the same), a sun turned to where that photo's sun is, a sky fill, a haze and a grade.
    /// It writes the <see cref="TimeOfDayPreset"/> assets, the blended sky material and the scene's <see cref="TimeOfDayBlender"/>
    /// rig (one grade volume per moment), and leaves the scene showing dawn. Never set by hand: change this table and re-apply.
    /// </summary>
    public static class PatataWildsLook
    {
        public const string SceneName = "PatataWilds";
        const string PresetDir = "Assets/Settings/Look/TimeOfDay/";
        const string SkyMaterialPath = "Assets/Art/Materials/Sky_PatataWilds.mat";
        const string HdriDir = NatureAssetImporter.HdriFolder;

        struct Moment
        {
            public string name, hdri;
            public float yaw, minElevation, maxElevation;
            public Color sun;
            public float intensity, shadow;
            public Color sky, equator, ground, fog;
            public float fogStart, fogEnd, skyExposure, wind, exposure, temperature;
        }

        static Color C(float r, float g, float b) => new Color(r, g, b);

        // The day, act by act. Yaw: the direction the sunlight travels; the elevation follows the photographed sun within the bounds.
        static readonly Moment[] Day =
        {
            new Moment { name = "1 Dawn", hdri = "qwantani_dawn_puresky", yaw = 75f, minElevation = 7f, maxElevation = 14f,
                         sun = C(1f, 0.68f, 0.44f), intensity = 1.6f, shadow = 0.82f,
                         sky = C(0.4f, 0.46f, 0.62f), equator = C(0.68f, 0.56f, 0.5f), ground = C(0.22f, 0.2f, 0.19f),
                         fog = C(0.82f, 0.68f, 0.6f), fogStart = 22f, fogEnd = 230f, skyExposure = 1f, wind = 0.25f, exposure = 0.2f, temperature = 10f },
            new Moment { name = "2 Noon", hdri = "qwantani_noon_puresky", yaw = 20f, minElevation = 48f, maxElevation = 62f,
                         sun = C(1f, 0.95f, 0.88f), intensity = 2.1f, shadow = 0.85f,
                         sky = C(0.48f, 0.6f, 0.82f), equator = C(0.6f, 0.6f, 0.58f), ground = C(0.27f, 0.25f, 0.21f),
                         fog = C(0.7f, 0.78f, 0.88f), fogStart = 70f, fogEnd = 460f, skyExposure = 0.55f, wind = 0.35f, exposure = 0f, temperature = -3f },
            new Moment { name = "3 Late afternoon", hdri = "qwantani_late_afternoon_puresky", yaw = 230f, minElevation = 20f, maxElevation = 30f,
                         sun = C(1f, 0.8f, 0.58f), intensity = 2f, shadow = 0.9f,
                         sky = C(0.42f, 0.5f, 0.72f), equator = C(0.7f, 0.56f, 0.48f), ground = C(0.26f, 0.22f, 0.22f),
                         fog = C(0.92f, 0.74f, 0.56f), fogStart = 60f, fogEnd = 380f, skyExposure = 0.8f, wind = 0.45f, exposure = 0.05f, temperature = 9f },
            new Moment { name = "4 Sunset", hdri = "qwantani_sunset_puresky", yaw = 250f, minElevation = 7f, maxElevation = 11f,
                         sun = C(1f, 0.56f, 0.32f), intensity = 1.6f, shadow = 0.85f,
                         sky = C(0.36f, 0.36f, 0.55f), equator = C(0.74f, 0.46f, 0.38f), ground = C(0.2f, 0.16f, 0.17f),
                         fog = C(0.92f, 0.56f, 0.4f), fogStart = 45f, fogEnd = 330f, skyExposure = 1f, wind = 0.55f, exposure = 0.12f, temperature = 13f },
            new Moment { name = "5 Dusk", hdri = "qwantani_dusk_2_puresky", yaw = 262f, minElevation = 2f, maxElevation = 5f,
                         sun = C(0.62f, 0.62f, 0.95f), intensity = 0.55f, shadow = 0.55f,
                         sky = C(0.26f, 0.3f, 0.46f), equator = C(0.32f, 0.28f, 0.36f), ground = C(0.12f, 0.11f, 0.14f),
                         fog = C(0.28f, 0.29f, 0.42f), fogStart = 35f, fogEnd = 260f, skyExposure = 1.8f, wind = 0.7f, exposure = 0.5f, temperature = -16f },
        };

        public static int MomentCount => Day.Length;

        /// <summary>The time of day at each checkpoint id: dawn at the start, dusk at the last checkpoint, evenly in between.</summary>
        public static float[] CheckpointTimes(int checkpoints)
        {
            var times = new float[checkpoints + 1];
            for (int id = 0; id <= checkpoints; id++) times[id] = checkpoints == 0 ? 0f : (Day.Length - 1) * id / (float)checkpoints;
            return times;
        }

        /// <summary>Writes the presets, the sky and the rig, and shows dawn in the open scene.</summary>
        public static void Apply(UnityEngine.SceneManagement.Scene scene)
        {
            System.IO.Directory.CreateDirectory(PresetDir.TrimEnd('/'));
            var presets = Day.Select(WritePreset).ToArray();
            var sky = SkyMaterial(presets);
            var first = presets[0];

            var sun = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).FirstOrDefault(l => l.type == LightType.Directional && !PrefabUtility.IsPartOfPrefabInstance(l));
            if (sun != null)
            {
                sun.transform.rotation = first.SunRotation;
                sun.color = first.sunColor;
                sun.intensity = first.sunIntensity;
                sun.shadowStrength = first.shadowStrength;
                sun.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(sun);
                EditorUtility.SetDirty(sun.transform);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = first.ambientSky;
            RenderSettings.ambientEquatorColor = first.ambientEquator;
            RenderSettings.ambientGroundColor = first.ambientGround;
            RenderSettings.fogColor = first.fogColor;
            RenderSettings.fogStartDistance = first.fogStart;
            RenderSettings.fogEndDistance = first.fogEnd;
            RenderSettings.skybox = sky;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = first.sky;
            RenderSettings.reflectionIntensity = 1f;

            int checkpoints = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Checkpoint>(true)).Select(c => c.Id).DefaultIfEmpty(0).Max();
            var blender = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TimeOfDayBlender>(true)).FirstOrDefault();
            if (blender == null)
            {
                var go = new GameObject("TimeOfDay");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                blender = go.AddComponent<TimeOfDayBlender>();
            }
            var grades = new Volume[presets.Length];
            for (int i = 0; i < presets.Length; i++)
            {
                string name = "Grade " + Day[i].name;
                var child = blender.transform.Find(name);
                if (child == null)
                {
                    child = new GameObject(name).transform;
                    child.SetParent(blender.transform, false);
                }
                var volume = child.GetComponent<Volume>() ?? child.gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;
                volume.sharedProfile = presets[i].grade;
                volume.weight = i == 0 ? 1f : 0f;
                grades[i] = volume;
                EditorUtility.SetDirty(volume);
            }
            var tuning = AssetDatabase.FindAssets("t:GameTuning").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameTuning>).FirstOrDefault();
            blender.Configure(tuning, presets, CheckpointTimes(checkpoints), sun, grades, sky);
            EditorUtility.SetDirty(blender);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static TimeOfDayPreset WritePreset(Moment m)
        {
            string path = PresetDir + "TimeOfDay " + m.name + ".asset";
            var preset = AssetDatabase.LoadAssetAtPath<TimeOfDayPreset>(path);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<TimeOfDayPreset>();
                AssetDatabase.CreateAsset(preset, path);
            }
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(HdriDir + m.hdri + "_2k.hdr")
                       ?? throw new System.InvalidOperationException($"missing sky {m.hdri} (run tools/Fetch-PolyHaven.ps1)");
            var (azimuth, elevation) = MeasureSun(cube);
            preset.sky = cube;
            preset.sunYaw = m.yaw;
            preset.sunElevation = Mathf.Clamp(elevation, m.minElevation, m.maxElevation);
            // the photographed sun is drawn where the light comes from: towards the sun = minus the light's travel direction
            var towardsSun = -(Quaternion.Euler(preset.sunElevation, m.yaw, 0f) * Vector3.forward);
            float worldAzimuth = Mathf.Atan2(towardsSun.z, towardsSun.x) * Mathf.Rad2Deg;
            preset.skyRotation = Mathf.Repeat(azimuth - worldAzimuth, 360f);
            preset.skyExposure = m.skyExposure;
            preset.sunColor = m.sun;
            preset.sunIntensity = m.intensity;
            preset.shadowStrength = m.shadow;
            preset.ambientSky = m.sky;
            preset.ambientEquator = m.equator;
            preset.ambientGround = m.ground;
            preset.fogColor = m.fog;
            preset.fogStart = m.fogStart;
            preset.fogEnd = m.fogEnd;
            preset.wind = m.wind;
            preset.grade = Grade(m);
            EditorUtility.SetDirty(preset);
            return preset;
        }

        static VolumeProfile Grade(Moment m)
        {
            string path = PresetDir + "Grade " + m.name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            var adjust = Get<ColorAdjustments>(profile);
            adjust.postExposure.Override(0.25f + m.exposure);   // the shared look's +0.25, plus this moment's own
            var balance = Get<WhiteBalance>(profile);
            balance.temperature.Override(m.temperature);
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

        static Material SkyMaterial(TimeOfDayPreset[] presets)
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("HotPatata/SkyBlend"));
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            }
            sky.SetTexture("_SkyA", presets[0].sky);
            sky.SetFloat("_ExposureA", presets[0].skyExposure);
            sky.SetFloat("_RotationA", presets[0].skyRotation);
            sky.SetTexture("_SkyB", presets[1].sky);
            sky.SetFloat("_ExposureB", presets[1].skyExposure);
            sky.SetFloat("_RotationB", presets[1].skyRotation);
            sky.SetFloat("_Blend", 0f);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        /// <summary>
        /// Where the photographed sun is in <paramref name="cube"/>: the brightest directions of a latitude-longitude unwrap, weighted
        /// by brightness. Returns the azimuth (degrees, atan2(z, x), the convention of HotPatata/SkyBlend) and the elevation.
        /// </summary>
        public static (float azimuth, float elevation) MeasureSun(Cubemap cube)
        {
            const int w = 512, h = 256;
            var mat = new Material(Shader.Find("Hidden/HotPatata/CubeToLatLong"));
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            var tex = new Texture2D(w, h, TextureFormat.RGBAHalf, false, true);
            try
            {
                mat.SetTexture("_Cube", cube);
                Graphics.Blit(null, rt, mat);
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                var pixels = tex.GetPixels();
                float max = 0f;
                foreach (var p in pixels) max = Mathf.Max(max, p.r * 0.2126f + p.g * 0.7152f + p.b * 0.0722f);
                Vector3 sum = Vector3.zero;
                // where the render target's first row lands depends on the graphics API (Direct3D starts at the top)
                float rowSign = SystemInfo.graphicsUVStartsAtTop ? -1f : 1f;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var p = pixels[y * w + x];
                        float lum = p.r * 0.2126f + p.g * 0.7152f + p.b * 0.0722f;
                        if (lum < max * 0.6f) continue;
                        float phi = ((x + 0.5f) / w) * Mathf.PI * 2f - Mathf.PI, theta = rowSign * ((y + 0.5f) / h - 0.5f) * Mathf.PI;
                        sum += new Vector3(Mathf.Cos(theta) * Mathf.Cos(phi), Mathf.Sin(theta), Mathf.Cos(theta) * Mathf.Sin(phi)) * lum;
                    }
                var d = sum.normalized;
                return (Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg, Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(mat);
            }
        }
    }
}
