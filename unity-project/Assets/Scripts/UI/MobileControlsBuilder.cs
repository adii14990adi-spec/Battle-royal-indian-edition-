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
            public GameObject Root;
        }

        public static Rig Build(Transform parent, PlayerInputRouter router, ThirdPersonMotor motor)
        {
            EnsureEventSystem();

            var cameraRig = Object.FindFirstObjectByType<CameraRig.ThirdPersonCameraController>();

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

            var moveStick = CreateStick(canvasObject.transform, "MoveStick", new Vector2(0f, 0f),
                new Vector2(300f, 300f), new Vector2(320f, 300f), new Color(1f, 1f, 1f, 0.20f));
            var lookStick = CreateStick(canvasObject.transform, "LookStick", new Vector2(1f, 0.5f),
                new Vector2(220f, 220f), new Vector2(-260f, -120f), new Color(1f, 1f, 1f, 0.14f));

            var jump = CreateJumpButton(canvasObject.transform, motor);
            var crouch = CreateHoldButton(canvasObject.transform, "CrouchButton", "CROUCH",
                new Vector2(1f, 0f), new Vector2(170f, 130f), new Vector2(-420f, 150f),
                new Color(0.55f, 0.75f, 1f, 0.35f), router != null ? (Action<bool>)router.SetCrouchHeld : null);
            var sprint = CreateHoldButton(canvasObject.transform, "SprintButton", "SPRINT",
                new Vector2(1f, 0f), new Vector2(170f, 130f), new Vector2(-420f, 320f),
                new Color(0.6f, 1f, 0.7f, 0.30f), router != null ? (Action<bool>)router.SetSprintHeld : null);

            var rig = new Rig
            {
                Canvas = canvas,
                MoveStick = moveStick,
                LookStick = lookStick,
                JumpButton = jump,
                CrouchButton = crouch,
                SprintButton = sprint,
                Root = canvasObject,
            };

            if (router != null)
            {
                router.Configure(motor, cameraRig, moveStick, lookStick);
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
            Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color, Action<bool> onHeldChanged)
        {
            var rect = CreatePanel(parent, name, anchor, size, new Vector2(0.5f, 0.5f), anchoredPosition, color);
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

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
#if UNITY_2023_1_OR_NEWER
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystem.AddComponent<StandaloneInputModule>();
#endif
        }

        private static VirtualJoystick CreateStick(Transform parent, string name, Vector2 anchor,
            Vector2 size, Vector2 pivotOffset, Color tint)
        {
            var area = CreatePanel(parent, name, anchor, size, new Vector2(0.5f, 0.5f), pivotOffset, tint);
            // A pointer handler needs a graphic that raycasts; the panel has one.
            var stick = area.gameObject.AddComponent<VirtualJoystick>();

            var knob = CreatePanel(area, "Knob", new Vector2(0.5f, 0.5f), size * 0.45f,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Color(1f, 1f, 1f, 0.35f));

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

        private static Button CreateJumpButton(Transform parent, ThirdPersonMotor motor)
        {
            var rect = CreatePanel(parent, "JumpButton", new Vector2(1f, 0f), new Vector2(190f, 190f),
                new Vector2(0.5f, 0.5f), new Vector2(-220f, 190f), new Color(1f, 0.85f, 0.35f, 0.45f));
            var button = rect.gameObject.AddComponent<Button>();
            CreateLabel(rect, "JUMP", 34);

            if (motor != null)
            {
                button.onClick.AddListener(motor.RequestJump);
            }

            return button;
        }
    }
}
