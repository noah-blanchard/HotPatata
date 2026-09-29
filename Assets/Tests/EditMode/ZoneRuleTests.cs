using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>
    /// Pure rules of the #68 obstacles: fuse-zone severity (PROJECT_SPEC §7.3), the flight-sweep geometry and the
    /// actuator motion derived from the replicated direction change (§13.15) and the transit exit arc (§13.16).
    /// </summary>
    public class ZoneRuleTests
    {
        GameTuning tuning;

        [SetUp]
        public void SetUp() => tuning = ScriptableObject.CreateInstance<GameTuning>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(tuning);

        // ------------------------------------------------------------------ fuse zones

        [Test]
        public void NoZone_BurnsAtTheNormalRate() => Assert.AreEqual(1f, FuseZone.Combine(new FuseZoneKind[0], tuning));

        [Test]
        public void EachKind_HasItsRate()
        {
            Assert.AreEqual(2f, FuseZone.RateOf(FuseZoneKind.Hot, tuning));
            Assert.AreEqual(0.5f, FuseZone.RateOf(FuseZoneKind.Cold, tuning));
            Assert.IsTrue(float.IsPositiveInfinity(FuseZone.RateOf(FuseZoneKind.Forbidden, tuning)));
        }

        [Test]
        public void OverlappingZones_TheMostSevereWins()
        {
            Assert.AreEqual(2f, FuseZone.Combine(new[] { FuseZoneKind.Cold, FuseZoneKind.Hot }, tuning));
            Assert.AreEqual(0.5f, FuseZone.Combine(new[] { FuseZoneKind.Cold, FuseZoneKind.Cold }, tuning));
            Assert.IsTrue(float.IsPositiveInfinity(FuseZone.Combine(new[] { FuseZoneKind.Cold, FuseZoneKind.Forbidden, FuseZoneKind.Hot }, tuning)),
                          "nothing cancels a forbidden zone");
        }

        // ------------------------------------------------------------------ sweep geometry

        static readonly Vector3 Half = new Vector3(1f, 1f, 0.25f);   // a 2 x 2 m curtain, 0.5 m deep

        [Test]
        public void Segment_CrossingTheBox_Touches_AtItsEntry()
        {
            Assert.IsTrue(Zone.SegmentHitsBox(new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f), Half, out float enter));
            Assert.AreEqual(0.375f, enter, 1e-4f);
        }

        [Test]
        public void Segment_PassingBesideTheBox_DoesNotTouch()
        {
            Assert.IsFalse(Zone.SegmentHitsBox(new Vector3(1.5f, 0f, -1f), new Vector3(1.5f, 0f, 1f), Half, out _));
            Assert.IsFalse(Zone.SegmentHitsBox(new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, -1f), Half, out _), "stops short");
        }

        [Test]
        public void Segment_StartingInside_TouchesAtOnce()
        {
            Assert.IsTrue(Zone.SegmentHitsBox(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 3f, 0f), Half, out float enter));
            Assert.AreEqual(0f, enter);
        }

        [Test]
        public void Segment_ParallelToAFace_OutsideIt_DoesNotTouch() =>
            Assert.IsFalse(Zone.SegmentHitsBox(new Vector3(-3f, 0f, 0.4f), new Vector3(3f, 0f, 0.4f), Half, out _));

        // ------------------------------------------------------------------ actuators

        [Test]
        public void Actuator_OpensOverItsTravelTime_ThenStays()
        {
            Assert.AreEqual(0f, SignalActuator.Progress(10.0, 0f, true, 2f, 10.0));
            Assert.AreEqual(0.5f, SignalActuator.Progress(10.0, 0f, true, 2f, 11.0), 1e-5f);
            Assert.AreEqual(1f, SignalActuator.Progress(10.0, 0f, true, 2f, 30.0));
        }

        // ------------------------------------------------------------------ transit

        [Test]
        public void TransitExitArc_ReachesItsTarget_AtTheFlightTime()
        {
            float g = ThrowBallistics.Gravity(tuning);
            var from = new Vector3(0f, 5f, 8f);
            var to = new Vector3(2f, 1.3f, 30f);
            Vector3 v = BombTransit.ExitVelocity(from, to, g, 2f);
            Vector3 at = ThrowBallistics.PositionAt(from, v, g, 2f);
            Assert.Less(Vector3.Distance(to, at), 1e-3f);
        }

        [Test]
        public void Actuator_ReversedMidway_ClosesFromWhereItWas()
        {
            // Half open when the source let go at t = 5: back to closed one second later (travel 2 s).
            Assert.AreEqual(0.25f, SignalActuator.Progress(5.0, 0.5f, false, 2f, 5.5), 1e-5f);
            Assert.AreEqual(0f, SignalActuator.Progress(5.0, 0.5f, false, 2f, 6.5));
        }
    }
}
