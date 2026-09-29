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
    /// The #68 bomb obstacles (MVP_TASKS M10), built from their kit prefabs on the open floor of PassSandbox and driven
    /// through the real bomb: fuse zones and laser curtains (PROJECT_SPEC §7.3), bomb gates, pressure plates, actuators
    /// and the checkpoint arch (§12.3, §13.15), tubes (§5 InTransit, §13.16).
    /// </summary>
    public class BombObstacleTests : SandboxTestBase
    {
        static readonly Vector3 O = new Vector3(0f, 0f, -14f);   // free floor, south of the spawn row

        BombFailReason? lastFailReason;
        int explosions, catches;

        [UnitySetUp]
        public IEnumerator Subscribe()
        {
            // The bomb is driven by hand; keep the RunManager's automatic reset out of the way.
            Object.FindFirstObjectByType<RunManager>().enabled = false;
            explosions = catches = 0;
            lastFailReason = null;
            bomb.BombExploded += (reason, detail) => { explosions++; lastFailReason = reason; };
            bomb.BombCaught += receiver => catches++;
            yield break;
        }

        static GameObject Spawn(string prefab, Vector3 position)
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + prefab + ".prefab");
            Assert.IsNotNull(asset, "missing prefab " + prefab);
            return Object.Instantiate(asset, position, Quaternion.identity);
#else
            Assert.Inconclusive("Editor only");
            return null;
#endif
        }

        /// <summary>Sets a serialized field on a spawned fixture (what the builders do in the Editor).</summary>
        static void SetField(Object target, string field, System.Action<object> set)
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
        static System.Action<object> Int(int v) => p => ((SerializedProperty)p).intValue = v;
        static System.Action<object> Ref(Object v) => p => ((SerializedProperty)p).objectReferenceValue = v;
#else
        static System.Action<object> Float(float v) => null;
        static System.Action<object> Int(int v) => null;
        static System.Action<object> Ref(Object v) => null;
