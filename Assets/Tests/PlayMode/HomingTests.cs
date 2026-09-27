using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Beep.Tests
{
    /// <summary>Soft homing: a throw is bent toward the receiver near your aim, imperfectly; catching stays timed.</summary>
    public class HomingTests : SandboxTestBase
    {
        int catches, explosions;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Object.FindFirstObjectByType<RunManager>().enabled = false;   // these tests drive the bomb by hand
            catches = explosions = 0;
            bomb.BombCaught += r => catches++;
            bomb.BombExploded += (r, d) => explosions++;
            yield break;
        }

        /// <summary>Velocity that would carry the bomb onto p2's chest, then rotated `yawErrorDegrees` off.</summary>
        Vector3 OffAimVelocity(float yawErrorDegrees, float speed = 12f)
        {
            Vector3 origin = p1.ThrowOrigin.position;
            Vector3 to = p2.CatchVolume.CatchCenter - origin;
            Vector3 dir = Quaternion.AngleAxis(yawErrorDegrees, Vector3.up) * (to.normalized + Vector3.up * 0.06f);
            return dir.normalized * speed;
        }

        IEnumerator Throw(Vector3 velocity)
        {
            bomb.BeginReset();
            bomb.EndReset(p1);
            yield return null;
            Assert.IsTrue(bomb.TryThrow(p1, p1.ThrowOrigin.position, velocity));
        }

        [UnityTest]
        public IEnumerator Pick_LocksTheReceiverNearTheAim_NotOneFarOffOrTheThrower()
        {
            Vector3 origin = p1.ThrowOrigin.position;
            Vector3 straight = p2.CatchVolume.CatchCenter - origin;

            Assert.IsTrue(HomingTargeting.TryPick(tuning, origin, straight, p1, Player.All, out var t, out float angle, out float q));
            Assert.AreSame(p2, t);
            Assert.Greater(q, tuning.homingStrength * 0.9f, "dead centre = nearly full strength");

            Assert.IsTrue(HomingTargeting.TryPick(tuning, origin, Quaternion.AngleAxis(20f, Vector3.up) * straight, p1, Player.All, out _, out _, out float weaker));
            Assert.Less(weaker, q, "the lock fades toward the edge of the cone");

            Assert.IsFalse(HomingTargeting.TryPick(tuning, origin, Quaternion.AngleAxis(60f, Vector3.up) * straight, p1, Player.All, out _, out _, out _), "outside the cone: no lock");
            Assert.IsFalse(HomingTargeting.TryPick(tuning, origin, Vector3.down, p1, Player.All, out _, out _, out _), "throwing at the floor locks nobody");
            yield break;
        }

        [UnityTest]
        public IEnumerator ThrowAimedALittleOff_IsBentIntoTheReceiver()
        {
            yield return Throw(OffAimVelocity(14f));
            Assert.AreSame(p2, bomb.HomingTarget, "the host locked onto the receiver");
            yield return CatchWhenNear(p2, 3.0f);
            yield return WaitUntil(() => catches > 0 || explosions > 0, 3f, "flight never ended");
            Assert.AreEqual(1, catches, "a throw 14 degrees off should still be bent into the receiver's hands");
        }

        [UnityTest]
        public IEnumerator SameOffAimThrow_MissesWithoutHoming()
        {
            float original = tuning.homingStrength;
            tuning.homingStrength = 0f;
            try
            {
                yield return Throw(OffAimVelocity(14f));
                Assert.IsNull(bomb.HomingTarget);
                yield return CatchWhenNear(p2, 3.0f);
                yield return WaitUntil(() => catches > 0 || explosions > 0, 3f, "flight never ended");
                Assert.AreEqual(0, catches, "control: without homing the same throw misses");
            }
            finally
            {
                tuning.homingStrength = original;
            }
        }

        [UnityTest]
        public IEnumerator HopelesslyOffAim_StillMisses()
        {
            yield return Throw(OffAimVelocity(55f));
            Assert.IsNull(bomb.HomingTarget, "outside the cone nothing is locked");
            yield return WaitUntil(() => explosions > 0, 3f, "should have hit the world");
            Assert.AreEqual(0, catches);
        }

        [UnityTest]
        public IEnumerator FastChargedThrow_IsBentToo()
        {
            yield return Throw(OffAimVelocity(12f, tuning.throwSpeedMax));
            Assert.AreSame(p2, bomb.HomingTarget);
            yield return CatchWhenNear(p2, 5.0f);
            yield return WaitUntil(() => catches > 0 || explosions > 0, 3f, "flight never ended");
            Assert.AreEqual(1, catches, "hard throws must be redirected as well");
        }

        [UnityTest]
        public IEnumerator Magnet_PullsTheBombIntoHands_WhenTheWindowOpensLate()
        {
            yield return Throw(OffAimVelocity(6f, 10f));
            // Wait until it is very close, and only then press catch.
            yield return WaitUntil(() => Vector3.Distance(bomb.transform.position, p2.CatchVolume.CatchCenter) < 1.6f, 2f, "bomb never got close");
            p2.Input.Scripted ??= new PlayerInputReader.ScriptedInput();
            p2.Input.Scripted.PressCatch();
            yield return WaitUntil(() => catches > 0 || explosions > 0, 2f, "flight never ended");
            Assert.AreEqual(1, catches);
        }

        [UnityTest]
        public IEnumerator PressingAfterItPassed_ExplainsWhy()
        {
            // A bomb hanging in the air far away, that "was in reach" 0.3 s ago.
            bomb.BeginReset();
            bomb.EndReset(p1);
            yield return null;
            Assert.IsTrue(bomb.TryThrow(p1, new Vector3(0f, 9f, 14f), Vector3.zero));
            p2.Catcher.NoteBombInReach();
            yield return WaitSeconds(0.3f);
            p2.Catcher.TryOpenWindow();
            StringAssert.Contains("Too late", p2.Catcher.Hint);
        }
    }
}
