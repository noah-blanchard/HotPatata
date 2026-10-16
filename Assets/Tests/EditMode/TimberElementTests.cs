using System.Linq;
using HotPatata.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata.Tests
{
    /// <summary>
    /// Frozen timber (ARCHITECTURE §27.1): a piece frozen into its elements looks and walks like the piece (same place, a walking
    /// surface at the boards' tops, a stair still a smooth ramp, a bridge's surface following its sag), a board taken away leaves a hole,
    /// attached ends stay put, ropes follow what they hang on, and the catalogue shares one mesh per shape. Built far from the origin in
    /// the test scene with a catalogue of their own, removed after; the watchers are off while they run.
    /// </summary>
    public class TimberElementTests
    {
        const string CatalogFolder = "Assets/Art/Generated/__TimberElementTests/";
        static readonly Vector3 Origin = new Vector3(-5000f, 0f, 5000f);

        Scene scene;
        GameObject[] before;

        [SetUp]
        public void SetUp()
        {
            LevelPieceWatcher.Enabled = false;
            TimberElementWatcher.Enabled = false;
            MeshCatalog.Folder = CatalogFolder;
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
            MeshCatalog.Folder = MeshCatalog.DefaultFolder;
            AssetDatabase.DeleteAsset(CatalogFolder.TrimEnd('/'));
            LevelPieceWatcher.Enabled = true;
            TimberElementWatcher.Enabled = true;
        }

        T Piece<T>(string name, Vector3 offset) where T : LevelPiece
        {
            var go = new GameObject(name);
            go.transform.position = Origin + offset;
            return go.AddComponent<T>();
        }

        static Bounds Drawn(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static bool SurfaceAt(Vector3 above, out float y)
        {
            y = float.NaN;
            if (!Physics.Raycast(above + Vector3.up * 3f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) return false;
            y = hit.point.y;
            return hit.collider.GetComponent<WalkableProxy>() != null;
        }

        // ------------------------------------------------------------------ freezing

        [Test]
        public void FrozenDeck_LooksAndWalksLikeThePiece()
        {
            var deck = Piece<TimberDeckShape>("Deck", new Vector3(0f, 3f, 0f));
            LevelPieceBuilder.Rebuild(deck);
            var drawn = Drawn(deck.Output);
            var frozen = TimberFreezer.Freeze(deck);

            Assert.IsNotNull(frozen, "not frozen");
            Assert.IsTrue(deck == null, "the piece is still there");
            var elements = frozen.GetComponentsInChildren<TimberElement>();
            Assert.Greater(elements.Count(e => e.Kind == TimberElementKind.Plank && e.Walk == TimberWalk.Slab), 20, "too few boards");
            Assert.IsTrue(elements.All(e => e.GetComponent<MeshFilter>().sharedMesh != null), "an element is not drawn");
            var after = Drawn(frozen.gameObject);
            Assert.Less(Vector3.Distance(drawn.min, after.min), 0.05f, "the frozen deck is not where the piece was drawn");
            Assert.Less(Vector3.Distance(drawn.max, after.max), 0.05f, "the frozen deck is not where the piece was drawn");
            Physics.SyncTransforms();
            Assert.IsTrue(SurfaceAt(Origin + new Vector3(0.3f, 3f, 0.4f), out float y), "no walking surface on the deck");
            Assert.AreEqual(Origin.y + 3f, y, 0.02f, "the walking surface is not at the boards' tops");
            Assert.AreEqual(WalkableProxy.Kind.Deck, frozen.Surface.GetComponent<WalkableProxy>().ProxyKind);
            Assert.Greater(frozen.GetComponentsInChildren<TimberWall>().Length, 0, "the rail lost its wall");
            Assert.IsTrue(elements.All(e => EditorUtility.IsPersistent(e.GetComponent<MeshFilter>().sharedMesh)), "a mesh is not in the catalogue");
        }

        [Test]
        public void FrozenStair_IsStillASmoothRamp()
        {
            var walk = Piece<TimberWalkShape>("Stair", Vector3.zero);
            walk.Points.Clear();
            walk.Points.AddRange(new[] { Vector3.zero, new Vector3(2f, 0f, 0f), new Vector3(7.5f, 3f, 0f), new Vector3(10f, 3f, 0f) });
            LevelPieceBuilder.Rebuild(walk);
            var frozen = TimberFreezer.Freeze(walk);
            Physics.SyncTransforms();

            Assert.Greater(frozen.WalkLine.Length, 10, "no walk line");
            Assert.AreEqual(WalkableProxy.Kind.Walkway, frozen.SurfaceKind);
            // along the flight the surface climbs evenly: no step edge to catch on
            float previous = float.NaN;
            for (float x = 3f; x <= 7f; x += 0.1f)
            {
                Assert.IsTrue(SurfaceAt(Origin + new Vector3(x, 2f, 0f), out float y), $"no surface at {x:F1} m");
                if (!float.IsNaN(previous)) Assert.Less(Mathf.Abs(y - previous), 0.1f, $"a step at {x:F1} m");
                previous = y;
            }
        }

        [Test]
        public void FrozenBridge_SurfaceFollowsTheSag()
        {
            var bridge = Piece<RopeBridgeShape>("Bridge", new Vector3(0f, 3f, 0f));
            bridge.Sag = 0.6f;
            LevelPieceBuilder.Rebuild(bridge);
            var frozen = TimberFreezer.Freeze(bridge);
            Physics.SyncTransforms();

            Assert.AreEqual(WalkableProxy.Kind.Bridge, frozen.SurfaceKind);
            Assert.IsTrue(SurfaceAt(Origin + new Vector3(0f, 3f, 5f), out float middle), "no surface at mid span");
            Assert.AreEqual(Origin.y + 3f - 0.6f, middle, 0.08f, "the surface does not dip with the bridge");
            Assert.Greater(frozen.GetComponentsInChildren<TimberPath>().Count(p => p.Kind == TimberElementKind.Rope), 4, "the ropes are missing");
        }

        [Test]
        public void TakingBoardsAway_LeavesAHole()
        {
            var deck = Piece<TimberDeckShape>("Deck", new Vector3(0f, 3f, 0f));
            LevelPieceBuilder.Rebuild(deck);
            var frozen = TimberFreezer.Freeze(deck);
            var at = Origin + new Vector3(0f, 3f, 0f);
            foreach (var board in frozen.GetComponentsInChildren<TimberBoard>().Where(b => b.Walk == TimberWalk.Slab).ToList())
            {
                var p = board.transform.position;
                var local = board.transform.InverseTransformPoint(at);
                if (Mathf.Abs(local.x) < board.Size.x * 0.5f + 0.5f && Mathf.Abs(local.z) < board.Size.z * 0.5f + 0.1f) Object.DestroyImmediate(board.gameObject);
            }
            Assert.IsTrue(WalkSurfaceBuilder.Refresh(frozen), "the surface was not laid again");
            Physics.SyncTransforms();
            Assert.IsFalse(SurfaceAt(at, out float y) && y > Origin.y + 2f, "the deck still holds a walker where its boards were taken away");
            Assert.IsTrue(SurfaceAt(at + new Vector3(0f, 0f, 1.6f), out _) || SurfaceAt(at + new Vector3(1.6f, 0f, 0f), out _), "the rest of the deck went too");
        }

        [Test]
        public void AWalkwayOnAFrozenDeck_KeepsItsEnd()
        {
            var deck = Piece<TimberDeckShape>("Deck", new Vector3(0f, 3f, 0f));
            var walk = Piece<TimberWalkShape>("Stair", new Vector3(-12f, 0f, 0f));
            walk.Points.Clear();
            walk.Points.AddRange(new[] { Vector3.zero, new Vector3(2f, 0f, 0f), new Vector3(7.5f, 3f, 0f), new Vector3(9f, 3f, 0f) });
            float along = deck.NearestAlong(Origin + new Vector3(-3f, 3f, 0f), out _);
            walk.End = new TimberAttach { deck = deck, along = along };
            LevelPieceBuilder.Refresh(new LevelPiece[] { deck, walk });
            var control = walk.WorldControl();

            TimberFreezer.Freeze(deck);
            Assert.IsFalse(walk.End.Attached, "the walkway still hangs on the gone deck");
            var now = walk.WorldControl();
            Assert.AreEqual(control.Count, now.Count);
            for (int i = 0; i < now.Count; i++) Assert.Less(Vector3.Distance(control[i], now[i]), 0.001f, "the walkway moved");
        }

        [Test]
        public void FrozenHouse_KeepsSolidWallsAndItsCeiling()
        {
            var house = Piece<HouseShape>("House", Vector3.zero);
            LevelPieceBuilder.Rebuild(house);
            var frozen = TimberFreezer.Freeze(house);
            Physics.SyncTransforms();

            Assert.IsNotNull(frozen.GetComponentInChildren<CourseCeiling>(), "the ceiling was lost");
            Assert.Greater(frozen.GetComponentsInChildren<TimberBoard>().Count(b => b.Solid), 20, "the walls do not collide");
            // the north wall (no door) stops a body; the south wall's door lets it through
            Assert.IsTrue(Physics.Raycast(Origin + new Vector3(0f, 1.2f, 0f), Vector3.forward, 4f, ~0, QueryTriggerInteraction.Ignore), "the north wall lets a body through");
            Assert.IsTrue(SurfaceAt(Origin + new Vector3(1f, 0f, 1f), out float floor) && floor > Origin.y - 0.05f, "no floor to walk on");
            Assert.IsNull(frozen.GetComponentsInChildren<BoxCollider>().FirstOrDefault(c => c.GetComponent<TimberElement>() == null && c.GetComponent<CourseCeiling>() == null && c.GetComponentInParent<TimberWall>() == null),
                          "a wall's old box was kept beside its boards");
        }

        [Test]
        public void Structures_BuildAndFreezeIntoSolidPostsAndPaint()
        {
            var shelter = Piece<ShelterShape>("Shelter", new Vector3(-20f, 0f, 0f));
            shelter.Drum = true;
            shelter.LowWalls = 1f;
            var gantry = Piece<GantryShape>("Gantry", new Vector3(-8f, 0f, 0f));
            var sign = Piece<SignShape>("Sign", new Vector3(4f, 0f, 0f));
            var beam = Piece<PulleyBeamShape>("Pulley beam", new Vector3(12f, 0f, 0f));
            var pieces = new LevelPiece[] { shelter, gantry, sign, beam };
            LevelPieceBuilder.Rebuild(pieces);
            foreach (var piece in pieces)
            {
                Assert.Greater(piece.Output.GetComponentsInChildren<MeshRenderer>().Length, 0, $"{piece.name} is not drawn");
                Assert.Greater(piece.Output.GetComponentsInChildren<BoxCollider>().Length, 0, $"{piece.name}'s posts do not collide");
            }
            var frozen = TimberFreezer.Freeze(pieces).ToDictionary(f => f.name);
            Assert.AreEqual(4, frozen.Count, "a structure did not freeze");
            Assert.GreaterOrEqual(frozen["Shelter"].GetComponentsInChildren<TimberBoard>().Count(b => b.Kind == TimberElementKind.Post && b.Solid), 4, "the shelter's posts are not solid");
            Assert.Greater(frozen["Shelter"].GetComponentsInChildren<TimberPath>().Count(p => p.Kind == TimberElementKind.Rope), 3, "the winch drum lost its rope");
            Assert.Greater(frozen["Sign"].GetComponentsInChildren<TimberPaint>().Length, 0, "the sign lost its glyph");
            Assert.AreEqual(1, frozen["Pulley beam"].GetComponentsInChildren<TimberTurned>().Length, "the pulley beam lost its wheel");
            foreach (var f in frozen.Values)
                Assert.IsNull(f.GetComponentsInChildren<BoxCollider>().FirstOrDefault(c => c.GetComponent<TimberElement>() == null && c.GetComponentInParent<TimberWall>() == null),
                              $"{f.name} kept a post's old box beside the post");
        }

        [Test]
        public void FreezingInPlace_DrawsTheKitsTimberAsElements()
        {
            var parent = new GameObject("Course").transform;
            var outline = new[] { new Vector2(-3f, -2f), new Vector2(3f, -2f), new Vector2(3f, 2f), new Vector2(-3f, 2f) }
                .Select(p => p + new Vector2(Origin.x, Origin.z)).ToList();
            var site = new TimberKit.Site { ground = _ => Origin.y, maxPost = 4f };
            ElementRecorder.FreezeInPlace = true;
            GameObject deck;
            try { deck = TimberKit.Deck(parent, "Deck", outline, _ => Origin.y + 3f, new TimberKit.DeckSpec { rails = true }, site); }
            finally { ElementRecorder.FreezeInPlace = false; }
            Physics.SyncTransforms();

            var frozen = deck.GetComponent<FrozenStructure>();
            Assert.IsNotNull(frozen, "the deck is not a structure");
            Assert.IsTrue(deck.GetComponentsInChildren<MeshRenderer>().All(r => r.GetComponent<TimberElement>() != null), "a merged mesh was drawn");
            Assert.Greater(deck.GetComponentsInChildren<TimberBoard>().Count(b => b.Walk == TimberWalk.Slab), 20, "the boards are not elements");
            Assert.Greater(deck.GetComponentsInChildren<TimberWall>().Length, 0, "the rail's wall is not a TimberWall");
            Assert.AreEqual(1, deck.GetComponentsInChildren<WalkableProxy>().Length, "the deck has more than its walking surface");
            Assert.IsTrue(SurfaceAt(Origin + new Vector3(0.3f, 3f, 0.4f), out float y) && Mathf.Abs(y - (Origin.y + 3f)) < 0.02f, "no walking surface at the boards' tops");
        }

        // ------------------------------------------------------------------ elements

        [Test]
        public void Catalogue_SharesOneMeshPerShape()
        {
            var a = new GameObject("A").AddComponent<TimberBoard>();
            var b = new GameObject("B").AddComponent<TimberBoard>();
            var c = new GameObject("C").AddComponent<TimberBoard>();
            a.Size = b.Size = new Vector3(0.2f, 0.05f, 2.3f);
            c.Size = new Vector3(0.2f, 0.05f, 2.4f);
            foreach (var e in new TimberElement[] { a, b, c }) TimberElementBuilder.Refresh(e);
            Assert.AreEqual(2, MeshCatalog.Flush(), "two shapes, two new meshes");
            var ma = a.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreSame(ma, b.GetComponent<MeshFilter>().sharedMesh, "the same board drew two meshes");
            Assert.AreNotSame(ma, c.GetComponent<MeshFilter>().sharedMesh, "a longer board drew the same mesh");
            Assert.IsTrue(EditorUtility.IsPersistent(ma));
        }

        [Test]
        public void Rope_FollowsWhatItHangsOn()
        {
            var post = new GameObject("Post");
            post.transform.position = Origin + new Vector3(3f, 2f, 0f);
            var rope = new GameObject("Rope").AddComponent<TimberPath>();
            rope.transform.position = Origin;
            rope.Points.Clear();
            rope.Points.AddRange(new[] { Vector3.zero, new Vector3(3f, 2f, 0f) });
            rope.Attach(false, post.transform);
            TimberElementBuilder.Refresh(rope);

            post.transform.position += new Vector3(0f, 1f, 1f);
            Assert.IsTrue(TimberElementBuilder.Refresh(rope), "the rope was not redrawn");
            Assert.Less(Vector3.Distance(rope.transform.TransformPoint(rope.Points[1]), post.transform.position), 0.001f, "the rope's end did not follow");
            Assert.Less(Vector3.Distance(rope.transform.TransformPoint(rope.Points[0]), Origin), 0.001f, "the free end moved");
        }

        [Test]
        public void SolidBoard_Collides_AndADrawnOneDoesNot()
        {
            var board = new GameObject("Board").AddComponent<TimberBoard>();
            TimberElementBuilder.Refresh(board);
            Assert.IsNull(board.GetComponent<Collider>(), "a drawn-only board collides");
            board.Solid = true;
            TimberElementBuilder.Refresh(board);
            var box = board.GetComponent<BoxCollider>();
            Assert.IsNotNull(box, "a solid board does not collide");
            Assert.AreEqual(board.Size, box.size);
            Assert.AreEqual(LayerMask.NameToLayer("Environment"), board.gameObject.layer);
        }
    }
}
