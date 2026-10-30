using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HotPatata.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HotPatata.Tests
{
    /// <summary>
    /// The Level Palette and its catalogue (ARCHITECTURE §27.3, docs/LEVEL_AUTHORING.md §25): every entry the old menus had is in it and
    /// creates what it did, in its group; the generated menus match the catalogue one to one, with no shared priority in a submenu; a burst
    /// never lands on a walk line, a bridge or water; favourites and recent persist; a walkway placed on a deck's edge attaches to it.
    /// Run in the test runner's scene, far from the origin; what they add is removed after.
    /// </summary>
    public class LevelPaletteTests
    {
        const string StoreFolder = "Assets/Art/Generated/__LevelPaletteTests/";
        static readonly Vector3 Origin = new Vector3(6000f, 0f, -6000f);

        Scene scene;
        GameObject[] before;
        LevelSettings settings;
        string favourites, recent;

        [SetUp]
        public void SetUp()
        {
            LevelPieceWatcher.Enabled = false;
            MeshCatalog.Folder = StoreFolder + "Catalog/";
            favourites = EditorPrefs.GetString("HotPatata.Palette.Favourites", "");
            recent = EditorPrefs.GetString("HotPatata.Palette.Recent", "");
            // the test runner's own scene (a fresh untitled one): what the tests add is removed after
            scene = SceneManager.GetActiveScene();
            before = scene.GetRootGameObjects();
            var level = new GameObject("Level");
            settings = level.AddComponent<LevelSettings>();
            Transform Group(string n)
            {
                var g = new GameObject(n).transform;
                g.SetParent(level.transform, false);
                return g;
            }
            settings.SetGroups(Group("Timber"), Group("Trees"), Group("Kit"), Group("Props"));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.layer = LayerMask.NameToLayer("Environment");
            ground.transform.position = Origin + Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(200f, 1f, 200f);
            // the ground a burst lands on (a level's terrain)
            ground.AddComponent<WalkableProxy>().Configure(WalkableProxy.Kind.Terrain, 0f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in scene.GetRootGameObjects().Except(before)) Object.DestroyImmediate(go);
            Undo.ClearAll();
            EditorPrefs.SetString("HotPatata.Palette.Favourites", favourites);
            EditorPrefs.SetString("HotPatata.Palette.Recent", recent);
            MeshCatalog.Folder = MeshCatalog.DefaultFolder;
            AssetDatabase.DeleteAsset(StoreFolder.TrimEnd('/'));
            LevelPieceWatcher.Enabled = true;
        }

        PlaceContext At(Vector3 offset)
        {
            var c = PlaceContext.At(Origin + offset, Vector3.up);
            c.Scene = scene;
            return c;
        }

        // ------------------------------------------------------------------ the old menus, one by one

        /// <summary>
        /// Every item of the old GameObject/HotPatata Level menus (80): the entry it became, its look, what it makes (a piece type, or the
        /// prefab path an instance comes from) and the group it goes in.
        /// </summary>
        static readonly (string old, string id, string look, object made, LevelGroup? group)[] OldMenus =
        {
            ("Deck", "timber.deck", null, typeof(TimberDeckShape), LevelGroup.Timber),
            ("Walkway or Stair", "timber.walkway", null, typeof(TimberWalkShape), LevelGroup.Timber),
            ("Rope Bridge", "timber.rope-bridge", null, typeof(RopeBridgeShape), LevelGroup.Timber),
            ("Railing", "timber.railing", null, typeof(RailingShape), LevelGroup.Timber),
            ("House", "timber.house", null, typeof(HouseShape), LevelGroup.Timber),
            ("Trestle Tower", "timber.trestle", null, typeof(TrestleShape), LevelGroup.Timber),
            ("Structure/Shelter", "structure.shelter", null, typeof(ShelterShape), LevelGroup.Timber),
            ("Structure/Gantry", "structure.gantry", null, typeof(GantryShape), LevelGroup.Timber),
            ("Structure/Sign", "structure.sign", null, typeof(SignShape), LevelGroup.Timber),
            ("Structure/Pulley Beam", "structure.pulley-beam", null, typeof(PulleyBeamShape), LevelGroup.Timber),
            ("Structure/Lane Marker", "structure.lane-marker", null, typeof(LaneMarkerShape), LevelGroup.Timber),
            ("Mine/Mine Frames", "timber.mine-frames", null, typeof(MineFrameShape), LevelGroup.Timber),
            ("Mine/Mine Track", "timber.mine-track", null, typeof(MineRailShape), LevelGroup.Timber),
            ("Rock/Cave Tunnel", "rock.tunnel", null, typeof(RockShellShape), LevelGroup.Props),
            ("Rock/Cavern", "rock.cavern", null, typeof(RockShellShape), LevelGroup.Props),
            ("Rock/Cliff", "rock.cliff", null, typeof(CliffShape), LevelGroup.Props),
            ("Giant Tree", "nature.giant-tree", null, typeof(HeroTreeShape), LevelGroup.Trees),
            ("Forest Area", "nature.forest", null, typeof(ForestArea), LevelGroup.Trees),
            ("Water", "ground.water", null, typeof(WaterShape), LevelGroup.Props),
            ("Mist Zone", "nature.mist-zone", null, typeof(MistZone), LevelGroup.Props),
            ("Sound Source", "air.sound-source", null, typeof(AmbientLoop), LevelGroup.Props),
            ("Ambience Zone", "air.ambience-zone", null, typeof(AmbienceZone), LevelGroup.Props),
            ("Element/Plank", "element.plank", null, typeof(TimberBoard), LevelGroup.Timber),
            ("Element/Post", "element.post", null, typeof(TimberBoard), LevelGroup.Timber),
            ("Element/Beam", "element.beam", null, typeof(TimberBoard), LevelGroup.Timber),
            ("Element/Iron Strap", "element.iron-strap", null, typeof(TimberBoard), LevelGroup.Timber),
            ("Element/Rope", "element.rope", null, typeof(TimberPath), LevelGroup.Timber),
            ("Checkpoint (campfire)", "course.checkpoint", null, typeof(Checkpoint), LevelGroup.Kit),
            ("Finish", "course.finish", null, typeof(FinishZone), LevelGroup.Kit),
            ("Kit/Door (rises)", "kit.door", null, BombObstacleKitBuilder.Door, LevelGroup.Kit),
            ("Kit/Sliding Door", "kit.sliding-door", null, BombObstacleKitBuilder.Door, LevelGroup.Kit),
            ("Kit/Bridge (slides out)", "kit.bridge", null, BombObstacleKitBuilder.Bridge, LevelGroup.Kit),
            ("Kit/Drawbridge", "kit.drawbridge", null, KitVariantBuilder.DrawbridgePath, LevelGroup.Kit),
            ("Kit/Pivot", "kit.pivot", null, BombObstacleKitBuilder.Pivot, LevelGroup.Kit),
            ("Kit/Lift", "kit.lift", null, BombObstacleKitBuilder.Lift, LevelGroup.Kit),
            ("Kit/Moving Platform", "kit.moving-platform", null, CourseKit.PlatformsDir + "Platform_Moving", LevelGroup.Kit),
            ("Kit/Moving Boulder", "kit.moving-boulder", null, KitVariantBuilder.BoulderPath, LevelGroup.Kit),
            ("Kit/Elevator", "kit.elevator", null, CourseKit.PlatformsDir + "Platform_Elevator", LevelGroup.Kit),
            ("Kit/Falling Platform", "kit.falling-platform", null, CourseKit.PlatformsDir + "Platform_Falling", LevelGroup.Kit),
            ("Kit/Conveyor", "kit.conveyor", null, CourseKit.PlatformsDir + "Platform_Conveyor", LevelGroup.Kit),
            ("Kit/Platform", "kit.platform", null, CourseKit.PlatformsDir + "Platform_Basic", LevelGroup.Kit),
            ("Kit/Narrow Platform", "kit.narrow-platform", null, CourseKit.PlatformsDir + "Platform_Narrow", LevelGroup.Kit),
            ("Kit/Plate", "kit.plate", null, BombObstacleKitBuilder.Plate, LevelGroup.Kit),
            ("Kit/Hands-Free Plate", "kit.hands-free-plate", null, BombObstacleKitBuilder.PlateHandsFree, LevelGroup.Kit),
            ("Kit/Heavy Plate", "kit.heavy-plate", null, BombObstacleKitBuilder.PlateHeavy, LevelGroup.Kit),
            ("Kit/Hourglass Plate", "kit.hourglass-plate", null, BombObstacleKitBuilder.PlateHourglass, LevelGroup.Kit),
            ("Kit/Hoop (ring)", "kit.hoop", null, BombObstacleKitBuilder.GateRing, LevelGroup.Kit),
            ("Kit/Arch", "kit.arch", null, BombObstacleKitBuilder.GateArch, LevelGroup.Kit),
            ("Kit/Sun Beam", "kit.sun-beam", null, BombObstacleKitBuilder.Beam, LevelGroup.Kit),
            ("Kit/Switch", "kit.switch", null, BombObstacleKitBuilder.Switch, LevelGroup.Kit),
            ("Kit/Hot Zone", "kit.hot-zone", null, BombObstacleKitBuilder.ZoneHot, LevelGroup.Kit),
            ("Kit/Cold Zone", "kit.cold-zone", null, BombObstacleKitBuilder.ZoneCold, LevelGroup.Kit),
            ("Kit/Forbidden Zone", "kit.forbidden-zone", null, BombObstacleKitBuilder.ZoneForbidden, LevelGroup.Kit),
            ("Kit/Laser Curtain", "kit.laser-curtain", null, BombObstacleKitBuilder.LaserCurtain, LevelGroup.Kit),
            ("Kit/Body Screen", "kit.body-screen", null, BombObstacleKitBuilder.Screen, LevelGroup.Kit),
            ("Kit/Tube", "kit.tube", null, BombObstacleKitBuilder.Tube, LevelGroup.Kit),
            ("Kit/Cannon", "kit.cannon", null, BombObstacleKitBuilder.Cannon, LevelGroup.Kit),
            ("Kit/Hoop Obstacle", "kit.hoop-obstacle", null, CourseKit.ObstaclesDir + "Obstacle_Hoop", LevelGroup.Kit),
            ("Kit/Rotating Bar", "kit.rotating-bar", null, CourseKit.ObstaclesDir + "Obstacle_RotatingBar", LevelGroup.Kit),
            ("Kit/Sweeper", "kit.sweeper", null, CourseKit.ObstaclesDir + "Obstacle_Sweeper", LevelGroup.Kit),
            ("Kit/Piston", "kit.piston", null, CourseKit.ObstaclesDir + "Obstacle_Piston", LevelGroup.Kit),
            ("Kit/Crusher", "kit.crusher", null, CourseKit.ObstaclesDir + "Obstacle_Crusher", LevelGroup.Kit),
            ("Kit/Windmill", "kit.windmill", null, CourseKit.ObstaclesDir + "Obstacle_Windmill", LevelGroup.Kit),
            ("Kit/Launch Pad", "kit.launch-pad", null, CourseKit.ObstaclesDir + "LaunchPad", LevelGroup.Kit),
            ("Kit/Kill Zone", "course.kill-zone", null, CourseKit.GameplayDir + "KillZone", LevelGroup.Kit),
            ("Kit/Forest/Winch (hourglass plate)", "kit.hourglass-plate", "forest", ForestKitBuilder.Winch, LevelGroup.Kit),
            ("Kit/Forest/Lever (hands-free plate)", "kit.hands-free-plate", "forest", ForestKitBuilder.Lever, LevelGroup.Kit),
            ("Kit/Forest/Drawbridge", "kit.drawbridge", "forest", ForestKitBuilder.Drawbridge, LevelGroup.Kit),
            ("Kit/Forest/Cage (lift)", "kit.lift", "forest", ForestKitBuilder.Cage, LevelGroup.Kit),
            ("Kit/Forest/Signal Ring (hoop)", "kit.hoop", "forest", ForestKitBuilder.SignalRing, LevelGroup.Kit),
            ("Kit/Forest/Bell Arch (arch)", "kit.arch", "forest", ForestKitBuilder.Bell, LevelGroup.Kit),
            ("Kit/Forest/Log Chute (tube)", "kit.tube", "forest", ForestKitBuilder.Chute, LevelGroup.Kit),
            ("Kit/Forest/Rope Net (body screen)", "kit.body-screen", "forest", ForestKitBuilder.Net, LevelGroup.Kit),
            ("Kit/Forest/Embers (hot zone)", "kit.hot-zone", "forest", ForestKitBuilder.Embers, LevelGroup.Kit),
            ("Kit/Forest/Sparks (laser curtain)", "kit.laser-curtain", "forest", ForestKitBuilder.Sparks, LevelGroup.Kit),
            ("Kit/Forest/Palisade Gate (door)", "kit.door", "forest", ForestKitBuilder.Gate, LevelGroup.Kit),
            ("Kit/Forest/Gangway (bridge)", "kit.bridge", "forest", ForestKitBuilder.Gangway, LevelGroup.Kit),
            ("Kit/Forest/Log Raft (moving platform)", "kit.moving-platform", "forest", ForestKitBuilder.Raft, LevelGroup.Kit),
            ("Kit/Forest/Rotten Planks (falling platform)", "kit.falling-platform", "forest", ForestKitBuilder.RottenPlank, LevelGroup.Kit),
            ("Kit/Forest/Swinging Log (sweeper)", "kit.sweeper", "forest", ForestKitBuilder.SwingingLog, LevelGroup.Kit),
        };

        [Test]
        public void EveryOldMenuItem_IsAnEntry()
        {
            Assert.AreEqual(80, OldMenus.Length, "the old menus had 80 items");
            foreach (var (old, id, look, _, _) in OldMenus)
            {
                var entry = PaletteCatalog.CodeEntries.FirstOrDefault(e => e.Id == id);
                Assert.IsNotNull(entry, $"{old}: no entry {id}");
                if (look != null) Assert.IsTrue(entry.Looks.Any(l => l.Id == look), $"{old}: {id} has no look {look}");
            }
        }

        [Test]
        public void EveryOldMenuItem_CreatesWhatItDid_InItsGroup()
        {
            int i = 0;
            foreach (var (old, id, look, made, group) in OldMenus)
            {
                var entry = PaletteCatalog.Find(id);
                var go = PaletteCatalog.Place(entry, look, At(new Vector3((i % 9) * 18f - 72f, 0f, (i / 9) * 18f - 72f)));
                i++;
                Assert.IsNotNull(go, $"{old} made nothing");
                if (made is Type type) Assert.IsNotNull(go.GetComponent(type), $"{old} has no {type.Name}");
                else
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    string path = ((string)made).EndsWith(".prefab") ? (string)made : made + ".prefab";
                    Assert.AreEqual(path, AssetDatabase.GetAssetPath(source), $"{old} is not an instance of its prefab");
                }
                if (group.HasValue)
                {
                    var expected = settings.Group(group.Value);
                    Assert.IsTrue(go.transform.IsChildOf(expected), $"{old} is not in the {group} group (in {go.transform.parent?.name})");
                }
            }
        }

        [Test]
        public void Pieces_GoWhereTheyAlwaysWent_AboveThePointClicked()
        {
            var deck = PaletteCatalog.Place(PaletteCatalog.Find("timber.deck"), null, At(Vector3.zero));
            Assert.AreEqual(3f, deck.transform.position.y - Origin.y, 1e-3f, "a deck goes 3 m over the ground, as the menu put it");
            var tree = PaletteCatalog.Place(PaletteCatalog.Find("nature.giant-tree"), null, At(new Vector3(30f, 0f, 0f)));
            Assert.AreEqual(-0.4f, tree.transform.position.y - Origin.y, 1e-3f, "a giant tree's foot sinks 0.4 m");
            var cliff = PaletteCatalog.Place(PaletteCatalog.Find("rock.cliff"), null, At(new Vector3(-30f, 0f, 0f)));
            Assert.AreEqual(-0.5f, cliff.transform.position.y - Origin.y, 1e-3f, "a cliff sinks 0.5 m");
            var cavern = PaletteCatalog.Place(PaletteCatalog.Find("rock.cavern"), null, At(new Vector3(0f, 0f, 50f))).GetComponent<RockShellShape>();
            Assert.AreEqual(RockShellKind.Cavern, cavern.Kind);
            Assert.AreEqual(10f, cavern.Height, 1e-3f);
        }

        // ------------------------------------------------------------------ the menus

        [Test]
        public void Menus_AreWrittenFromTheCatalogue_OneToOne()
        {
            Assert.IsTrue(PaletteMenuWriter.UpToDate(), "LevelMenus.g.cs is stale: HotPatata/Level/Write Palette Menus");
            var menus = typeof(PaletteCatalog).Assembly.GetType("HotPatata.Editor.LevelMenus")
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Select(m => m.GetCustomAttribute<MenuItem>()).Where(a => a != null).ToList();
            var items = PaletteMenuWriter.Items();
            Assert.AreEqual(items.Count, menus.Count, "a menu item without an entry, or an entry without a menu item");
            foreach (var item in items)
                Assert.IsTrue(menus.Any(m => m.menuItem == item.Path && m.priority == item.Priority), $"no menu item {item.Path}");
            foreach (var entry in PaletteCatalog.CodeEntries.Where(e => e.InMenu))
                foreach (var look in entry.Looks)
                    Assert.IsTrue(items.Any(i => i.Entry == entry.Id && (i.Look ?? entry.DefaultLook.Id) == look.Id), $"{entry.Id}/{look.Id} has no menu item");
        }

        [Test]
        public void Menus_NoTwoItemsShareAPriorityInASubmenu()
        {
            foreach (var group in PaletteMenuWriter.Items().GroupBy(i => i.Path.Substring(0, i.Path.LastIndexOf('/'))))
            {
                var dupes = group.GroupBy(i => i.Priority).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                Assert.IsEmpty(dupes, $"{group.Key} has items sharing a priority");
            }
            Assert.IsEmpty(PaletteMenuWriter.Items().GroupBy(i => i.Path).Where(g => g.Count() > 1).Select(g => g.Key), "two items share a path");
        }

        [Test]
        public void PropsBrush_IsGone_ItsBrushesAreBurstPresets()
        {
            Assert.IsNull(typeof(PaletteCatalog).Assembly.GetType("HotPatata.Editor.PropBrushWindow"), "the Props Brush window is still there");
            foreach (var name in new[] { "Pebbles", "Twigs", "Roots", "Moss", "Ferns" })
                Assert.IsTrue(PaletteAssets.BurstPresets.Any(b => b.label == name), $"no {name} burst preset");
        }

        // ------------------------------------------------------------------ burst

        [Test]
        public void Burst_NeverLandsOnAWalkABridgeOrWater()
        {
            var walk = PaletteCatalog.Place(PaletteCatalog.Find("timber.walkway"), null, At(new Vector3(0f, 0f, -6f))).GetComponent<TimberWalkShape>();
            walk.Points.Clear();
            walk.Points.AddRange(new[] { Vector3.zero, new Vector3(0f, 0f, 12f) });
            walk.OnGround = true;
            LevelPieceBuilder.Rebuild(walk);
            var water = PaletteCatalog.Place(PaletteCatalog.Find("ground.water"), null, At(new Vector3(6f, 0f, 0f))).GetComponent<WaterShape>();
            Physics.SyncTransforms();
            var pebbles = new PaletteEntry { Id = "test.pebbles", Label = "Pebbles", Burst = true, BurstPrefabs = new[] { "Assets/Prefabs/Gameplay/KillZone.prefab" } };
            var placed = PalettePlacer.Dab(scene, pebbles, Origin, Vector3.up, new PalettePlacer.BurstSettings { Count = 300, Radius = 10f }, 7);
            Assert.Greater(placed.Count, 50, "the burst placed almost nothing");
            var paths = LevelForest.Paths(scene);
            var waters = LevelForest.Waters(scene);
            foreach (var go in placed)
            {
                var q = new Vector2(go.transform.position.x, go.transform.position.z);
                Assert.GreaterOrEqual(LevelForest.Distance(paths, q), PalettePlacer.BurstPathClearance, "a copy on the walkway");
                Assert.IsFalse(LevelForest.OnWater(waters, q), "a copy on the water");
            }
            Assert.AreEqual(placed.Count, PalettePlacer.Session.Count(g => g != null && placed.Contains(g)), "the session does not hold what it placed");
            int erased = PalettePlacer.Erase(Origin, 30f);
            Assert.AreEqual(placed.Count, erased, "Shift+click does not erase the burst");
            Assert.IsNotNull(water);
        }

        // ------------------------------------------------------------------ favourites, recent, snapping

        [Test]
        public void FavouritesAndRecent_Persist()
        {
            PaletteCatalog.SetFavourite("timber.house", true);
            Assert.IsTrue(PaletteCatalog.Favourites().Contains("timber.house"));
            PaletteCatalog.SetFavourite("timber.house", false);
            Assert.IsFalse(PaletteCatalog.IsFavourite("timber.house"));
            for (int i = 0; i < PaletteCatalog.RecentCount + 3; i++) PaletteCatalog.Remember("test." + i);
            PaletteCatalog.Place(PaletteCatalog.Find("structure.sign"), null, At(Vector3.zero));
            var recentList = PaletteCatalog.Recent();
            Assert.AreEqual("structure.sign", recentList[0], "the last placed entry is not first in Recent");
            Assert.AreEqual(PaletteCatalog.RecentCount, recentList.Count, "Recent keeps its last 12");
        }

        [Test]
        public void Walkway_PlacedOnADeckEdge_AttachesToIt()
        {
            var deck = PaletteCatalog.Place(PaletteCatalog.Find("timber.deck"), null, At(Vector3.zero)).GetComponent<TimberDeckShape>();
            Physics.SyncTransforms();
            float along = 0.1f;
            var edge = deck.EdgePoint(along, out var outward);
            var entry = PaletteCatalog.Find("timber.walkway");
            var snap = PalettePlacer.FindSnap(scene, entry, edge + outward * 0.6f);
            Assert.AreEqual(PaletteSnap.DeckEdge, snap.Kind, "no deck edge found near the mouse");
            var c = At(Vector3.zero);
            c.Point = snap.Point;
            c.Snapped = snap;
            var walk = PaletteCatalog.Place(entry, null, c).GetComponent<TimberWalkShape>();
            Assert.AreEqual(deck, walk.Start.deck, "the walkway's start is not on the deck");
            Assert.Greater(Vector3.Dot(walk.transform.forward, outward), 0.9f, "the walkway does not run off the deck");
            Assert.IsTrue(TimberLinks.OpeningsOf(deck).Any(), "the deck's rail is not opened where the walkway arrives");
            PalettePlacer.Disabled = PaletteSnap.DeckEdge;
            try { Assert.IsFalse(PalettePlacer.FindSnap(scene, entry, edge + outward * 0.6f).Found, "the deck edge snap is off but still snaps"); }
            finally { PalettePlacer.Disabled = PaletteSnap.None; }
        }
    }
}
