using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Example variants of kit prefabs (docs/OBSTACLES.md §4): the same logic under another visual or another motion, made the way
    /// anyone makes one by hand (Prefab Variant, base visual switched off, a <see cref="CustomVisual"/> beside the colliders).
    /// <list type="bullet">
    /// <item><c>Platform_Moving_Boulder</c>: a moving platform drawn as a scanned boulder (the collider is unchanged).</item>
    /// <item><c>Actuator_Bridge_Drawbridge</c>: the signal bridge hinged at its near end, standing up when closed and lowered when
    /// open (<c>rotateWithWaypoints</c>), instead of sliding.</item>
    /// </list>
    /// Menu <b>HotPatata/Kit/Build Example Variants</b>; idempotent. <c>ObstaclePrefabTests</c> validates them.
    /// </summary>
    public static class KitVariantBuilder
    {
        public const string VariantsDir = "Assets/Prefabs/Variants/";
        public const string BoulderPath = VariantsDir + "Platform_Moving_Boulder.prefab";
        public const string DrawbridgePath = VariantsDir + "Actuator_Bridge_Drawbridge.prefab";

        [MenuItem("HotPatata/Kit/Build Example Variants")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(VariantsDir);
            MakeVariant(CourseKit.PlatformsDir + "Platform_Moving.prefab", BoulderPath, DressBoulder);
            MakeVariant(CourseKit.PlatformsDir + "Actuator_Bridge.prefab", DrawbridgePath, MakeDrawbridge);
            AssetDatabase.SaveAssets();
            Debug.Log("[KitVariantBuilder] example variants built");
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

        /// <summary>Switches the kit box off and puts a boulder under the moving part, its top flush with the collider's top.</summary>
        static void DressBoulder(GameObject root)
        {
            var mover = root.GetComponent<MovingPlatform>();
            var box = mover.Platform.GetComponentsInChildren<BoxCollider>(true).First(c => !c.isTrigger);
            foreach (var r in mover.Platform.GetComponentsInChildren<Renderer>(true)) r.gameObject.SetActive(false);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Nature/namaqualand_boulder_04/namaqualand_boulder_04_1k.fbx");
            var mesh = model.GetComponentInChildren<MeshFilter>().sharedMesh;
            var visual = new GameObject("BoulderVisual", typeof(CustomVisual));
            visual.transform.SetParent(mover.Platform, false);
            var art = new GameObject("Boulder");
            art.transform.SetParent(visual.transform, false);
            art.AddComponent<MeshFilter>().sharedMesh = mesh;
            art.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Nature/Nature_Prop_Boulder04.mat");

            // Fit the boulder's footprint to the collider's and keep its top 2 cm under the collider's top (never above what players walk on).
            Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale), center = box.transform.TransformPoint(box.center);
            var b = mesh.bounds;
            float s = Mathf.Min(size.x / b.size.x, size.z / b.size.z);
            art.transform.localScale = Vector3.one * s;
            Vector3 top = mover.Platform.InverseTransformPoint(center + Vector3.up * (size.y / 2f - 0.02f));
            art.transform.localPosition = top - new Vector3(b.center.x, b.max.y, b.center.z) * s;
        }

        /// <summary>Hinges the bridge at its near end: closed it stands up, open it lies across the gap; the slide is gone.</summary>
        static void MakeDrawbridge(GameObject root)
        {
            var bridge = root.GetComponent<SignalActuator>();
            var platform = bridge.Platform;
            var box = platform.GetComponentsInChildren<BoxCollider>(true).First(c => !c.isTrigger);
            float half = box.size.z * box.transform.localScale.z / 2f;
            foreach (Transform part in platform) part.localPosition += Vector3.forward * half;   // the moving part's pivot becomes the hinge
            var hinge = bridge.WaypointClosed.localPosition - Vector3.forward * half;
            bridge.WaypointClosed.localPosition = hinge;
            bridge.WaypointClosed.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // standing up, its far end in the air
            bridge.WaypointOpen.localPosition = hinge;
            bridge.WaypointOpen.localRotation = Quaternion.identity;
            platform.localPosition = hinge;
            platform.localRotation = bridge.WaypointClosed.localRotation;
            CourseKit.SetField(bridge, "rotateWithWaypoints", p => p.boolValue = true);
            CourseKit.SetField(bridge, "travelSeconds", p => p.floatValue = 1.6f);
        }
    }
}
