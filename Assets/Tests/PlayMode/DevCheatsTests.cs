using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// The map-testing cheats (DevCheats, docs/TESTING.md §2): noclip flies through the floor, invincible sends a falling player
    /// back to their spawn without a team reset, no explosions hands the bomb back instead of blowing it up.
    /// </summary>
    public class DevCheatsTests : SandboxTestBase
    {
        static readonly Vector3 Area = new Vector3(0f, 0f, -120f);   // clear of the sandbox

        RunManager run;
        int explosions;

        [UnitySetUp]
        public IEnumerator Prepare()
        {
            run = RunManager.Instance;
            explosions = 0;
            bomb.BombExploded += (reason, detail) => explosions++;
            yield break;
        }

        [TearDown]
        public void CheatsOff()
        {
            DevCheats.NoClip = DevCheats.Invincible = DevCheats.NoExplosions = false;
        }

        [UnityTest]
        public IEnumerator NoClip_FliesThroughTheFloor_AndLandsAgainWhenOff()
        {
            float start = p1.transform.position.y;
            DevCheats.NoClip = true;
            Drive.Crouch = true;   // crouch = down while flying
            yield return WaitSeconds(0.5f);
            Drive.Crouch = false;
            Assert.Less(p1.transform.position.y, start - 2f, "flew down through the floor");

            DevCheats.NoClip = false;
            yield return Place(p1, new Vector3(0f, 0.05f, -6f));   // walking again: collisions are back
            Assert.IsTrue(p1.Motor.Grounded);
        }

        [UnityTest]
        public IEnumerator Invincible_AFallSendsThePlayerBackToTheirSpawn_WithoutAReset()
        {
            DevCheats.Invincible = true;
            Vector3 spawn = p1.transform.position;
            var pit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pit.layer = LayerMask.NameToLayer("Trigger");
            pit.transform.position = Area;
            pit.transform.localScale = new Vector3(10f, 2f, 10f);
            pit.AddComponent<KillZone>();
            Physics.SyncTransforms();

            p1.TeleportTo(Area + Vector3.up * 4f, Quaternion.identity);
            yield return WaitUntil(() => (p1.transform.position - spawn).sqrMagnitude < 4f, 3f, "never sent back to the spawn");
            Assert.AreEqual(0, run.ResetCount, "no team reset");
            Assert.AreEqual(RunState.Playing, run.State);
        }

        [UnityTest]
        public IEnumerator NoExplosions_HandsTheBombBack_InsteadOfExploding()
        {
            DevCheats.NoExplosions = true;
            Give(p1);
            yield return null;
            Vector3 origin = p1.ThrowOrigin.position;
            Assert.IsTrue(bomb.TryThrow(p1, origin, Vector3.down * 10f), "thrown at the floor");
            yield return WaitUntil(() => bomb.State == BombState.Held, 2f, "the bomb never came back");
            Assert.AreEqual(p1, bomb.Carrier, "back in its thrower's hand");

            bomb.Fuse.SetDurationOverride(0.3f);   // and a fuse running out does not blow it up either
            Give(p1);
            yield return WaitSeconds(1f);
            Assert.AreEqual(0, explosions);
            Assert.AreEqual(0, run.ResetCount);
            Assert.AreEqual(p1, bomb.Carrier);
        }
    }
}
