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
    /// The authored levels' timber pieces (ARCHITECTURE §27): each piece drawn from its data, attached ends opening a deck's rail,
    /// rebuilds only when the inputs change (moves, undo, duplicates), meshes written per piece and cleaned up, and the menu's level
    /// list kept in step (display names). Built far from the origin in the test scene, removed after; the watcher is off while they run.
    /// </summary>
    public class LevelAuthoringTests
    {
        const string StoreFolder = "Assets/Art/Generated/__LevelAuthoringTests/";
        static readonly Vector3 Origin = new Vector3(5000f, 0f, 5000f);

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
            ground.transform.localScale = new Vector3(120f, 1f, 120f);
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

        static int RailWallsNear(Vector3 at, float radius) =>
            Physics.OverlapSphere(at, radius, ~0, QueryTriggerInteraction.Ignore)
                .Count(c => c is BoxCollider && c.GetComponentInParent<LevelPieceOutput>() != null);

        // ------------------------------------------------------------------ pieces

        [Test]
        public void Deck_IsBoardsOnAWalkableProxyWithRails()
        {
            var deck = Piece<TimberDeckShape>("Deck", new Vector3(0f, 3f, 0f));
            LevelPieceBuilder.Rebuild(deck);
            Assert.IsNotNull(deck.Output, "no output");
            var proxy = deck.Output.GetComponentsInChildren<WalkableProxy>().Single();
            Assert.AreEqual(WalkableProxy.Kind.Deck, proxy.ProxyKind);
            Assert.Greater(deck.Output.GetComponentsInChildren<MeshRenderer>().Length, 0, "nothing drawn");
            Assert.Greater(RailWallsNear(deck.EdgePoint(0.1f) + Vector3.up * 0.6f, 0.3f), 0, "no rail on the edge");
            Assert.IsTrue(Physics.Raycast(Origin + new Vector3(0f, 6f, 0f), Vector3.down, out var hit, 10f) && Mathf.Abs(hit.point.y - (Origin.y + 3f)) < 0.05f,
                          "the deck's surface is not where the piece is");
        }

        [Test]
        public void AttachedStair_OpensTheDeckRailWhereItArrives()
        {
            var deck = Piece<TimberDeckShape>("Deck", new Vector3(0f, 3f, 0f));
            var walk = Piece<TimberWalkShape>("Stair", new Vector3(-12f, 0f, 0f));
            walk.Points.Clear();
            walk.Points.AddRange(new[] { Vector3.zero, new Vector3(2f, 0f, 0f), new Vector3(7.5f, 3f, 0f), new Vector3(9f, 3f, 0f) });
            float along = deck.NearestAlong(Origin + new Vector3(-3f, 3f, 0f), out _);
            walk.End = new TimberAttach { deck = deck, along = along };
            LevelPieceBuilder.Refresh(new LevelPiece[] { deck, walk });

            Assert.AreEqual(1, TimberLinks.OpeningsOf(deck).Count);
            var landing = deck.EdgePoint(along);
            Assert.AreEqual(0, RailWallsNear(landing + Vector3.up * 0.6f, 0.25f), "the deck's rail closes the stair's landing");
            Assert.Greater(RailWallsNear(deck.EdgePoint(Mathf.Repeat(along + 0.5f, 1f)) + Vector3.up * 0.6f, 0.3f), 0, "the rest of the rail is gone");
            var line = walk.Output.GetComponent<LevelPieceOutput>().WalkLine;
            Assert.Less(Vector3.Distance(line[line.Length - 1], landing), TimberWalkShape.AttachLead + 0.15f, "the stair does not arrive on the deck");
        }

        [Test]
        public void Snap_AttachesAnEndDroppedNearADeckEdge()
        {
            var deck = Piece<TimberDeckShape>("Deck", new Vector3(0f, 3f, 0f));
            var walk = Piece<TimberWalkShape>("Stair", new Vector3(-12f, 0f, 0f));
            Assert.IsTrue(TimberLinks.TrySnap(scene, Origin + new Vector3(-3.5f, 3.2f, 0.4f), walk, out var attach, out var edge));
            Assert.AreEqual(deck, attach.deck);
            Assert.AreEqual(Origin.y + 3f, edge.y, 0.01f);
            Assert.IsFalse(TimberLinks.TrySnap(scene, Origin + new Vector3(-6f, 3f, 0f), walk, out _, out _), "snapped from 3 m away");
            Assert.IsFalse(TimberLinks.TrySnap(scene, Origin + new Vector3(-3.2f, 0f, 0f), walk, out _, out _), "snapped 3 m under the deck");
        }

        [Test]
        public void WalkwayJoinedToABridgeEnd_FollowsItAndNeverLoops()
        {
            var bridge = Piece<RopeBridgeShape>("Bridge", new Vector3(0f, 3f, 0f));
            var walk = Piece<TimberWalkShape>("Walk", new Vector3(-10f, 0f, -0.5f));
            walk.Points.Clear();
            walk.Points.AddRange(new[] { Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(9f, 2.8f, 0f), new Vector3(9.6f, 3f, 0.4f) });
            Assert.IsTrue(TimberLinks.TrySnap(scene, walk.World(walk.Points[3]), walk, out var attach, out var at), "the bridge's start is 0.7 m away");
            Assert.AreEqual(bridge, attach.joined);
            Assert.IsTrue(attach.joinedStart);
            walk.End = attach;
            Assert.AreEqual(bridge.WorldEnds().a, walk.WorldControl().Last());

            // the bridge's start joined back to the walkway's end: each reads the other's own end, no loop
            bridge.StartAttach = new TimberAttach { joined = walk, joinedStart = false };
            Assert.DoesNotThrow(() => bridge.WorldEnds());
            var all = new LevelPiece[] { bridge, walk };
            LevelPieceBuilder.Refresh(all);
            bridge.StartAttach = default;

            bridge.transform.position += new Vector3(0f, 1f, 2f);
            Assert.AreEqual(2, LevelPieceBuilder.Refresh(all), "the walkway joined to the bridge is not rebuilt with it");
            Assert.Less(Vector3.Distance(walk.WorldControl().Last(), bridge.WorldEnds().a), 1e-4f, "the joined end did not follow");

            Object.DestroyImmediate(bridge.gameObject);
            Assert.IsFalse(walk.End.Attached, "joined to a removed bridge");
            Assert.DoesNotThrow(() => walk.WorldControl());
        }

        [Test]
        public void MovingADeck_RebuildsItAndTheEndsOnIt_Only()
        {
            var deck = Piece<TimberDeckShape>("Deck", new Vector3(0f, 3f, 0f));
            var other = Piece<TimberDeckShape>("Other deck", new Vector3(30f, 3f, 0f));
            var bridge = Piece<RopeBridgeShape>("Bridge", Vector3.zero);
            bridge.StartAttach = new TimberAttach { deck = deck, along = deck.NearestAlong(Origin + new Vector3(0f, 3f, 2f), out _) };
            bridge.B = new Vector3(0f, 3f, 14f);
            var all = new LevelPiece[] { deck, other, bridge };
            Assert.AreEqual(3, LevelPieceBuilder.Refresh(all));
            Assert.AreEqual(0, LevelPieceBuilder.Refresh(all), "rebuilt with nothing changed (the signature sees its own geometry?)");
            var otherOutput = other.Output;
            deck.transform.position += Vector3.right;
            Assert.AreEqual(2, LevelPieceBuilder.Refresh(all), "the deck and the bridge on it");
            Assert.AreEqual(otherOutput, other.Output, "an unrelated deck was rebuilt");
        }

        [Test]
        public void Undo_BringsBackThePieceAndItsGeometry()
        {
            var deck = Piece<TimberDeckShape>("Deck", new Vector3(0f, 3f, 0f));
            LevelPieceBuilder.Rebuild(deck);
            string before = LevelPieceBuilder.Signature(deck);
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(deck.transform, "move deck");
            deck.transform.position += Vector3.up * 2f;
            Undo.IncrementCurrentGroup();
            Assert.AreEqual(1, LevelPieceBuilder.Refresh(new LevelPiece[] { deck }));
            Undo.PerformUndo();
            Assert.AreEqual(1, LevelPieceBuilder.Refresh(new LevelPiece[] { deck }), "not rebuilt after the undo");
            Assert.AreEqual(before, deck.Output.GetComponent<LevelPieceOutput>().Signature);
            Assert.AreEqual(1, deck.GetComponentsInChildren<LevelPieceOutput>(true).Count(o => o.transform.parent == deck.transform), "stale geometry left");
        }

        [Test]
        public void Duplicate_GetsItsOwnIdAndGeometry()
        {
            var deck = Piece<TimberDeckShape>("Deck", new Vector3(0f, 3f, 0f));
            LevelPieceBuilder.Rebuild(deck);
            var copy = Object.Instantiate(deck.gameObject).GetComponent<TimberDeckShape>();
            copy.transform.position += Vector3.forward * 10f;
            Assert.AreEqual(deck.PieceId, copy.PieceId);
            LevelPieceWatcher.Flush(scene);
            Assert.AreNotEqual(deck.PieceId, copy.PieceId);
            Assert.AreNotEqual(deck.Output, copy.Output);
            Assert.AreEqual(1, copy.GetComponentsInChildren<LevelPieceOutput>(true).Count(o => o.transform.parent == copy.transform));
        }

        [Test]
        public void ForcedSupports_ChangeWhatHoldsADeck()
        {
            var auto = Piece<TimberDeckShape>("Auto deck", new Vector3(0f, 3f, 0f));
            var none = Piece<TimberDeckShape>("Auto deck", new Vector3(20f, 3f, 0f));
            none.Support = TimberSupport.None;
            LevelPieceBuilder.Refresh(new LevelPiece[] { auto, none });
            int Triangles(LevelPiece p) => LevelPieceBuilder.Meshes(p.Output).Where(m => p.Output.GetComponentsInChildren<MeshFilter>().Any(f => f.sharedMesh == m)).Sum(m => m.triangles.Length / 3);
            Assert.Less(Triangles(none), Triangles(auto), "a deck held by nothing has as many beams as one on posts");
        }

        [Test]
        public void RingRoundATrunk_HasNoRailOnItsSeam()
        {
            var ring = DeckShapes.RoundTrunk(Vector2.zero, 1f, 3f, 0f, 360f);
            var skip = LevelPieceBuilder.SeamSkip(ring);
            Assert.IsTrue(skip(new Vector2(2.5f, 0.005f)), "rail on the seam");
            Assert.IsFalse(skip(new Vector2(-4.12f, 0f)), "no rail on the outer edge");
        }

        [Test]
        public void House_WallsStopButTheDoorIsOpenAndTheRoofIsACeiling()
        {
            var house = Piece<HouseShape>("House", Vector3.zero);
            LevelPieceBuilder.Rebuild(house);
            var south = house.Doors[0];
            Assert.AreEqual(HouseShape.Wall.South, south.wall);
            var mask = LayerMask.GetMask("Environment");
            // through the door (the middle of the south wall) and through the wall beside it, at chest height
            Assert.IsFalse(Physics.Raycast(Origin + new Vector3(0f, 1.2f, -5f), Vector3.forward, 4f, mask, QueryTriggerInteraction.Ignore), "the door is shut");
            Assert.IsTrue(Physics.Raycast(Origin + new Vector3(-2.2f, 1.2f, -5f), Vector3.forward, 4f, mask, QueryTriggerInteraction.Ignore), "the wall lets through");
            Assert.AreEqual(1, house.Output.GetComponentsInChildren<CourseCeiling>().Length);
        }

        // ------------------------------------------------------------------ meshes

        [Test]
        public void Meshes_StayInMemoryUntilSaved_ThenOneFolderPerPiece()
        {
            var deck = Piece<TimberDeckShape>("Deck", new Vector3(0f, 3f, 0f));
            var walk = Piece<TimberWalkShape>("Walk", new Vector3(10f, 0f, 0f));
            var pieces = new LevelPiece[] { deck, walk };
            LevelPieceBuilder.Rebuild(pieces);
            Assert.IsTrue(pieces.SelectMany(p => LevelPieceBuilder.Meshes(p.Output)).All(m => !EditorUtility.IsPersistent(m)), "written while editing");

            int written = LevelMeshStore.Persist(pieces, StoreFolder);
            Assert.Greater(written, 0);
            Assert.IsTrue(pieces.SelectMany(p => LevelPieceBuilder.Meshes(p.Output)).All(EditorUtility.IsPersistent), "a mesh left in memory");
            string deckFolder = LevelMeshStore.PiecesFolder(StoreFolder) + deck.PieceId;
            var files = Directory.GetFiles(deckFolder, "*.asset");
            string guid = AssetDatabase.AssetPathToGUID(files[0].Replace('\\', '/'));

            deck.transform.position += Vector3.up;
            LevelPieceBuilder.Refresh(pieces);
            LevelMeshStore.Persist(pieces, StoreFolder);
            Assert.AreEqual(files.Length, Directory.GetFiles(deckFolder, "*.asset").Length, "the rebuild left stale meshes");
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(files[0].Replace('\\', '/')), "the rebuilt mesh is a new asset");

            Assert.AreEqual(1, LevelMeshStore.DeleteOrphans(new[] { walk.PieceId }, StoreFolder));
            Assert.IsFalse(Directory.Exists(deckFolder), "the removed deck's folder stays");
        }

        // ------------------------------------------------------------------ menu

        [Test]
        public void Menu_KeepsItsListsTogether_AndShowsDisplayNames()
        {
            var go = new GameObject("Bootstrap");
            var bootstrap = go.AddComponent<NetworkBootstrap>();
            var so = new SerializedObject(bootstrap);
            CourseKit.WriteMenu(so, new[]
            {
                new CourseKit.MenuEntry { scene = "PatataWilds", checkpoints = 25, minPlayers = 2 },
                new CourseKit.MenuEntry { scene = "PassSandbox", checkpoints = 1, minPlayers = 2 },
            });
            CourseKit.RegisterInMenu(so, "MyLevel", 3, CourseKit.MenuSlot.Keep, 3, "My Level");
            CourseKit.RegisterInMenu(so, "PatataTemple", 15, CourseKit.MenuSlot.Third, 3);
            so.ApplyModifiedPropertiesWithoutUndo();

            var menu = CourseKit.ReadMenu(new SerializedObject(bootstrap));
            CollectionAssert.AreEqual(new[] { "PatataWilds", "MyLevel", "PatataTemple", "PassSandbox" }, menu.Select(e => e.scene).ToArray());
            CollectionAssert.AreEqual(new[] { 25, 3, 15, 1 }, menu.Select(e => e.checkpoints).ToArray());
            CollectionAssert.AreEqual(new[] { 2, 3, 3, 2 }, menu.Select(e => e.minPlayers).ToArray());
            Assert.AreEqual("My Level", bootstrap.DisplayNameOf(1));
            Assert.AreEqual("PatataTemple", bootstrap.DisplayNameOf(2), "no display name: the scene's");
            Assert.AreEqual("", bootstrap.DisplayNameOf(9));
        }

        [Test]
        public void LevelNames_AreSceneNamesNotTaken()
        {
            Assert.IsNull(LevelScenes.NameProblem("MyNewLevel42"));
            Assert.IsNotNull(LevelScenes.NameProblem("My Level"), "spaces");
            Assert.IsNotNull(LevelScenes.NameProblem("4Level"), "a digit first");
            Assert.IsNotNull(LevelScenes.NameProblem("PatataRanger"), "a course's name");
        }
    }
}
