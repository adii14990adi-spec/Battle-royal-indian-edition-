using System.Collections.Generic;
using UnityEngine;
using BattleHorizons.Core;

namespace BattleHorizons.Gameplay.Loot
{
    /// <summary>
    /// Spawns loot inside every registered <see cref="LootContainer"/> using a
    /// seeded <see cref="RandomStream"/>: same config seed + same scene = byte
    /// identical loot layout, which is what validate_csharp.py rule 8 and the
    /// replay tooling require.
    ///
    /// Tier logic lives in <see cref="LootSpawnConfig"/>: a container resolves
    /// its Siran zone (SiranMapConfig.ZoneAt), the zone rule caps the tier
    /// (Tier 3 = Military Base only) and picks the loot table.
    ///
    /// Greybox mode: when an item has no worldPrefab, a small tier-coloured
    /// cube is built at runtime so the loop is playable before art exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootSpawner : MonoBehaviour
    {
        private const string Tag = "loot";

        [Header("Configuration")]
        [Tooltip("Leave empty to use DefaultLootContent.CreateConfig() (the code-authored starter set).")]
        [SerializeField] private LootSpawnConfig config;
        [Tooltip("Optional prefab override for every pickup (must carry a LootPickup component).")]
        [SerializeField] private GameObject pickupPrefab;
        [Tooltip("Parent for spawned pickups; created when null.")]
        [SerializeField] private Transform pickupRoot;
        [SerializeField] private bool spawnOnStart = true;

        private readonly List<LootPickup> _spawned = new List<LootPickup>(512);
        private LootSpawnConfig _activeConfig;
        private GameObject _fallbackPrototype;

        public int SpawnedCount => _spawned.Count;
        public LootSpawnConfig ActiveConfig => _activeConfig;

        private void Start()
        {
            if (spawnOnStart)
            {
                SpawnAll();
            }
        }

        /// <summary>Clears previous loot and re-rolls every container from the config seed.</summary>
        public void SpawnAll(int? seedOverride = null)
        {
            ClearAll();

            _activeConfig = config != null ? config : DefaultLootContent.CreateConfig();
            var rng = new RandomStream(seedOverride ?? _activeConfig.seed);
            EnsurePickupRoot();

            var containers = LootContainer.Registry;
            for (var i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                if (container != null)
                {
                    SpawnInContainer(container, rng);
                }
            }

            GameLog.Info(Tag, $"spawned {_spawned.Count} pickups in {containers.Count} containers (seed {rng.Seed})");
        }

        /// <summary>Rolls and instantiates the loot for one container.</summary>
        public int SpawnInContainer(LootContainer container, RandomStream rng)
        {
            if (container == null || rng == null)
            {
                return 0;
            }

            EnsureConfig();
            var rule = _activeConfig.RuleFor(container.ZoneId);
            var maxTier = rule != null ? rule.maxTier : _activeConfig.defaultMaxTier;
            var table = rule != null && rule.table != null ? rule.table : _activeConfig.defaultTable;
            if (table == null)
            {
                GameLog.Warn(Tag, $"no loot table for zone '{container.ZoneId}' — container {container.name} skipped");
                return 0;
            }

            var minDrops = rule != null ? rule.minDropsPerContainer : 1;
            var maxDrops = rule != null ? rule.maxDropsPerContainer : 2;
            var drops = container.FixedDropCount > 0
                ? container.FixedDropCount
                : rng.Range(Mathf.Min(minDrops, maxDrops), Mathf.Max(minDrops, maxDrops));

            var allowed = rule != null && rule.allowedCategories != null && rule.allowedCategories.Length > 0
                ? new HashSet<ItemCategory>(rule.allowedCategories)
                : null;

            var bounds = container.WorldBounds;
            var placed = 0;
            for (var i = 0; i < drops; i++)
            {
                var roll = table.RollOne(rng, maxTier, allowed);
                if (roll.Item == null)
                {
                    continue;
                }

                var position = new Vector3(
                    rng.Range(bounds.min.x, bounds.max.x),
                    bounds.min.y + 0.25f,
                    rng.Range(bounds.min.z, bounds.max.z));
                _spawned.Add(CreatePickup(roll.Item, roll.Count, position));
                placed++;
            }

            return placed;
        }

        /// <summary>Destroys every pickup this spawner created.</summary>
        public void ClearAll()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Destroy(_spawned[i].gameObject);
                }
            }

            _spawned.Clear();
        }

        private void EnsureConfig()
        {
            _activeConfig ??= config != null ? config : DefaultLootContent.CreateConfig();
        }

        private void EnsurePickupRoot()
        {
            if (pickupRoot != null)
            {
                return;
            }

            pickupRoot = new GameObject("LootPickups").transform;
            pickupRoot.SetParent(transform, false);
        }

        private LootPickup CreatePickup(ItemDefinition item, int count, Vector3 position)
        {
            GameObject instance;
            if (item.worldPrefab != null)
            {
                instance = Instantiate(item.worldPrefab, position, Quaternion.identity, pickupRoot);
            }
            else if (pickupPrefab != null)
            {
                instance = Instantiate(pickupPrefab, position, Quaternion.identity, pickupRoot);
            }
            else
            {
                instance = Instantiate(FallbackPrototype(), position, Quaternion.identity, pickupRoot);
                instance.name = "Pickup_" + item.itemId;
                instance.SetActive(true);
                if (instance.TryGetComponent(out Renderer renderer))
                {
                    renderer.material.color = ItemDefinition.TierColor(item.tier);
                }
            }

            var pickup = instance.GetComponent<LootPickup>();
            if (pickup == null)
            {
                pickup = instance.AddComponent<LootPickup>();
            }

            pickup.Configure(item, count);
            return pickup;
        }

        /// <summary>
        /// Greybox cube prototype built once: trigger collider, LootPickup and a
        /// material whose colour is set per tier on instantiation.
        /// </summary>
        private GameObject FallbackPrototype()
        {
            if (_fallbackPrototype != null)
            {
                return _fallbackPrototype;
            }

            _fallbackPrototype = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _fallbackPrototype.name = "LootPickupPrototype";
            _fallbackPrototype.transform.localScale = Vector3.one * 0.35f;

            var collider = _fallbackPrototype.GetComponent<Collider>();
            collider.isTrigger = true;
            _fallbackPrototype.AddComponent<LootPickup>();
            _fallbackPrototype.SetActive(false);
            return _fallbackPrototype;
        }

        private void OnDestroy()
        {
            if (_fallbackPrototype != null)
            {
                Destroy(_fallbackPrototype);
            }
        }
    }
}
