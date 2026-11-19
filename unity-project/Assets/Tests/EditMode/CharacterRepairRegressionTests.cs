using System;
using System.Collections.Generic;
using BattleHorizons.Characters;
using BattleHorizons.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BattleHorizons.Tests
{
    /// <summary>
    /// Regression tests for the targeted PR #26 repairs: shared IDs after Unity
    /// deserialization, independent Male/Female clip resolution and the
    /// single-IsAiming Animator contract. Tests use scratch assets only.
    /// </summary>
    public class CharacterRepairRegressionTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private readonly List<string> _controllerPaths = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var path in _controllerPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
            _controllerPaths.Clear();
            foreach (var obj in _objects)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _objects.Clear();
        }

        private CharacterAnimationConfig Config()
        {
            var config = ScriptableObject.CreateInstance<CharacterAnimationConfig>();
            _objects.Add(config);
            return config;
        }

        private AnimationClip Clip(string name)
        {
            var clip = new AnimationClip { name = name };
            _objects.Add(clip);
            return clip;
        }

        private static CharacterAnimAssignment Assignment(string id,
            CharacterAnimCategory category, AnimationClip clip)
        {
            return new CharacterAnimAssignment
            {
                characterId = id,
                category = category,
                clip = clip,
                sourcePath = clip.name,
                duration = 0.1f,
                frameRate = 30f,
                loop = false,
                retargetable = true,
            };
        }

        [Test]
        public void FindAssignmentHandlesNullEmptyValidUnmatchedAndIndependentIds()
        {
            var config = Config();
            var nullShared = Assignment(null, CharacterAnimCategory.Walk, Clip("NullSharedWalk"));
            var emptyShared = Assignment(string.Empty, CharacterAnimCategory.Idle, Clip("EmptySharedIdle"));
            var whitespaceShared = Assignment("  ", CharacterAnimCategory.Run, Clip("WhitespaceSharedRun"));
            var male = Assignment("MaLe", CharacterAnimCategory.Idle, Clip("MaleIdle"));
            var female = Assignment("female", CharacterAnimCategory.Idle, Clip("FemaleIdle"));
            config.assignments.AddRange(new[] { nullShared, emptyShared, whitespaceShared, male, female });

            Assert.AreSame(nullShared, config.FindAssignment("male", CharacterAnimCategory.Walk));
            Assert.AreSame(nullShared, config.FindAssignment(null, CharacterAnimCategory.Walk));
            Assert.AreSame(emptyShared, config.FindAssignment("unknown", CharacterAnimCategory.Idle));
            Assert.AreSame(emptyShared, config.FindAssignment(string.Empty, CharacterAnimCategory.Idle));
            Assert.AreSame(whitespaceShared, config.FindAssignment("male", CharacterAnimCategory.Run));
            Assert.AreSame(male, config.FindAssignment("MALE", CharacterAnimCategory.Idle));
            Assert.AreSame(female, config.FindAssignment("Female", CharacterAnimCategory.Idle));
            Assert.AreSame(emptyShared, config.FindAssignment("not-a-character", CharacterAnimCategory.Idle));
            Assert.IsNull(config.FindAssignment("male", CharacterAnimCategory.Jump));
        }

        [Test]
        public void BuildControllerUsesEmptySharedIdsAndDoesNotDuplicateAiming()
        {
            var config = Config();
            config.characters.Add(new CharacterDefinition { id = "repairmale", displayName = "Repair Male" });
            config.characters.Add(new CharacterDefinition { id = "repairfemale", displayName = "Repair Female" });
            config.AddOrUpdateAssignment(
                Assignment(string.Empty, CharacterAnimCategory.Idle, Clip("SharedIdle")), string.Empty);
            config.AddOrUpdateAssignment(
                Assignment(string.Empty, CharacterAnimCategory.Aim, Clip("SharedAim")), string.Empty);
            config.AddOrUpdateAssignment(
                Assignment("repairmale", CharacterAnimCategory.Idle, Clip("MaleIdle")), "repairmale");

            var maleResult = AnimatorControllerBuilder.BuildController(config, config.characters[0]);
            var femaleResult = AnimatorControllerBuilder.BuildController(config, config.characters[1]);
            _controllerPaths.Add(CharacterAnimationConfigLibrary.ControllersFolder + "/repairmale_Locomotion.controller");
            _controllerPaths.Add(CharacterAnimationConfigLibrary.ControllersFolder + "/repairfemale_Locomotion.controller");

            Assert.IsTrue(maleResult.success, maleResult.summary);
            Assert.IsTrue(femaleResult.success, femaleResult.summary);
            Assert.AreEqual("MaleIdle", MotionName(maleResult.controller, "Idle"));
            Assert.AreEqual("SharedIdle", MotionName(femaleResult.controller, "Idle"));

            foreach (var controller in new[] { maleResult.controller, femaleResult.controller })
            {
                var isAimingParameters = 0;
                foreach (var parameter in controller.parameters)
                {
                    if (parameter.name == CharacterAnimParameters.IsAiming) isAimingParameters++;
                }
                Assert.AreEqual(1, isAimingParameters, "exactly one IsAiming parameter");

                var aimTransitions = 0;
                foreach (var transition in controller.layers[0].stateMachine.anyStateTransitions)
                {
                    foreach (var condition in transition.conditions)
                    {
                        if (condition.parameter == CharacterAnimParameters.IsAiming) aimTransitions++;
                    }
                }
                Assert.AreEqual(1, aimTransitions, "exactly one IsAiming Any-State transition");
            }
        }

        private static string MotionName(AnimatorController controller, string stateName)
        {
            foreach (var state in controller.layers[0].stateMachine.states)
            {
                if (state.state != null && state.state.name == stateName && state.state.motion != null)
                {
                    return state.state.motion.name;
                }
            }
            return null;
        }
    }
}
