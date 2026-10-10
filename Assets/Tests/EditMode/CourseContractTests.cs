using HotPatata.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>
    /// Section contracts (PROJECT_SPEC §13.20): the reach envelopes they are measured with, then every course scene checked
    /// against its contracts and the screen and curtain scans (<see cref="CourseContractCheck"/>). Findings are warnings,
    /// not failures: they point at a mechanic a team could skip.
    /// </summary>
    public class CourseContractTests
    {
        static readonly string[] Courses =
        {
            "Assets/Scenes/PatataCanopy.unity",
            "Assets/Scenes/PatataTemple.unity",
            "Assets/Scenes/PatataWilds.unity",
            "Assets/Scenes/IndustrialPlant.unity"
        };

        GameTuning tuning;

        [SetUp]
        public void SetUp() => tuning = CourseContractCheck.Tuning;

        [Test]
        public void ClimbReach_IsAJumpPlusAMantle() =>
            Assert.AreEqual(tuning.jumpHeight + tuning.mantleMaxHeight, SectionContract.MaxClimb(tuning), 1e-5f);

        [Test]
        public void GapReach_ShrinksUphill_AndEndsWhereTheLedgeIsOutOfReach()
        {
            float flat = SectionContract.MaxGap(tuning, 0f);
            Assert.Greater(flat, 7.4f, "at least a sprint jump");
            Assert.Less(flat, 16f);
            Assert.Less(SectionContract.MaxGap(tuning, 2f), flat, "a higher ledge is reached closer");
            Assert.Greater(SectionContract.MaxGap(tuning, -3f), flat, "a lower one farther");
            Assert.AreEqual(0f, SectionContract.MaxGap(tuning, SectionContract.MaxClimb(tuning) + 0.1f), "above a jump and a mantle: never");
        }

        [Test]
        public void LobReach_ClearsAnyRooflessWallAWayAhead()
        {
            Assert.Greater(SectionContract.MaxLobHeight(tuning, 1f), 20f, "next to the wall a lob climbs very high: only a roof seals it");
            Assert.Less(SectionContract.MaxLobHeight(tuning, 80f), 0f, "far away it falls short");
        }

        [Test]
        public void SpreadReach_GrowsWithTheFirstSourcesHourglass()
        {
            Assert.AreEqual(SectionContract.SpreadMargin, SectionContract.MaxSpread(tuning, 0f), 1e-5f, "no hourglass: only the step between two plates");
            Assert.AreEqual(tuning.sprintSpeed * 5f + SectionContract.SpreadMargin, SectionContract.MaxSpread(tuning, 5f), 1e-4f,
                            "a five-second hourglass: a sprint further");
        }

        [Test]
        public void Scan_FindsAScreenYouWalkRound_UntilWallsCloseItsEnds()
        {
            // Far below everything, in the scene the runner has open, removed afterwards.
            var root = new GameObject("ContractScanFixture");
            root.transform.position = new Vector3(0f, -5000f, 0f);
            try
            {
                GameObject Box(Vector3 local, Vector3 size)
                {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.layer = LayerMask.NameToLayer("Environment");
                    box.transform.SetParent(root.transform, false);
                    box.transform.localPosition = local;
                    box.transform.localScale = size;
                    return box;
                }
                Box(new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
                var screen = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BombObstacleKitBuilder.Screen + ".prefab"));
                screen.transform.SetParent(root.transform, false);

                var open = CourseContractCheck.Problems(root.scene, tuning).FindAll(p => p.Contains("ContractScanFixture"));
                Assert.AreEqual(2, open.FindAll(p => p.Contains("walks round")).Count, string.Join("\n", open));

                Box(new Vector3(-6f, 2f, 0f), new Vector3(8f, 4f, 1f));   // from the posts outward
                Box(new Vector3(6f, 2f, 0f), new Vector3(8f, 4f, 1f));
                var closed = CourseContractCheck.Problems(root.scene, tuning).FindAll(p => p.Contains("ContractScanFixture"));
                Assert.IsFalse(closed.Exists(p => p.Contains("walks round")), string.Join("\n", closed));

                var curtain = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BombObstacleKitBuilder.LaserCurtain + ".prefab"));
                curtain.transform.SetParent(root.transform, false);
                curtain.transform.localPosition = new Vector3(0f, 0f, 5f);
                var loose = CourseContractCheck.Problems(root.scene, tuning).FindAll(p => p.Contains("LaserCurtain"));
                Assert.AreEqual(2, loose.FindAll(p => p.Contains("flies round")).Count, "a curtain standing alone: " + string.Join("\n", loose));
                Assert.AreEqual(1, loose.FindAll(p => p.Contains("flies over")).Count);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCaseSource(nameof(Courses))]
        public void Course_KeepsItsContracts(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) Assert.Ignore(path + " is not built");
            var open = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool alreadyOpen = open.isLoaded;   // never close a scene the developer has open
            var scene = alreadyOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var problems = CourseContractCheck.Problems(scene, tuning);
                if (problems.Count == 0) return;
                string report = $"{problems.Count} finding(s) in {scene.name}:\n  " + string.Join("\n  ", problems);
                Debug.LogWarning("[CourseContract] " + report);   // a warning, never a failure
                Assert.Pass(report);
            }
            finally
            {
                if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