#endif

        static SignalActuator Actuator(GameObject go, MonoBehaviour source)
        {
            var actuator = go.GetComponent<SignalActuator>();
            SetField(actuator, "source", Ref(source));
            return actuator;
        }

        IEnumerator PassFrom(Player thrower, Vector3 throwerSpot, Player receiver, Vector3 receiverSpot)
        {
            yield return Place(receiver, receiverSpot);
            yield return Place(thrower, throwerSpot);
            Give(thrower);
            yield return null;
            ThrowAt(thrower, receiver);
            yield return CatchWhenNear(receiver);
        }

        // ------------------------------------------------------------------ fuse zones

        [UnityTest]
        public IEnumerator ForbiddenZone_ExplodesTheBomb_WhenItsCarrierWalksIn()
        {
            Spawn("Gameplay/Zone_Forbidden", O);
            yield return Place(p1, O + new Vector3(0f, 0.05f, -4f));
            Give(p1);
            yield return WaitSeconds(0.2f);
            Assert.AreEqual(0, explosions, "outside the zone nothing happens");

            yield return Place(p1, O + new Vector3(0f, 0.05f, 0f));
            yield return WaitUntil(() => explosions > 0, 1f, "the carrier stood in a forbidden zone");
            Assert.AreEqual(BombFailReason.ForbiddenZone, lastFailReason);
        }

        [UnityTest]
        public IEnumerator ForbiddenZone_LetsAPlayerWithoutTheBombCross()
        {
            Spawn("Gameplay/Zone_Forbidden", O);
            yield return Place(p2, O + new Vector3(0f, 0.05f, -4f));
            Give(p2);
            yield return Place(p1, O + new Vector3(0f, 0.05f, 0f));
            yield return WaitSeconds(0.5f);

            Assert.AreEqual(0, explosions);
            Assert.AreEqual(BombState.Held, bomb.State);
        }

        IEnumerator MeasureBurn(float seconds, System.Action<float> consumed)
        {
            float start = bomb.Fuse.Remaining, t0 = Time.time;
            yield return WaitSeconds(seconds);
            consumed((start - bomb.Fuse.Remaining) / (Time.time - t0));
        }

        [UnityTest]
        public IEnumerator HotZone_BurnsTheFuseTwiceAsFast_ColdZoneHalfAsFast()
        {
            var hot = Spawn("Gameplay/Zone_Hot", O);
            yield return Place(p1, O + new Vector3(0f, 0.05f, 0f));
            Give(p1);
            yield return null;
            float rate = 0f;
            yield return MeasureBurn(1f, r => rate = r);
            Assert.AreEqual(tuning.hotZoneFuseRate, rate, 0.15f, "hot zone");
            Assert.AreEqual(tuning.hotZoneFuseRate, bomb.Fuse.Rate, 1e-4f);

            Object.Destroy(hot);
            Spawn("Gameplay/Zone_Cold", O);
            Give(p1);
            yield return null;
            yield return MeasureBurn(1f, r => rate = r);
            Assert.AreEqual(tuning.coldZoneFuseRate, rate, 0.1f, "cold zone");
            Assert.AreEqual(0, explosions);
        }

        [UnityTest]
        public IEnumerator OverlappingHotAndColdZones_TheHotOneWins()
        {
            Spawn("Gameplay/Zone_Cold", O);
            Spawn("Gameplay/Zone_Hot", O);
            yield return Place(p1, O + new Vector3(0f, 0.05f, 0f));
            Give(p1);
            yield return null;
            yield return null;
            Assert.AreEqual(tuning.hotZoneFuseRate, bomb.Fuse.Rate, 1e-4f);
        }

        [UnityTest]
        public IEnumerator CatchInsideAForbiddenZone_Explodes()
        {
            Spawn("Gameplay/Zone_Forbidden", O);
            yield return PassFrom(p1, O + new Vector3(0f, 0.05f, -5f), p2, O + new Vector3(0f, 0.05f, 0f));
            yield return WaitUntil(() => explosions > 0, 2f, "a catch inside a forbidden zone");

            Assert.AreEqual(1, catches, "the catch itself is valid");
            Assert.AreEqual(BombFailReason.ForbiddenZone, lastFailReason);
        }

        // ------------------------------------------------------------------ laser curtains and windows

        [UnityTest]
        public IEnumerator LaserCurtain_ExplodesAThrowAcrossIt()
        {
            Spawn("Gameplay/LaserCurtain", O);
            yield return PassFrom(p1, O + new Vector3(0f, 0.05f, -5f), p2, O + new Vector3(0f, 0.05f, 5f));
            yield return WaitUntil(() => explosions > 0, 2f, "a throw through a laser curtain");

            Assert.AreEqual(0, catches);
            Assert.AreEqual(BombFailReason.ForbiddenZone, lastFailReason);
            Assert.Less(bomb.transform.position.z, O.z + 1f, "it exploded at the curtain, not at the receiver");
        }

        [UnityTest]
        public IEnumerator WindowBetweenTwoCurtains_LetsThePassThrough_AndPlayersWalkThroughTheCurtains()
        {
            Spawn("Gameplay/LaserCurtain", O + new Vector3(-3f, 0f, 0f));
            Spawn("Gameplay/LaserCurtain", O + new Vector3(3f, 0f, 0f));
            yield return PassFrom(p1, O + new Vector3(0f, 0.05f, -5f), p2, O + new Vector3(0f, 0.05f, 5f));
            yield return WaitUntil(() => catches > 0 || explosions > 0, 2f, "the pass through the window");
            Assert.AreEqual(1, catches);
            Assert.AreEqual(0, explosions);

            // The thrower, now empty-handed, walks through a curtain.
            yield return Place(p1, O + new Vector3(-3f, 0.05f, 0f));
            yield return WaitSeconds(0.4f);
            Assert.AreEqual(0, explosions);
        }

        // ------------------------------------------------------------------ signals

        [UnityTest]
        public IEnumerator BombGate_OpensItsActuator_ForItsHoldTime_ThenItCloses()
        {
            var gate = Spawn("Gameplay/BombGate_Ring", O + new Vector3(0f, 1.6f, 0f)).GetComponent<BombGate>();
            SetField(gate, "holdSeconds", Float(1.5f));
            var lift = Actuator(Spawn("Platforms/Actuator_Lift", O + new Vector3(7f, 0.25f, 0f)), gate);
            yield return null;
            Assert.IsFalse(lift.Opening);

            yield return PassFrom(p1, O + new Vector3(0f, 0.05f, -5f), p2, O + new Vector3(0f, 0.05f, 5f));
            yield return WaitUntil(() => catches > 0 || explosions > 0, 2f, "the pass through the ring");
            Assert.AreEqual(1, catches, "the ring lets a clean pass through");
            Assert.IsTrue(gate.PassedThisSection);
            yield return WaitUntil(() => lift.Opening, 0.5f, "the gate opened the lift");

            yield return WaitUntil(() => !lift.Opening, 3f, "the gate closed after its hold time");
            yield return WaitUntil(() => lift.CurrentProgress <= 0f, 3f, "the lift went back down");
        }

        [UnityTest]
        public IEnumerator CarryingTheBombThroughAGate_DoesNotCount()
        {
            var gate = Spawn("Gameplay/BombGate_Arch", O).GetComponent<BombGate>();
            yield return Place(p1, O + new Vector3(0f, 0.05f, -3f));
            Give(p1);
            yield return Place(p1, O + new Vector3(0f, 0.05f, 0f));
            yield return Place(p1, O + new Vector3(0f, 0.05f, 3f));
            yield return WaitSeconds(0.2f);
            Assert.IsFalse(gate.PassedThisSection);
        }

        [UnityTest]
        public IEnumerator PressurePlate_HoldsItsDoorOpen_AndTheClosingDoorKills()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            // (the kit demo's launch pad sits just west of O: keep the plate clear of it)
            var plate = Spawn("Gameplay/PressurePlate", O + new Vector3(-1.5f, 0f, -3f)).GetComponent<PressurePlate>();
            var door = Actuator(Spawn("Obstacles/Actuator_Door", O + new Vector3(3.5f, 1.8f, 0f)), plate);
            yield return Place(p2, O + new Vector3(-1.5f, 0.05f, -3f));
            yield return WaitUntil(() => door.CurrentProgress >= 1f, 2f, "the plate opened the door");

            yield return Place(p1, O + new Vector3(3.5f, 0.05f, 0f));   // under the open door
            yield return WaitSeconds(0.3f);
            Assert.AreEqual(RunState.Playing, run.State, "an open door is harmless");

            yield return Place(p2, O + new Vector3(-1.5f, 0.05f, 1.5f));   // off the plate
            yield return WaitUntil(() => run.State == RunState.Failing, 2f, "the closing door caught the player under it");
        }

        // ------------------------------------------------------------------ transit

        /// <summary>
        /// The tube prefab's default route, spawned at O: mouth 1.6 m up at O facing the thrower (-Z), exit at O + (0, 5, 8),
        /// receiver pad at O + (0, 0, 14).
        /// </summary>
        IEnumerator ThrowIntoTube(BombTransit tube)
        {
            yield return Place(p1, O + new Vector3(0f, 0.05f, -4f));
            Give(p1);
            yield return null;
            Vector3 origin = p1.ThrowOrigin.position;
            Vector3 mouth = tube.transform.Find("Mouth_1").position;
            Assert.IsTrue(bomb.TryThrow(p1, origin, VelocityToHit(origin, mouth, 0.3f)));
            yield return WaitUntil(() => bomb.State != BombState.Thrown, 1f, "the bomb reached the tube");
        }

        [UnityTest]
        public IEnumerator Tube_SwallowsTheBomb_ThenSendsItToTheReceiverOnItsPad()
        {
            var tube = Spawn("Obstacles/Obstacle_Tube", O).GetComponent<BombTransit>();
            yield return Place(p2, O + new Vector3(0f, 0.05f, 14f));
            yield return ThrowIntoTube(tube);

            Assert.AreEqual(BombState.InTransit, bomb.State, "captured, not exploded");
            Assert.AreEqual(0, explosions);
            Assert.AreEqual(0, tube.ActiveExit);
            Assert.IsFalse(bomb.GetComponent<SphereCollider>().enabled, "nothing touches a bomb in transit");
            float fuse = bomb.Fuse.Remaining;
            yield return WaitSeconds(tube.Delay * 0.6f);
            Assert.AreEqual(BombState.InTransit, bomb.State);
            Assert.AreEqual(fuse, bomb.Fuse.Remaining, 1e-4f, "no fuse burns in transit");

            yield return WaitUntil(() => bomb.State == BombState.Thrown, tube.Delay, "the bomb came out of the exit");
            Assert.IsNull(bomb.LastThrower, "nobody threw the exit arc");
            Assert.AreEqual(-1, tube.ActiveExit);
            yield return CatchWhenNear(p2);
            yield return WaitUntil(() => catches > 0 || explosions > 0, 3f, "the exit arc reached the pad");
            Assert.AreEqual(1, catches, "the exit arc lands in the hands of the receiver on the pad");
        }

        [UnityTest]
        public IEnumerator SectionReset_DuringATransit_RestoresTheBombNormally()
        {
            var tube = Spawn("Obstacles/Obstacle_Tube", O).GetComponent<BombTransit>();
            yield return ThrowIntoTube(tube);
            Assert.AreEqual(BombState.InTransit, bomb.State);

            Give(p1);   // reset path: BeginReset + EndReset
            Assert.AreEqual(BombState.Held, bomb.State);
            Assert.AreEqual(-1, tube.ActiveExit);
            yield return WaitSeconds(tube.Delay + 0.3f);
            Assert.AreEqual(BombState.Held, bomb.State, "the cancelled transit never fires");
            Assert.AreSame(p1, bomb.Carrier);
        }

        [UnityTest]
        public IEnumerator ArchCheckpoint_ActivatesOnlyAfterAPassThroughItsArch()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            var arch = Spawn("Gameplay/BombGate_Arch", O).GetComponent<BombGate>();
            var cp = Spawn("Gameplay/Checkpoint", O + new Vector3(0f, 0f, 4.5f)).GetComponent<Checkpoint>();
            SetField(cp, "id", Int(9));
            SetField(cp, "claimGate", Ref(arch));
            yield return Place(p1, O + new Vector3(-0.8f, 0.05f, 4.5f));
            yield return Place(p2, O + new Vector3(0.8f, 0.05f, 4.5f));
            yield return WaitSeconds(0.4f);
            Assert.IsFalse(cp.Activated, "everyone is in, but the bomb has not flown through the arch");

            yield return PassFrom(p1, O + new Vector3(0f, 0.05f, -4f), p2, O + new Vector3(0f, 0.05f, 4.5f));
            yield return WaitUntil(() => catches > 0 || explosions > 0, 2f, "the pass through the arch");
            Assert.AreEqual(1, catches);
            Assert.IsTrue(arch.PassedThisSection);

            yield return Place(p1, O + new Vector3(-0.8f, 0.05f, 4.5f));
            yield return WaitUntil(() => cp.Activated, 1f, "the claimed checkpoint activated");
            Assert.AreSame(cp, run.CurrentCheckpoint);
        }
    }
}
