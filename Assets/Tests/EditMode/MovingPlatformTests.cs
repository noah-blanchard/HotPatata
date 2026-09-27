using NUnit.Framework;
using UnityEngine;
using Motion = HotPatata.MovingPlatform.Motion;

namespace HotPatata.Tests
{
    /// <summary>MovingPlatform.Evaluate: the pure clock-to-position maths every machine runs.</summary>
    public class MovingPlatformTests
    {
        const float Length = 4f, Speed = 2f;   // a full A-B-A cycle takes 4 s

        [Test]
        public void PingPong_MatchesTheOriginalConstantSpeedFormula()
        {
            for (float t = 0f; t < 10f; t += 0.37f)
                foreach (float phase in new[] { 0f, 0.25f, 0.5f })
                    Assert.AreEqual(Mathf.PingPong(t * Speed / Length + phase * 2f, 1f),
                                    MovingPlatform.Evaluate(t, Length, Speed, phase, Motion.PingPong, 0.5f), 1e-4f);
        }

        [Test]
        public void Dwell_WaitsAtEachEnd_ThenCrosses()
        {
            // Dwell 0.5: each 2 s leg waits 1 s, then travels for 1 s.
            Assert.AreEqual(0f, MovingPlatform.Evaluate(0.2f, Length, Speed, 0f, Motion.Dwell, 0.5f), 1e-4f);
            Assert.AreEqual(0f, MovingPlatform.Evaluate(0.99f, Length, Speed, 0f, Motion.Dwell, 0.5f), 1e-4f);
            Assert.AreEqual(0.5f, MovingPlatform.Evaluate(1.5f, Length, Speed, 0f, Motion.Dwell, 0.5f), 1e-4f);
            Assert.AreEqual(1f, MovingPlatform.Evaluate(2.2f, Length, Speed, 0f, Motion.Dwell, 0.5f), 1e-4f);
            Assert.AreEqual(1f, MovingPlatform.Evaluate(2.99f, Length, Speed, 0f, Motion.Dwell, 0.5f), 1e-4f);
            Assert.AreEqual(0.5f, MovingPlatform.Evaluate(3.5f, Length, Speed, 0f, Motion.Dwell, 0.5f), 1e-4f);
            Assert.AreEqual(0f, MovingPlatform.Evaluate(4.2f, Length, Speed, 0f, Motion.Dwell, 0.5f), 1e-4f, "the cycle repeats");
        }

        [Test]
        public void Dwell_HalfPhase_StartsAtTheOtherEnd()
        {
            Assert.AreEqual(1f, MovingPlatform.Evaluate(0.3f, Length, Speed, 0.5f, Motion.Dwell, 0.5f), 1e-4f);
        }

        [Test]
        public void Evaluate_IsContinuous_NoJumps()
        {
            foreach (var motion in new[] { Motion.PingPong, Motion.Dwell })
            {
                float prev = MovingPlatform.Evaluate(0f, Length, Speed, 0.1f, motion, 0.4f);
                for (float t = 0.01f; t < 8f; t += 0.01f)
                {
                    float u = MovingPlatform.Evaluate(t, Length, Speed, 0.1f, motion, 0.4f);
                    Assert.Less(Mathf.Abs(u - prev), 0.05f, $"{motion} jumped at t={t}");
                    Assert.That(u, Is.InRange(0f, 1f));
                    prev = u;
                }
            }
        }
    }
}
