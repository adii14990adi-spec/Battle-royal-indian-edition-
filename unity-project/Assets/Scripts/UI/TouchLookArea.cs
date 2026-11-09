using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleHorizons.UI
{
    /// <summary>
    /// Full-screen drag surface behind the controls: a drag anywhere that is not
    /// on a joystick or button becomes camera look.
    ///
    /// Why: on a phone the look joystick is small, and "swipe anywhere" is what
    /// players actually do. The Godot build never had this (it only had a debug
    /// spectator), so it is new behaviour, not a port.
    /// </summary>
    public sealed class TouchLookArea : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        [SerializeField] private float sensitivity = 0.16f;
        [SerializeField] private Player.PlayerInputRouter router;

        public void Configure(Player.PlayerInputRouter inputRouter, float sensitivityValue = 0.16f)
        {
            router = inputRouter;
            sensitivity = sensitivityValue;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // A tap on empty space does not move the camera; only drags do.
        }

        public void OnDrag(PointerEventData eventData)
        {
            var cameraRig = Object.FindFirstObjectByType<CameraRig.ThirdPersonCameraController>();
            if (cameraRig == null)
            {
                return;
            }

            var delta = eventData.delta * sensitivity;
            cameraRig.AddLook(new Vector2(delta.x, delta.y));
        }
    }
}
