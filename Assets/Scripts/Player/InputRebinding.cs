using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HotPatata
{
    /// <summary>
    /// Key and button rebinding (spec §19, M6.5): which bindings a player may change, the interactive rebind, conflict
    /// detection, and saving the overrides with the other <see cref="Settings"/>. Every player's
    /// <see cref="PlayerInputReader"/> applies the saved overrides to its own copy of HotPatataControls.
    /// A settings screen works on <see cref="CreateEditableCopy"/> and commits with <see cref="Commit"/>.
    /// </summary>
    public static class InputRebinding
    {
        /// <summary>After a first match, how long to wait for a better one (a stick nudge must not beat the intended button).</summary>
        const float MatchWaitSeconds = 0.1f;

        /// <summary>One binding a player can change.</summary>
        public readonly struct Entry
        {
            public readonly InputAction Action;
            public readonly int BindingIndex;
            public readonly InputSource Device;

            public Entry(InputAction action, int bindingIndex, InputSource device)
            {
                Action = action;
                BindingIndex = bindingIndex;
                Device = device;
            }

            public InputBinding Binding => Action.bindings[BindingIndex];
            /// <summary>"Jump", or "Move Up" for a part of a composite.</summary>
            public string Label => Binding.isPartOfComposite ? $"{Action.name} {Capitalise(Binding.name)}" : Action.name;
            public string Current => Action.GetBindingDisplayString(BindingIndex);
            public bool IsOverridden => !string.IsNullOrEmpty(Binding.overridePath);
        }

        /// <summary>Which device a binding belongs to, from the path it was authored with.</summary>
        public static InputSource DeviceOf(InputBinding binding)
        {
            string path = binding.path ?? "";
            if (path.StartsWith("<Gamepad>", StringComparison.Ordinal)) return InputSource.Gamepad;
            if (path.StartsWith("<Keyboard>", StringComparison.Ordinal) || path.StartsWith("<Mouse>", StringComparison.Ordinal))
                return InputSource.KeyboardMouse;
            return InputSource.None;
        }

        /// <summary>
        /// The bindings a player may change for one device: every button, and each part of a composite (WASD).
        /// Sticks and mouse delta (Move/Look as a whole) are not buttons, so they stay as authored.
        /// </summary>
        public static List<Entry> Entries(InputActionAsset asset, InputSource device)
        {
            var list = new List<Entry>();
            foreach (var map in asset.actionMaps)
            foreach (var action in map.actions)
            {
                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                {
                    var b = bindings[i];
                    if (b.isComposite || DeviceOf(b) != device) continue;
                    if (action.type != InputActionType.Button && !b.isPartOfComposite) continue;
                    list.Add(new Entry(action, i, device));
                }
            }
            return list;
        }

        /// <summary>Other rebindable bindings of the same device that now use the same control as <paramref name="entry"/>.</summary>
        public static List<Entry> Conflicts(InputActionAsset asset, Entry entry)
        {
            var result = new List<Entry>();
            string path = entry.Binding.effectivePath;
            if (string.IsNullOrEmpty(path)) return result;
            foreach (var other in Entries(asset, entry.Device))
            {
                if (other.Action == entry.Action && other.BindingIndex == entry.BindingIndex) continue;
                if (string.Equals(other.Binding.effectivePath, path, StringComparison.OrdinalIgnoreCase)) result.Add(other);
            }
            return result;
        }

        /// <summary>
        /// Waits for the next button on <paramref name="entry"/>'s device and binds it. Esc (keyboard) or Start
        /// (gamepad) cancels. <paramref name="done"/> gets true when a new control was bound. Dispose the returned
        /// operation to abort (e.g. the screen closes).
        /// </summary>
        public static InputActionRebindingExtensions.RebindingOperation Start(Entry entry, Action<bool> done)
        {
            var action = entry.Action;
            bool wasEnabled = action.enabled;
            action.Disable();   // an enabled action cannot be rebound

            var op = action.PerformInteractiveRebinding(entry.BindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Pointer>/position")
                .OnMatchWaitForAnother(MatchWaitSeconds);

            if (entry.Device == InputSource.Gamepad)
                op = op.WithControlsHavingToMatchPath("<Gamepad>").WithCancelingThrough("<Gamepad>/start");
            else
                op = op.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>")
                    .WithCancelingThrough("<Keyboard>/escape");

            void Finish(InputActionRebindingExtensions.RebindingOperation o, bool bound)
            {
                o.Dispose();
                if (wasEnabled) action.Enable();
                done?.Invoke(bound);
            }

            return op.OnComplete(o => Finish(o, true)).OnCancel(o => Finish(o, false)).Start();
        }

        public static void Reset(Entry entry) => entry.Action.RemoveBindingOverride(entry.BindingIndex);

        public static void ResetAll(InputActionAsset asset) => asset.RemoveAllBindingOverrides();

        // ------------------------------------------------------------------ saved overrides

        /// <summary>Loads the saved overrides into <paramref name="actions"/> (a player's copy); nothing saved = the authored bindings.</summary>
        public static void ApplySaved(InputActionAsset actions)
        {
            if (actions == null) return;
            actions.RemoveAllBindingOverrides();
            string json = Settings.Current?.bindingOverrides;
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                actions.LoadBindingOverridesFromJson(json, removeExisting: true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] saved key bindings could not be applied, using defaults: {e.Message}");
                actions.RemoveAllBindingOverrides();
            }
        }

        /// <summary>A copy of the controls, with the saved overrides, for a settings screen to edit. Destroy it when done.</summary>
        public static InputActionAsset CreateEditableCopy(InputActionAsset source)
        {
            var copy = UnityEngine.Object.Instantiate(source);   // same action and binding ids, so saved overrides match
            ApplySaved(copy);
            return copy;
        }

        /// <summary>Saves <paramref name="edited"/>'s overrides with the other settings; every player picks them up.</summary>
        public static void Commit(InputActionAsset edited, GameTuning tuning)
        {
            var data = Settings.Editable(tuning);
            data.bindingOverrides = edited.SaveBindingOverridesAsJson();
            Settings.Save(data, tuning);
        }

        static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
