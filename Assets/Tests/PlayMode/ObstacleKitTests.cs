using System.Collections;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// The obstacle systems as reusable pieces (docs/OBSTACLES.md): each kit prefab is dropped on a test floor beside PassSandbox and
    /// must do its job alone (belts carry, elevators and pistons lift, sweepers and crushers are lethal only to what they should
    /// catch), and the logic must not care what a prefab looks like or how its children are named: a variant with another visual,
    /// a variant with another motion, a subclass overriding the path, and a visual driven only by the obstacle's state.
    /// </summary>
    public class ObstacleKitTests : SandboxTestBase
    {
        static readonly Vector3 Area = new Vector3(120f, 0f, 0f);   // clear of the sandbox (|x| <= 50)

        RunManager run;

        [UnitySetUp]
        public IEnumerator Find()
        {
            run = RunManager.Instance;
            bomb.Fuse.SetDurationOverride(999f);   // these tests wait on obstacles, not on the fuse
            Floor(Area + new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 60f));
            yield break;
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Floor(Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TestFloor";
            go.layer = LayerMask.NameToLayer("Environment");
            go.transform.position = center;
            go.transform.localScale = size;
            Physics.SyncTransforms();
            return go;
        }

        static GameObject Spawn(string path, Vector3 position)
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            var go = Object.Instantiate(prefab, position, Quaternion.identity);
            Physics.SyncTransforms();
            return go;
#else
            Assert.Inconclusive("Editor only");
            return null;
#endif
        }

        static void Set(Object target, string field, System.Action<object> set)
        {
#if UNITY_EDITOR
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            Assert.IsNotNull(p, field);
            set(p);
            so.ApplyModifiedPropertiesWithoutUndo();
#endif
        }

#if UNITY_EDITOR
        static System.Action<object> Float(float v) => p => ((SerializedProperty)p).floatValue = v;
        static System.Action<object> Ref(Object v) => p => ((SerializedProperty)p).objectReferenceValue = v;
