using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Beep.Tests
{
    /// <summary>M1.5 throw by input and M1.10 fast, clean section reset (RunManager enabled).</summary>
    public class RunAndThrowTests : SandboxTestBase
    {
        RunManager run;

        [UnitySetUp]
        public IEnumerator Find()
        {
            run = Object.FindFirstObjectByType<RunManager>();
            yield break;
        }

        [UnityTest]
        public IEnumerator Run_StartsPlaying_WithBombHeldByStartingCarrier()
        {
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(BombState.Held, bomb.State);
            Assert.AreSame(p1, bomb.Carrier);
            Assert.AreEqual(tuning.holdFuseDuration, bomb.Fuse.Remaining, 0.2f);
            yield break;
        }

        [UnityTest]
        public IEnumerator ThrowInput_AimedAtOtherPlayer_ReleasesBomb_AndItIsCaught()
        {
            int catches = 0;
            bomb.BombCaught += r => catches++;

            // Face player 2 (6 m to the +X side), aim level.
            p1.Look.SetAim(90f, 0f);
            Drive.PressThrow();
            yield return null;
            Assert.AreEqual(BombState.Thrown, bomb.State, "throw input should release the bomb");
            yield return CatchWhenNear(p2);

            yield return WaitUntil(() => catches > 0 || bomb.State == BombState.Exploding, 2f, "flight never ended");
            Assert.AreEqual(1, catches, "a level throw at a player 6 m away should be caught");
            Assert.AreSame(p2, bomb.Carrier);
        }

        [UnityTest]
        public IEnumerator SameAim_GivesSameThrow()
        {
            p1.Look.SetAim(90f, 0f);
            Vector3 origin = p1.ThrowOrigin.position;
            Vector3 a = p1.Thrower.ComputeThrowVelocity(origin, 0f);
            Vector3 b = p1.Thrower.ComputeThrowVelocity(origin, 0f);
            Assert.AreEqual(0f, Vector3.Distance(a, b), 1e-5f);
            Assert.AreEqual(tuning.throwSpeedMin, a.magnitude, 0.01f, "no charge = minimum speed");
            Assert.AreEqual(tuning.throwSpeedMax, p1.Thrower.ComputeThrowVelocity(origin, 1f).magnitude, 0.01f, "full charge = maximum speed");
            yield break;
        }

        [UnityTest]
        public IEnumerator Throw_IsIgnored_ForPlayerWithoutTheBomb()
        {
            p2.Input.Scripted = new PlayerInputReader.ScriptedInput();
            p2.Input.Scripted.PressThrow();
            yield return null;
            Assert.AreEqual(BombState.Held, bomb.State);
            Assert.AreSame(p1, bomb.Carrier);
        }

        [UnityTest]
        public IEnumerator Explosion_ResetsTheSection_QuicklyAndCleanly()
        {
            // Scatter the players and fire the bomb into the floor.
            yield return Place(p1, new Vector3(0f, 0.05f, 5f));
            yield return Place(p2, new Vector3(6f, 0.05f, 5f));
            Drive.Move = Vector2.up;

            bomb.Explode(BombFailReason.WorldContact, "test");
            Assert.AreEqual(RunState.Failing, run.State);
            Assert.IsTrue(p1.ControlLocked && p2.ControlLocked, "players are locked out during the failure beat");

            float t0 = Time.time;
            yield return WaitUntil(() => run.State == RunState.Playing, 3f, "section never resumed");
            Assert.AreEqual(tuning.resetDelay, Time.time - t0, 0.25f, "reset should take about resetDelay");

            Assert.AreEqual(1, run.ResetCount);
            Assert.AreEqual(BombState.Held, bomb.State);
            Assert.AreSame(p1, bomb.Carrier);
            Assert.AreEqual(tuning.holdFuseDuration, bomb.Fuse.Remaining, 0.2f, "fuse must be full again");
            Assert.AreEqual(0f, bomb.Body.Velocity.magnitude, 1e-4f, "no stale bomb velocity");
            Assert.IsFalse(p1.ControlLocked || p2.ControlLocked, "control returns");
            Assert.AreEqual(-3f, p1.transform.position.x, 0.05f);
            Assert.AreEqual(3f, p2.transform.position.x, 0.05f);
            Assert.AreEqual(0f, p1.Motor.Velocity.x + p2.Motor.Velocity.x, 0.1f, "players' motion is cleared");
        }

        [UnityTest]
        public IEnumerator RepeatedFailures_LeaveExactlyOneBomb_InAValidState()
        {
            for (int i = 1; i <= 4; i++)
            {
                bomb.Explode(i % 2 == 0 ? BombFailReason.HoldFuseExpired : BombFailReason.WorldContact, "loop " + i);
                yield return WaitUntil(() => run.State == RunState.Playing, 3f, "no resume on failure " + i);
                Assert.AreEqual(BombState.Held, bomb.State, "failure " + i);
                Assert.IsNotNull(bomb.Carrier, "failure " + i);
            }
            Assert.AreEqual(4, run.ResetCount);
            Assert.AreEqual(1, Object.FindObjectsByType<BombController>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator HoldingTooLong_TriggersAResetOnItsOwn()
        {
            var clone = Object.Instantiate(tuning);
            clone.holdFuseDuration = 0.8f;
            bomb.Fuse.SetTuning(clone);
            bomb.BeginReset();
            bomb.EndReset(p1);

            yield return WaitUntil(() => run.ResetCount == 1, 3f, "fuse expiry never failed the section");
            yield return WaitUntil(() => run.State == RunState.Playing, 3f, "section never resumed");
            Assert.AreEqual(BombState.Held, bomb.State);
        }

        [UnityTest]
        public IEnumerator PassLoop_TwentyPassesBetweenTwoPlayers_NoFailures()
        {
            p2.Input.Scripted = new PlayerInputReader.ScriptedInput();
            int explosions = 0, catches = 0;
            bomb.BombExploded += (r, d) => explosions++;
            bomb.BombCaught += r => catches++;

            for (int pass = 0; pass < 20; pass++)
            {
                Player from = bomb.Carrier;
                Player to = from == p1 ? p2 : p1;
                yield return WaitUntil(() => bomb.State == BombState.Held, 2f, "bomb never became throwable on pass " + pass);

                // Aim level at the other player (p1 is at -X, p2 at +X) and throw with the real input path.
                from.Look.SetAim(from == p1 ? 90f : 270f, 0f);
                int before = catches;
                from.Input.Scripted.PressThrow();
                yield return null;
                yield return CatchWhenNear(to);
                yield return WaitUntil(() => catches > before || explosions > 0, 2f, "pass " + pass + " never landed");
                Assert.AreEqual(0, explosions, "pass " + pass + " failed the section");
                Assert.AreSame(to, bomb.Carrier, "pass " + pass);
            }
            Assert.AreEqual(20, catches);
            Assert.AreEqual(0, run.ResetCount);
        }

        // ------------------------------------------------------------------ charged throw

        [UnityTest]
        public IEnumerator Tap_ThrowsAtTheMinimumSpeed()
        {
            p1.Look.SetAim(90f, 0f);
            Drive.PressThrow();
            yield return null;
            Assert.AreEqual(BombState.Thrown, bomb.State);
            Assert.AreEqual(tuning.throwSpeedMin, bomb.Body.Velocity.magnitude, 1.0f);
        }

        [UnityTest]
        public IEnumerator HoldingCharges_AndReleaseThrowsFasterThanATap()
        {
            p1.Look.SetAim(90f, 0f);
            Drive.SetThrowHeld(true);
            yield return WaitSeconds(tuning.throwChargeTime * 0.5f);
            Assert.IsTrue(p1.Thrower.Charging);
            Assert.AreEqual(0.5f, p1.Thrower.Charge01, 0.2f);
            Assert.AreEqual(BombState.Held, bomb.State, "still charging, not thrown yet");

            Drive.SetThrowHeld(false);
            yield return null;

            Assert.AreEqual(BombState.Thrown, bomb.State);
            float expected = Mathf.Lerp(tuning.throwSpeedMin, tuning.throwSpeedMax, 0.5f);
            Assert.AreEqual(expected, bomb.Body.Velocity.magnitude, 2.5f);
            Assert.Greater(bomb.Body.Velocity.magnitude, tuning.throwSpeedMin + 2f, "a half charge must be clearly faster than a tap");
            Assert.IsFalse(p1.Thrower.Charging);
        }

        [UnityTest]
        public IEnumerator FullCharge_IsCapped_AtTheMaximumSpeed()
        {
            p1.Look.SetAim(90f, 0f);
            Drive.SetThrowHeld(true);
            yield return WaitSeconds(tuning.throwChargeTime + 0.4f);
            Assert.AreEqual(1f, p1.Thrower.Charge01, 1e-4f);

            Drive.SetThrowHeld(false);
            yield return null;
            Assert.AreEqual(tuning.throwSpeedMax, bomb.Body.Velocity.magnitude, 1.0f);
        }

        [UnityTest]
        public IEnumerator ChargedThrow_TravelsFartherThanATap()
        {
            // Aim level over open floor (+X, the far wall is 23 m away) and compare where the bomb first touches down.
            p1.Look.SetAim(90f, -5f);
            bomb.BombExploded += (r, d) => { };
            Drive.PressThrow();
            yield return WaitUntil(() => bomb.State == BombState.Exploding, 3f, "tap never landed");
            float tapDistance = bomb.transform.position.x - p1.transform.position.x;

            yield return WaitUntil(() => run.State == RunState.Playing, 3f, "reset");
            p1.Look.SetAim(90f, -5f);
            Drive.SetThrowHeld(true);
            yield return WaitSeconds(tuning.throwChargeTime + 0.1f);
            Drive.SetThrowHeld(false);
            yield return WaitUntil(() => bomb.State == BombState.Exploding, 4f, "charged throw never landed");
            float chargedDistance = bomb.transform.position.x - p1.transform.position.x;

            Assert.Greater(chargedDistance, tapDistance * 1.5f, $"charged {chargedDistance:F1} m vs tap {tapDistance:F1} m");
        }

        // ------------------------------------------------------------------ natural motion

        [UnityTest]
        public IEnumerator ThrownBomb_Tumbles_ThenSettlesInTheReceiversHand()
        {
            var visual = bomb.transform.Find("Visual");
            p2.Input.Scripted = new PlayerInputReader.ScriptedInput();
            p1.Look.SetAim(90f, 0f);
            Drive.PressThrow();
            yield return null;
            yield return WaitUntil(() => bomb.State == BombState.Thrown, 1f, "not thrown");

            yield return WaitSeconds(0.1f);
            Quaternion a = visual.rotation;
            yield return WaitSeconds(0.15f);
            Assert.Greater(Quaternion.Angle(a, visual.rotation), 15f, "the potato should visibly tumble in flight");

            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => bomb.State == BombState.CaughtGrace || bomb.State == BombState.Exploding, 2f, "flight never ended");
            yield return WaitSeconds(0.6f);
            Assert.AreEqual(BombState.Held, bomb.State);
            Assert.Less(Quaternion.Angle(visual.localRotation, Quaternion.identity), 12f, "settled into a gentle sway, not still spinning");
        }

        // ------------------------------------------------------------------ first person

        [UnityTest]
        public IEnumerator HeldBomb_IsVisible_InFrontOfTheHoldersCamera()
        {
            yield return null;
            yield return null;   // camera follows in LateUpdate
            Assert.AreSame(p1, bomb.Carrier);

            var vp = Camera.main.WorldToViewportPoint(bomb.transform.position);
            Assert.Greater(vp.z, 0.3f, "bomb must be in front of the camera, past the near plane");
            Assert.That(vp.x, Is.InRange(0.55f, 0.95f), "bomb should sit toward the right of the view");
            Assert.That(vp.y, Is.InRange(0.05f, 0.45f), "bomb should sit toward the bottom of the view");
        }

        [UnityTest]
        public IEnumerator HeldBomb_StaysInView_WhenLookingUpAndDown()
        {
            foreach (float pitch in new[] { -60f, 60f })
            {
                p1.Look.SetAim(0f, pitch);
                yield return null;
                yield return null;
                var vp = Camera.main.WorldToViewportPoint(bomb.transform.position);
                Assert.That(vp.x, Is.InRange(0f, 1f), "pitch " + pitch);
                Assert.That(vp.y, Is.InRange(0f, 1f), "pitch " + pitch);
                Assert.Greater(vp.z, 0.3f, "pitch " + pitch);
            }
        }

        [UnityTest]
        public IEnumerator OwnBodyIsHiddenFromOwnCamera_OtherPlayersStayVisible()
        {
            yield return null;
            foreach (var r in p1.GetComponentsInChildren<Renderer>())
                if (r.GetComponentInParent<BombController>() == null)
                    Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly, r.shadowCastingMode, r.name + " of the local player");
            foreach (var r in p2.GetComponentsInChildren<Renderer>())
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On, r.shadowCastingMode, r.name + " of the remote player");
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On, bomb.GetComponentInChildren<Renderer>().shadowCastingMode, "the held bomb must stay visible");
        }
    }
}
