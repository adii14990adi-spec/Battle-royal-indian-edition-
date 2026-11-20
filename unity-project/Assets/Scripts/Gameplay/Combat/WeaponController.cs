using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using BattleHorizons.CameraRig;
using BattleHorizons.Characters;
using BattleHorizons.Core;
using BattleHorizons.Gameplay.Loot;
using BattleHorizons.Gameplay.Match;
using BattleHorizons.Player;

namespace BattleHorizons.Gameplay.Combat
{
    /// <summary>
    /// Player-side shooting: equip from loot, ADS with a 60° → 45° FOV pull-in,
    /// hitscan fire with spread, procedural recoil into the orbit camera, and
    /// placeholder muzzle/audio/tracer FX.
    ///
    /// Contract with the rest of the project:
    ///   * input comes from <see cref="PlayerInputRouter"/> (AimHeld/FireHeld —
    ///     touch FIRE button, mouse, or gamepad triggers), never from devices directly;
    ///   * weapons arrive through the static <see cref="LootPickup.Collected"/>
    ///     event; itemId → <see cref="WeaponData"/> via the loadout dictionary
    ///     (scene overrides + <see cref="DefaultWeaponContent"/>);
    ///   * damage lands on <see cref="IDamageable"/> found in the hit collider's
    ///     parents, source id = weaponId (kill-feed ready);
    ///   * animation hooks call <see cref="PlayerCharacterVisual"/>.SetAiming /
    ///     TriggerShoot / TriggerReload when a character visual is present —
    ///     the greybox capsule simply has none;
    ///   * randomness (spread, recoil jitter) flows through seeded
    ///     <see cref="RandomStream"/>s — validator rule 8, no UnityEngine.Random.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponController : MonoBehaviour
    {
        private const string Tag = "weapon";

        [Header("References (auto-found once in Start when left empty)")]
        [SerializeField] private PlayerInputRouter inputRouter;
        [SerializeField] private ThirdPersonCameraController cameraRig;
        [SerializeField] private PlayerCharacterVisual characterVisual;
        [Tooltip("Where the gun model will hang (empty child of the character). Muzzle FX parent to this.")]
        [SerializeField] private Transform weaponMount;
        [Tooltip("Exact muzzle tip; created on the mount/camera when null.")]
        [SerializeField] private Transform muzzlePoint;

        [Header("Loadout")]
        [Tooltip("Scene-authored stats; entries win over DefaultWeaponContent for the same weaponId.")]
        [SerializeField] private WeaponData[] weaponOverrides = Array.Empty<WeaponData>();
        [SerializeField] private string startWeaponId = DefaultWeaponContent.StarterWeaponId;
        [Tooltip("Seed for spread + recoil jitter streams (match-reproducible).")]
        [SerializeField] private int fireSeed = 1101;

        [Header("Camera / hitscan")]
        [Tooltip("Hip-fire FOV; ADS targets WeaponData.adsFovDeg (45 for the rifle, 30 for the sniper).")]
        [SerializeField] private float hipFovDeg = 60f;
        [SerializeField] private LayerMask hitscanMask = ~0;
        [SerializeField] private bool usePlaceholderFx = true;

        private readonly Dictionary<string, WeaponData> _loadout = new Dictionary<string, WeaponData>(8);

        private Camera _camera;
        private WeaponRuntime _runtime;
        private RecoilModel _recoil;
        private RandomStream _spreadRng;
        private AudioSource _audio;
        private ParticleSystem _muzzleFlash;
        private bool _aiming;
        private bool _wasAiming;
        private int _lastAmmoMag = -1;
        private int _lastAmmoReserve = -1;

        public WeaponData CurrentWeapon => _runtime?.Data;
        public bool HasWeapon => _runtime != null && _runtime.Data != null;
        public bool IsAiming => _aiming;
        public int MagAmmo => _runtime?.MagAmmo ?? 0;
        public int ReserveAmmo => _runtime?.ReserveAmmo ?? 0;
        public bool IsReloading => _runtime != null && _runtime.IsReloading;

        /// <summary>(weapon, magAmmo, reserveAmmo) after every shot/reload/pickup — HUD subscribes.</summary>
        public event Action<WeaponData, int, int> AmmoChanged;

        /// <summary>Fired after a successful equip — HUD/crosshair subscribes.</summary>
        public event Action<WeaponData> WeaponEquipped;

        private void OnEnable()
        {
            LootPickup.Collected += OnLootCollected;
        }

        private void OnDisable()
        {
            LootPickup.Collected -= OnLootCollected;
        }

        private void Start()
        {
            if (inputRouter == null)
            {
                inputRouter = FindFirstObjectByType<PlayerInputRouter>();
            }

            if (cameraRig == null)
            {
                cameraRig = FindFirstObjectByType<ThirdPersonCameraController>();
            }

            if (characterVisual == null)
            {
                characterVisual = GetComponentInChildren<PlayerCharacterVisual>();
            }

            if (cameraRig != null)
            {
                _camera = cameraRig.GetComponent<Camera>();
            }

            foreach (var data in DefaultWeaponContent.CreateAll())
            {
                _loadout[data.Key] = data.Value;
            }

            if (weaponOverrides != null)
            {
                for (var i = 0; i < weaponOverrides.Length; i++)
                {
                    if (weaponOverrides[i] != null && !string.IsNullOrEmpty(weaponOverrides[i].weaponId))
                    {
                        _loadout[weaponOverrides[i].weaponId] = weaponOverrides[i];
                    }
                }
            }

            _recoil = new RecoilModel(new RandomStream(fireSeed));
            _spreadRng = new RandomStream(unchecked(fireSeed * 7 + 13));
            _audio = GetComponent<AudioSource>();
            if (_audio == null && usePlaceholderFx)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
            }

            if (!string.IsNullOrEmpty(startWeaponId) && _loadout.TryGetValue(startWeaponId, out var starter))
            {
                Equip(starter);
            }
        }

