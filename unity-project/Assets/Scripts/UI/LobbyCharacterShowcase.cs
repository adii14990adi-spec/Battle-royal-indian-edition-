using BattleHorizons.Characters;
using BattleHorizons.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BattleHorizons.UI
{
    /// <summary>
    /// The real operator on the Home Screen: the configured production
    /// character (imported humanoid + real animation clips) rendered live
    /// into a transparent RenderTexture and composited over the lobby vista,
    /// exactly where the key-art soldier used to be baked into the JPEG.
    ///
    /// Reuses <see cref="CharacterPreviewController"/> (same engine as the
    /// Character Bay) with a taller 720x1024 target, a zero-alpha clear so the
    /// sunset shows through, the idle clip auto-selected and a slow turntable
    /// yaw so the lobby feels alive. When no character is configured the
    /// showcase honestly hides itself instead of showing a placeholder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyCharacterShowcase : MonoBehaviour
    {
        public const string RootName = "CharacterShowcase";

        private CharacterPreviewController _preview;
        private RawImage _image;
        private GameObject _spotlight;

        public bool HasCharacter => _preview != null && _preview.HasCharacter;
        public CharacterPreviewController Preview => _preview;

        /// <summary>Builds the showcase under <paramref name="frame"/> (the 16:9 stage).</summary>
        public static LobbyCharacterShowcase Build(Transform frame)
        {
            var go = new GameObject(RootName, typeof(RectTransform), typeof(RawImage), typeof(LobbyCharacterShowcase));
            go.transform.SetParent(frame, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.335f, 0.015f);
            rect.anchorMax = new Vector2(0.665f, 0.995f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.color = Color.white;

            var showcase = go.GetComponent<LobbyCharacterShowcase>();
            showcase._image = image;

            // Ground spotlight glow behind the operator's feet.
            var spot = new GameObject("Spotlight", typeof(RectTransform), typeof(Image));
            spot.transform.SetParent(go.transform, false);
            var spotRect = (RectTransform)spot.transform;
            spotRect.anchorMin = new Vector2(0.10f, 0.0f);
            spotRect.anchorMax = new Vector2(0.90f, 0.22f);
            spotRect.offsetMin = Vector2.zero;
            spotRect.offsetMax = Vector2.zero;
            var spotImage = spot.GetComponent<Image>();
            spotImage.sprite = LobbyArtFactory.Spotlight();
            spotImage.color = new Color(1f, 0.85f, 0.55f, 0.55f);
            spotImage.raycastTarget = false;
            showcase._spotlight = spot;

            var previewObject = new GameObject("ShowcasePreview", typeof(CharacterPreviewController));
            previewObject.transform.SetParent(go.transform, false);
            showcase._preview = previewObject.GetComponent<CharacterPreviewController>();
            showcase._preview.SetManualRendering(false);
            showcase._preview.Initialize(CharacterAnimationConfig.Load(), 720, 1024);

            var camera = showcase._preview.PreviewCamera;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            image.texture = showcase._preview.OutputTexture;

            if (!showcase._preview.HasCharacter)
            {
                GameLog.Info("menu", "character showcase hidden: " + showcase._preview.LastMessage);
                image.enabled = false;
                spot.SetActive(false);
                return showcase;
            }

            showcase.SelectIdleClip();
            showcase._preview.SetPlaying(true);
            GameLog.Info("menu", "home screen showcases " + showcase._preview.CurrentCharacterName);
            return showcase;
        }

        /// <summary>Picks the first clip whose name reads as an idle/stand loop.</summary>
        private void SelectIdleClip()
        {
            for (var i = 0; i < _preview.AnimationCount; i++)
            {
                _preview.SelectAnimation(i);
                var name = _preview.CurrentAnimationName ?? string.Empty;
                if (name.ToLowerInvariant().Contains("idle") || name.ToLowerInvariant().Contains("stand"))
                {
                    return;
                }
            }

            _preview.SelectAnimation(0);
        }

        private void Update()
        {
            if (!HasCharacter)
            {
                return;
            }

            // Gentle turntable: 9 degrees per second of yaw drift.
            _preview.Rotate(9f * Time.unscaledDeltaTime, 0f);
        }

        private void OnDestroy()
        {
            if (_preview != null)
            {
                Destroy(_preview.gameObject);
            }

            if (_spotlight != null)
            {
                Destroy(_spotlight);
            }
        }
    }
}
