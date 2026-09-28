using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HotPatata.Tests
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
            Assert.That(t.catchRadius, Is.InRange(0.7f, 1.0f), "catch zone was widened after the friend playtest");
            Assert.That(t.assistConeDegrees, Is.InRange(0f, 10f), "the aim assist cone stays small (no soft homing)");
            Assert.That(t.assistMaxCorrectionDegrees, Is.InRange(0f, 5f), "the aim assist only nudges the direction");
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
        public void TuningAsset_PlayerIdentity_OneDistinctColourAndShapePerSlot()
        {
            var t = Tuning;
            Assert.AreEqual(Player.MaxSlots, t.playerColors.Length, "one colour per player slot");
            Assert.AreEqual(Player.MaxSlots, t.playerShapes.Length, "one shape per player slot (spec §19: never colour alone)");
            Assert.AreEqual(t.playerShapes.Length, t.playerShapes.Distinct().Count(), "shapes must differ");
            Assert.AreEqual(t.playerColors.Length, t.playerColors.Distinct().Count(), "colours must differ");
        }

        [Test]
        public void PlayerShapeMeshes_AreDistinctSolids_FacingOutward()
        {
            var shapes = (PlayerShape[])System.Enum.GetValues(typeof(PlayerShape));
            Assert.AreEqual(shapes.Length, shapes.Select(PlayerShapeMesh.For).Distinct().Count(), "one mesh per shape");
            foreach (var shape in shapes)
            {
                var mesh = PlayerShapeMesh.For(shape);
                Assert.AreSame(mesh, PlayerShapeMesh.For(shape), shape + " is built once");
                Assert.LessOrEqual(mesh.bounds.extents.magnitude, new Vector3(0.5f, 0.5f, 0.5f).magnitude + 1e-4f, shape + " fits the unit cube");
                var v = mesh.vertices;
                var tris = mesh.triangles;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
                    Assert.GreaterOrEqual(Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c), -1e-6f, shape + " triangle " + i / 3 + " faces inward (culled)");
                }
            }
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
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/HotPatataControls.inputactions");
            Assert.IsNotNull(asset);
            var map = asset.FindActionMap("Player");
            Assert.IsNotNull(map);
            foreach (var a in new[] { "Move", "Look", "Jump", "Throw", "Catch" })
                Assert.IsNotNull(map.FindAction(a), "missing action " + a);
        }
    }
}
