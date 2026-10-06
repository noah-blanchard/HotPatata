using System.Linq;
using HotPatata.Editor;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>
    /// The obstacle prefab contract (docs/OBSTACLES.md §4): every obstacle prefab of the project, the kit and anyone's variants,
    /// keeps it; the validator catches each way of breaking it; the example variants are real Prefab Variants of the kit; and the
    /// builders' look passes never touch a hand-made visual.
    /// </summary>
    public class ObstaclePrefabTests
    {
        GameObject made;

        [TearDown]
        public void Clean()
        {
            if (made != null) Object.DestroyImmediate(made);
        }

        [Test]
        public void EveryObstaclePrefab_KeepsTheContract()
        {
            var paths = ObstaclePrefabValidator.ObstaclePrefabPaths().ToList();
            Assert.Greater(paths.Count, 20, "the kit is found");
            var broken = paths.Select(p => (p, problems: ObstaclePrefabValidator.Validate(AssetDatabase.LoadAssetAtPath<GameObject>(p))))
                              .Where(x => x.problems.Count > 0)
                              .Select(x => x.p + ":\n  " + string.Join("\n  ", x.problems)).ToList();
            Assert.IsEmpty(broken, string.Join("\n", broken));
        }

        [Test]
        public void TheExampleVariants_AreVariantsOfTheKit()
        {
            var boulder = AssetDatabase.LoadAssetAtPath<GameObject>(KitVariantBuilder.BoulderPath);
            var bridge = AssetDatabase.LoadAssetAtPath<GameObject>(KitVariantBuilder.DrawbridgePath);
            Assert.IsTrue(PrefabUtility.IsPartOfVariantPrefab(boulder) && PrefabUtility.IsPartOfVariantPrefab(bridge));
            Assert.AreEqual("Platform_Moving", PrefabUtility.GetCorrespondingObjectFromSource(boulder).name);
            Assert.AreEqual("Actuator_Bridge", PrefabUtility.GetCorrespondingObjectFromSource(bridge).name);

            var visual = boulder.GetComponentInChildren<CustomVisual>(true);
            Assert.IsNotNull(visual, "the boulder draws its own model");
            Assert.IsNotNull(visual.GetComponentInChildren<MeshRenderer>(true));
            var box = boulder.GetComponent<MovingPlatform>().Platform.GetComponentsInChildren<BoxCollider>(true).Single(c => !c.isTrigger);
            Assert.AreEqual(new Vector3(3f, 0.5f, 3f), box.size, "the collider is the kit's");
            Assert.IsTrue(boulder.GetComponent<MovingPlatform>().Platform.GetComponentsInChildren<KitSkin>(true).All(s => !s.gameObject.activeSelf),
                          "the kit box is switched off");

            var actuator = bridge.GetComponent<SignalActuator>();
            Assert.IsTrue(new SerializedObject(actuator).FindProperty("rotateWithWaypoints").boolValue, "the drawbridge turns");
            Assert.AreEqual(actuator.WaypointClosed.position, actuator.WaypointOpen.position, "around one hinge");
        }

        [Test]
        public void TheLookPasses_LeaveHandMadeVisualsAlone()
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var boulder = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KitVariantBuilder.BoulderPath), preview);
                var bridge = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KitVariantBuilder.DrawbridgePath), preview);
                var rock = boulder.GetComponentInChildren<CustomVisual>(true).GetComponentInChildren<Renderer>(true);
                var before = rock.sharedMaterial;
                var kit = bridge.GetComponentInChildren<KitSkin>(true).GetComponent<Renderer>();
                var kitBefore = kit.sharedMaterial;

                var nature = new CourseKit.SurfaceTheme("Nature/Nature_GrassPath", "Nature/Nature_RockFace", "Nature/Nature_RockFace");
                NatureRestyle.Apply(boulder.transform, nature);
                NatureRestyle.Apply(bridge.transform, nature);
                Assert.AreSame(before, rock.sharedMaterial, "the nature pass kept the boulder's own material");
                Assert.AreNotSame(kitBefore, kit.sharedMaterial, "while the kit box beside it was redrawn");
                IndustrialRestyle.Apply(boulder.transform, CourseKit.CurrentTheme);
                Assert.AreSame(before, rock.sharedMaterial, "the industrial pass kept it too");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        // ------------------------------------------------------------------ the validator catches each break

        static GameObject Child(GameObject parent, string name, string layer = "Default")
        {
            var go = new GameObject(name) { layer = LayerMask.NameToLayer(layer) };
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A valid hand-built moving platform: root / Body (kinematic, box) + A + B.</summary>
        GameObject GoodMover()
        {
            made = new GameObject("Mover");
            var body = Child(made, "Body", "Environment");
            body.AddComponent<Rigidbody>().isKinematic = true;
            body.AddComponent<BoxCollider>();
            var mp = made.AddComponent<MovingPlatform>();
            Set(mp, "platform", body.transform);
            Set(mp, "waypointA", Child(made, "A").transform);
            Set(mp, "waypointB", Child(made, "B").transform);
            return made;
        }

        static string Problems(GameObject go) => string.Join(" | ", ObstaclePrefabValidator.Validate(go));

        [Test]
        public void Validator_AcceptsAHandBuiltPlatform_WithAnyNames()
        {
            Assert.IsEmpty(ObstaclePrefabValidator.Validate(GoodMover()));
        }

        [Test]
        public void Validator_RejectsAMovingPartThatIsTheRoot()
        {
            var go = GoodMover();
            Set(go.GetComponent<MovingPlatform>(), "platform", go.transform);
            StringAssert.Contains("never the object carrying the system", Problems(go));
        }

        [Test]
        public void Validator_RejectsAMissingWaypoint()
        {
            var go = GoodMover();
            Set(go.GetComponent<MovingPlatform>(), "waypointB", null);
            StringAssert.Contains("waypointB is not set", Problems(go));
        }

        [Test]
        public void Validator_RejectsAColliderInAHandMadeVisual()
        {
            var go = GoodMover();
            var visual = Child(go.GetComponent<MovingPlatform>().Platform.gameObject, "Art");
            visual.AddComponent<CustomVisual>();
            Child(visual, "Rock", "Environment").AddComponent<SphereCollider>();
            StringAssert.Contains("under a CustomVisual", Problems(go));
        }

        [Test]
        public void Validator_RejectsAMeshCollider_AndANonGameplayLayer()
        {
            var go = GoodMover();
            Child(go.GetComponent<MovingPlatform>().Platform.gameObject, "Scan", "Default").AddComponent<MeshCollider>();
            string problems = Problems(go);
            StringAssert.Contains("no MeshCollider", problems);
            StringAssert.Contains("layer 'Default'", problems);
        }

        [Test]
        public void Validator_RejectsAMovingPartWithoutAKinematicBody()
        {
            var go = GoodMover();
            Object.DestroyImmediate(go.GetComponent<MovingPlatform>().Platform.GetComponent<Rigidbody>());
            StringAssert.Contains("kinematic Rigidbody", Problems(go));
        }

        [Test]
        public void Validator_RejectsAHazardWithoutAKillTrigger()
        {
            made = new GameObject("Blade");
            made.AddComponent<Rigidbody>().isKinematic = true;
            Child(made, "Edge", "Hazard").AddComponent<BoxCollider>();
            made.AddComponent<RotatingObstacle>();
            StringAssert.Contains("needs a KillZone", Problems(made));
        }

        [Test]
        public void Validator_RejectsAReplicatedSystemWithoutItsNetworkCompanion()
        {
            made = new GameObject("Door");
            var part = Child(made, "Leaf", "Environment");
            part.AddComponent<Rigidbody>().isKinematic = true;
            part.AddComponent<BoxCollider>();
            var door = made.AddComponent<SignalActuator>();
            Set(door, "platform", part.transform);
            Set(door, "waypointClosed", Child(made, "Shut").transform);
            Set(door, "waypointOpen", Child(made, "Up").transform);
            StringAssert.Contains("NetworkSignalActuator is missing", Problems(made));
            made.AddComponent<NetworkObject>();
            made.AddComponent<NetworkSignalActuator>();
            Assert.IsEmpty(ObstaclePrefabValidator.Validate(made));
        }
    }
}
