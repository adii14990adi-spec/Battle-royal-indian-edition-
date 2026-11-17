using UnityEngine;
using UnityEngine.UI;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// The in-game Character Bay: a modal built in code (same overlay pattern
    /// as the existing lobby modals) that hosts the real-time
    /// <see cref="CharacterPreviewController"/>. Reachable from the existing
    /// Character hotspot on the Home Screen — the menu itself is extended, not
    /// rebuilt. When no character has been configured the modal reports that
    /// honestly instead of showing a placeholder.
    /// </summary>
    public static class CharacterBayMenu
    {
        public const string RootName = "CharacterBayModal";

        private static Text _statusLabel;
        private static Text _characterLabel;
        private static Text _animationLabel;
        private static Text _animationInfoLabel;
        private static Text _rigLabel;
        private static Text _materialLabel;
        private static Text _playLabel;
        private static Text _zoomLabel;
        private static CharacterPreviewController _preview;

        public static CharacterPreviewController Preview => _preview;

        public static GameObject Build(Transform canvas, System.Action onClose)
        {
            var existing = canvas.Find(RootName);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                RefreshLabels();
                return existing.gameObject;
            }

            var root = new GameObject(RootName, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(canvas, false);
            StretchFull((RectTransform)root.transform);
            var backdrop = root.GetComponent<Image>();
            backdrop.color = new Color(0.02f, 0.04f, 0.07f, 0.86f);
            backdrop.raycastTarget = true;
            root.GetComponent<Button>().onClick.AddListener(() =>
            {
                root.SetActive(false);
                onClose?.Invoke();
            });

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(root.transform, false);
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(1180f, 720f);
            card.GetComponent<Image>().color = new Color(0.07f, 0.11f, 0.16f, 0.98f);

            var title = CreateText(card.transform, "Title", "CHARACTER BAY",
                new Vector2(0.5f, 1f), new Vector2(900f, 54f), new Vector2(0f, -34f),
                34, FontStyle.Bold, new Color(1f, 0.85f, 0.35f, 1f));

            // ---- Preview viewport (real-time 3D) ----
            var viewport = new GameObject("PreviewViewport", typeof(RectTransform), typeof(RawImage), typeof(CharacterPreviewDragHandler));
            viewport.transform.SetParent(card.transform, false);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
            viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.sizeDelta = new Vector2(520f, 560f);
            viewportRect.anchoredPosition = new Vector2(-190f, -30f);
            var rawImage = viewport.GetComponent<RawImage>();
            rawImage.color = Color.white;
            rawImage.raycastTarget = true;

            var previewObject = new GameObject("CharacterPreview", typeof(CharacterPreviewController));
            previewObject.transform.SetParent(card.transform, false);
            _preview = previewObject.GetComponent<CharacterPreviewController>();
            _preview.SetManualRendering(false);
            viewport.GetComponent<CharacterPreviewDragHandler>().Bind(_preview);

            _preview.Initialize(CharacterAnimationConfig.Load());
            rawImage.texture = _preview.OutputTexture;

            // ---- Right-hand info + controls column ----
            var infoX = 380f;
            _characterLabel = CreateText(card.transform, "CharacterLabel", "character: —",
                new Vector2(0.5f, 1f), new Vector2(560f, 44f), new Vector2(infoX, -110f),
                26, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            _animationLabel = CreateText(card.transform, "AnimationLabel", "animation: —",
                new Vector2(0.5f, 1f), new Vector2(560f, 40f), new Vector2(infoX, -158f),
                24, FontStyle.Bold, new Color(0.4f, 0.9f, 1f, 1f), TextAnchor.MiddleLeft);
            _animationInfoLabel = CreateText(card.transform, "AnimationInfo", "",
                new Vector2(0.5f, 1f), new Vector2(560f, 40f), new Vector2(infoX, -200f),
                20, FontStyle.Normal, new Color(0.85f, 0.9f, 0.95f, 1f), TextAnchor.MiddleLeft);
            _rigLabel = CreateText(card.transform, "RigLabel", "",
                new Vector2(0.5f, 1f), new Vector2(560f, 96f), new Vector2(infoX, -270f),
                19, FontStyle.Normal, new Color(0.7f, 0.95f, 0.75f, 1f), TextAnchor.UpperLeft);
            _materialLabel = CreateText(card.transform, "MaterialLabel", "",
                new Vector2(0.5f, 1f), new Vector2(560f, 84f), new Vector2(infoX, -372f),
                19, FontStyle.Normal, new Color(0.95f, 0.9f, 0.7f, 1f), TextAnchor.UpperLeft);
            _statusLabel = CreateText(card.transform, "StatusLine", "",
                new Vector2(0.5f, 1f), new Vector2(560f, 60f), new Vector2(infoX, -470f),
                19, FontStyle.Italic, new Color(0.8f, 0.85f, 0.9f, 1f), TextAnchor.UpperLeft);

            // Character switch
            CreateButton(card.transform, "PrevCharacter", "◀ CHAR",
                new Vector2(0.5f, 1f), new Vector2(150f, 52f), new Vector2(infoX - 210f, -60f),
                new Color(0.16f, 0.32f, 0.44f, 0.96f), () =>
                {
                    _preview.PrevCharacter();
                    RefreshLabels();
                });
            CreateButton(card.transform, "NextCharacter", "CHAR ▶",
                new Vector2(0.5f, 1f), new Vector2(150f, 52f), new Vector2(infoX + 210f, -60f),
                new Color(0.16f, 0.32f, 0.44f, 0.96f), () =>
                {
                    _preview.NextCharacter();
                    RefreshLabels();
                });

            // Animation switch
            CreateButton(card.transform, "PrevAnimation", "◀ CLIP",
                new Vector2(0.5f, 1f), new Vector2(150f, 52f), new Vector2(infoX - 210f, -500f),
                new Color(0.18f, 0.28f, 0.38f, 0.96f), () =>
                {
                    _preview.PrevAnimation();
                    RefreshLabels();
                });
            CreateButton(card.transform, "NextAnimation", "CLIP ▶",
                new Vector2(0.5f, 1f), new Vector2(150f, 52f), new Vector2(infoX + 210f, -500f),
                new Color(0.18f, 0.28f, 0.38f, 0.96f), () =>
                {
                    _preview.NextAnimation();
                    RefreshLabels();
                });

            // Play/pause + zoom + rotate hint
            CreateButton(card.transform, "PlayPause", "PAUSE",
                new Vector2(0.5f, 1f), new Vector2(160f, 52f), new Vector2(infoX - 210f, -560f),
                new Color(0.20f, 0.42f, 0.28f, 0.96f), () =>
                {
                    _preview.TogglePlayPause();
                    RefreshLabels();
                }, out _playLabel);
            CreateButton(card.transform, "ZoomIn", "ZOOM +",
                new Vector2(0.5f, 1f), new Vector2(150f, 52f), new Vector2(infoX, -560f),
                new Color(0.16f, 0.32f, 0.44f, 0.96f), () =>
                {
                    _preview.ZoomBy(-0.4f);
                    RefreshLabels();
                });
            CreateButton(card.transform, "ZoomOut", "ZOOM −",
                new Vector2(0.5f, 1f), new Vector2(150f, 52f), new Vector2(infoX + 210f, -560f),
                new Color(0.16f, 0.32f, 0.44f, 0.96f), () =>
                {
                    _preview.ZoomBy(0.4f);
                    RefreshLabels();
                });

            // Rotate hint under the viewport
            CreateText(card.transform, "RotateHint", "drag the preview to rotate · pinch/scroll to zoom",
                new Vector2(0.5f, 0f), new Vector2(560f, 34f), new Vector2(-190f, 34f),
                18, FontStyle.Italic, new Color(0.65f, 0.72f, 0.8f, 1f));

            CreateButton(card.transform, "CloseButton", "CLOSE",
                new Vector2(0.5f, 0f), new Vector2(280f, 58f), new Vector2(0f, 36f),
                new Color(0.88f, 0.66f, 0.16f, 0.98f), () =>
                {
                    root.SetActive(false);
                    onClose?.Invoke();
                });

            RefreshLabels();
            return root;
        }

        /// <summary>Push the current preview state into the cached labels.</summary>
        public static void RefreshLabels()
        {
            if (_preview == null)
            {
                return;
            }

            if (_characterLabel != null)
            {
                _characterLabel.text = "character: " + _preview.CurrentCharacterName;
            }

            if (_animationLabel != null)
            {
                _animationLabel.text = "animation: " + _preview.CurrentAnimationName;
            }

            if (_animationInfoLabel != null)
            {
                _animationInfoLabel.text = _preview.CurrentAnimationInfo;
            }

            if (_rigLabel != null)
            {
                _rigLabel.text = _preview.RigStatusText;
            }

            if (_materialLabel != null)
            {
                _materialLabel.text = _preview.MaterialStatusText;
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = _preview.LastMessage
                    + string.Format("\nzoom: {0:0.0} m · yaw: {1:0}°", _preview.Distance, _preview.Yaw);
            }

            if (_playLabel != null)
            {
                _playLabel.text = _preview.IsPlaying ? "PAUSE" : "PLAY";
            }

            if (_zoomLabel != null)
            {
                _zoomLabel.text = string.Format("ZOOM {0:0.0}", _preview.Distance);
            }
        }

        private static Text CreateText(
            Transform parent, string name, string text,
            Vector2 anchor, Vector2 size, Vector2 anchoredPosition,
            int fontSize, FontStyle style, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private static Button CreateButton(
            Transform parent, string name, string labelText,
            Vector2 anchor, Vector2 size, Vector2 anchoredPosition,
            Color background, UnityEngine.Events.UnityAction onClick,
            out Text labelComponent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var image = go.GetComponent<Image>();
            image.color = background;
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var label = CreateText(go.transform, "Label", labelText,
                new Vector2(0.5f, 0.5f), size, Vector2.zero,
                22, FontStyle.Bold, Color.white);
            label.transform.SetParent(go.transform, false);
            ((RectTransform)label.transform).anchorMin = Vector2.zero;
            ((RectTransform)label.transform).anchorMax = Vector2.one;
            ((RectTransform)label.transform).sizeDelta = Vector2.zero;
            ((RectTransform)label.transform).anchoredPosition = Vector2.zero;
            labelComponent = label;
            return button;
        }

        private static Button CreateButton(
            Transform parent, string name, string labelText,
            Vector2 anchor, Vector2 size, Vector2 anchoredPosition,
            Color background, UnityEngine.Events.UnityAction onClick)
        {
            return CreateButton(parent, name, labelText, anchor, size, anchoredPosition,
                background, onClick, out _);
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
