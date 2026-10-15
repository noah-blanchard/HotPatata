using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// PatataRanger (PROJECT_SPEC §15f, M17). The route runner: a player is driven with scripted input (the real motor, no noclip, no
    /// invincibility) along every walkway, stair and bridge the builder laid; it must reach the end of each without stalling or falling.
    /// A gate on the way is opened the way the course opens it: the partner stands on its plate. The bomb is kept from exploding while
    /// it runs (a traversal test); the timings it logs feed the trials' windows. The walker is the shared <see cref="RouteRunner"/>.
    /// </summary>
    public class PatataRangerTests : SandboxTestBase
    {
        protected override string SceneName => "PatataRanger";

        [UnityTest]
        public IEnumerator RouteRunner_WalksEveryWalkwayStairAndBridge()
        {
            DevCheats.NoExplosions = true;
            try
            {
                var walks = Object.FindFirstObjectByType<RouteWalks>();
                Assert.IsNotNull(walks, "the course keeps its walked lines");
                Assert.Greater(walks.Lines.Count, 10, "every walkway, stair and bridge is listed");
                var runner = new RouteRunner(p1, p2, Drive);
                yield return runner.WalkAll(walks);
                Debug.Log("[RangerRouteRunner] timings\n" + runner.Report);
                Assert.IsEmpty(runner.Failures, string.Join("\n", runner.Failures));
            }
            finally { DevCheats.NoExplosions = false; }
        }
    }
}
