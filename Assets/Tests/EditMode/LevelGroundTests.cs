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
    /// The layered ground (ARCHITECTURE §27.4, docs/LEVEL_AUTHORING.md §26): the eight layers in their order and the ground material; the
    /// rules' bake (steep slopes are rock, shores mud, dry hollows litter, walk lines trail); a re-bake keeps what was painted by hand,
    /// and Reset Painted gives it back to the rules; resampling a terrain to another fineness keeps its heights at the old samples, its
    /// trees, details and holes, and Undo restores it; the shader fogs through HP_MixFog in its four passes. A small terrain made in the
    /// test runner's scene, its data under a test folder, removed after.
    /// </summary>
    public class LevelGroundTests
    {
        const string Folder = "Assets/Art/Generated/__LevelGroundTests/";
        static readonly Vector3 Origin = new Vector3(-7000f, 0f, -7000f);
        const float Size = 128f, Height = 60f;

        Scene scene;
        GameObject[] before;
        Terrain terrain;

        [SetUp]
        public void SetUp()
        {
            LevelPieceWatcher.Enabled = false;
            MeshCatalog.Folder = Folder + "Catalog/";
            scene = SceneManager.GetActiveScene();
            before = scene.GetRootGameObjects();
            Directory.CreateDirectory(Folder + "Terrain");
            AssetDatabase.Refresh();
            var data = new TerrainData { heightmapResolution = 129 };
            data.size = new Vector3(Size, Height, Size);
            AssetDatabase.CreateAsset(data, Folder + "Terrain/Test_Terrain.asset");
            var go = Terrain.CreateTerrainGameObject(data);
            go.layer = LayerMask.NameToLayer("Environment");
            go.transform.position = Origin;
            terrain = go.GetComponent<Terrain>();
            Flat(10f);
            LevelTerrain.MakeLayered(terrain);
            terrain.materialTemplate = GroundLayerBuilder.Material();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in scene.GetRootGameObjects().Except(before)) Object.DestroyImmediate(go);
            Undo.ClearAll();
            MeshCatalog.Folder = MeshCatalog.DefaultFolder;
            AssetDatabase.DeleteAsset(Folder.TrimEnd('/'));
            LevelPieceWatcher.Enabled = true;
        }

        /// <summary>The whole terrain at <paramref name="y"/> m over its origin.</summary>
        void Flat(float y)
        {
            var d = terrain.terrainData;
            int r = d.heightmapResolution;
            var h = new float[r, r];
            for (int z = 0; z < r; z++)
                for (int x = 0; x < r; x++) h[z, x] = y / Height;
            d.SetHeights(0, 0, h);
        }

        /// <summary>Sets the heights from a function of the world (x, z) → metres over the origin.</summary>
        void Shape(System.Func<float, float, float> f)
        {
            var d = terrain.terrainData;
            int r = d.heightmapResolution;
            var h = new float[r, r];
            for (int z = 0; z < r; z++)
                for (int x = 0; x < r; x++)
                    h[z, x] = Mathf.Clamp01(f(Origin.x + x * Size / (r - 1), Origin.z + z * Size / (r - 1)) / Height);
            d.SetHeights(0, 0, h);
            Physics.SyncTransforms();
        }

        /// <summary>The strongest layer at a world (x, z).</summary>
        int Dominant(float wx, float wz)
        {
            var d = terrain.terrainData;
            int res = d.alphamapResolution;
            int x = Mathf.RoundToInt((wx - Origin.x) / Size * (res - 1)), z = Mathf.RoundToInt((wz - Origin.z) / Size * (res - 1));
            var m = d.GetAlphamaps(x, z, 1, 1);
            int best = 0;
            for (int l = 1; l < GroundBaker.Layers; l++)
                if (m[0, 0, l] > m[0, 0, best]) best = l;
            return best;
        }

        // ------------------------------------------------------------------ layers

        [Test]
        public void Layers_AreTheGroundListInOrder_AndTheMaterialDrawsThem()
        {
            Assert.IsTrue(GroundBaker.IsLayered(terrain), "the terrain is not layered");
            var layers = terrain.terrainData.terrainLayers;
            Assert.AreEqual(8, layers.Length);
            for (int i = 0; i < 8; i++) StringAssert.StartsWith($"{i} {GroundLayerBuilder.Layers[i].Name}", layers[i].name);
            Assert.AreEqual(GroundLayerBuilder.ShaderName, terrain.materialTemplate.shader.name);
            var albedo = (Texture2DArray)terrain.materialTemplate.GetTexture("_GroundAlbedo");
            Assert.IsNotNull(albedo, "no albedo array");
            Assert.AreEqual(8, albedo.depth);
            Assert.IsNotNull(terrain.GetComponent<GroundSplat>(), "no GroundSplat binds the control maps");
            Assert.AreEqual(terrain.terrainData.heightmapResolution - 1, terrain.terrainData.alphamapResolution, "the splat is not as fine as the heights");
        }

        [Test]
        public void Shader_FogsThroughMixFog_InFourPasses()
        {
            string text = File.ReadAllText("Assets/Art/Shaders/HotPatataNatureGround.shader");
            StringAssert.Contains("HP_MixFog(", text);
            foreach (var pass in new[] { "UniversalForward", "ShadowCaster", "DepthOnly", "DepthNormals" }) StringAssert.Contains($"\"LightMode\" = \"{pass}\"", text);
            Assert.IsFalse(ShaderUtil.ShaderHasError(Shader.Find(GroundLayerBuilder.ShaderName)), "the ground shader has errors");
        }

        // ------------------------------------------------------------------ the rules

        [Test]
        public void Bake_RockOnSteepSlopes_MudOnShores_LitterInHollows_TrailOnWalks()
        {
            // a flat floor at 10 m, a 60° bank to the east, a dry hollow to the north-west, a pond to the south-west
            Vector2 hollow = new Vector2(Origin.x + 30f, Origin.z + 95f), pond = new Vector2(Origin.x + 30f, Origin.z + 30f);
            Shape((x, z) =>
            {
                float h = 10f;
                float east = x - (Origin.x + 90f);
                if (east > 0f) h += Mathf.Min(east * 1.75f, 30f);
                float dh = Vector2.Distance(new Vector2(x, z), hollow);
                h -= 2.5f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - dh / 14f));
                float dp = Vector2.Distance(new Vector2(x, z), pond);
                h -= 3f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1.4f - dp / 12f));
                return h;
            });
            var water = new GameObject("Pond").AddComponent<WaterShape>();
            water.transform.position = new Vector3(pond.x, Origin.y + 9f, pond.y);
            water.Size = new Vector2(22f, 22f);
            var walk = new GameObject("Walk").AddComponent<TimberWalkShape>();
            walk.transform.position = new Vector3(Origin.x + 60f, Origin.y + 10.05f, Origin.z + 20f);
            walk.OnGround = true;
            walk.Points.Clear();
            walk.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 0f, 40f), new Vector3(0f, 0f, 80f) });
            LevelPieceBuilder.Rebuild(walk);

            GroundBaker.Bake(terrain);
            Assert.AreEqual(GroundLayerBuilder.Rock, Dominant(Origin.x + 100f, Origin.z + 64f), "a 60° bank is not rock");
            Assert.AreEqual(GroundLayerBuilder.Riverbed, Dominant(pond.x, pond.y), "the pond's bed is not riverbed");
            Assert.AreEqual(GroundLayerBuilder.Mud, Dominant(pond.x + 12.5f, pond.y), "the pond's shore is not mud");
            Assert.AreEqual(GroundLayerBuilder.LeafLitter, Dominant(hollow.x, hollow.y), "a dry hollow is not leaf litter");
            Assert.AreEqual(GroundLayerBuilder.Trail, Dominant(Origin.x + 60f, Origin.z + 60f), "a walk line on the ground is not trail");
        }

        [Test]
        public void Rebake_KeepsWhatWasPainted_AndResetGivesItBack()
        {
            GroundBaker.Bake(terrain);
            var d = terrain.terrainData;
            int res = d.alphamapResolution;
            // paint a 10 m patch of gravel by hand
            var patch = new float[10, 10, 8];
            for (int z = 0; z < 10; z++)
                for (int x = 0; x < 10; x++) patch[z, x, GroundLayerBuilder.Gravel] = 1f;
            int px = res / 2, pz = res / 2;
            d.SetAlphamaps(px, pz, patch);
            float wx = Origin.x + (px + 5) * Size / (res - 1), wz = Origin.z + (pz + 5) * Size / (res - 1);
            Shape((x, z) => 10f + (x - Origin.x) * 0.02f);   // the ground changed a little: the rules move, the paint stays
            int kept = GroundBaker.Bake(terrain);
            Assert.GreaterOrEqual(kept, 100, "the painted patch was not kept");
            Assert.AreEqual(GroundLayerBuilder.Gravel, Dominant(wx, wz), "the bake painted over the hand's gravel");
            var painted = GroundBaker.Painted(terrain);
            Assert.AreEqual(1, painted[(pz + 5) * res + px + 5], "the patch is not in the painted mask");
            Assert.AreEqual(0, painted[10 * res + 10], "an untouched texel is in the painted mask");

            int reset = GroundBaker.ResetPainted(terrain, new Vector3(wx, 0f, wz), 12f);
            Assert.GreaterOrEqual(reset, 100);
            Assert.AreNotEqual(GroundLayerBuilder.Gravel, Dominant(wx, wz), "Reset Painted left the gravel");
            Assert.AreEqual(0, GroundBaker.Painted(terrain)[(pz + 5) * res + px + 5], "the reset texel is still painted");
            GroundBaker.Bake(terrain);
            Assert.AreEqual(0, GroundBaker.Painted(terrain).Count(b => b != 0), "a re-bake after the reset finds paint");
        }

        // ------------------------------------------------------------------ resample

        [Test]
        public void Resample_ToFine_KeepsHeightsTreesAndDetails_AndUndoRestores()
        {
            Shape((x, z) => 10f + 6f * Mathf.Sin((x - Origin.x) * 0.07f) * Mathf.Cos((z - Origin.z) * 0.05f));
            var d = terrain.terrainData;
            int oldRes = d.heightmapResolution;
            var oldHeights = d.GetHeights(0, 0, oldRes, oldRes);
            d.treePrototypes = new[] { new TreePrototype { prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Nature/Tree_Pine_0.prefab") } };
            var treeWorld = Origin + new Vector3(37.3f, 0f, 81.9f);
            d.treeInstances = new[] { new TreeInstance { position = new Vector3(37.3f / Size, 0f, 81.9f / Size), widthScale = 1f, heightScale = 1f, prototypeIndex = 0 } };
            d.detailPrototypes = new[] { new DetailPrototype { prototype = AssetDatabase.LoadAssetAtPath<GameObject>(LevelTerrain.DetailsFolder + "Detail_Fern_0.prefab"), usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit } };
            d.SetDetailResolution(128, 16);
            var detail = new int[128, 128];
            detail[64, 20] = 5;
            d.SetDetailLayer(0, 0, 0, detail);
            Undo.IncrementCurrentGroup();

            TerrainResample.Resample(terrain, Size, TerrainResample.Resolution(Size, TerrainFineness.Fine) > 257 ? 257 : TerrainResample.Resolution(Size, TerrainFineness.Fine));
            Assert.AreEqual(257, d.heightmapResolution);
            Assert.AreEqual(Origin, terrain.transform.position, "the terrain moved");
            var heights = d.GetHeights(0, 0, 257, 257);
            float worst = 0f;
            for (int z = 0; z < oldRes; z++)
                for (int x = 0; x < oldRes; x++) worst = Mathf.Max(worst, Mathf.Abs(heights[z * 2, x * 2] - oldHeights[z, x]) * Height);
            Assert.Less(worst, 0.02f, "an old sample moved more than 2 cm");
            var tree = d.treeInstances.Single();
            Assert.Less(Vector3.Distance(new Vector3(treeWorld.x, 0f, treeWorld.z), Origin + Vector3.Scale(new Vector3(tree.position.x, 0f, tree.position.z), d.size)), 0.01f, "the tree moved");
            var details = d.GetDetailLayer(0, 0, d.detailResolution, d.detailResolution, 0);
            Assert.AreEqual(5, details[64, 20], "the detail cell moved or was lost");
            Assert.AreEqual(256, d.alphamapResolution, "the splat does not follow the heights");

            Undo.PerformUndo();
            Assert.AreEqual(oldRes, terrain.terrainData.heightmapResolution, "Undo did not bring back the old heightmap");
        }

        [Test]
        public void Resolution_FollowsSizeAndFineness()
        {
            Assert.AreEqual(257, TerrainResample.Resolution(256f, TerrainFineness.Normal));
            Assert.AreEqual(513, TerrainResample.Resolution(256f, TerrainFineness.Fine));
            Assert.AreEqual(1025, TerrainResample.Resolution(1024f, TerrainFineness.Normal));
            Assert.AreEqual(2049, TerrainResample.Resolution(1536f, TerrainFineness.Normal));
            Assert.AreEqual(4097, TerrainResample.Resolution(1536f, TerrainFineness.Fine));
            Assert.Greater(TerrainResample.MemoryMb(4097), TerrainResample.WarnMb / 2f);
        }

        // ------------------------------------------------------------------ L2: the height stack, trails, rivers

        float GroundAt(float wx, float wz) => terrain.SampleHeight(new Vector3(wx, 0f, wz)) + Origin.y;

        T Piece<T>(string name, Vector3 offset) where T : LevelPiece
        {
            var go = new GameObject(name);
            go.transform.position = Origin + offset;
            var piece = go.AddComponent<T>();
            piece.EnsureId();
            return piece;
        }

        [Test]
        public void SculptSurvivesShapeMove()
        {
            var hill = Piece<LandShape>("Hill", new Vector3(40f, 10f, 40f));
            hill.Points.Clear();
            hill.Points.AddRange(new[] { new Vector3(-8f, 0f, -8f), new Vector3(8f, 0f, -8f), new Vector3(8f, 0f, 8f), new Vector3(-8f, 0f, 8f) });
            hill.Height = 8f;
            hill.Falloff = 6f;
            hill.OutlineNoise = 0f;
            hill.SurfaceNoise = 0f;
            LevelPieceBuilder.Rebuild(hill);
            Assert.IsTrue(TerrainStack.Has(terrain), "the first land piece made no stack");
            Assert.Greater(GroundAt(Origin.x + 40f, Origin.z + 40f), Origin.y + 16f, "the hill did not rise");

            // the author sculpts a bump 3 m high beside the hill (a brush stroke reaches the stack through the terrain's callback)
            var d = terrain.terrainData;
            int res = d.heightmapResolution;
            int bx = Mathf.RoundToInt(70f / Size * (res - 1)), bz = Mathf.RoundToInt(40f / Size * (res - 1));
            var patch = d.GetHeights(bx - 3, bz - 3, 7, 7);
            for (int z = 0; z < 7; z++)
                for (int x = 0; x < 7; x++) patch[z, x] += 3f / Height;
            d.SetHeights(bx - 3, bz - 3, patch);
            float bump = GroundAt(Origin.x + 70f, Origin.z + 40f);
            Assert.AreEqual(13f, bump - Origin.y, 0.1f, "the sculpt did not take");

            // the hill moves away: its old ground comes back, the sculpt stays
            Undo.RecordObject(hill.transform, "move");
            hill.transform.position = Origin + new Vector3(40f, 10f, 100f);
            LevelPieceBuilder.Rebuild(hill);
            Assert.AreEqual(10f, GroundAt(Origin.x + 40f, Origin.z + 40f) - Origin.y, 0.1f, "the hill's old ground did not come back");
            Assert.Greater(GroundAt(Origin.x + 40f, Origin.z + 100f), Origin.y + 16f, "the hill did not rise where it went");
            Assert.AreEqual(bump, GroundAt(Origin.x + 70f, Origin.z + 40f), 0.05f, "the sculpt was lost when the hill moved");

            // deleted: its ground comes back
            Object.DestroyImmediate(hill.gameObject);
            TerrainStack.Sync(scene);
            Assert.AreEqual(10f, GroundAt(Origin.x + 40f, Origin.z + 100f) - Origin.y, 0.1f, "a deleted hill left its ground");
            Assert.AreEqual(bump, GroundAt(Origin.x + 70f, Origin.z + 40f), 0.05f, "the sculpt was lost when the hill was deleted");
        }

        [Test]
        public void Trail_HoldsItsGrade_BanksMeetTheGround_AndIsWalked()
        {
            // a 40 % slope rising to the north
            Shape((x, z) => 5f + (z - Origin.z) * 0.4f);
            var trail = Piece<TrailShape>("Trail", new Vector3(64f, 0f, 10f));
            trail.Points.Clear();
            trail.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 0f, 40f), new Vector3(0f, 0f, 80f), new Vector3(0f, 0f, 100f) });
            trail.MaxGrade = 30f;
            trail.Bank = 4f;
            trail.EdgeNoise = 0f;
            LevelPieceBuilder.Rebuild(trail);
            var line = GroundShapes.Trails[trail.PieceId].line;
            float worst = 0f;
            for (int i = 1; i < line.Length; i++)
            {
                float run = Vector2.Distance(new Vector2(line[i].x, line[i].z), new Vector2(line[i - 1].x, line[i - 1].z));
                if (run > 0.05f) worst = Mathf.Max(worst, Mathf.Abs(line[i].y - line[i - 1].y) / run);
            }
            Assert.LessOrEqual(worst, 0.305f, "the trail is steeper than its grade");
            // the banks meet the ground: at their outer edge the ground is the slope's own, within 0.3 m
            float outer = trail.Width * 0.5f + trail.Bank + 0.5f;
            for (float z = 20f; z < 90f; z += 7f)
                foreach (float side in new[] { -outer, outer })
                    Assert.AreEqual(5f + z * 0.4f, GroundAt(Origin.x + 64f + side, Origin.z + z) - Origin.y, 0.3f, $"a step where the bank meets the ground at z {z:F0}");
            var walk = trail.Output.GetComponent<LevelPieceOutput>().WalkLine;
            Assert.Greater(walk.Length, 10, "the trail has no walk line");
            Assert.AreEqual(GroundAt(walk[walk.Length / 2].x, walk[walk.Length / 2].z), walk[walk.Length / 2].y - 0.05f, 0.25f, "the walk line is not on the trail");
            Assert.IsTrue(LevelForest.Paths(scene).Count > 0, "forests and bursts do not keep off the trail");
        }

        [Test]
        public void River_CarvesItsBed_WaterUnderTheBanks_KillZonesUnderIt()
        {
            var river = Piece<RiverShape>("River", new Vector3(30f, 10f, 10f));
            river.Points.Clear();
            river.Points.AddRange(new[] { Vector3.zero, new Vector3(10f, 0f, 40f), new Vector3(30f, 0f, 90f) });
            river.Width = 8f;
            river.Depth = 3f;
            river.WaterDrop = 1.2f;
            river.Slope = 0f;
            river.BankNoise = 0f;
            LevelPieceBuilder.Rebuild(river);
            Physics.SyncTransforms();
            var bed = GroundShapes.Rivers[river.PieceId];
            var mid = bed.bed[bed.bed.Length / 2];
            Assert.AreEqual(Origin.y + 10f - 3f, GroundAt(mid.x, mid.z), 0.3f, "the bed is not carved to its depth");
            var water = river.Output.GetComponentsInChildren<NatureWater>().Single();
            var surface = water.GetComponent<MeshFilter>().sharedMesh.vertices.Average(v => v.y);
            Assert.AreEqual(Origin.y + 10f - 1.2f, surface, 0.15f, "the water is not Water Drop under the banks");
            for (int i = 0; i < bed.bed.Length; i += 6)
            {
                var p = new Vector3(bed.bed[i].x, bed.surface[i] - 0.6f, bed.bed[i].z);
                Assert.IsTrue(Physics.OverlapSphere(p, 0.05f, ~0, QueryTriggerInteraction.Collide).Any(k => k.GetComponentInParent<KillZone>() != null),
                              $"no kill zone under the water at {i}");
            }

            var ravine = Piece<RiverShape>("Ravine", new Vector3(100f, 10f, 10f));
            ravine.Kind = RiverKind.Ravine;
            ravine.Points.Clear();
            ravine.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 0f, 80f) });
            LevelPieceBuilder.Rebuild(ravine);
            Assert.IsEmpty(ravine.Output.GetComponentsInChildren<NatureWater>(), "a ravine has water");
            Assert.IsEmpty(ravine.Output.GetComponentsInChildren<KillZone>(), "a ravine has kill zones");
            Assert.Less(GroundAt(Origin.x + 100f, Origin.z + 50f), Origin.y + 10f - ravine.Depth + 0.3f, "the ravine is not carved");
        }
    }
}
