using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Builds the living menu backdrop (ARCHITECTURE §6.2) as one prefab, <c>Assets/Prefabs/Menu/MenuBackdrop.prefab</c>,
    /// and places its instance in <c>Bootstrap</c> with the menu camera, sky and fog. On a small KayKit island four
    /// mannequins in the slot colours pass the live potato (<see cref="MenuHotPotato"/>); flags sway, stars spin, little
    /// platforms bob, clouds drift, sparkles float, and the camera drifts slowly. Everything in it is visual: copies of the
    /// player's mannequin and the bomb's visual, never their gameplay components, and no colliders that matter.
    /// Idempotent: rebuild after changing it (menu HotPatata/Menu/Build Menu Backdrop), never edit it by hand.
    /// </summary>
    public static class MenuBackdropBuilder
    {
        public const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        public const string PrefabPath = "Assets/Prefabs/Menu/MenuBackdrop.prefab";
        const string ControllerPath = "Assets/Art/Models/Player/Controllers/MenuMannequin.controller";
        const string RigDir = "Assets/Art/Models/Player/Animations/fbx/Rig_Medium/";
        const string TuningPath = "Assets/ScriptableObjects/Tuning/GameTuning.asset";
        const string PlayerPrefab = "Assets/Prefabs/Player/Player.prefab";
        const string BombPrefab = "Assets/Prefabs/Bomb/Bomb.prefab";
        const string ExplosionPrefab = "Assets/Prefabs/VFX/VFX_Explosion.prefab";
        const string SparkleTexture = "Assets/Art/VFX/Textures/SoftDot.png";
        const string RootName = "MenuBackdrop";

        // The camera looks a little left of the island, so the island sits on the right, beside the left-docked menu.
        static readonly Vector3 CameraLookAt = new Vector3(-6.2f, 1.6f, 1.6f);
        static readonly Vector3 CameraPosition = new Vector3(-17.5f, 8.6f, -17.5f);
        const float CameraFov = 34f;

        /// <summary>Where the four players stand: a shallow arc open to the camera, so every one of them shows.</summary>
        static readonly Vector3[] PlayerSpots =
        {
            new Vector3(-3.4f, 0f, 0.4f), new Vector3(-1.2f, 0f, 2.7f), new Vector3(1.5f, 0f, 2.7f), new Vector3(3.6f, 0f, 0.3f)
        };

        /// <summary>Animator states of the menu mannequins (clip name = state name), all driven by MenuHotPotato.</summary>
        static readonly (string clip, string rig)[] Clips =
        {
            ("Idle_A", "General"), ("Idle_B", "General"), ("Throw", "General"), ("Hit_A", "General"),
            ("Cheering", "Simulation"), ("Waving", "Simulation"), ("Jump_Full_Short", "MovementBasic")
        };

        [MenuItem("HotPatata/Menu/Build Menu Backdrop")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath) ?? throw new InvalidOperationException("missing " + TuningPath);
            var controller = BuildController();
            BuildPrefab(tuning, controller);

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);   // reopening would tear down its NetworkManager
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName).ToList()) Object.DestroyImmediate(old);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            instance.name = RootName;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SetUpCamera(scene, tuning);
            SetUpSky();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MenuBackdropBuilder] menu backdrop built");
        }

        // ------------------------------------------------------------------ prefab

        static void BuildPrefab(GameTuning tuning, AnimatorController controller)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            var root = new GameObject(RootName);
            try
            {
                var t = root.transform;
                BuildSun(t);
                BuildIsland(Group(t, "Island"));
                BuildProps(Group(t, "Props"));
                BuildFloaters(Group(t, "Floaters"));
                PatataParkBuilder.BuildKayKitBackdrop(Group(t, "FarIslands"), 8642, 14, 25f, 200f);
                var clouds = Group(t, "Clouds");
                BuildBackdrop(clouds, 9753, 0, 0f, 0f, 26, 10f, 240f);
                clouds.gameObject.AddComponent<MenuCloudDrift>().Configure(1.4f, 190f);
                BuildSparkles(t);

                var show = Group(t, "Show");
                var players = BuildPlayers(show, controller);
                var (potato, sparks, trail, puffs) = BuildPotato(show);
                var boom = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPrefab)?.GetComponent<ExplosionFx>();
                show.gameObject.AddComponent<MenuHotPotato>().Configure(tuning, players, potato, sparks, trail, puffs, boom);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void BuildSun(Transform parent)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(parent, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(45f, 35f, 0f);   // from behind the camera's left: the island's front is lit
        }

        /// <summary>The island: a green top on stone layers that narrow downwards, a yellow terrace and a blue step at the back.</summary>
        static void BuildIsland(Transform parent)
        {
            Block(parent, "Ground", new Vector3(0f, -1f, 0.8f), new Vector3(16f, 2f, 12f), KitRole.Ground);
            Block(parent, "Rock_1", new Vector3(0f, -3f, 0.8f), new Vector3(12f, 2f, 9f), KitRole.Wall);
            Block(parent, "Rock_2", new Vector3(0f, -5f, 0.8f), new Vector3(8f, 2f, 6f), KitRole.Wall);
            Block(parent, "Rock_3", new Vector3(0f, -7f, 0.8f), new Vector3(4f, 2f, 3f), KitRole.Wall);
            Block(parent, "Terrace", new Vector3(-5f, 0.5f, 5.3f), new Vector3(5f, 1f, 3f), KitRole.Falling);
            Block(parent, "Step", new Vector3(5.5f, 1f, 5.3f), new Vector3(3f, 2f, 3f), KitRole.Mover);
            foreach (var c in parent.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);   // nothing walks here
        }

        static void BuildProps(Transform parent)
        {
            var mat = Mat(KitMaterial);
            Prop(parent, "arch_wide", KitColor.Blue, new Vector3(0f, 0f, 6.1f), 0f, mat, shadows: true);
            Prop(parent, "signage_finish_wide", KitColor.Neutral, new Vector3(0f, 0f, 6.3f), 0f, mat, shadows: true, y: 3.1f);
            Prop(parent, "spring_pad", KitColor.Green, new Vector3(-4f, 1f, 5.2f), 0f, mat, shadows: true);
            Prop(parent, "cone", KitColor.Red, new Vector3(-7.3f, 0f, -1.2f), 20f, mat, shadows: true);
            Prop(parent, "cone", KitColor.Red, new Vector3(7.2f, 0f, -3.6f), -15f, mat, shadows: true);
            Prop(parent, "ball", KitColor.Blue, new Vector3(5.8f, 0f, -3.4f), 0f, mat, shadows: true);
            Prop(parent, "barrier_1x1x1", KitColor.Yellow, new Vector3(-7.2f, 0f, 2.2f), 0f, mat, shadows: true);
            Prop(parent, "pillar_1x1x2", KitColor.Neutral, new Vector3(7.2f, 0f, 1.5f), 0f, mat, shadows: true);

            // Flags in the wind.
            Sway(Prop(parent, "flag_A", KitColor.Red, new Vector3(6.8f, 0f, 4.4f), -30f, mat, shadows: true), 4f, 2.2f, 0f);
            Sway(Prop(parent, "flag_B", KitColor.Yellow, new Vector3(-7.2f, 0f, 3.2f), 25f, mat, shadows: true), 5f, 2.7f, 0.8f);
            Sway(Prop(parent, "flag_C", KitColor.Blue, new Vector3(-6.4f, 1f, 5.6f), 10f, mat, shadows: true), 4f, 2.4f, 1.6f);

            // Collectables floating and spinning above the island.
            Spin(Prop(parent, "star", KitColor.Yellow, new Vector3(-2.2f, 4.4f, 4.2f), 0f, mat), 90f, 0.3f, 3.1f, 0f);
            Spin(Prop(parent, "star", KitColor.Yellow, new Vector3(3.2f, 5.1f, 4.8f), 0f, mat), 70f, 0.35f, 3.6f, 1.2f);
            Spin(Prop(parent, "heart", KitColor.Red, new Vector3(-5.2f, 2.8f, -1.2f), 0f, mat), 60f, 0.25f, 2.8f, 0.6f);
            Spin(Prop(parent, "diamond", KitColor.Blue, new Vector3(5.4f, 2.6f, -1.4f), 0f, mat), 80f, 0.25f, 3.3f, 2.1f);
            Spin(Prop(parent, "hoop", KitColor.Red, new Vector3(8.6f, 4.6f, 6.4f), 0f, mat), 20f, 0.4f, 5f, 0.3f);
        }

        /// <summary>Little platforms bobbing in the air around the island.</summary>
        static void BuildFloaters(Transform parent)
        {
            (Vector3 at, Vector3 size, KitRole role, float phase)[] floaters =
            {
                (new Vector3(10.5f, 2.2f, 3.5f), new Vector3(3f, 0.8f, 3f), KitRole.Mover, 0f),
                (new Vector3(-10.8f, 3.4f, 1.5f), new Vector3(2.5f, 0.8f, 2.5f), KitRole.Falling, 1.4f),
                (new Vector3(6.5f, 6f, 9.5f), new Vector3(2f, 0.6f, 2f), KitRole.Ground, 2.3f),
                (new Vector3(-7.5f, 6.8f, 9f), new Vector3(2.2f, 0.6f, 2.2f), KitRole.Mover, 3.1f)
            };
            int i = 0;
            foreach (var (at, size, role, phase) in floaters)
            {
                var pivot = Group(parent, "Floater_" + i++);
                pivot.localPosition = at;
                var block = Block(pivot, "Block", Vector3.zero, size, role);
                block.transform.localPosition = Vector3.zero;   // Block places in world space
                foreach (var c in pivot.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                pivot.gameObject.AddComponent<MenuFloat>().Configure(0.35f, 4.5f + phase, 0f, 0f, 0f, phase);
            }
        }

        static void BuildSparkles(Transform parent)
        {
            var go = new GameObject("Sparkles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 3f, 2f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.useUnscaledTime = true;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f, 0.9f), new Color(1f, 1f, 1f, 0.6f));
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 12f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 7f, 18f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.3f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MakeUnlitMaterial("Menu_Sparkle", Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(SparkleTexture), additive: true);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>The four players: copies of the player's mannequin (model and Animator only) on yaw pivots.</summary>
        static Transform[] BuildPlayers(Transform parent, AnimatorController controller)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab)?.transform.Find("Visual/Mannequin")
                         ?? throw new InvalidOperationException("no Visual/Mannequin in " + PlayerPrefab);
            var pivots = new Transform[PlayerSpots.Length];
            for (int i = 0; i < PlayerSpots.Length; i++)
            {
                var pivot = Group(parent, "Player_" + (i + 1));
                pivot.localPosition = PlayerSpots[i];
                pivot.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, 1.4f) - PlayerSpots[i]);
                var mannequin = Object.Instantiate(source.gameObject, pivot, false);
                mannequin.name = "Mannequin";
                var animator = mannequin.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                pivots[i] = pivot;
            }
            return pivots;
        }

        /// <summary>The potato: a copy of the bomb's visual (model, wick, sparks) and its flight trail, a bit bigger to read.</summary>
        static (Transform, ParticleSystem, TrailRenderer, ParticleSystem) BuildPotato(Transform parent)
        {
            var bomb = AssetDatabase.LoadAssetAtPath<GameObject>(BombPrefab)?.transform ?? throw new InvalidOperationException("missing " + BombPrefab);
            var potato = Object.Instantiate(bomb.Find("Visual").gameObject, parent, false).transform;
            potato.name = "Potato";
            potato.localScale = Vector3.one * 3f;
            var flight = Object.Instantiate(bomb.Find("FlightFx").gameObject, potato, false).transform;
            flight.name = "FlightFx";
            var sparks = potato.Find("FuseSparks")?.GetComponent<ParticleSystem>();
            if (sparks != null)
            {
                var main = sparks.main;
                main.useUnscaledTime = true;
            }
            var puffs = flight.Find("FlightPuffs")?.GetComponent<ParticleSystem>();
            if (puffs != null)
            {
                var main = puffs.main;
                main.useUnscaledTime = true;
                var emission = puffs.emission;
                emission.rateOverDistance = 1.6f;
            }
            return (potato, sparks, flight.GetComponent<TrailRenderer>(), puffs);
        }

        // ------------------------------------------------------------------ props

        static Transform Prop(Transform parent, string model, KitColor color, Vector3 at, float yaw, Material mat, bool shadows = false, float y = 0f)
        {
            PatataParkBuilder.Prop(parent, model, color, at + Vector3.up * y, yaw, mat);
            var go = parent.GetChild(parent.childCount - 1);
            if (shadows)
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        static void Sway(Transform t, float degrees, float period, float phase) =>
            t.gameObject.AddComponent<MenuFloat>().Configure(0f, 0f, 0f, degrees, period, phase);

        static void Spin(Transform t, float degreesPerSecond, float bob, float bobPeriod, float phase) =>
            t.gameObject.AddComponent<MenuFloat>().Configure(bob, bobPeriod, degreesPerSecond, 0f, 0f, phase);

        // ------------------------------------------------------------------ animator

        /// <summary>The menu mannequins' controller: one state per clip, no transitions (MenuHotPotato cross-fades).</summary>
        static AnimatorController BuildController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var s in machine.states.ToList()) machine.RemoveState(s.state);
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var rig in Clips.Select(c => c.rig).Distinct())
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(RigDir + "Rig_Medium_" + rig + ".fbx").OfType<AnimationClip>())
                    clips[clip.name] = clip;
            foreach (var (name, rig) in Clips)
            {
                if (!clips.TryGetValue(name, out var clip)) throw new InvalidOperationException($"missing clip {name} in Rig_Medium_{rig}.fbx");
                var state = machine.AddState(name);
                state.motion = clip;
                if (name == "Idle_A") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        // ------------------------------------------------------------------ scene: camera and sky

        static void SetUpCamera(UnityEngine.SceneManagement.Scene scene, GameTuning tuning)
        {
            var camera = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<Camera>()).FirstOrDefault(c => c != null)
                         ?? throw new InvalidOperationException("no camera in " + ScenePath);
            camera.transform.SetPositionAndRotation(CameraPosition, Quaternion.LookRotation(CameraLookAt - CameraPosition, Vector3.up));
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = CameraFov;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;
            var drift = camera.GetComponent<MenuCameraDrift>() ?? camera.gameObject.AddComponent<MenuCameraDrift>();
            drift.Configure(tuning, CameraLookAt);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(drift);
        }

        /// <summary>The courses' sky, ambient light and fog (ARCHITECTURE §25), saved with the scene.</summary>
        static void SetUpSky()
        {
            RenderSettings.skybox = Mat("Sky_HotPatata");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.7f, 0.7f, 0.8f);
            RenderSettings.ambientGroundColor = new Color(0.5f, 0.42f, 0.45f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.87f, 1f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 320f;
        }
    }
}
