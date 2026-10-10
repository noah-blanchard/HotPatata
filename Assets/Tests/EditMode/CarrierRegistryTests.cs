using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>
    /// Carrier ids (docs/netcode-deterministic-plan.md §2.2): derived from the scene and the sibling-index path, so every
    /// machine gets the same ones; unique in every course scene, or riders on the clashing carriers could not be synced.
    /// </summary>
    public class CarrierRegistryTests
    {
        static readonly string[] Scenes =
        {
            "Assets/Scenes/PatataWilds.unity",
            "Assets/Scenes/PatataCanopy.unity",
            "Assets/Scenes/IndustrialPlant.unity",
            "Assets/Scenes/PassSandbox.unity"
        };

        [Test]
        public void Hash_IsStable_AndNeverZero()
        {
            Assert.AreEqual(CarrierRegistry.Hash("PatataWilds/3/0/5"), CarrierRegistry.Hash("PatataWilds/3/0/5"));
            Assert.AreNotEqual(CarrierRegistry.Hash("PatataWilds/3/0/5"), CarrierRegistry.Hash("PatataWilds/3/0/6"));
            Assert.AreEqual(unchecked((int)2166136261u), CarrierRegistry.Hash(""), "FNV-1a offset basis: the same on every runtime");
        }

        [Test]
        public void IdFor_FollowsTheSiblingPath_NotTheName()
        {
            var root = new GameObject("CarrierIdFixture");
            try
            {
                var a = new GameObject("Same").transform;
                var b = new GameObject("Same").transform;
                a.SetParent(root.transform);
                b.SetParent(root.transform);
                int idA = CarrierRegistry.IdFor(a);
                Assert.AreEqual(idA, CarrierRegistry.IdFor(a), "deterministic");
                Assert.AreNotEqual(idA, CarrierRegistry.IdFor(b), "same name, other sibling: another id");
                b.name = "Renamed";
                Assert.AreEqual(CarrierRegistry.IdFor(b), CarrierRegistry.IdFor(b));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCaseSource(nameof(Scenes))]
        public void EveryMovingCarrier_HasAUniqueId(string path)
        {
            var open = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool alreadyOpen = open.isLoaded;   // never close a scene the developer has open
            var scene = alreadyOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var seen = new Dictionary<int, string>();
                int count = 0;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb is not IPlatformCarrier carrier || !carrier.Moves) continue;
                    int id = CarrierRegistry.IdFor(mb);
                    Assert.AreNotEqual(0, id);
                    Assert.IsFalse(seen.TryGetValue(id, out string other), $"{mb.name} and {other} share carrier id {id} in {scene.name}");
                    seen[id] = mb.name;
                    count++;
                }
                Debug.Log($"[CarrierRegistry] {scene.name}: {count} moving carriers, ids unique");
            }
            finally
            {
                if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
