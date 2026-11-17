using System;
using System.Collections.Generic;
using BattleHorizons.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Auto-generates / updates Animator Controllers from the saved
    /// assignments (§ auto-generate Animator controllers):
    ///
    ///  * parameters: Speed, VerticalSpeed, IsAirborne, IsCrouching, IsAiming,
    ///    Shoot, Reload, Death, WeaponSwitch, Melee
    ///  * locomotion states (Idle/Walk/Run/Sprint) driven by Speed,
    ///    Crouch as a 1D blend (Crouch ↔ Crouch Walk),
    ///  * Jump / Fall via IsAirborne + VerticalSpeed,
    ///  * weapon one-shots (Shoot, Reload, WeaponSwitch, Melee) as triggers,
    ///  * Death as an absorbing Any State,
    ///  * a second UpperBody layer with an AvatarMask (legs/root disabled)
    ///    holding Rifle Idle / Rifle Run for aiming,
    ///  * smooth transitions (0.12s damping, no forced exit time),
    ///  * no animation events (none are required by this setup),
    ///  * missing clips are skipped with a warning instead of generating a
    ///    broken controller.
    /// </summary>
    public static class AnimatorControllerBuilder
    {
        public const string LocomotionState = "Locomotion"; // marker state when no movement clips yet

        private static readonly (CharacterAnimCategory category, float speed, bool triggerLike)[] SpeedStates =
        {
            (CharacterAnimCategory.Idle, 0f, false),
            (CharacterAnimCategory.Walk, 1.5f, false),
            (CharacterAnimCategory.Run, 4.5f, false),
            (CharacterAnimCategory.Sprint, 7f, false),
        };

        private static readonly CharacterAnimCategory[] RequiredCategories =
        {
            CharacterAnimCategory.Idle,
        };

        public sealed class BuildResult
        {
            public AnimatorController controller;
            public readonly List<string> warnings = new List<string>();
            public readonly List<string> missing = new List<string>();
            public bool success;
            public string summary = string.Empty;
        }

        /// <summary>
        /// Build (or rebuild) the Locomotion controller for one character at
        /// Assets/AnimationControllers/{id}_Locomotion.controller.
        /// </summary>
        public static BuildResult BuildController(CharacterAnimationConfig config, CharacterDefinition character)
        {
            var result = new BuildResult();
            if (config == null || character == null)
            {
                result.summary = "no configuration";
                return result;
            }

            // ---- resolve clips -------------------------------------------
            var resolved = new Dictionary<CharacterAnimCategory, AnimationClip>();
            for (var i = 0; i < config.assignments.Count; i++)
            {
                var assignment = config.assignments[i];
                if (assignment == null || assignment.clip == null)
                {
                    continue;
                }

                if (assignment.characterId != null
                    && !string.Equals(assignment.characterId, character.id, StringComparison.Ordinal))
                {
                    continue;
                }

                // Per-character entries win over the shared default (null id).
                if (!resolved.ContainsKey(assignment.category) || assignment.characterId != null)
                {
                    resolved[assignment.category] = assignment.clip;
                }
            }

            foreach (var required in RequiredCategories)
            {
                if (!resolved.ContainsKey(required))
                {
                    result.summary = "cannot build controller for " + character.displayName
                        + ": required category '" + CharacterAnimCategories.DisplayName(required)
                        + "' has no assigned clip";
                    result.missing.Add(required.ToString());
                    return result;
                }
            }

            CharacterAnimationConfigLibrary.EnsureFolder(
                CharacterAnimationConfigLibrary.ControllersFolder);
            CharacterAnimationConfigLibrary.EnsureFolder(
                CharacterAnimationConfigLibrary.AvatarMasksFolder);

            var controllerPath = CharacterAnimationConfigLibrary.ControllersFolder + "/"
                + character.id + "_Locomotion.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (controller == null)
            {
                result.summary = "could not create controller at " + controllerPath;
                return result;
            }

            // ---- parameters ----------------------------------------------
            AddParameter(controller, CharacterAnimParameters.Speed, AnimatorControllerParameterType.Float);
            AddParameter(controller, CharacterAnimParameters.VerticalSpeed, AnimatorControllerParameterType.Float);
            AddParameter(controller, CharacterAnimParameters.IsAirborne, AnimatorControllerParameterType.Bool);
            AddParameter(controller, CharacterAnimParameters.IsCrouching, AnimatorControllerParameterType.Bool);
            AddParameter(controller, CharacterAnimParameters.IsAiming, AnimatorControllerParameterType.Bool);
            AddParameter(controller, CharacterAnimParameters.Shoot, AnimatorControllerParameterType.Trigger);
            AddParameter(controller, CharacterAnimParameters.Reload, AnimatorControllerParameterType.Trigger);
            AddParameter(controller, CharacterAnimParameters.Death, AnimatorControllerParameterType.Trigger);
            AddParameter(controller, CharacterAnimParameters.WeaponSwitch, AnimatorControllerParameterType.Trigger);
            AddParameter(controller, CharacterAnimParameters.Melee, AnimatorControllerParameterType.Trigger);

            // ---- base layer ------------------------------------------------
            var baseLayer = controller.layers[0];
            var machine = baseLayer.stateMachine;
            machine.name = "Locomotion";

            var defaultState = AddState(machine, "Idle", ClipFor(resolved, CharacterAnimCategory.Idle, result));
            machine.defaultState = defaultState;

            // Speed-driven states (missing clips are skipped with warnings).
            var speedBounds = new List<(float low, float high, ChildAnimatorState state)>();
            for (var i = 0; i < SpeedStates.Length; i++)
            {
                var entry = SpeedStates[i];
                var clip = ClipFor(resolved, entry.category, result);
                if (clip == null && entry.category != CharacterAnimCategory.Idle)
                {
                    continue;
                }

                var stateName = entry.category.ToString();
                var state = stateName == "Idle"
                    ? defaultState
                    : AddState(machine, stateName, clip);
                var high = i + 1 < SpeedStates.Length ? SpeedStates[i + 1].speed : float.MaxValue;
                speedBounds.Add((entry.speed, high, new ChildAnimatorState { state = state, stateMachine = machine }));
            }

            // Zone-based Any State speed transitions (no exit time, smooth).
            for (var i = 0; i < speedBounds.Count; i++)
            {
                var bound = speedBounds[i];
                var transition = machine.AddAnyStateTransition(bound.state.state);
                ConfigureMotionless(transition);
                transition.AddCondition(AnimatorConditionMode.Greater, bound.low - 0.001f,
                    CharacterAnimParameters.Speed);
                if (bound.high != float.MaxValue)
                {
                    transition.AddCondition(AnimatorConditionMode.Less, bound.high,
                        CharacterAnimParameters.Speed);
                }
            }

            // ---- Crouch (1D blend: Crouch ↔ Crouch Walk) --------------------
            var crouchClip = ClipFor(resolved, CharacterAnimCategory.Crouch, result);
            var crouchWalkClip = ClipFor(resolved, CharacterAnimCategory.CrouchWalk, result);
            if (crouchClip != null)
            {
                var blend = BlendTreeHelper.CreateBlendTree1D(
                    "Crouch", CharacterAnimParameters.Speed,
                    new[]
                    {
                        (0f, crouchClip),
                        (1.6f, crouchWalkClip != null ? crouchWalkClip : crouchClip),
                    });
                // The tree must live inside the controller asset or it would
                // be lost on the next domain reload / player build.
                AssetDatabase.AddObjectToAsset(blend, controller);
                var crouchState = machine.AddState("Crouch");
                crouchState.motion = blend;
                AttachCrouchTransitions(machine, crouchState);

                if (crouchWalkClip == null)
                {
                    result.warnings.Add("Crouch Walk not assigned — Crouch blend tree uses the Crouch clip only");
                }
            }
            else
            {
                result.warnings.Add("Crouch not assigned — crouch locomotion state skipped");
            }

            // ---- Airborne ---------------------------------------------------
            var jumpClip = ClipFor(resolved, CharacterAnimCategory.Jump, result);
            var fallClip = ClipFor(resolved, CharacterAnimCategory.Fall, result);
            if (jumpClip != null)
            {
                var jumpState = AddState(machine, "Jump", jumpClip);
                AttachAirborneTransitions(machine, jumpState);
            }
            else
            {
                result.warnings.Add("Jump not assigned — airborne state skipped");
            }

            if (fallClip != null)
            {
                var fallState = AddState(machine, "Fall", fallClip);
                AttachAirborneTransitions(machine, fallState);
            }
            else
            {
                result.warnings.Add("Fall not assigned — fall state skipped");
            }

            // ---- weapon one-shots -------------------------------------------
            AttachOneShot(machine, resolved, CharacterAnimCategory.Shoot, result);
            AttachOneShot(machine, resolved, CharacterAnimCategory.Reload, result);
            AttachOneShot(machine, resolved, CharacterAnimCategory.WeaponSwitch, result);
            AttachOneShot(machine, resolved, CharacterAnimCategory.Melee, result);

            // ---- Death (absorbing) ------------------------------------------
            var deathClip = ClipFor(resolved, CharacterAnimCategory.Death, result);
            if (deathClip != null)
            {
                var deathState = AddState(machine, "Death", deathClip);
                var deathTransition = deathState.AddTransition(FindState(machine, "Idle"));
                ConfigureMotionless(deathTransition);
                deathTransition.hasExitTime = true;
                deathTransition.exitTime = 1f;
                deathTransition.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimParameters.Death);
            }
            else
            {
                result.warnings.Add("Death not assigned — death trigger will do nothing");
            }

            // ---- Aim (grounded upper-body stance lives on layer 1) -----------
            var aimClip = ClipFor(resolved, CharacterAnimCategory.Aim, result);
            if (aimClip != null)
            {
                var aimState = AddState(machine, "Aim", aimClip);
                AttachAimTransitions(machine, aimState);
            }

            // ---- UpperBody layer + AvatarMask --------------------------------
            var rifleIdle = ClipFor(resolved, CharacterAnimCategory.RifleIdle, result);
            var rifleRun = ClipFor(resolved, CharacterAnimCategory.RifleRun, result);
            if (rifleIdle != null || rifleRun != null)
            {
                var mask = BuildUpperBodyMask(
                    CharacterAnimationConfigLibrary.AvatarMasksFolder + "/UpperBody.mask");
                var layer = new AnimatorControllerLayer
                {
                    name = "UpperBody",
                    defaultWeight = 0f,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    avatarMask = mask,
                    stateMachine = new AnimatorStateMachine(),
                };
                AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
                layer.stateMachine.name = "UpperBodyMachine";
                controller.AddLayer(layer);

                var upperMachine = controller.layers[controller.layers.Length - 1].stateMachine;
                if (rifleIdle != null)
                {
                    var idleState = AddState(upperMachine, "RifleIdle", rifleIdle);
                    idleState.writeDefaultValues = false;
                    upperMachine.defaultState = idleState;

                    if (rifleRun != null)
                    {
                        var runState = AddState(upperMachine, "RifleRun", rifleRun);
                        runState.writeDefaultValues = false;
                        var toRun = idleState.AddTransition(runState);
                        ConfigureMotionless(toRun);
                        toRun.AddCondition(AnimatorConditionMode.Greater, 3.3f, CharacterAnimParameters.Speed);
                        var toIdle = runState.AddTransition(idleState);
                        ConfigureMotionless(toIdle);
                        toIdle.AddCondition(AnimatorConditionMode.Less, 3.3f, CharacterAnimParameters.Speed);
                    }
                }
                else if (rifleRun != null)
                {
                    var runState = AddState(upperMachine, "RifleRun", rifleRun);
                    runState.writeDefaultValues = false;
                    upperMachine.defaultState = runState;
                }
            }
            else
            {
                result.warnings.Add("no Rifle Idle / Rifle Run assigned — UpperBody aiming layer skipped");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            character.controller = controller;
            result.controller = controller;
            result.success = true;
            result.summary = "controller built at " + controllerPath
                + " (" + controller.layers.Length + " layer(s), "
                + result.missing.Count + " missing clip(s), "
                + result.warnings.Count + " warning(s))";
            return result;
        }

        // ------------------------------------------------------------------

        private static AnimationClip ClipFor(
            Dictionary<CharacterAnimCategory, AnimationClip> resolved,
            CharacterAnimCategory category,
            BuildResult result)
        {
            if (resolved.TryGetValue(category, out var clip) && clip != null)
            {
                return clip;
            }

            if (!result.missing.Contains(category.ToString()))
            {
                result.missing.Add(category.ToString());
                result.warnings.Add(CharacterAnimCategories.DisplayName(category)
                    + " not assigned — related state skipped");
            }

            return null;
        }

        /// <summary>
        /// AnimatorStateMachine has no GetState API — look the state up by
        /// name in the state list instead.
        /// </summary>
        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            var states = machine.states;
            for (var i = 0; i < states.Length; i++)
            {
                if (states[i].state != null && states[i].state.name == name)
                {
                    return states[i].state;
                }
            }

            return null;
        }

        private static void AddParameter(AnimatorController controller, string name,
            AnimatorControllerParameterType type)
        {
            if (controller.parameters == null || controller.parameters.Length == 0)
            {
                controller.AddParameter(name, type);
                return;
            }

            for (var i = 0; i < controller.parameters.Length; i++)
            {
                if (controller.parameters[i].name == name)
                {
                    return;
                }
            }

            controller.AddParameter(name, type);
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion)
        {
            var state = machine.AddState(name);
            state.motion = motion;
            state.tag = string.Empty;
            return state;
        }

        /// <summary>Exit-time-free, smoothed transition defaults.</summary>
        private static void ConfigureMotionless(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.exitTime = 1f;
            transition.hasFixedDuration = false;
            transition.duration = 0.12f;
            transition.offset = 0f;
            transition.canTransitionToSelf = false;
        }

        private static void AttachCrouchTransitions(AnimatorStateMachine machine, AnimatorState crouchState)
        {
            var toCrouch = machine.AddAnyStateTransition(crouchState);
            ConfigureMotionless(toCrouch);
            toCrouch.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimParameters.IsCrouching);

            var fromCrouch = crouchState.AddTransition(FindState(machine, "Idle"));
            ConfigureMotionless(fromCrouch);
            fromCrouch.AddCondition(AnimatorConditionMode.IfNot, 0f, CharacterAnimParameters.IsCrouching);
        }

        private static void AttachAirborneTransitions(AnimatorStateMachine machine, AnimatorState airborneState)
        {
            var entry = machine.AddAnyStateTransition(airborneState);
            ConfigureMotionless(entry);
            entry.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimParameters.IsAirborne);
            // Which airborne state: Jump rises, Fall descends.
            if (airborneState.name == "Jump")
            {
                entry.AddCondition(AnimatorConditionMode.Greater, 0.5f, CharacterAnimParameters.VerticalSpeed);
            }
            else
            {
                entry.AddCondition(AnimatorConditionMode.Less, 0.5f, CharacterAnimParameters.VerticalSpeed);
            }

            var exit = airborneState.AddTransition(FindState(machine, "Idle"));
            ConfigureMotionless(exit);
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, CharacterAnimParameters.IsAirborne);
        }

        private static void AttachOneShot(
            AnimatorStateMachine machine,
            Dictionary<CharacterAnimCategory, AnimationClip> resolved,
            CharacterAnimCategory category,
            BuildResult result)
        {
            var clip = ClipFor(resolved, category, result);
            if (clip == null)
            {
                return;
            }

            var state = AddState(machine, category.ToString(), clip);
            var entry = machine.AddAnyStateTransition(state);
            ConfigureMotionless(entry);
            entry.AddCondition(AnimatorConditionMode.If, 0f, TriggerParameterFor(category));

            var exit = state.AddTransition(FindState(machine, "Idle"));
            exit.hasExitTime = true;
            exit.exitTime = 1f;
            exit.hasFixedDuration = false;
            exit.duration = 0.12f;
            exit.offset = 0f;
            exit.canTransitionToSelf = false;
        }

        private static string TriggerParameterFor(CharacterAnimCategory category)
        {
            switch (category)
            {
                case CharacterAnimCategory.Shoot:
                    return CharacterAnimParameters.Shoot;
                case CharacterAnimCategory.Reload:
                    return CharacterAnimParameters.Reload;
                case CharacterAnimCategory.WeaponSwitch:
                    return CharacterAnimParameters.WeaponSwitch;
                case CharacterAnimCategory.Melee:
                    return CharacterAnimParameters.Melee;
                case CharacterAnimCategory.Death:
                    return CharacterAnimParameters.Death;
                default:
                    return CharacterAnimParameters.Shoot;
            }
        }

        private static void AttachAimTransitions(AnimatorStateMachine machine, AnimatorState aimState)
        {
            var enter = machine.AddAnyStateTransition(aimState);
            ConfigureMotionless(enter);
            enter.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimParameters.IsAiming);
            enter.AddCondition(AnimatorConditionMode.Less, 0.4f, CharacterAnimParameters.Speed);
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, CharacterAnimParameters.IsCrouching);
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, CharacterAnimParameters.IsAirborne);

            var exit = aimState.AddTransition(FindState(machine, "Idle"));
            ConfigureMotionless(exit);
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, CharacterAnimParameters.IsAiming);
        }

        private static AvatarMask BuildUpperBodyMask(string maskPath)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            var isNew = mask == null;
            if (isNew)
            {
                mask = new AvatarMask { name = "UpperBody" };
            }

            // Whole-body off; torso, arms, hands, fingers and head on.
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.SourceOBj, true);
            mask.transformCount = 0; // humanoid masks don't need transform entries

            if (isNew)
            {
                AssetDatabase.CreateAsset(mask, maskPath);
            }

            EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>
        /// Scratch controller used by the editor preview tab before any
        /// per-character assignments have been saved. Only the clips present
        /// are wired; states are selected explicitly by name.
        /// </summary>
        public static AnimatorController BuildScratchPreviewController(
            string controllerPath, IList<AnimationClip> clips)
        {
            CharacterAnimationConfigLibrary.EnsureFolder(
                PathOfFolder(controllerPath));
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (controller == null)
            {
                return null;
            }

            controller.AddParameter(CharacterAnimParameters.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterAnimParameters.IsAirborne, AnimatorControllerParameterType.Bool);
            controller.AddParameter(CharacterAnimParameters.IsCrouching, AnimatorControllerParameterType.Bool);

            var machine = controller.layers[0].stateMachine;
            AnimatorState firstState = null;
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip == null)
                {
                    continue;
                }

                var state = machine.AddState(clip.name);
                state.motion = clip;
                if (firstState == null)
                {
                    firstState = state;
                }
            }

            if (firstState != null)
            {
                machine.defaultState = firstState;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static string PathOfFolder(string filePath)
        {
            var index = filePath.LastIndexOf('/');
            return index > 0 ? filePath.Substring(0, index) : "Assets";
        }
    }

    /// <summary>
    /// Small helper around BlendTree assets (kept separate to make the
    /// 1D movement blend explicit and testable).
    /// </summary>
    public static class BlendTreeHelper
    {
        public static BlendTree CreateBlendTree1D<T>(
            string name, string parameter, IList<(float threshold, T motion)> children)
            where T : Motion
        {
            var tree = ScriptableObject.CreateInstance<BlendTree>();
            tree.name = name;
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = parameter;
            tree.useAutomaticThresholds = false;
            for (var i = 0; i < children.Count; i++)
            {
                tree.AddChild(children[i].motion, children[i].threshold);
            }

            return tree;
        }
    }
}
