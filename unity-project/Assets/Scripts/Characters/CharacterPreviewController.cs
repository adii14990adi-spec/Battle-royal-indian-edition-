using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// Real-time character preview used by the in-game Character Bay modal and
    /// driven manually by the editor-time Character &amp; Animation Manager.
    /// Renders the selected character (real model, real generated controller,
    /// real imported clips — never placeholder motion) into a RenderTexture.
    ///
    /// In play mode the camera renders the texture automatically every frame;
    /// in edit mode the hosting window calls <see cref="Tick"/> and
    /// <see cref="RenderOnce"/> from EditorApplication.update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterPreviewController : MonoBehaviour
    {
        [Header("Optional (created when empty)")]
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Light previewLight;

        private CharacterAnimationConfig _config;
        private readonly List<CharacterDefinition> _characters = new List<CharacterDefinition>();
        private readonly List<AnimationClip> _clips = new List<AnimationClip>();
        private readonly List<string> _clipStateNames = new List<string>();

        private GameObject _instance;
        private Animator _animator;
        private RenderTexture _texture;
        private int _characterIndex = -1;
        private int _clipIndex = -1;
        private float _yaw = 28f;
        private float _pitch = 4f;
        private float _distance = 3.2f;
        private bool _playing = true;
        private bool _manualRendering;
        private string _lastMessage = "not initialized";

        public RenderTexture OutputTexture => _texture;
        public bool IsPlaying => _playing;
        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public float Distance => _distance;
        public bool HasCharacter => _instance != null;
        public Animator CharacterAnimator => _animator;
        public int CharacterCount => _characters.Count;
        public int CharacterIndex => _characterIndex;
        public int AnimationIndex => _clipIndex;
        public int AnimationCount => _clips.Count;
        public string LastMessage => _lastMessage;
        public CharacterDefinition CurrentCharacter =>
            _characterIndex >= 0 && _characterIndex < _characters.Count ? _characters[_characterIndex] : null;
        public AnimationClip CurrentClip =>
            _clipIndex >= 0 && _clipIndex < _clips.Count ? _clips[_clipIndex] : null;

        public string CurrentCharacterName
        {
            get
            {
                var character = CurrentCharacter;
                return character != null ? character.displayName : "—";
            }
        }

        public string CurrentAnimationName
        {
            get
            {
                var clip = CurrentClip;
                if (clip != null)
                {
                    return clip.name;
                }

                var character = CurrentCharacter;
                if (character == null)
                {
                    return "—";
                }

                return character.controller != null
                    ? "no clip selected"
                    : "assign & save to enable playback";
            }
        }

        public string CurrentAnimationInfo
        {
            get
            {
                var clip = CurrentClip;
                if (clip == null)
                {
                    return string.Empty;
                }

                return string.Format("{0:0.00}s · {1:0} fps · {2} · {3}",
                    clip.length, clip.frameRate,
                    clip.humanMotion ? "humanoid retargeted" : "generic",
                    clip.isLooping ? "loop" : "one-shot");
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
                var assignments = CountAssignedClips(character);
                return string.Format("rig: {0} · {1} assigned clip(s){2}\n{3}",
                    rig, assignments,
                    character.controller != null ? " · controller ready" : " · controller missing",
                    character.rigReport ?? string.Empty);
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
            _config = config;
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
                    ? "No character configuration found. Import a character and save assignments in the editor (Battle Horizons > Asset Management)."
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

            _animator = _instance.GetComponentInChildren<Animator>();
            if (_animator == null)
            {
                _lastMessage = character.displayName + ": model has no Animator";
                return;
            }

            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (character.controller != null)
            {
                _animator.runtimeAnimatorController = character.controller;
            }

            RebuildClipList(character);
            _clipIndex = _clips.Count > 0 ? 0 : -1;
            _lastMessage = character.controller != null
                ? character.displayName + " ready"
                : character.displayName + ": controller missing — assign animations and save to enable playback";
            ApplyAnimation(0, true);
        }

        public void NextAnimation() => SelectAnimation(_clipIndex + 1);

        public void PrevAnimation() => SelectAnimation(_clipIndex - 1);

        public void SelectAnimation(int index)
        {
            if (_clips.Count == 0)
            {
                return;
            }

            ApplyAnimation(index, false);
        }

        public void TogglePlayPause() => SetPlaying(!_playing);

        public void SetPlaying(bool playing)
        {
            _playing = playing;
            if (_animator != null)
            {
                _animator.speed = playing ? 1f : 0f;
            }
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

        /// <summary>Editor host: drive Animator evaluation manually in edit mode.</summary>
        public void Tick(float deltaTime)
        {
            if (_animator == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                _animator.Update(deltaTime);
            }
        }

        /// <summary>Editor host: force one camera render into the texture.</summary>
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
            // into the RenderTexture here (the editor window calls
            // RenderOnce() itself from its update loop).
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

        private int CountAssignedClips(CharacterDefinition character)
        {
            if (_config == null || character == null)
            {
                return 0;
            }

            var count = 0;
            var categories = CharacterAnimCategories.All;
            for (var i = 0; i < categories.Length; i++)
            {
                if (_config.FindAssignment(character.id, categories[i])?.clip != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void RebuildClipList(CharacterDefinition character)
        {
            _clips.Clear();
            _clipStateNames.Clear();

            // Prefer the assigned clips — those are what the game will play.
            if (_config == null || character == null)
            {
                return;
            }

            var categories = CharacterAnimCategories.All;
            for (var i = 0; i < categories.Length; i++)
            {
                var assignment = _config.FindAssignment(character.id, categories[i]);
                if (assignment != null && assignment.clip != null)
                {
                    _clips.Add(assignment.clip);
                    _clipStateNames.Add(StateNameForCategory(assignment.category));
                }
            }
        }

        private static string StateNameForCategory(CharacterAnimCategory category)
        {
            // CrouchWalk previews ride the Crouch blend tree (Speed is raised).
            return category == CharacterAnimCategory.CrouchWalk
                ? "Crouch"
                : category.ToString();
        }

        private void ApplyAnimation(int index, bool rebuildOnly)
        {
            if (_clips.Count == 0)
            {
                _clipIndex = -1;
                return;
            }

            _clipIndex = ((index % _clips.Count) + _clips.Count) % _clips.Count;
            if (rebuildOnly && _clipIndex != 0)
            {
                return;
            }

            var stateName = _clipStateNames[_clipIndex];
            if (_animator == null)
            {
                return;
            }

            if (_animator.runtimeAnimatorController == null)
            {
                _lastMessage = CurrentCharacterName + ": controller missing — cannot play clips";
                return;
            }

            // Rifle states live on the UpperBody layer; force the layer visible
            // for preview so the clip plays regardless of aiming input.
            if (_animator.layerCount > CharacterAnimParameters.UpperBodyLayerIndex)
            {
                var upper = stateName == "RifleIdle" || stateName == "RifleRun";
                _animator.SetLayerWeight(CharacterAnimParameters.UpperBodyLayerIndex, upper ? 1f : 0f);
            }

            _animator.Play(stateName, 0, 0f);
            _animator.Update(0f);
            _lastMessage = "playing " + _clips[_clipIndex].name + " as " + stateName;
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
                _animator = null;
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
