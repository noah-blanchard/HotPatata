using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>ARCHITECTURE §25.2 (issue #87): the industrial test map plays like the course it copies and its look never leaks into gameplay.</summary>
    public class IndustrialLabTests : SandboxTestBase
    {
        protected override string SceneName => "IndustrialLab";
        static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None);

        [Test]
        public void Route_HasTwoCheckpoints_AFinish_AndOneBomb()
        {
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, All<Checkpoint>().Select(c => c.Id));
            Assert.AreEqual(1, All<FinishZone>().Length);
            Assert.AreEqual(1, All<BombController>().Length);
            Assert.GreaterOrEqual(All<KillZone>().Length, 1);
        }

        [Test]
        public void IntendedPasses_ClearCeilings_Solids_AndDecoration()
        {
            Physics.SyncTransforms();
            var points = new List<Vector3>();
            var errors = new List<string>();
            var decorations = All<CourseDecoration>().SelectMany(d => d.GetComponentsInChildren<Renderer>()).ToArray();
            Assert.GreaterOrEqual(All<PassCorridor>().Length, 3);
            Assert.Greater(decorations.Length, 50, "the details are drawn as decoration");
            foreach (var pass in All<PassCorridor>())
            {
                Assert.IsTrue(pass.TrySample(tuning, points), pass.name + " is unreachable with throw tuning");
                foreach (var point in points)
                {
                    if (Physics.RaycastAll(point, Vector3.up, PassCorridor.CeilingMargin, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore)
                        .Any(h => h.collider.GetComponentInParent<CourseCeiling>() != null))
                        errors.Add(pass.name + " ceiling at " + point);
                    foreach (var c in Physics.OverlapSphere(point, PassCorridor.SweepRadius, LayerMask.GetMask("Environment", "Hazard"), QueryTriggerInteraction.Ignore))
                        errors.Add(pass.name + " blocked by " + c.transform.parent.name + "/" + c.name + " at " + point);
                    foreach (var d in decorations)
                        if (Vector3.Distance(d.bounds.ClosestPoint(point), point) < PassCorridor.DecorationClearance)
                            errors.Add(pass.name + " decoration " + d.name);
                }
            }
            Assert.IsEmpty(errors.Distinct().Take(20).ToArray());
        }

        [Test]
        public void BevelledVisuals_NeverStickOutOfTheirCollider()
        {
            Physics.SyncTransforms();
            int checkedBoxes = 0;
            foreach (var skin in All<KitSkin>().Where(s => s.Shape == KitShape.BevelBox))
            {
                var collider = skin.transform.parent != null ? skin.transform.parent.GetComponent<BoxCollider>() : null;
                if (collider == null || collider.isTrigger) continue;   // decoration
                var visual = skin.GetComponent<Renderer>().bounds;
                var box = collider.bounds;
                for (int a = 0; a < 3; a++)
                {
                    Assert.LessOrEqual(visual.max[a], box.max[a] + 1e-3f, skin.name + " axis " + a);
                    Assert.GreaterOrEqual(visual.min[a], box.min[a] - 1e-3f, skin.name + " axis " + a);
                }
                checkedBoxes++;
            }
            Assert.Greater(checkedBoxes, 10);
        }

        [Test]
        public void EveryIndustrialMaterial_IsDrawn()
        {
            var used = All<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).ToHashSet();
            foreach (var name in new[] { "Industrial_Concrete", "Industrial_PaintedMetal", "Industrial_RawMetal", "Industrial_Rubber", "Industrial_PaintedMetal_Yellow", "Industrial_PaintedMetal_Safety" })
                Assert.IsTrue(used.Contains(name), name + " is not used by the lab");
        }
    }
}
