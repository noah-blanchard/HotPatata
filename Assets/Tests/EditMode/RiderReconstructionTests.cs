using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>
    /// A remote rider is drawn at the owner's offset from the carrier as this machine draws it
    /// (docs/netcode-deterministic-plan.md §2.3): glued while riding, blended when boarding or leaving, snapped on a teleport.
    /// </summary>
    public class RiderReconstructionTests
    {
        const float Dt = 1f / 60f, Blend = 0.15f, Smoothing = 15f, Snap = 3f;
        const int Carrier = 42;

        static Vector3 Step(RiderReconstruction r, Vector3 world, Vector3 anchor, int carrier, Vector3 offset)
        {
            r.Retarget(carrier, offset, Dt, Smoothing);
            return r.Blend(world, anchor, carrier != 0 && carrier == r.CarrierId, Dt, Blend, Snap);
        }

        [Test]
        public void Riding_FollowsTheLocalCarrier_WhateverTheLaggingWorldPosition()
        {
            var r = new RiderReconstruction();
            var offset = new Vector3(0.5f, 1f, 0f);
            Vector3 p = default;
            for (int i = 0; i < 60; i++)   // a second: the board blend is over
            {
                var anchor = new Vector3(0f, i * 0.05f, 0f);           // an elevator going up
                var laggingWorld = anchor + offset - Vector3.up * 0.4f;  // the replicated position, 0.4 m late
                p = Step(r, laggingWorld, anchor, Carrier, offset);
            }
            var lastAnchor = new Vector3(0f, 59 * 0.05f, 0f);
            Assert.AreEqual(1f, r.Weight, 1e-5f);
            Assert.Less((p - (lastAnchor + offset)).magnitude, 1e-4f, "glued to the platform as drawn here");
        }

        [Test]
        public void Boarding_BlendsOverTheBlendTime()
        {
            var r = new RiderReconstruction();
            var world = new Vector3(0f, 0.4f, 0f);
            var anchor = Vector3.zero;
            var offset = Vector3.up;
            var first = Step(r, world, anchor, Carrier, offset);
            Assert.Less((first - world).magnitude, 0.05f, "the first frame barely moves: no pop");
            int frames = Mathf.CeilToInt(Blend / Dt);
            Vector3 p = first;
            for (int i = 1; i <= frames; i++) p = Step(r, world, anchor, Carrier, offset);
            Assert.AreEqual(1f, r.Weight, 1e-5f);
            Assert.Less((p - (anchor + offset)).magnitude, 1e-4f);
        }

        [Test]
        public void Offset_IsSmoothed_NotSnapped_WhileRidingTheSameCarrier()
        {
            var r = new RiderReconstruction();
            Step(r, Vector3.zero, Vector3.zero, Carrier, Vector3.zero);
            Step(r, Vector3.zero, Vector3.zero, Carrier, new Vector3(1f, 0f, 0f));
            Assert.Greater(r.Offset.x, 0f);
            Assert.Less(r.Offset.x, 0.5f, "one frame closes only part of a step");
            for (int i = 0; i < 120; i++) Step(r, Vector3.zero, Vector3.zero, Carrier, new Vector3(1f, 0f, 0f));
            Assert.AreEqual(1f, r.Offset.x, 1e-3f);
        }

        [Test]
        public void Leaving_BlendsBackToTheWorldPosition_ThenLetsGo()
        {
            var r = new RiderReconstruction();
            var anchor = Vector3.zero;
            var offset = Vector3.up;
            for (int i = 0; i < 30; i++) Step(r, Vector3.up, anchor, Carrier, offset);
            var world = new Vector3(0.6f, 1.2f, 0f);   // jumped off
            var p = Step(r, world, anchor, 0, Vector3.zero);
            Assert.Less((p - (anchor + offset)).magnitude, 0.05f, "the first frame barely moves: no pop");
            for (int i = 0; i < 30; i++) p = Step(r, world, anchor, 0, Vector3.zero);
            Assert.IsFalse(r.Active);
            Assert.AreEqual(world, p);
        }

        [Test]
        public void ATeleport_SnapsInsteadOfSweepingAcrossTheLevel()
        {
            var r = new RiderReconstruction();
            for (int i = 0; i < 30; i++) Step(r, Vector3.up, Vector3.zero, Carrier, Vector3.up);
            var respawn = new Vector3(40f, 0f, 0f);
            var p = Step(r, respawn, Vector3.zero, 0, Vector3.zero);
            Assert.AreEqual(respawn, p);
            Assert.IsFalse(r.Active);
        }
    }
}
