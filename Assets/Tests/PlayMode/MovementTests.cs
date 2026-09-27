using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Beep.Tests
{
    /// <summary>M1.2: responsive movement - accel/brake, jump, coyote time, jump buffer.</summary>
    public class MovementTests : SandboxTestBase
    {
        [UnityTest]
        public IEnumerator DrivenPlayerMoves_PlayerWithoutInput_StaysIdle()
        {
            Assert.IsFalse(p2.Input.Active);

            Drive.Move = Vector2.up;
            yield return WaitSeconds(0.5f);
            Drive.Move = Vector2.zero;

            Assert.Greater(p1.transform.position.z, -3.9f, "focused player should have moved");
            Assert.AreEqual(-4f, p2.transform.position.z, 0.01f, "unfocused player must not react to input");
        }

        [UnityTest]
        public IEnumerator Walking_ReachesMoveSpeed_ThenStopsQuickly()
        {
            Drive.Move = Vector2.up;
            yield return WaitSeconds(0.5f);
            Assert.AreEqual(tuning.moveSpeed, HorizontalSpeed(p1), 0.05f, "should be at full speed");

            Drive.Move = Vector2.zero;
            yield return WaitSeconds(tuning.moveSpeed / tuning.braking + 0.12f);
            Assert.Less(HorizontalSpeed(p1), 0.1f, "should brake to a halt without sliding");
        }

        [UnityTest]
        public IEnumerator Walking_MovesRelativeToCameraYaw()
        {
            // Yaw starts at 0 (facing +Z): W goes +Z, D goes +X.
            Vector3 start = p1.transform.position;
            Drive.Move = Vector2.right;
            yield return WaitSeconds(0.4f);
            Drive.Move = Vector2.zero;
            Vector3 d = p1.transform.position - start;
            Assert.Greater(d.x, 0.5f);
            Assert.Less(Mathf.Abs(d.z), 0.05f);
        }

        [UnityTest]
        public IEnumerator Jump_ReachesConfiguredHeight_AndLands()
        {
            float y0 = p1.transform.position.y;
            float apex = y0;

            Drive.PressJump();
            yield return null;

            float end = Time.time + 1.5f;
            bool leftGround = false;
            while (Time.time < end)
            {
                apex = Mathf.Max(apex, p1.transform.position.y);
                if (!p1.Motor.Grounded) leftGround = true;
                if (leftGround && p1.Motor.Grounded) break;
                yield return null;
            }

            Assert.IsTrue(leftGround, "player never left the ground");
            Assert.IsTrue(p1.Motor.Grounded, "player never landed");
            Assert.AreEqual(tuning.jumpHeight, apex - y0, 0.2f, "apex should match tuning.jumpHeight");
        }

        [UnityTest]
        public IEnumerator Jump_CannotDoubleJumpInMidair()
        {
            Drive.PressJump();
            yield return null;
            yield return WaitUntil(() => !p1.Motor.Grounded, 1f, "did not take off");
            yield return WaitSeconds(0.25f);   // well past the coyote window, still rising/at apex

            float vyBefore = p1.Motor.Velocity.y;
            Drive.PressJump();
            yield return null;
            yield return null;

            Assert.LessOrEqual(p1.Motor.Velocity.y, vyBefore + 0.01f, "a second press in the air must not add upward velocity");
        }

        [UnityTest]
        public IEnumerator JumpBuffer_PressJustBeforeLanding_JumpsOnLanding()
        {
            Drive.PressJump();
            yield return null;
            yield return WaitUntil(() => !p1.Motor.Grounded, 1f, "did not take off");
            yield return WaitUntil(() => p1.Motor.Velocity.y < -3f, 1f, "never started falling");

            // Press while still airborne but about to land (inside the buffer window).
            yield return WaitUntil(() => p1.transform.position.y < 0.2f + 0.05f, 1f, "never came close to the ground");
            Drive.PressJump();
            yield return null;

            bool jumpedAgain = false;
            float end = Time.time + 0.6f;
            bool landed = false;
            while (Time.time < end)
            {
                if (p1.Motor.Grounded) landed = true;
                if (landed && p1.Motor.Velocity.y > 5f) { jumpedAgain = true; break; }
                yield return null;
            }
            Assert.IsTrue(jumpedAgain, "a buffered press should trigger a jump as soon as we land");
        }

        [UnityTest]
        public IEnumerator JumpPressedTooEarly_OutsideBuffer_IsIgnored()
        {
            Drive.PressJump();
            yield return null;
            yield return WaitUntil(() => !p1.Motor.Grounded, 1f, "did not take off");
            yield return WaitSeconds(0.1f);   // near the top: > 0.3s of flight remaining

            Drive.PressJump();
            yield return null;

            yield return WaitUntil(() => p1.Motor.Grounded, 2f, "did not land");
            yield return WaitSeconds(0.2f);
            Assert.Less(p1.Motor.Velocity.y, 1f, "an early press (outside the 0.1s buffer) must be forgotten");
        }

        [UnityTest]
        public IEnumerator CoyoteTime_JumpJustAfterWalkingOffALedge_Works()
        {
            yield return WalkOffPlatformAndPressJumpAfter(tuning.coyoteTime * 0.5f);
            Assert.Greater(p1.Motor.Velocity.y, 3f, "jump within the coyote window should work");
        }

        [UnityTest]
        public IEnumerator CoyoteTime_JumpLongAfterLeavingLedge_DoesNothing()
        {
            yield return WalkOffPlatformAndPressJumpAfter(tuning.coyoteTime + 0.04f);
            Assert.Less(p1.Motor.Velocity.y, 0.5f, "jump after the coyote window must not work in midair");
        }

        IEnumerator WalkOffPlatformAndPressJumpAfter(float delayAfterLeavingGround)
        {
            // Platform_A spans x -7.5..-3.5, top at y=1. Start near its +X edge, walk off eastwards (D).
            yield return Place(p1, new Vector3(-4.2f, 1.05f, 0f));
            Drive.Move = Vector2.right;
            yield return WaitUntil(() => !p1.Motor.Grounded, 2f, "never walked off the ledge");
            float t0 = Time.time;
            yield return WaitUntil(() => Time.time - t0 >= delayAfterLeavingGround, 1f, "timing");
            Drive.PressJump();
            yield return null;
            yield return null;
            Drive.Move = Vector2.zero;
        }
    }
}
