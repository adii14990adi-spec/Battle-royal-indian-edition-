using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleHorizons.Player
{
    /// <summary>
    /// A uGUI twin-stick joystick for Android touch input.
    ///
    /// It is a plain <see cref="IPointerDownHandler"/>/<see cref="IDragHandler"/>
    /// pair assigned to a <see cref="RectTransform"/> rather than a scene-bound
    /// prefab, so the whole control can be created from code by
    /// <see cref="MobileControlsBuilder"/> — one less asset to keep in sync, and
    /// the layout is testable in edit mode.
    ///
    /// Value is normalised to a unit disc (with a dead zone) and is
    /// <c>Vector2.zero</c> when nothing is touching it, which is exactly the
    /// contract <see cref="PlayerInputRouter"/> expects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
    [SerializeField] private float radiusPixels = 120f;
        [SerializeField] private float deadZone = 0.12f;
        [SerializeField] private RectTransform knob;

        private RectTransform _area;
        private Canvas _canvas;
        private Vector2 _value;

        /// <summary>Normalised direction, magnitude 0..1, dead zone applied.</summary>
        public Vector2 Value => _value;

        /// <summary>True while a pointer is holding the stick.</summary>
        public bool IsEngaged { get; private set; }

        public void Configure(RectTransform area, RectTransform knobRect, Canvas canvas, float radius, float dead = 0.12f)
        {
            _area = area;
            knob = knobRect;
            _canvas = canvas;
            radiusPixels = radius;
            deadZone = dead;
        }

        private void Start()
        {
            if (_area == null)
            {
                _area = transform as RectTransform;
            }

            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdateFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateFromPointer(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsEngaged = false;
            _value = Vector2.zero;
            if (knob != null)
            {
                knob.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>Set the value directly (tests, bots, keyboard emulation).</summary>
        public void SetValue(Vector2 value)
        {
            _value = value.sqrMagnitude < deadZone * deadZone ? Vector2.zero : Vector2.ClampMagnitude(value, 1f);
            IsEngaged = _value != Vector2.zero;
            if (knob != null)
            {
                knob.anchoredPosition = _value * radiusPixels;
            }
        }

        private void UpdateFromPointer(PointerEventData eventData)
        {
            if (_area == null)
            {
                return;
            }

            var cameraForCanvas = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, eventData.position,
                    cameraForCanvas, out var local))
            {
                return;
            }

            var normalised = local / Mathf.Max(radiusPixels, 1f);
            var magnitude = normalised.magnitude;
            if (magnitude < deadZone)
            {
                IsEngaged = false;
                _value = Vector2.zero;
                if (knob != null)
                {
                    knob.anchoredPosition = Vector2.zero;
                }

                return;
            }

            _value = Vector2.ClampMagnitude(normalised, 1f);
            IsEngaged = true;
            if (knob != null)
            {
                knob.anchoredPosition = _value * radiusPixels;
            }
        }
    }
}
