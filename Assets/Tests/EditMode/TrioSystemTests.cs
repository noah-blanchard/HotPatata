using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>
    /// Pure rules of the systems built for three players (PROJECT_SPEC §13.21-§13.24): the heavy plate and the bomb in
    /// flight still being its thrower's, the hourglass plate's memory, the pivot carrying its riders round, the sun beam.
    /// </summary>
    public class TrioSystemTests
    {
        readonly List<GameObject> made = new List<GameObject>();

        Player NewPlayer(string name)
        {
            var go = new GameObject(name);
            made.Add(go);
            return go.AddComponent<Player>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in made) Object.DestroyImmediate(go);
            made.Clear();
        }

        // ------------------------------------------------------------------ heavy plate (§13.21)

        [Test]
        public void HeavyPlate_NeedsTwoCountedBodies()
        {
            Player a = NewPlayer("a"), b = NewPlayer("b"), carrier = NewPlayer("c");
            Assert.IsFalse(PressurePlate.IsHeld(new HashSet<Player> { a }, null, 2), "one body is not enough");
            Assert.IsTrue(PressurePlate.IsHeld(new HashSet<Player> { a, b }, null, 2), "two bodies hold it");
            Assert.IsFalse(PressurePlate.IsHeld(new HashSet<Player> { a, carrier }, carrier, 2), "the carrier does not count on a hands-free heavy plate");
            Assert.IsTrue(PressurePlate.IsHeld(new HashSet<Player> { a, b, carrier }, carrier, 2), "two empty-handed and the carrier");
            Assert.AreEqual(2, PressurePlate.Counted(new HashSet<Player> { a, b, carrier }, carrier));
        }

        [Test]
        public void BombInFlight_IsStillItsThrowers()
        {
            Player thrower = NewPlayer("t"), catcher = NewPlayer("r");
            Assert.AreEqual(thrower, PressurePlate.ExcludedPlayer(BombState.Thrown, null, thrower), "a lob does not free the thrower");
            Assert.AreEqual(thrower, PressurePlate.ExcludedPlayer(BombState.InTransit, null, thrower), "nor a tube");
            Assert.AreEqual(catcher, PressurePlate.ExcludedPlayer(BombState.CaughtGrace, catcher, thrower), "once caught, the catcher holds it");
            Assert.AreEqual(catcher, PressurePlate.ExcludedPlayer(BombState.Held, catcher, thrower));
            Assert.IsNull(PressurePlate.ExcludedPlayer(BombState.Resetting, null, thrower), "nobody while the section resets");
            Assert.IsNull(PressurePlate.ExcludedPlayer(BombState.Thrown, null, null), "a transit exit arc belongs to nobody");

            // Two players on a hands-free heavy plate juggling: the one in the air is still the thrower's.
            var both = new HashSet<Player> { thrower, catcher };
            Assert.IsFalse(PressurePlate.IsHeld(both, PressurePlate.ExcludedPlayer(BombState.Thrown, null, thrower), 2));
        }

        // ------------------------------------------------------------------ hourglass plate (§13.22)

        [Test]
        public void HourglassPlate_StaysActiveForItsMemory()
        {
            Assert.IsTrue(PressurePlate.IsActive(PressurePlate.HeldForever, 1e6), "held: open-ended");
            double end = PressurePlate.ReleaseEnd(10.0, 4f);
            Assert.AreEqual(14.0, end, 1e-9);
            Assert.IsTrue(PressurePlate.IsActive(end, 13.9), "the sand is still running");
            Assert.IsFalse(PressurePlate.IsActive(end, 14.0), "and runs out");
            Assert.IsFalse(PressurePlate.IsActive(double.NegativeInfinity, 0.0), "at rest");
            Assert.AreEqual(10.0, PressurePlate.ReleaseEnd(10.0, 0f), 1e-9, "no memory: off at once");
        }

        // ------------------------------------------------------------------ pivot carrying its riders (§13.24)

        [Test]
        public void Pivot_SwingsItsRiderRoundTheAnchor()
        {
            Vector3 anchor = new Vector3(10f, 2f, 0f);
            Vector3 feet = anchor + new Vector3(4f, 0f, 0f);
            var quarter = Quaternion.Euler(0f, 90f, 0f);
            Vector3 carried = feet + PlayerMotor.Carry(feet, Vector3.zero, anchor, quarter);
            Assert.That(Vector3.Distance(carried, anchor + new Vector3(0f, 0f, -4f)), Is.LessThan(1e-4f), "a quarter turn moves the rider a quarter round");
            Assert.AreEqual(90f, PlayerMotor.CarriedYaw(quarter), 1e-3f, "and turns the view with it");
            Assert.AreEqual(-30f, PlayerMotor.CarriedYaw(Quaternion.Euler(0f, -30f, 0f)), 1e-3f);
        }

        [Test]
        public void SlidingCarrier_StillCarriesByItsDelta()
        {
            Vector3 feet = new Vector3(1f, 0f, 1f);
            Vector3 delta = new Vector3(0.2f, 0f, -0.1f);
            Assert.That(Vector3.Distance(PlayerMotor.Carry(feet, delta, Vector3.zero, Quaternion.identity), delta), Is.LessThan(1e-5f));
            Assert.AreEqual(0f, PlayerMotor.CarriedYaw(Quaternion.identity));
        }

        [Test]
        public void RemoteRider_IsRebuiltInTheCarriersFrame()
        {
            var rider = new RiderReconstruction();
            rider.Retarget(7, new Vector3(3f, 0f, 0f), 0.02f, 10f);
            var turned = Quaternion.Euler(0f, 90f, 0f);
            Vector3 drawn = Vector3.zero;
            for (int i = 0; i < 20; i++) drawn = rider.Blend(Vector3.zero, Vector3.zero, turned, true, 0.1f, 0.1f, 50f);
            Assert.That(Vector3.Distance(drawn, new Vector3(0f, 0f, -3f)), Is.LessThan(1e-3f), "the offset turns with the pivot");
        }

        // ------------------------------------------------------------------ sun beam (§13.23)

        [Test]
        public void SunBeam_IsCutWhereTheNearestBodyStands()
        {
            Assert.AreEqual(0f, SunBeam.CutFraction(-5f, 5f), 1e-5f, "at the slit");
            Assert.AreEqual(0.5f, SunBeam.CutFraction(0f, 5f), 1e-5f, "half way");
            Assert.AreEqual(1f, SunBeam.CutFraction(9f, 5f), 1e-5f, "past the eye: clamped");
            Assert.AreEqual(0f, SunBeam.CutFraction(1f, 0f), "a beam of no length");
        }
    }
}
