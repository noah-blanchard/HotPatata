using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>Fuse maths (ARCHITECTURE §18.1): refresh, expiry, warning phase and stage bands.</summary>
    public class BombFuseTests
    {
        GameObject go;
        BombFuse fuse;
        GameTuning tuning;

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<GameTuning>();   // spec defaults: 6s / 2s warning
            go = new GameObject("fuse");
            fuse = go.AddComponent<BombFuse>();
            fuse.SetTuning(tuning);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void Starts_Full()
        {
            Assert.AreEqual(6f, fuse.Remaining, 1e-4f);
            Assert.AreEqual(0f, fuse.Consumed01, 1e-4f);
            Assert.IsFalse(fuse.IsExpired);
            Assert.AreEqual(FuseStage.Calm, fuse.Stage);
        }

        [Test]
        public void Tick_ConsumesTime()
        {
            fuse.Tick(1.5f);
            Assert.AreEqual(4.5f, fuse.Remaining, 1e-4f);
            Assert.AreEqual(0.25f, fuse.Consumed01, 1e-4f);
        }

        [Test]
        public void Expires_Exactly_Once()
        {
            int fired = 0;
            fuse.Expired += () => fired++;

            fuse.Tick(5.9f);
            Assert.AreEqual(0, fired);
            fuse.Tick(0.2f);
            fuse.Tick(1f);
            fuse.Tick(1f);

            Assert.AreEqual(1, fired);
            Assert.IsTrue(fuse.IsExpired);
            Assert.AreEqual(0f, fuse.Remaining);
        }

        [Test]
        public void Refresh_RestoresFullFuse_AndRearmsExpiry()
        {
            int fired = 0;
            fuse.Expired += () => fired++;

            fuse.Tick(6.5f);
            Assert.AreEqual(1, fired);

            fuse.Refresh();
            Assert.AreEqual(6f, fuse.Remaining, 1e-4f);
            Assert.IsFalse(fuse.IsExpired);

            fuse.Tick(7f);
            Assert.AreEqual(2, fired, "a refreshed fuse can expire again");
        }

        [Test]
        public void WarningPhase_IsTheFinalTwoSeconds()
        {
            fuse.Tick(3.9f);   // 2.1s left
            Assert.IsFalse(fuse.InWarningPhase);
            fuse.Tick(0.2f);   // 1.9s left
            Assert.IsTrue(fuse.InWarningPhase);
        }

        [TestCase(0.0f, FuseStage.Calm)]
        [TestCase(2.9f, FuseStage.Calm)]      // 48% consumed
        [TestCase(3.1f, FuseStage.Medium)]    // 52%
        [TestCase(4.6f, FuseStage.Urgent)]    // 77%
        [TestCase(5.5f, FuseStage.Critical)]  // 92%
        public void Stage_FollowsConsumedFraction(float consumedSeconds, FuseStage expected)
        {
            fuse.Tick(consumedSeconds);
            Assert.AreEqual(expected, fuse.Stage);
        }

        [Test]
        public void Duration_FollowsTuning()
        {
            tuning.holdFuseDuration = 4.5f;
            fuse.Refresh();
            Assert.AreEqual(4.5f, fuse.Remaining, 1e-4f);
            Assert.AreEqual(4.5f, fuse.Duration, 1e-4f);
        }
    }
}
