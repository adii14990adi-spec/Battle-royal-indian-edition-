using System;
using System.Collections.Generic;
using UnityEngine;
using BattleHorizons.Core;

namespace BattleHorizons.Gameplay.Match
{
    public enum SafeZoneState
    {
        /// <summary>Match not started; the island is fully open.</summary>
        Idle = 0,
        /// <summary>Holding the current circle, counting down to the next shrink.</summary>
        Waiting = 1,
        /// <summary>Interpolating from the current circle to the target circle.</summary>
        Shrinking = 2,
        /// <summary>Final circle reached; only damage ticks remain.</summary>
        Finished = 3,
    }

    /// <summary>
    /// The shrinking blue zone. Drives the circle schedule from
    /// <see cref="SafeZoneSettings"/>, moves the next centre with the match
    /// <see cref="RandomStream"/> (never UnityEngine.Random — rule 8), damages
    /// every tracked <see cref="IDamageable"/> outside the rim, and scales a
    /// greybox cylinder so the wall is visible on device.
    ///
    /// Wire-up: one controller per match scene, same matchSeed as
    /// LootSpawner/PlayerSpawnController. Register players with
    /// <see cref="AddTrackedPlayer"/> (the future health component implements
    /// IDamageable; until then the debug HUD can register a test dummy).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SafeZoneController : MonoBehaviour
    {
        private const string Tag = "safezone";
        public const string DamageSourceId = "safe_zone";

        [Header("Schedule")]
        [Tooltip("Empty = SafeZoneSettings.CreateDefault() (the greybox 6-phase schedule).")]
        [SerializeField] private SafeZoneSettings settings;
        [Tooltip("Match seed — share with LootSpawner/PlayerSpawnController.")]
        [SerializeField] private int matchSeed = 20261119;
        [SerializeField] private bool startOnEnable = true;

        [Header("Visual")]
        [Tooltip("Optional artist-made zone wall; scaled to (2r, height, 2r). When null a greybox cylinder is built.")]
        [SerializeField] private Transform zoneVisual;
        [SerializeField] private float visualHeightM = 300f;
        [SerializeField] private Color visualColor = new Color(0.25f, 0.55f, 1f, 0.35f);

        private readonly List<TrackedPlayer> _tracked = new List<TrackedPlayer>(8);

        private SafeZoneSettings _activeSettings;
        private RandomStream _rng;
        private ZoneCircle _current;
        private ZoneCircle _phaseStart;
        private ZoneCircle _target;
        private float _phaseTimer;
        private GameObject _builtVisual;

        private struct TrackedPlayer
        {
            public IDamageable Health;
            public Transform Transform;
        }

        /// <summary>Fired on every state/phase change: (phaseIndex, newState).</summary>
        public event Action<int, SafeZoneState> PhaseChanged;

        public SafeZoneState State { get; private set; } = SafeZoneState.Idle;
        public int PhaseIndex { get; private set; }
        public ZoneCircle CurrentCircle => _current;
        public ZoneCircle TargetCircle => _target;
        public SafeZoneSettings ActiveSettings => _activeSettings;

        /// <summary>Seconds left in the current wait/shrink step.</summary>
        public float SecondsRemaining => Mathf.Max(0f, _phaseTimer);

        public float CurrentDamagePerSecond
        {
            get
            {
                if (_activeSettings == null || State == SafeZoneState.Idle)
                {
                    return 0f;
                }

                var phase = _activeSettings.PhaseAt(PhaseIndex);
                return phase != null ? phase.damagePerSecond : _activeSettings.finalDamagePerSecond;
            }
        }

        private void OnEnable()
        {
            if (startOnEnable && State == SafeZoneState.Idle)
            {
                StartMatch();
            }
        }

        /// <summary>Resets and opens phase 0 with the full-island starting circle.</summary>
        public void StartMatch(int? seedOverride = null, SafeZoneSettings settingsOverride = null)
        {
            _activeSettings = settingsOverride != null ? settingsOverride
                : settings != null ? settings
                : SafeZoneSettings.CreateDefault();
            _rng = new RandomStream(seedOverride ?? matchSeed);

            _current = new ZoneCircle(Vector2.zero, _activeSettings.startRadiusM);
            _target = _current;
            PhaseIndex = 0;
            State = SafeZoneState.Idle;
            BeginPhase(0);

            EnsureVisual();
            UpdateVisual();
            GameLog.Info(Tag, $"match started, seed {_rng.Seed}, start radius {_current.Radius:F0} m");
        }

        /// <summary>Registers a player transform + health for outside-the-zone damage.</summary>
        public void AddTrackedPlayer(IDamageable health, Transform playerTransform)
        {
            if (health == null || playerTransform == null)
            {
                return;
            }

            RemoveTrackedPlayer(health);
            _tracked.Add(new TrackedPlayer { Health = health, Transform = playerTransform });
        }

        public void RemoveTrackedPlayer(IDamageable health)
        {
            for (var i = _tracked.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_tracked[i].Health, health))
                {
                    _tracked.RemoveAt(i);
                }
            }
        }

        public bool IsInsideSafeZone(Vector3 worldPosition) =>
            SafeZoneMath.Contains(_current, worldPosition.x, worldPosition.z);

        private void Update()
        {
            if (State == SafeZoneState.Idle)
            {
                return;
            }

            _phaseTimer -= Time.deltaTime;

            if (State == SafeZoneState.Shrinking)
            {
                var phase = _activeSettings.PhaseAt(PhaseIndex);
                var total = Mathf.Max(0.001f, phase.shrinkSeconds);
                var t = 1f - Mathf.Clamp01(_phaseTimer / total);
                _current = SafeZoneMath.Lerp(_phaseStart, _target, t);
                UpdateVisual();
            }

            ApplyZoneDamage(Time.deltaTime);

            if (_phaseTimer <= 0f)
            {
                Advance();
            }
        }

        private void BeginPhase(int index)
        {
            var phase = _activeSettings.PhaseAt(index);
            if (phase == null)
            {
                State = SafeZoneState.Finished;
                PhaseChanged?.Invoke(index, State);
                return;
            }

            PhaseIndex = index;
            _target = SafeZoneMath.NextCircle(_rng, _current, phase.radiusFactor);
            _phaseStart = _current;
            _phaseTimer = Mathf.Max(0f, phase.waitSeconds);
            State = SafeZoneState.Waiting;
            PhaseChanged?.Invoke(index, State);
        }

        private void Advance()
        {
            if (State == SafeZoneState.Waiting)
            {
                var phase = _activeSettings.PhaseAt(PhaseIndex);
                _phaseStart = _current;
                _phaseTimer = phase.shrinkSeconds;
                State = SafeZoneState.Shrinking;
                PhaseChanged?.Invoke(PhaseIndex, State);
                return;
            }

            if (State == SafeZoneState.Shrinking)
            {
                _current = _target;
                UpdateVisual();
                var next = PhaseIndex + 1;
                if (next >= _activeSettings.PhaseCount)
                {
                    State = SafeZoneState.Finished;
                    PhaseChanged?.Invoke(PhaseIndex, State);
                    GameLog.Info(Tag, $"final circle reached: r={_current.Radius:F1} m");
                    return;
                }

                BeginPhase(next);
            }
        }

        private void ApplyZoneDamage(float deltaTime)
        {
            var dps = CurrentDamagePerSecond;
            if (dps <= 0f || _tracked.Count == 0)
            {
                return;
            }

            var amount = dps * deltaTime;
            for (var i = _tracked.Count - 1; i >= 0; i--)
            {
                var entry = _tracked[i];
                if (entry.Health == null || entry.Transform == null)
                {
                    _tracked.RemoveAt(i);
                    continue;
                }

                if (!entry.Health.IsAlive)
                {
                    continue;
                }

                var position = entry.Transform.position;
                if (!SafeZoneMath.Contains(_current, position.x, position.z))
                {
                    entry.Health.TakeDamage(amount, DamageSourceId);
                }
            }
        }

        private void EnsureVisual()
        {
            if (zoneVisual != null)
            {
                return;
            }

            _builtVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _builtVisual.name = "SafeZoneGreybox";
            var collider = _builtVisual.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            if (_builtVisual.TryGetComponent(out Renderer renderer))
            {
                renderer.material.color = visualColor;
            }

            _builtVisual.transform.SetParent(transform, false);
            zoneVisual = _builtVisual.transform;
        }

        private void UpdateVisual()
        {
            if (zoneVisual == null)
            {
                return;
            }

            var diameter = Mathf.Max(1f, _current.Radius * 2f);
            zoneVisual.localScale = new Vector3(diameter, visualHeightM * 0.5f, diameter);
            zoneVisual.position = new Vector3(_current.Center.x, visualHeightM * 0.5f, _current.Center.y);
        }

        private void OnDestroy()
        {
            if (_builtVisual != null)
            {
                Destroy(_builtVisual);
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawCircleGizmo(_current, new Color(0.3f, 0.6f, 1f, 0.9f));
            DrawCircleGizmo(_target, new Color(1f, 1f, 1f, 0.6f));
        }

        private static void DrawCircleGizmo(ZoneCircle circle, Color color)
        {
            if (circle.Radius <= 0f)
            {
                return;
            }

            Gizmos.color = color;
            const int segments = 64;
            var previous = new Vector3(circle.Center.x + circle.Radius, 1f, circle.Center.y);
            for (var i = 1; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var next = new Vector3(
                    circle.Center.x + Mathf.Cos(angle) * circle.Radius,
                    1f,
                    circle.Center.y + Mathf.Sin(angle) * circle.Radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
