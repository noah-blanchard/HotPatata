using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HotPatata.Tests
{
    /// <summary>M6.5 / #18: which bindings can be changed, conflicts, and saved overrides reaching a player's copy.</summary>
    public class InputRebindingTests
    {
        InputActionAsset source;
        InputActionAsset copy;

        [SetUp]
        public void SetUp()
        {
            source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/HotPatataControls.inputactions");
            Assume.That(source, Is.Not.Null);
            copy = Object.Instantiate(source);   // never override the asset itself
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(copy);

        static InputRebinding.Entry Find(InputActionAsset asset, InputSource device, string label) =>
            InputRebinding.Entries(asset, device).First(e => e.Label == label);

        [Test]
        public void Entries_KeyboardMouse_AreTheButtonsAndTheWasdParts()
        {
            var labels = InputRebinding.Entries(copy, InputSource.KeyboardMouse).Select(e => e.Label).ToList();
            CollectionAssert.IsSubsetOf(new[] { "Jump", "Throw", "Catch", "Sprint", "Crouch", "Pause", "Move Up", "Move Down", "Move Left", "Move Right" }, labels);
            CollectionAssert.DoesNotContain(labels, "Look", "mouse delta is not a button");
        }

        [Test]
        public void Entries_Gamepad_AreTheButtonsOnly()
        {
            var entries = InputRebinding.Entries(copy, InputSource.Gamepad);
            var labels = entries.Select(e => e.Label).ToList();
            CollectionAssert.IsSubsetOf(new[] { "Jump", "Throw", "Catch", "Sprint", "Crouch", "Pause" }, labels);
            CollectionAssert.DoesNotContain(labels, "Move", "sticks stay as authored");
            CollectionAssert.DoesNotContain(labels, "Look");
            Assert.IsTrue(entries.All(e => e.Binding.path.StartsWith("<Gamepad>")));
        }

        [Test]
        public void Conflicts_TwoActionsOnOneKey_AreReported()
        {
            var throwKey = Find(copy, InputSource.KeyboardMouse, "Throw");
            throwKey.Action.ApplyBindingOverride(throwKey.BindingIndex, "<Keyboard>/space");

            var conflicts = InputRebinding.Conflicts(copy, throwKey);
            Assert.IsTrue(conflicts.Any(c => c.Label == "Jump"), "Throw on Space collides with Jump");
            Assert.IsEmpty(InputRebinding.Conflicts(copy, Find(copy, InputSource.KeyboardMouse, "Catch")));
        }

        [Test]
        public void SavedOverrides_ReachAnotherCopyOfTheControls()
        {
            var throwKey = Find(copy, InputSource.KeyboardMouse, "Throw");
            throwKey.Action.ApplyBindingOverride(throwKey.BindingIndex, "<Keyboard>/f");
            string json = copy.SaveBindingOverridesAsJson();

            var player = Object.Instantiate(source);   // what PlayerInputReader does
            try
            {
                player.LoadBindingOverridesFromJson(json);
                var loaded = Find(player, InputSource.KeyboardMouse, "Throw");
                Assert.AreEqual("<Keyboard>/f", loaded.Binding.effectivePath);
                Assert.AreEqual("<Mouse>/rightButton", Find(player, InputSource.KeyboardMouse, "Catch").Binding.effectivePath, "others untouched");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void ResetAll_RestoresTheAuthoredBindings()
        {
            var jump = Find(copy, InputSource.Gamepad, "Jump");
            jump.Action.ApplyBindingOverride(jump.BindingIndex, "<Gamepad>/buttonNorth");
            Assert.IsTrue(jump.IsOverridden);

            InputRebinding.ResetAll(copy);
            Assert.IsFalse(jump.IsOverridden);
            Assert.AreEqual("<Gamepad>/buttonSouth", jump.Binding.effectivePath);
        }

        [Test]
        public void Settings_KeepTheOverridesJson()
        {
            var t = ScriptableObject.CreateInstance<GameTuning>();
            try
            {
                var s = Settings.Parse("{\"bindingOverrides\":\"{\\\"bindings\\\":[]}\"}", t);
                Assert.AreEqual("{\"bindings\":[]}", s.bindingOverrides);
                Assert.AreEqual("", Settings.Parse("", t).bindingOverrides, "nothing saved = authored bindings");
            }
            finally
            {
                Object.DestroyImmediate(t);
            }
        }
    }
}