#endif

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static Vector3 TopOf(GameObject go)
        {
            var b = go.GetComponentInChildren<Collider>().bounds;
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        // ------------------------------------------------------------------ the kit, one piece at a time

        [UnityTest]
        public IEnumerator Conveyor_CarriesAnIdlePlayer_TheWayItRuns()
        {
            var belt = Spawn("Assets/Prefabs/Platforms/Platform_Conveyor.prefab", Area + new Vector3(0f, 0.5f, 0f));
            belt.transform.localScale = new Vector3(4f, 1f, 20f);
            Physics.SyncTransforms();
            yield return Place(p1, TopOf(belt) + new Vector3(0f, 0.05f, -6f));
            float z0 = p1.transform.position.z;
            yield return WaitSeconds(1f);
            Assert.Greater(p1.transform.position.z - z0, 2f, "the belt carries the player forward (3 m/s)");
#if UNITY_EDITOR
            Set(belt.GetComponent<Conveyor>(), "speed", Float(-4f));
#endif
            z0 = p1.transform.position.z;
            yield return WaitSeconds(1f);
            Assert.Less(p1.transform.position.z - z0, -2.5f, "a reversed belt carries the player back (4 m/s)");
            Assert.AreEqual(0, run.ResetCount);
        }

        [UnityTest]
        public IEnumerator Elevator_LiftsARider()
        {
            var lift = Spawn("Assets/Prefabs/Platforms/Platform_Elevator.prefab", Area + new Vector3(0f, -0.25f, 0f));
            var mover = lift.GetComponent<MovingPlatform>();
            float travel = mover.WaypointB.position.y - mover.WaypointA.position.y;
            yield return WaitUntil(() => mover.Platform.position.y < mover.WaypointA.position.y + 0.05f, 8f, "the elevator never came down");
            yield return Place(p1, mover.Platform.position + new Vector3(0f, 0.3f, 0f));
            float start = p1.transform.position.y, top = start;
            float end = Time.time + 6f;
            while (Time.time < end)
            {
                top = Mathf.Max(top, p1.transform.position.y);
                yield return null;
            }
            Assert.Greater(top - start, travel - 0.3f, "the rider went all the way up");
            Assert.AreEqual(0, run.ResetCount, "riding an elevator is safe");
        }

        [UnityTest]
        public IEnumerator PistonTile_CarriesARiderUpAndDown()
        {
            var piston = Spawn("Assets/Prefabs/Obstacles/Obstacle_Piston.prefab", Area + new Vector3(0f, -0.5f, 0f));
            var mover = piston.GetComponent<MovingPlatform>();
            float travel = mover.WaypointB.position.y - mover.WaypointA.position.y;
            yield return WaitUntil(() => mover.Platform.position.y < mover.WaypointA.position.y + 0.05f, 8f, "the tile never came down");
            yield return Place(p1, mover.Platform.position + new Vector3(0f, 0.6f, 0f));
            float start = p1.transform.position.y, top = start;
            float end = Time.time + 6f;
            while (Time.time < end)
            {
                top = Mathf.Max(top, p1.transform.position.y);
                yield return null;
            }
            Assert.Greater(top - start, travel - 0.3f, "rode the tile to its high stop");
            Assert.AreEqual(0, run.ResetCount);
        }

        [UnityTest]
        public IEnumerator Sweeper_FailsTheSection_ForAPlayerStandingInItsPath()
        {
            Spawn("Assets/Prefabs/Obstacles/Obstacle_Sweeper.prefab", Area);
            yield return Place(p1, Area + new Vector3(4f, 0.05f, 0f));
            yield return WaitUntil(() => run.ResetCount == 1, 5f, "the sweeper should hit a player who stands still");
        }

        [UnityTest]
        public IEnumerator Sweeper_CanBeJumped()
        {
            var sweeper = Spawn("Assets/Prefabs/Obstacles/Obstacle_Sweeper.prefab", Area).GetComponent<RotatingObstacle>();
            Vector3 spot = Area + new Vector3(4f, 0f, 0f);   // on the +x axis: the bar is here when the angle is 0 or 180
            // Step in while the bar is well clear of the spot (passed it at least 30 deg ago, back in 60 deg or more).
            yield return WaitUntil(() => DegreesUntilBar(sweeper) > 60f && DegreesUntilBar(sweeper) < 150f, 3f, "the bar never moved away");
            yield return Place(p1, spot + new Vector3(0f, 0.05f, 0f));

            // Jump so that the apex (0.34 s) is when the bar passes; the bar needs ~0.2 s to cross a standing player.
            for (int pass = 0; pass < 2; pass++)
            {
                yield return WaitUntil(() => DegreesUntilBar(sweeper) < 32f && DegreesUntilBar(sweeper) > 26f, 5f, "never lined up the jump");
                Drive.PressJump();
                yield return WaitSeconds(0.9f);
            }
            Assert.AreEqual(0, run.ResetCount, "well-timed jumps clear the knee-high bar");
        }

        // Degrees the sweeper still has to turn before its bar crosses the +x axis again (bar is symmetric: every 180).
        static float DegreesUntilBar(RotatingObstacle r) => 180f - Mathf.Repeat(r.CurrentAngle, 180f);

        GameObject Crusher()
        {
            // Root at the up pose: the slab's underside stops 1.45 m above the floor when down (CourseKit.AddCrusher).
            return Spawn("Assets/Prefabs/Obstacles/Obstacle_Crusher.prefab", Area + new Vector3(0f, 4.95f, 0f));
        }

        [UnityTest]
        public IEnumerator Crusher_CatchesAStandingPlayer()
        {
            Crusher();
            yield return Place(p1, Area + new Vector3(0f, 0.05f, 0f));
            yield return WaitUntil(() => run.ResetCount == 1, 6f, "standing under the crusher should fail the section");
        }

        [UnityTest]
        public IEnumerator Crusher_SparesACrouchedPlayer()
        {
            var mover = Crusher().GetComponent<MovingPlatform>();
            var slab = mover.Platform;
            float high = mover.WaypointA.position.y;
            // Step in at the start of the crusher's wait at the top, then crouch before it comes down.
            yield return WaitUntil(() => slab.position.y < high - 0.5f, 6f, "the crusher never came down");
            yield return WaitUntil(() => slab.position.y >= high - 0.01f, 6f, "the crusher never went back up");
            Drive.Crouch = true;
            yield return Place(p1, Area + new Vector3(0f, 0.05f, 0f));
            yield return WaitSeconds(6f);   // two full crusher cycles
            Assert.AreEqual(0, run.ResetCount, "a crouched player (1.0 m) fits under the lowered crusher");
            Drive.Crouch = false;
        }

        // ------------------------------------------------------------------ generic: any look, any motion, any names

        [UnityTest]
        public IEnumerator AVariantWithItsOwnVisual_StillMovesAndCarries()
        {
            var go = Spawn("Assets/Prefabs/Variants/Platform_Moving_Boulder.prefab", Area + new Vector3(0f, 1f, 10f));
            var mover = go.GetComponent<MovingPlatform>();
            Assert.IsNotNull(go.GetComponentInChildren<CustomVisual>(), "the variant draws its own model");
            Assert.IsNull(go.GetComponentInChildren<CustomVisual>().GetComponentInChildren<Collider>(), "the model has no collider");
            yield return Place(p1, TopOf(mover.Platform.gameObject) + new Vector3(0f, 0.05f, 0f));
            Vector3 start = mover.Platform.position;
            Vector3 offset = Flat(p1.transform.position - mover.Platform.position);
            yield return WaitSeconds(1.2f);
            Assert.Greater(Vector3.Distance(start, mover.Platform.position), 1.5f, "the platform travelled");
            Assert.Less(Vector3.Distance(offset, Flat(p1.transform.position - mover.Platform.position)), 0.4f, "the rider stayed on the boulder");
            Assert.IsTrue(p1.Motor.Grounded);
        }

        [UnityTest]
        public IEnumerator ADrawbridgeVariant_StandsUp_ThenLiesAcrossWhileItsPlateIsHeld()
        {
            var bridge = Spawn("Assets/Prefabs/Variants/Actuator_Bridge_Drawbridge.prefab", Area + new Vector3(-8f, -0.25f, 10f)).GetComponent<SignalActuator>();
            var plate = Spawn("Assets/Prefabs/Gameplay/PressurePlate.prefab", Area + new Vector3(6f, 0f, 10f));
#if UNITY_EDITOR
            Set(bridge, "source", Ref(plate.GetComponent<PressurePlate>()));
#endif
            var deck = bridge.Platform.GetComponentInChildren<BoxCollider>();
            yield return WaitSeconds(0.3f);
            Assert.Greater(deck.bounds.size.y, 5f, "closed, the bridge stands up");

            yield return Place(p1, plate.transform.position + new Vector3(0f, 0.05f, 0f));
            yield return WaitSeconds(bridge.TravelSeconds + 0.4f);
            Assert.Less(deck.bounds.size.y, 1f, "held open, the bridge lies flat");
            Assert.Greater(deck.bounds.size.z, 5f, "and reaches across");
            Assert.AreEqual(Area.y - 0.25f, deck.bounds.center.y, 0.3f, "at the hinge's height");
            Assert.AreEqual(0, run.ResetCount);
        }

        /// <summary>A platform whose path is overridden in code: it arcs 2 m up between its waypoints.</summary>
        class ArcPlatform : MovingPlatform
        {
            protected override Vector3 PositionAt(Vector3 a, Vector3 b, float u) => Vector3.Lerp(a, b, u) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * 2f);
        }

        [UnityTest]
        public IEnumerator ASubclass_OverridesThePath_AndAVisualFollowsTheState_WhateverTheNames()
        {
            // Built from scratch with names the kit never uses: only the references matter.
            var root = new GameObject("Hovering thing");
            root.transform.position = Area + new Vector3(10f, 1f, -10f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Whatever";
            body.layer = LayerMask.NameToLayer("Environment");
            body.transform.SetParent(root.transform, false);
            var rb = body.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var from = new GameObject("here").transform;
            from.SetParent(root.transform, false);
            var to = new GameObject("there").transform;
            to.SetParent(root.transform, false);
            to.localPosition = new Vector3(0f, 0f, 8f);
            root.SetActive(false);
            var arc = root.AddComponent<ArcPlatform>();
#if UNITY_EDITOR
            Set(arc, "platform", Ref(body.transform));
            Set(arc, "waypointA", Ref(from));
            Set(arc, "waypointB", Ref(to));
            Set(arc, "speed", Float(4f));
#endif
            var driver = body.AddComponent<ObstacleVisualDriver>();   // finds the ArcPlatform up the hierarchy
            int on = 0, off = 0;
            float lastProgress = -1f;
            driver.onActivated.AddListener(() => on++);
            driver.onDeactivated.AddListener(() => off++);
            driver.onProgress.AddListener(p => lastProgress = p);
            root.SetActive(true);

            float highest = float.MinValue;
            float end = Time.time + 4.5f;   // more than a full cycle (16 m at 4 m/s)
            while (Time.time < end)
            {
                float u = arc.Progress;
                float straight = Mathf.Lerp(from.position.y, to.position.y, u);
                highest = Mathf.Max(highest, body.transform.position.y - straight);
                yield return null;
            }
            Assert.Greater(highest, 1.8f, "the overridden path arcs above the straight line");
            Assert.GreaterOrEqual(on, 1, "the visual heard the platform turn towards B");
            Assert.GreaterOrEqual(off, 1, "and back towards A");
            Assert.AreEqual(arc.Progress, lastProgress, 0.05f, "the visual reads the same progress as the logic");
        }

        [UnityTest]
        public IEnumerator AVisualDriver_HearsAPlateSwitchOnAndOff()
        {
            var plate = Spawn("Assets/Prefabs/Gameplay/PressurePlate.prefab", Area + new Vector3(-10f, 0f, -10f));
            var driver = plate.AddComponent<ObstacleVisualDriver>();
            int on = 0, off = 0;
            driver.onActivated.AddListener(() => on++);
            driver.onDeactivated.AddListener(() => off++);
            yield return WaitSeconds(0.2f);
            yield return Place(p1, plate.transform.position + new Vector3(0f, 0.05f, 0f));
            yield return WaitUntil(() => on == 1, 2f, "stepping on the plate switches it on");
            yield return Place(p1, plate.transform.position + new Vector3(5f, 0.05f, 0f));
            yield return WaitUntil(() => off == 1, 2f, "stepping off switches it off");
        }
    }
}
