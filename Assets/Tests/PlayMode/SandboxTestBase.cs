using System.Collections;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Beep.Tests
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

        [UnitySetUp]
        public IEnumerator LoadSandbox()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;   // the Editor may be unfocused while tests run
            LocalPlayerSwitcher.SuppressAutoFocus = true;

#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PassSandbox.unity", new LoadSceneParameters(LoadSceneMode.Single));
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

        /// <summary>Place a player on flat ground facing +Z, ready to be driven.</summary>
        protected static IEnumerator Place(Player p, Vector3 position)
        {
            p.TeleportTo(position, Quaternion.identity);
            yield return WaitUntil(() => p.Motor.Grounded, 2f, p + " did not land after teleport");
        }
    }
}
