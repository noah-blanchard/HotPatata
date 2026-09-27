using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>The host's record of a flight, used to validate lag-compensated catches.</summary>
    public class FlightHistoryTests
    {
        const float Reach = 1f;

        // The bomb flies along +X past a receiver (slot 1) standing at x = 5; one sample every 0.1 s at 10 m/s.
        static FlightHistory FlyPast(int samples = 10)
        {
            var h = new FlightHistory(16);
            for (int i = 0; i < samples; i++)
            {
                h.Begin(i * 0.1f, new Vector3(i, 0f, 0f));
                h.AddCenter(1, new Vector3(5f, 0f, 0f));
            }
            return h;
        }

        [Test]
        public void LastReach_ReturnsTheLatestSampleInReach()
        {
            // In reach at x = 4, 5, 6 (t = 0.4, 0.5, 0.6): the latest is 0.6.
            Assert.AreEqual(0.6f, FlyPast().LastReach(1, 0f, Reach), 1e-4f);
        }

        [Test]
        public void LastReach_IgnoresSamplesOlderThanSince()
        {
            var h = FlyPast();
            Assert.AreEqual(-1f, h.LastReach(1, 0.65f, Reach), "the bomb left reach before 0.65 s");
            Assert.AreEqual(0.6f, h.LastReach(1, 0.55f, Reach), 1e-4f);
        }

        [Test]
        public void LastReach_IsPerSlot()
        {
            var h = FlyPast();
            Assert.AreEqual(-1f, h.LastReach(2, 0f, Reach), "slot 2 was never recorded");
            Assert.AreEqual(-1f, h.LastReach(7, 0f, Reach), "out-of-range slot");
        }

        [Test]
        public void OldSamples_AreOverwritten_WhenFull()
        {
            var h = FlyPast(40);   // capacity 16: only t = 2.4 .. 3.9 (x = 24 .. 39) remain
            Assert.AreEqual(16, h.Count);
            Assert.AreEqual(-1f, h.LastReach(1, 0f, Reach), "the pass near x = 5 has been overwritten");
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            var h = FlyPast();
            h.Clear();
            Assert.AreEqual(0, h.Count);
            Assert.AreEqual(-1f, h.LastReach(1, 0f, Reach));
        }
    }
}
