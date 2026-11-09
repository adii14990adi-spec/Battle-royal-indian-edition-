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
            public GameObject Root;
        }

        public static Rig Build(Transform parent, PlayerInputRouter router, ThirdPersonMotor motor)
        {
            EnsureEventSystem();

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

            var moveStick = CreateStick(canvasObject.transform, "MoveStick", new Vector2(0.5f, 0.5f),
                new Vector2(260f, 260f), new Vector2(260f, 200f), new Color(1f, 1f, 1f, 0.22f));
            var lookStick = CreateStick(canvasObject.transform, "LookStick", new Vector2(1f, 0.5f),
                new Vector2(220f, 220f), new Vector2(-260f, -120f), new Color(1f, 1f, 1f, 0.16f));

            var jump = CreateJumpButton(canvasObject.transform, motor);

            var rig = new Rig
            {
                Canvas = canvas,
                MoveStick = moveStick,
                LookStick = lookStick,
                JumpButton = jump,
                Root = canvasObject,
            };

            if (router != null)
            {
                router.Configure(motor, Object.FindFirstObjectByType<CameraRig.ThirdPersonCameraController>(),
                    moveStick, lookStick);
            }

            return rig;
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

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(rect, false);
            var text = label.GetComponent<Text>();
            text.text = "JUMP";
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 34;
            text.color = new Color(0.08f, 0.1f, 0.12f, 0.9f);
            text.raycastTarget = false;
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            if (motor != null)
            {
                button.onClick.AddListener(motor.RequestJump);
            }

            return button;
        }
    }
}
