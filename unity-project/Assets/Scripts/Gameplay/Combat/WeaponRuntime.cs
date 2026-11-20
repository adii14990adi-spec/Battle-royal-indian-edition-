using UnityEngine;

namespace BattleHorizons.Gameplay.Combat
{
    /// <summary>
    /// Pure trigger/magazine/reload state machine — no MonoBehaviour, no
    /// Time.deltaTime, no UnityEngine.Random. Callers pass the clock in, which
    /// makes fire rate, semi-auto edge detection and reload timing exactly
    /// testable in EditMode (see CombatSystemsTests) and reusable later by the
    /// server-authoritative match code.
    /// </summary>
    public sealed class WeaponRuntime
    {
        private readonly WeaponData _data;
        private float _nextShotTime;
        private float _reloadEndTime;
        private bool _triggerWasHeld;

        public WeaponRuntime(WeaponData data)
        {
            _data = data;
            MagAmmo = data != null ? data.magazineSize : 0;
            ReserveAmmo = data != null ? data.startingReserve : 0;
        }

        public WeaponData Data => _data;
        public int MagAmmo { get; private set; }
        public int ReserveAmmo { get; private set; }
        public bool IsReloading { get; private set; }
        public float ReloadEndTime => _reloadEndTime;

        /// <summary>0..1 progress of the running reload (1 when not reloading).</summary>
        public float ReloadProgress =>
            !IsReloading || _data == null
                ? 1f
                : Mathf.Clamp01(1f - (_reloadEndTime - _lastTime) / Mathf.Max(0.001f, _data.reloadSeconds));

        // The clock is injected via Tick; between ticks the last observed time
        // keeps ReloadProgress honest for the HUD.
        private float _lastTime;

        /// <summary>
        /// Advances the weapon to <paramref name="time"/> (seconds, e.g.
        /// Time.time) with the trigger state, and returns how many shots fired
        /// this tick (0 or 1 for semi/auto alike — tick at frame rate).
        /// An empty mag with a held trigger auto-starts the reload.
        /// </summary>
        public int Tick(float time, bool triggerHeld)
        {
            _lastTime = time;

            if (IsReloading && time >= _reloadEndTime)
            {
                FinishReload();
            }

            if (IsReloading || _data == null)
            {
                _triggerWasHeld = triggerHeld;
                return 0;
            }

            var fired = 0;
            var triggerEdge = triggerHeld && (!_triggerWasHeld || _data.fireMode == FireMode.Auto);
            if (triggerEdge && time >= _nextShotTime)
            {
                if (MagAmmo > 0)
                {
                    MagAmmo--;
                    _nextShotTime = time + _data.SecondsPerShot;
                    fired = 1;
                }
                else if (triggerHeld)
                {
                    StartReload(time);
                }
            }

            _triggerWasHeld = triggerHeld;
            return fired;
        }

        /// <summary>Begin reloading; false when already full, reloading, or out of reserve.</summary>
        public bool StartReload(float time)
        {
            if (_data == null || IsReloading || MagAmmo >= _data.magazineSize || ReserveAmmo <= 0)
            {
                return false;
            }

            IsReloading = true;
            _reloadEndTime = time + _data.reloadSeconds;
            return true;
        }

        /// <summary>Adds loose ammo to the reserve (pickup/loot system hook).</summary>
        public void AddReserve(int amount)
        {
            if (_data == null || amount <= 0)
            {
                return;
            }

            ReserveAmmo = Mathf.Min(_data.maxReserve, ReserveAmmo + amount);
        }

        private void FinishReload()
        {
            var need = _data.magazineSize - MagAmmo;
            var moved = Mathf.Min(need, ReserveAmmo);
            MagAmmo += moved;
            ReserveAmmo -= moved;
            IsReloading = false;
        }
    }
}
