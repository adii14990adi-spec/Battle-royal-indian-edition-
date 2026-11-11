using System;
using BattleHorizons.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleHorizons.UI
{
    /// <summary>
    /// Builds the Android touch HUD from code: two joysticks (move left, look
    /// right), a jump button and a crouch/sprint affordance, plus a
    /// <see cref="TouchLookArea"/> over the right half of the screen so a drag
    /// anywhere looks around before the stick is found.
    ///
    /// Code instead of a prefab on purpose: the layout is four RectTransforms and
    /// two colours, so a prefab would be more asset churn than it saves, and this
    /// way the same builder produces the control rig for the player test scene and
    /// for any future gameplay scene.
    ///
    /// Uses the legacy uGUI input module hookup already in the scene (an
    /// <see cref="EventSystem"/> is created if missing).
    /// </summary>
    public static class MobileControlsBuilder
    {
        public struct Rig
        {
            public Canvas Canvas;
            public VirtualJoystick MoveStick;
            public VirtualJoystick LookStick;
            public Button JumpButton;
            public HoldButton CrouchButton;
            public HoldButton SprintButton;
            public Button CrouchUiButton;
            public Button SprintUiButton;
            public GameObject Root;
        }

        public static Rig Build(Transform parent, PlayerInputRouter router, ThirdPersonMotor motor,
            PlayerController playerController = null)
        {
            EnsureEventSystem();

            if (playerController == null && motor != null)
            {
                playerController = motor.GetComponent<PlayerController>();
            }
            if (playerController == null)
            {
                playerController = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            }

            var cameraRig = UnityEngine.Object.FindFirstObjectByType<CameraRig.ThirdPersonCameraController>();

            var existingCanvasTransform = parent != null ? parent.Find("MobileControls") : null;
            if (existingCanvasTransform != null)
            {
                if (Application.isPlaying)
                {
                    existingCanvasTransform.SetParent(null, false);
                    existingCanvasTransform.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(existingCanvasTransform.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(existingCanvasTransform.gameObject);
                }
            }

            var canvasObject = new GameObject("MobileControls");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            // The look surface is created first so it sits *behind* every other
            // control: a drag on a stick or a button hits that control, a drag on
            // empty screen falls through to the surface. uGUI hit-testing walks the
            // hierarchy in reverse sibling order, so earlier == behind.
            var lookArea = CreateLookArea(canvasObject.transform, router, cameraRig);

            GameLogo.CreateLogoImage(
                canvasObject.transform,
                "GameLogoBadge",
                new Vector2(1f, 1f),
                new Vector2(112f, 112f),
                new Vector2(-78f, -78f),
                0.92f);

            CreateLobbyReturnButton(canvasObject.transform);

            var moveStick = CreateStick(canvasObject.transform, "MoveStick", new Vector2(0f, 0f),
                new Vector2(300f, 300f), new Vector2(320f, 300f), new Color(1f, 1f, 1f, 0.25f));
            var lookStick = CreateStick(canvasObject.transform, "LookStick", new Vector2(1f, 0.5f),
                new Vector2(220f, 220f), new Vector2(-260f, -120f), new Color(1f, 1f, 1f, 0.16f));

            var jump = CreateJumpButton(canvasObject.transform, motor, playerController);
            var crouch = CreateHoldButton(canvasObject.transform, "CrouchButton", "CROUCH",
                new Vector2(1f, 0f), new Vector2(180f, 130f), new Vector2(-430f, 150f),
                new Color(0.55f, 0.75f, 1f, 0.45f),
                playerController == null && router != null ? (Action<bool>)router.SetCrouchHeld : null,
                playerController != null ? (UnityEngine.Events.UnityAction)playerController.OnCrouchButtonClicked : null,
                out var crouchUiButton);
            var sprint = CreateHoldButton(canvasObject.transform, "SprintButton", "SPRINT",
                new Vector2(1f, 0f), new Vector2(180f, 130f), new Vector2(-430f, 320f),
                new Color(0.6f, 1f, 0.7f, 0.45f),
                playerController == null && router != null ? (Action<bool>)router.SetSprintHeld : null,
                playerController != null ? (UnityEngine.Events.UnityAction)playerController.OnSprintButtonClicked : null,
                out var sprintUiButton);

            var rig = new Rig
            {
                Canvas = canvas,
                MoveStick = moveStick,
                LookStick = lookStick,
                JumpButton = jump,
                CrouchButton = crouch,
                SprintButton = sprint,
                CrouchUiButton = crouchUiButton,
                SprintUiButton = sprintUiButton,
                Root = canvasObject,
            };

            if (router != null)
            {
                router.Configure(motor, cameraRig, moveStick, lookStick);
            }

            if (playerController != null)
            {
                playerController.BindUiControls(moveStick, sprintUiButton, jump, crouchUiButton);
            }

            return rig;
        }

        private static TouchLookArea CreateLookArea(Transform parent, PlayerInputRouter router,
            CameraRig.ThirdPersonCameraController cameraRig)
        {
            var go = new GameObject("LookArea", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f); // invisible but still raycastable
            image.raycastTarget = true;

            var area = go.AddComponent<TouchLookArea>();
            area.Configure(router, cameraRig);
            return area;
        }

        private static HoldButton CreateHoldButton(Transform parent, string name, string label,
            Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color, Action<bool> onHeldChanged,
            UnityEngine.Events.UnityAction onClickAction, out Button uiButton)
        {
            var rect = CreatePanel(parent, name, anchor, size, new Vector2(0.5f, 0.5f), anchoredPosition, color);
            uiButton = rect.gameObject.AddComponent<Button>();
            uiButton.targetGraphic = rect.GetComponent<Image>();
            if (onClickAction != null)
            {
                uiButton.onClick.AddListener(onClickAction);
            }

            var hold = rect.gameObject.AddComponent<HoldButton>();
            hold.Configure(onHeldChanged);
            CreateLabel(rect, label, 30);
            return hold;
        }

        private static void CreateLabel(RectTransform parent, string text, int fontSize)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(parent, false);
            var component = label.GetComponent<Text>();
            component.text = text;
            component.alignment = TextAnchor.MiddleCenter;
            component.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            component.fontSize = fontSize;
            component.color = new Color(0.08f, 0.1f, 0.12f, 0.9f);
            component.raycastTarget = false;
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        public static EventSystem EnsureEventSystem()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
#if UNITY_2023_1_OR_NEWER
                if (existing.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
                {
                    var legacy = existing.GetComponent<StandaloneInputModule>();
                    if (legacy != null)
                    {
                        if (Application.isPlaying)
                        {
                            UnityEngine.Object.Destroy(legacy);
                        }
                        else
                        {
                            UnityEngine.Object.DestroyImmediate(legacy);
                        }
                    }
                    existing.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                }
#else
                if (existing.GetComponent<BaseInputModule>() == null)
                {
                    existing.gameObject.AddComponent<StandaloneInputModule>();
                }
#endif
                return existing;
            }

            var eventSystem = new GameObject("EventSystem");
            var component = eventSystem.AddComponent<EventSystem>();
#if UNITY_2023_1_OR_NEWER
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystem.AddComponent<StandaloneInputModule>();
#endif
            return component;
        }

        private static VirtualJoystick CreateStick(Transform parent, string name, Vector2 anchor,
            Vector2 size, Vector2 pivotOffset, Color tint)
        {
            var area = CreatePanel(parent, name, anchor, size, new Vector2(0.5f, 0.5f), pivotOffset, tint);
            // A pointer handler needs a graphic that raycasts; the panel has one.
            var stick = area.gameObject.AddComponent<VirtualJoystick>();

            var knob = CreatePanel(area, "Knob", new Vector2(0.5f, 0.5f), size * 0.45f,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Color(1f, 1f, 1f, 0.35f));
            var knobImage = knob.GetComponent<Image>();
            if (knobImage != null)
            {
                knobImage.raycastTarget = false;
            }

            stick.Configure(area, knob, parent.GetComponentInParent<Canvas>(), size.x * 0.5f);
            return stick;
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchor,
            Vector2 size, Vector2 pivot, Vector2 anchoredPosition, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return rect;
        }

        private static Button CreateJumpButton(Transform parent, ThirdPersonMotor motor,
            PlayerController playerController = null)
        {
            var rect = CreatePanel(parent, "JumpButton", new Vector2(1f, 0f), new Vector2(190f, 190f),
                new Vector2(0.5f, 0.5f), new Vector2(-220f, 190f), new Color(1f, 0.85f, 0.35f, 0.55f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            CreateLabel(rect, "JUMP", 34);

            if (playerController != null)
            {
                button.onClick.AddListener(playerController.OnJumpButtonClicked);
            }
            else if (motor != null)
            {
                button.onClick.AddListener(motor.RequestJump);
            }

            return button;
        }

        private static Button CreateLobbyReturnButton(Transform parent)
        {
            var rect = CreatePanel(
                parent,
                "LobbyButton",
                new Vector2(1f, 1f),
                new Vector2(150f, 56f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-225f, -78f),
                new Color(0.08f, 0.14f, 0.20f, 0.80f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(rect, false);
            var labelText = label.GetComponent<Text>();
            labelText.text = "LOBBY";
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 24;
            labelText.fontStyle = FontStyle.Bold;
            labelText.color = new Color(1f, 0.88f, 0.42f, 0.96f);
            labelText.raycastTarget = false;
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            button.onClick.AddListener(() =>
            {
                if (Application.CanStreamedLevelBeLoaded(Core.GameBootstrap.SceneNames.MainMenu))
                {
                    var host = parent.GetComponentInParent<MonoBehaviour>();
                    if (host != null)
                    {
                        LoadingScreenView.LoadSceneAsyncWithScreen(host, Core.GameBootstrap.SceneNames.MainMenu, 1.1f);
                    }
                    else
                    {
                        UnityEngine.SceneManagement.SceneManager.LoadScene(Core.GameBootstrap.SceneNames.MainMenu);
                    }
                }
            });

            return button;
        }
    }
}
