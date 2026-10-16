using System.Linq;
using HotPatata.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Tests
{
    /// <summary>
    /// The rock and mine pieces of the authored levels (ARCHITECTURE §27.2): a cave tunnel and a cavern are solid rock with a roof that is a
    /// ceiling (no lob passes it) and their clear space inside, skylights are real holes; a cliff's top and shelves are walked on and its
    /// faces stop a body; mine frames stand on what is under them, their posts solid; lane markers draw their lane's glyph; the timber ones
    /// freeze into elements (their lanterns carried over). Built far from the origin, removed after; the watcher is off while they run.
    /// </summary>
    public class LevelRockTests
    {
        const string StoreFolder = "Assets/Art/Generated/__LevelRockTests/";
        static readonly Vector3 Origin = new Vector3(-5000f, 0f, 5000f);

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
            ground.transform.localScale = new Vector3(160f, 1f, 160f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in scene.GetRootGameObjects().Except(before)) Object.DestroyImmediate(go);
            MeshCatalog.Folder = MeshCatalog.DefaultFolder;
            AssetDatabase.DeleteAsset(StoreFolder.TrimEnd('/'));
            LevelPieceWatcher.Enabled = true;
        }

        T Piece<T>(string name, Vector3 offset) where T : LevelPiece
        {
            var go = new GameObject(name);
            go.transform.position = Origin + offset;
            return go.AddComponent<T>();
        }

        static int Env => LayerMask.GetMask("Environment");

        static bool Hit(Vector3 from, Vector3 dir, float reach, out RaycastHit hit) =>
            Physics.Raycast(from, dir, out hit, reach, Env, QueryTriggerInteraction.Ignore);

        [Test]
        public void Tunnel_IsSolidRockWithACeilingAndItsClearSpace()
        {
            var shell = Piece<RockShellShape>("Tunnel", Vector3.zero);
            shell.Points.Clear();
            shell.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 0f, 15f), new Vector3(4f, 0f, 30f) });
            shell.Width = 8f;
            shell.Height = 6f;
            LevelPieceBuilder.Rebuild(shell);
            Physics.SyncTransforms();
            Assert.IsNotNull(shell.Output);
            var proxy = shell.Output.GetComponentsInChildren<WalkableProxy>().Single();
            Assert.AreEqual(WalkableProxy.Kind.Solid, proxy.ProxyKind);
            Assert.AreEqual(LayerMask.NameToLayer("Environment"), proxy.gameObject.layer);
            Assert.IsNotNull(proxy.GetComponent<CourseCeiling>(), "a cave's roof is a ceiling");

            var inside = Origin + new Vector3(0f, 1f, 10f);
            Assert.IsTrue(Hit(inside, Vector3.up, 30f, out var roof), "no roof over the tunnel");
            Assert.GreaterOrEqual(roof.point.y - Origin.y, shell.Height - 0.05f, "the rock comes into the clear space");
            Assert.IsTrue(Hit(inside, Vector3.right, 20f, out var wall), "no wall beside the tunnel");
            Assert.GreaterOrEqual(wall.distance, shell.Width * 0.5f - 0.6f, "the wall comes into the clear space");
            Assert.IsFalse(Physics.OverlapCapsule(inside, inside + Vector3.up * 3f, 0.4f, Env, QueryTriggerInteraction.Ignore).Any(c => c.GetComponentInParent<LevelPieceOutput>() != null),
                           "a body does not fit in the tunnel");
            Assert.IsFalse(Hit(Origin + new Vector3(0f, 2f, -4f), Vector3.forward, 8f, out var mouth) && mouth.collider.GetComponentInParent<LevelPieceOutput>() != null,
                           "the tunnel's mouth is closed");
        }

        [Test]
        public void Tunnel_SkylightIsAHoleAndADeadEndIsClosed()
        {
            var shell = Piece<RockShellShape>("Tunnel", Vector3.zero);
            shell.Points.Clear();
            shell.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 0f, 20f) });
            shell.CloseEnd = true;
            shell.Skylights.Add(new RockSkylight { at = new Vector2(0f, 8f), radius = 2f });
            LevelPieceBuilder.Rebuild(shell);
            Physics.SyncTransforms();
            Assert.IsFalse(Hit(Origin + new Vector3(0f, 1f, 8f), Vector3.up, 30f, out _), "no hole under the skylight");
            Assert.IsTrue(Hit(Origin + new Vector3(0f, 1f, 14f), Vector3.up, 30f, out _), "the roof is gone past the skylight");
            Assert.IsTrue(Hit(Origin + new Vector3(0f, 2f, 15f), Vector3.forward, 12f, out _), "the dead end is open");
        }

        [Test]
        public void Cavern_DomesOverItsOutline()
        {
            var shell = Piece<RockShellShape>("Cavern", Vector3.zero);
            shell.Kind = RockShellKind.Cavern;
            shell.Height = 10f;
            shell.Floor = true;
            shell.Points.Clear();
            shell.Points.AddRange(DeckShapes.Oval(24f, 20f, 16));
            LevelPieceBuilder.Rebuild(shell);
            Physics.SyncTransforms();
            Assert.IsTrue(Hit(Origin + Vector3.up * 1f, Vector3.up, 40f, out var crown), "no dome");
            Assert.GreaterOrEqual(crown.point.y - Origin.y, shell.Height - 0.1f);
            Assert.IsTrue(Hit(Origin + Vector3.up * 1f, Vector3.right, 40f, out var wall) && wall.distance > 8f, "no wall round the cavern");
            Assert.IsTrue(Hit(Origin + new Vector3(3f, 3f, 2f), Vector3.down, 6f, out var floor) && Mathf.Abs(floor.point.y - Origin.y) < 0.05f, "the floor is not at the piece");
        }

        [Test]
        public void Cliff_TopAndShelvesAreWalkedOn_FacesStopABody()
        {
            var cliff = Piece<CliffShape>("Cliff", Vector3.zero);
            cliff.Outline.Clear();
            cliff.Outline.AddRange(DeckShapes.Rectangle(12f, 10f));
            cliff.Height = 10f;
            cliff.Ledges.Add(new CliffLedge { height = 5f, inset = 2f });
            LevelPieceBuilder.Rebuild(cliff);
            Physics.SyncTransforms();
            CollectionAssert.AreEqual(new[] { 5f, 5f }, cliff.Faces());
            Assert.IsTrue(Hit(Origin + new Vector3(0f, 20f, 0f), Vector3.down, 30f, out var top) && Mathf.Abs(top.point.y - (Origin.y + 10f)) < 0.05f, "the top is not walkable at its height");
            Assert.IsTrue(Hit(Origin + new Vector3(5f, 8f, 0f), Vector3.down, 10f, out var shelf) && Mathf.Abs(shelf.point.y - (Origin.y + 5f)) < 0.6f, "no shelf at the ledge");
            Assert.IsTrue(Hit(Origin + new Vector3(-20f, 2f, 0f), Vector3.right, 30f, out var face) && face.point.x < Origin.x - 5f, "the face does not stop a body");
            Assert.AreEqual(WalkableProxy.Kind.Solid, cliff.Output.GetComponentInChildren<WalkableProxy>().ProxyKind);
        }

        [Test]
        public void MineFrames_StandOnTheRock_TheirPostsSolid_AndFreezeWithTheirLanterns()
        {
            var cliff = Piece<CliffShape>("Floor rock", new Vector3(0f, 0f, 40f));
            cliff.Outline.Clear();
            cliff.Outline.AddRange(DeckShapes.Rectangle(14f, 20f));
            cliff.Height = 2f;
            cliff.Roughness = 0f;
            LevelPieceBuilder.Rebuild(cliff);
            Physics.SyncTransforms();
            var frames = Piece<MineFrameShape>("Frames", new Vector3(0f, 2f, 40f));
            frames.Points.Clear();
            frames.Points.AddRange(new[] { new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 6f) });
            frames.LanternEvery = 2;
            LevelPieceBuilder.Rebuild(frames);
            Physics.SyncTransforms();
            var posts = frames.Output.GetComponentsInChildren<BoxCollider>().Where(b => b.gameObject.layer == LayerMask.NameToLayer("Environment")).ToList();
            Assert.GreaterOrEqual(posts.Count, 8, "four frames, two posts each");
            Assert.That(posts.All(p => Mathf.Abs(p.bounds.min.y - (Origin.y + 2f)) < 0.1f), "a post does not stand on the rock under it");
            int lanterns = frames.Output.GetComponentsInChildren<Transform>().Count(t => PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TimberMine.LanternPrefab) != null) Assert.Greater(lanterns, 0, "no lantern hangs");

            var frozen = TimberFreezer.Freeze(frames);
            Assert.IsNotNull(frozen);
            Assert.Greater(frozen.GetComponentsInChildren<TimberElement>().Length, 10);
            Assert.AreEqual(lanterns, frozen.GetComponentsInChildren<Transform>().Count(t => PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)), "the lanterns are lost on freezing");
        }

        [Test]
        public void LaneMarker_DrawsItsGlyph_AndTrackLiesOnTheGround()
        {
            var sun = Piece<LaneMarkerShape>("Sun marker", new Vector3(20f, 0f, 0f));
            sun.Form = LaneMarkerForm.Arch;
            LevelPieceBuilder.Rebuild(sun);
            Assert.Greater(sun.Output.GetComponentsInChildren<MeshRenderer>().Count(r => r.sharedMaterial != null && r.sharedMaterial.name == NatureMaterialBuilder.PaintAmber), 2,
                           "no gold sun painted");
            var moon = Piece<LaneMarkerShape>("Moon marker", new Vector3(26f, 0f, 0f));
            moon.Lane = Lane.Moon;
            LevelPieceBuilder.Rebuild(moon);
            Assert.Greater(moon.Output.GetComponentsInChildren<MeshRenderer>().Count(r => r.sharedMaterial != null && r.sharedMaterial.name == NatureMaterialBuilder.PaintBlue), 0,
                           "no blue moon painted");

            var track = Piece<MineRailShape>("Track", new Vector3(30f, 0f, 0f));
            LevelPieceBuilder.Rebuild(track);
            var drawn = track.Output.GetComponentsInChildren<MeshRenderer>().Select(r => r.bounds).ToList();
            Assert.IsNotEmpty(drawn);
            Assert.That(drawn.All(b => b.min.y > Origin.y - 0.05f && b.max.y < Origin.y + 0.4f), "the track does not lie on the ground");
            Assert.IsEmpty(track.Output.GetComponentsInChildren<Collider>(), "a track is decoration");
        }
    }
}
