using System.Collections.Generic;
using System.Linq;
using HotPatata.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Tests
{
    /// <summary>
    /// The authored levels' game tools (ARCHITECTURE §27, M18.3): checkpoints numbered along the walk from the start, a contract's section,
    /// the wiring described with its hold, the walk probe finding what blocks a walkway, and the kit's resize functions reading back
    /// what the size handles read. Built far from the origin in the test scene, removed after; the watcher is off while they run.
    /// </summary>
    public class LevelGameTests
    {
        static readonly Vector3 Origin = new Vector3(5000f, 0f, -5000f);

        Scene scene;
        GameObject[] before;

        [SetUp]
        public void SetUp()
        {
            LevelPieceWatcher.Enabled = false;
            scene = SceneManager.GetActiveScene();
            before = scene.GetRootGameObjects();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.layer = LayerMask.NameToLayer("Environment");
            ground.transform.position = Origin + Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(300f, 1f, 300f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in scene.GetRootGameObjects().Except(before)) Object.DestroyImmediate(go);
            LevelPieceWatcher.Enabled = true;
        }

        T Piece<T>(string name, Vector3 offset) where T : LevelPiece
        {
            var go = new GameObject(name);
            go.transform.position = Origin + offset;
            return go.AddComponent<T>();
        }

        [Test]
        public void WalkDistances_FollowTheStairNotTheStraightLine()
        {
            // a deck 6 m up right above the start's side, reached only by a stair that starts 30 m away
            var deck = Piece<TimberDeckShape>("Deck", new Vector3(0f, 6f, 8f));
            var stair = Piece<TimberWalkShape>("Stair", new Vector3(30f, 0f, 8f));
            stair.Points.Clear();
            stair.Points.AddRange(new[] { Vector3.zero, new Vector3(-4f, 0f, 0f), new Vector3(-24f, 6f, 0f), new Vector3(-26f, 6f, 0f) });
            stair.End = new TimberAttach { deck = deck, along = deck.NearestAlong(Origin + new Vector3(3f, 6f, 8f), out _) };
            LevelPieceBuilder.Refresh(new LevelPiece[] { deck, stair });
            var targets = new List<Vector3> { Origin + new Vector3(0f, 6f, 8f), Origin + new Vector3(10f, 0f, -20f) };
            var d = LevelCourse.WalkDistances(scene, Origin, targets);
            Assert.IsFalse(float.IsInfinity(d[0]), "the deck is not reached by its stair");
            Assert.Greater(d[0], 45f, "the deck was reached in a straight line, not by the stair 30 m away");
            Assert.Less(d[1], 30f, "a point on the ground nearby is far");
        }

        [Test]
        public void ContractSection_IsItsCheckpoints()
        {
            var cpGo = new GameObject("CP");
            var cp = cpGo.AddComponent<Checkpoint>();
            var so = new SerializedObject(cp);
            so.FindProperty("id").intValue = 3;
            so.ApplyModifiedPropertiesWithoutUndo();
            var under = LevelCourse.AddContract(cpGo.transform, 2);
            Assert.AreEqual(3, LevelCourse.SectionOf(under));
            var level = new GameObject("Level");
            var start = LevelCourse.AddContract(level.transform, 3);
            Assert.AreEqual(0, LevelCourse.SectionOf(start));
            Assert.AreEqual(3, start.MinPlayers);
            Assert.IsFalse(string.IsNullOrEmpty(start.TwoFail), "a section for three says why two fail");
        }

        [Test]
        public void Wiring_SaysWhatDrivesAndHowLongItHolds()
        {
            var plate = CourseKit.Place(null, BombObstacleKitBuilder.Plate, "Plate", Origin, Quaternion.identity).GetComponent<PressurePlate>();
            Assert.AreEqual("plate, while held", KitSceneTools.Describe(plate));
            var hourglass = CourseKit.Place(null, BombObstacleKitBuilder.PlateHourglass, "Hourglass", Origin, Quaternion.identity).GetComponent<PressurePlate>();
            StringAssert.StartsWith("hourglass plate, holds", KitSceneTools.Describe(hourglass));
            var ring = CourseKit.Place(null, BombObstacleKitBuilder.GateRing, "Ring", Origin, Quaternion.identity).GetComponent<BombGate>();
            Assert.AreEqual($"hoop, holds {ring.HoldSeconds:0.#} s", KitSceneTools.Describe(ring));
        }

        [Test]
        public void ResizedKit_ReadsBackItsSize()
        {
            var door = CourseKit.Place(null, BombObstacleKitBuilder.Door, "Door", Origin, Quaternion.identity);
            BombObstacleKitBuilder.ResizeActuator(door, new Vector3(5f, 3f, 0.5f), new Vector3(0f, 3.2f, 0f));
            var actuator = door.GetComponent<SignalActuator>();
            var box = actuator.Platform.GetComponentsInChildren<BoxCollider>(true).First(c => !c.isTrigger);
            Assert.AreEqual(new Vector3(5f, 3f, 0.5f), box.size);
            Assert.AreEqual(new Vector3(0f, 3.2f, 0f), actuator.WaypointOpen.localPosition - actuator.WaypointClosed.localPosition);
            var plate = CourseKit.Place(null, BombObstacleKitBuilder.Plate, "Plate", Origin, Quaternion.identity);
            BombObstacleKitBuilder.ResizePlate(plate, 3f);
            Assert.AreEqual(3f, plate.GetComponent<BoxCollider>().size.x, 1e-4f);
        }

        [Test]
        public void WalkProbe_FindsWhatStandsInAWalk()
        {
            var walk = new List<Vector3> { Origin + new Vector3(0f, 0f, 30f), Origin + new Vector3(10f, 0f, 30f) };
            Assert.IsEmpty(WalkProbe.Run(new[] { ("Clear walk", (IList<Vector3>)walk) }).Where(p => p.message.Contains("in the way")));
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.layer = LayerMask.NameToLayer("Environment");
            post.transform.position = Origin + new Vector3(5f, 1f, 30f);
            Physics.SyncTransforms();
            var problems = WalkProbe.Run(new[] { ("Blocked walk", (IList<Vector3>)walk) });
            Assert.IsTrue(problems.Any(p => p.message.Contains("in the way") && p.blocker != null && p.blocker.gameObject == post), "the post in the walk is not found");
        }

        [Test]
        public void Checkpoint_ItsCampfireStandsBesideItOnItsFloor_AndTheZoneFollowsIt()
        {
            // a deck 6 m up, 8 m by 12 m: the checkpoint near its east edge, so its right (east) has no floor
            var deck = Piece<TimberDeckShape>("Deck", new Vector3(0f, 6f, 40f));
            deck.Outline.Clear();
            deck.Outline.AddRange(DeckShapes.Rectangle(8f, 12f));
            LevelPieceBuilder.Refresh(new LevelPiece[] { deck });
            Physics.SyncTransforms();
            var parent = new GameObject("Kit").transform;
            var cp = LevelCourse.AddCheckpoint(scene, parent, Origin + new Vector3(2f, 6f, 40f));
            var fire = LevelCourse.FireOf(cp);
            Assert.IsNotNull(fire, "no campfire");
            Assert.AreEqual(cp.transform, fire.transform.parent, "the campfire moves with its checkpoint");
            var off = fire.transform.position - cp.transform.position;
            Assert.That(new Vector2(off.x, off.z).magnitude, Is.InRange(3f, 6f), "the campfire stands beside the spawns");
            Assert.AreEqual(0f, off.y, 0.01f, "the campfire is on the checkpoint's floor");
            Assert.IsTrue(Physics.Raycast(fire.transform.position + Vector3.up, Vector3.down, 1.5f, LayerMask.GetMask("Environment")),
                          $"the campfire stands off the deck at {fire.transform.position - Origin}");

            var trigger = (BoxCollider)new SerializedObject(cp).FindProperty("trigger").objectReferenceValue;
            Physics.SyncTransforms();
            Assert.IsTrue(trigger.bounds.Contains(fire.transform.position + Vector3.up * 0.5f), "the zone covers the campfire");
            Assert.Less(trigger.bounds.size.y, 3.5f, "the zone is a man's height, not a tower");

            // moved by hand: the zone follows it, and shrinks back from where it was
            fire.transform.position = cp.transform.position + new Vector3(0f, 0f, 4.5f);
            Assert.IsTrue(LevelCourse.FitZone(cp, false));
            Physics.SyncTransforms();
            Assert.IsTrue(trigger.bounds.Contains(fire.transform.position + Vector3.up * 0.5f), "the zone does not follow the campfire");
            Assert.Less(trigger.bounds.max.x - cp.transform.position.x, 2.5f, "the zone still reaches where the campfire was");
        }

        [Test]
        public void Verify_FindsACheckpointWhoseSpawnsAreInARailOrInTheAir()
        {
            var parent = new GameObject("Kit").transform;
            List<LevelFinding> Findings(Checkpoint cp)
            {
                Physics.SyncTransforms();
                var all = new List<LevelFinding>();
                LevelVerifier.Spawns(scene, all);
                return all.Where(f => f.context == cp.gameObject).ToList();
            }
            var clear = LevelCourse.AddCheckpoint(scene, parent, Origin + new Vector3(-20f, 0f, 0f));
            Assert.IsEmpty(Findings(clear), "a checkpoint in the open is fine");
            var railed = LevelCourse.AddCheckpoint(scene, parent, Origin + new Vector3(20f, 0f, 0f));
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.layer = LayerMask.NameToLayer("Environment");
            rail.transform.position = railed.GetComponentsInChildren<PlayerSpawn>()[0].transform.position + Vector3.up;
            rail.transform.localScale = new Vector3(0.2f, 1.2f, 3f);
            Assert.IsTrue(Findings(railed).Any(f => f.severity == LevelFinding.Severity.Error && f.message.Contains("stands in")), "a spawn in a rail is not found");
            var floating = LevelCourse.AddCheckpoint(scene, parent, Origin + new Vector3(0f, 6f, 40f));
            Assert.IsTrue(Findings(floating).Any(f => f.message.Contains("stands on nothing")), "a spawn in the air is not found");
        }
    }
}
