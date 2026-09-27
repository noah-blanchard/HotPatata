using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Beep.Tests
{
    /// <summary>M0: project baseline - tuning defaults, layers and the collision matrix.</summary>
    public class ConfigurationTests
    {
        const string TuningPath = "Assets/ScriptableObjects/Tuning/GameTuning.asset";

        static GameTuning Tuning => AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath);

        [Test]
        public void TuningAsset_Exists_WithSpecStartingValues()
        {
            var t = Tuning;
            Assert.IsNotNull(t, "GameTuning asset missing");
            Assert.AreEqual(6.0f, t.holdFuseDuration, 1e-4f);
            Assert.AreEqual(2.0f, t.warningDuration, 1e-4f);
            Assert.AreEqual(0.35f, t.caughtGraceDuration, 1e-4f);
            Assert.AreEqual(1.0f, t.resetDelay, 1e-4f);
            Assert.AreEqual(0.1f, t.coyoteTime, 1e-4f);
            Assert.AreEqual(0.1f, t.jumpBuffer, 1e-4f);
            Assert.That(t.catchRadius, Is.InRange(0.4f, 0.8f), "catch radius was tightened after playtest");
            Assert.Less(t.throwSpeedMin, t.throwSpeedMax);
            Assert.Greater(t.throwChargeTime, 0f);
            Assert.That(t.catchWindowDuration, Is.InRange(0.05f, 0.5f), "catch window must stay small");
            Assert.Greater(t.catchCooldown, 0f);
        }

        [Test]
        public void TuningAsset_FeedbackArraysAreWellFormed()
        {
            var t = Tuning;
            Assert.AreEqual(4, t.beepIntervals.Length, "one beep interval per fuse stage");
            Assert.AreEqual(3, t.stageThresholds.Length, "medium/urgent/critical thresholds");
            for (int i = 1; i < t.beepIntervals.Length; i++)
                Assert.Less(t.beepIntervals[i], t.beepIntervals[i - 1], "beeps must speed up with urgency");
            for (int i = 1; i < t.stageThresholds.Length; i++)
                Assert.Greater(t.stageThresholds[i], t.stageThresholds[i - 1]);
        }

        [Test]
        public void GameplayLayers_Exist()
        {
            foreach (var name in new[] { "Player", "PlayerCatch", "Bomb", "Environment", "Hazard", "Trigger" })
                Assert.GreaterOrEqual(LayerMask.NameToLayer(name), 0, "missing layer " + name);
        }

        [Test]
        public void CollisionMatrix_BombIgnoresPlayerBodies_ButHitsWorld()
        {
            int bomb = LayerMask.NameToLayer("Bomb");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(bomb, LayerMask.NameToLayer("Player")),
                "bomb must not collide with player bodies (catches go through PlayerCatch)");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(bomb, LayerMask.NameToLayer("Environment")));
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(bomb, LayerMask.NameToLayer("Hazard")));
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(bomb, LayerMask.NameToLayer("PlayerCatch")));
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(bomb, LayerMask.NameToLayer("Trigger")));
        }

        [Test]
        public void CollisionMatrix_CatchVolumesTouchOnlyTheBomb()
        {
            int catchLayer = LayerMask.NameToLayer("PlayerCatch");
            foreach (var other in new[] { "Player", "Environment", "Hazard", "Trigger", "PlayerCatch" })
                Assert.IsTrue(Physics.GetIgnoreLayerCollision(catchLayer, LayerMask.NameToLayer(other)),
                    "PlayerCatch should ignore " + other);
        }

        [Test]
        public void InputActions_HaveTheGameplayActions()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/BeepControls.inputactions");
            Assert.IsNotNull(asset);
            var map = asset.FindActionMap("Player");
            Assert.IsNotNull(map);
            foreach (var a in new[] { "Move", "Look", "Jump", "Throw", "Catch" })
                Assert.IsNotNull(map.FindAction(a), "missing action " + a);
        }
    }
}
