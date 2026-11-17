using System.Collections;
using BattleHorizons.Characters;
using BattleHorizons.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattleHorizons.Tests.PlayMode
{
    /// <summary>
    /// Runtime behaviour of the character &amp; animation system: the preview
    /// controller renders into its RenderTexture without external
    /// dependencies, the Character Bay modal opens from the existing Home
    /// Screen hotspot, and the player visual spawns the configured character
    /// (or honestly stays a capsule when nothing is configured).
    /// </summary>
    public class CharacterPreviewRuntimeTests
    {
        private GameObject _menuRoot;
        private GameObject _previewRoot;
        private GameObject _playerRoot;

        [TearDown]
        public void Cleanup()
        {
            if (_menuRoot != null) Object.Destroy(_menuRoot);
            if (_previewRoot != null) Object.Destroy(_previewRoot);
            if (_playerRoot != null) Object.Destroy(_playerRoot);
            _menuRoot = null;
            _previewRoot = null;
            _playerRoot = null;
        }

        [UnityTest]
        public IEnumerator PreviewController_InitializesTextureAndSurvivesNavigation()
        {
            _previewRoot = new GameObject("PreviewUnderTest");
            var preview = _previewRoot.AddComponent<CharacterPreviewController>();
            var config = CharacterAnimationConfig.Load();
            preview.Initialize(config);
            yield return null;

            Assert.IsNotNull(preview.OutputTexture, "preview must own a RenderTexture");
            Assert.IsTrue(preview.OutputTexture.IsCreated(), "RenderTexture must be created");
            Assert.IsNotNull(preview.LastMessage, "preview must report status");

            // Navigation APIs must never throw, configured or not.
            preview.NextCharacter();
            preview.PrevCharacter();
            preview.NextAnimation();
            preview.PrevAnimation();
            preview.TogglePlayPause();
            preview.Rotate(15f, 5f);
            preview.ZoomBy(-0.5f);
            preview.Tick(1f / 60f);
            preview.RenderOnce();
            yield return null;

            Assert.IsNotNull(preview.CurrentCharacterName);
            Assert.IsNotNull(preview.CurrentAnimationName);
            Assert.IsNotNull(preview.RigStatusText);
            Assert.IsNotNull(preview.MaterialStatusText);

            if (preview.HasCharacter)
            {
                // A configured character must expose real rig/material state.
                StringAssert.Contains("rig:", preview.RigStatusText);
                Assert.IsNotNull(preview.CharacterAnimator == null ? null : preview.CharacterAnimator,
                    "spawned character must have an Animator");
            }
        }

        [UnityTest]
        public IEnumerator CharacterBay_OpensFromHomeScreenCharacterHotspot()
        {
            _menuRoot = new GameObject("MainMenu");
            _menuRoot.AddComponent<BattleHorizons.UI.MainMenuController>();
            yield return null;

            var canvas = _menuRoot.transform.Find("MainMenuCanvas");
            Assert.IsNotNull(canvas, "MainMenu must build its canvas");
            var characterButton = canvas.Find("HomeArtworkFrame/CharacterButton");
            Assert.IsNotNull(characterButton,
                "CharacterButton hotspot must exist on the Home Screen");

            var button = characterButton.GetComponent<UnityEngine.UI.Button>();
            Assert.IsNotNull(button, "CharacterButton must be a Button");
            button.onClick.Invoke();
            yield return null;

            var bay = canvas.Find(CharacterBayMenu.RootName);
            Assert.IsNotNull(bay,
                "clicking CharacterButton must open the Character Bay modal");
            Assert.IsTrue(bay.gameObject.activeSelf, "modal must be visible");

            var preview = bay.GetComponentInChildren<CharacterPreviewController>(true);
            Assert.IsNotNull(preview, "Character Bay must host a real-time preview controller");
            Assert.IsNotNull(preview.OutputTexture,
                "preview RenderTexture must be bound to the modal viewport");
        }

        [UnityTest]
        public IEnumerator PlayerCharacterVisual_SpawnsConfiguredCharacterOrStaysCapsule()
        {
            _playerRoot = new GameObject("PlayerUnderTest");
            var motor = _playerRoot.AddComponent<ThirdPersonMotor>();
            var visual = _playerRoot.AddComponent<PlayerCharacterVisual>();
            yield return null;
            yield return null;

            var config = CharacterAnimationConfig.Load();
            var character = config != null ? config.SelectedCharacter() : null;
            var hasUsableCharacter = character != null && character.model != null;

            if (hasUsableCharacter)
            {
                Assert.IsTrue(visual.IsCharacterSpawned,
                    "configured character must spawn under the player: " + character.displayName);
                Assert.IsNotNull(visual.CharacterAnimator,
                    "spawned character must expose its Animator");
                Assert.IsNotNull(visual.ActiveDefinition);
            }
            else
            {
                Assert.IsFalse(visual.IsCharacterSpawned,
                    "without a configured character the visual must stay dormant (capsule)");
            }

            // Hooks must be safe in both states.
            visual.SetAiming(true);
            visual.TriggerShoot();
            visual.TriggerReload();
            visual.TriggerDeath();
            visual.ResetToDefaultState();
            yield return null;
            Assert.IsNotNull(motor);
        }
    }
}
