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
    /// Walls (M21, ARCHITECTURE §27.7): every style builds on a three-point line with an opening of each passage, the kit placed, sized
    /// and kept (with its wiring) by a rebuild; a stone wall collides in runs up to its locked height and is open where its openings are;
    /// mantle, climb and lock read from the height; Verify finds a bomb-only opening without its screen and an unwired door.
    /// Built far from the origin, removed after; the watcher is off while they run.
    /// </summary>
    public class LevelWallTests
    {
        const string StoreFolder = "Assets/Art/Generated/__LevelWallTests/";
        static readonly Vector3 Origin = new Vector3(6000f, 0f, -6000f);

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
            ground.transform.localScale = new Vector3(200f, 1f, 600f);
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

        WallShape Wall(string name, Vector3 offset, WallStyle style)
        {
            var go = new GameObject(name);
            go.transform.position = Origin + offset;
            var wall = go.AddComponent<WallShape>();
            wall.EnsureId();
            WallEntries.Dress(wall, style);
            return wall;
        }

        static int Env => LayerMask.GetMask("Environment");

        static bool Hit(Vector3 from, Vector3 dir, float reach, out RaycastHit hit) =>
            Physics.Raycast(from, dir, out hit, reach, Env, QueryTriggerInteraction.Ignore);

        [Test]
        public void EveryStyle_BuildsWithAnOpeningOfEachPassage_ItsKitPlacedSizedAndKeptWired()
        {
            StoneKitBuilder.EnsureBuilt();
            var plate = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/PressurePlate.prefab"));
            plate.transform.position = Origin + new Vector3(-20f, 0f, 0f);
            var source = plate.GetComponents<MonoBehaviour>().First(m => m is ISignalSource);
            var styles = (WallStyle[])System.Enum.GetValues(typeof(WallStyle));
            for (int k = 0; k < styles.Length; k++)
            {
                var wall = Wall(styles[k].ToString(), new Vector3(0f, 0f, -250f + k * 32f), styles[k]);
                wall.Points.Clear();
                wall.Points.AddRange(new[] { new Vector3(-12f, 0f, 0f), new Vector3(0f, 0f, 3f), new Vector3(12f, 0f, 0f) });
                float h = Mathf.Max(0.8f, wall.Height - 0.3f);
                wall.Openings.Add(new WallOpening(3f, 1.4f, h, OpeningPassage.Free, OpeningShape.RoundArch));
                wall.Openings.Add(new WallOpening(8.5f, 2.4f, h, OpeningPassage.BombOnly));
                wall.Openings.Add(new WallOpening(15f, 2.4f, h, OpeningPassage.RunnersOnly));
                wall.Openings.Add(new WallOpening(21f, 2.6f, h, OpeningPassage.KitDoor) { look = k % 2 == 0 ? StoneKitBuilder.Portcullis : "" });
                LevelPieceBuilder.Rebuild(wall);
                Assert.IsNotNull(wall.Output, $"{styles[k]}: nothing built");
                Assert.IsNotEmpty(wall.Output.GetComponentsInChildren<BoxCollider>(true), $"{styles[k]}: no collision");
                Assert.IsNotEmpty(wall.Output.GetComponentsInChildren<Renderer>(true), $"{styles[k]}: nothing drawn");

                var screen = wall.Openings[1].kit;
                Assert.IsNotNull(screen, $"{styles[k]}: no body screen");
                var screenBox = screen.GetComponentInChildren<BodyScreen>(true).GetComponentInChildren<BoxCollider>(true);
                Assert.AreEqual(2.4f, screenBox.size.x, 0.01f, $"{styles[k]}: the screen is not the opening's width");
                Assert.IsNotNull(wall.Openings[2].kit?.GetComponentInChildren<BombBarrier>(true), $"{styles[k]}: no curtain");
                var door = wall.Openings[3].kit;
                Assert.IsNotNull(door, $"{styles[k]}: no door");
                var actuator = door.GetComponent<SignalActuator>();
                var doorBox = actuator.Platform.GetComponentsInChildren<BoxCollider>(true).First(c => !c.isTrigger && c.GetComponent<KillZone>() == null);
                Assert.AreEqual(2.6f, doorBox.size.x, 0.01f, $"{styles[k]}: the door is not the opening's width");
                if (k % 2 == 0)
                    Assert.AreEqual(StoneKitBuilder.Portcullis, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(door), "the look chosen");

                // wire the door, change the wall: the same kit, moved and still wired
                var so = new SerializedObject(actuator);
                so.FindProperty("source").objectReferenceValue = source;
                so.ApplyModifiedPropertiesWithoutUndo();
                var at = door.transform.position;
                wall.transform.position += new Vector3(1f, 0f, 0f);
                wall.Height += 0.2f;
                LevelPieceBuilder.Rebuild(wall);
                Assert.AreSame(door, wall.Openings[3].kit, $"{styles[k]}: the door was made again");
                Assert.AreSame(screen, wall.Openings[1].kit, $"{styles[k]}: the screen was made again");
                Assert.AreEqual(1f, door.transform.position.x - at.x, 0.01f, $"{styles[k]}: the door did not follow its wall");
                Assert.AreSame(source, door.GetComponent<SignalActuator>().Source as MonoBehaviour, $"{styles[k]}: the door lost its wiring");
            }
        }

        [Test]
        public void StoneWall_CollidesInRunsUpToItsTop_OpenWhereItsOpeningsAre()
        {
            var wall = Wall("Dry stone", Vector3.zero, WallStyle.DryStone);
            wall.Points.Clear();
            wall.Points.AddRange(new[] { new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f) });
            wall.Height = 2f;
            wall.Openings.Add(new WallOpening(10f, 1.6f, 1.8f, OpeningPassage.Free, OpeningShape.RoundArch) { frame = OpeningFrame.Stone });
            LevelPieceBuilder.Rebuild(wall);
            Physics.SyncTransforms();
            var boxes = wall.Output.GetComponentsInChildren<BoxCollider>(true);
            Assert.IsTrue(boxes.All(b => b.size.z <= MasonryKit.MaxSolid + 0.01f), "a collision box longer than a run");
            Assert.IsTrue(Hit(Origin + new Vector3(-5f, 1f, -3f), Vector3.forward, 6f, out _), "the wall does not stop a body");
            Assert.IsFalse(Hit(Origin + new Vector3(-5f, 2.3f, -3f), Vector3.forward, 6f, out _), "the wall collides above its top");
            Assert.IsFalse(Hit(Origin + new Vector3(0f, 0.8f, -3f), Vector3.forward, 6f, out var shut), $"the opening is shut by {shut.collider?.name}");
            Assert.AreEqual(2f, MasonryKit.LockedHeight(wall), 0.01f);
            StringAssert.StartsWith("climbed", MasonryKit.Reads(2f));
            StringAssert.StartsWith("mantled", MasonryKit.Reads(1.1f));
            StringAssert.StartsWith("a lock", MasonryKit.Reads(3.6f));

            var ruin = Wall("Ruin", new Vector3(0f, 0f, 20f), WallStyle.Ruined);
            LevelPieceBuilder.Rebuild(ruin);
            float locked = MasonryKit.LockedHeight(ruin);
            Assert.Less(locked, ruin.Height - 1f, "a ruined wall's breach does not lower its locked height");
            Assert.IsTrue(ruin.Output.GetComponentsInChildren<BoxCollider>(true).All(b => b.bounds.max.y <= ruin.transform.position.y + ruin.Height + 0.01f));
        }

        [Test]
        public void Verify_FindsABombOnlyOpeningWithoutItsScreen_AndAnUnwiredDoor()
        {
            var wall = Wall("Checked wall", new Vector3(0f, 0f, 60f), WallStyle.Mortared);
            wall.Height = 3f;
            wall.Openings.Add(new WallOpening(4f, 2f, 2.4f, OpeningPassage.BombOnly));
            wall.Openings.Add(new WallOpening(9f, 2.4f, 2.6f, OpeningPassage.KitDoor));
            LevelPieceBuilder.Rebuild(wall);
            List<LevelFinding> Check()
            {
                var all = new List<LevelFinding>();
                LevelVerifier.Walls(scene, all);
                return all.Where(f => f.message.StartsWith(wall.name)).ToList();
            }
            var findings = Check();
            Assert.IsFalse(findings.Any(f => f.message.Contains("opening 1")), string.Join("\n", findings.Select(f => f.message)));
            Assert.IsTrue(findings.Any(f => f.message.Contains("opening 2") && f.message.Contains("unwired")), "an unwired door is not reported");
            Object.DestroyImmediate(wall.Openings[0].kit);
            findings = Check();
            Assert.IsTrue(findings.Any(f => f.severity == LevelFinding.Severity.Error && f.message.Contains("opening 1")), "a bomb-only opening with no screen is not reported");
        }

        [Test]
        public void StoneLooks_KeepTheContract_AndTheirBase()
        {
            StoneKitBuilder.EnsureBuilt();
            var door = AssetDatabase.LoadAssetAtPath<GameObject>(BombObstacleKitBuilder.Door + ".prefab").GetComponent<SignalActuator>();
            foreach (var path in StoneKitBuilder.Variants())
            {
                var look = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(look, path);
                Assert.IsEmpty(ObstaclePrefabValidator.Validate(look), path);
                Assert.IsTrue(PrefabUtility.IsPartOfVariantPrefab(look), $"{path} is not a variant");
                Assert.AreEqual("Actuator_Door", PrefabUtility.GetCorrespondingObjectFromSource(look).name);
                var a = look.GetComponent<SignalActuator>();
                Assert.IsNotNull(a.LethalEdge, $"{path}: no lethal lower edge");
                Assert.IsNotNull(a.LethalEdge.GetComponent<KillZone>(), $"{path}: its lower edge does not kill");
                Assert.AreEqual(door.TravelSeconds, a.TravelSeconds, $"{path}: not the base's timing");
                Assert.IsNotNull(a.Platform.GetComponentInChildren<CustomVisual>(true), $"{path}: no look of its own");
                Assert.IsTrue(a.Platform.GetComponentsInChildren<KitSkin>(true).All(s => s.GetComponent<Renderer>() == null || !s.GetComponent<Renderer>().enabled || !s.gameObject.activeSelf),
                              $"{path}: the kit box still shows");
                var open = a.WaypointOpen.localPosition - a.WaypointClosed.localPosition;
                if (path.Contains("Sliding")) Assert.Greater(Mathf.Abs(open.x), Mathf.Abs(open.y), $"{path} does not slide");
                else Assert.Greater(open.y, 1f, $"{path} does not rise");
            }
        }
    }
}
