using BattleHorizons.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleHorizons.UI
{
    /// <summary>
    /// Interactive Battle Horizons: Last Survive Home Screen (Lobby).
    /// Renders the 16:9 Fort Kargha / Operator lobby key art with aspect-safe
    /// framing, interactive Store / Luck Royale / Battle Pass / Missions / Events /
    /// Vault / Weapon / Character / Profile / Settings / Notifications hotspots,
    /// live Solo / Duo / Squad match mode selector, and the golden START button
    /// launching Siran Island via <see cref="LoadingScreenView"/>.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string HomeBackgroundResourcePath = "UI/battle_horizons_home_bg";

        public enum MatchMode
        {
            Solo,
            Duo,
            Squad,
        }

        private static Sprite _cachedHomeSprite;

        private MatchMode _selectedMode = MatchMode.Squad;
        private Text _modeTitleLabel;
        private Image _soloHighlight;
        private Image _duoHighlight;
        private Image _squadHighlight;

        private GameObject _modalRoot;
        private Text _modalTitle;
        private Text _modalBody;
        private GameObject _settingsActionsGroup;
        private Text _qualityButtonLabel;

        public MatchMode SelectedMode => _selectedMode;
        public string ModeHeaderText => _modeTitleLabel != null ? _modeTitleLabel.text : string.Empty;

        private void Start()
        {
            BuildUi();
            GameLog.Info("menu", "home screen lobby ready");
        }

        private void BuildUi()
        {
            MobileControlsBuilder.EnsureEventSystem();

            var canvasObject = new GameObject("MainMenuCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var homeSprite = LoadHomeSprite();

            // Full-bleed atmospheric backdrop for ultra-wide (20:9) mobile displays.
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            background.transform.SetParent(canvasObject.transform, false);
            StretchFull((RectTransform)background.transform);
            var backgroundImage = background.GetComponent<Image>();
            if (homeSprite != null)
            {
                backgroundImage.sprite = homeSprite;
                backgroundImage.color = new Color(0.30f, 0.31f, 0.35f, 1f);
            }
            else
            {
                backgroundImage.color = new Color(0.06f, 0.09f, 0.14f, 1f);
            }
            backgroundImage.raycastTarget = false;
            var bgFitter = background.GetComponent<AspectRatioFitter>();
            bgFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            bgFitter.aspectRatio = 16f / 9f;

            // Exact 16:9 Home Screen frame so top currency bar, side nav, bottom loadout,
            // mode selector, and START button never crop on any mobile aspect ratio.
            var frame = new GameObject("HomeArtworkFrame", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            frame.transform.SetParent(canvasObject.transform, false);
            StretchFull((RectTransform)frame.transform);
            var frameImage = frame.GetComponent<Image>();
            if (homeSprite != null)
            {
                frameImage.sprite = homeSprite;
                frameImage.color = Color.white;
            }
            else
            {
                frameImage.color = new Color(0.10f, 0.13f, 0.16f, 1f);
            }
            frameImage.raycastTarget = false;
            var frameFitter = frame.GetComponent<AspectRatioFitter>();
            frameFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            frameFitter.aspectRatio = 16f / 9f;

            // Dynamic MODE header ("Battle Royale - Squad" / "Duo" / "Solo")
            _modeTitleLabel = CreateOverlayLabel(
                frame.transform,
                "ModeTitle",
                new Vector2(0.7788f, 0.2205f),
                new Vector2(0.9595f, 0.2578f),
                "Battle Royale - Squad",
                TextAnchor.MiddleLeft,
                24,
                Color.white);

            // Solo / Duo / Squad selector buttons + active cyan underline highlights
            CreateModeButton(frame.transform, "ModeSolo", new Vector2(0.7715f, 0.1476f), new Vector2(0.8359f, 0.2188f),
                MatchMode.Solo, out _soloHighlight);
            CreateModeButton(frame.transform, "ModeDuo", new Vector2(0.8369f, 0.1476f), new Vector2(0.9023f, 0.2188f),
                MatchMode.Duo, out _duoHighlight);
            CreateModeButton(frame.transform, "ModeSquad", new Vector2(0.9033f, 0.1476f), new Vector2(0.9707f, 0.2188f),
                MatchMode.Squad, out _squadHighlight);

            // Golden START button (named EnterSiranIsland for PlayMode test & scene launch)
            CreateHotspotButton(
                frame.transform,
                "EnterSiranIsland",
                new Vector2(0.7690f, 0.0295f),
                new Vector2(0.9741f, 0.1363f),
                StartMatch,
                new Color(1f, 0.92f, 0.45f, 0.18f),
                new Color(1f, 0.80f, 0.20f, 0.32f));

            // Left navigation stack
            CreateHotspotButton(frame.transform, "StoreButton", new Vector2(0.0342f, 0.6771f), new Vector2(0.1914f, 0.7613f),
                () => ShowInfoModal("STORE", "Featured Season 05 Tactical Bundles, Desert Camo Skins, and Supply Crates.\n\nOffline Prototype Build — Store purchases are disabled."));
            CreateHotspotButton(frame.transform, "LuckRoyaleButton", new Vector2(0.0342f, 0.5799f), new Vector2(0.1914f, 0.6632f),
                () => ShowInfoModal("LUCK ROYALE", "Fort Kargha Gold Spin & Tactical Gear Draw.\n\nAvailable Coins: 45,820   |   Gems: 1,240"));
            CreateHotspotButton(frame.transform, "BattlePassButton", new Vector2(0.0342f, 0.4826f), new Vector2(0.1914f, 0.5660f),
                () => ShowInfoModal("BATTLE PASS — SEASON 05", "Tier 18 Unlocked  •  Fort Kargha Vanguard\n\nComplete survival matches on Siran Island to unlock Season 05 rewards."));
            CreateHotspotButton(frame.transform, "MissionsButton", new Vector2(0.0342f, 0.3854f), new Vector2(0.1914f, 0.4688f),
                () => ShowInfoModal("DAILY MISSIONS", "• Explore Siran Island Greybox (0/1)\n• Sprint 500m across rugged terrain (0/500)\n• Survive in the Battlezone (Reward: 500 Coins)"));
            CreateHotspotButton(frame.transform, "EventsButton", new Vector2(0.0342f, 0.2882f), new Vector2(0.1914f, 0.3715f),
                () => ShowInfoModal("EVENTS — PATCH 2.4.1", "Fort Kargha & Siran Island Reconnaissance Event Active!\n\nTap START in the bottom-right corner to deploy."));

            // Bottom-left loadout bar
            CreateHotspotButton(frame.transform, "VaultButton", new Vector2(0.0220f, 0.0469f), new Vector2(0.1396f, 0.1146f),
                () => ShowInfoModal("VAULT", "Equipped Outfit: Special Operations Olive Tactical Rig + NVG Helmet\nParachute: Battle Horizons Gold Emblem"));
            CreateHotspotButton(frame.transform, "WeaponButton", new Vector2(0.1455f, 0.0469f), new Vector2(0.2725f, 0.1146f),
                () => ShowInfoModal("WEAPON ARMORY", "Primary Slot: Assault Rifle (Phase 2 Combat System)\nMelee: Tactical Combat Blades"));
            CreateHotspotButton(frame.transform, "CharacterButton", new Vector2(0.2793f, 0.0469f), new Vector2(0.4063f, 0.1146f),
                () => ShowInfoModal("OPERATOR — COMMANDER_K", "Role: Reconnaissance & Survival Specialist\nPassive: +10% Stamina Recovery on Siran Island"));

            // Top-right profile, settings & notifications
            CreateHotspotButton(frame.transform, "ProfileButton", new Vector2(0.5249f, 0.8993f), new Vector2(0.6758f, 0.9670f),
                () => ShowInfoModal("OPERATOR PROFILE", "Callsign: Commander_K\nSeason 05 Rank: Vanguard IV\nPreferred Mode: " + ModeDisplayName(_selectedMode)));
            CreateHotspotButton(frame.transform, "SettingsButton", new Vector2(0.8799f, 0.9028f), new Vector2(0.9238f, 0.9635f),
                ShowSettingsModal);
            CreateHotspotButton(frame.transform, "NotificationsButton", new Vector2(0.9248f, 0.9028f), new Vector2(0.9688f, 0.9635f),
                () => ShowInfoModal("NOTIFICATIONS", "Welcome to Battle Horizons: Last Survive (Patch 2.4.1).\nSiran Island 4000x4000m terrain & mobile touch controls are ready."));

            BuildModalOverlay(canvasObject.transform);
            SelectMode(MatchMode.Squad);
        }

        public void SelectMode(MatchMode mode)
        {
            _selectedMode = mode;
            if (_modeTitleLabel != null)
            {
                _modeTitleLabel.text = ModeDisplayName(mode);
            }

            SetHighlight(_soloHighlight, mode == MatchMode.Solo);
            SetHighlight(_duoHighlight, mode == MatchMode.Duo);
            SetHighlight(_squadHighlight, mode == MatchMode.Squad);
        }

        private static string ModeDisplayName(MatchMode mode)
        {
            switch (mode)
            {
                case MatchMode.Solo:
                    return "Battle Royale - Solo";
                case MatchMode.Duo:
                    return "Battle Royale - Duo";
                default:
                    return "Battle Royale - Squad";
            }
        }

        private static void SetHighlight(Image highlight, bool active)
        {
            if (highlight == null)
            {
                return;
            }

            highlight.color = active
                ? new Color(0.18f, 0.82f, 0.98f, 0.28f)
                : new Color(0.18f, 0.82f, 0.98f, 0f);
        }

        private void StartMatch()
        {
            var target = Application.CanStreamedLevelBeLoaded(GameBootstrap.SceneNames.SiranIslandGreybox)
                ? GameBootstrap.SceneNames.SiranIslandGreybox
                : GameBootstrap.SceneNames.SiranIsland;
            LoadScene(target);
        }

        private void ShowInfoModal(string title, string body)
        {
            if (_modalRoot == null)
            {
                return;
            }

            _modalTitle.text = title;
            _modalBody.text = body;
            _settingsActionsGroup.SetActive(false);
            _modalRoot.SetActive(true);
        }

        private void ShowSettingsModal()
        {
            if (_modalRoot == null)
            {
                return;
            }

            _modalTitle.text = "SYSTEM SETTINGS";
            _modalBody.text = string.Format(
                "Active Graphics Profile: {0}   (Auto-Detected: {1})\nDevice Target: Low-End Android / Unisoc T606 Ready",
                QualityProfiles.Active,
                QualityProfiles.DetectFromDevice());
            if (_qualityButtonLabel != null)
            {
                _qualityButtonLabel.text = QualityButtonLabel();
            }

            _settingsActionsGroup.SetActive(true);
            _modalRoot.SetActive(true);
        }

        private void CloseModal()
        {
            if (_modalRoot != null)
            {
                _modalRoot.SetActive(false);
            }
        }

        private static string QualityButtonLabel() =>
            QualityProfiles.Active == QualityProfile.High
                ? "GRAPHICS QUALITY: HIGH (TAP TO SWITCH)"
                : "GRAPHICS QUALITY: LOW (TAP TO SWITCH)";

        private void ToggleQuality()
        {
            var next = QualityProfiles.Active == QualityProfile.High ? QualityProfile.Low : QualityProfile.High;
            QualityProfiles.SetOverride(QualityProfiles.QualityIndex(next));
            QualityProfiles.Apply(next);
            GameLog.Info("menu", "quality switched to " + next);
            ShowSettingsModal();
        }

        private void LoadScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                GameLog.Error("menu", "scene '" + sceneName +
                    "' is not in EditorBuildSettings — run Battle Horizons > Project Setup > Configure");
                return;
            }

            LoadingScreenView.LoadSceneAsyncWithScreen(this, sceneName);
        }

        private void CreateModeButton(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            MatchMode mode,
            out Image highlightImage)
        {
            var button = CreateHotspotButton(
                parent,
                name,
                anchorMin,
                anchorMax,
                () => SelectMode(mode),
                new Color(0.25f, 0.85f, 1f, 0.12f),
                new Color(0.25f, 0.85f, 1f, 0.25f));

            var indicator = new GameObject("ActiveHighlight", typeof(RectTransform), typeof(Image));
            indicator.transform.SetParent(button.transform, false);
            var rect = (RectTransform)indicator.transform;
            rect.anchorMin = new Vector2(0.08f, 0.04f);
            rect.anchorMax = new Vector2(0.92f, 0.96f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            highlightImage = indicator.GetComponent<Image>();
            highlightImage.color = new Color(0.18f, 0.82f, 0.98f, 0f);
            highlightImage.raycastTarget = false;
        }

        private static Button CreateHotspotButton(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            UnityEngine.Events.UnityAction onClick,
            Color? highlightedTint = null,
            Color? pressedTint = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = highlightedTint ?? new Color(0.75f, 0.90f, 1f, 0.12f);
            colors.pressedColor = pressedTint ?? new Color(1f, 0.85f, 0.35f, 0.24f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            return button;
        }

        private static Text CreateOverlayLabel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string text,
            TextAnchor alignment,
            int maxFontSize,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = maxFontSize;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private void BuildModalOverlay(Transform canvasTransform)
        {
            _modalRoot = new GameObject("LobbyModal", typeof(RectTransform), typeof(Image), typeof(Button));
            _modalRoot.transform.SetParent(canvasTransform, false);
            StretchFull((RectTransform)_modalRoot.transform);
            var backdrop = _modalRoot.GetComponent<Image>();
            backdrop.color = new Color(0.02f, 0.04f, 0.07f, 0.76f);
            backdrop.raycastTarget = true;
            _modalRoot.GetComponent<Button>().onClick.AddListener(CloseModal);

            var card = new GameObject("ModalCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(_modalRoot.transform, false);
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(960f, 540f);
            var cardImage = card.GetComponent<Image>();
            cardImage.color = new Color(0.07f, 0.11f, 0.16f, 0.98f);
            cardImage.raycastTarget = true;

            GameLogo.CreateLogoImage(
                card.transform,
                "ModalLogo",
                new Vector2(0.5f, 1f),
                new Vector2(92f, 92f),
                new Vector2(0f, -64f));

            _modalTitle = CreateCardText(card.transform, "ModalTitle", new Vector2(0.5f, 1f),
                new Vector2(860f, 58f), new Vector2(0f, -138f), 36, FontStyle.Bold, new Color(1f, 0.85f, 0.35f, 1f));
            _modalBody = CreateCardText(card.transform, "ModalBody", new Vector2(0.5f, 0.5f),
                new Vector2(840f, 180f), new Vector2(0f, 12f), 26, FontStyle.Normal, new Color(0.88f, 0.92f, 0.96f, 1f));

            _settingsActionsGroup = new GameObject("SettingsActions", typeof(RectTransform));
            _settingsActionsGroup.transform.SetParent(card.transform, false);
            StretchFull((RectTransform)_settingsActionsGroup.transform);

            CreateActionButton(
                _settingsActionsGroup.transform,
                "ToggleQuality",
                QualityButtonLabel(),
                new Vector2(0.5f, 0f),
                new Vector2(620f, 64f),
                new Vector2(0f, 168f),
                new Color(0.16f, 0.38f, 0.48f, 0.96f),
                ToggleQuality,
                out _qualityButtonLabel);

            CreateActionButton(
                _settingsActionsGroup.transform,
                "PlayerTest",
                "OPEN PLAYER CONTROLLER TEST SCENE",
                new Vector2(0.5f, 0f),
                new Vector2(620f, 58f),
                new Vector2(0f, 98f),
                new Color(0.18f, 0.28f, 0.38f, 0.96f),
                () => LoadScene(GameBootstrap.SceneNames.PlayerTest),
                out _);

            CreateActionButton(
                card.transform,
                "CloseModalButton",
                "CLOSE",
                new Vector2(0.5f, 0f),
                new Vector2(280f, 56f),
                new Vector2(0f, 34f),
                new Color(0.88f, 0.66f, 0.16f, 0.98f),
                CloseModal,
                out _);

            _modalRoot.SetActive(false);
        }

        private static Text CreateCardText(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 size,
            Vector2 anchoredPosition,
            int fontSize,
            FontStyle style,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateActionButton(
            Transform parent,
            string name,
            string labelText,
            Vector2 anchor,
            Vector2 size,
            Vector2 anchoredPosition,
            Color bgColor,
            UnityEngine.Events.UnityAction onClick,
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
            image.color = bgColor;
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(go.transform, false);
            StretchFull((RectTransform)labelObj.transform);
            labelComponent = labelObj.GetComponent<Text>();
            labelComponent.text = labelText;
            labelComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelComponent.fontSize = 24;
            labelComponent.fontStyle = FontStyle.Bold;
            labelComponent.alignment = TextAnchor.MiddleCenter;
            labelComponent.color = Color.white;
            labelComponent.raycastTarget = false;
            return button;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite LoadHomeSprite()
        {
            var sprite = Resources.Load<Sprite>(HomeBackgroundResourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            if (_cachedHomeSprite != null)
            {
                return _cachedHomeSprite;
            }

            var texture = Resources.Load<Texture2D>(HomeBackgroundResourcePath);
            if (texture == null)
            {
                return null;
            }

            _cachedHomeSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _cachedHomeSprite.name = "BattleHorizonsHomeBg_RuntimeSprite";
            return _cachedHomeSprite;
        }
    }
}
