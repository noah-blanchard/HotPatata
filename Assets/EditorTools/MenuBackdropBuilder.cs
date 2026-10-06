using System;
using System.Collections.Generic;
using System.Linq;
using HotPatata;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using static HotPatata.Editor.CourseKit;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Builds the living menu backdrop (ARCHITECTURE §6.2) as one prefab, <c>Assets/Prefabs/Menu/MenuBackdrop.prefab</c>,
    /// and places its instance in <c>Bootstrap</c> with the menu camera. In a closed factory hall drawn in the industrial look
    /// (§25.2, matching the UI theme) the four slots' characters pass the live potato from hand to hand on a marked bay
    /// (<see cref="MenuHotPotato"/>); lamps hang from the trusses, dust drifts in their light, and the camera drifts slowly. Everything in it is visual: copies of the
    /// players' characters and the bomb's visual, never their gameplay components, and no colliders that matter.
    /// The menu itself lives in the hall too (#79): four stations (Title, Play, Level, Lobby), each a world-space board
    /// with a Cinemachine camera spot and a framing volume (vignette), the lobby stage the players step onto, and the
    /// menu camera's brain and rig. Fill, fog and grade come from <see cref="LookBuilder"/> (an interior: no sun).
    /// Idempotent: rebuild after changing it (menu HotPatata/Menu/Build Menu Backdrop), never edit it by hand.
    /// </summary>
    public static class MenuBackdropBuilder
    {
        public const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        public const string PrefabPath = "Assets/Prefabs/Menu/MenuBackdrop.prefab";
        const string TuningPath = "Assets/ScriptableObjects/Tuning/GameTuning.asset";
        const string BombPrefab = "Assets/Prefabs/Bomb/Bomb.prefab";
        const string ExplosionPrefab = "Assets/Prefabs/VFX/VFX_Explosion.prefab";
        const string SparkleTexture = "Assets/Art/VFX/Textures/SoftDot.png";
        const string WorldPanelPath = "Assets/UI/Resources/HotPatataWorldPanel.asset";
        const string ScreenPanelPath = "Assets/UI/Resources/HotPatataPanel.asset";
        const string RootName = "MenuBackdrop";
        const string BoardLayer = "UI";          // the boards take the mouse (PanelInputConfiguration's interaction layer)
        const string PlateLayer = "Default";     // nameplates are read, never clicked
        const float CameraFov = 34f;
        const float MenuPotatoScale = 1.4f;   // the show's potato, relative to the game's (GameTuning.potatoVisualScale)
        const float BoardPixelsPerMetre = 200f;  // board layouts are authored in px, like the screens

        /// <summary>
        /// One station: where its board stands (centre) and its size in px, and the camera spot that frames it. Boards
        /// stand upright and face their camera. Decoration stays clear of the boards so nothing hides them.
        /// </summary>
        static readonly (MenuStationId id, Vector3 board, Vector2 px, Vector3 camera, Vector3 lookAt)[] Stations =
        {
            // Title: the logo floats over the island, the players pass the potato below it.
            (MenuStationId.Title, new Vector3(0f, 7.6f, 5.2f), new Vector2(1800f, 1150f), new Vector3(-9.5f, 6.8f, -15f), new Vector3(0f, 4.3f, 2.5f)),
            // Play: a board at the island's front left, filling the left of the view, the island on the right.
            (MenuStationId.Play, new Vector3(-9.5f, 3f, -3.5f), new Vector2(780f, 1000f), new Vector3(-13.3f, 4f, -13.3f), new Vector3(-7.1f, 3f, -4.4f)),
            // Level: off the island's front right corner, clear of the floaters, the island on the left.
            (MenuStationId.Level, new Vector3(11f, 3f, -3f), new Vector2(760f, 640f), new Vector3(13.8f, 3.6f, -10.4f), new Vector3(9.1f, 2.8f, -3.7f)),
            // Lobby: a wide board over the stage at the island's front, the players lined up under it.
            (MenuStationId.Lobby, new Vector3(0f, 4.7f, -0.8f), new Vector2(1440f, 720f), new Vector3(0f, 3.5f, -15f), new Vector3(0f, 3.1f, -1f))
        };

        /// <summary>The lobby stage: one spot per slot in a line under the Lobby board, facing its camera.</summary>
        static readonly Vector3[] StageSpots =
        {
            new Vector3(-3f, 0f, -2.8f), new Vector3(-1f, 0f, -2.8f), new Vector3(1f, 0f, -2.8f), new Vector3(3f, 0f, -2.8f)
        };
        const float PlateHeight = 2.3f;   // over the character's head
        static readonly Vector2 PlatePx = new Vector2(340f, 84f);

        /// <summary>Where the four players stand: a shallow arc open to the camera, so every one of them shows.</summary>
        static readonly Vector3[] PlayerSpots =
        {
            new Vector3(-3.4f, 0f, 0.4f), new Vector3(-1.2f, 0f, 2.7f), new Vector3(1.5f, 0f, 2.7f), new Vector3(3.6f, 0f, 0.3f)
        };

        [MenuItem("HotPatata/Menu/Build Menu Backdrop")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath) ?? throw new InvalidOperationException("missing " + TuningPath);
            var controller = PlayerCharacterBuilder.MenuController ?? throw new InvalidOperationException("build the characters first (HotPatata/Player/Build Characters)");
            BuildPrefab(tuning, controller);

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);   // reopening would tear down its NetworkManager
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName).ToList()) Object.DestroyImmediate(old);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            instance.name = RootName;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SetUpCamera(scene, tuning);
            LookBuilder.ApplyToScene(scene);   // sky, ambient, fog and the look volume, as in every scene

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MenuBackdropBuilder] menu backdrop built");
        }

        // ------------------------------------------------------------------ prefab

        static void BuildPrefab(GameTuning tuning, RuntimeAnimatorController controller)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            var root = new GameObject(RootName);
            try
            {
                var t = root.transform;
                IndustrialMaterialBuilder.Ensure(false);
                using (UseLookSet(LookSet.Industrial))
                using (UseTheme(HallTheme))
                {
                    BuildHall(Group(t, "Hall"));
                    BuildHallDressing(Group(t, "Dressing"));
                    BuildHallLights(Group(t, "Lights"));
                }
                foreach (var c in t.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);   // nothing walks here
                BuildDust(t);

                var show = Group(t, "Show");
                var players = BuildPlayers(show, tuning, controller);
                var (potato, sparks, trail, puffs) = BuildPotato(show, tuning);
                var boom = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPrefab)?.GetComponent<ExplosionFx>();
                var potatoShow = show.gameObject.AddComponent<MenuHotPotato>();
                potatoShow.Configure(tuning, players, potato, sparks, trail, puffs, boom, PlayerCharacterBuilder.MeasureThrow().releaseSeconds);

                BuildStations(Group(t, "Stations"), tuning, potatoShow, EnsureWorldPanel());

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

        // ------------------------------------------------------------------ the hall (industrial look, ARCHITECTURE §25.2)

        /// <summary>The hall's floor, walls and ceiling materials: the menu matches the industrial plant and the UI theme.</summary>
        static readonly SurfaceTheme HallTheme = new SurfaceTheme(IndustrialDir + "Industrial_Floor_Concrete", IndustrialDir + "Industrial_Wall_Brick",
                                                                   IndustrialDir + "Industrial_Ceiling_Concrete");
        const float HallX = 24f, HallBack = 22f, HallFront = -24f, HallHeight = 16f;

        /// <summary>
        /// A closed factory hall around every station and camera: concrete floor, brick walls, a concrete roof on steel trusses
        /// and columns along the walls. The show plays on a marked bay in the middle; the logo hangs on a steel gantry at the
        /// back. Nothing stands between a camera spot and its board.
        /// </summary>
        static void BuildHall(Transform p)
        {
            float width = 2 * HallX, depth = HallBack - HallFront, midZ = (HallBack + HallFront) / 2;
            Block(p, "Floor", new Vector3(0, -0.5f, midZ), new Vector3(width, 1, depth), KitRole.Floor);
            Block(p, "Ceiling", new Vector3(0, HallHeight + 0.5f, midZ), new Vector3(width, 1, depth), KitRole.Ceiling);
            foreach (float x in new[] { -HallX - 0.5f, HallX + 0.5f })
                Block(p, "Wall", new Vector3(x, HallHeight / 2, midZ), new Vector3(1, HallHeight + 2, depth + 2), KitRole.Wall);
            foreach (float z in new[] { HallFront - 0.5f, HallBack + 0.5f })
                Block(p, "End wall", new Vector3(0, HallHeight / 2, z), new Vector3(width + 2, HallHeight + 2, 1), KitRole.Wall);
            for (float z = HallFront + 4; z < HallBack; z += 6)
            {
                IndustrialKit.Detail(p, "Roof truss", new Vector3(0, HallHeight - 0.4f, z), new Vector3(width - 0.4f, 0.6f, 0.35f), KitRole.Truss);
                IndustrialKit.Detail(p, "Roof truss flange", new Vector3(0, HallHeight - 0.05f, z), new Vector3(width - 0.4f, 0.08f, 0.8f), KitRole.Truss);
            }
            foreach (float x in new[] { -HallX + 1.2f, HallX - 1.2f })
                for (float z = HallFront + 6; z < HallBack; z += 12)
                {
                    Block(p, "Column", new Vector3(x, HallHeight / 2, z), new Vector3(0.9f, HallHeight, 0.9f), KitRole.Pillar);
                    IndustrialKit.ColumnTrim(p, "Column trim", new Vector3(x, 0, z), HallHeight, 0.9f);
                }
            // the show's bay, marked on the floor, and the joints of the slab
            IndustrialKit.EdgeStrip(p, "Bay line", 0, 0, -4.2f, 0.25f, 14f);
            IndustrialKit.EdgeStrip(p, "Bay line", 0, 0, 6.2f, 0.25f, 14f);
            IndustrialKit.EdgeStrip(p, "Bay line", -7f, 0, 1f, 10.6f, 0.25f);
            IndustrialKit.EdgeStrip(p, "Bay line", 7f, 0, 1f, 10.6f, 0.25f);
            for (float z = HallFront + 6; z < HallBack; z += 6) IndustrialKit.Seam(p, "Floor joint", 0, 0, z, width - 2);
        }

        /// <summary>Steel and machinery round the walls, the logo gantry, a belt with crates at the back, ducts and pipes.</summary>
        static void BuildHallDressing(Transform p)
        {
            // the logo gantry: two painted columns, a beam, and a dark steel plate behind the Title board
            foreach (float x in new[] { -6.2f, 6.2f })
            {
                Block(p, "Gantry column", new Vector3(x, 6.5f, 8.8f), new Vector3(0.7f, 13f, 0.7f), KitRole.Frame);
                IndustrialKit.ColumnTrim(p, "Gantry trim", new Vector3(x, 0, 8.8f), 13f, 0.7f);
            }
            Block(p, "Gantry beam", new Vector3(0, 12.6f, 8.8f), new Vector3(13.4f, 0.8f, 0.8f), KitRole.Frame);
            Block(p, "Logo plate", new Vector3(0, 7.6f, 8.5f), new Vector3(11f, 7f, 0.3f), KitRole.Rubber);   // dark, so the logo stands out; behind the Title board even at its turned edge
            foreach (float x in new[] { -3.5f, 3.5f })
                IndustrialKit.Detail(p, "Plate chain", new Vector3(x, 11.65f, 8.5f), new Vector3(0.06f, 1.1f, 0.06f), KitRole.Truss);

            // machines along both walls (out of every camera's line to its board)
            foreach (float x in new[] { -20.5f, 20.5f })
                foreach (float z in new[] { -10f, 2f, 14f })
                    Machine(p, "Machine", new Vector3(x, 3f, z), new Vector3(5f, 6f, 7f));

            // a belt along the back wall carrying crates, on painted legs
            Block(p, "Belt", new Vector3(0, 1.1f, 16.5f), new Vector3(30f, 0.4f, 2.4f), KitRole.Rubber);
            for (float x = -14f; x <= 14f; x += 4f)
                foreach (float z in new[] { 15.5f, 17.5f })
                    Block(p, "Belt leg", new Vector3(x, 0.45f, z), new Vector3(0.25f, 0.9f, 0.25f), KitRole.Truss);
            foreach (var (x, size) in new[] { (-10f, 1.2f), (-3.5f, 1f), (2.5f, 1.4f), (9f, 1f) })
                Crate(p, new Vector3(x, 1.3f + size / 2, 16.5f), size);

            // crate stacks in the back corners
            foreach (var (x, z) in new[] { (-15.5f, 11f), (-13f, 12.5f), (14.5f, 10.5f), (16.5f, 12f) })
            {
                Crate(p, new Vector3(x, 0.8f, z), 1.6f);
                if ((x < 0) == (z > 12)) Crate(p, new Vector3(x + 0.1f, 2.4f, z - 0.1f), 1.4f);
            }

            // ducts and pipes along the walls
            foreach (float x in new[] { -HallX + 0.5f, HallX - 0.5f })
            {
                IndustrialKit.Detail(p, "Duct", new Vector3(x, 11f, 0), new Vector3(0.7f, 0.7f, HallBack - HallFront - 4), KitRole.Rust);
                IndustrialKit.Detail(p, "Pipe", new Vector3(x, 8.6f, 0), new Vector3(0.35f, 0.35f, HallBack - HallFront - 4), KitRole.Grating);
            }
            IndustrialKit.Detail(p, "Back duct", new Vector3(0, 12.5f, HallBack - 0.6f), new Vector3(2 * HallX - 4, 0.8f, 0.8f), KitRole.Rust);

            // mouldings, frames and props along the side walls and the back wall (IndustrialDetails), between the machines and the
            // columns; the hall's columns stand in for pilasters
            var options = new IndustrialDetails.Options { pilasters = false, windows = true, density = 0.85f };
            var sideRuns = new List<IndustrialDetails.Run>();
            foreach (int side in new[] { -1, 1 })
                foreach (var (a, b) in new[] { (HallFront + 0.5f, -19f), (-17f, -13.6f), (-5f, -1.6f), (7f, 10.4f), (19f, HallBack - 0.5f) })
                    sideRuns.Add(new IndustrialDetails.Run { side = side, half = HallX, z0 = a, z1 = b, floor = 0, top = HallHeight, upper = true });
            IndustrialDetails.DressFrame(p, sideRuns, new[] { (-1, HallX, HallFront, HallBack), (1, HallX, HallFront, HallBack) }, HallHeight, options, 4242);
            // the back wall, in a frame turned so that its face is the plane x = HallBack
            var back = new GameObject("Back wall").transform;
            back.SetParent(p, false);
            back.localRotation = Quaternion.Euler(0, -90, 0);
            IndustrialDetails.DressFrame(back, new[] { new IndustrialDetails.Run { side = 1, half = HallBack, z0 = -HallX + 2.5f, z1 = HallX - 2.5f, floor = 0, top = HallHeight, upper = true } },
                                         new[] { (1, HallBack, -HallX, HallX) }, HallHeight, options, 4243);
        }

        static void Machine(Transform p, string name, Vector3 center, Vector3 size)
        {
            Block(p, name, center, size, KitRole.Pillar);
            IndustrialKit.Detail(p, name + " band", center + new Vector3(0, size.y * 0.15f, 0), new Vector3(size.x + 0.04f, size.y * 0.3f, size.z - 1f), KitRole.Rubber);
            IndustrialKit.Detail(p, name + " lamp", center + new Vector3(-Mathf.Sign(center.x) * (size.x / 2 + 0.05f), size.y * 0.35f, 0), new Vector3(0.12f, 0.25f, 0.25f), KitRole.Lamp);
        }

        static void Crate(Transform p, Vector3 center, float size)
        {
            Block(p, "Crate", center, Vector3.one * size, KitRole.Frame);
            IndustrialKit.Detail(p, "Crate band", center, new Vector3(size + 0.04f, size * 0.12f, size + 0.04f), KitRole.Truss);
        }

        /// <summary>
        /// The hall's light: warm hanging lamps on a grid under the roof, a warm wash on the show, the logo and the boards, cold
        /// wall lamps. No sun: the hall is closed, and LookBuilder gives Bootstrap the industrial interior fill and exposure.
        /// </summary>
        static void BuildHallLights(Transform p)
        {
            var warm = new Color(1f, 0.72f, 0.45f);
            foreach (float z in new[] { -16f, -6f, 4f, 14f })
                foreach (float x in new[] { -13f, 0f, 13f })
                {
                    if (z == 4f && x == 0f) continue;   // the logo hangs there
                    var at = new Vector3(x, 12f, z);
                    IndustrialKit.Detail(p, "Lamp chain", new Vector3(x, 14f, z), new Vector3(0.06f, 4f, 0.06f), KitRole.Truss);
                    IndustrialKit.Detail(p, "Lamp shade", at + Vector3.up * 0.18f, new Vector3(1.2f, 0.22f, 1.2f), KitRole.Truss);
                    IndustrialKit.Detail(p, "Lamp bulb", at, new Vector3(0.8f, 0.14f, 0.8f), KitRole.Lamp);
                    LookBuilder.PracticalLamp(p, at - Vector3.up * 0.6f, warm, 70f, 26f, "Lamp light");
                }
            LookBuilder.PracticalLamp(p, new Vector3(0f, 6f, -2f), warm, 45f, 16f, "Show light");
            LookBuilder.PracticalLamp(p, new Vector3(0f, 9f, 2f), warm, 40f, 14f, "Logo light");
            LookBuilder.PracticalLamp(p, new Vector3(-9f, 6f, -8f), warm, 30f, 14f, "Play light");
            LookBuilder.PracticalLamp(p, new Vector3(10f, 6f, -8f), warm, 30f, 14f, "Level light");
            foreach (float x in new[] { -HallX + 1.6f, HallX - 1.6f })
                foreach (float z in new[] { -12f, 0f, 12f })
                    LookBuilder.PracticalLamp(p, new Vector3(x, 6f, z), new Color(0.6f, 0.78f, 1f), 18f, 14f, "Wall lamp light");
        }

        /// <summary>Dust motes drifting in the lamp light (the island's sparkles, now dim and warm).</summary>
        static void BuildDust(Transform parent)
        {
            var go = new GameObject("Dust");
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
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.6f, 0.35f), new Color(0.9f, 0.85f, 0.8f, 0.2f));
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 18f;
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

        /// <summary>
        /// The four players: copies of each slot's character (PlayerCharacter: model, materials, hand socket; the
        /// menu's controller) on yaw pivots.
        /// </summary>
        static Transform[] BuildPlayers(Transform parent, GameTuning tuning, RuntimeAnimatorController controller)
        {
            var pivots = new Transform[PlayerSpots.Length];
            for (int i = 0; i < PlayerSpots.Length; i++)
            {
                var pivot = Group(parent, "Player_" + (i + 1));
                pivot.localPosition = PlayerSpots[i];
                pivot.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, 1.4f) - PlayerSpots[i]);
                var source = PlayerIdentity.CharacterFor(tuning, i) ?? throw new InvalidOperationException("no character for slot " + (i + 1));
                var character = Object.Instantiate(source, pivot, false);
                character.name = source.name;
                var animator = character.Animator;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                pivots[i] = pivot;
            }
            return pivots;
        }

        /// <summary>The potato: a copy of the bomb's visual (model, wick, sparks) and its flight trail, a bit bigger to read.</summary>
        static (Transform, ParticleSystem, TrailRenderer, ParticleSystem) BuildPotato(Transform parent, GameTuning tuning)
        {
            var bomb = AssetDatabase.LoadAssetAtPath<GameObject>(BombPrefab)?.transform ?? throw new InvalidOperationException("missing " + BombPrefab);
            var potato = Object.Instantiate(bomb.Find("Visual").gameObject, parent, false).transform;
            potato.name = "Potato";
            potato.localScale = Vector3.one * (tuning.potatoVisualScale * MenuPotatoScale);   // reads from the menu camera, still fits a hand
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

        // ------------------------------------------------------------------ menu stations (#79)

        /// <summary>The boards' panel: world space, the shared theme, <see cref="BoardPixelsPerMetre"/>.</summary>
        static PanelSettings EnsureWorldPanel()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(WorldPanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, WorldPanelPath);
            }
            var screen = AssetDatabase.LoadAssetAtPath<PanelSettings>(ScreenPanelPath) ?? throw new InvalidOperationException("missing " + ScreenPanelPath);
            panel.themeStyleSheet = screen.themeStyleSheet;
            panel.renderMode = PanelRenderMode.WorldSpace;
            var so = new SerializedObject(panel);
            so.FindProperty("m_PixelsPerUnit").floatValue = BoardPixelsPerMetre;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            return panel;
        }

        static void BuildStations(Transform parent, GameTuning tuning, MenuHotPotato show, PanelSettings panel)
        {
            // World-space pointer input: the boards' generated colliders, seen through the main (menu) camera.
            var input = new GameObject("BoardInput").AddComponent<PanelInputConfiguration>();
            input.transform.SetParent(parent, false);
            input.interactionLayers = 1 << LayerMask.NameToLayer(BoardLayer);
            input.defaultEventCameraIsMainCamera = true;

            foreach (var (id, boardAt, px, cameraAt, lookAt) in Stations)
            {
                var root = Group(parent, id.ToString());
                var spot = new GameObject("Spot").AddComponent<CinemachineCamera>();
                spot.transform.SetParent(root, false);
                spot.transform.SetPositionAndRotation(cameraAt, Quaternion.LookRotation(lookAt - cameraAt, Vector3.up));
                spot.Lens.FieldOfView = CameraFov;
                spot.Lens.NearClipPlane = 0.3f;
                spot.Lens.FarClipPlane = 600f;
                spot.Priority = 0;
                if (id == MenuStationId.Title) spot.gameObject.AddComponent<MenuCameraDrift>().Configure(tuning, lookAt);

                var board = Board(root, "Board", panel, boardAt, px, cameraAt, BoardLayer);
                var focus = FocusVolume(root, id);
                root.gameObject.AddComponent<MenuStation>().Configure(id, spot, board, focus);
                if (id == MenuStationId.Lobby) BuildStage(root, tuning, show, panel, cameraAt);
            }
        }

        /// <summary>
        /// The station's cinematic framing (ARCHITECTURE §25): a light vignette, its own profile (generated next to the
        /// look), weight 0 until the camera is at this station; MenuCameraRig sets it to the camera-effects strength. No
        /// depth of field: the world-space boards write no depth, so it would blur them as if they were sky.
        /// </summary>
        static UnityEngine.Rendering.Volume FocusVolume(Transform parent, MenuStationId id)
        {
            string path = "Assets/Settings/Look/MenuFocus_" + id + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            if (profile.TryGet(out UnityEngine.Rendering.Universal.DepthOfField dof))
            {
                profile.Remove<UnityEngine.Rendering.Universal.DepthOfField>();
                AssetDatabase.RemoveObjectFromAsset(dof);
            }
            if (!profile.TryGet(out UnityEngine.Rendering.Universal.Vignette vignette))
            {
                vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
                vignette.name = "Vignette";
                AssetDatabase.AddObjectToAsset(vignette, profile);
            }
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.12f, 0.06f, 0.08f));
            EditorUtility.SetDirty(vignette);
            EditorUtility.SetDirty(profile);

            var go = new GameObject("Focus");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.priority = 5f;   // over the look volume, under the speed effects
            volume.weight = 0f;
            volume.sharedProfile = profile;
            return volume;
        }

        /// <summary>A world-space UI document of <paramref name="px"/> pixels, upright, facing <paramref name="viewer"/>.</summary>
        static UIDocument Board(Transform parent, string name, PanelSettings panel, Vector3 at, Vector2 px, Vector3 viewer, string layer)
        {
            var go = new GameObject(name);
            go.layer = LayerMask.NameToLayer(layer);
            go.transform.SetParent(parent, false);
            var away = at - viewer;
            away.y = 0f;
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(away, Vector3.up));   // the panel's front faces -forward
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            doc.worldSpaceSize = px;
            doc.pivot = Pivot.Center;
            return doc;
        }

        /// <summary>The lobby stage: a spot per slot and a nameplate (slot chip + name) above each (MenuLobbyStage).</summary>
        static void BuildStage(Transform parent, GameTuning tuning, MenuHotPotato show, PanelSettings panel, Vector3 viewer)
        {
            var stage = Group(parent, "Stage");
            var spots = new Transform[StageSpots.Length];
            var plates = new UIDocument[StageSpots.Length];
            for (int i = 0; i < StageSpots.Length; i++)
            {
                var spot = Group(stage, "Spot_" + (i + 1));
                var toViewer = viewer - StageSpots[i];
                toViewer.y = 0f;
                spot.SetPositionAndRotation(StageSpots[i], Quaternion.LookRotation(toViewer, Vector3.up));
                plates[i] = Board(spot, "Nameplate", panel, StageSpots[i] + Vector3.up * PlateHeight, PlatePx, viewer, PlateLayer);
                spots[i] = spot;
            }
            stage.gameObject.AddComponent<MenuLobbyStage>().Configure(tuning, show, spots, plates);
        }

        // ------------------------------------------------------------------ scene: camera and sky

        /// <summary>
        /// The menu camera: framed on the Title station, flown between stations by its Cinemachine brain and
        /// <see cref="MenuCameraRig"/> (the drift now lives on the Title spot).
        /// </summary>
        static void SetUpCamera(UnityEngine.SceneManagement.Scene scene, GameTuning tuning)
        {
            var camera = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<Camera>()).FirstOrDefault(c => c != null)
                         ?? throw new InvalidOperationException("no camera in " + ScenePath);
            var (_, _, _, at, lookAt) = Stations.First(s => s.id == MenuStationId.Title);
            camera.transform.SetPositionAndRotation(at, Quaternion.LookRotation(lookAt - at, Vector3.up));
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = CameraFov;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;
            var drift = camera.GetComponent<MenuCameraDrift>();
            if (drift != null) Object.DestroyImmediate(drift);
            if (camera.GetComponent<CinemachineBrain>() == null) camera.gameObject.AddComponent<CinemachineBrain>();
            var rig = camera.GetComponent<MenuCameraRig>();
            if (rig == null) rig = camera.gameObject.AddComponent<MenuCameraRig>();
            rig.Configure(tuning);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(rig);
        }

    }
}
