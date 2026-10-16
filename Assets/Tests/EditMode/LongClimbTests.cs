using System.Linq;
using HotPatata.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Tests
{
    /// <summary>
    /// The Long Climb (PROJECT_SPEC §15g), the authored level for two: Verify Level reports no error and every section's locks hold, its
    /// six checkpoints come in order with the fuse curve, it is for exactly two and first in the menu, every door, bridge and cage is
    /// worked by a source of its own, each split is marked for both lanes, and nothing it draws lost its mesh. The route runner walks it in
    /// <c>AuthoredLevelTests</c>. The scene is opened beside the test's own and closed after, untouched.
    /// </summary>
    public class LongClimbTests
    {
        const string ScenePath = "Assets/Scenes/Levels/LongClimb.unity";
        Scene scene;

        [OneTimeSetUp]
        public void Open()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "The Long Climb is not in the project");
            LevelPieceWatcher.Enabled = false;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Physics.SyncTransforms();
        }

        [OneTimeTearDown]
        public void Close()
        {
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            LevelPieceWatcher.Enabled = true;
        }

        T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

        [Test]
        public void VerifyLevel_ReportsNoError()
        {
            var errors = LevelVerifier.Run(scene).Where(f => f.severity == LevelFinding.Severity.Error).Select(f => f.message).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void EverySection_HoldsItsLocks()
        {
            var problems = CourseContractCheck.Problems(scene, CourseContractCheck.Tuning);
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Assert.AreEqual(7, All<SectionContract>().Length, "a contract for the start and for each of the six checkpoints");
        }

        [Test]
        public void SixCheckpoints_InOrder_WithTheFuseCurve()
        {
            var cps = All<Checkpoint>().OrderBy(c => c.Id).ToArray();
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, cps.Select(c => c.Id).ToArray());
            float[] fuses = { 0f, 0f, 5f, 5f, 4.5f, 4.5f };
            for (int i = 0; i < cps.Length; i++)
                Assert.AreEqual(fuses[i], new SerializedObject(cps[i]).FindProperty("holdFuseOverride").floatValue, 1e-3f, $"CP{i + 1}'s fuse");
            Assert.AreEqual(1, All<FinishZone>().Length, "one finish");
        }

        [Test]
        public void IsForExactlyTwo_AndFirstInTheMenu()
        {
            var settings = LevelScenes.SettingsOf(scene);
            Assert.AreEqual(2, settings.MinPlayers);
            Assert.AreEqual(2, settings.MaxPlayers);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CourseKit.NetworkPrefab);
            var menu = CourseKit.ReadMenu(new SerializedObject(prefab.GetComponent<NetworkBootstrap>()));
            Assert.AreEqual("LongClimb", menu[0].scene, "first in the level list");
            Assert.AreEqual(2, CourseKit.MaxOf(menu[0]));
        }

        [Test]
        public void EveryActuator_IsWorkedByASourceOfItsOwn()
        {
            var actuators = All<SignalActuator>();
            Assert.Greater(actuators.Length, 20);
            foreach (var a in actuators) Assert.IsNotNull(a.Source as Object, $"{a.name} has no source");
            var shared = actuators.GroupBy(a => a.Source as Object).Where(g => g.Count() > 1).Select(g => g.Key.name).ToList();
            Assert.IsEmpty(shared, "one source drives one actuator: " + string.Join(", ", shared));
        }

        [Test]
        public void EverySplit_IsMarkedForBothLanes()
        {
            var markers = All<LaneMarkerShape>();
            int sun = markers.Count(m => m.Lane == Lane.Sun), moon = markers.Count(m => m.Lane == Lane.Moon);
            Assert.GreaterOrEqual(sun, 7, "a Sun marker at each split");
            Assert.GreaterOrEqual(moon, 7, "a Moon marker at each split");
        }

        [Test]
        public void NothingItDraws_LostItsMesh()
        {
            var missing = All<LevelPiece>().Where(p => p.Output != null)
                .SelectMany(p => p.Output.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh == null && !PrefabUtility.IsPartOfPrefabInstance(f)).Select(f => p.name)
                    .Concat(p.Output.GetComponentsInChildren<MeshCollider>(true).Where(c => c.sharedMesh == null).Select(c => p.name)))
                .Distinct().ToList();
            Assert.IsEmpty(missing, string.Join(", ", missing));
        }
    }
}
