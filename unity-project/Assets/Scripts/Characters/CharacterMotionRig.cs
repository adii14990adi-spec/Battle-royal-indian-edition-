using UnityEngine;
using BattleHorizons.CameraRig;
using BattleHorizons.Core;
using BattleHorizons.Gameplay.Combat;
using BattleHorizons.Player;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// Wires the motion stack onto whichever character the bay spawns.
    ///
    /// The IK components only receive OnAnimatorIK when they sit on the
    /// Animator's GameObject, and <see cref="PlayerCharacterVisual"/> can
    /// (re)create that instance at any time — so this orchestrator watches the
    /// visual, and when an Animator appears it attaches <see cref="FootIK"/>,
    /// <see cref="WeaponHandIK"/>, <see cref="HeadLookIK"/> and
    /// <see cref="ProceduralLean"/> to it, binds the camera/weapon/controller
    /// references, and re-attaches automatically after a character swap.
    /// Put one on the player root next to PlayerCharacterVisual.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterMotionRig : MonoBehaviour
    {
        private const string Tag = "motion";

        [Header("References (auto-found once in Start when empty)")]
        [SerializeField] private PlayerCharacterVisual characterVisual;
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private ThirdPersonCameraController cameraRig;
        [SerializeField] private PlayerController playerController;

        private Animator _attached;
        private FootIK _footIk;
        private WeaponHandIK _handIk;
        private HeadLookIK _headLook;

        public Animator AttachedAnimator => _attached;

        private void Start()
        {
            if (characterVisual == null)
            {
                characterVisual = GetComponentInChildren<PlayerCharacterVisual>();
            }

            if (weaponController == null)
            {
                weaponController = GetComponent<WeaponController>();
            }

            if (cameraRig == null)
            {
                cameraRig = FindFirstObjectByType<ThirdPersonCameraController>();
            }

            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }
        }

        private void Update()
        {
            var animator = characterVisual != null ? characterVisual.CharacterAnimator : null;
            if (animator == _attached)
            {
                PushFrameState();
                return;
            }

            if (_attached != null)
            {
                GameLog.Info(Tag, "character instance changed — re-attaching motion components");
            }

            Attach(animator);
        }

        private void Attach(Animator animator)
        {
            _attached = animator;
            _footIk = null;
            _handIk = null;
            _headLook = null;
            if (animator == null)
            {
                return;
            }

            var host = animator.gameObject;

            _footIk = host.GetComponent<FootIK>();
            if (_footIk == null)
            {
                _footIk = host.AddComponent<FootIK>();
            }

            _handIk = host.GetComponent<WeaponHandIK>();
            if (_handIk == null)
            {
                _handIk = host.AddComponent<WeaponHandIK>();
            }

            _headLook = host.GetComponent<HeadLookIK>();
            if (_headLook == null)
            {
                _headLook = host.AddComponent<HeadLookIK>();
            }

            if (host.GetComponent<ProceduralLean>() == null)
            {
                host.AddComponent<ProceduralLean>();
            }

            if (playerController != null && playerController.TryGetComponent(out CharacterController controller))
            {
                _footIk.Bind(controller);
            }

            if (cameraRig != null)
            {
                _headLook.Bind(cameraRig.transform);
            }

            _handIk.Bind(weaponController != null ? weaponController.WeaponMount : null);

            // Anti-slide driver lives on the player root (CharacterController GO).
            var locomotion = playerController != null ? playerController.GetComponent<PlayerLocomotion>() : null;
            if (playerController != null && locomotion == null)
            {
                locomotion = playerController.gameObject.AddComponent<PlayerLocomotion>();
                locomotion.Bind(animator, playerController.GetComponent<CharacterController>());
            }

            GameLog.Info(Tag, $"motion rig attached to '{host.name}' (foot/hand/look IK + procedural lean + anti-slide locomotion)");
        }

        private void PushFrameState()
        {
            if (_handIk != null && weaponController != null)
            {
                _handIk.SetWeaponState(weaponController.HasWeapon, weaponController.IsAiming);
            }

            if (_headLook != null)
            {
                _headLook.SetAiming(weaponController != null && weaponController.IsAiming);
            }
        }
    }
}
