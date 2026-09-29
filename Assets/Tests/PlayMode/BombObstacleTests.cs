using System.Collections;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// The #68 bomb obstacles (MVP_TASKS M10), built from their kit prefabs on the open floor of PassSandbox and driven
    /// through the real bomb: fuse zones and laser curtains (PROJECT_SPEC §7.3).
    /// </summary>
    public class BombObstacleTests : SandboxTestBase
    {
        static readonly Vector3 O = new Vector3(0f, 0f, -14f);   // free floor, south of the spawn row

        BombFailReason? lastFailReason;
        int explosions, catches;

        [UnitySetUp]
        public IEnumerator Subscribe()
        {
            // The bomb is driven by hand; keep the RunManager's automatic reset out of the way.
            Object.FindFirstObjectByType<RunManager>().enabled = false;
            explosions = catches = 0;
            lastFailReason = null;
            bomb.BombExploded += (reason, detail) => { explosions++; lastFailReason = reason; };
            bomb.BombCaught += receiver => catches++;
            yield break;
        }

        static GameObject Spawn(string prefab, Vector3 position)
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + prefab + ".prefab");
            Assert.IsNotNull(asset, "missing prefab " + prefab);
            return Object.Instantiate(asset, position, Quaternion.identity);
#else
            Assert.Inconclusive("Editor only");
            return null;
#endif
        }

        IEnumerator PassFrom(Player thrower, Vector3 throwerSpot, Player receiver, Vector3 receiverSpot)
        {
            yield return Place(receiver, receiverSpot);
            yield return Place(thrower, throwerSpot);
            Give(thrower);
            yield return null;
            ThrowAt(thrower, receiver);
            yield return CatchWhenNear(receiver);
        }

        // ------------------------------------------------------------------ fuse zones

        [UnityTest]
        public IEnumerator ForbiddenZone_ExplodesTheBomb_WhenItsCarrierWalksIn()
        {
            Spawn("Gameplay/Zone_Forbidden", O);
            yield return Place(p1, O + new Vector3(0f, 0.05f, -4f));
            Give(p1);
            yield return WaitSeconds(0.2f);
            Assert.AreEqual(0, explosions, "outside the zone nothing happens");

            yield return Place(p1, O + new Vector3(0f, 0.05f, 0f));
            yield return WaitUntil(() => explosions > 0, 1f, "the carrier stood in a forbidden zone");
            Assert.AreEqual(BombFailReason.ForbiddenZone, lastFailReason);
        }

        [UnityTest]
        public IEnumerator ForbiddenZone_LetsAPlayerWithoutTheBombCross()
        {
            Spawn("Gameplay/Zone_Forbidden", O);
            yield return Place(p2, O + new Vector3(0f, 0.05f, -4f));
            Give(p2);
            yield return Place(p1, O + new Vector3(0f, 0.05f, 0f));
            yield return WaitSeconds(0.5f);

            Assert.AreEqual(0, explosions);
            Assert.AreEqual(BombState.Held, bomb.State);
        }

        IEnumerator MeasureBurn(float seconds, System.Action<float> consumed)
        {
            float start = bomb.Fuse.Remaining, t0 = Time.time;
            yield return WaitSeconds(seconds);
            consumed((start - bomb.Fuse.Remaining) / (Time.time - t0));
        }

        [UnityTest]
        public IEnumerator HotZone_BurnsTheFuseTwiceAsFast_ColdZoneHalfAsFast()
        {
            var hot = Spawn("Gameplay/Zone_Hot", O);
            yield return Place(p1, O + new Vector3(0f, 0.05f, 0f));
            Give(p1);
            yield return null;
            float rate = 0f;
            yield return MeasureBurn(1f, r => rate = r);
            Assert.AreEqual(tuning.hotZoneFuseRate, rate, 0.15f, "hot zone");
            Assert.AreEqual(tuning.hotZoneFuseRate, bomb.Fuse.Rate, 1e-4f);

            Object.Destroy(hot);
            Spawn("Gameplay/Zone_Cold", O);
            Give(p1);
            yield return null;
            yield return MeasureBurn(1f, r => rate = r);
            Assert.AreEqual(tuning.coldZoneFuseRate, rate, 0.1f, "cold zone");
            Assert.AreEqual(0, explosions);
        }

        [UnityTest]
        public IEnumerator OverlappingHotAndColdZones_TheHotOneWins()
        {
            Spawn("Gameplay/Zone_Cold", O);
            Spawn("Gameplay/Zone_Hot", O);
            yield return Place(p1, O + new Vector3(0f, 0.05f, 0f));
            Give(p1);
            yield return null;
            yield return null;
            Assert.AreEqual(tuning.hotZoneFuseRate, bomb.Fuse.Rate, 1e-4f);
        }

        [UnityTest]
        public IEnumerator CatchInsideAForbiddenZone_Explodes()
        {
            Spawn("Gameplay/Zone_Forbidden", O);
            yield return PassFrom(p1, O + new Vector3(0f, 0.05f, -5f), p2, O + new Vector3(0f, 0.05f, 0f));
            yield return WaitUntil(() => explosions > 0, 2f, "a catch inside a forbidden zone");

            Assert.AreEqual(1, catches, "the catch itself is valid");
            Assert.AreEqual(BombFailReason.ForbiddenZone, lastFailReason);
        }

        // ------------------------------------------------------------------ laser curtains and windows

        [UnityTest]
        public IEnumerator LaserCurtain_ExplodesAThrowAcrossIt()
        {
            Spawn("Gameplay/LaserCurtain", O);
            yield return PassFrom(p1, O + new Vector3(0f, 0.05f, -5f), p2, O + new Vector3(0f, 0.05f, 5f));
            yield return WaitUntil(() => explosions > 0, 2f, "a throw through a laser curtain");

            Assert.AreEqual(0, catches);
            Assert.AreEqual(BombFailReason.ForbiddenZone, lastFailReason);
            Assert.Less(bomb.transform.position.z, O.z + 1f, "it exploded at the curtain, not at the receiver");
        }

        [UnityTest]
        public IEnumerator WindowBetweenTwoCurtains_LetsThePassThrough_AndPlayersWalkThroughTheCurtains()
        {
            Spawn("Gameplay/LaserCurtain", O + new Vector3(-3f, 0f, 0f));
            Spawn("Gameplay/LaserCurtain", O + new Vector3(3f, 0f, 0f));
            yield return PassFrom(p1, O + new Vector3(0f, 0.05f, -5f), p2, O + new Vector3(0f, 0.05f, 5f));
            yield return WaitUntil(() => catches > 0 || explosions > 0, 2f, "the pass through the window");
            Assert.AreEqual(1, catches);
            Assert.AreEqual(0, explosions);

            // The thrower, now empty-handed, walks through a curtain.
            yield return Place(p1, O + new Vector3(-3f, 0.05f, 0f));
            yield return WaitSeconds(0.4f);
            Assert.AreEqual(0, explosions);
        }
    }
}
