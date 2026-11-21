using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// Real-time character preview used by the in-game Character Bay modal and
    /// the Home Screen showcase. Renders the selected character (real imported
    /// model — never a placeholder) into a RenderTexture.
    ///
    /// History: the preview used to play the generated Animator Controller and
    /// the imported Mixamo clip library. All animation clips were removed from
    /// the project on 2026-11-20 by owner decision, so the preview shows the
    /// character in its static imported pose; orbit/zoom interaction is
    /// unchanged.
    ///
    /// In play mode the camera renders the texture automatically every frame;
    /// a manual-rendering host can call <see cref="RenderOnce"/> itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterPreviewController : MonoBehaviour
    {
        [Header("Optional (created when empty)")]
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Light previewLight;

        private readonly List<CharacterDefinition> _characters = new List<CharacterDefinition>();

        private GameObject _instance;
        private RenderTexture _texture;
        private int _characterIndex = -1;
        private float _yaw = 28f;
        private float _pitch = 4f;
        private float _distance = 3.2f;
        private bool _manualRendering;
        private string _lastMessage = "not initialized";

        public RenderTexture OutputTexture => _texture;

        /// <summary>The preview camera (exposed so hosts can tune the clear colour).</summary>
        public Camera PreviewCamera => previewCamera;
        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public float Distance => _distance;
        public bool HasCharacter => _instance != null;
        public int CharacterCount => _characters.Count;
        public int CharacterIndex => _characterIndex;
        public string LastMessage => _lastMessage;
        public CharacterDefinition CurrentCharacter =>
            _characterIndex >= 0 && _characterIndex < _characters.Count ? _characters[_characterIndex] : null;

        public string CurrentCharacterName
        {
            get
            {
                var character = CurrentCharacter;
                return character != null ? character.displayName : "—";
            }
        }

        /// <summary>Skeleton / rig validation status of the selected character.</summary>
        public string RigStatusText
        {
            get
            {
                var character = CurrentCharacter;
                if (character == null)
                {
                    return "rig: no character";
                }

                var rig = character.humanoidRigValid ? "Humanoid avatar VALID" : "avatar NOT humanoid";
                return string.Format("rig: {0}\n{1}", rig, character.rigReport ?? string.Empty);
            }
        }

        /// <summary>Material / texture inspection summary of the selected character.</summary>
        public string MaterialStatusText
        {
            get
            {
                var character = CurrentCharacter;
                if (character == null)
                {
                    return "materials: no character";
                }

                return string.IsNullOrEmpty(character.materialReport)
                    ? "materials: no import report"
                    : character.materialReport;
            }
        }

        private void OnDestroy()
        {
            ClearInstance();
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
                _texture = null;
            }
        }

        /// <summary>
        /// Bind a configuration and build the preview scene objects
        /// (camera + light + RenderTexture). Safe to call again to rebind.
        /// </summary>
        public void Initialize(CharacterAnimationConfig config, int width = 512, int height = 640)
        {
            _characters.Clear();
            if (config != null && config.characters != null)
            {
                for (var i = 0; i < config.characters.Count; i++)
                {
                    var entry = config.characters[i];
                    if (entry != null && entry.model != null)
                    {
                        _characters.Add(entry);
                    }
                }
            }

            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                if (_texture != null)
                {
                    _texture.Release();
                    Destroy(_texture);
                }

                _texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
                {
                    name = "CharacterBayPreviewRT",
                };
                _texture.Create();
            }

            EnsureCameraAndLight();

            if (_characters.Count == 0)
            {
                _lastMessage = config == null
                    ? "No character configuration found. Build the character roster in the editor (Battle Horizons > Asset Management)."
                    : "No characters with models are configured yet.";
                ClearInstance();
                _characterIndex = -1;
                return;
            }

            var startIndex = Mathf.Clamp(
                Mathf.Max(0, _characterIndex), 0, _characters.Count - 1);
            SetCharacter(startIndex);
        }

        public void NextCharacter() => SetCharacter(_characterIndex + 1);

        public void PrevCharacter() => SetCharacter(_characterIndex - 1);

        public void SetCharacter(int index)
        {
            if (_characters.Count == 0)
            {
                return;
            }

            _characterIndex = ((index % _characters.Count) + _characters.Count) % _characters.Count;
            ClearInstance();

            var character = _characters[_characterIndex];
            _instance = Instantiate(character.model, transform);
            _instance.name = "CharacterPreviewInstance";
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;

            _lastMessage = character.displayName + " ready";
        }

        public void Rotate(float deltaYaw, float deltaPitch)
        {
            _yaw = Mathf.Repeat(_yaw + deltaYaw, 360f);
            _pitch = Mathf.Clamp(_pitch + deltaPitch, -40f, 60f);
        }

        /// <summary>Zoom in (negative delta) / out (positive delta), clamped.</summary>
        public void ZoomBy(float delta)
        {
            _distance = Mathf.Clamp(_distance + delta, 1.2f, 8f);
        }

        /// <summary>Host-driven rendering: force one camera render into the texture.</summary>
        public void RenderOnce()
        {
            if (previewCamera != null)
            {
                previewCamera.Render();
            }
        }

        public void SetManualRendering(bool manual)
        {
            _manualRendering = manual;
        }

        private void Update()
        {
            if (_manualRendering)
            {
                return;
            }

            UpdateCameraTransform();

            // In play mode nobody else renders the disabled camera, so draw
            // into the RenderTexture here.
            if (previewCamera != null && _texture != null)
            {
                previewCamera.Render();
            }
        }

        private void UpdateCameraTransform()
        {
            if (previewCamera == null)
            {
                return;
            }

            var pivot = transform.position + Vector3.up * 1.0f;
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            previewCamera.transform.position = pivot + rotation * (Vector3.back * _distance);
            previewCamera.transform.LookAt(pivot);
        }

        private void ClearInstance()
        {
            if (_instance != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_instance);
                }
                else
                {
                    DestroyImmediate(_instance);
                }

                _instance = null;
            }
        }

        private void EnsureCameraAndLight()
        {
            if (previewCamera == null)
            {
                var cameraObject = new GameObject("CharacterPreviewCamera");
                cameraObject.transform.SetParent(transform, false);
                previewCamera = cameraObject.AddComponent<Camera>();
                previewCamera.enabled = false; // rendered manually or via RT each frame
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = new Color(0.05f, 0.07f, 0.10f, 1f);
                previewCamera.fieldOfView = 35f;
                previewCamera.nearClipPlane = 0.05f;
            }

            previewCamera.targetTexture = _texture;

            if (previewLight == null)
            {
                var lightObject = new GameObject("CharacterPreviewLight");
                lightObject.transform.SetParent(transform, false);
                previewLight = lightObject.AddComponent<Light>();
                previewLight.type = LightType.Directional;
                previewLight.intensity = 1.1f;
                previewLight.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            }
        }
    }

    /// <summary>
    /// Forwards drag gestures on the preview viewport to the controller so the
    /// character can be rotated with touch or mouse. Added in code only.
    /// </summary>
    public sealed class CharacterPreviewDragHandler :
        MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        [SerializeField] private CharacterPreviewController controller;

        public void Bind(CharacterPreviewController target)
        {
            controller = target;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (controller != null)
            {
                controller.Rotate(eventData.delta.x * 0.35f, -eventData.delta.y * 0.25f);
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (controller != null)
            {
                controller.ZoomBy(-eventData.scrollDelta.y * 0.35f);
            }
        }
    }
}
