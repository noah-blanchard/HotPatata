using NUnit.Framework;
using UnityEngine;

namespace Beep.Tests
{
    /// <summary>
    /// The release-time aim assist and the arc maths it relies on (PROJECT_SPEC §8.3): it only turns the direction,
    /// a few capped degrees, and never gives a weak throw the range to reach a far receiver.
    /// </summary>
    public class AimAssistTests
    {
        GameTuning t;

        [SetUp]
        public void Make() => t = ScriptableObject.CreateInstance<GameTuning>();

        [TearDown]
        public void Destroy() => Object.DestroyImmediate(t);

        static float Elevation(Vector3 d) => Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;

        static Vector3 Dir(float yaw, float elevation) => Quaternion.Euler(-elevation, yaw, 0f) * Vector3.forward;

        float Gravity => ThrowBallistics.Gravity(t);

        [Test]
        public void Ballistics_SolvedArc_PassesThroughTheTarget()
        {
            Vector3 to = new Vector3(3f, -0.3f, 9f);
            Assert.IsTrue(ThrowBallistics.TrySolveLowArc(to, 22f, Gravity, out var dir, out float time));
            float miss = ThrowBallistics.ClosestApproach(Vector3.zero, dir * 22f, Gravity, to, time + 0.5f, out float at);
            Assert.Less(miss, 0.05f);
            Assert.AreEqual(time, at, 0.03f, "the reported flight time matches the arc");
        }

        [Test]
        public void Ballistics_OutOfRange_HasNoSolution()
        {
            float maxRange = 16f * 16f / Gravity;   // 45° on level ground
            Assert.IsFalse(ThrowBallistics.TrySolveLowArc(new Vector3(0f, 0f, maxRange + 2f), 16f, Gravity, out _, out _));
        }

        [Test]
        public void Strength_IsZeroOutsideTheCone_AndFadesWithAngleAndDistance()
        {
            Assert.AreEqual(0f, AimAssist.StrengthFor(t, t.assistConeDegrees + 0.1f, 5f));
            Assert.AreEqual(t.assistStrength, AimAssist.StrengthFor(t, 0f, 5f), 1e-4f, "dead on, close: full strength");
            Assert.Less(AimAssist.StrengthFor(t, t.assistConeDegrees * 0.5f, 5f), AimAssist.StrengthFor(t, 0f, 5f));
            Assert.Less(AimAssist.StrengthFor(t, 0f, t.assistMaxRange), AimAssist.StrengthFor(t, 0f, 5f));
        }

        [Test]
        public void OffAimAngle_CountsTheBodyAsADisc()
        {
            Vector3 to = new Vector3(0f, 0f, 8f);
            Assert.AreEqual(0f, AimAssist.OffAimAngle(Vector3.forward, to), 1e-4f);
            float edge = Mathf.Atan2(AimAssist.BodyRadius, 8f) * Mathf.Rad2Deg;
            Assert.AreEqual(0f, AimAssist.OffAimAngle(Dir(edge * 0.9f, 0f), to), 1e-3f, "aiming at the body's edge is on target");
            Assert.AreEqual(5f, AimAssist.OffAimAngle(Dir(edge + 5f, 0f), to), 0.05f);
        }

        [Test]
        public void Correction_NeverExceedsTheCaps()
        {
            var rng = new System.Random(7);
            for (int i = 0; i < 300; i++)
            {
                float yaw = (float)rng.NextDouble() * 20f - 10f;
                float elevation = (float)rng.NextDouble() * 30f - 10f;
                Vector3 raw = Dir(yaw, elevation);
                Vector3 center = new Vector3((float)rng.NextDouble() * 6f - 3f, (float)rng.NextDouble() * 3f - 1.5f, 4f + (float)rng.NextDouble() * 10f);
                float speed = Mathf.Lerp(t.throwSpeedMin, t.throwSpeedMax, (float)rng.NextDouble());

                var r = AimAssist.Correct(t, Vector3.zero, raw, speed, Vector3.zero, center, Vector3.zero, 0f, center.magnitude);
                Assert.LessOrEqual(r.Correction, t.assistMaxCorrectionDegrees + 0.01f, $"case {i}");
                Assert.LessOrEqual(Elevation(r.Direction) - elevation, t.assistMaxElevationDegrees + 0.01f, $"case {i}: too much lift");
                Assert.AreEqual(1f, r.Direction.magnitude, 1e-4f);
            }
        }

        [Test]
        public void NearMiss_IsTurnedOntoTheReceiver()
        {
            Vector3 center = new Vector3(0f, 0f, 10f);
            float speed = 22f;
            Assert.IsTrue(ThrowBallistics.TrySolveLowArc(center, speed, Gravity, out var perfect, out _));
            Vector3 raw = Quaternion.AngleAxis(2f, Vector3.up) * perfect;   // 2° off to the side: ~35 cm at 10 m

            var r = AimAssist.Correct(t, Vector3.zero, raw, speed, Vector3.zero, center, Vector3.zero, 0f, 10f);
            Assert.IsTrue(r.Reachable);
            float before = ThrowBallistics.ClosestApproach(Vector3.zero, raw * speed, Gravity, center, 1.5f, out _);
            float after = ThrowBallistics.ClosestApproach(Vector3.zero, r.Direction * speed, Gravity, center, 1.5f, out _);
            Assert.Less(after, before * 0.6f, $"closer after the assist ({after:F2} m vs {before:F2} m)");
        }

        [Test]
        public void WeakThrow_AtAFarReceiver_IsOnlyTurned_AndFallsShort()
        {
            // A tap aimed straight at a receiver 14 m away on level ground: the lock may show, but no range is added.
            Vector3 center = new Vector3(0f, 0f, 14f);
            float speed = t.throwSpeedMin;
            Vector3 raw = Dir(1.5f, t.throwUpAngle);

            var r = AimAssist.Correct(t, Vector3.zero, raw, speed, Vector3.zero, center, Vector3.zero, 0f, 14f);
            float rawRange = Range(raw * speed);
            float assistedRange = Range(r.Direction * speed);

            if (!r.Reachable) Assert.AreEqual(Elevation(raw), Elevation(r.Direction), 1e-3f, "too weak: heading only, no lift");
            Assert.LessOrEqual(assistedRange, rawRange + 1f, $"the assist adds at most a metre of range ({rawRange:F1} -> {assistedRange:F1} m)");
            Assert.Less(assistedRange, 12f, "a tap must not become a 14 m pass");
        }

        [Test]
        public void MovingReceiver_IsLedALittle()
        {
            Vector3 center = new Vector3(0f, 0f, 8f);
            float speed = 22f;
            Assert.IsTrue(ThrowBallistics.TrySolveLowArc(center, speed, Gravity, out var raw, out _));
            var r = AimAssist.Correct(t, Vector3.zero, raw, speed, Vector3.zero, center, new Vector3(6f, 0f, 0f), 0f, 8f);
            Assert.Greater(r.Direction.x, 0.001f, "aimed a little ahead, toward where they are running");
        }

        /// <summary>Where a throw from the origin comes back down to launch height.</summary>
        float Range(Vector3 v)
        {
            float time = 2f * v.y / Gravity;
            return new Vector2(v.x, v.z).magnitude * Mathf.Max(0f, time);
        }
    }
}
