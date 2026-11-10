using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleHorizons.UI
{
    /// <summary>
    /// Full-screen drag surface behind the controls: a drag anywhere that is not
    /// on a joystick or button becomes camera look.
    ///
    /// On phones, a full-screen look surface is easier to use than a small
    /// dedicated look stick; controls above it continue to receive their own input.
    /// </summary>
    public sealed class TouchLookArea : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        [SerializeField] private float sensitivity = 0.16f;
        [SerializeField] private Player.PlayerInputRouter router;
        [SerializeField] private CameraRig.ThirdPersonCameraController cameraRig;

        /// <summary>
        /// Wires the surface up. Caching the rig here matters: <c>OnDrag</c> fires
        /// every frame the finger moves, and a scene search per drag event is a
        /// measurable cost on a low-end phone.
        /// </summary>
        public void Configure(Player.PlayerInputRouter inputRouter,
            CameraRig.ThirdPersonCameraController targetCamera, float sensitivityValue = 0.16f)
        {
            router = inputRouter;
            cameraRig = targetCamera;
            sensitivity = sensitivityValue;
        }

        private void Start()
        {
            if (cameraRig == null)
            {
                cameraRig = Object.FindFirstObjectByType<CameraRig.ThirdPersonCameraController>();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // A tap on empty space does not move the camera; only drags do.
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (cameraRig == null)
            {
                return;
            }

            // eventData.delta is in screen pixels; scale is per-pixel degrees.
            var delta = eventData.delta * sensitivity;
            if (Application.isMobilePlatform)
            {
                // Touch deltas are reported in physical pixels, so a 1440p phone
                // would look twice as fast as a 720p one without this.
                delta *= 1080f / Mathf.Max(Screen.height, 1f);
            }

            cameraRig.AddLook(new Vector2(delta.x, delta.y));
        }
    }
}
