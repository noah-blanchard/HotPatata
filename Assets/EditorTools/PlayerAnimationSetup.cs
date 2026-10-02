using System;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>Creates and maintains the mannequin/animation setup on the Player prefab.</summary>
    [InitializeOnLoad]
    public static class PlayerAnimationSetup
    {
        const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        const string ModelPath = "Assets/Art/Models/Player/Mannequin Character/characters/Mannequin_Medium.fbx";
        const string MovementPath = "Assets/Art/Models/Player/Animations/fbx/Rig_Medium/Rig_Medium_MovementBasic.fbx";
        const string GeneralPath = "Assets/Art/Models/Player/Animations/fbx/Rig_Medium/Rig_Medium_General.fbx";
        const string ControllerFolder = "Assets/Art/Models/Player/Controllers";
        const string ControllerPath = ControllerFolder + "/PlayerMannequin.controller";

        static PlayerAnimationSetup()
        {
            EditorApplication.delayCall += SetupIfNeeded;
        }

        [MenuItem("HotPatata/Setup Player Mannequin & Animations")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            ConfigureClipLooping(MovementPath, "WALKING_A", "RUNNING_A");
            ConfigureClipLooping(GeneralPath, "IDLE_A");
            AnimationClip idle = FindClip(GeneralPath, "IDLE_A", "IDLE_B");
            AnimationClip walk = FindClip(MovementPath, "WALKING_A", "WALKING");
            AnimationClip run = FindClip(MovementPath, "RUNNING_A", "RUNNING");
            AnimationClip jump = FindClip(MovementPath, "JUMP_FULL_SHORT", "JUMP_FULL", "JUMP");
            AnimationClip throwClip = FindClip(GeneralPath, "THROW");

            if (idle == null || walk == null || run == null || jump == null || throwClip == null)
            {
                Debug.LogError("[Player Animation] Required clips were not found. Available movement clips: " +
                               ClipList(MovementPath) + "; general clips: " + ClipList(GeneralPath));
                return;
            }

            EnsureFolder(ControllerFolder);
            AnimatorController controller = CreateController(idle, walk, run, jump, throwClip);
            ConfigurePrefab(controller, idle, walk, run, jump, throwClip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Player Animation] Mannequin, locomotion, jump, throw and hand anchor configured.");
        }

        static void SetupIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab != null && prefab.GetComponent<PlayerAnimator>() != null &&
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) return;
            Setup();
        }

        static AnimatorController CreateController(AnimationClip idle, AnimationClip walk, AnimationClip run,
            AnimationClip jump, AnimationClip throwClip)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Throw", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ThrowSpeed", AnimatorControllerParameterType.Float);
            AnimatorControllerParameter[] parameters = controller.parameters;
            parameters[parameters.Length - 1].defaultFloat = 1f;
            controller.parameters = parameters;

            AnimatorStateMachine baseMachine = controller.layers[0].stateMachine;
            var locomotion = baseMachine.AddState("Locomotion");
            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 0.35f);
            tree.AddChild(run, 1f);
            locomotion.motion = tree;
            baseMachine.defaultState = locomotion;

            var jumping = baseMachine.AddState("Jump");
            jumping.motion = jump;
            var toJump = locomotion.AddTransition(jumping);
            toJump.hasExitTime = false;
            toJump.duration = 0.08f;
            toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            var toLocomotion = jumping.AddTransition(locomotion);
            toLocomotion.hasExitTime = false;
            toLocomotion.duration = 0.1f;
            toLocomotion.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            controller.AddLayer("Throw");
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer throwLayer = layers[1];
            throwLayer.defaultWeight = 1f;
            throwLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            layers[1] = throwLayer;
            controller.layers = layers;
            AnimatorStateMachine throwMachine = throwLayer.stateMachine;
            var empty = throwMachine.AddState("Empty");
            var throwing = throwMachine.AddState("Throw");
            throwing.motion = throwClip;
            throwing.speedParameterActive = true;
            throwing.speedParameter = "ThrowSpeed";
            throwMachine.defaultState = empty;
            var startThrow = empty.AddTransition(throwing);
            startThrow.hasExitTime = false;
            startThrow.duration = 0.04f;
            startThrow.AddCondition(AnimatorConditionMode.If, 0f, "Throw");
            var finishThrow = throwing.AddTransition(empty);
            finishThrow.hasExitTime = true;
            finishThrow.exitTime = 0.95f;
            finishThrow.duration = 0.08f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void ConfigurePrefab(AnimatorController controller, params AnimationClip[] clips)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform visual = root.transform.Find("Visual");
                if (visual == null) throw new InvalidOperationException("Player prefab has no Visual child.");

                Transform mannequin = visual.Find("Mannequin");
                if (mannequin == null)
                {
                    foreach (string oldName in new[] { "Body", "Nose" })
                    {
                        Transform old = visual.Find(oldName);
                        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    }

                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                    if (model == null) throw new InvalidOperationException("Mannequin model is missing.");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
                    instance.name = "Mannequin";
                    mannequin = instance.transform;
                    mannequin.localPosition = Vector3.zero;
                    mannequin.localRotation = Quaternion.identity;
                    mannequin.localScale = Vector3.one;
                    SetLayerRecursively(instance, root.layer);
                }

                FitToController(mannequin);

                Animator animator = mannequin.GetComponentInChildren<Animator>(true);
                if (animator == null) animator = mannequin.gameObject.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                ValidateClipCompatibility(mannequin, clips);

                Transform leftHand = FindBone(mannequin, "hand.l", "wrist.l");
                Transform rightHand = FindBone(mannequin, "hand.r", "wrist.r");
                if (leftHand == null || rightHand == null)
                    throw new InvalidOperationException("Could not find hand.l/hand.r in the mannequin skeleton.");

                var driver = root.GetComponent<PlayerAnimator>();
                if (driver == null) driver = root.AddComponent<PlayerAnimator>();
                var driverSo = new SerializedObject(driver);
                driverSo.FindProperty("animator").objectReferenceValue = animator;
                driverSo.FindProperty("leftHand").objectReferenceValue = leftHand;
                driverSo.FindProperty("rightHand").objectReferenceValue = rightHand;
                driverSo.ApplyModifiedPropertiesWithoutUndo();

                var presentation = root.GetComponent<PlayerPresentation>();
                // Every mannequin part takes the slot colour, not only the first one found.
                Renderer[] parts = mannequin.GetComponentsInChildren<Renderer>(true);
                var presentationSo = new SerializedObject(presentation);
                var bodyRenderers = presentationSo.FindProperty("bodyRenderers");
                bodyRenderers.arraySize = parts.Length;
                for (int i = 0; i < parts.Length; i++)
                    bodyRenderers.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
                presentationSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void FitToController(Transform mannequin)
        {
            Renderer[] renderers = mannequin.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.y < 0.01f) return;

            float scale = 1.75f / bounds.size.y;
            mannequin.localScale *= scale;

            renderers = mannequin.GetComponentsInChildren<Renderer>(true);
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            mannequin.position += Vector3.up * (mannequin.parent.position.y - bounds.min.y);
        }

        static AnimationClip FindClip(string path, params string[] candidates)
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach (string candidate in candidates)
            {
                AnimationClip exact = clips.FirstOrDefault(c =>
                    string.Equals(c.name, candidate, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;
                AnimationClip suffix = clips.FirstOrDefault(c =>
                    c.name.EndsWith(candidate, StringComparison.OrdinalIgnoreCase));
                if (suffix != null) return suffix;
            }
            return null;
        }

        static void ConfigureClipLooping(string path, params string[] loopingNames)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            bool changed = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool loop = loopingNames.Any(expected => ClipNameMatches(clip.name, expected));
                if (clip.loopTime == loop) continue;
                clip.loopTime = loop;
                clip.loopPose = loop;
                changed = true;
            }

            if (!changed && importer.clipAnimations.Length > 0) return;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static bool ClipNameMatches(string actual, string expected) =>
            string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

        static string ClipList(string path) => string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>().Select(c => c.name));

        static Transform FindBone(Transform root, params string[] names)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                foreach (string candidate in names)
                    if (string.Equals(child.name, candidate, StringComparison.OrdinalIgnoreCase)) return child;
            return null;
        }

        static void ValidateClipCompatibility(Transform mannequin, params AnimationClip[] clips)
        {
            foreach (AnimationClip clip in clips)
            {
                int matchingBindings = AnimationUtility.GetCurveBindings(clip).Count(binding =>
                    binding.type == typeof(Transform) &&
                    (string.IsNullOrEmpty(binding.path) || mannequin.Find(binding.path) != null));
                if (matchingBindings == 0)
                    throw new InvalidOperationException($"Animation '{clip.name}' does not match the mannequin hierarchy.");
            }
        }

        static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }

        static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string part in path.Split('/').Skip(1))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