        /// <summary>Swaps to a weapon; ammo comes from its WeaponData (mag + starting reserve).</summary>
        public void Equip(WeaponData data)
        {
            if (data == null)
            {
                return;
            }

            _runtime = new WeaponRuntime(data);
            _recoil.Reset();
            EnsureMuzzlePoint();
            if (usePlaceholderFx && _muzzleFlash == null && _muzzlePoint != null)
            {
                _muzzleFlash = CombatPlaceholderFx.BuildMuzzleFlash(_muzzlePoint);
            }

            GameLog.Info(Tag, $"equipped {data.displayName} ({data.tier}, {data.fireMode}) mag {MagAmmo}/{ReserveAmmo}");
            WeaponEquipped?.Invoke(data);
            AnnounceAmmo(true);
        }

        /// <summary>Equip by loot itemId — used by the pickup event and debug keys.</summary>
        public bool EquipById(string itemId)
        {
            if (_loadout.TryGetValue(itemId, out var data))
            {
                Equip(data);
                return true;
            }

            return false;
        }

        public void RequestReload()
        {
            if (_runtime != null && _runtime.StartReload(Time.time))
            {
                characterVisual?.TriggerReload();
                AnnounceAmmo(true);
            }
        }

        private void OnLootCollected(LootPickup pickup)
        {
            var item = pickup != null ? pickup.Item : null;
            if (item == null)
            {
                return;
            }

            if (item.category == ItemCategory.Weapon)
            {
                EquipById(item.itemId);
                return;
            }

            if (item.category == ItemCategory.Ammo && _runtime != null)
            {
                _runtime.AddReserve(pickup.Count);
                AnnounceAmmo(true);
            }
        }

        private void Update()
        {
            if (!HasWeapon)
            {
                return;
            }

            var dt = Time.deltaTime;
            _aiming = inputRouter != null && inputRouter.AimHeld;

            if (_aiming != _wasAiming)
            {
                characterVisual?.SetAiming(_aiming);
                _wasAiming = _aiming;
            }

            UpdateAdsFov(dt);

            // Recoil recovery returns part of the accumulated kick to the camera.
            var recovery = _recoil.Recover(_runtime.Data, dt);
            if (cameraRig != null && (recovery.x != 0f || recovery.y != 0f))
            {
                cameraRig.AddLook(new Vector2(-recovery.x, recovery.y));
            }

            var fireHeld = inputRouter != null && inputRouter.FireHeld;
            var shots = _runtime.Tick(Time.time, fireHeld);
            for (var i = 0; i < shots; i++)
            {
                FireShot();
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                RequestReload();
            }

            AnnounceAmmo(false);
        }

