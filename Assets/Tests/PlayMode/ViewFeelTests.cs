using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Beep.Tests
{
    /// <summary>First-person speed feel: progressive acceleration, FOV, lean, bob/dip, post effects, accessibility.</summary>
    public class ViewFeelTests : SandboxTestBase
    {
        Camera Cam => Camera.main;

        [UnityTest]
        public IEnumerator Acceleration_IsProgressive_ButStillReachesFullSpeedQuickly()
        {
            Drive.Move = Vector2.up;
            yield return WaitSeconds(0.06f);
            Assert.Less(HorizontalSpeed(p1), tuning.moveSpeed * 0.6f, "the run should build up, not snap to speed");

            yield return WaitSeconds(0.6f);
            Assert.AreEqual(tuning.moveSpeed, HorizontalSpeed(p1), 0.1f, "but full speed arrives within a fraction of a second");
        }

        [UnityTest]
        public IEnumerator Running_WidensTheFov_AndItRelaxesWhenStopped()
        {
            float baseFov = tuning.fieldOfView;
            Assert.AreEqual(baseFov, Cam.fieldOfView, 1f, "standing still = base FOV");

            Drive.Move = Vector2.up;
            yield return WaitSeconds(1.0f);
            Assert.Greater(Cam.fieldOfView, baseFov + tuning.fovKickAtSpeed * 0.6f, "running should open the view");

            Drive.Move = Vector2.zero;
            yield return WaitSeconds(1.8f);
            Assert.AreEqual(baseFov, Cam.fieldOfView, 1.5f, "the FOV eases back after stopping (momentum, not a snap)");
        }

        [UnityTest]
        public IEnumerator Strafing_LeansTheView()
        {
            Drive.Move = Vector2.right;
            yield return WaitSeconds(0.6f);
            float roll = Mathf.Abs(Mathf.DeltaAngle(Cam.transform.eulerAngles.z, 0f));
            Assert.Greater(roll, 0.8f, "strafing should tilt the camera");
            Assert.Less(roll, 6f, "but only a little");
        }

        [UnityTest]
        public IEnumerator Running_BuildsUpPostEffects_ThatFadeAgain()
        {
            var volume = Cam.GetComponentInChildren<Volume>();
            Assert.IsTrue(volume.sharedProfile.TryGet(out Vignette vignette));

            Assert.Less(vignette.intensity.value, 0.02f, "clean when standing still");
            Drive.Move = Vector2.up;
            yield return WaitSeconds(1.0f);
            Assert.Greater(vignette.intensity.value, 0.12f, "vignette grows with speed");

            Drive.Move = Vector2.zero;
            yield return WaitSeconds(1.0f);
            Assert.Less(vignette.intensity.value, 0.05f);
        }

        [UnityTest]
        public IEnumerator HardLanding_DipsTheView()
        {
            p1.TeleportTo(new Vector3(0f, 5f, -6f), Quaternion.identity);
            float lowest = 0f;
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                lowest = Mathf.Min(lowest, p1.Feel.EyeOffset.y);
                if (p1.Motor.Grounded && Time.time > end - 2.6f && lowest < -0.03f) break;
                yield return null;
            }
            Assert.Less(lowest, -0.03f, "landing from height should dip the eye");
        }

        [UnityTest]
        public IEnumerator EffectsStrengthZero_KeepsTheCameraSteady()
        {
            float original = tuning.viewEffectsStrength;
            tuning.viewEffectsStrength = 0f;
            try
            {
                Drive.Move = Vector2.right + Vector2.up;
                yield return WaitSeconds(1.0f);
                Assert.AreEqual(tuning.fieldOfView, Cam.fieldOfView, 0.01f, "no FOV effect");
                Assert.AreEqual(0f, Mathf.DeltaAngle(Cam.transform.eulerAngles.z, 0f), 0.01f, "no roll");
                Assert.AreEqual(0f, p1.Feel.EyeOffset.magnitude, 0.0001f, "no bob");
            }
            finally
            {
                tuning.viewEffectsStrength = original;
            }
        }
    }
}
