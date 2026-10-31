using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// Every authored level (ARCHITECTURE §27, Assets/Scenes/Levels): the route runner walks every walkway, stair and bridge it lays
    /// (its <see cref="RouteWalks"/>, gathered on save) and must reach each end without stalling or falling; the level has players,
    /// a bomb and a finish. One fixture per level; none when there are no levels. The level tools' showcase (a dev scene, never in the
    /// menu, M21 L6) is walked the same way.
    /// </summary>
    [TestFixtureSource(nameof(Levels))]
    public class AuthoredLevelTests : SandboxTestBase
    {
        readonly string level;

        public AuthoredLevelTests(string level) => this.level = level;

        protected override string SceneName => level.Contains("/") ? level : "Levels/" + level;

        static IEnumerable<string> Levels()
        {
#if UNITY_EDITOR
            const string folder = "Assets/Scenes/Levels", showcase = "Assets/Scenes/Dev/LevelToolsShowcase.unity";
            var levels = UnityEditor.AssetDatabase.IsValidFolder(folder)
                ? UnityEditor.AssetDatabase.FindAssets("t:Scene", new[] { folder }).Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                    .Select(System.IO.Path.GetFileNameWithoutExtension).OrderBy(n => n).ToList()
                : new List<string>();
            if (System.IO.File.Exists(showcase)) levels.Add("Dev/LevelToolsShowcase");
            return levels;
#else
            return Enumerable.Empty<string>();
#endif
        }

        [Test]
        public void Level_HasItsRunBombAndFinish()
        {
            Assert.IsNotNull(Object.FindFirstObjectByType<RunManager>(), "no run");
            Assert.IsNotNull(bomb, "no bomb");
            Assert.AreEqual(1, Object.FindObjectsByType<FinishZone>(FindObjectsSortMode.None).Length, "a level has one finish");
        }

        [UnityTest]
        public IEnumerator RouteRunner_WalksEveryWalkwayStairAndBridge()
        {
            DevCheats.NoExplosions = true;
            try
            {
                var walks = Object.FindFirstObjectByType<RouteWalks>();
                Assert.IsNotNull(walks, "the level keeps its walked lines (saved with the level tools)");
                var runner = new RouteRunner(p1, p2, Drive);
                yield return runner.WalkAll(walks);
                Debug.Log($"[{level} route runner] timings\n" + runner.Report);
                Assert.IsEmpty(runner.Failures, string.Join("\n", runner.Failures));
            }
            finally { DevCheats.NoExplosions = false; }
        }
    }
}
