using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static HotPatata.Editor.CourseKit;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// The systems built for three players (PROJECT_SPEC §13.21-§13.24, M15): the heavy plate and the hourglass plate (Prefab
    /// Variants of the hands-free plate), the pivot (a Prefab Variant of the signal bridge that turns instead of sliding) and the
    /// sun beam. Built by <see cref="BuildAll"/>; the course helpers below keep instances configurable with plain overrides.
    /// </summary>
    public static partial class BombObstacleKitBuilder
    {
        public const string PlateHeavy = KitVariantBuilder.VariantsDir + "PressurePlate_Heavy";
        public const string PlateHourglass = KitVariantBuilder.VariantsDir + "PressurePlate_Hourglass";
        public const string Pivot = KitVariantBuilder.VariantsDir + "Actuator_Bridge_Pivot";
        public const string Beam = GameplayDir + "SunBeam";

        public const float HeavyPadSize = 3.6f;
        public const float HourglassSeconds = 5f;
        public const float PivotSeconds = 6f;
        const int SandLamps = 12;

        /// <summary>Builds only the three-player systems (icon, materials, prefabs), leaving the rest of the kit untouched.</summary>
        [MenuItem("HotPatata/Course/Build Trio Kit Prefabs")]
        public static void BuildTrioKitOnly()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            DrawIcon("TwoBodies", TwoBodies);
            AssetDatabase.Refresh();
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconDir + "TwoBodies.png");
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            BuildTrioMaterials();
            AssetDatabase.SaveAssets();
            BuildTrioKit();
            AssetDatabase.SaveAssets();
            Debug.Log("[BombObstacleKitBuilder] trio kit built");
        }

        static void BuildTrioKit()
        {
            Directory.CreateDirectory(KitVariantBuilder.VariantsDir);
            MakeVariant(PlateHandsFree + ".prefab", PlateHeavy + ".prefab", MakeHeavyPlate);
            MakeVariant(PlateHandsFree + ".prefab", PlateHourglass + ".prefab", MakeHourglassPlate);
            MakeVariant(Bridge + ".prefab", Pivot + ".prefab", MakePivot);
            MakePrefab(Beam + ".prefab", true, BuildSunBeam);
        }

        static void BuildTrioMaterials()
        {
            // Heavy plates: terracotta and sand checker, two footprints and the "two bodies" glyph: never the yellow or blue of the others.
            MakeMaterial("Pad_Heavy", "Greybox_Hazard", new Color(0.92f, 0.45f, 0.2f), new Color(1f, 0.62f, 0.38f),
                         1, new Color(0.35f, 0.15f, 0.08f), 0.6f, 0.55f);
            MakeMaterial("Pad_Footprint", "Greybox_Hazard", new Color(0.98f, 0.9f, 0.7f), Color.white, 0, Color.black, 1f, 0f);
            MakeUnlitMaterial("Icon_Heavy", new Color(1f, 0.93f, 0.85f, 1f), Icon("TwoBodies"), false);
            // Hourglass plates: the hands-free pad with a ring of amber sand lamps that empties after release.
            MakeMaterial("Pad_Sand", "Greybox_Hazard", new Color(0.95f, 0.75f, 0.35f), new Color(1f, 0.85f, 0.5f), 0, Color.black, 1f, 0f);
            // Sun beams: a bright gold shaft (additive) and a stone eye that lights while the light reaches it.
            MakeUnlitMaterial("SunBeam_Shaft", new Color(1f, 0.82f, 0.35f, 0.55f), null, true);
            MakeMaterial("SunBeam_Eye", "Greybox_Hazard", new Color(0.95f, 0.8f, 0.4f), new Color(1f, 0.9f, 0.6f), 0, Color.black, 1f, 0f);
        }

        /// <summary>Instantiates <paramref name="basePath"/> in a preview scene, lets <paramref name="edit"/> override it, saves it as a variant.</summary>
        static void MakeVariant(string basePath, string variantPath, System.Action<GameObject> edit)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(basePath), preview);
                instance.name = Path.GetFileNameWithoutExtension(variantPath);
                edit(instance);
                PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        // ------------------------------------------------------------------ heavy plate (§13.21)

        /// <summary>
        /// A wide hands-free plate that needs two empty-handed players at once (PROJECT_SPEC §13.21): terracotta, two footprints that
        /// light one per counted player, the "two bodies" glyph between them.
        /// </summary>
        static void MakeHeavyPlate(GameObject root)
        {
            var plate = root.GetComponent<PressurePlate>();
            SetField(plate, "requiredBodies", p => p.intValue = 2);
            ResizePlate(root, HeavyPadSize);
            var pad = root.transform.Find("Pad").GetComponent<Renderer>();
            pad.sharedMaterial = Mat("Pad_Heavy");
            var icon = root.transform.Find("Icon");
            icon.GetComponent<Renderer>().sharedMaterial = Mat("Icon_Heavy");
            icon.localScale = Vector3.one * 1.3f;
            var lamps = new List<Renderer>();
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * HeavyPadSize * 0.3f;
                lamps.Add(Shape($"Footprint_{i + 1}", PrimitiveType.Cylinder, root.transform, new Vector3(x, 0.1f, 0f), Quaternion.identity,
                                new Vector3(0.8f, 0.02f, 1.2f), Mat("Pad_Footprint"), "Default").GetComponent<Renderer>());
            }
            var gauge = root.AddComponent<PlateGauge>();
            SetReference(gauge, "plate", plate);
            SetField(gauge, "bodyLamps", p =>
            {
                p.arraySize = lamps.Count;
                for (int i = 0; i < lamps.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
            });
        }

        /// <summary>Sizes a plate's trigger and pad to a square of <paramref name="size"/> metres.</summary>
        static void ResizePlate(GameObject root, float size)
        {
            var box = root.GetComponent<BoxCollider>();
            box.size = new Vector3(size, box.size.y, size);
            root.transform.Find("Rim").localScale = new Vector3(size + 0.4f, 0.02f, size + 0.4f);
            root.transform.Find("Pad").localScale = new Vector3(size, 0.04f, size);
        }

        // ------------------------------------------------------------------ hourglass plate (§13.22)

        /// <summary>
        /// A hands-free plate that stays active <see cref="HourglassSeconds"/> after the last empty-handed player stepped off
        /// (PROJECT_SPEC §13.22): a ring of amber lamps around it empties as the sand runs, and its pad pulses meanwhile. Replicated.
        /// </summary>
        static void MakeHourglassPlate(GameObject root)
        {
            var plate = root.GetComponent<PressurePlate>();
            SetField(plate, "memorySeconds", p => p.floatValue = HourglassSeconds);
            AddSandRing(root, plate, 1.7f);
            EnsureHourglassNetwork(root);
        }

        static void AddSandRing(GameObject root, PressurePlate plate, float radius)
        {
            var ring = new GameObject("SandRing").transform;
            ring.SetParent(root.transform, false);
            var lamps = new List<Renderer>();
            for (int i = 0; i < SandLamps; i++)
            {
                // Clockwise from the top (as seen from above), so the sand runs out like a clock hand.
                float a = Mathf.PI / 2f - i * 2f * Mathf.PI / SandLamps;
                var pos = new Vector3(Mathf.Cos(a) * radius, 0.06f, Mathf.Sin(a) * radius);
                lamps.Add(Shape($"Sand_{i + 1}", PrimitiveType.Cube, ring, pos, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f),
                                new Vector3(0.3f, 0.08f, 0.5f), Mat("Pad_Sand"), "Default").GetComponent<Renderer>());
            }
            var gauge = root.GetComponent<PlateGauge>() ?? root.AddComponent<PlateGauge>();
            SetReference(gauge, "plate", plate);
            SetField(gauge, "sandLamps", p =>
            {
                p.arraySize = lamps.Count;
                for (int i = 0; i < lamps.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
            });
        }

        static void EnsureHourglassNetwork(GameObject root)
        {
            if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
            if (root.GetComponent<NetworkPressurePlate>() == null) root.AddComponent<NetworkPressurePlate>();
        }

        /// <summary>
        /// Course helper: gives a placed plate (a heavy one, say) an hourglass of <paramref name="seconds"/>, with its sand ring
        /// and its replication. Added components on the instance, so its prefab stays as it is.
        /// </summary>
        public static void MakeHourglass(GameObject plateInstance, float seconds)
        {
            var plate = plateInstance.GetComponent<PressurePlate>();
            SetField(plate, "memorySeconds", p => p.floatValue = seconds);
            if (plateInstance.transform.Find("SandRing") == null)
                AddSandRing(plateInstance, plate, plateInstance.GetComponent<BoxCollider>().size.x * 0.5f + 0.5f);
            EnsureHourglassNetwork(plateInstance);
        }

        // ------------------------------------------------------------------ pivot (§13.24)

        /// <summary>
        /// The signal bridge turned into a pivot (PROJECT_SPEC §13.24): it turns about its centre instead of sliding, from the
        /// closed heading to the open one (a quarter turn by default) over <see cref="PivotSeconds"/>, carrying its riders round.
        /// </summary>
        static void MakePivot(GameObject root)
        {
            var bridge = root.GetComponent<SignalActuator>();
            bridge.WaypointClosed.localPosition = Vector3.zero;
            bridge.WaypointClosed.localRotation = Quaternion.identity;
            bridge.WaypointOpen.localPosition = Vector3.zero;
            bridge.WaypointOpen.localRotation = Quaternion.Euler(0f, 90f, 0f);
            bridge.Platform.localPosition = Vector3.zero;
            bridge.Platform.localRotation = Quaternion.identity;
            SetField(bridge, "rotateWithWaypoints", p => p.boolValue = true);
            SetField(bridge, "travelSeconds", p => p.floatValue = PivotSeconds);
            // The hub: a drum under the centre, so it reads as turning, not sliding (no collider: the deck is the slab).
            Shape("Hub", PrimitiveType.Cylinder, bridge.Platform, new Vector3(0f, -0.6f, 0f), Quaternion.identity, new Vector3(1.6f, 0.4f, 1.6f),
                  Mat(Look(KitRole.Mover).material), "Default");
        }

        /// <summary>Course helper: a pivot's slab size, and how far it turns (degrees, about Y) from closed to open.</summary>
        public static void ConfigurePivot(GameObject pivot, Vector3 size, float turnDegrees, float seconds)
        {
            ResizeActuator(pivot, size, Vector3.zero);
            var a = pivot.GetComponent<SignalActuator>();
            a.WaypointOpen.localRotation = a.WaypointClosed.localRotation * Quaternion.Euler(0f, turnDegrees, 0f);
            a.Platform.localRotation = a.WaypointClosed.localRotation;
            SetField(a, "travelSeconds", p => p.floatValue = seconds);
        }

        // ------------------------------------------------------------------ sun beam (§13.23)

        const float BeamWidth = 0.7f, BeamHeight = 0.9f, BeamDefaultLength = 12f;

        /// <summary>
        /// A sun beam (PROJECT_SPEC §13.23; pivot at the slit, at the beam's height; the light runs along +Z): the stone slit, the
        /// gold shaft, the eye at the far end, and the thin trigger volume a body cuts. Nothing collides.
        /// </summary>
        static GameObject BuildSunBeam()
        {
            var root = new GameObject("SunBeam") { layer = Layer("Trigger") };
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var zone = root.AddComponent<Zone>();
            SetReference(zone, "volume", box);
            var beam = root.AddComponent<SunBeam>();
            var t = root.transform;

            Cube("Slit", t, new Vector3(0f, 0f, -0.25f), new Vector3(1.4f, 1.6f, 0.5f), KitRole.Wall, "Default");
            var shaft = new GameObject("Shaft").transform;
            shaft.SetParent(t, false);
            Shape("Light", PrimitiveType.Cylinder, shaft, new Vector3(0f, 0f, 0.5f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.45f, 0.5f, 0.45f),
                  Mat("SunBeam_Shaft"), "Default");
            var eye = new GameObject("Eye").transform;
            eye.SetParent(t, false);
            Cube("Socket", eye, new Vector3(0f, 0f, 0.25f), new Vector3(1.4f, 1.6f, 0.5f), KitRole.Wall, "Default");
            var lamp = Shape("Lamp", PrimitiveType.Cylinder, eye, new Vector3(0f, 0f, -0.02f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.8f, 0.03f, 0.8f),
                             Mat("SunBeam_Eye"), "Default");

            var visual = root.AddComponent<SunBeamVisual>();
            SetReference(visual, "beam", beam);
            SetReference(visual, "shaft", shaft);
            SetField(visual, "eyeLamps", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = lamp.GetComponent<Renderer>();
            });
            ResizeSunBeam(root, BeamDefaultLength);
            return root;
        }

        /// <summary>Sets a sun beam's length (slit to eye): its volume, its shaft and where its eye stands.</summary>
        public static void ResizeSunBeam(GameObject beam, float length)
        {
            var box = beam.GetComponent<BoxCollider>();
            box.size = new Vector3(BeamWidth, BeamHeight, length);
            box.center = new Vector3(0f, 0f, length / 2f);
            beam.transform.Find("Eye").localPosition = new Vector3(0f, 0f, length);
            beam.transform.Find("Shaft").localScale = new Vector3(1f, 1f, length);
            SetField(beam.GetComponent<SunBeamVisual>(), "length", p => p.floatValue = length);
        }

        /// <summary>Course helper: active while the light reaches the eye instead of while it is cut.</summary>
        public static void BeamActiveWhileWhole(GameObject beam, bool whole) =>
            SetField(beam.GetComponent<SunBeam>(), "activeWhileWhole", p => p.boolValue = whole);

        // ------------------------------------------------------------------ PassSandbox demo

        /// <summary>
        /// Rebuilds <c>SectionRoot/KitDemo/TrioObstacles</c> in PassSandbox, in the free east strip (clear of the pass range and the
        /// rotating bar): a heavy plate raising a lift, a sun beam turning a pivot while a body cuts it, an hourglass plate
        /// raising a lift for five seconds after release.
        /// </summary>
        [MenuItem("HotPatata/Course/Build Sandbox Trio Obstacles")]
        public static void BuildSandboxTrioDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.OpenScene(SandboxScene);
            var kitDemo = GameObject.Find("SectionRoot/KitDemo")?.transform ?? throw new System.InvalidOperationException("PassSandbox has no SectionRoot/KitDemo");
            RebuildGroup(kitDemo, "TrioObstacles", demo =>
            {
                var heavy = Place(demo, PlateHeavy, "Demo_HeavyPlate", new Vector3(16.8f, 0f, -17.5f), Quaternion.identity).GetComponent<PressurePlate>();
                var heavyLift = Place(demo, Lift, "Demo_HeavyLift", new Vector3(16.8f, 0.3f, -13.5f), Quaternion.identity);
                ResizeActuator(heavyLift, new Vector3(2.5f, 0.5f, 2.5f), new Vector3(0f, 2.5f, 0f));
                Wire(heavyLift, heavy);

                var beam = Place(demo, Beam, "Demo_SunBeam", new Vector3(19.4f, 1.1f, -9f), Quaternion.Euler(0f, -90f, 0f));
                ResizeSunBeam(beam, 5f);
                var pivot = Place(demo, Pivot, "Demo_Pivot", new Vector3(16.8f, 0.25f, -3f), Quaternion.identity);
                ConfigurePivot(pivot, new Vector3(1.6f, 0.5f, 4.4f), 90f, PivotSeconds);
                Wire(pivot, beam.GetComponent<SunBeam>());

                var hourglass = Place(demo, PlateHourglass, "Demo_HourglassPlate", new Vector3(16.8f, 0f, 2f), Quaternion.identity).GetComponent<PressurePlate>();
                var hourglassLift = Place(demo, Lift, "Demo_HourglassLift", new Vector3(12f, 0.3f, 2.5f), Quaternion.identity);
                ResizeActuator(hourglassLift, new Vector3(2.5f, 0.5f, 2.5f), new Vector3(0f, 2.5f, 0f));
                Wire(hourglassLift, hourglass);
            });
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BombObstacleKitBuilder] PassSandbox trio obstacles built");
        }

        // ------------------------------------------------------------------ icon

        /// <summary>Two heads and shoulders side by side: "two of you here" (heavy plates).</summary>
        static float TwoBodies(Vector2 p)
        {
            float d = float.MaxValue;
            foreach (float x in new[] { -0.4f, 0.4f })
            {
                d = Mathf.Min(d, Circle(p, new Vector2(x, 0.3f), 0.22f));
                d = Mathf.Min(d, Mathf.Max(Ellipse(p, new Vector2(x, -0.42f), new Vector2(0.34f, 0.42f)), -(p.y + 0.42f)));
            }
            return d;
        }
    }
}
