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
    /// Caves v2 (M21, ARCHITECTURE §27.6): a tunnel's clear space is exact at each of its sections and its floor is walked; a shaped cavern
    /// has its crown, a pillar that collides and a shelf that is walked (both out of its clear space); a shaft's walls rise round an open top
    /// and a Mine Frames stair is walked up; a network's tunnels follow their chambers, flare where they meet them and leave no rock in any
    /// clear space; a cave mouth makes its parts and they go with it; dressing keeps out of the walk and its large stalagmites collide.
    /// Built far from the origin, removed after; the watcher is off while they run.
    /// </summary>
    public class LevelCaveTests
    {
        const string StoreFolder = "Assets/Art/Generated/__LevelCaveTests/";
        static readonly Vector3 Origin = new Vector3(-7000f, 0f, 7000f);

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
            ground.transform.localScale = new Vector3(240f, 1f, 240f);
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

        static bool Built(Collider c) => c.GetComponentInParent<LevelPieceOutput>() != null;

        [Test]
        public void SectionClearSpace_IsExactAtEachPoint_AndItsFloorIsWalked()
        {
            var tunnel = Piece<RockShellShape>("Tunnel", Vector3.zero);
            tunnel.Points.Clear();
            tunnel.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 0f, 20f), new Vector3(0f, 0f, 40f) });
            tunnel.Sections.Add(new CaveSection(8f, 6f, CaveProfile.Round));
            tunnel.Sections.Add(new CaveSection(3f, 7f, CaveProfile.Rift));
            tunnel.Sections.Add(new CaveSection(10f, 3f, CaveProfile.Gallery));
            tunnel.Floor = true;
            LevelPieceBuilder.Rebuild(tunnel);
            Physics.SyncTransforms();
            var clear = LevelRock.Clear(tunnel);
            var world = tunnel.WorldPoints();
            for (int i = 0; i < world.Count; i++)
            {
                var sec = tunnel.Sections[i];
                // a metre inside the ends, where the frame is the point's own
                var at = world[i] + Vector3.forward * (i == 0 ? 1f : i == world.Count - 1 ? -1f : 0f);
                Assert.IsTrue(clear.Contains(at + Vector3.up * 1f), $"section {i}: its middle is not clear");
                Assert.IsTrue(clear.Contains(at + Vector3.up * (sec.height - 0.4f)), $"section {i}: not clear up to its height");
                Assert.IsFalse(clear.Contains(at + Vector3.up * (sec.height + 0.3f)), $"section {i}: clear above its height");
                Assert.IsTrue(clear.Contains(at + Vector3.right * (sec.width * 0.5f - 0.6f) + Vector3.up * 0.5f), $"section {i}: not clear near its wall");
                Assert.IsFalse(clear.Contains(at + Vector3.right * (sec.width * 0.5f + 0.3f) + Vector3.up * 0.5f), $"section {i}: clear beyond its width");
                // the rock only ever bulges outward from it
                Assert.IsTrue(Hit(at + Vector3.up * 1f, Vector3.up, 30f, out var roof) && Built(roof.collider), $"section {i}: no roof");
                Assert.GreaterOrEqual(roof.point.y - at.y, sec.height * 0.97f - 0.05f, $"section {i}: the roof comes into the clear space");
                Assert.IsTrue(Hit(at + Vector3.up * 0.5f, Vector3.right, 30f, out var wall) && Built(wall.collider), $"section {i}: no wall");
                Assert.GreaterOrEqual(wall.distance, sec.width * 0.5f - 0.7f, $"section {i}: the wall comes into the clear space");
            }
            var floor = Curves.Resample(world, 0.5f).ToList();
            var problems = WalkProbe.Run(new (string, IList<Vector3>)[] { ("tunnel floor", floor) });
            Assert.IsEmpty(problems, string.Join("\n", problems.Select(p => p.message)));
        }

        [Test]
        public void ShapedCavern_HasItsCrown_APillarThatCollides_AndAShelfThatIsWalked()
        {
            var cavern = Piece<RockShellShape>("Cavern", Vector3.zero);
            CaveEntries.Shape(cavern);
            cavern.Dressing = default;
            cavern.Floor = true;
            LevelPieceBuilder.Rebuild(cavern);
            Physics.SyncTransforms();
            Assert.IsTrue(cavern.ShapedCavern);
            var w = cavern.WorldPoints();
            var middle = new Vector3(w.Average(p => p.x), Origin.y, w.Average(p => p.z));
            var crown = middle + new Vector3(cavern.Apex.x, 0f, cavern.Apex.y);
            Assert.IsTrue(Hit(crown + Vector3.up, Vector3.up, 40f, out var top) && Built(top.collider), "no crown");
            Assert.GreaterOrEqual(top.point.y - Origin.y, cavern.ApexHeight - 0.2f, "the crown is lower than its height");
            var clear = LevelRock.Clear(cavern);
            Assert.IsTrue(clear.Contains(crown + Vector3.up * (cavern.ApexHeight - 0.5f)), "under the crown is not clear");

            var pillar = cavern.Pillars[0];
            var at = cavern.transform.TransformPoint(new Vector3(pillar.at.x, 0f, pillar.at.y));
            Assert.IsTrue(Hit(at + new Vector3(-6f, 1.5f, 0f), Vector3.right, 6f, out var side) && Built(side.collider), "the pillar does not collide");
            Assert.Greater(side.distance, 6f - pillar.radius - 0.8f);
            Assert.IsFalse(clear.Contains(at + Vector3.up * 2f), "the pillar is in the clear space");

            var shelf = cavern.Floors[0];
            var c = shelf.outline.Aggregate(Vector2.zero, (a, p) => a + p) / shelf.outline.Count;
            var on = cavern.transform.TransformPoint(new Vector3(c.x, 0f, c.y));
            Assert.IsTrue(Hit(on + Vector3.up * (shelf.height + 2f), Vector3.down, 4f, out var deck) && Built(deck.collider), "nothing to stand on at the shelf");
            Assert.AreEqual(Origin.y + shelf.height, deck.point.y, 0.05f, "the shelf is not at its height");
            Assert.IsNotNull(deck.collider.GetComponent<WalkableProxy>(), "the shelf is not walked");
            Assert.IsFalse(clear.Contains(on + Vector3.up * (shelf.height - 0.3f)), "the shelf is in the clear space");
            Assert.IsTrue(clear.Contains(on + Vector3.up * (shelf.height + 1f)), "over the shelf is not clear");
        }

        [Test]
        public void Shaft_RisesRoundAnOpenTop_AndAMineStairIsWalkedUp()
        {
            var shaft = Piece<RockShellShape>("Shaft", new Vector3(60f, 0f, 0f));
            shaft.Kind = RockShellKind.Shaft;
            shaft.Height = 20f;
            shaft.Floor = true;
            shaft.Points.Clear();
            shaft.Points.AddRange(DeckShapes.Oval(14f, 14f, 14));
            LevelPieceBuilder.Rebuild(shaft);
            var stair = Piece<MineFrameShape>("Stair", new Vector3(-30f, 0f, 0f));
            stair.Stair = true;
            stair.Width = 2.2f;
            stair.Height = 2.6f;
            stair.Spacing = 2f;
            stair.Lagging = false;
            stair.Points.Clear();
            stair.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 3f, 6f), new Vector3(0f, 6f, 12f) });
            LevelPieceBuilder.Rebuild(stair);
            Physics.SyncTransforms();

            var inside = shaft.transform.position + Vector3.up * 2f;
            Assert.IsFalse(Hit(inside, Vector3.up, 40f, out _), "the shaft's top is closed");
            Assert.IsTrue(Hit(inside + Vector3.up * 15f, Vector3.right, 20f, out var wall) && Built(wall.collider) && wall.distance > 6f, "no wall high up the shaft");
            Assert.IsFalse(LevelRock.Clear(shaft).Contains(inside + Vector3.up * 25f), "the clear space goes above the shaft");

            var line = stair.Output.GetComponent<LevelPieceOutput>().WalkLine;
            Assert.Greater(line.Length, 2, "the stair has no walk line");
            Assert.Greater(line.Max(p => p.y) - line.Min(p => p.y), 5.5f, "the stair does not climb");
            var problems = WalkProbe.Run(new (string, IList<Vector3>)[] { ("stair", line) }).Where(p => !p.message.Contains("past its end")).ToList();
            Assert.IsEmpty(problems, string.Join("\n", problems.Select(p => p.message)));
        }

        [Test]
        public void Network_TunnelsFollowTheirChambers_FlareAndLeaveNoRockInAnyClearSpace()
        {
            var network = Piece<CaveNetwork>("Network", Vector3.zero);
            var a = CaveNetworks.AddNode(network, Origin);
            var b = CaveNetworks.AddNode(network, Origin + new Vector3(45f, 0f, 0f));
            var c = CaveNetworks.AddNode(network, Origin + new Vector3(20f, 0f, 45f));
            var ab = CaveNetworks.Link(network, a, b);
            var bc = CaveNetworks.Link(network, b, c);
            var ca = CaveNetworks.Link(network, c, a);
            Assert.AreEqual(3, network.Nodes.Count());
            Assert.AreEqual(3, network.Links.Count());
            Physics.SyncTransforms();

            // a flared junction
            var control = ab.WorldPoints();
            var path = Curves.Resample(Curves.Spline(control, LevelRock.Step * 0.5f), LevelRock.Step);
            var rings = LevelRock.Rings(ab, control, path);
            Assert.Greater(rings[0].half / rings[rings.Length / 2].half, 1.3f, "the junction does not flare");

            // the way from a's middle to b's middle is open
            var from = CaveNetworks.Middle(a) + Vector3.up * 1.5f;
            var to = CaveNetworks.Middle(b) + Vector3.up * 1.5f;
            Assert.IsFalse(Physics.Linecast(from, to, out var hit, Env, QueryTriggerInteraction.Ignore) && Built(hit.collider),
                           $"the way from {a.name} to {b.name} is shut by {hit.collider?.name} at {hit.point - Origin}");

            // no rock of one cave in another's clear space
            var shells = new[] { a, b, c, ab, bc, ca };
            foreach (var shell in shells)
            {
                var others = shells.Where(s => s != shell).Select(LevelRock.Clear).ToList();
                foreach (var f in shell.Output.GetComponentsInChildren<MeshCollider>())
                {
                    var v = f.sharedMesh.vertices;
                    var t = f.sharedMesh.triangles;
                    for (int k = 0; k < t.Length; k += 3)
                    {
                        // deep in it: a skin's edge where a tunnel meets a chamber's wall at a slant may graze it
                        var mid = f.transform.TransformPoint((v[t[k]] + v[t[k + 1]] + v[t[k + 2]]) / 3f);
                        bool deep = others.Any(o => o.Contains(mid) && o.Contains(mid + Vector3.right * 0.75f) && o.Contains(mid - Vector3.right * 0.75f) &&
                                                    o.Contains(mid + Vector3.forward * 0.75f) && o.Contains(mid - Vector3.forward * 0.75f) && o.Contains(mid + Vector3.up * 0.75f));
                        Assert.IsFalse(deep, $"{f.name}: rock at {mid - Origin} in another cave's clear space");
                    }
                }
            }

            // moving a chamber moves its tunnels' ends
            var endBefore = ab.WorldPoints().Last();
            b.transform.position += new Vector3(10f, 0f, 0f);
            int rebuilt = LevelPieceBuilder.Refresh(new List<LevelPiece> { network, a, b, c, ab, bc, ca });
            Assert.AreEqual(10f, ab.WorldPoints().Last().x - endBefore.x, 0.01f, "the tunnel's end did not follow its chamber");
            Assert.GreaterOrEqual(rebuilt, 3, "the chamber and its two tunnels were not rebuilt");
        }

        [Test]
        public void CaveMouth_MakesItsParts_AndTheyGoWithIt()
        {
            // a 30° hillside rising toward +z
            var hill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hill.layer = LayerMask.NameToLayer("Environment");
            hill.transform.SetPositionAndRotation(Origin + new Vector3(0f, -4f, 30f), Quaternion.Euler(-30f, 0f, 0f));
            hill.transform.localScale = new Vector3(60f, 8f, 60f);
            Physics.SyncTransforms();
            Assert.IsTrue(Hit(Origin + new Vector3(0f, 50f, 20f), Vector3.down, 100f, out var slope));
            var mouth = CaveMouths.Make(null, slope.point, -slope.normal);
            Physics.SyncTransforms();
            Assert.IsNotNull(mouth.Tunnel.Output, "no tunnel");
            Assert.IsTrue(mouth.Tunnel.Floor, "the tunnel has no floor (the ground would not open)");
            Assert.IsNotNull(mouth.Lintel.Output, "no lintel");
            Assert.Greater(mouth.Lintel.Scree, 0f, "no scree");
            Assert.Greater(mouth.Lintel.Fuse, 0f, "no talus");
            Assert.IsNotNull(mouth.Ambience, "no ambience");
            Assert.AreEqual(2, mouth.Lanterns.Count(l => l != null), "not two lanterns");
            var into = mouth.transform.forward;
            var door = mouth.transform.position - into * 2f + Vector3.up * 2f;   // inside the tunnel's clear space: no rock there
            Assert.IsFalse(Physics.Raycast(door, into, out var shut, 10f, Env, QueryTriggerInteraction.Ignore) && Built(shut.collider),
                           $"the mouth is shut by {shut.collider?.name}");

            var parts = new Object[] { mouth.Tunnel.gameObject, mouth.Lintel.gameObject, mouth.Ambience.gameObject }.Concat(mouth.Lanterns).ToList();
            Object.DestroyImmediate(mouth.gameObject);
            Assert.IsTrue(parts.All(p => p == null), "a part outlived its mouth");
        }

        [Test]
        public void Dressing_NeverInTheWalk_AndLargeStalagmitesCollide()
        {
            var tunnel = Piece<RockShellShape>("Tunnel", Vector3.zero);
            tunnel.Points.Clear();
            tunnel.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 0f, 40f) });
            tunnel.Width = 13f;
            tunnel.Height = 7f;
            tunnel.Floor = true;
            tunnel.Dressing = new CaveDressing { stalactites = 1f, stalagmites = 1f, columns = 1f, lanternSpacing = 10f };
            LevelPieceBuilder.Rebuild(tunnel);
            Physics.SyncTransforms();
            var drawn = tunnel.Output.GetComponentsInChildren<MeshFilter>().Where(f => f.name.EndsWith("dripstone")).ToList();
            var solid = tunnel.Output.GetComponentsInChildren<MeshFilter>().Where(f => f.name.EndsWith("dripstone (solid)")).ToList();
            Assert.IsNotEmpty(drawn, "no stalactites nor small stalagmites");
            Assert.IsNotEmpty(solid, "no large stalagmites nor columns");
            Assert.IsTrue(solid.All(f => f.GetComponent<MeshCollider>() != null), "large stalagmites do not collide");
            Assert.IsTrue(drawn.All(f => f.GetComponent<Collider>() == null), "small dripstone collides");
            float x = Origin.x;
            foreach (var f in drawn)
                foreach (var v in f.sharedMesh.vertices.Select(f.transform.TransformPoint))
                    if (Mathf.Abs(v.x - x) < LevelRock.WalkClear - 1f)
                        Assert.GreaterOrEqual(v.y - Origin.y, LevelRock.StalactiteClear - 0.01f, $"dripstone at {v - Origin} hangs into the walk");
            foreach (var f in solid)
                foreach (var v in f.sharedMesh.vertices.Select(f.transform.TransformPoint))
                    if (v.y - Origin.y < LevelRock.StalactiteClear)
                        Assert.GreaterOrEqual(Mathf.Abs(v.x - x), LevelRock.Corridor - 1.3f, $"a large stalagmite at {v - Origin} stands in the corridor");
            Assert.GreaterOrEqual(tunnel.Output.GetComponentsInChildren<Transform>().Count(t => t.name.EndsWith("lantern")), 3, "no lanterns along the walk");
        }
    }
}
