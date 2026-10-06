using System.Collections;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HotPatata.Tests
{
    /// <summary>
    /// Loads PassSandbox and hands player 1 a scripted input source, so tests drive the game
    /// independently of the developer's hardware and of Editor window focus.
    /// </summary>
    public abstract class SandboxTestBase
    {
        protected Player p1, p2;
        /// <summary>Scripted input for player 1 (the other player gets none and stays idle).</summary>
        protected PlayerInputReader.ScriptedInput Drive;
        protected BombController bomb;
        protected GameTuning tuning;

        bool previousRunInBackground;

        /// <summary>The scene under test (name without folder/extension).</summary>
        protected virtual string SceneName => "PassSandbox";

        [UnitySetUp]
        public IEnumerator LoadSandbox()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;   // the Editor may be unfocused while tests run
            LocalPlayerSwitcher.SuppressAutoFocus = true;

#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/" + SceneName + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/ScriptableObjects/Tuning/GameTuning.asset");
#else
            Assert.Inconclusive("Sandbox tests run in the Editor only.");
#endif
            yield return null;   // let Start() run

            p1 = GameObject.Find("Player_1").GetComponent<Player>();
            p2 = GameObject.Find("Player_2").GetComponent<Player>();
            bomb = Object.FindFirstObjectByType<BombController>();
            Drive = new PlayerInputReader.ScriptedInput();
            p1.Input.Scripted = Drive;
            FirstPersonCamera.Instance.Target = p1;   // the switcher is suppressed, so pick the camera's player here

            yield return WaitUntil(() => p1.Motor.Grounded && p2.Motor.Grounded, 2f, "players never landed");
        }

        [TearDown]
        public void RestoreGlobals()
        {
            Application.runInBackground = previousRunInBackground;
            LocalPlayerSwitcher.SuppressAutoFocus = false;
        }

        // ------------------------------------------------------------------ helpers

        protected static IEnumerator WaitUntil(System.Func<bool> condition, float timeout, string failMessage)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out: " + failMessage);
                yield return null;
            }
        }

        protected static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        protected static float HorizontalSpeed(Player p)
        {
            var v = p.Motor.Velocity;
            return new Vector2(v.x, v.z).magnitude;
        }

        /// <summary>
        /// Plays a receiver who presses catch as the thrown bomb gets close (default 1.8 m out, i.e. well inside
        /// the catch window at any throw speed). Call after the throw has started.
        /// </summary>
        protected IEnumerator CatchWhenNear(Player receiver, float distance = 1.8f)
        {
            receiver.Input.Scripted ??= new PlayerInputReader.ScriptedInput();
            float end = Time.realtimeSinceStartup + 3f;
            while (bomb.State == BombState.Thrown && Time.realtimeSinceStartup < end)
            {
                if (Vector3.Distance(bomb.transform.position, receiver.CatchVolume.CatchCenter) <= distance)
                {
                    receiver.Input.Scripted.PressCatch();
                    yield break;
                }
                yield return null;
            }
        }

        /// <summary>Hands the bomb to <paramref name="p"/> directly (reset path), with a fresh fuse.</summary>
        protected void Give(Player p)
        {
            bomb.BeginReset();
            bomb.EndReset(p);
        }

        /// <summary>Velocity that carries a projectile from origin to target in `time` seconds under bomb gravity.</summary>
        protected Vector3 VelocityToHit(Vector3 origin, Vector3 target, float time)
        {
            Vector3 g = Physics.gravity * tuning.bombGravityScale;
            return (target - origin) / time - 0.5f * g * time;
        }

        protected static Vector3 CatchPoint(Player p) => p.CatchVolume.CatchCenter;

        /// <summary>An exact ballistic throw from <paramref name="from"/>'s hand to <paramref name="to"/>'s catch centre.</summary>
        protected void ThrowAt(Player from, Player to, float time = 0.5f)
        {
            Vector3 origin = from.ThrowOrigin.position;
            Assert.IsTrue(bomb.TryThrow(from, origin, VelocityToHit(origin, CatchPoint(to), time)));
        }

        /// <summary>Place a player on flat ground facing +Z, ready to be driven.</summary>
        protected static IEnumerator Place(Player p, Vector3 position)
        {
            p.TeleportTo(position, Quaternion.identity);
            yield return WaitUntil(() => p.Motor.Grounded, 2f, p + " did not land after teleport");
        }

#if UNITY_EDITOR
        /// <summary>
        /// A campfire marks its checkpoint (spec §19): it stands beside the spawns (never on one), inside the checkpoint's zone
        /// with room around it, and its stone ring is seen at its real size.
        /// </summary>
        protected static void AssertCampfireMarks(CampfirePresentation fire, Checkpoint cp)
        {
            float d = Vector3.Distance(fire.transform.position, cp.transform.position);
            Assert.That(d, Is.InRange(3f, 6f), cp.name + ": the campfire stands beside the spawns, never on one");
            var trigger = (BoxCollider)new SerializedObject(cp).FindProperty("trigger").objectReferenceValue;
            foreach (var offset in new[] { Vector3.zero, Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
            {
                var local = trigger.transform.InverseTransformPoint(fire.transform.position + offset * 1.5f + Vector3.up * 0.5f) - trigger.center;
                var half = trigger.size * 0.5f;
                Assert.IsTrue(Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z,
                    cp.name + ": the team gathered at the campfire is inside the checkpoint's zone");
            }
            Renderer pit = null;
            foreach (var filter in fire.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null && filter.sharedMesh.name == "stone_fire_pit") pit = filter.GetComponent<Renderer>();
            Assert.IsNotNull(pit, cp.name + ": the campfire has its stone fire pit");
            Assert.That(pit.bounds.size.y, Is.InRange(0.3f, 0.7f), cp.name + ": the fire pit stands at its real height");
            Assert.Greater(pit.bounds.size.x, 1f, cp.name + ": the fire pit lies flat, at its real width");
        }
#endif

        /// <summary>Places both players at <paramref name="fire"/> (on the spawns' side of it, but off the spawns).</summary>
        protected IEnumerator GatherAtCampfire(CampfirePresentation fire, Checkpoint cp)
        {
            var toward = cp.transform.position - fire.transform.position;
            toward.y = 0f;
            toward.Normalize();
            var side = Vector3.Cross(Vector3.up, toward) * 0.6f;
            var at = fire.transform.position + toward * 1.3f + Vector3.up * 0.1f;
            yield return Place(p1, at + side);
            yield return Place(p2, at - side);
        }
    }
}