        private void UpdateAdsFov(float deltaTime)
        {
            if (_camera == null)
            {
                return;
            }

            var data = _runtime.Data;
            var target = _aiming ? data.adsFovDeg : hipFovDeg;
            var seconds = Mathf.Max(0.05f, _aiming ? data.adsEnterSeconds : data.adsExitSeconds);
            var step = Mathf.Abs(hipFovDeg - data.adsFovDeg) / seconds * deltaTime;
            _camera.fieldOfView = Mathf.MoveTowards(_camera.fieldOfView, target, step);
        }

        private void FireShot()
        {
            var data = _runtime.Data;

            // Hitscan from the crosshair (camera centre), with seeded cone spread.
            var ray = _camera != null
                ? _camera.ViewportPointToRay(0.5f, 0.5f)
                : new Ray(transform.position + Vector3.up * 1.5f, transform.forward);
            var spread = _aiming ? data.adsSpreadDeg : data.hipSpreadDeg;
            if (spread > 0f)
            {
                var right = _camera != null ? _camera.transform.right : transform.right;
                var up = _camera != null ? _camera.transform.up : transform.up;
                ray.direction = Quaternion.AngleAxis(_spreadRng.Range(-spread, spread), up)
                    * Quaternion.AngleAxis(_spreadRng.Range(-spread, spread), right)
                    * ray.direction.normalized;
            }

            var endPoint = ray.origin + ray.direction.normalized * data.rangeM;
            if (Physics.Raycast(ray, out var hit, data.rangeM, hitscanMask, QueryTriggerInteraction.Ignore)
                && hit.collider.transform.root != transform.root)
            {
                endPoint = hit.point;
                var target = hit.collider.GetComponentInParent<IDamageable>();
                target?.TakeDamage(data.damage, data.weaponId);
            }

            // Camera kick: AddLook pitch convention is `pitch -= delta.y`, so an
            // upward kick sends a negative y and recovery sends it back positive.
            var kick = _recoil.ApplyShot(data, _aiming);
            if (cameraRig != null)
            {
                cameraRig.AddLook(new Vector2(kick.x, -kick.y));
            }

            if (usePlaceholderFx)
            {
                if (_muzzleFlash != null)
                {
                    _muzzleFlash.Play();
                }

                if (_audio != null)
                {
                    _audio.PlayOneShot(CombatPlaceholderFx.GunshotClip());
                }

                CombatPlaceholderFx.SpawnTracer(MuzzleWorldPosition(), endPoint);
            }

            characterVisual?.TriggerShoot();
            AnnounceAmmo(true);
        }

        private Vector3 MuzzleWorldPosition() =>
            muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 1.4f;

        private void EnsureMuzzlePoint()
        {
            if (muzzlePoint != null)
            {
                return;
            }

            var host = weaponMount != null ? weaponMount : cameraRig != null ? cameraRig.transform : transform;
            var go = new GameObject("MuzzlePoint");
            go.transform.SetParent(host, false);
            // Greybox: right of the orbit camera, like a shoulder-held rifle.
            go.transform.localPosition = weaponMount != null
                ? new Vector3(0f, 0f, 0.4f)
                : new Vector3(0.35f, -0.25f, 0.6f);
            muzzlePoint = go.transform;
        }

        private void AnnounceAmmo(bool force)
        {
            if (!HasWeapon)
            {
                return;
            }

            if (!force && MagAmmo == _lastAmmoMag && ReserveAmmo == _lastAmmoReserve)
            {
                return;
            }

            _lastAmmoMag = MagAmmo;
            _lastAmmoReserve = ReserveAmmo;
            AmmoChanged?.Invoke(_runtime.Data, MagAmmo, ReserveAmmo);
        }
    }
}
