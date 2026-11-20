using BattleHorizons.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BattleHorizons.UI
{
    /// <summary>
    /// Interactive Battle Horizons: Last Survive Home Screen (Lobby).
    ///
    /// The lobby is 100% code-built: <see cref="LobbyArtFactory"/> paints the
    /// sunset vista, panels, chips and every glyph at runtime, and this
    /// controller lays them out on a 1920x1080 reference canvas — logo block,
    /// profile/coin/gem chips, settings + notification buttons, the left nav
    /// (Store / Luck Royale / Battle Pass / Missions / Events), the loadout
    /// bar (Vault / Weapon / Character), the Solo/Duo/Squad mode selector and
    /// the golden START button that deploys to Siran Island through
    /// <see cref="LoadingScreenView"/>.
    ///
    /// No baked UI artwork is used any more: the old 16:9 JPEG with the menu
    /// burnt into it duplicated and cropped on 20:9 phones, and invisible
    /// hotspot rects over art are not real UI. Everything here is a real
    /// widget (Image/Button/Text) that scales with the canvas.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public enum MatchMode
        {
            Solo,
            Duo,
            Squad,
        }

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
            GameLog.Info("menu", "home screen lobby ready (procedural code-built UI)");
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

            // Full-bleed dusk gradient: on ultra-wide phones the sides show a
            // calm sky instead of duplicated artwork.
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvasObject.transform, false);
            StretchFull((RectTransform)background.transform);
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.sprite = LobbyArtFactory.SkyGradient();
            backgroundImage.color = Color.white;
            backgroundImage.raycastTarget = false;

            // 16:9 stage: the painted vista + every widget live inside it so
            // the composition never crops on any aspect ratio.
            var frame = new GameObject("HomeArtworkFrame", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            frame.transform.SetParent(canvasObject.transform, false);
            StretchFull((RectTransform)frame.transform);
            var frameImage = frame.GetComponent<Image>();
            frameImage.sprite = LobbyArtFactory.Landscape();
            frameImage.color = Color.white;
            frameImage.raycastTarget = false;
            var frameFitter = frame.GetComponent<AspectRatioFitter>();
            frameFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            frameFitter.aspectRatio = 16f / 9f;

            // The real operator: configured production character rendered live
            // into a transparent RT, composited where the baked soldier stood.
            LobbyCharacterShowcase.Build(frame.transform);

            BuildLogo(frame.transform);
            BuildTopBar(frame.transform);
            BuildLeftNav(frame.transform);
            BuildLoadoutBar(frame.transform);
            BuildModeSelector(frame.transform);
            BuildStartButton(frame.transform);

            CreateOverlayLabel(
                frame.transform,
                "FooterStatus",
                new Vector2(0.030f, 0.010f),
                new Vector2(0.520f, 0.040f),
                "MAP: FORT KARGHA   •   SEASON 05   •   PATCH 2.4.1",
                TextAnchor.MiddleLeft,
                18,
                new Color(0.90f, 0.92f, 0.95f, 0.75f));

            BuildModalOverlay(canvasObject.transform);
            SelectMode(MatchMode.Squad);
        }

        // --- header -----------------------------------------------------------

        private static void BuildLogo(Transform parent)
        {
            var mark = new GameObject("LogoMark", typeof(RectTransform), typeof(Image));
            mark.transform.SetParent(parent, false);
            Anchor((RectTransform)mark.transform, new Vector2(0.020f, 0.885f), new Vector2(0.072f, 0.978f));
            var markImage = mark.GetComponent<Image>();
            markImage.sprite = LobbyArtFactory.Icon(LobbyArtFactory.LobbyIcon.Compass);
            markImage.color = new Color(1f, 0.85f, 0.35f, 0.95f);
            markImage.raycastTarget = false;

            CreateOverlayLabel(parent, "LogoTitle", new Vector2(0.076f, 0.918f), new Vector2(0.310f, 0.978f),
                "BATTLE HORIZONS:", TextAnchor.MiddleLeft, 42, Color.white);
            CreateOverlayLabel(parent, "LogoSubtitle", new Vector2(0.078f, 0.882f), new Vector2(0.310f, 0.920f),
                "L A S T   S U R V I V E", TextAnchor.MiddleLeft, 24, new Color(1f, 0.85f, 0.35f, 1f));
        }

        private void BuildTopBar(Transform parent)
        {
            // Profile chip: avatar + callsign + online dot.
            CreatePanelButton(
                parent,
                "ProfileButton",
                new Vector2(0.505f, 0.895f),
                new Vector2(0.670f, 0.975f),
                new Color(0.05f, 0.09f, 0.14f, 0.82f),
                () => ShowInfoModal("OPERATOR PROFILE",
                    "Callsign: " + LobbyProfile.CallSign +
                    "\nSeason 05 Rank: Vanguard IV\nPreferred Mode: " + ModeDisplayName(_selectedMode)),
                out var profileContent);
            AddIcon(profileContent, LobbyArtFactory.LobbyIcon.Avatar, new Vector2(0.04f, 0.10f), new Vector2(0.30f, 0.90f),
                new Color(0.85f, 0.90f, 0.95f, 1f));
            CreateOverlayLabel(profileContent, "CallSign", new Vector2(0.34f, 0.18f), new Vector2(0.92f, 0.82f),
                LobbyProfile.CallSign, TextAnchor.MiddleLeft, 26, new Color(0.55f, 0.95f, 1f, 1f));
            var onlineDot = new GameObject("OnlineDot", typeof(RectTransform), typeof(Image));
            onlineDot.transform.SetParent(profileContent, false);
            Anchor((RectTransform)onlineDot.transform, new Vector2(0.245f, 0.10f), new Vector2(0.30f, 0.28f));
            onlineDot.GetComponent<Image>().sprite = LobbyArtFactory.Icon(LobbyArtFactory.LobbyIcon.Coin);
            onlineDot.GetComponent<Image>().color = new Color(0.25f, 0.95f, 0.45f, 1f);
            onlineDot.GetComponent<Image>().raycastTarget = false;

            // Currency chips.
            CreatePanelButton(parent, "CoinChip", new Vector2(0.680f, 0.895f), new Vector2(0.780f, 0.975f),
                new Color(0.05f, 0.09f, 0.14f, 0.82f),
                () => ShowInfoModal("COINS", "Balance: " + LobbyProfile.FormatCoins +
                    " coins.\nEarn more by surviving matches and completing daily missions."),
                out var coinContent);
            AddIcon(coinContent, LobbyArtFactory.LobbyIcon.Coin, new Vector2(0.06f, 0.14f), new Vector2(0.34f, 0.86f),
                new Color(1f, 0.80f, 0.20f, 1f));
            CreateOverlayLabel(coinContent, "CoinCount", new Vector2(0.36f, 0.14f), new Vector2(0.96f, 0.86f),
                LobbyProfile.FormatCoins, TextAnchor.MiddleLeft, 26, new Color(1f, 0.88f, 0.45f, 1f));

            CreatePanelButton(parent, "GemChip", new Vector2(0.790f, 0.895f), new Vector2(0.872f, 0.975f),
                new Color(0.05f, 0.09f, 0.14f, 0.82f),
                () => ShowInfoModal("GEMS", "Balance: " + LobbyProfile.FormatGems +
                    " gems.\nGems are the premium currency (Luck Royale spins)."),
                out var gemContent);
            AddIcon(gemContent, LobbyArtFactory.LobbyIcon.Gem, new Vector2(0.06f, 0.14f), new Vector2(0.34f, 0.86f),
                new Color(0.80f, 0.45f, 1f, 1f));
            CreateOverlayLabel(gemContent, "GemCount", new Vector2(0.36f, 0.14f), new Vector2(0.96f, 0.86f),
                LobbyProfile.FormatGems, TextAnchor.MiddleLeft, 26, new Color(0.90f, 0.65f, 1f, 1f));

            // Settings + notifications.
            CreatePanelButton(parent, "SettingsButton", new Vector2(0.882f, 0.895f), new Vector2(0.933f, 0.975f),
                new Color(0.05f, 0.09f, 0.14f, 0.82f), ShowSettingsModal, out var settingsContent);
            AddIcon(settingsContent, LobbyArtFactory.LobbyIcon.Gear, new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.84f),
                new Color(0.88f, 0.92f, 0.96f, 1f));

            CreatePanelButton(parent, "NotificationsButton",
                new Vector2(0.941f, 0.895f), new Vector2(0.992f, 0.975f),
                new Color(0.05f, 0.09f, 0.14f, 0.82f),
                () => ShowInfoModal("NOTIFICATIONS",
                    "Welcome to Battle Horizons: Last Survive (Patch 2.4.1).\nSiran Island 2000x2000m terrain & mobile touch controls are ready."),
                out var bellContent);
            AddIcon(bellContent, LobbyArtFactory.LobbyIcon.Bell, new Vector2(0.16f, 0.12f), new Vector2(0.84f, 0.80f),
                new Color(0.88f, 0.92f, 0.96f, 1f));
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(bellContent, false);
            Anchor((RectTransform)badge.transform, new Vector2(0.66f, 0.66f), new Vector2(0.86f, 0.86f));
            badge.GetComponent<Image>().sprite = LobbyArtFactory.Icon(LobbyArtFactory.LobbyIcon.Coin);
            badge.GetComponent<Image>().color = new Color(0.95f, 0.20f, 0.20f, 1f);
            badge.GetComponent<Image>().raycastTarget = false;
        }

        // --- navigation --------------------------------------------------------

        private void BuildLeftNav(Transform parent)
        {
            NavButton(parent, "StoreButton", LobbyArtFactory.LobbyIcon.Cart, "Store",
                new Vector2(0.665f, 0.748f),
                () => ShowInfoModal("STORE",
                    "Featured Season 05 Tactical Bundles, Desert Camo Skins, and Supply Crates.\n\nOffline Prototype Build — Store purchases are disabled."));
            NavButton(parent, "LuckRoyaleButton", LobbyArtFactory.LobbyIcon.Luck, "Luck Royale",
                new Vector2(0.567f, 0.650f),
                () => ShowInfoModal("LUCK ROYALE",
                    "Fort Kargha Gold Spin & Tactical Gear Draw.\n\nAvailable Coins: " + LobbyProfile.FormatCoins +
                    "   |   Gems: " + LobbyProfile.FormatGems));
            NavButton(parent, "BattlePassButton", LobbyArtFactory.LobbyIcon.Trophy, "Battle Pass",
                new Vector2(0.469f, 0.552f),
                () => ShowInfoModal("BATTLE PASS — SEASON 05",
                    "Tier 18 Unlocked  •  Fort Kargha Vanguard\n\nComplete survival matches on Siran Island to unlock Season 05 rewards."));
            NavButton(parent, "MissionsButton", LobbyArtFactory.LobbyIcon.Target, "Missions",
                new Vector2(0.371f, 0.454f),
                () => ShowInfoModal("DAILY MISSIONS",
                    "• Explore Siran Island Greybox (0/1)\n• Sprint 500m across rugged terrain (0/500)\n• Survive in the Battlezone (Reward: 500 Coins)"));
            NavButton(parent, "EventsButton", LobbyArtFactory.LobbyIcon.Calendar, "Events",
                new Vector2(0.273f, 0.356f),
                () => ShowInfoModal("EVENTS — PATCH 2.4.1",
                    "Fort Kargha & Siran Island Reconnaissance Event Active!\n\nTap START in the bottom-right corner to deploy."));
        }

        private void NavButton(Transform parent, string name, LobbyArtFactory.LobbyIcon icon, string label,
            Vector2 yBand, UnityEngine.Events.UnityAction onClick)
        {
            CreatePanelButton(parent, name, new Vector2(0.028f, yBand.x), new Vector2(0.190f, yBand.y),
                new Color(0.04f, 0.08f, 0.13f, 0.80f), onClick, out var content);
            AddIcon(content, icon, new Vector2(0.05f, 0.16f), new Vector2(0.30f, 0.84f), new Color(1f, 0.85f, 0.35f, 0.95f));
            CreateOverlayLabel(content, "Label", new Vector2(0.34f, 0.14f), new Vector2(0.96f, 0.86f),
                label, TextAnchor.MiddleLeft, 28, Color.white);
        }

        private void BuildLoadoutBar(Transform parent)
        {
            NavWideButton(parent, "VaultButton", LobbyArtFactory.LobbyIcon.Vault, "Vault",
                new Vector2(0.030f, 0.048f), new Vector2(0.148f, 0.120f),
                () => ShowInfoModal("VAULT",
                    "Equipped Outfit: Special Operations Olive Tactical Rig + NVG Helmet\nParachute: Battle Horizons Gold Emblem"));
            NavWideButton(parent, "WeaponButton", LobbyArtFactory.LobbyIcon.Weapon, "Weapon",
                new Vector2(0.156f, 0.048f), new Vector2(0.274f, 0.120f),
                () => ShowInfoModal("WEAPON ARMORY",
                    "Primary Slot: Assault Rifle (Phase 2 Combat System)\nMelee: Tactical Combat Blades"));

            // Character Bay = real-time character & animation preview (the
            // modal reports "not configured" until a character is imported
            // and saved in the Character & Animation Manager).
            NavWideButton(parent, "CharacterButton", LobbyArtFactory.LobbyIcon.Character, "Character",
                new Vector2(0.282f, 0.048f), new Vector2(0.400f, 0.120f),
                ShowCharacterBay);
        }

        private void NavWideButton(Transform parent, string name, LobbyArtFactory.LobbyIcon icon, string label,
            Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
        {
            CreatePanelButton(parent, name, anchorMin, anchorMax,
                new Color(0.04f, 0.08f, 0.13f, 0.80f), onClick, out var content);
            AddIcon(content, icon, new Vector2(0.06f, 0.18f), new Vector2(0.32f, 0.82f), new Color(1f, 0.85f, 0.35f, 0.95f));
            CreateOverlayLabel(content, "Label", new Vector2(0.36f, 0.14f), new Vector2(0.96f, 0.86f),
                label, TextAnchor.MiddleLeft, 26, Color.white);
        }

        // --- mode + start -------------------------------------------------------

        private void BuildModeSelector(Transform parent)
        {
            var panel = new GameObject("ModePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Anchor((RectTransform)panel.transform, new Vector2(0.742f, 0.150f), new Vector2(0.988f, 0.345f));
            var panelImage = panel.GetComponent<Image>();
            panelImage.sprite = LobbyArtFactory.Panel();
            panelImage.color = new Color(0.03f, 0.06f, 0.10f, 0.86f);
            panelImage.raycastTarget = false;

            CreateOverlayLabel(panel.transform, "ModeCaption", new Vector2(0.05f, 0.74f), new Vector2(0.32f, 0.95f),
                "MODE", TextAnchor.MiddleLeft, 20, new Color(0.75f, 0.82f, 0.90f, 0.9f));
            var caret = new GameObject("ModeCaret", typeof(RectTransform), typeof(Image));
            caret.transform.SetParent(panel.transform, false);
            Anchor((RectTransform)caret.transform, new Vector2(0.32f, 0.78f), new Vector2(0.40f, 0.92f));
            var caretImage = caret.GetComponent<Image>();
            caretImage.sprite = LobbyArtFactory.Icon(LobbyArtFactory.LobbyIcon.CaretDown);
            caretImage.color = new Color(0.75f, 0.82f, 0.90f, 0.9f);
            caretImage.raycastTarget = false;

            _modeTitleLabel = CreateOverlayLabel(panel.transform, "ModeTitle",
                new Vector2(0.05f, 0.46f), new Vector2(0.95f, 0.74f),
                "Battle Royale - Squad", TextAnchor.MiddleLeft, 26, Color.white);

            // The three segment buttons stay DIRECT children of the stage frame
            // (PlayMode tests resolve "HomeArtworkFrame/ModeSolo" etc. by path);
            // the frame-space rects below line them up inside the ModePanel.
            CreateModeButton(parent, "ModeSolo", new Vector2(0.7506f, 0.1617f), new Vector2(0.8269f, 0.2358f),
                MatchMode.Solo, LobbyArtFactory.LobbyIcon.PersonSolo, "Solo", out _soloHighlight);
            CreateModeButton(parent, "ModeDuo", new Vector2(0.8294f, 0.1617f), new Vector2(0.9058f, 0.2358f),
                MatchMode.Duo, LobbyArtFactory.LobbyIcon.PersonDuo, "Duo", out _duoHighlight);
            CreateModeButton(parent, "ModeSquad", new Vector2(0.9080f, 0.1617f), new Vector2(0.9843f, 0.2358f),
                MatchMode.Squad, LobbyArtFactory.LobbyIcon.PersonSquad, "Squad", out _squadHighlight);
        }

        private void BuildStartButton(Transform parent)
        {
            var go = new GameObject("EnterSiranIsland", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, new Vector2(0.742f, 0.028f), new Vector2(0.988f, 0.140f));
            var image = go.GetComponent<Image>();
            image.sprite = LobbyArtFactory.GoldButton();
            image.color = Color.white;
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 0.92f, 1f);
            colors.pressedColor = new Color(0.85f, 0.68f, 0.25f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(StartMatch);

            CreateOverlayLabel(go.transform, "Label", new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.88f),
                "START", TextAnchor.MiddleCenter, 44, new Color(0.16f, 0.10f, 0.02f, 1f));
        }

        public void SelectMode(MatchMode mode)
        {
            _selectedMode = mode;
            if (_modeTitleLabel != null)
            {
                _modeTitleLabel.text = "Battle Royale - " + mode;
            }

            SetHighlight(_soloHighlight, mode == MatchMode.Solo);
            SetHighlight(_duoHighlight, mode == MatchMode.Duo);
            SetHighlight(_squadHighlight, mode == MatchMode.Squad);
        }

        private static string ModeDisplayName(MatchMode mode) => "Battle Royale - " + mode;

        private static void SetHighlight(Image highlight, bool active)
        {
            if (highlight == null)
            {
                return;
            }

            highlight.color = active
                ? new Color(0.18f, 0.82f, 0.98f, 0.30f)
                : new Color(0.18f, 0.82f, 0.98f, 0f);
        }

        private void StartMatch()
        {
            var target = Application.CanStreamedLevelBeLoaded(GameBootstrap.SceneNames.SiranIslandGreybox)
                ? GameBootstrap.SceneNames.SiranIslandGreybox
                : GameBootstrap.SceneNames.SiranIsland;
            LoadScene(target);
        }

        private void ShowCharacterBay()
        {
            if (_modalRoot == null)
            {
                return;
            }

            // Real-time 3D preview inside the existing game UI: the Character
            // Bay is built next to the existing LobbyModal on the same canvas.
            var canvas = _modalRoot.transform.parent;
            if (canvas == null)
            {
                return;
            }

            Characters.CharacterBayMenu.Build(canvas, null);
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

        // --- widget helpers ------------------------------------------------------

        private void CreateModeButton(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            MatchMode mode,
            LobbyArtFactory.LobbyIcon icon,
            string label,
            out Image highlightImage)
        {
            var button = CreatePanelButton(parent, name, anchorMin, anchorMax,
                new Color(0.10f, 0.16f, 0.24f, 0.90f), () => SelectMode(mode), out var content);

            AddIcon(content, icon, new Vector2(0.30f, 0.42f), new Vector2(0.70f, 0.94f), new Color(0.88f, 0.92f, 0.96f, 1f));
            CreateOverlayLabel(content, "Label", new Vector2(0.1f, 0.06f), new Vector2(0.9f, 0.42f),
                label, TextAnchor.MiddleCenter, 20, new Color(0.85f, 0.90f, 0.96f, 1f));

            var indicator = new GameObject("ActiveHighlight", typeof(RectTransform), typeof(Image));
            indicator.transform.SetParent(button.transform, false);
            StretchFull((RectTransform)indicator.transform);
            highlightImage = indicator.GetComponent<Image>();
            highlightImage.color = new Color(0.18f, 0.82f, 0.98f, 0f);
            highlightImage.raycastTarget = false;
        }

        private static Button CreatePanelButton(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color backgroundColor,
            UnityEngine.Events.UnityAction onClick,
            out Transform contentRoot)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = LobbyArtFactory.Panel();
            image.color = backgroundColor;
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.72f, 0.80f, 0.90f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(onClick);

            contentRoot = go.transform;
            return button;
        }

        private static void AddIcon(Transform contentRoot, LobbyArtFactory.LobbyIcon icon,
            Vector2 anchorMin, Vector2 anchorMax, Color tint)
        {
            var go = new GameObject("Icon_" + icon, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(contentRoot, false);
            Anchor((RectTransform)go.transform, anchorMin, anchorMax);
            var image = go.GetComponent<Image>();
            image.sprite = LobbyArtFactory.Icon(icon);
            image.color = tint;
            image.raycastTarget = false;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
            Anchor((RectTransform)go.transform, anchorMin, anchorMax);

            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
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
            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(960f, 540f);
            cardRect.anchoredPosition = new Vector2(0f, -300f);
            var cardImage = card.GetComponent<Image>();
            cardImage.sprite = LobbyArtFactory.Panel();
            cardImage.color = new Color(0.06f, 0.10f, 0.15f, 0.98f);
            cardImage.raycastTarget = true;

            var modalMark = new GameObject("ModalLogo", typeof(RectTransform), typeof(Image));
            modalMark.transform.SetParent(card.transform, false);
            var markRect = (RectTransform)modalMark.transform;
            markRect.anchorMin = new Vector2(0.5f, 1f);
            markRect.anchorMax = new Vector2(0.5f, 1f);
            markRect.pivot = new Vector2(0.5f, 0.5f);
            markRect.sizeDelta = new Vector2(92f, 92f);
            markRect.anchoredPosition = new Vector2(0f, -64f);
            var markImage = modalMark.GetComponent<Image>();
            markImage.sprite = LobbyArtFactory.Icon(LobbyArtFactory.LobbyIcon.Compass);
            markImage.color = new Color(1f, 0.85f, 0.35f, 0.95f);
            markImage.raycastTarget = false;

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
            image.sprite = LobbyArtFactory.Panel();
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
    }
}
