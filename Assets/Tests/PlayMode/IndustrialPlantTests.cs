using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HotPatata.Tests
{
    /// <summary>ARCHITECTURE §25.2 (issue #87): the industrial plant is a closed, dark, playable map drawn only in the industrial look.</summary>
    public class IndustrialPlantTests : SandboxTestBase
    {
        protected override string SceneName => "IndustrialPlant";
        static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None);

        [Test]
        public void Route_HasThreeCheckpoints_AFinish_AKillZone_AndOneBomb()
        {
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, All<Checkpoint>().Select(c => c.Id));
            Assert.AreEqual(1, All<FinishZone>().Length);
            Assert.GreaterOrEqual(All<KillZone>().Length, 1);
            Assert.AreEqual(1, All<BombController>().Length);
            Assert.GreaterOrEqual(All<MovingPlatform>().Length, 1, "the crane");
        }

        [Test]
        public void ThePlant_IsClosed_AroundEveryPointOfTheRoute()
        {
            Physics.SyncTransforms();
            var points = All<PassCorridor>().Select(p => p.transform.position)
                .Concat(All<Checkpoint>().Select(c => c.transform.position + Vector3.up))
                .Concat(All<FinishZone>().Select(f => f.transform.position + Vector3.up)).ToList();
            Assert.GreaterOrEqual(points.Count, 8);
            int mask = LayerMask.GetMask("Environment");
            foreach (var p in points)
            {
                Assert.IsTrue(Physics.Raycast(p, Vector3.up, 40f, mask, QueryTriggerInteraction.Ignore), "open to the sky above " + p);
                Assert.IsTrue(Physics.Raycast(p, Vector3.left, 26f, mask, QueryTriggerInteraction.Ignore), "open to the west at " + p);
                Assert.IsTrue(Physics.Raycast(p, Vector3.right, 26f, mask, QueryTriggerInteraction.Ignore), "open to the east at " + p);
            }
        }

        [Test]
        public void IntendedPasses_ClearCeilings_Solids_AndDecoration()
        {
            Physics.SyncTransforms();
            var points = new List<Vector3>();
            var errors = new List<string>();
            var decorations = All<CourseDecoration>().SelectMany(d => d.GetComponentsInChildren<Renderer>()).ToArray();
            Assert.GreaterOrEqual(All<PassCorridor>().Length, 6);
            Assert.Greater(decorations.Length, 100);
            foreach (var pass in All<PassCorridor>())
            {
                Assert.IsTrue(pass.TrySample(tuning, points), pass.name + " is unreachable with throw tuning");
                foreach (var point in points)
                {
                    if (Physics.RaycastAll(point, Vector3.up, PassCorridor.CeilingMargin, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore)
                        .Any(h => h.collider.GetComponentInParent<CourseCeiling>() != null))
                        errors.Add(pass.name + " ceiling at " + point);
                    foreach (var c in Physics.OverlapSphere(point, PassCorridor.SweepRadius, LayerMask.GetMask("Environment", "Hazard"), QueryTriggerInteraction.Ignore))
                    {
                        if (pass.Timed && c.GetComponentInParent<MovingPlatform>() != null) continue;
                        errors.Add(pass.name + " blocked by " + c.transform.parent.name + "/" + c.name + " at " + point);
                    }
                    foreach (var d in decorations)
                        if (Vector3.Distance(d.bounds.ClosestPoint(point), point) < PassCorridor.DecorationClearance)
                            errors.Add(pass.name + " decoration " + d.name);
                }
            }
            Assert.IsEmpty(errors.Distinct().Take(20).ToArray());
        }

        [Test]
        public void TheLook_IsDark_WithManyWarmPracticals()
        {
            float Luma(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            Assert.Less(Luma(RenderSettings.ambientSkyColor), 0.12f);
            Assert.Less(Luma(RenderSettings.ambientEquatorColor), 0.12f);
            Assert.Less(Luma(RenderSettings.fogColor), 0.2f);
            var lights = All<Light>().Where(l => l.type == LightType.Point).ToArray();
            Assert.GreaterOrEqual(lights.Length, 25);
            Assert.GreaterOrEqual(lights.Count(l => l.color.r > l.color.b * 1.5f), 20, "warm lamps");
        }

        [Test]
        public void EverySurface_IsIndustrial_AndBevelledVisualsStayInTheirColliders()
        {
            Physics.SyncTransforms();
            var names = All<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).ToHashSet();
            foreach (var name in new[] { "Industrial_Concrete", "Industrial_PaintedMetal", "Industrial_PaintedMetal_Yellow", "Industrial_RawMetal", "Industrial_Rubber", "Industrial_Lamp", "Industrial_Glow" })
                Assert.IsTrue(names.Contains(name), name + " is not used");
            CollectionAssert.IsEmpty(names.Where(n => n.StartsWith("KayKit_") || n.StartsWith("Greybox_")).ToArray(), "no cartoon material in the plant");
            int boxes = 0;
            foreach (var skin in All<KitSkin>().Where(s => s.Shape == KitShape.BevelBox))
            {
                var collider = skin.GetComponentInParent<Collider>();
                if (collider == null) continue;
                var visual = skin.GetComponent<Renderer>().bounds;
                var box = collider.bounds;
                for (int a = 0; a < 3; a++)
                {
                    Assert.LessOrEqual(visual.max[a], box.max[a] + 1e-3f, skin.name + " axis " + a);
                    Assert.GreaterOrEqual(visual.min[a], box.min[a] - 1e-3f, skin.name + " axis " + a);
                }
                boxes++;
            }
            Assert.Greater(boxes, 30);
        }
    }
}
