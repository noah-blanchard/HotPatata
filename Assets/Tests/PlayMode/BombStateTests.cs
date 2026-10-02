using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// Bomb state machine (M1.4) plus the catch / world-contact / fuse rules it enforces
    /// (M1.6-M1.8), exercised through the real components in the PassSandbox scene.
    /// </summary>
    public class BombStateTests : SandboxTestBase
    {
        BombFailReason? lastFailReason;
        int explosions, catches;

        [UnitySetUp]
        public IEnumerator Subscribe()
        {
            // These tests drive the bomb by hand; keep the RunManager's automatic reset out of the way.
            Object.FindFirstObjectByType<RunManager>().enabled = false;
            explosions = catches = 0;
            lastFailReason = null;
            bomb.BombExploded += (reason, detail) => { explosions++; lastFailReason = reason; };
            bomb.BombCaught += receiver => catches++;
            yield break;
        }

        // ------------------------------------------------------------------ helpers

        void UseShortFuse(float seconds)
        {
            var clone = Object.Instantiate(tuning);
            clone.holdFuseDuration = seconds;
            bomb.Fuse.SetTuning(clone);
        }

        // ------------------------------------------------------------------ M1.4 attach / detach

        [UnityTest]
        public IEnumerator Held_BombAttachesToHandAnchor_WithCollisionOff()
        {
            Give(p1);
            yield return null;

            Assert.AreEqual(BombState.Held, bomb.State);
            Assert.AreSame(p1, bomb.Carrier);
            Assert.AreSame(p1.HandAnchor, bomb.transform.parent);
            Assert.AreEqual(0f, Vector3.Distance(bomb.transform.position, p1.HandAnchor.position), 0.001f);
            Assert.IsFalse(bomb.GetComponent<SphereCollider>().enabled, "held bomb must not collide with anything");
            Assert.IsTrue(bomb.GetComponent<Rigidbody>().isKinematic);
        }

        [UnityTest]
        public IEnumerator Throw_DetachesBombIntoFreeFlight()
        {
            Give(p1);
            yield return null;
            Vector3 origin = p1.ThrowOrigin.position;

            Assert.IsTrue(bomb.TryThrow(p1, origin, new Vector3(0f, 3f, 10f)));

            Assert.AreEqual(BombState.Thrown, bomb.State);
            Assert.IsNull(bomb.Carrier);
            Assert.AreSame(p1, bomb.LastThrower);
            Assert.IsNull(bomb.transform.parent);
            var rb = bomb.GetComponent<Rigidbody>();
            Assert.IsFalse(rb.isKinematic);
            Assert.IsTrue(bomb.GetComponent<SphereCollider>().enabled);
            Assert.AreEqual(10f, rb.linearVelocity.z, 0.01f);
            Assert.AreEqual(origin, bomb.transform.position);
        }

        [UnityTest]
        public IEnumerator Throw_IsRejected_ForNonCarrier_AndWhenNotHeld()
        {
            Give(p1);
            yield return null;
            Assert.IsFalse(bomb.TryThrow(p2, p2.ThrowOrigin.position, Vector3.forward * 5f), "only the carrier can throw");
            Assert.AreEqual(BombState.Held, bomb.State);

            bomb.BeginReset();
            Assert.IsFalse(bomb.TryThrow(p1, p1.ThrowOrigin.position, Vector3.forward * 5f), "cannot throw while Resetting");
        }

        [UnityTest]
        public IEnumerator HeldBomb_InsideWallGeometry_DoesNotExplode()
        {
            // Wall_Test spans z 9.5..10.5. Put the carrier so the hand anchor is inside it.
            bomb.Fuse.SetTuning(Object.Instantiate(tuning));   // fresh fuse; irrelevant, but isolate from asset
            yield return Place(p1, new Vector3(0f, 0.05f, 9.8f));
            Give(p1);
            Assert.Greater(p1.HandAnchor.position.z, 9.5f);

            yield return WaitSeconds(0.6f);

            Assert.AreEqual(BombState.Held, bomb.State);
            Assert.AreEqual(0, explosions);
        }

        // ------------------------------------------------------------------ M1.7 world contact

        [UnityTest]
        public IEnumerator Thrown_IntoFloor_Explodes_WithWorldContactReason()
        {
            Give(p1);
            yield return null;
            Assert.IsTrue(bomb.TryThrow(p1, p1.ThrowOrigin.position, Vector3.down * 6f));

            yield return WaitUntil(() => explosions > 0, 2f, "bomb thrown at the floor never exploded");
            Assert.AreEqual(BombFailReason.WorldContact, lastFailReason);
            Assert.AreEqual(BombState.Exploding, bomb.State);
        }

        [UnityTest]
        public IEnumerator Thrown_IntoWall_Explodes()
        {
            Give(p1);
            yield return null;
            Vector3 origin = p1.ThrowOrigin.position;
            // Wall_Test face is at z=9.5; aim straight at it from ~13 m.
            Assert.IsTrue(bomb.TryThrow(p1, origin, VelocityToHit(origin, new Vector3(-3f, 2f, 9.3f), 1.0f)));

            yield return WaitUntil(() => explosions > 0, 3f, "bomb thrown at the wall never exploded");
            Assert.AreEqual(BombFailReason.WorldContact, lastFailReason);
        }

        [UnityTest]
        public IEnumerator Thrown_IntoPlatform_Explodes()
        {
            Give(p2);
            yield return null;
            Vector3 origin = p2.ThrowOrigin.position;
            // Platform_B: x 3.5..7.5, z -3..3, top y=1. Lob it at its side face.
            Assert.IsTrue(bomb.TryThrow(p2, origin, VelocityToHit(origin, new Vector3(5f, 0.5f, -2.9f), 0.5f)));

            yield return WaitUntil(() => explosions > 0, 3f, "bomb thrown at the platform never exploded");
            Assert.AreEqual(BombFailReason.WorldContact, lastFailReason);
        }

        [UnityTest]
        public IEnumerator Thrown_InsideKillZone_Explodes_WithKillZoneReason()
        {
            Give(p1);
            yield return null;
            Assert.IsTrue(bomb.TryThrow(p1, new Vector3(0f, -15f, 0f), Vector3.zero));

            yield return WaitUntil(() => explosions > 0, 2f, "bomb inside the kill zone never exploded");
            Assert.AreEqual(BombFailReason.KillZone, lastFailReason);
        }

        [UnityTest]
        public IEnumerator Explosion_HappensOnlyOnce()
        {
            Give(p1);
            yield return null;
            Assert.IsTrue(bomb.TryThrow(p1, p1.ThrowOrigin.position, Vector3.down * 6f));
            yield return WaitUntil(() => explosions > 0, 2f, "no explosion");
            yield return WaitSeconds(0.5f);
            Assert.AreEqual(1, explosions);
        }

        // ------------------------------------------------------------------ M1.6 catch

        [UnityTest]
        public IEnumerator Pass_ToOtherPlayer_IsCaught_AndSnapsToTheirHand()
        {
            Give(p1);
            yield return null;
            ThrowAt(p1, p2);
            yield return CatchWhenNear(p2);

            yield return WaitUntil(() => catches > 0, 2f, "pass was never caught");

            Assert.AreSame(p2, bomb.Carrier);
            Assert.AreSame(p2.HandAnchor, bomb.transform.parent);
            Assert.AreEqual(0, explosions);
            Assert.AreEqual(BombState.CaughtGrace, bomb.State);
        }

        [UnityTest]
        public IEnumerator Catch_EntersGrace_ThenBecomesHeld_WithoutExploding()
        {
            Give(p1);
            yield return null;
            ThrowAt(p1, p2);
            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => catches > 0, 2f, "pass was never caught");
            Assert.AreEqual(BombState.CaughtGrace, bomb.State);

            yield return WaitSeconds(tuning.caughtGraceDuration + 0.25f);

            Assert.AreEqual(BombState.Held, bomb.State);
            Assert.AreSame(p2, bomb.Carrier);
            Assert.AreEqual(0, explosions);
        }

        [UnityTest]
        public IEnumerator Catch_ResolvesExactlyOnce()
        {
            Give(p1);
            yield return null;
            ThrowAt(p1, p2);
            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => catches > 0, 2f, "pass was never caught");
            yield return WaitSeconds(0.5f);
            Assert.AreEqual(1, catches);
        }

        [UnityTest]
        public IEnumerator Thrower_CannotCatchTheirOwnThrow()
        {
            Give(p1);
            yield return null;
            // Straight up: the bomb flies through p1's own catch volume on the way up and down.
            Assert.IsTrue(bomb.TryThrow(p1, p1.ThrowOrigin.position, Vector3.up * 4f));

            yield return WaitUntil(() => explosions > 0, 3f, "bomb should have fallen and exploded");
            Assert.AreEqual(0, catches, "the thrower must never catch their own throw");
        }

        [UnityTest]
        public IEnumerator Catch_RefreshesTheFuse()
        {
            UseShortFuse(3f);
            Give(p1);
            yield return WaitSeconds(1.2f);
            Assert.Less(bomb.Fuse.Remaining, 2f, "fuse should have been burning while held");

            ThrowAt(p1, p2);
            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => catches > 0, 2f, "pass was never caught");

            Assert.AreEqual(3f, bomb.Fuse.Remaining, 0.1f, "catch must restore the full hold window");
        }

        [UnityTest]
        public IEnumerator Fuse_DoesNotBurn_WhileThrown()
        {
            UseShortFuse(3f);
            Give(p1);
            yield return null;
            float before = bomb.Fuse.Remaining;

            // A slow, high lob so the flight lasts a while but ends in a catch.
            ThrowAt(p1, p2, 0.9f);
            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => bomb.State != BombState.Thrown, 3f, "flight never ended");

            Assert.AreEqual(BombState.CaughtGrace, bomb.State);
            Assert.GreaterOrEqual(bomb.Fuse.Remaining, before - 0.15f);
        }

        // ------------------------------------------------------------------ timed catch (never automatic)

        [UnityTest]
        public IEnumerator Catch_IsNeverAutomatic_WithoutPressing_TheBombFliesThrough()
        {
            Give(p1);
            yield return null;
            ThrowAt(p1, p2);   // perfectly on target, but p2 never presses catch

            yield return WaitUntil(() => explosions > 0, 3f, "the missed bomb should end up hitting the world");
            Assert.AreEqual(0, catches);
            Assert.AreEqual(BombFailReason.WorldContact, lastFailReason);
        }

        [UnityTest]
        public IEnumerator Catch_PressedTooEarly_MissesTheWindow()
        {
            Give(p1);
            yield return null;
            ThrowAt(p1, p2, 0.7f);
            p2.Input.Scripted = new PlayerInputReader.ScriptedInput();
            p2.Input.Scripted.PressCatch();   // window (0.25 s) is long over before the bomb arrives at ~0.7 s

            yield return WaitUntil(() => explosions > 0, 3f, "the missed bomb should end up hitting the world");
            Assert.AreEqual(0, catches);
        }

        [UnityTest]
        public IEnumerator Catch_PressedWhileBombIsAlreadyInsideTheVolume_StillCatches()
        {
            Give(p1);
            yield return null;
            // Park a slow-falling thrown bomb right inside p2's catch volume, then press catch a moment later.
            Assert.IsTrue(bomb.TryThrow(p1, p2.CatchVolume.CatchCenter + Vector3.up * 0.2f, Vector3.zero));
            yield return WaitSeconds(0.08f);
            p2.Input.Scripted = new PlayerInputReader.ScriptedInput();
            p2.Input.Scripted.PressCatch();

            yield return WaitUntil(() => catches > 0 || explosions > 0, 1f, "nothing happened");
            Assert.AreEqual(1, catches);
            Assert.AreSame(p2, bomb.Carrier);
        }

        [UnityTest]
        public IEnumerator CatchButton_OpensAWindow_ThenCoolsDown_AndCannotBeSpammed()
        {
            p2.Input.Scripted = new PlayerInputReader.ScriptedInput();
            Give(p1);
            yield return null;

            p2.Input.Scripted.PressCatch();
            yield return null;
            Assert.IsTrue(p2.Catcher.WindowOpen, "pressing catch opens the window");

            yield return WaitSeconds(tuning.catchWindowDuration + 0.05f);
            Assert.IsFalse(p2.Catcher.WindowOpen, "the window is short");
            Assert.IsTrue(p2.Catcher.OnCooldown);

            p2.Input.Scripted.PressCatch();   // mashing during the cooldown does nothing
            yield return null;
            Assert.IsFalse(p2.Catcher.WindowOpen);

            yield return WaitSeconds(tuning.catchCooldown + 0.05f);
            p2.Input.Scripted.PressCatch();
            yield return null;
            Assert.IsTrue(p2.Catcher.WindowOpen, "available again after the cooldown");
        }

        [UnityTest]
        public IEnumerator Carrier_CannotOpenACatchWindow()
        {
            Give(p1);
            yield return null;
            p1.Input.Scripted.PressCatch();
            yield return null;
            Assert.IsFalse(p1.Catcher.WindowOpen);
        }

        // ------------------------------------------------------------------ M1.8 fuse

        [UnityTest]
        public IEnumerator HoldingTooLong_ExplodesWithFuseReason()
        {
            UseShortFuse(1f);
            Give(p1);

            yield return WaitUntil(() => explosions > 0, 3f, "held bomb never exploded");
            Assert.AreEqual(BombFailReason.HoldFuseExpired, lastFailReason);
            Assert.AreEqual(BombState.Exploding, bomb.State);
        }

        [UnityTest]
        public IEnumerator Fuse_CannotExpire_AfterExplosion_OrDuringReset()
        {
            UseShortFuse(0.5f);
            Give(p1);
            yield return WaitUntil(() => explosions > 0, 2f, "no explosion");
            yield return WaitSeconds(0.8f);
            Assert.AreEqual(1, explosions, "an exploded bomb must not explode again");

            bomb.BeginReset();
            yield return WaitSeconds(0.8f);
            Assert.AreEqual(1, explosions, "fuse must be frozen during Resetting");
            Assert.AreEqual(BombState.Resetting, bomb.State);
        }
    }
}
