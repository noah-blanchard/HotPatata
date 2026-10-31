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
    /// Stone (M21, ARCHITECTURE §27.7): a dry-stone wall freezes per stone under the limit and only per segment over it, its stones jitter
    /// and set down, its masonry walls collide and Open Here cuts them; the stone ways are walked (a stair, raised paving, stepping stones,
    /// a stone bridge, a walkway on stone piers); a broken column, a tower with floors, a roof and a kit door, an arch carrying timber.
    /// Built far from the origin, removed after; the watcher is off while they run.
    /// </summary>
    public class LevelStoneTests
    {
        const string StoreFolder = "Assets/Art/Generated/__LevelStoneTests/";
        static readonly Vector3 Origin = new Vector3(-6500f, 0f, -6500f);

        Scene scene;
        GameObject[] before;

        [SetUp]
        public void SetUp()
        {
            LevelPieceWatcher.Enabled = false;
            MeshCatalog.Folder = StoreFolder + "Catalog/";
            scene = SceneManager.GetActiveScene();
            before = scene.GetRootGameObjects();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.layer = LayerMask.NameToLayer("Environment");
            ground.transform.position = Origin + Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(400f, 1f, 400f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in scene.GetRootGameObjects().Except(before)) Object.DestroyImmediate(go);
            Undo.ClearAll();
            MeshCatalog.Folder = MeshCatalog.DefaultFolder;
            AssetDatabase.DeleteAsset(StoreFolder.TrimEnd('/'));
            LevelPieceWatcher.Enabled = true;
        }

        T Piece<T>(string name, Vector3 offset) where T : LevelPiece
        {
            var go = new GameObject(name);
            go.transform.position = Origin + offset;
            var piece = go.AddComponent<T>();
            piece.EnsureId();
            return piece;
        }

        static int Env => LayerMask.GetMask("Environment");

        static bool Hit(Vector3 from, Vector3 dir, float reach, out RaycastHit hit) =>
            Physics.Raycast(from, dir, out hit, reach, Env, QueryTriggerInteraction.Ignore);

        WallShape DryStone(string name, Vector3 offset, float length)
        {
            var wall = Piece<WallShape>(name, offset);
            WallEntries.Dress(wall, WallStyle.DryStone);
            wall.Points.Clear();
            wall.Points.AddRange(new[] { new Vector3(-length * 0.5f, 0f, 0f), new Vector3(length * 0.5f, 0f, 0f) });
            wall.Height = 2f;
            LevelPieceBuilder.Rebuild(wall);
            return wall;
        }

        [Test]
        public void DryStoneWall_FreezesPerStone_ItsWallsCollide_AndOpenHereCutsThem()
        {
            var wall = DryStone("Frozen wall", Vector3.zero, 24f);
            var frozen = TimberFreezer.Freeze(wall);
            Assert.IsNotNull(frozen, "the wall did not freeze");
            Physics.SyncTransforms();
            var stones = frozen.GetComponentsInChildren<StoneBlock>(true);
            Assert.That(stones.Length, Is.InRange(150, StoneFreeze.StoneFreezeLimit), "not a stone per stone");
            Assert.IsTrue(stones.All(s => s.GetComponent<MeshFilter>()?.sharedMesh != null), "a stone is not drawn");
            Assert.Less(stones.Select(s => s.GetComponent<MeshFilter>().sharedMesh).Distinct().Count(), stones.Length / 2, "the stones do not share their meshes");
            Assert.IsNotEmpty(frozen.GetComponentsInChildren<MasonryWall>(true), "no masonry wall");
            Assert.IsTrue(Hit(Origin + new Vector3(4f, 1f, -3f), Vector3.forward, 6f, out _), "the frozen wall does not stop a body");

            // the element tools work on stones
            var some = stones.Take(20).Cast<TimberElement>().ToList();
            var at = some.Select(s => s.transform.position).ToList();
            TimberElementTools.Jitter(some, 0.02f, 2f, 3);
            Assert.IsTrue(some.Select((s, i) => (s.transform.position - at[i]).magnitude).Any(d => d > 1e-4f), "jitter did not move a stone");

            // open here: the stones round x = -4 under 1.5 m taken out, the wall cut round them
            var taken = stones.Where(s => Mathf.Abs(s.transform.position.x - (Origin.x - 4f)) < 0.7f && s.transform.position.y - Origin.y < 1.5f).ToList();
            Assert.IsNotEmpty(taken);
            int cut = StoneFreeze.OpenHere(taken);
            Physics.SyncTransforms();
            Assert.Greater(cut, 0, "no wall was cut");
            Assert.IsFalse(Hit(Origin + new Vector3(-4f, 0.6f, -3f), Vector3.forward, 6f, out var shut) && shut.collider.GetComponentInParent<MasonryWall>() != null,
                           "the opening is still shut");
            Assert.IsTrue(Hit(Origin + new Vector3(-4f, 1.85f, -3f), Vector3.forward, 6f, out _), "the wall over the opening is gone");
        }

        [Test]
        public void ALongWall_FreezesOnlyPerSegment()
        {
            var wall = DryStone("Long wall", new Vector3(0f, 0f, 30f), 80f);
            Assert.IsNull(TimberFreezer.Freeze(wall), "a wall of more stones than the limit froze per stone");
            var frozen = TimberFreezer.Freeze(wall, true);
            Assert.IsNotNull(frozen);
            Assert.IsEmpty(frozen.GetComponentsInChildren<StoneBlock>(true), "per segment makes no stone elements");
            Assert.IsNotEmpty(frozen.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Run")), "no run meshes");
            Assert.IsNotEmpty(frozen.GetComponentsInChildren<MasonryWall>(true));
        }

        static List<string> Walk(IList<Vector3> line, string name) =>
            WalkProbe.Run(new (string, IList<Vector3>)[] { (name, line) }).Where(p => !p.message.Contains("past its")).Select(p => p.message).ToList();

        [Test]
        public void StoneWays_AreWalked()
        {
            var stair = Piece<StoneStairShape>("Stair", new Vector3(0f, 0f, 70f));
            stair.Kind = StoneStairKind.Built;
            LevelPieceBuilder.Rebuild(stair);
            var line = stair.Output.GetComponent<LevelPieceOutput>().WalkLine;
            Assert.Greater(line.Length, 2);
            Assert.AreEqual(2.4f, line.Last().y - line.First().y, 0.05f, "the stair does not climb its rise");
            Assert.IsEmpty(Walk(line, "stair"));

            var paving = Piece<PavingShape>("Terrace", new Vector3(20f, 0f, 70f));
            paving.Raised = 1.5f;
            LevelPieceBuilder.Rebuild(paving);
            Physics.SyncTransforms();
            Assert.IsTrue(Hit(paving.transform.position + Vector3.up * 4f, Vector3.down, 6f, out var top) && top.collider.GetComponent<WalkableProxy>() != null, "the terrace is not walked");
            Assert.AreEqual(paving.transform.position.y + 1.5f, top.point.y, 0.02f);
            Assert.IsTrue(Hit(paving.transform.position + new Vector3(-8f, 0.7f, 0f), Vector3.right, 6f, out _), "the terrace's edge does not stop a body");

            var stepping = Piece<SteppingStonesShape>("Stepping", new Vector3(40f, 0f, 70f));
            LevelPieceBuilder.Rebuild(stepping);
            Physics.SyncTransforms();
            var tops = stepping.Output.GetComponent<LevelPieceOutput>().WalkLine;
            Assert.GreaterOrEqual(tops.Length, 5);
            foreach (var p in tops)
                Assert.IsTrue(Hit(p + Vector3.up * 1f, Vector3.down, 1.3f, out var stone) && Mathf.Abs(stone.point.y - p.y) < 0.1f, $"no stone to stand on at {p - Origin}");
            for (int i = 1; i < tops.Length; i++) Assert.LessOrEqual(Vector3.Distance(tops[i], tops[i - 1]), 2.5f + 2f * stepping.Jitter + 0.01f, "a gap too wide to jump");

            var bridge = Piece<StoneBridgeShape>("Bridge", new Vector3(60f, 0f, 70f));
            bridge.Start = new Vector3(0f, 2f, -10f);
            bridge.End = new Vector3(0f, 2f, 10f);
            LevelPieceBuilder.Rebuild(bridge);
            Physics.SyncTransforms();
            var deck = bridge.Output.GetComponent<LevelPieceOutput>().WalkLine;
            Assert.IsEmpty(Walk(deck, "bridge"));
            Assert.IsTrue(Hit(deck[16] + Vector3.up * 0.5f + Vector3.left * 3f, Vector3.right, 3f, out _), "no parapet");

            var walk = Piece<TimberWalkShape>("Pier walk", new Vector3(80f, 3f, 70f));
            walk.Support = TimberSupport.StonePiers;
            LevelPieceBuilder.Rebuild(walk);
            Assert.IsTrue(walk.Output.GetComponentsInChildren<BoxCollider>(true).Any(b => b.name.Contains("pier")), "the walkway has no stone pier");
        }

        [Test]
        public void Column_Tower_AndArch_CollideAsSpecified()
        {
            var column = Piece<ColumnShape>("Column", new Vector3(0f, 0f, 120f));
            column.Broken = 0.5f;
            LevelPieceBuilder.Rebuild(column);
            var shaft = column.Output.GetComponentsInChildren<BoxCollider>(true).First(b => b.name.EndsWith("shaft"));
            Assert.Less(shaft.bounds.max.y - column.transform.position.y, column.Height * 0.8f, "a broken column stands whole");
            Assert.IsNotEmpty(column.Output.GetComponentsInChildren<BoxCollider>(true).Where(b => b.name.Contains("drum")), "no fallen drums");

            var tower = Piece<TowerShape>("Tower", new Vector3(30f, 0f, 120f));
            tower.Top = TowerTop.ConeRoof;
            tower.Floors.Clear();
            tower.Floors.AddRange(new[] { 3.5f, 7f });
            tower.Openings.Add(new WallOpening(2f, 2.2f, 2.8f, OpeningPassage.KitDoor));
            LevelPieceBuilder.Rebuild(tower);
            Physics.SyncTransforms();
            foreach (float h in tower.Floors)
                Assert.IsTrue(Hit(tower.transform.position + new Vector3(-1f, h + 1f, 0f), Vector3.down, 1.5f, out var floor) && Mathf.Abs(floor.point.y - tower.transform.position.y - h) < 0.05f,
                              $"no floor at {h} m");
            Assert.IsNotNull(tower.Output.GetComponentInChildren<CourseCeiling>(true), "the roof is not a ceiling");
            Assert.IsNotNull(tower.Openings[0].kit?.GetComponent<SignalActuator>(), "the tower's door has no kit");

            var arch = Piece<ArchShape>("Arch", new Vector3(60f, 0f, 120f));
            LevelPieceBuilder.Rebuild(arch);
            Physics.SyncTransforms();
            Assert.IsFalse(Hit(arch.transform.position + new Vector3(0f, 1f, -3f), Vector3.forward, 6f, out _), "the passage under the arch is shut");
            float top = SceneSite.GroundAt(new Vector2(arch.transform.position.x, arch.transform.position.z), arch.transform.position.y + 10f, 12f);
            Assert.Greater(top - arch.transform.position.y, arch.PierHeight + arch.Rise, "timber finds no top on the arch");
            Assert.IsTrue(Hit(arch.transform.position + new Vector3(-(arch.Span * 0.5f + arch.PierWidth * 0.5f), 1f, -3f), Vector3.forward, 6f, out _), "a pier does not collide");
        }
    }
}
