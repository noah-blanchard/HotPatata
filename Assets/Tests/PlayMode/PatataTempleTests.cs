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

        [Test]
        public void ScannedProps_KeepClearOfPasses_StayInTheirBoxes_StatuesAreSolid()
        {
            Physics.SyncTransforms();
            var errors = new List<string>();
            foreach (var d in All<CourseDecoration>())
                Assert.IsEmpty(d.GetComponentsInChildren<Collider>().Select(c => c.name).ToArray(), d.name + " never collides");
            // the scanned props (reliefs, offerings, roof combs, colossi): never within the decoration clearance of an intended pass
            // (the stepped pyramid keeps its own rule: 12 m under every floor near it)
            var decorations = All<CourseDecoration>().Where(d => d.transform.parent.name == "Temple props" || d.name == "Colossi")
                .SelectMany(d => d.GetComponentsInChildren<MeshRenderer>()).ToArray();
            Assert.Greater(decorations.Length, 400, "the temple's reliefs, offerings, roof combs and colossi");
            var points = new List<Vector3>();
            foreach (var pass in All<PassCorridor>())
            {
                if (!pass.TrySample(tuning, points)) continue;
                var arc = new Bounds(points[0], Vector3.zero);
                foreach (var p in points) arc.Encapsulate(p);
                arc.Expand(2f * PassCorridor.DecorationClearance);
                foreach (var r in decorations.Where(r => r.bounds.Intersects(arc)))
                    foreach (var p in points)
                        if (Vector3.Distance(ClosestOnBox(r, p), p) < PassCorridor.DecorationClearance) { errors.Add(pass.name + " near " + r.transform.parent?.name); break; }
            }
            // a mover drawn as carved slabs: every slab inside the platform's collider (its top flush, nothing drawn where nothing collides)
            var carved = All<CustomVisual>().Where(v => v.name == "Carved stone").ToArray();
            Assert.GreaterOrEqual(carved.Length, 10, "the shuttle, the ford stones, the lift, the bridges and the pivots are carved");
            foreach (var v in carved)
            {
                var box = v.transform.parent.GetComponentsInChildren<BoxCollider>().Where(c => !c.isTrigger && !CustomVisual.Covers(c))
                    .OrderByDescending(c => c.bounds.size.x * c.bounds.size.z).First();
                foreach (var r in v.GetComponentsInChildren<MeshRenderer>())
                    foreach (var corner in Corners(r))
                    {
                        var local = box.transform.InverseTransformPoint(corner) - box.center;
                        var half = box.size / 2f + Vector3.one * 0.02f;
                        if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z) { errors.Add(v.transform.parent.name + " drawn outside its collider"); break; }
                    }
            }
            // a statue looks like it can be climbed: it is solid
            var statues = All<Transform>().Where(t => t.name == "Statue box").ToArray();
            Assert.GreaterOrEqual(statues.Length, 10, "statues round the courts");
            foreach (var s in statues)
            {
                var c = s.GetComponent<BoxCollider>();
                Assert.IsNotNull(c, s.parent.name);
                Assert.AreEqual(LayerMask.NameToLayer("Environment"), s.gameObject.layer, s.parent.name);
                Assert.IsFalse(c.isTrigger, s.parent.name);
            }
            Assert.IsEmpty(errors.Distinct().Take(15).ToArray());
        }

        /// <summary>The point of a renderer's own box nearest <paramref name="p"/> (exact for scaled and quarter-turned scans).</summary>
        static Vector3 ClosestOnBox(Renderer r, Vector3 p)
        {
            var b = r.localBounds;
            var local = r.transform.InverseTransformPoint(p);
            return r.transform.TransformPoint(new Vector3(Mathf.Clamp(local.x, b.min.x, b.max.x), Mathf.Clamp(local.y, b.min.y, b.max.y), Mathf.Clamp(local.z, b.min.z, b.max.z)));
        }

        static IEnumerable<Vector3> Corners(Renderer r)
        {
            var b = r.localBounds;
            for (int c = 0; c < 8; c++)
                yield return r.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
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
