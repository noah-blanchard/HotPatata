using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// PROJECT_SPEC §15e (PatataTemple, M15): structure, rules, layout and the systems built for three, on the course itself. Its
    /// contracts are checked in EditMode (<c>CourseContractTests</c>); teleport-based tests do not certify a human clear, nor that two
    /// players fail (that is the contracts' "with two" line and the group playtest).
    /// </summary>
    public class PatataTempleTests : SandboxTestBase
    {
        protected override string SceneName => "PatataTemple";
        static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        static T Named<T>(string name) where T : Component => All<T>().Single(c => c.name == name);
        static Transform Section(string name) => All<Transform>().Single(t => t.name == name && t.parent != null && t.parent.name == "PatataTemple");

        [UnitySetUp]
        public IEnumerator DriveTheBombByHand()
        {
            Object.FindFirstObjectByType<RunManager>().enabled = false;
            yield break;
        }

        [Test]
        public void Route_ThreeActs_15Checkpoints_OneFinish_EveryContractForThree()
        {
            var checkpoints = All<Checkpoint>().OrderBy(c => c.Id).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(1, 15), checkpoints.Select(c => c.Id));
            Assert.AreEqual(1, All<FinishZone>().Length);
            Assert.AreEqual(1, All<BombController>().Length);
            var contracts = All<SectionContract>();
            Assert.AreEqual(15, contracts.Length, "one contract per section");
            foreach (var c in contracts)
            {
                string section = c.transform.parent.name;
                Assert.IsNotEmpty(c.Force, section + " says what it forces");
                Assert.AreEqual(3, c.MinPlayers, section + " is for three");
                StringAssert.StartsWith("With two:", c.TwoFail, section + " says why two fail");
            }
            Assert.AreEqual(3, All<Checkpoint>().Count(c => c.ClaimGate != null), "arches at checkpoints 5, 10 and 15");
        }

        [Test]
        public void FuseOverride_FiveFromCheckpoint8_FourAndAHalfFrom13()
        {
            foreach (var cp in All<Checkpoint>())
            {
                float expected = cp.Id >= 13 ? 4.5f : cp.Id >= 8 ? 5f : 0f;
                Assert.AreEqual(expected, cp.HoldFuseOverride, 1e-4f, "checkpoint " + cp.Id);
            }
        }

        [Test]
        public void TheSystemsForThree_AreAllThere_AndWired()
        {
            var plates = All<PressurePlate>();
            var heavy = plates.Where(p => p.RequiredBodies == 2).ToArray();
            Assert.GreaterOrEqual(heavy.Length, 5, "heavy slabs");
            Assert.IsTrue(heavy.All(p => !p.CountsCarrier), "a heavy slab never counts its carrier");
            var hourglasses = plates.Where(p => p.MemorySeconds > 0f).ToArray();
            Assert.GreaterOrEqual(hourglasses.Length, 10, "hourglass plates");
            Assert.IsTrue(hourglasses.All(p => p.GetComponents<MonoBehaviour>().Any(m => m.GetType().Name == "NetworkPressurePlate")), "an hourglass is replicated");
            Assert.GreaterOrEqual(All<SunBeam>().Length, 6, "sun beams");
            var actuators = All<SignalActuator>();
            Assert.AreEqual(3, actuators.Count(a => a.Platform != null && a.WaypointClosed.position == a.WaypointOpen.position), "three pivots");
            foreach (var a in actuators) Assert.IsNotNull(a.Source, a.name + " has its one source");
            foreach (var beam in All<SunBeam>())
                Assert.IsTrue(actuators.Any(a => ReferenceEquals(a.Source, beam)), beam.name + " drives something");
        }

        [Test]
        public void IntendedPasses_ClearCeilings_AndSolids()
        {
            Physics.SyncTransforms();
            var points = new List<Vector3>();
            var errors = new List<string>();
            Assert.GreaterOrEqual(All<PassCorridor>().Length, 15);
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
                        if (pass.Timed && (c.GetComponentInParent<MovingPlatform>() != null || c.GetComponentInParent<RotatingObstacle>() != null)) continue;
                        if (pass.Arc == PassCorridor.ArcKind.Fixed && c.GetComponentInParent<BombTransit>() != null) continue;
                        errors.Add(pass.name + " blocked by " + c.transform.parent?.name + "/" + c.name + " at " + point);
                    }
                }
            }
            Assert.IsEmpty(errors.Distinct().Take(20).ToArray());
        }

        [Test]
        public void Layout_TheAltarCrownsThePyramid_AboveEveryOtherFloor()
        {
            var pyramid = GameObject.Find("Great pyramid");
            Assert.IsNotNull(pyramid, "the great pyramid stands in the ring");
            var top = pyramid.GetComponent<Renderer>().bounds;
            var finish = All<FinishZone>().Single().transform.position;
            Assert.Less(Vector2.Distance(new Vector2(finish.x, finish.z), new Vector2(top.center.x, top.center.z)), 45f, "the altar is over the pyramid");
            var cp1 = All<Checkpoint>().Single(c => c.Id == 1).transform.position;
            Assert.Greater(finish.y, cp1.y + 25f, "the course ends far above where it starts (the well)");
            Assert.Less(top.max.y, finish.y - 10f, "the pyramid's summit stays under the altar's kill plane");
        }

        [Test]
        public void Scenery_NeverLooksLikeGroundWithinAJump()
        {
            // nothing drawn as ground or canopy comes within 6 m under a floor near it: a fall dies before it lands on scenery
            Physics.SyncTransforms();
            var errors = new List<string>();
            int course = LayerMask.GetMask("Environment", "Hazard", BodyScreen.LayerName);
            foreach (var instancer in All<FoliageInstancer>())
                foreach (var batch in instancer.Set.batches.Where(b => b.name.StartsWith("Canopy")))
                {
                    var local = batch.LocalBounds(0);
                    foreach (var cell in batch.cells)
                        for (int i = 0; i < cell.Count; i += 7)
                        {
                            var (position, _, scale) = FoliageSet.Unpack(batch, cell, i);
                            float crown = position.y + local.max.y * scale;
                            foreach (var c in Physics.OverlapBox(new Vector3(position.x, 0f, position.z), new Vector3(12f, 300f, 12f), Quaternion.identity, course, QueryTriggerInteraction.Ignore))
                                if (c.GetComponentInParent<KillZone>() == null && crown > c.bounds.min.y - 6f)
                                    errors.Add($"a crown at {position} reaches {crown:F1}, under {c.transform.parent?.name}/{c.name} at {c.bounds.min.y:F1}");
                        }
                }
            Assert.IsEmpty(errors.Distinct().Take(10).ToArray());
        }

        // ------------------------------------------------------------------ the systems on the course

        [UnityTest]
        public IEnumerator DalleLourde_TwoEmptyHandsRaiseTheBridge_TheCarrierDoesNot()
        {
            var room = Section("01 La Dalle lourde");
            var slab = room.GetComponentsInChildren<PressurePlate>().Single(p => p.RequiredBodies == 2);
            var bridge = room.GetComponentsInChildren<SignalActuator>().Single(a => ReferenceEquals(a.Source, slab));
            Vector3 c = slab.transform.position;
            yield return Place(p1, c + slab.transform.right * -0.9f + Vector3.up * 0.05f);
            yield return Place(p2, c + slab.transform.right * 0.9f + Vector3.up * 0.05f);
            Give(p1);
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(slab.Active, "the carrier does not count on the heavy slab");
            bomb.BeginReset();
            yield return WaitUntil(() => bridge.CurrentProgress > 0.9f, 4f, "two empty hands raise the bridge");
        }

        [UnityTest]
        public IEnumerator RayonsCroises_ABodyInTheWestLight_RaisesTheEastHerse()
        {
            var room = Section("08 Les Rayons croisés");
            var beam = room.GetComponentsInChildren<SunBeam>().Single(b => b.name == "West beam");
            var herse = room.GetComponentsInChildren<SignalActuator>().Single(a => ReferenceEquals(a.Source, beam));
            Assert.IsFalse(beam.Cut);
            Assert.AreEqual("East herse", herse.name, "the west light opens the east wing");
            yield return Place(p2, beam.transform.position + beam.transform.forward * 10f - Vector3.up * 1.05f);
            yield return WaitUntil(() => herse.CurrentProgress > 0.9f, 3f, "the herse rose while the light is cut");
            yield return Place(p2, beam.transform.position + beam.transform.forward * 10f + beam.transform.right * 2.5f - Vector3.up * 1.05f);
            yield return WaitUntil(() => herse.CurrentProgress < 0.1f, 3f, "and fell once the light was whole");
        }

        [UnityTest]
        public IEnumerator LaRose_ThePlateTurnsThePivotToTheDocks()
        {
            var room = Section("09 La Rose");
            var plate = room.GetComponentsInChildren<PressurePlate>().Single(p => p.name == "Rose plate");
            var pivot = room.GetComponentsInChildren<SignalActuator>().Single(a => ReferenceEquals(a.Source, plate));
            Give(p1);
            yield return Place(p2, plate.transform.position + Vector3.up * 0.05f);
            yield return WaitUntil(() => pivot.CurrentProgress > 0.4f, 8f, "an empty-handed holder turns the slab");
            var turned = pivot.Platform.rotation * Quaternion.Inverse(pivot.WaypointClosed.rotation);
            Assert.Greater(Quaternion.Angle(Quaternion.identity, turned), 10f, "it turns, it does not slide");
        }
    }
}
