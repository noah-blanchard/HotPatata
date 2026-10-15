using System.IO;
using System.Linq;
using HotPatata.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Tests
{
    /// <summary>
    /// The authored levels' nature (ARCHITECTURE §27, M18.2): a giant tree is a trunk decks lean on and is sawn round the decks that
    /// wrap it; water has its kill zone under it; a forest keeps off walkways, decks and water and is written with the piece on save;
    /// valley walls rise at the terrain's edge only; the safety net stays under the terrain; a mist zone raises the mist inside it.
    /// Built far from the origin in the test scene, removed after; the watcher is off while they run.
    /// </summary>
    public class LevelNatureTests
    {
        const string StoreFolder = "Assets/Art/Generated/__LevelNatureTests/";
        static readonly Vector3 Origin = new Vector3(-5000f, 0f, 5000f);

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
            ground.transform.localScale = new Vector3(200f, 1f, 200f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in scene.GetRootGameObjects().Except(before)) Object.DestroyImmediate(go);
            AssetDatabase.DeleteAsset(StoreFolder.TrimEnd('/'));
            LevelPieceWatcher.Enabled = true;
        }

        T Piece<T>(string name, Vector3 offset) where T : LevelPiece
        {
            var go = new GameObject(name);
            go.transform.position = Origin + offset;
            return go.AddComponent<T>();
        }

        [Test]
        public void GiantTree_IsATrunkTheDeckLeansOn_AndIsSawnRoundIt()
        {
            var tree = Piece<HeroTreeShape>("Oak", new Vector3(0f, -0.4f, 0f));
            LevelPieceBuilder.Rebuild(tree);
            Assert.IsTrue(tree.TryTrunkAt(Origin.y + 8f, out var center, out float radius));
            Assert.Greater(radius, 1f);
            Assert.Greater(tree.Output.GetComponentsInChildren<CapsuleCollider>().Length, 4, "the trunk does not collide");
            Assert.IsTrue(tree.Output.GetComponentsInChildren<Collider>().All(c => c.gameObject.layer == LayerMask.NameToLayer("Environment")));
            Assert.AreEqual(0, LevelNature.SawnBands(tree).Count);

            var deck = Piece<TimberDeckShape>("Terrace", new Vector3(0f, 8f, 0f));
            deck.TrunkComponent = tree;
            var ring = DeckShapes.RoundTrunk(new Vector2(center.x, center.z), radius, 3f, 0f, 360f);
            deck.Outline.Clear();
            deck.Outline.AddRange(ring.Select(p => { var l = deck.Local(new Vector3(p.x, deck.transform.position.y, p.y)); return new Vector3(l.x, 0f, l.z); }));
            var all = new LevelPiece[] { tree, deck };
            Assert.AreEqual(2, LevelPieceBuilder.Refresh(all), "the tree is not sawn round the new deck");
            var band = LevelNature.SawnBands(tree).Single();
            Assert.Less(band.x, 8.4f);
            Assert.Greater(band.y, 8.4f);
            Assert.AreEqual(0, LevelPieceBuilder.Refresh(all), "rebuilt again with nothing changed");

            deck.transform.position += Vector3.up * 3f;
            Assert.AreEqual(2, LevelPieceBuilder.Refresh(all), "raising the deck re-saws the tree and rebuilds the deck");
        }

        [Test]
        public void Water_HasItsKillZoneUnderItsSurface()
        {
            var water = Piece<WaterShape>("Pond", new Vector3(0f, 0.2f, 0f));
            water.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            LevelPieceBuilder.Rebuild(water);
            var kill = water.Output.GetComponentInChildren<KillZone>();
            Assert.IsNotNull(kill, "no kill zone");
            var box = kill.GetComponent<Collider>().bounds;
            Assert.Less(box.max.y, water.transform.position.y, "the kill zone reaches over the surface");
            Assert.LessOrEqual(box.min.y, water.transform.position.y - water.Depth + 0.01f, "the kill zone does not reach the depth");
            Assert.IsNotNull(water.Output.GetComponentInChildren<NatureWater>());
            Assert.AreEqual(30f, kill.transform.eulerAngles.y, 0.01f, "the kill zone does not turn with the water");
        }

        /// <summary>A flat Unity terrain under the test area (the ground a level's forest stands on), the test's cube taken away.</summary>
        Terrain FlatTerrain()
        {
            foreach (var go in scene.GetRootGameObjects().Except(before).Where(g => g.GetComponent<BoxCollider>() != null)) Object.DestroyImmediate(go);
            var data = new TerrainData { heightmapResolution = 65, size = new Vector3(200f, 50f, 200f) };
            var terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.layer = LayerMask.NameToLayer("Environment");
            terrainGo.transform.position = Origin + new Vector3(-100f, 0f, -100f);
            Physics.SyncTransforms();
            return terrainGo.GetComponent<Terrain>();
        }

        [Test]
        public void Forest_KeepsOffWalksDecksAndWater_AndIsWrittenOnSave()
        {
            FlatTerrain();
            var forest = Piece<ForestArea>("Forest", Vector3.zero);
            var walk = Piece<TimberWalkShape>("Path", new Vector3(-30f, 0f, 0f));
            walk.Points.Clear();
            walk.Points.AddRange(new[] { Vector3.zero, new Vector3(60f, 0f, 0f) });
            walk.OnGround = true;
            var water = Piece<WaterShape>("Pond", new Vector3(10f, 0.2f, 15f));
            var pieces = new LevelPiece[] { walk, water, forest };
            LevelPieceBuilder.Refresh(pieces);

            var set = forest.Output.GetComponentInChildren<FoliageInstancer>().Set;
            Assert.Greater(set.InstanceCount(), 50, "almost nothing planted");
            var paths = LevelForest.Paths(scene);
            var waters = LevelForest.Waters(scene);
            foreach (var batch in set.batches)
                foreach (var cell in batch.cells)
                    for (int i = 0; i < cell.Count; i++)
                    {
                        var (p, _, _) = FoliageSet.Unpack(batch, cell, i);
                        Assert.IsFalse(float.IsNaN(p.y), $"{batch.name} at no height");
                        Assert.Less(Mathf.Abs(p.y - Origin.y), 1.5f, $"{batch.name} not on the ground at {p}");
                        var q = new Vector2(p.x, p.z);
                        Assert.Greater(LevelForest.Distance(paths, q), 1.5f, $"{batch.name} on the path at {p}");
                        Assert.IsFalse(LevelForest.OnWater(waters, q), $"{batch.name} in the pond at {p}");
                    }

            walk.transform.position += Vector3.forward * 5f;
            Assert.AreEqual(2, LevelPieceBuilder.Refresh(pieces), "moving the path does not replant the forest");

            Assert.IsFalse(EditorUtility.IsPersistent(forest.Output.GetComponentInChildren<FoliageInstancer>().Set));
            LevelMeshStore.Persist(pieces, StoreFolder);
            Assert.IsTrue(EditorUtility.IsPersistent(forest.Output.GetComponentInChildren<FoliageInstancer>().Set), "the planting stayed in memory");
            Assert.IsTrue(File.Exists(LevelMeshStore.PiecesFolder(StoreFolder) + forest.PieceId + "/Foliage.asset"));
        }

        [Test]
        public void ValleyWalls_RiseAtTheEdgeOnly_AndTheSafetyNetStaysUnder()
        {
            var data = new TerrainData { heightmapResolution = 129, size = new Vector3(200f, 100f, 200f) };
            var terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.transform.position = Origin + new Vector3(-100f, -10f, -100f);
            var terrain = terrainGo.GetComponent<Terrain>();
            var flat = new float[129, 129];
            for (int z = 0; z < 129; z++) for (int x = 0; x < 129; x++) flat[z, x] = 0.1f;
            data.SetHeights(0, 0, flat);
            Assert.Greater(LevelTerrain.RaiseBorder(terrain, 30f, 35f), 0);
            float H(float x, float z) => terrain.SampleHeight(terrain.transform.position + new Vector3(x, 0f, z)) + terrain.transform.position.y;
            Assert.AreEqual(Origin.y, H(100f, 100f), 0.01f, "the middle moved");
            Assert.Greater(H(2f, 100f), Origin.y + 25f, "no wall at the edge");

            var net = new GameObject("Net");
            net.AddComponent<LevelSafetyNet>();
            Assert.IsTrue(LevelTerrain.FitSafetyNet(scene));
            Assert.Less(net.transform.position.y, terrain.transform.position.y + data.bounds.min.y - 10f);
            Assert.GreaterOrEqual(net.transform.localScale.x, 200f);
        }

        [Test]
        public void MistZone_RaisesTheMistInsideAndBlendsOut()
        {
            var zone = new GameObject("Mist zone").AddComponent<MistZone>();
            zone.transform.position = Origin + Vector3.up * 3f;
            zone.Size = new Vector3(20f, 6f, 20f);
            zone.Blend = 5f;
            Assert.AreEqual(1f, zone.Weight(Origin.x, Origin.z, out float floor));
            Assert.AreEqual(Origin.y + 6f, floor, 1e-4f, "the mist is not up to the box's top");
            Assert.AreEqual(0.5f, zone.Weight(Origin.x + 12.5f, Origin.z, out _), 1e-4f);
            Assert.AreEqual(0f, zone.Weight(Origin.x + 30f, Origin.z, out _));
        }
    }
}
