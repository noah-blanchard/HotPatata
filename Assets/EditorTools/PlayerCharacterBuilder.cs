using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HotPatata;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HotPatata.Editor
{
    /// <summary>
    /// Builds the players' look from the Mixamo characters (ARCHITECTURE §25): the clips' import settings, one Humanoid
    /// controller for players and one for the menu show, an upper-body mask so a throw never stops the legs, a prefab
    /// per character (model, toon materials, a socket in the right palm, <see cref="PlayerCharacter"/>), the slots'
    /// characters in <see cref="GameTuning.playerCharacters"/>, and the Player prefab's default character and slot ring.
    /// Every character is Humanoid and the clips copy James's avatar, so one set of clips plays on all of them.
    /// Idempotent: rebuild after changing it (menu HotPatata/Player/Build Characters), never edit the results by hand.
    /// </summary>
    public static class PlayerCharacterBuilder
    {
        const string CharactersDir = "Assets/Art/Models/Characters";
        const string AnimationsDir = CharactersDir + "/Animations";
        const string ControllersDir = CharactersDir + "/Controllers";
        public const string PlayerControllerPath = ControllersDir + "/PlayerCharacter.controller";
        public const string MenuControllerPath = ControllersDir + "/MenuCharacter.controller";
        const string MaskPath = ControllersDir + "/ThrowUpperBody.mask";
        const string PrefabDir = "Assets/Prefabs/Player/Characters";
        const string MaterialDir = "Assets/Art/Materials/Characters";
        const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        const string TuningPath = "Assets/ScriptableObjects/Tuning/GameTuning.asset";
        const string ToonShader = "HotPatata/Toon";

        /// <summary>Every character is fitted to this height (m): it fills the 1.8 m capsule, eyes near the 1.6 m camera.</summary>
        const float CharacterHeight = 1.75f;
        /// <summary>The potato's centre from the hand bone: along the fingers, then off the palm (m, fitted size).</summary>
        const float SocketAlongFingers = 0.08f, SocketOffPalm = 0.055f;

        static readonly (string id, string model)[] Characters =
        {
            ("James", "James/James.fbx"), ("Remy", "Remy/Remy.fbx"), ("TheBoss", "The Boss/The Boss.fbx")
        };

        /// <summary>The character of each slot (until there is a 4th model, slot 4 is James again). Spec §17.2: not a choice.</summary>
        static readonly string[] SlotCharacters = { "James", "Remy", "TheBoss", "James" };

        /// <summary>Materials kept as imported (they need alpha the toon shader's lit pass does not clip).</summary>
        static readonly string[] KeepImportedMaterials = { "hair", "eyelash" };

        /// <summary>The clips: loop or not, and for the jumps the airborne frames (take-off to the peak, measured on James).</summary>
        static readonly (string name, bool loop, int first, int last, bool required)[] Clips =
        {
            ("Standing Idle", true, -1, -1, true),
            ("Slow Run", true, -1, -1, true),
            ("Fast Run", true, -1, -1, true),
            ("Jump", false, 16, 26, true),
            ("Running Jump", false, 0, 11, true),
            ("Throw", false, -1, -1, true),
            // Optional, for the menu show (Mixamo, same settings): without them it falls back to Jump / Standing Idle.
            ("Cheering", false, -1, -1, false),
            ("Waving", false, -1, -1, false),
            ("Hit Reaction", false, -1, -1, false)
        };

        [MenuItem("HotPatata/Player/Build Characters")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureFolder(ControllersDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(MaterialDir);

            ConfigureClips();
            var mask = BuildMask();
            var playerController = BuildPlayerController(mask);
            BuildMenuController();

            var prefabs = Characters.Select(c => BuildCharacter(c.id, CharactersDir + "/" + c.model, playerController)).ToList();
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath) ?? throw new InvalidOperationException("missing " + TuningPath);
            tuning.playerCharacters = SlotCharacters.Select(id => prefabs.First(p => p.Id == id)).ToArray();
            EditorUtility.SetDirty(tuning);
            ConfigurePlayerPrefab(prefabs[0]);
            AssetDatabase.SaveAssets();

            var (hold, release) = MeasureThrow();
            Debug.Log($"[PlayerCharacterBuilder] characters built. Throw clip: hand furthest back at {hold:F2} (GameTuning.throwAnimationHoldNormalized = {tuning.throwAnimationHoldNormalized:F2}), comes forward at {release:F2} s");
        }

        // ------------------------------------------------------------------ clips

        public static AnimationClip Clip(string name)
        {
            string path = AnimationsDir + "/" + name + ".fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        /// <summary>Names each clip after its file, bakes the root into the pose (the motor moves the player), loops and trims.</summary>
        static void ConfigureClips()
        {
            foreach (var (name, loop, first, last, required) in Clips)
            {
                string path = AnimationsDir + "/" + name + ".fbx";
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                {
                    if (required) throw new InvalidOperationException("missing clip " + path);
                    continue;
                }
                var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                var clip = clips[0];
                string before = Signature(clip);
                clip.name = name;
                clip.loopTime = loop;
                clip.loopPose = loop;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                if (first >= 0)
                {
                    clip.firstFrame = first;
                    clip.lastFrame = last;
                }
                if (Signature(clip) == before && importer.clipAnimations.Length > 0) continue;
                importer.clipAnimations = new[] { clip };
                importer.SaveAndReimport();
            }
        }

        static string Signature(ModelImporterClipAnimation c) =>
            $"{c.name}|{c.loopTime}|{c.loopPose}|{c.lockRootRotation}|{c.lockRootHeightY}|{c.lockRootPositionXZ}|{c.keepOriginalOrientation}|{c.keepOriginalPositionY}|{c.keepOriginalPositionXZ}|{c.firstFrame}|{c.lastFrame}";

        // ------------------------------------------------------------------ controllers

        /// <summary>The throw's mask: spine, head and arms; the legs keep running.</summary>
        static AvatarMask BuildMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, MaskPath);
            }
            var upper = new HashSet<AvatarMaskBodyPart>
            {
                AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
                AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK
            };
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, upper.Contains(part));
            EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>
        /// The players' controller, with the parameters and states <see cref="PlayerAnimator"/> drives: Locomotion (0 idle,
        /// 1 run, 2 sprint), Jump (the air pose, standing or running), and the Throw layer (upper body, its speed on
        /// ThrowSpeed: it winds up, stops at the hold point while the button is held, and follows through on release).
        /// </summary>
        static AnimatorController BuildPlayerController(AvatarMask mask)
        {
            var controller = FreshController(PlayerControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Throw", AnimatorControllerParameterType.Trigger);
            controller.AddParameter(new AnimatorControllerParameter { name = "ThrowSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var grounded = controller.parameters.First(p => p.name == "Grounded");
            grounded.defaultBool = true;
            controller.parameters = controller.parameters.Select(p => p.name == "Grounded" ? grounded : p).ToArray();

            var machine = controller.layers[0].stateMachine;
            var locomotion = machine.AddState("Locomotion");
            locomotion.motion = Blend(controller, "Locomotion", (Clip("Standing Idle"), 0f), (Clip("Slow Run"), 1f), (Clip("Fast Run"), 2f));
            machine.defaultState = locomotion;

            var jump = machine.AddState("Jump");
            jump.motion = Blend(controller, "Jump", (Clip("Jump"), 0f), (Clip("Running Jump"), 1f));
            var toJump = locomotion.AddTransition(jump);
            toJump.hasExitTime = false;
            toJump.duration = 0.08f;
            toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            var toLocomotion = jump.AddTransition(locomotion);
            toLocomotion.hasExitTime = false;
            toLocomotion.duration = 0.12f;
            toLocomotion.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            controller.AddLayer("Throw");
            var layers = controller.layers;
            var throwLayer = layers[1];
            throwLayer.defaultWeight = 1f;
            throwLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            throwLayer.avatarMask = mask;
            layers[1] = throwLayer;
            controller.layers = layers;
            var throwMachine = throwLayer.stateMachine;
            var empty = throwMachine.AddState("Empty");
            var throwing = throwMachine.AddState("Throw");
            throwing.motion = Clip("Throw");
            throwing.speedParameterActive = true;
            throwing.speedParameter = "ThrowSpeed";
            throwMachine.defaultState = empty;
            var start = empty.AddTransition(throwing);
            start.hasExitTime = false;
            start.duration = 0.06f;
            start.AddCondition(AnimatorConditionMode.If, 0f, "Throw");
            var finish = throwing.AddTransition(empty);
            finish.hasExitTime = true;
            finish.exitTime = 0.92f;
            finish.duration = 0.15f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// The controller at <paramref name="path"/>, emptied (parameters, extra layers, states, blend trees) but kept: the
        /// same asset and GUID, so prefabs that reference it stay valid. Deleting and recreating it left them pointing at
        /// nothing until they were reimported.
        /// </summary>
        static AnimatorController FreshController(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) return AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var p in controller.parameters) controller.RemoveParameter(p);
            while (controller.layers.Length > 1) controller.RemoveLayer(controller.layers.Length - 1);
            var machine = controller.layers[0].stateMachine;
            foreach (var s in machine.states.ToList()) machine.RemoveState(s.state);
            foreach (var t in machine.anyStateTransitions.ToList()) machine.RemoveAnyStateTransition(t);
            foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>().ToList())
                AssetDatabase.RemoveObjectFromAsset(tree);
            return controller;
        }

        static BlendTree Blend(AnimatorController controller, string name, params (AnimationClip clip, float threshold)[] children)
        {
            var tree = new BlendTree { name = name, blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            foreach (var (clip, threshold) in children)
            {
                if (clip == null) throw new InvalidOperationException($"missing clip for {name}");
                tree.AddChild(clip, threshold);
            }
            return tree;
        }

        /// <summary>
        /// The menu show's controller: one state per clip, named after the clip (MenuHotPotato cross-fades by name and
        /// looks the clip's length up by it). A missing optional clip falls back to Jump or Standing Idle.
        /// </summary>
        static void BuildMenuController()
        {
            var controller = FreshController(MenuControllerPath);
            var machine = controller.layers[0].stateMachine;
            (string state, string fallback)[] states =
            {
                ("Standing Idle", null), ("Throw", null), ("Jump", null),
                ("Cheering", "Jump"), ("Waving", "Standing Idle"), ("Hit Reaction", "Standing Idle")
            };
            foreach (var (state, fallback) in states)
            {
                var s = machine.AddState(state);
                s.motion = Clip(state) ?? Clip(fallback);
                if (state == "Standing Idle") machine.defaultState = s;
            }
            EditorUtility.SetDirty(controller);
        }

        public static RuntimeAnimatorController MenuController => AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MenuControllerPath);

        // ------------------------------------------------------------------ characters

        /// <summary>
        /// One character prefab: the model fitted to <see cref="CharacterHeight"/> with its feet on the ground, its
        /// Animator on the shared controller, toon materials, and the potato's socket in the right palm.
        /// </summary>
        static PlayerCharacter BuildCharacter(string id, string modelPath, RuntimeAnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) ?? throw new InvalidOperationException("missing " + modelPath);
            var root = new GameObject(id);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = "Model";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                Fit(instance.transform);

                var animator = instance.GetComponent<Animator>();
                if (animator == null) throw new InvalidOperationException(modelPath + " has no Animator");
                if (animator.avatar == null || !animator.avatar.isHuman) throw new InvalidOperationException(modelPath + " is not Humanoid");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers) r.sharedMaterials = r.sharedMaterials.Select(m => ToonMaterial(id, m)).ToArray();

                var socket = HandSocket(animator);
                root.AddComponent<PlayerCharacter>().Configure(id, animator, socket, renderers);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + id + ".prefab");
                return prefab.GetComponent<PlayerCharacter>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Scales the model to <see cref="CharacterHeight"/> (bind pose, skinned) and stands it on the ground.</summary>
        static void Fit(Transform model)
        {
            var bounds = SkinnedBounds(model);
            if (bounds.size.y < 0.01f) return;
            model.localScale *= CharacterHeight / bounds.size.y;
            bounds = SkinnedBounds(model);
            model.position += Vector3.up * (model.parent.position.y - bounds.min.y);
        }

        static Bounds SkinnedBounds(Transform model)
        {
            var bounds = new Bounds();
            bool any = false;
            var baked = new Mesh();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.BakeMesh(baked, true);
                foreach (var v in baked.vertices)
                {
                    var world = smr.transform.TransformPoint(v);
                    if (!any) bounds = new Bounds(world, Vector3.zero);
                    else bounds.Encapsulate(world);
                    any = true;
                }
            }
            Object.DestroyImmediate(baked);
            return bounds;
        }

        /// <summary>
        /// The socket in the right palm (bind pose: arms out, palms down): along the fingers, then off the palm by the
        /// potato's half-thickness. All characters share the avatar, so the same offsets fit them all.
        /// </summary>
        static Transform HandSocket(Animator animator)
        {
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand) ?? throw new InvalidOperationException("no right hand bone");
            var middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            var little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            Vector3 fingers = middle != null ? (middle.position - hand.position).normalized : animator.transform.right;
            Vector3 palm = index != null && little != null ? Vector3.Cross(index.position - little.position, fingers).normalized : -animator.transform.up;
            if (Vector3.Dot(palm, -animator.transform.up) < 0f) palm = -palm;   // the palm faces down in the bind pose
            var socket = new GameObject("HandSocket").transform;
            socket.SetParent(hand, false);
            socket.position = hand.position + fingers * SocketAlongFingers + palm * SocketOffPalm;
            socket.rotation = animator.transform.rotation;
            return socket;
        }

        /// <summary>The model's material on the project's shader (its diffuse kept), unless it needs its alpha.</summary>
        static Material ToonMaterial(string character, Material source)
        {
            if (source == null) return null;
            if (KeepImportedMaterials.Any(k => source.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)) return source;
            string path = MaterialDir + "/" + character + "_" + source.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(ToonShader));
                AssetDatabase.CreateAsset(mat, path);
            }
            var diffuse = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture;
            mat.SetTexture("_BaseMap", diffuse);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_SuitTint", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ the Player prefab

        /// <summary>The Player prefab's default character (slot 1's) and its slot ring; the old mannequin goes.</summary>
        static void ConfigurePlayerPrefab(PlayerCharacter character)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var visual = root.transform.Find("Visual") ?? throw new InvalidOperationException("Player prefab has no Visual");
                foreach (Transform child in visual.Cast<Transform>().ToList()) Object.DestroyImmediate(child.gameObject);
                var shown = (PlayerCharacter)PrefabUtility.InstantiatePrefab(character, visual);
                shown.name = character.name;
                SetLayer(shown.transform, root.layer);

                var ring = root.transform.Find("SlotRing")?.gameObject ?? new GameObject("SlotRing");
                ring.transform.SetParent(root.transform, false);
                ring.transform.localPosition = Vector3.up * 0.02f;
                ring.transform.localScale = new Vector3(0.95f, 1f, 0.95f);
                var ringRenderer = FlatRenderer(ring, CourseKit.MakeUnlitMaterial("Player_SlotRing", Color.white, null, additive: false));
                var shape = ring.transform.Find("Shape")?.gameObject ?? new GameObject("Shape");
                shape.transform.SetParent(ring.transform, false);
                shape.transform.localPosition = Vector3.up * 0.005f;
                shape.transform.localScale = Vector3.one * 0.5f;
                FlatRenderer(shape, CourseKit.MakeUnlitMaterial("Player_SlotRingShape", Color.white, null, additive: false));
                SetLayer(ring.transform, root.layer);

                var driver = new SerializedObject(root.GetComponent<PlayerAnimator>());
                driver.FindProperty("animator").objectReferenceValue = shown.Animator;
                driver.FindProperty("handSocket").objectReferenceValue = shown.HandSocket;
                driver.FindProperty("poseRoot").objectReferenceValue = null;
                driver.ApplyModifiedPropertiesWithoutUndo();

                var presentation = new SerializedObject(root.GetComponent<PlayerPresentation>());
                presentation.FindProperty("visualRoot").objectReferenceValue = visual;
                presentation.FindProperty("character").objectReferenceValue = shown;
                presentation.FindProperty("slotRing").objectReferenceValue = ringRenderer;
                presentation.FindProperty("slotRingShape").objectReferenceValue = shape.GetComponent<MeshFilter>();
                presentation.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>A mesh renderer for a flat glyph (its mesh is set at runtime, PlayerShapeMesh.Flat), no shadows.</summary>
        static Renderer FlatRenderer(GameObject go, Material material)
        {
            if (go.GetComponent<MeshFilter>() == null) go.AddComponent<MeshFilter>();
            var r = go.GetComponent<MeshRenderer>();
            if (r == null) r = go.AddComponent<MeshRenderer>();   // never ?? on a component: Unity's fake null
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayer(child, layer);
        }

        // ------------------------------------------------------------------ throw clip measurements

        /// <summary>
        /// Samples the Throw clip on James: when the right hand is furthest behind the hips (normalized: the held
        /// wind-up), and when it comes forward past the shoulder (seconds: the potato leaves the hand in the menu show).
        /// </summary>
        public static (float holdNormalized, float releaseSeconds) MeasureThrow()
        {
            var clip = Clip("Throw");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharactersDir + "/" + Characters[0].model);
            if (clip == null || model == null) return (0.27f, 0.75f);
            var scene = EditorSceneManager.NewPreviewScene();
            var go = (GameObject)Object.Instantiate(model);
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                var animator = go.GetComponent<Animator>();
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var shoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
                int frames = Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate));
                float minZ = float.MaxValue;
                int back = 0, forward = frames;
                AnimationMode.StartAnimationMode();
                for (int f = 0; f <= frames; f++)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(go, clip, f / clip.frameRate);
                    AnimationMode.EndSampling();
                    float z = go.transform.InverseTransformPoint(hand.position).z;
                    if (z < minZ) { minZ = z; back = f; }
                }
                for (int f = back; f <= frames; f++)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(go, clip, f / clip.frameRate);
                    AnimationMode.EndSampling();
                    if (go.transform.InverseTransformPoint(hand.position).z > go.transform.InverseTransformPoint(shoulder.position).z)
                    {
                        forward = f;
                        break;
                    }
                }
                return (back / (float)frames, forward / clip.frameRate);
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(go);
                EditorSceneManager.ClosePreviewScene(scene);
            }
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
