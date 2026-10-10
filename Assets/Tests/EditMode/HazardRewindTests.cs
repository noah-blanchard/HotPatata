using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Motion = HotPatata.MovingPlatform.Motion;

namespace HotPatata.Tests
{
    /// <summary>
    /// The host judges a remote player against a moving hazard at the time that player saw it
    /// (docs/netcode-deterministic-plan.md §2.4): the hazard is posed at that time (pure functions of time), the claim is
    /// capped, and the overlap test is plain geometry.
    /// </summary>
    public class HazardRewindTests
    {
        static readonly Vector3 Up = Vector3.up;

        static HazardRewind.OrientedBox Box(Vector3 center, Vector3 half, Quaternion? rotation = null) =>
            new HazardRewind.OrientedBox { Center = center, HalfExtents = half, Rotation = rotation ?? Quaternion.identity };

        [Test]
        public void Capsule_TouchesABox_OnlyWithinItsRadius()
        {
            var box = Box(Vector3.zero, new Vector3(1f, 0.25f, 0.25f));
            Assert.IsTrue(HazardRewind.CapsuleHitsBox(new Vector3(0f, -1f, 0f), new Vector3(0f, 1f, 0f), 0.3f, box), "through it");
            Assert.IsTrue(HazardRewind.CapsuleHitsBox(new Vector3(0f, 0f, 0.5f), new Vector3(0f, 1.5f, 0.5f), 0.3f, box), "grazing the side");
            Assert.IsFalse(HazardRewind.CapsuleHitsBox(new Vector3(0f, 0f, 0.6f), new Vector3(0f, 1.5f, 0.6f), 0.3f, box), "a hair beside it");
            Assert.IsFalse(HazardRewind.CapsuleHitsBox(new Vector3(0f, 0.6f, 0f), new Vector3(0f, 2f, 0f), 0.3f, box), "above it");
        }

        [Test]
        public void Capsule_TestsARotatedBoxInItsOwnFrame()
        {
            // A 4 m bar turned 90°: it now lies along z.
            var bar = Box(Vector3.zero, new Vector3(2f, 0.2f, 0.2f), Quaternion.AngleAxis(90f, Up));
            Assert.IsTrue(HazardRewind.CapsuleHitsBox(new Vector3(0f, -1f, 1.5f), new Vector3(0f, 1f, 1.5f), 0.3f, bar));
            Assert.IsFalse(HazardRewind.CapsuleHitsBox(new Vector3(1.5f, -1f, 0f), new Vector3(1.5f, 1f, 0f), 0.3f, bar));
        }

        [Test]
        public void Carry_MovesTheBoxRigidlyWithItsMover()
        {
            // A sweeper arm: the lethal box sits 2 m out along x from a pivot at the origin.
            var arm = Box(new Vector3(2f, 0f, 0f), new Vector3(1f, 0.2f, 0.2f));
            var then = HazardRewind.Carry(arm, Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.AngleAxis(90f, Up));
            Assert.Less((then.Center - new Vector3(0f, 0f, -2f)).magnitude, 1e-4f, "a quarter turn back, the arm pointed along -z");
            Assert.Less(Quaternion.Angle(then.Rotation, Quaternion.AngleAxis(90f, Up)), 0.01f);

            var lift = Box(new Vector3(0f, 3f, 0f), Vector3.one);
            var lower = HazardRewind.Carry(lift, new Vector3(0f, 3f, 0f), Quaternion.identity, new Vector3(0f, 1f, 0f), Quaternion.identity);
            Assert.Less((lower.Center - new Vector3(0f, 1f, 0f)).magnitude, 1e-4f, "a translation carries the box along");
        }

        [Test]
        public void TheVerdict_IsTheOneAtTheClaimedTime_NotNow()
        {
            // The bar swept through the player's spot 150 ms ago (on their screen) and is a quarter turn away now.
            var nowArm = Box(new Vector3(0f, 0f, 2f), new Vector3(0.2f, 0.2f, 1f));
            var playerBottom = new Vector3(2f, -0.5f, 0f);
            var playerTop = new Vector3(2f, 1f, 0f);
            Assert.IsFalse(HazardRewind.CapsuleHitsBox(playerBottom, playerTop, 0.3f, nowArm), "the host's present: no hit");

            var thenArm = HazardRewind.Carry(nowArm, Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.AngleAxis(90f, Up));
            Assert.IsTrue(HazardRewind.CapsuleHitsBox(playerBottom, playerTop, 0.3f, thenArm), "what the player saw: hit");
        }

        [Test]
        public void TheClaim_IsClamped_NeverFuture_NeverOlderThanTheCap()
        {
            Assert.AreEqual(10.0, HazardRewind.ClampClaim(10.5, 10.0, 0.35f), 1e-9, "a future claim is judged now");
            Assert.AreEqual(9.65, HazardRewind.ClampClaim(9.0, 10.0, 0.35f), 1e-6, "a very old claim is judged at the cap");
            Assert.AreEqual(9.8, HazardRewind.ClampClaim(9.8, 10.0, 0.35f), 1e-9);
        }

        [Test]
        public void ADoorsLethalEdge_IsArmedOnlyWhileClosing()
        {
            // Started closing from fully open at t = 10, 1 s of travel.
            Assert.IsFalse(SignalActuator.LethalWhileClosingAt(10.0, 1f, false, 1f, 9.9), "before the closing began");
            Assert.IsTrue(SignalActuator.LethalWhileClosingAt(10.0, 1f, false, 1f, 10.5));
            Assert.IsFalse(SignalActuator.LethalWhileClosingAt(10.0, 1f, false, 1f, 11.2), "closed");
            Assert.IsFalse(SignalActuator.LethalWhileClosingAt(10.0, 0f, true, 1f, 10.5), "opening");
        }

        [Test]
        public void AMovingPlatform_PosesAnyTime_WithTheSameFormulaItMovesBy()
        {
            var root = new GameObject("PoseFixture");
            try
            {
                var part = new GameObject("Part").transform;
                var a = new GameObject("A").transform;
                var b = new GameObject("B").transform;
                part.SetParent(root.transform);
                a.SetParent(root.transform);
                b.SetParent(root.transform);
                a.position = new Vector3(0f, 0f, 0f);
                b.position = new Vector3(0f, 4f, 0f);
                var mp = root.AddComponent<MovingPlatform>();
                var so = new SerializedObject(mp);
                so.FindProperty("platform").objectReferenceValue = part;
                so.FindProperty("waypointA").objectReferenceValue = a;
                so.FindProperty("waypointB").objectReferenceValue = b;
                so.FindProperty("speed").floatValue = 2f;
                so.FindProperty("motion").enumValueIndex = (int)Motion.Dwell;
                so.FindProperty("dwellFraction").floatValue = 0.5f;
                so.ApplyModifiedPropertiesWithoutUndo();

                // No RunManager in EditMode: section time = server time.
                foreach (double t in new[] { 0.2, 1.5, 2.5, 3.5 })
                {
                    Assert.IsTrue(mp.TryPoseAt(t, out Vector3 p, out _));
                    float u = MovingPlatform.Evaluate((float)t, 4f, 2f, 0f, Motion.Dwell, 0.5f);
                    Assert.Less((p - Vector3.Lerp(a.position, b.position, u)).magnitude, 1e-4f, $"t={t}");
                }
                Assert.IsFalse(mp.TryPoseAt(-1.0, out _, out _), "before the section began");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
