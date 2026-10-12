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
    /// Riding a fast vertical carrier (#97). The courses' lifts peak at 2.5 to 9 m/s, faster than the motor's ground stick:
    /// a rider must stay grounded and on the lift on EVERY frame, at any frame rate, up, down and through a reversal, and
    /// never fall through it. A pass between two riders of the same lift is caught (the throw keeps the lift's velocity,
    /// PROJECT_SPEC §8.2), and a jump off a rising lift keeps its rise (PROJECT_SPEC §11).
    /// The frame rate is pinned with Time.captureFramerate, so a run is the same on any machine.
    /// </summary>
    public class VerticalCarrierRideTests : SandboxTestBase
    {
        static readonly Vector3 Area = new Vector3(-120f, 0f, 0f);   // clear of the sandbox (|x| <= 50)
        const float Settle = 0.1f;          // seconds after boarding before every frame must be clean
        const float SinkTolerance = 0.05f;  // metres between the feet and the drawn deck top

        /// <summary>A plain test source for an actuator: on while <see cref="On"/>.</summary>
        public class TestSignal : MonoBehaviour, ISignalSource
        {
            public bool On;
            public bool Active => On;
        }

        RunManager run;
        int explosions, catches;

        [UnitySetUp]
        public IEnumerator Prepare()
        {
            run = RunManager.Instance;
            bomb.Fuse.SetDurationOverride(999f);
            explosions = catches = 0;
            bomb.BombExploded += (reason, detail) => explosions++;
            bomb.BombCaught += receiver => catches++;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.layer = LayerMask.NameToLayer("Environment");
            floor.transform.position = Area + new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(30f, 1f, 30f);
            Physics.SyncTransforms();
            yield break;
        }

        [TearDown]
        public void RestoreFrameRate() => Time.captureFramerate = 0;

        // ------------------------------------------------------------------ fixtures

        /// <summary>An Actuator_Lift with a 6 x 0.5 x 6 deck rising <paramref name="travel"/> m in <paramref name="seconds"/> s (peak 1.5 × travel / seconds).</summary>
        SignalActuator Lift(float travel, float seconds, out TestSignal signal)
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Platforms/Actuator_Lift.prefab");
            Assert.IsNotNull(prefab);
            var go = Object.Instantiate(prefab, Area + new Vector3(0f, 0.25f, 0f), Quaternion.identity);
            var lift = go.GetComponent<SignalActuator>();
            var deckSize = new Vector3(6f, 0.5f, 6f);
            lift.Platform.Find("Visual").localScale = deckSize;
            lift.Platform.Find("Collision").GetComponent<BoxCollider>().size = deckSize;
            lift.WaypointOpen.localPosition = new Vector3(0f, travel, 0f);
            signal = go.AddComponent<TestSignal>();
            var so = new SerializedObject(lift);
            so.FindProperty("source").objectReferenceValue = signal;
            so.FindProperty("travelSeconds").floatValue = seconds;
            so.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();
            return lift;
#else
            Assert.Inconclusive("Editor only");
            signal = null;
            return null;
#endif
        }

        static float DeckTop(SignalActuator lift) => lift.Platform.position.y + 0.25f;

        /// <summary>
        /// Watches <paramref name="p"/> on <paramref name="lift"/> until <paramref name="done"/>: counts the frames where they are
        /// not grounded on it and the worst gap between their feet and the drawn deck.
        /// </summary>
        static IEnumerator Watch(Player p, SignalActuator lift, System.Func<bool> done, float timeout, int[] offFrames, float[] worstGap)
        {
            float start = Time.time;
            float rest = p.transform.position.y - DeckTop(lift);   // how a rider stands on a still deck (the controller's skin)
            while (!done())
            {
                if (Time.time - start > timeout) Assert.Fail("the lift never got there");
                yield return null;
                if (Time.time - start < Settle) continue;
                if (!p.Motor.Grounded || !ReferenceEquals(p.Motor.RidingCarrier, lift)) offFrames[0]++;
                worstGap[0] = Mathf.Max(worstGap[0], Mathf.Abs(p.transform.position.y - DeckTop(lift) - rest));
            }
        }

        IEnumerator RideUp(int fps, float travel, float seconds)
        {
            Time.captureFramerate = fps;
            var lift = Lift(travel, seconds, out var signal);
            yield return null;
            yield return Place(p1, new Vector3(Area.x, DeckTop(lift) + 0.05f, Area.z));
            yield return WaitSeconds(0.2f);

            signal.On = true;
            var off = new int[1];
            var gap = new float[1];
            yield return Watch(p1, lift, () => lift.CurrentProgress >= 1f, seconds + 2f, off, gap);
            yield return WaitSeconds(0.2f);

            Assert.AreEqual(0, off[0], $"{fps} fps, {1.5f * travel / seconds:F1} m/s: frames off the lift");
            Assert.Less(gap[0], SinkTolerance, $"{fps} fps: the feet left the drawn deck by {gap[0] * 100f:F1} cm");
            Assert.Greater(p1.transform.position.y, DeckTop(lift) - SinkTolerance, "rode it to the top");
            Assert.AreEqual(0, run.ResetCount, "riding a lift is safe");
        }

        // ------------------------------------------------------------------ up, at the courses' speeds and frame rates

        [UnityTest] public IEnumerator RisingLift_9ms_30fps() => RideUp(30, 30f, 5f);
        [UnityTest] public IEnumerator RisingLift_9ms_60fps() => RideUp(60, 30f, 5f);
        [UnityTest] public IEnumerator RisingLift_9ms_144fps() => RideUp(144, 30f, 5f);
        [UnityTest] public IEnumerator RisingLift_7ms_60fps() => RideUp(60, 14f, 3f);
        [UnityTest] public IEnumerator RisingLift_4ms_144fps() => RideUp(144, 7f, 2.5f);

        // ------------------------------------------------------------------ down and through reversals

        [UnityTest] public IEnumerator DescendingLift_30fps() => RideDown(30);
        [UnityTest] public IEnumerator DescendingLift_144fps() => RideDown(144);

        IEnumerator RideDown(int fps)
        {
            Time.captureFramerate = fps;
            var lift = Lift(14f, 3f, out var signal);
            signal.On = true;
            yield return WaitUntil(() => lift.CurrentProgress >= 1f, 5f, "the lift never rose");
            yield return Place(p1, new Vector3(Area.x, DeckTop(lift) + 0.05f, Area.z));
            yield return WaitSeconds(0.2f);

            signal.On = false;
            var off = new int[1];
            var gap = new float[1];
            yield return Watch(p1, lift, () => lift.CurrentProgress <= 0f, 5f, off, gap);
            Assert.AreEqual(0, off[0], "frames off the lift on the way down");
            Assert.Less(gap[0], SinkTolerance, "floated over (or sank into) a descending deck");
            Assert.AreEqual(0, run.ResetCount);
        }

        [UnityTest] public IEnumerator LiftReversingMidTravel_30fps() => RideReversals(30);
        [UnityTest] public IEnumerator LiftReversingMidTravel_144fps() => RideReversals(144);

        IEnumerator RideReversals(int fps)
        {
            Time.captureFramerate = fps;
            var lift = Lift(14f, 3f, out var signal);
            yield return null;
            yield return Place(p1, new Vector3(Area.x, DeckTop(lift) + 0.05f, Area.z));
            yield return WaitSeconds(0.2f);

            var off = new int[1];
            var gap = new float[1];
            signal.On = true;
            yield return Watch(p1, lift, () => lift.CurrentProgress >= 0.5f, 5f, off, gap);
            signal.On = false;   // at full speed up: straight back down
            yield return Watch(p1, lift, () => lift.CurrentProgress <= 0.3f, 5f, off, gap);
            signal.On = true;    // and up again: the deck slams up under the rider
            yield return Watch(p1, lift, () => lift.CurrentProgress >= 1f, 5f, off, gap);

            Assert.AreEqual(0, off[0], "frames off the lift through the reversals");
            Assert.Less(gap[0], SinkTolerance);
            Assert.AreEqual(0, run.ResetCount, "no fall through the deck");
        }

        // ------------------------------------------------------------------ what riders do on a lift

        [UnityTest]
        public IEnumerator JumpOffARisingLift_KeepsTheLiftsRise()
        {
            Time.captureFramerate = 60;
            var lift = Lift(30f, 5f, out var signal);
            yield return null;
            yield return Place(p1, new Vector3(Area.x, DeckTop(lift) + 0.05f, Area.z));
            signal.On = true;
            yield return WaitUntil(() => lift.CurrentProgress >= 0.5f, 5f, "the lift never got to full speed");

            float liftSpeed = 1.5f * 30f / 5f;
            float jumpSpeed = Mathf.Sqrt(2f * tuning.gravity * tuning.jumpHeight);
            Drive.PressJump();
            yield return WaitUntil(() => !p1.Motor.Grounded, 0.5f, "never jumped");
            Assert.Greater(p1.Motor.Velocity.y, jumpSpeed + 0.8f * liftSpeed - tuning.gravity * 0.05f, "the jump kept the lift's rise");
            yield return WaitSeconds(0.2f);
            Assert.Greater(p1.transform.position.y - DeckTop(lift), 0.5f, "it really left the deck, it was not caught by it");
        }

        [UnityTest] public IEnumerator PassBetweenTwoRidersOfARisingLift_30fps() => PassOnTheLift(30);
        [UnityTest] public IEnumerator PassBetweenTwoRidersOfARisingLift_144fps() => PassOnTheLift(144);

        IEnumerator PassOnTheLift(int fps)
        {
            Time.captureFramerate = fps;
            var lift = Lift(30f, 5f, out var signal);   // 9 m/s at mid travel
            yield return null;
            float top = DeckTop(lift) + 0.05f;
            yield return Place(p2, new Vector3(Area.x, top, Area.z + 1.5f));
            yield return Place(p1, new Vector3(Area.x, top, Area.z - 1.5f));
            Give(p1);
            signal.On = true;
            yield return WaitUntil(() => lift.CurrentProgress >= 0.45f, 5f, "the lift never got to full speed");

            // A tap pass straight at the teammate 3 m away, as a player would aim it.
            Vector3 to = p2.CatchVolume.CatchCenter - p1.CameraTarget.position;
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float elevation = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
            p1.Look.SetAim(yaw, -elevation);
            yield return null;
            Assert.IsTrue(p1.Thrower.TryThrow(0f), "the throw left");
            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => catches > 0 || explosions > 0, 2f, "the pass on the lift");
            Assert.AreEqual(0, explosions, "the rising deck caught the bomb");
            Assert.AreEqual(1, catches, "the teammate on the lift caught it");
        }
    }
}
