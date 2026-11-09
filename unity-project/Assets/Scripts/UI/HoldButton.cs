using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleHorizons.UI
{
    /// <summary>
    /// A touch button that reports *held* rather than *clicked*.
    ///
    /// Crouch and sprint are held states, so a uGUI <see cref="UnityEngine.UI.Button"/>
    /// (which fires once per tap) is the wrong control for them: holding the button
    /// must keep the state, and lifting the finger must clear it even if the finger
    /// slides off the button first. That is why this class handles pointer down / up /
    /// exit explicitly instead of using onClick.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Action<bool> _onHeldChanged;
        private bool _held;

        public bool IsHeld => _held;

        public void Configure(Action<bool> onHeldChanged)
        {
            _onHeldChanged = onHeldChanged;
        }

        public void OnPointerDown(PointerEventData eventData) => SetHeld(true);

        public void OnPointerUp(PointerEventData eventData) => SetHeld(false);

        public void OnPointerExit(PointerEventData eventData)
        {
            // A finger that slides off the button must not leave the state stuck on.
            if (eventData.pointerPress != gameObject)
            {
                return;
            }

            SetHeld(false);
        }

        private void OnDisable() => SetHeld(false);

        private void SetHeld(bool held)
        {
            if (_held == held)
            {
                return;
            }

            _held = held;
            _onHeldChanged?.Invoke(held);
        }
    }
}
